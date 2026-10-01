using MelonLoader;

namespace FruktSharedLibrary.Core
{
    /// <summary>
    /// The library's own preferences, stored by MelonLoader in UserData/MelonPreferences.cfg
    /// under the [FruktSharedLibrary] section.
    /// </summary>
    public static class FruktConfig
    {
        private static MelonPreferences_Category _category;
        private static MelonPreferences_Entry<bool> _debugLogging;
        private static MelonPreferences_Entry<string> _modMenuKey;
        private static MelonPreferences_Entry<bool> _showNotifications;
        private static MelonPreferences_Entry<bool> _builtInMenuPage;
        private static MelonPreferences_Entry<bool> _nativeStyle;
        private static MelonPreferences_Entry<bool> _pauseMenuButton;

        internal static void Initialize()
        {
            _category = MelonPreferences.CreateCategory("FruktSharedLibrary", "Frukt Shared Library");
            _debugLogging = _category.CreateEntry("DebugLogging", false, "Debug logging",
                "Write extra diagnostic lines to the MelonLoader console.");
            _modMenuKey = _category.CreateEntry("ModMenuKey", "F8", "Mod menu key",
                "Key that opens the mod menu. Examples: F8, Insert, Ctrl+M.");
            _showNotifications = _category.CreateEntry("ShowNotifications", true, "Show notifications",
                "Show on-screen notifications posted by mods.");
            _nativeStyle = _category.CreateEntry("NativeStyle", true, "Native-style menu",
                "Draw the mod menu in FRUKT's own style (falls back to a simple menu if that fails).");
            _pauseMenuButton = _category.CreateEntry("PauseMenuButton", true, "Pause menu button",
                "Adds a line to the game's pause menu that opens the mod menu.");
            _builtInMenuPage = _category.CreateEntry("BuiltInMenuPage", true, "Built-in sandbox tools page",
                "Adds the library's own sandbox tools page to the mod menu.");
        }

        public static bool DebugLogging
        {
            get => _debugLogging?.Value ?? false;
            set { if (_debugLogging != null) _debugLogging.Value = value; }
        }

        public static string ModMenuKey
        {
            get => _modMenuKey?.Value ?? "F8";
            set { if (_modMenuKey != null) _modMenuKey.Value = value; }
        }

        public static bool ShowNotifications
        {
            get => _showNotifications?.Value ?? true;
            set { if (_showNotifications != null) _showNotifications.Value = value; }
        }

        public static bool BuiltInMenuPage => _builtInMenuPage?.Value ?? true;

        public static bool NativeStyle => _nativeStyle?.Value ?? true;

        public static bool PauseMenuButton => _pauseMenuButton?.Value ?? true;
    }
}
