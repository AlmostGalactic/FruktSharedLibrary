using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.UI;
using MelonLoader;
using MelonLoader.Resolver;

namespace FruktSharedLibrary.Internal
{
    internal enum LibraryModState
    {
        /// <summary>Started normally.</summary>
        Running,
        /// <summary>The player switched it off in the mod menu.</summary>
        TurnedOff,
        /// <summary>Uses parts of the library this version doesn't have, so it wasn't started.</summary>
        NeedsNewerLibrary,
        /// <summary>MelonLoader didn't start it (a missing dependency, or it crashed while loading).</summary>
        DidNotStart,
    }

    /// <summary>One installed mod that uses the library.</summary>
    internal sealed class LibraryModInfo
    {
        internal MelonAssembly Assembly;
        internal MelonBase Melon;
        internal string Name;
        internal string Author;
        internal string Version;
        internal Version BuiltAgainst;
        internal List<string> Missing = new();
        internal LibraryModState State;

        /// <summary>Whether the player wants it on (takes effect the next time the game starts).</summary>
        internal bool WantsOn => !FruktConfig.DisabledMods.Contains(Name, StringComparer.OrdinalIgnoreCase);

        /// <summary>True when the switch in the mod menu no longer matches what's running.</summary>
        internal bool RestartNeeded => (State == LibraryModState.Running && !WantsOn) || (State == LibraryModState.TurnedOff && WantsOn);
    }

    /// <summary>
    /// Checks every mod that uses the library before it starts. Mods the player switched off, and mods that use
    /// parts of the library this version doesn't have, aren't started at all: MelonLoader is stopped from
    /// registering them, so none of their code runs and nothing half-works. The player is told when a map opens.
    /// </summary>
    internal static class ModGuard
    {
        private static readonly List<LibraryModInfo> Mods = new();
        private static bool _announced;

        /// <summary>Every installed mod that uses the library, including the ones that weren't started.</summary>
        internal static IReadOnlyList<LibraryModInfo> All => Mods;

        internal static LibraryModInfo Find(MelonBase melon)
            => melon == null ? null : Mods.Find(m => m.Melon == melon || m.Assembly == melon.MelonAssembly);

        /// <summary>
        /// Runs while MelonLoader registers the library, which is before any mod that depends on it: every mod
        /// assembly is loaded by then, but none of them is registered yet.
        /// </summary>
        internal static void Initialize()
        {
            // A mod built against a newer library asks for that version; give it this one so it can be checked
            // (and reported) instead of failing to load with a confusing error.
            MelonAssemblyResolver.OnAssemblyResolve += (name, version) =>
                name == ModCompatibility.LibraryName ? typeof(ModGuard).Assembly : null;

            foreach (var assembly in MelonAssembly.LoadedAssemblies)
            {
                if (assembly?.Assembly == null || assembly.Assembly == typeof(ModGuard).Assembly)
                    continue;
                try
                {
                    var info = Check(assembly);
                    if (info != null)
                        Mods.Add(info);
                }
                catch (Exception e)
                {
                    FruktLog.Warning($"Checking '{assembly.Location}' against the library failed: {e.Message}");
                }
            }

            foreach (var mod in Mods)
            {
                if (mod.State != LibraryModState.Running)
                    Hold(mod);
            }
        }

        // MelonLoader's own methods can't be Harmony-patched (its assembly is marked [PatchShield]), so a mod is
        // kept from starting another way: MelonBase.Register() returns straight away, before subscribing anything,
        // for a melon already marked Registered. The mark is taken off again once registration is over, so the mod
        // ends up simply never registered.
        private static readonly PropertyInfo RegisteredProperty = typeof(MelonBase).GetProperty(nameof(MelonBase.Registered));
        private static readonly List<MelonBase> Held = new();

        private static void Hold(LibraryModInfo mod)
        {
            FruktLog.Msg(mod.State == LibraryModState.TurnedOff
                ? $"Not starting {mod.Name}: it's switched off in the mod menu."
                : $"Not starting {mod.Name}: {Problem(mod)}");
            foreach (var melon in mod.Assembly.LoadedMelons)
            {
                if (melon.Registered)
                    continue; // Already running (a plugin, which starts before the library); nothing to stop.
                try
                {
                    RegisteredProperty.SetValue(melon, true);
                    Held.Add(melon);
                }
                catch (Exception e)
                {
                    FruktLog.Error($"Couldn't stop {mod.Name} from starting", e);
                }
            }
        }

        /// <summary>Runs once every mod is registered: finds the ones MelonLoader didn't start for its own reasons.</summary>
        internal static void AfterStartup()
        {
            foreach (var melon in Held)
            {
                try
                {
                    RegisteredProperty.SetValue(melon, false);
                }
                catch (Exception e)
                {
                    FruktLog.Error($"Couldn't release '{melon.Info?.Name}'", e);
                }
            }
            Held.Clear();
            foreach (var mod in Mods)
            {
                if (mod.State == LibraryModState.Running && (mod.Melon == null || !mod.Melon.Registered))
                    mod.State = LibraryModState.DidNotStart;
            }
            int running = Mods.FindAll(m => m.State == LibraryModState.Running).Count;
            FruktLog.Msg($"{Mods.Count} mod(s) use the library; {running} started.");
            GameEvents.SandboxReady += _ => Announce();
        }

        private static LibraryModInfo Check(MelonAssembly assembly)
        {
            var builtAgainst = ModCompatibility.ReferencedVersion(assembly.Assembly);
            if (builtAgainst == null)
                return null;
            MelonBase melon = assembly.LoadedMelons.Count > 0 ? assembly.LoadedMelons[0] : null;
            var info = new LibraryModInfo
            {
                Assembly = assembly,
                Melon = melon,
                Name = melon?.Info?.Name ?? assembly.Assembly.GetName().Name,
                Author = melon?.Info?.Author,
                Version = melon?.Info?.Version,
                BuiltAgainst = builtAgainst,
            };
            if (!info.WantsOn)
            {
                info.State = LibraryModState.TurnedOff;
                return info;
            }
            info.Missing = ModCompatibility.FindMissing(assembly.Assembly, assembly.Location);
            info.State = info.Missing.Count > 0 ? LibraryModState.NeedsNewerLibrary : LibraryModState.Running;
            if (info.State == LibraryModState.NeedsNewerLibrary)
                FruktLog.Warning($"{info.Name} uses parts of the library this version doesn't have: {string.Join(", ", info.Missing)}");
            return info;
        }

        /// <summary>One sentence saying why a mod isn't running, for the log, notifications and the mod menu.</summary>
        internal static string Problem(LibraryModInfo mod)
        {
            var current = typeof(ModGuard).Assembly.GetName().Version;
            switch (mod.State)
            {
                case LibraryModState.NeedsNewerLibrary:
                    return mod.BuiltAgainst != null && mod.BuiltAgainst > current
                        ? $"it needs FruktSharedLibrary {Short(mod.BuiltAgainst)} or newer, and this is {Short(current)}."
                        : $"it uses parts of FruktSharedLibrary that {Short(current)} doesn't have. Update the library.";
                case LibraryModState.DidNotStart:
                    return "MelonLoader couldn't start it. MelonLoader/Latest.log says why.";
                case LibraryModState.TurnedOff:
                    return "it's switched off.";
                default:
                    return "";
            }
        }

        internal static string Short(Version version)
            => version == null ? "?" : version.Build > 0 ? $"{version.Major}.{version.Minor}.{version.Build}" : $"{version.Major}.{version.Minor}.0";

        private static void Announce()
        {
            if (_announced)
                return;
            _announced = true;
            foreach (var mod in Mods)
            {
                if (mod.State == LibraryModState.NeedsNewerLibrary || mod.State == LibraryModState.DidNotStart)
                    Notifications.Warn($"{mod.Name} is turned off: {Problem(mod)}", 10f);
            }
        }
    }
}
