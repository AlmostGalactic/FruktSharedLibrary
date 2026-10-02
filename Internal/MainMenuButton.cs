using System;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.UI;
using Il2CppInfrastructure.Project.AssetsHandlers.SFX;
using Il2CppViews.Generic;
using Il2CppViews.MainMenu;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// A MODS line on the game's main menu, after SETTINGS, that opens the list of installed mods (just that list,
    /// not the whole mod menu). It's a copy of the game's own SETTINGS line, so it looks and animates like the rest.
    /// </summary>
    internal static class MainMenuButton
    {
        internal const string ObjectName = "FruktSharedLibrary.MainMenuButton";

        private static MenuLineButton _button;
        private static IDisposable _subscription;
        private static float _nextSearch;
        private static bool _failed;

        /// <summary>The line, once it's been added (null before that, or outside the main menu).</summary>
        internal static MenuLineButton Button => _button;

        internal static void Update()
        {
            if (_failed || !FruktConfig.MainMenuButton || !GameState.InMainMenu)
                return;
            // The main menu is rebuilt each time it loads, which destroys the old line.
            if (_button.Exists() || Time.unscaledTime < _nextSearch)
                return;
            _nextSearch = Time.unscaledTime + 1f;
            try
            {
                Attach();
            }
            catch (Exception e)
            {
                _failed = true;
                FruktLog.Warning("Adding the MODS line to the main menu failed: " + e.Message);
            }
        }

        private static void Attach()
        {
            var view = GameServices.FindObject<MainMenuView>(true);
            var settings = view != null ? view.m_settingsButton : null;
            if (settings == null || settings.transform.parent == null)
                return;
            _subscription?.Dispose();
            _subscription = null;
            var copy = FruktUi.CloneGameUi(settings.gameObject, settings.transform.parent, ObjectName);
            var button = copy != null ? copy.GetComponent<MenuLineButton>() : null;
            if (button == null)
            {
                if (copy != null)
                    UnityEngine.Object.Destroy(copy);
                _failed = true;
                FruktLog.Warning("The main menu's SETTINGS line couldn't be copied.");
                return;
            }
            copy.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1);
            button.SetWord("MODS");
            try
            {
                button.ApplySelection(false);
            }
            catch
            {
                // Only cosmetic.
            }
            _subscription = button.OnClicked.Listen(Click);
            copy.SetActive(true);
            _button = button;
            FruktLog.Debug("Main menu MODS line added");
        }

        private static void Click()
        {
            Sounds.Play(UISFXType.LargeButtonClick, 0.8f);
            ModMenu.OpenModsList();
        }
    }
}
