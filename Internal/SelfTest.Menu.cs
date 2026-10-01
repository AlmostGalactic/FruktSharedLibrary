using System.Collections;
using System.Linq;
using FruktSharedLibrary.Controls;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FruktSharedLibrary.Internal
{
    // The mod menu part of the self-test. Real mouse clicks, wheel turns and key presses are sent by the
    // external watcher script when the test logs "[SelfTest] CLICK/WHEEL/KEY ..." markers, so this checks the
    // menu the way a player uses it.
    internal static partial class SelfTest
    {
        private enum TestMode
        {
            Easy,
            Normal,
            Hard,
        }

        private static IEnumerator TestModMenu()
        {
            bool toggle = false;
            float slider = 0.25f;
            int whole = 3;
            int choice = 0;
            var mode = TestMode.Normal;
            var bind = new KeyBind(Key.G);
            int clicks = 0;

            var page = ModMenu.AddPage("Self-test menu")
                .Header("Controls")
                .Label("Every row type, driven by real mouse clicks.")
                .Toggle("Test toggle", () => toggle, v => toggle = v).WithTooltip("A hint line under the toggle.")
                .Slider("Test slider", 0f, 1f, () => slider, v => slider = v)
                .Slider("Whole numbers", 0, 10, () => whole, v => whole = v)
                .Choice("Test choice", new[] { "flat", "shift", "free" }, () => choice, v => choice = v)
                .Choice("Enum choice", () => mode, v => mode = v)
                .KeyBinding("Test key", () => bind, v => bind = v)
                .Separator()
                .Button("Test button", () => clicks++);
            var subPage = page.AddSubPage("Test sub-page").Label("Inside a sub-page.");
            for (int i = 1; i <= 8; i++)
                page.Label($"filler line {i}, here so the page needs scrolling");
            ModMenuItem Find(string text) => page.Items.Find(i => i.SafeText == text);
            var toggleItem = Find("Test toggle");
            var sliderItem = Find("Test slider");
            var choiceItem = Find("Test choice");
            var keyItem = Find("Test key");
            var buttonItem = Find("Test button");
            var subLink = Find("Test sub-page");

            try
            {
                Section("Mod menu open", () =>
                {
                    ModMenu.Open();
                    Check("ModMenu.Open() shows the native menu", ModMenu.IsNative, $"fonts={FruktTheme.Available} failed={NativeModMenu.Failed}");
                });
                if (!ModMenu.IsNative)
                    yield break;
                yield return Wait(1f);
                Check("Native menu lists the pages", NativeModMenu.RowCount >= 2, NativeModMenu.RowCount + " rows");
                Shot("menu-root");
                yield return Wait(1.5f);

                Click("page", NativeModMenu.HitForPage(page));
                yield return Wait(1.2f);
                Check("Clicking a page line opens it", NativeModMenu.CurrentPage == page);
                if (NativeModMenu.CurrentPage != page)
                    NativeModMenu.ShowPage(page);
                yield return Wait(0.5f);
                Shot("menu-page");
                yield return Wait(1.5f);

                Click("toggle", NativeModMenu.HitFor(toggleItem));
                yield return Wait(1f);
                Check("Clicking a toggle flips it", toggle);

                Click("choice", NativeModMenu.OptionFor(choiceItem, 2));
                yield return Wait(1f);
                Check("Clicking a choice option selects it", choice == 2, "choice=" + choice);

                Click("slider", NativeModMenu.TrackFor(sliderItem), 0.75f);
                yield return Wait(1f);
                Check("Clicking a slider track sets the value", Mathf.Abs(slider - 0.75f) < 0.05f, slider.ToString("0.000"));

                NativeModMenu.ScrollTo(keyItem);
                yield return Wait(0.3f);
                Click("keychip", NativeModMenu.HitFor(keyItem));
                yield return Wait(1f);
                Check("Clicking a key chip starts listening", NativeModMenu.CapturingKey);
                Shot("menu-capturing");
                yield return Wait(1f);
                PressKey("K", 0x4B);
                yield return Wait(1f);
                Check("Pressing a key rebinds it", bind != null && bind.Key == Key.K && !NativeModMenu.CapturingKey, bind?.ToString() ?? "null");
                Check("The menu stayed open after rebinding", ModMenu.IsOpen);

                Click("button", NativeModMenu.HitFor(buttonItem));
                yield return Wait(1f);
                Check("Clicking a button runs it", clicks == 1, "clicks=" + clicks);

                NativeModMenu.ScrollTo(subLink);
                yield return Wait(0.3f);
                Click("sublink", NativeModMenu.HitFor(subLink));
                yield return Wait(1f);
                Check("Clicking a sub-page line opens it", NativeModMenu.CurrentPage == subPage && NativeModMenu.PageStack.Count == 2);
                Click("esc-sub", NativeModMenu.EscChip, 0.1f);
                yield return Wait(1f);
                Check("Back from a sub-page returns to its page", NativeModMenu.CurrentPage == page);

                NativeModMenu.ShowPage(page);
                yield return Wait(0.3f);
                Click("over-list", NativeModMenu.Viewport, 0.9f); // empty space: puts the cursor over the list
                yield return Wait(0.8f);
                Wheel("down", -5);
                yield return Wait(1.2f);
                Check("The mouse wheel scrolls the page", NativeModMenu.ScrollOffset > 100f, NativeModMenu.ScrollOffset.ToString("0"));
                Shot("menu-scrolled");
                yield return Wait(1.5f);

                Click("esc-chip", NativeModMenu.EscChip, 0.1f);
                yield return Wait(1f);
                Check("The ESC chip goes back to the page list", NativeModMenu.CurrentPage == null && ModMenu.IsOpen);

                // MODS: every installed mod that uses the library, with its own pages inside.
                var modsPage = ModMenu.ModsPage;
                Check("MODS is the first top-level line", ModMenu.RootPages().First() == modsPage);
                Check("Built-in pages stay at the top level", ModMenu.RootPages().Any(p => p.Title == "Sandbox Tools"));
                Click("mods", NativeModMenu.HitForPage(modsPage));
                yield return Wait(1f);
                Check("Clicking MODS opens the list of mods", NativeModMenu.CurrentPage == modsPage);
                Shot("menu-mods");
                yield return Wait(1.5f);
                var example = ModMenu.LibraryMods.FirstOrDefault(m => m.Info.Name == "ExampleMod");
                if (example == null)
                {
                    Check("MODS says when no mod uses the library", modsPage.Items.Count == 1 && modsPage.Items[0].SafeText.StartsWith("No installed mod"),
                        "ExampleMod not installed; " + modsPage.Items.Count + " items");
                }
                else
                {
                    var modPage = ModMenu.PageOfMod(example);
                    Check("A mod's page is not listed at the top level", !ModMenu.RootPages().Any(p => p.Owner == example));
                    Click("mod-example", NativeModMenu.HitForPage(modPage));
                    yield return Wait(1f);
                    Check("Clicking a mod opens its entry", NativeModMenu.CurrentPage == modPage);
                    string info = modPage.Items.Count > 0 ? modPage.Items[0].SafeText : "";
                    Check("A mod's entry shows its version and author", info == "version 1.0.0 by test", info);
                    var links = modPage.Items.FindAll(i => i.Kind == ModMenuItemKind.Link);
                    Check("A mod's entry links to each of its pages", links.Count == 2 && links.Exists(l => l.SafeText == "Example settings"),
                        string.Join(",", links.ConvertAll(l => l.SafeText)));
                    Shot("menu-mod-example");
                    yield return Wait(1.5f);
                    var settingsLink = links.Find(l => l.SafeText == "Example settings");
                    if (settingsLink != null)
                    {
                        Click("mod-example-settings", NativeModMenu.HitFor(settingsLink));
                        yield return Wait(1f);
                        Check("A mod's page opens from its entry", NativeModMenu.CurrentPage?.Title == "Example settings" && NativeModMenu.PageStack.Count == 3,
                            NativeModMenu.CurrentPage?.Title);
                        Shot("menu-mod-page");
                        yield return Wait(1.5f);
                        Click("esc-mod-1", NativeModMenu.EscChip, 0.1f);
                        yield return Wait(0.8f);
                    }
                    Click("esc-mod-2", NativeModMenu.EscChip, 0.1f);
                    yield return Wait(0.8f);
                }
                Click("esc-mods", NativeModMenu.EscChip, 0.1f);
                yield return Wait(1f);
                Check("Back from MODS returns to the top level", NativeModMenu.CurrentPage == null && ModMenu.IsOpen);
                PressKey("Escape", 0x1B);
                yield return Wait(1f);
                Check("Esc closes the menu", !ModMenu.IsOpen);
                Check("The Esc that closed the menu didn't also open the pause menu", !Gameplay.World.IsPaused);
                if (Gameplay.World.IsPaused)
                    Gameplay.World.Resume();

                if (ModMenu.PauseEntry != null)
                {
                    Section("Pause menu line", () => Gameplay.World.Pause());
                    yield return Wait(2.5f);
                    Section("Time scale while paused", () =>
                    {
                        Gameplay.World.TimeScale = 0.5f;
                        Check("Setting the time scale while paused keeps the game paused", Time.timeScale == 0f && Mathf.Abs(Gameplay.World.TimeScale - 0.5f) < 0.001f,
                            $"unity={Time.timeScale} world={Gameplay.World.TimeScale}");
                    });
                    Check("PauseMenu added its line to the pause menu", PauseMenu.Attached && ModMenu.PauseEntry.Button != null,
                        ModMenu.PauseEntry.Button == null ? "no button" : ModMenu.PauseEntry.Label);
                    Shot("pause-with-button");
                    yield return Wait(1.5f);
                    if (ModMenu.PauseEntry.Button != null)
                    {
                        Click("pause-button", ModMenu.PauseEntry.Button.GetComponent<RectTransform>(), 0.3f);
                        yield return Wait(1.2f);
                        Check("Clicking the pause menu line opens the mod menu", ModMenu.IsNative);
                        Shot("menu-from-pause");
                        yield return Wait(1.5f);
                        PressKey("Escape2", 0x1B);
                        yield return Wait(1.2f);
                        Check("Esc returns from the mod menu to the pause menu", !ModMenu.IsOpen && Gameplay.World.IsPaused);
                    }
                    Gameplay.World.Resume();
                    yield return Wait(1f);
                    Check("A time scale set while paused applies on resume", Mathf.Abs(Time.timeScale - 0.5f) < 0.01f, Time.timeScale.ToString("0.###"));
                    Gameplay.World.TimeScale = 1f;
                }

                ModMenu.ForceSimple = true;
                ModMenu.Open();
                ModMenu.AddPage("Self-test menu"); // already exists; merging must not duplicate it
                int drawsBefore = ModMenu.DrawCount;
                yield return Wait(1f);
                Shot("menu-simple");
                yield return Wait(1.5f);
                Check("The simple menu draws without errors", ModMenu.DrawCount > drawsBefore && !ModMenu.DrawFailed, $"{ModMenu.DrawCount - drawsBefore} draws");
                Check("Pages with the same title are merged", ModMenu.Pages.FindAll(p => p.Title == "Self-test menu").Count == 1);
                ModMenu.Close();
                ModMenu.ForceSimple = false;

                Section("Preferences page", TestPreferencesPage);
                var library = ModMenu.Pages.Find(p => p.Title == "Library settings");
                Check("The library's settings page exists and is listed last", library != null && ModMenu.RootPages().Last() == library);
                if (library != null)
                {
                    ModMenu.Open();
                    yield return Wait(0.3f);
                    NativeModMenu.ShowPage(library);
                    yield return Wait(1f);
                    Shot("menu-library-settings");
                    yield return Wait(1.5f);
                    var tools = ModMenu.Pages.Find(p => p.Title == "Sandbox Tools");
                    if (tools != null)
                    {
                        NativeModMenu.ShowPage(tools);
                        yield return Wait(1f);
                        Shot("menu-sandbox-tools");
                        yield return Wait(1.5f);
                    }
                }
            }
            finally
            {
                ModMenu.Close();
                ModMenu.ForceSimple = false;
                ModMenu.RemovePage(page);
            }
        }

        private static void TestPreferencesPage()
        {
            string file = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "MelonPreferences.cfg");
            var category = MelonLoader.MelonPreferences.CreateCategory("FruktSharedLibrarySelfTest", "Self-test settings");
            var flag = category.CreateEntry("Flag", false, "A flag", "Bool entry.");
            var count = category.CreateEntry("Count", 3, "Count", null, false, false, new MelonLoader.Preferences.ValueRange<int>(0, 10));
            var ratio = category.CreateEntry("Ratio", 0.5f, "Ratio", null, false, false, new MelonLoader.Preferences.ValueRange<float>(0f, 1f));
            var mode = category.CreateEntry("Mode", TestMode.Normal, "Mode");
            var key = category.CreateEntry("ToggleKey", "Ctrl+K", "Toggle key");
            category.CreateEntry("Name", "hello", "Name");
            category.CreateEntry("Secret", 1, "Secret", null, true);
            try
            {
                var page = ModMenu.AddPreferencesPage(category);
                var kinds = string.Join(",", page.Items.ConvertAll(i => i.Kind.ToString()));
                Check("AddPreferencesPage picks a row per entry type",
                    kinds == "Toggle,Slider,Slider,Choice,KeyBinding,Label,Separator,Button", kinds);
                Check("Hidden entries are left out", page.Items.TrueForAll(i => i.SafeText != "Secret"));
                Check("Descriptions become hints", page.Items[0].Tooltip == "Bool entry.");

                page.Items[0].SetBool(true);
                page.Items[1].SetFloat(7f);
                page.Items[2].SetFloat(0.25f);
                page.Items[3].SetInt(2);
                page.Items[4].SetKey(new KeyBind(Key.J, ctrl: false, shift: true, alt: false));
                Check("Preference rows write the entries", flag.Value && count.Value == 7 && System.Math.Abs(ratio.Value - 0.25f) < 0.001f && mode.Value == TestMode.Hard,
                    $"flag={flag.Value} count={count.Value} ratio={ratio.Value} mode={mode.Value}");
                Check("Key rows store the key as text", KeyBind.TryParse(key.Value, out var bind) && bind.Key == Key.J && bind.Shift, key.Value);
                Check("Preference rows read the entries", page.Items[1].GetFloat() == 7f && page.Items[3].GetInt() == 2);

                page.Items[page.Items.Count - 1].OnClick();
                Check("Reset to defaults", !flag.Value && count.Value == 3 && mode.Value == TestMode.Normal);
                ModMenu.RemovePage(page);
            }
            finally
            {
                // The throwaway category must never be written to MelonPreferences.cfg: MelonLoader keeps saved
                // sections in memory and writes them back on exit even after the category is removed.
                ModMenu.ForgetUnsaved(category);
                MelonLoader.MelonPreferences.Categories.Remove(category);
            }

            // Saving, checked on a real entry of the library's own page (and put back afterwards).
            var library = ModMenu.Pages.Find(p => p.Title == "Library settings");
            var debugRow = library?.Items.Find(i => i.Kind == ModMenuItemKind.Toggle && i.SafeText == "Debug logging");
            if (debugRow == null)
            {
                Check("Library settings page has a Debug logging toggle", false);
                return;
            }
            bool before = FruktConfig.DebugLogging;
            debugRow.SetBool(!before);
            ModMenu.SaveUnsaved();
            Check("Changed preferences are saved", System.IO.File.ReadAllText(file).Contains($"DebugLogging = {(!before).ToString().ToLowerInvariant()}"));
            debugRow.SetBool(before);
            ModMenu.SaveUnsaved();
            Check("Preferences restored after the save test", FruktConfig.DebugLogging == before);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float end = Now() + seconds; Now() < end;)
                yield return null;
        }

        private static void Shot(string name) => FruktLog.Msg("[SelfTest] SCREENSHOT " + name);

        private static void PressKey(string name, int virtualKey) => FruktLog.Msg($"[SelfTest] KEY {name} {virtualKey}");

        private static void Wheel(string name, int notches) => FruktLog.Msg($"[SelfTest] WHEEL {name} {notches}");

        /// <summary>Asks the watcher to click a rect, at <paramref name="along"/> of its width, as a fraction of the game window.</summary>
        private static void Click(string name, RectTransform rect, float along = 0.5f)
        {
            if (rect == null)
            {
                Check($"Click target '{name}' exists", false);
                return;
            }
            var bounds = rect.rect;
            var world = rect.TransformPoint(new Vector2(bounds.xMin + bounds.width * along, bounds.center.y));
            var canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screen = RectTransformUtility.WorldToScreenPoint(camera, world);
            FruktLog.Msg($"[SelfTest] CLICK {name} {screen.x / Screen.width:0.0000} {1f - screen.y / Screen.height:0.0000}");
        }
    }
}
