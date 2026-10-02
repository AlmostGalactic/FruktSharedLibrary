using System;
using System.Collections.Generic;
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

        /// <summary>The library's preference category (shown as the "Library settings" page).</summary>
        internal static MelonPreferences_Category Category => _category;
        private static MelonPreferences_Entry<bool> _debugLogging;
        private static MelonPreferences_Entry<string> _modMenuKey;
        private static MelonPreferences_Entry<bool> _showNotifications;
        private static MelonPreferences_Entry<bool> _builtInMenuPage;
        private static MelonPreferences_Entry<bool> _nativeStyle;
        private static MelonPreferences_Entry<bool> _pauseMenuButton;
        private static MelonPreferences_Entry<bool> _mainMenuButton;
        private static MelonPreferences_Entry<string[]> _disabledMods;

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
            _mainMenuButton = _category.CreateEntry("MainMenuButton", true, "Main menu button",
                "Adds a MODS line to the game's main menu that opens the mod menu.");
            _disabledMods = _category.CreateEntry("DisabledMods", Array.Empty<string>(), "Disabled mods",
                "Names of mods that use the library and are switched off. Change it from the mod menu.", is_hidden: true);
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

        public static bool MainMenuButton => _mainMenuButton?.Value ?? true;

        /// <summary>Names of the mods the player switched off. They aren't started the next time the game runs.</summary>
        internal static IReadOnlyList<string> DisabledMods => _disabledMods?.Value ?? Array.Empty<string>();

        internal static void SetModDisabled(string name, bool disabled)
        {
            if (_disabledMods == null || string.IsNullOrEmpty(name))
                return;
            var list = new List<string>(DisabledMods);
            list.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (disabled)
                list.Add(name);
            _disabledMods.Value = list.ToArray();
            _category.SaveToFile(false);
        }
    }
}
