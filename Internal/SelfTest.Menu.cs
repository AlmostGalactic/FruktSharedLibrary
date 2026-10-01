using System.Collections;
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
            for (int i = 1; i <= 8; i++)
                page.Label($"filler line {i}, here so the page needs scrolling");
            ModMenuItem Find(string text) => page.Items.Find(i => i.SafeText == text);
            var toggleItem = Find("Test toggle");
            var sliderItem = Find("Test slider");
            var choiceItem = Find("Test choice");
            var keyItem = Find("Test key");
            var buttonItem = Find("Test button");

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

                NativeModMenu.ShowPage(page);
                yield return Wait(0.3f);
                Wheel("down", -5);
                yield return Wait(1.2f);
                Check("The mouse wheel scrolls the page", NativeModMenu.ScrollOffset > 100f, NativeModMenu.ScrollOffset.ToString("0"));
                Shot("menu-scrolled");
                yield return Wait(1.5f);

                Click("esc-chip", NativeModMenu.EscChip, 0.1f);
                yield return Wait(1f);
                Check("The ESC chip goes back to the page list", NativeModMenu.CurrentPage == null && ModMenu.IsOpen);
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
            }
            finally
            {
                ModMenu.Close();
                ModMenu.ForceSimple = false;
                ModMenu.RemovePage(page);
            }
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
