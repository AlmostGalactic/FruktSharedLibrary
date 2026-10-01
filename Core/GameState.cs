using Il2CppData.Maps;
using UnityEngine.SceneManagement;

namespace FruktSharedLibrary.Core
{
    /// <summary>Which part of the game is currently running.</summary>
    public enum GamePhase
    {
        /// <summary>Splash screen / registration, before the main menu.</summary>
        Booting,
        /// <summary>The main menu is active.</summary>
        MainMenu,
        /// <summary>A map is loading.</summary>
        LoadingMap,
        /// <summary>A map is loaded and playable.</summary>
        Sandbox,
        /// <summary>Leaving a map (heading to the main menu or reloading).</summary>
        Transitioning,
    }

    /// <summary>Current high-level game state. Kept up to date by the library.</summary>
    public static class GameState
    {
        /// <summary>Current phase of the game.</summary>
        public static GamePhase Phase { get; internal set; } = GamePhase.Booting;

        /// <summary>True while the main menu is active.</summary>
        public static bool InMainMenu => Phase == GamePhase.MainMenu;

        /// <summary>True while a map is loaded and playable. Most gameplay APIs need this.</summary>
        public static bool InSandbox => Phase == GamePhase.Sandbox;

        /// <summary>The loaded (or loading) map, or null outside of the sandbox.</summary>
        public static MapID? CurrentMap { get; internal set; }

        /// <summary>Name of the active Unity scene.</summary>
        public static string ActiveSceneName => SceneManager.GetActiveScene().name;
    }
}
