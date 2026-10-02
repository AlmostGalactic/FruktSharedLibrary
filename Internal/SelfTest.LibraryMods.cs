using System.Collections;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.UI;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // Runs on the main menu, before the test map loads: the startup checks of mods that use the library, the MODS
    // line on the main menu, and switching a mod off and on again from the mod menu.
    internal static partial class SelfTest
    {
        // Name of the optional test mod built from tools/CompatTestMod (see docs/building-and-testing.md).
        private const string CompatTestMod = "FSL Compat Test";

        private static IEnumerator TestMainMenu()
        {
            Section("Mods using the library", () =>
            {
                Check("The library checked the installed mods", true, string.Join(", ", ModGuard.All.Select(m => $"{m.Name}={m.State}")));
                foreach (var mod in ModGuard.All)
                {
                    bool registered = mod.Melon != null && mod.Melon.Registered;
                    Check($"{mod.Name} is running exactly when it's allowed to", registered == (mod.State == LibraryModState.Running),
                        $"{mod.State}, registered={registered}");
                }
                var compat = ModGuard.All.FirstOrDefault(m => m.Name == CompatTestMod);
                if (compat != null)
                {
                    Check("A mod that uses a feature this version doesn't have isn't started",
                        compat.State == LibraryModState.NeedsNewerLibrary && compat.Missing.Any(n => n.Contains("FutureFeature")),
                        $"{compat.State}: {string.Join(", ", compat.Missing)}");
                    Check("It says which version it needs", ModGuard.Problem(compat).Contains("9.9.0"), ModGuard.Problem(compat));
                    Check("Features it uses that do exist aren't reported", !compat.Missing.Any(n => n.Contains("Notifications")));
                }
            });

            for (float end = Now() + 5f; Now() < end && !MainMenuButton.Button.Exists();)
                yield return null;
            Check("The main menu has a MODS line", MainMenuButton.Button.Exists());
            if (!MainMenuButton.Button.Exists())
                yield break;
            yield return Wait(1f);
            Shot("main-menu");
            yield return Wait(1f);
            Click("main-menu-mods", MainMenuButton.Button.GetComponent<RectTransform>(), 0.2f);
            yield return Wait(1.5f);
            Check("Clicking it opens the mod menu", ModMenu.IsOpen);
            if (!ModMenu.IsNative)
            {
                ModMenu.Close();
                yield break;
            }
            Check("It opens on just the list of mods", NativeModMenu.CurrentPage == ModMenu.ModsPage && NativeModMenu.PageStack.Count == 1,
                NativeModMenu.CurrentPage?.Title ?? "the top-level list");
            Shot("main-menu-modlist");
            yield return Wait(1f);
            PressKey("Escape-modlist", 0x1B);
            yield return Wait(1f);
            Check("Esc on that list closes the menu instead of going to the rest of it", !ModMenu.IsOpen);
            ModMenu.Close();
            yield return Wait(0.5f);
            ModMenu.OpenModsList();
            yield return Wait(1f);

            // A running mod is switched off and on again; a switched-off one (from an earlier run) on and off again.
            var target = ModGuard.All.FirstOrDefault(m => m.State == LibraryModState.Running || m.State == LibraryModState.TurnedOff);
            var page = target != null ? ModMenu.PageOfMod(target.Melon) : null;
            var enabled = page?.Items.Find(i => i.Kind == ModMenuItemKind.Toggle && i.SafeText == "Enabled");
            Check("Each mod's page has an Enabled switch", enabled != null, target?.Name ?? "no mod uses the library");
            if (enabled != null)
            {
                bool wasOn = target.WantsOn;
                NativeModMenu.ShowPage(page);
                yield return Wait(1f);
                Click("mod-enabled", NativeModMenu.HitFor(enabled));
                yield return Wait(1f);
                Check("Switching a mod is saved and asks for a restart", target.WantsOn != wasOn && target.RestartNeeded,
                    $"{target.Name}: {target.State}, disabled list: {string.Join(",", FruktConfig.DisabledMods)}");
                Shot("mod-switched");
                yield return Wait(1f);
                Click("mod-enabled-again", NativeModMenu.HitFor(enabled));
                yield return Wait(1f);
                Check("Switching it back undoes that", target.WantsOn == wasOn && !target.RestartNeeded);
                if (target.WantsOn != wasOn)
                    FruktConfig.SetModDisabled(target.Name, !wasOn);
            }
            ModMenu.Close();
            yield return Wait(1f);
        }
    }
}
