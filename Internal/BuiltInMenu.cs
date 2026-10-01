using FruktSharedLibrary.Combat;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.UI;
using FruktSharedLibrary.Utilities;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// The library's own pages. Only things the base game can't already do: gravity, map/body/creature
    /// clean-up and spawning live in the game's terminal (I), and delete/health/walk in its right-click menu.
    /// </summary>
    internal static class BuiltInMenu
    {
        internal static void Register()
        {
            if (FruktConfig.BuiltInMenuPage)
                RegisterSandboxTools();
            RegisterDeveloperTools();
        }

        private static void RegisterSandboxTools()
        {
            static bool InSandbox() => GameState.InSandbox;

            ModMenu.AddPage("Sandbox Tools")
                .Label("Load a map to use these tools.").OnlyWhen(() => !InSandbox())

                // The game's slow motion (T) is one fixed speed; this is any speed.
                .Header("Time").OnlyWhen(InSandbox)
                .Slider("Time scale", 0.05f, 2f, () => World.TimeScale, v => World.TimeScale = v).OnlyWhen(InSandbox)
                .WithTooltip("Any game speed. Applies when you unpause; slow motion (T) switches back to normal speed.")
                .Button("Normal speed", () => World.TimeScale = 1f).OnlyWhen(InSandbox)

                .Header("Creature under crosshair").OnlyWhen(InSandbox)
                .Label(AimedStatus).OnlyWhen(InSandbox)
                .Button("Heal", () => WithAimed(c => c.Heal(), "Healed")).OnlyWhen(InSandbox)
                .WithTooltip("Refills blood and closes bleeding wounds (lost tissue and limbs don't grow back).")
                .Button("Kill", () => WithAimed(c => c.Kill(), "Killed")).OnlyWhen(InSandbox)
                .Button("Launch upwards", () => WithAimed(c => c.AddForce(Vector3.up * 600f), "Launched")).OnlyWhen(InSandbox)
                .Button("Explosion at crosshair", Explode).OnlyWhen(InSandbox)
                .WithTooltip("Tears tissue and throws everything within a few metres.");
        }

        private static void RegisterDeveloperTools()
        {
            // Appended to the generated Library settings page.
            ModMenu.AddPage("Library settings")
                .Header("Developer tools")
                .Button("Log creature under crosshair", () => WithAimed(DevTools.LogCreature, "Report written to the console")).OnlyWhen(() => GameState.InSandbox)
                .Button("Log registered prefab IDs", DevTools.LogPrefabIds).OnlyWhen(() => GameState.InSandbox)
                .Label(() => $"FruktSharedLibrary v{FruktSharedLibraryMod.Version} · game hooks {LibraryPatches.Status.Applied}/{LibraryPatches.Status.Total} · mods using it: {ModMenu.LibraryMods.Count}");
        }

        private static string AimedStatus()
        {
            var creature = Creatures.GetAimedCreature();
            if (creature == null)
                return "Nothing under the crosshair.";
            return $"{creature.GetDisplayName()}, {(creature.IsLiving() ? "alive" : "dead")}";
        }

        private static void WithAimed(System.Action<Il2CppLVA.Creatures.AbstractCreature> action, string message)
        {
            var creature = Creatures.GetAimedCreature();
            if (creature == null)
            {
                Notifications.Warn("Aim at a creature first.");
                return;
            }
            string name = creature.GetDisplayName();
            action(creature);
            Notifications.Show($"{message}: {name}");
        }

        private static void Explode()
        {
            if (!LocalPlayer.TryGetAimPoint(out var point))
            {
                Notifications.Warn("Aim at something first.");
                return;
            }
            int limbs = Damage.Explosion(point, 2.5f, 40f, 5);
            Sounds.Play(Il2CppInfrastructure.Project.AssetsHandlers.SFX.ImpactSFXType.Bullet762HardSurface, point, 1f);
            LocalPlayer.ShakeCamera(0.6f);
            Notifications.Show($"Boom ({limbs} limbs hit)");
        }
    }
}
