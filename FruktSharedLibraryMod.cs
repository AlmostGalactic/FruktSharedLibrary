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
        public const string Version = "1.0.0";

        /// <summary>The running library instance.</summary>
        public static FruktSharedLibraryMod Instance { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            FruktLog.Initialize(LoggerInstance);
            FruktConfig.Initialize();
            LibraryPatches.Apply(HarmonyInstance);
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
            ModMenu.Update();
            GameEvents.RaiseUpdate();
        }

        public override void OnFixedUpdate() => GameEvents.RaiseFixedUpdate();

        public override void OnLateUpdate() => GameEvents.RaiseLateUpdate();

        public override void OnGUI()
        {
            Notifications.Draw();
            ModMenu.Draw();
        }
    }
}
