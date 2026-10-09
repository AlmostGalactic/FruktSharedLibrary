using FruktSharedLibrary.Core;
using FruktSharedLibrary.Internal;
using FruktSharedLibrary.UI;
using MelonLoader;

namespace FruktSharedLibrary
{
    /// <summary>
    /// MelonLoader entry point of the library. Mods don't need to touch this class: reference
    /// FruktSharedLibrary.dll and use the static APIs (GameServices, GameEvents, World, LocalPlayer,
    /// Creatures, Spawner, Damage, ContextMenus, ModMenu...).
    /// </summary>
    public sealed class FruktSharedLibraryMod : MelonMod
    {
        /// <summary>Library version. Mods can compare against it to require a minimum version.</summary>
        public const string Version = "0.4.1";

        /// <summary>The running library instance.</summary>
        public static FruktSharedLibraryMod Instance { get; private set; }

        // MelonLoader registers the library before any mod that depends on it, so this runs before those mods
        // have started: the moment to check them, and keep the switched-off and incompatible ones from starting.
        public override void OnEarlyInitializeMelon()
        {
            Instance = this;
            FruktLog.Initialize(LoggerInstance);
            FruktConfig.Initialize();
            ModGuard.Initialize();
        }

        public override void OnInitializeMelon()
        {
            ModGuard.AfterStartup();
            LibraryPatches.Apply(HarmonyInstance);
            PauseMenu.Initialize();
            GameEvents.SandboxExited += Objects.Joints.Clear;
            GameEvents.SandboxExited += Combat.Effects.Clear;
            GameEvents.SandboxExited += Entities.Tissue.StopAll;
            ModMenu.Initialize();
            BuiltInMenu.Register();
            Scheduler.Every(10f, ContextMenuCarrier.Cleanup);
            SelfTest.Initialize();
            FruktLog.Msg($"FruktSharedLibrary v{Version} ready. Mod menu: {ModMenu.ToggleKey}");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
            => GameFlow.OnSceneLoaded(sceneName);

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
            => GameFlow.OnSceneUnloaded(sceneName);

        public override void OnUpdate()
        {
            Scheduler.Tick();
            GameFlow.Update();
            CreatureTracker.Update();
            Gameplay.Toolbar.Update();
            ModItems.Update();
            ModCategories.Update();
            ModMenu.Update();
            PauseMenu.Update();
            MainMenuButton.Update();
            Notifications.Update();
            Combat.Bullets.Update();
            Combat.Effects.Update();
            Entities.Tissue.Update();
            GameEvents.RaiseUpdate();
        }

        public override void OnFixedUpdate() => GameEvents.RaiseFixedUpdate();

        public override void OnLateUpdate()
        {
            Objects.Joints.Update();
            // After the camera has moved, so labels don't trail a frame behind.
            UI.WorldLabels.Update();
            GameEvents.RaiseLateUpdate();
        }

        public override void OnGUI()
        {
            Notifications.Draw();
            ModMenu.Draw();
        }
    }
}
