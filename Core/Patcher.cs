using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace FruktSharedLibrary.Core
{
    /// <summary>
    /// Harmony helpers that isolate failures. With IL2CPP a single bad patch (wrong signature after a game
    /// update, an inlined method...) normally aborts <c>PatchAll</c>; these helpers apply patches one by one
    /// and log what failed instead.
    /// </summary>
    public static class Patcher
    {
        /// <summary>
        /// Applies every [HarmonyPatch] class in the assembly individually. Returns how many succeeded.
        /// Add <c>[assembly: HarmonyDontPatchAll]</c> to your mod and call this from OnInitializeMelon.
        /// </summary>
        public static int PatchAllSafe(HarmonyLib.Harmony harmony, Assembly assembly)
        {
            int ok = 0, failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
            {
                if (!type.GetCustomAttributes(typeof(HarmonyPatch), true).Any())
                    continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    failed++;
                    FruktLog.Warning($"[{harmony.Id}] Patch class {type.FullName} failed: {e.InnerException?.Message ?? e.Message}");
                }
            }
            if (failed > 0)
                FruktLog.Warning($"[{harmony.Id}] {ok} patch classes applied, {failed} failed.");
            return ok;
        }

        /// <summary>
        /// Patches one method. <paramref name="prefix"/>/<paramref name="postfix"/> are static methods
        /// (use <c>AccessTools.Method(typeof(MyPatches), nameof(MyPatches.Postfix))</c>).
        /// </summary>
        public static bool TryPatch(HarmonyLib.Harmony harmony, MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, string description = null)
        {
            string name = description ?? (original == null ? "<missing method>" : $"{original.DeclaringType?.Name}.{original.Name}");
            if (original == null)
            {
                FruktLog.Warning($"[{harmony.Id}] Can't patch {name}: method not found (game update?).");
                return false;
            }
            try
            {
                harmony.Patch(original,
                    prefix: prefix == null ? null : new HarmonyMethod(prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(postfix));
                FruktLog.Debug($"[{harmony.Id}] Patched {name}");
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning($"[{harmony.Id}] Patching {name} failed: {e.InnerException?.Message ?? e.Message}");
                return false;
            }
        }

        /// <summary>Patches a method looked up by name (and optionally parameter types).</summary>
        public static bool TryPatch(HarmonyLib.Harmony harmony, Type type, string methodName, Type[] parameters = null, MethodInfo prefix = null, MethodInfo postfix = null)
        {
            MethodInfo original = null;
            try
            {
                original = AccessTools.Method(type, methodName, parameters);
            }
            catch (AmbiguousMatchException)
            {
                FruktLog.Warning($"[{harmony.Id}] {type.Name}.{methodName} is overloaded; pass parameter types.");
                return false;
            }
            return TryPatch(harmony, original, prefix, postfix, $"{type.Name}.{methodName}");
        }
    }
}
