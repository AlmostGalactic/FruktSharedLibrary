using FruktSharedLibrary.Combat;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Spawning;
using FruktSharedLibrary.UI;
using FruktSharedLibrary.Utilities;
using Il2CppData.Game;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// The library's own "Sandbox Tools" page in the mod menu. Doubles as a live example of the API.
    /// Can be turned off with the BuiltInMenuPage preference.
    /// </summary>
    internal static class BuiltInMenu
    {
        internal static void Register()
        {
            if (!FruktConfig.BuiltInMenuPage)
                return;

            static bool InSandbox() => GameState.InSandbox;

            ModMenu.AddPage("Sandbox Tools")
                .Label(Status)

                .Header("World").OnlyWhen(InSandbox)
                .Slider("Time scale", 0.05f, 2f, () => World.TimeScale, v => World.TimeScale = v).OnlyWhen(() => InSandbox() && !World.IsPaused)
                .Slider("Gravity", GravityRange().Min, GravityRange().Max, () => World.GravityStrength, v => World.GravityStrength = v, "0.0").OnlyWhen(InSandbox)
                .Button("Reset gravity", () => World.ResetGravity()).OnlyWhen(InSandbox)
                .Button("Reset map props", () => World.ResetMap()).OnlyWhen(InSandbox)
                .Button("Delete bodies", () => World.DeleteBodies()).OnlyWhen(InSandbox)
                .Button("Delete every creature", () => World.DeleteAllCreatures()).OnlyWhen(InSandbox)

                .Header("Spawn").OnlyWhen(InSandbox)
                .Button("Human", () => Creatures.SpawnHumanInFront(3f, c => Notifications.Show($"Spawned {c.GetDisplayName()}"))).OnlyWhen(InSandbox)
                .Button("Viper-17", () => Spawner.SpawnFirearmInFront(FirearmType.Viper17)).OnlyWhen(InSandbox)
                .Button("Lynx-F", () => Spawner.SpawnFirearmInFront(FirearmType.LynxF)).OnlyWhen(InSandbox)
                .Button("Grist-03", () => Spawner.SpawnFirearmInFront(FirearmType.Grist03)).OnlyWhen(InSandbox)

                .Header("Creature under crosshair").OnlyWhen(InSandbox)
                .Label(AimedStatus).OnlyWhen(InSandbox)
                .Button("Heal", () => WithAimed(c => c.Heal(), "Healed")).OnlyWhen(InSandbox)
                .Button("Kill", () => WithAimed(c => c.Kill(), "Killed")).OnlyWhen(InSandbox)
                .Button("Launch upwards", () => WithAimed(c => c.AddForce(Vector3.up * 600f), "Launched")).OnlyWhen(InSandbox)
                .Button("Delete", () => WithAimed(c => c.Delete(), "Deleted")).OnlyWhen(InSandbox)
                .Button("Explosion at crosshair", Explode).OnlyWhen(InSandbox)
                .Button("Log creature report", () => WithAimed(DevTools.LogCreature, "Report written to the console")).OnlyWhen(InSandbox)

                .Header("Library")
                .Toggle("Debug logging", () => FruktConfig.DebugLogging, v => FruktConfig.DebugLogging = v)
                .Toggle("Notifications", () => FruktConfig.ShowNotifications, v => FruktConfig.ShowNotifications = v)
                .Button("Log registered prefab IDs", DevTools.LogPrefabIds).OnlyWhen(InSandbox)
                .Label(() => $"<size=12>FruktSharedLibrary v{FruktSharedLibraryMod.Version} · hooks {LibraryPatches.Status.Applied}/{LibraryPatches.Status.Total}</size>");
        }

        private static (float Min, float Max) GravityRange()
        {
            try
            {
                return (WorldGravity.MIN_STRENGTH, WorldGravity.MAX_STRENGTH);
            }
            catch
            {
                return (0f, 30f);
            }
        }

        private static string Status()
        {
            if (!GameState.InSandbox)
                return $"Phase: {GameState.Phase}";
            return $"Map: {World.GetMapDisplayName(GameState.CurrentMap ?? default)}   Creatures: {Creatures.Count}   Kills: {World.KillCount}";
        }

        private static string AimedStatus()
        {
            var creature = Creatures.GetAimedCreature();
            if (creature == null)
                return "<i>Nothing under the crosshair.</i>";
            return $"{creature.GetDisplayName()} — {(creature.IsLiving() ? "alive" : "dead")}, blood {creature.GetBlood():0.#}/{creature.GetBloodCapacity():0.#}, pain {creature.GetPain():0.00}, limbs {creature.GetLimbCount()}";
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
