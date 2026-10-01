using System;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.UI;
using Il2CppData.Maps;
using Il2CppPresenters.MainMenu;
using Il2CppServices.Creatures;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    /// <summary>Tracks <see cref="GameState.Phase"/> and raises the flow events in <see cref="GameEvents"/>.</summary>
    internal static class GameFlow
    {
        private const float SandboxSettleSeconds = 0.5f;

        private static float _loadingSince;
        private static float _nextMenuProbe;

        internal static void OnMainMenuEntered()
        {
            if (GameState.Phase == GamePhase.Sandbox)
                GameEvents.RaiseSandboxExited();
            GameState.Phase = GamePhase.MainMenu;
            GameState.CurrentMap = null;
            FruktLog.Debug("Main menu entered.");
            GameEvents.RaiseMainMenuEntered();
        }

        internal static void OnMapLoading(MapID map)
        {
            if (GameState.Phase == GamePhase.Sandbox)
                GameEvents.RaiseSandboxExited();
            GameState.Phase = GamePhase.LoadingMap;
            GameState.CurrentMap = map;
            _loadingSince = Time.realtimeSinceStartup;
            FruktLog.Debug($"Loading map {map}.");
            GameEvents.RaiseMapLoading(map);
        }

        internal static void OnSceneUnloaded(string sceneName)
        {
            if (GameState.Phase != GamePhase.Sandbox || GameState.CurrentMap == null)
                return;
            if (string.Equals(World.GetMapSceneName(GameState.CurrentMap.Value), sceneName, StringComparison.OrdinalIgnoreCase))
                OnSandboxExit();
        }

        internal static void OnSandboxExit()
        {
            if (GameState.Phase != GamePhase.Sandbox && GameState.Phase != GamePhase.LoadingMap)
                return;
            bool wasReady = GameState.Phase == GamePhase.Sandbox;
            GameState.Phase = GamePhase.Transitioning;
            ModMenu.ReleaseOnSceneChange();
            if (wasReady)
                GameEvents.RaiseSandboxExited();
        }

        internal static void OnSceneLoaded(string sceneName)
        {
            GameServices.ClearCache();
            ContextMenuCarrier.Cleanup();
            ModMenu.ReleaseOnSceneChange();

            // Fallback in case the state-machine patches couldn't be applied after a game update.
            if (GameState.Phase is GamePhase.Booting or GamePhase.Transitioning or GamePhase.MainMenu)
            {
                foreach (var map in World.Maps)
                {
                    if (string.Equals(World.GetMapSceneName(map), sceneName, StringComparison.OrdinalIgnoreCase))
                    {
                        OnMapLoading(map);
                        break;
                    }
                }
            }
            GameEvents.RaiseSceneLoaded(sceneName);
        }

        internal static void Update()
        {
            switch (GameState.Phase)
            {
                case GamePhase.LoadingMap:
                    float waited = Time.realtimeSinceStartup - _loadingSince;
                    if (waited < SandboxSettleSeconds)
                        return;
                    bool player = LocalPlayer.Exists;
                    bool registry = GameServices.Has<ICreatureRegistryService>();
                    if (player && registry)
                    {
                        GameState.Phase = GamePhase.Sandbox;
                        var map = GameState.CurrentMap ?? default;
                        FruktLog.Msg($"Sandbox ready ({World.GetMapDisplayName(map)}).");
                        GameEvents.RaiseSandboxReady(map);
                    }
                    else if (waited > 20f && Time.realtimeSinceStartup >= _nextMenuProbe)
                    {
                        _nextMenuProbe = Time.realtimeSinceStartup + 10f;
                        FruktLog.Warning($"Still waiting for the map after {waited:0}s: player={player} creatureRegistry={registry} " +
                                         $"sceneContainer={GameServices.SceneContainer != null} projectContainer={GameServices.ProjectContainer != null} scene='{GameState.ActiveSceneName}'");
                    }
                    break;

                case GamePhase.Booting:
                case GamePhase.Transitioning:
                    // Fallback main-menu detection.
                    if (Time.realtimeSinceStartup < _nextMenuProbe)
                        return;
                    _nextMenuProbe = Time.realtimeSinceStartup + 1f;
                    var presenter = GameServices.FindObject<MainMenuPresenter>();
                    if (presenter.Exists() && presenter.isActiveAndEnabled)
                        OnMainMenuEntered();
                    break;
            }
        }
    }
}
