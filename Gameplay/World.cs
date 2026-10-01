using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using Il2CppData.Game;
using Il2CppData.Maps;
using Il2CppInfrastructure.Project.States.Variants;
using Il2CppPresenters.MainMenu;
using Il2CppPresenters.Pause;
using Il2CppServices.Creatures;
using Il2CppServices.Game;
using Il2CppServices.Infrastructure;
using UnityEngine;

namespace FruktSharedLibrary.Gameplay
{
    /// <summary>
    /// World-level controls: time scale, pause, gravity, map reset/loading, the kill counter and
    /// bulk creature deletion. Everything here goes through the game's own services, so the game's
    /// UI, audio and save state stay in sync.
    /// </summary>
    public static class World
    {
        // ------------------------------------------------------------ time

        /// <summary>
        /// Game time scale (1 = normal, 0.25 = the game's slow motion). Uses the game's time-scale service so
        /// audio pitch follows. While paused, this reads and sets the speed the game resumes at (the real time
        /// scale stays 0 until the game unpauses).
        /// </summary>
        public static float TimeScale
        {
            get
            {
                // While paused the real time scale is 0; report the speed the game resumes at.
                var paused = PausedService();
                if (paused != null)
                    return paused.m_timeBeforePause;
                var service = GameServices.TryGet<ITimeScaleService>();
                return service != null ? service.Get() : Time.timeScale;
            }
            set
            {
                value = Mathf.Max(0f, value);
                var paused = PausedService();
                if (paused != null)
                {
                    // Applied when the game unpauses (the pause service restores this value).
                    paused.m_timeBeforePause = value;
                    return;
                }
                var service = GameServices.TryGet<ITimeScaleService>();
                if (service != null)
                    service.Set(value);
                else
                    Time.timeScale = value;
            }
        }

        private static Il2CppGame.PauseService PausedService()
        {
            var service = GameServices.TryGet<IPauseService>()?.TryCast<Il2CppGame.PauseService>();
            return service != null && service.Paused ? service : null;
        }

        /// <summary>True while the pause menu is open.</summary>
        public static bool IsPaused => GameServices.TryGet<IPauseService>()?.Paused ?? false;

        /// <summary>Pauses the game (opens the pause state like pressing Esc).</summary>
        public static bool Pause()
        {
            var service = GameServices.TryGet<IPauseService>();
            if (service == null || service.Paused)
                return false;
            service.SetPause();
            return true;
        }

        /// <summary>Unpauses the game.</summary>
        public static bool Resume()
        {
            var service = GameServices.TryGet<IPauseService>();
            if (service == null || !service.Paused)
                return false;
            service.CancelPause();
            return true;
        }

        // ------------------------------------------------------------ gravity

        /// <summary>
        /// The world gravity as the game models it: a strength (default 9.81) plus a tilt and turn that
        /// rotate the gravity direction. Setting it applies it exactly like the in-game terminal does.
        /// </summary>
        public static WorldGravity Gravity
        {
            get
            {
                var service = GameServices.TryGet<IWorldGravityService>();
                return service != null ? service.Current : WorldGravity.Default;
            }
            set => GameServices.TryGet<IWorldGravityService>()?.Set(value);
        }

        /// <summary>Gravity strength in m/s² (default 9.81). Clamped by the game to its supported range.</summary>
        public static float GravityStrength
        {
            get => Gravity.Strength;
            set
            {
                float clamped = Mathf.Clamp(value, WorldGravity.MIN_STRENGTH, WorldGravity.MAX_STRENGTH);
                Gravity = Gravity.WithStrength(clamped);
            }
        }

        /// <summary>Sets gravity strength and direction. Tilt 0 means straight down.</summary>
        public static void SetGravity(float strength, float tiltDegrees = 0f, float turnDegrees = 0f)
        {
            strength = Mathf.Clamp(strength, WorldGravity.MIN_STRENGTH, WorldGravity.MAX_STRENGTH);
            tiltDegrees = Mathf.Clamp(tiltDegrees, WorldGravity.MIN_TILT, WorldGravity.MAX_TILT);
            Gravity = new WorldGravity(strength, tiltDegrees, turnDegrees);
        }

        /// <summary>Restores the default gravity.</summary>
        public static void ResetGravity() => GameServices.TryGet<IWorldGravityService>()?.ResetToDefaults();

        // ------------------------------------------------------------ map & creatures

        /// <summary>Resets the map's props back to where they started (same as the terminal's map reset).</summary>
        public static bool ResetMap()
        {
            var service = GameServices.TryGet<IMapResetService>();
            if (service == null)
                return false;
            service.ResetMap();
            return true;
        }

        /// <summary>Deletes every creature in the world, alive or dead.</summary>
        public static bool DeleteAllCreatures()
        {
            var service = GameServices.TryGet<ICreatureSweepService>();
            if (service == null)
                return false;
            service.DeleteEveryCreature();
            return true;
        }

        /// <summary>Deletes dead bodies and loose body parts, keeping living creatures.</summary>
        public static bool DeleteBodies()
        {
            var service = GameServices.TryGet<ICreatureSweepService>();
            if (service == null)
                return false;
            service.DeleteBodies();
            return true;
        }

        /// <summary>The session kill counter shown in the HUD.</summary>
        public static int KillCount => GameServices.TryGet<IKillsService>()?.KillsCount ?? 0;

        /// <summary>Adds one to the kill counter.</summary>
        public static void AddKill() => GameServices.TryGet<IKillsService>()?.AddKill();

        // ------------------------------------------------------------ maps

        /// <summary>All maps the game knows about.</summary>
        public static IReadOnlyList<MapID> Maps => (MapID[])Enum.GetValues(typeof(MapID));

        /// <summary>The loaded map, or null outside of the sandbox.</summary>
        public static MapID? CurrentMap => GameState.CurrentMap;

        /// <summary>Display name of a map as the game shows it (MapID.Yard is "SPIRE", MapID.Flatland is "HOMESTEAD").</summary>
        public static string GetMapDisplayName(MapID map)
        {
            try
            {
                var info = GameServices.TryGet<IMapCatalogue>()?.Info(map);
                if (info != null && !string.IsNullOrEmpty(info.Word))
                    return info.Word;
            }
            catch
            {
                // Not all maps have catalogue cards.
            }
            return map.ToString();
        }

        /// <summary>Unity scene name of a map, or null if the catalogue isn't available.</summary>
        public static string GetMapSceneName(MapID map)
        {
            try
            {
                return GameServices.TryGet<IMapCatalogue>()?.SceneName(map);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Loads a map. From the main menu this is the same as clicking the map; from inside a map it switches
        /// directly through the game's state machine.
        /// </summary>
        public static bool LoadMap(MapID map)
        {
            var presenter = GameServices.FindObject<MainMenuPresenter>();
            if (presenter.Exists())
            {
                try
                {
                    presenter.LoadMap(map);
                    return true;
                }
                catch (Exception e)
                {
                    FruktLog.Warning($"Loading {map} from the main menu failed: {e.Message}");
                }
            }

            var stateMachine = GameServices.TryGet<IGameStateMachine>();
            if (stateMachine == null)
                return false;
            try
            {
                stateMachine.Enter<SandboxState, MapID>(map);
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Loading {map} through the state machine failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Leaves the map and returns to the main menu (same as "Back to menu" in the pause menu).</summary>
        public static bool ReturnToMainMenu()
        {
            var pause = GameServices.FindObject<PausePresenter>(includeInactive: true);
            if (pause.Exists())
            {
                try
                {
                    pause.BackToMenu();
                    return true;
                }
                catch (Exception e)
                {
                    FruktLog.Warning($"BackToMenu failed: {e.Message}");
                }
            }

            var stateMachine = GameServices.TryGet<IGameStateMachine>();
            if (stateMachine == null)
                return false;
            try
            {
                stateMachine.Enter<MainMenuState>();
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Entering the main menu state failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Closes the game.</summary>
        public static void Quit() => Application.Quit();
    }
}
