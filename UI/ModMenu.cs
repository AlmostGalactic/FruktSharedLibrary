using System;
using System.Collections.Generic;
using System.Linq;
using FruktSharedLibrary.Controls;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FruktSharedLibrary.UI
{
    /// <summary>
    /// A shared in-game mod menu (default key F8, configurable). Every mod can add its own page with
    /// <see cref="AddPage"/>; pages appear as tabs. While open the cursor is freed and the game's
    /// button input is blocked.
    /// </summary>
    public static class ModMenu
    {
        private const float Width = 440f;
        private const float Padding = 12f;
        private const float TabHeight = 26f;

        private static readonly List<ModMenuPage> Pages = new();
        private static Il2CppSystem.Object _cursorOwner;
        private static int _selected;
        private static float _scroll;
        private static float _contentHeight;
        private static KeyBind _keyBind;
        private static string _keyBindText;
        private static bool _sliderBroken;
        private static bool _drawFailed;

        /// <summary>True while the menu is shown.</summary>
        public static bool IsOpen { get; private set; }

        internal static bool DrawFailed => _drawFailed;

        internal static int DrawCount { get; private set; }

        /// <summary>Raised when the menu opens (true) or closes (false).</summary>
        public static event Action<bool> OpenChanged;

        /// <summary>The key that toggles the menu (from the library's preferences).</summary>
        public static KeyBind ToggleKey
        {
            get
            {
                var text = FruktConfig.ModMenuKey;
                if (_keyBind == null || text != _keyBindText)
                {
                    _keyBindText = text;
                    _keyBind = KeyBind.Parse(text, Key.F8);
                }
                return _keyBind;
            }
        }

        /// <summary>Adds a page (tab) to the menu. Pages with the same title are merged.</summary>
        public static ModMenuPage AddPage(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Page title is empty.", nameof(title));
            var existing = Pages.FirstOrDefault(p => p.Title == title);
            if (existing != null)
                return existing;
            var page = new ModMenuPage(title);
            Pages.Add(page);
            return page;
        }

        /// <summary>Removes a page.</summary>
        public static void RemovePage(ModMenuPage page) => Pages.Remove(page);

        public static void Open() => SetOpen(true);

        public static void Close() => SetOpen(false);

        public static void Toggle() => SetOpen(!IsOpen);

        internal static void Update()
        {
            if (ToggleKey.WasPressed())
                Toggle();
            if (IsOpen && FruktInput.GetKeyDown(Key.Escape))
                Close();
        }

        internal static void ReleaseOnSceneChange()
        {
            if (IsOpen)
                SetOpen(false);
        }

        private static void SetOpen(bool open)
        {
            if (IsOpen == open)
                return;
            IsOpen = open;
            _cursorOwner ??= new Il2CppSystem.Object();
            if (open)
                LocalPlayer.CaptureCursor(_cursorOwner);
            else
                LocalPlayer.ReleaseCursor(_cursorOwner);

            var handlers = OpenChanged;
            if (handlers == null)
                return;
            foreach (Action<bool> handler in handlers.GetInvocationList())
            {
                try { handler(open); }
                catch (Exception e) { FruktLog.Error("ModMenu.OpenChanged handler threw", e); }
            }
        }

        // ------------------------------------------------------------ drawing

        internal static void Draw()
        {
            if (!IsOpen || _drawFailed)
                return;
            try
            {
                if (IsOpen)
                {
                    // Keep the cursor free even if the game re-locks it (e.g. after unpausing).
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                DrawWindow();
                DrawCount++;
            }
            catch (Exception e)
            {
                _drawFailed = true;
                FruktLog.Error("Drawing the mod menu failed; the menu is disabled", e);
            }
        }

        private static void DrawWindow()
        {
            var visiblePages = Pages.Where(IsPageVisible).ToList();
            float height = Mathf.Min(Screen.height - 40f, 680f);
            var window = new Rect(20f, 20f, Width, height);
            GUI.DrawTexture(window, GuiStyles.Panel);
            GUI.DrawTexture(new Rect(window.x, window.y, window.width, 3f), GuiStyles.Accent);

            float x = window.x + Padding;
            float y = window.y + 8f;
            float innerWidth = Width - Padding * 2f;
            GUI.Label(new Rect(x, y, innerWidth, 24f), "<b>MODS</b>", GuiStyles.Title);
            GUI.Label(new Rect(x + innerWidth - 150f, y, 150f, 24f), $"<size=12>{ToggleKey} / Esc to close</size>", GuiStyles.Label);
            y += 30f;

            if (visiblePages.Count == 0)
            {
                GUI.Label(new Rect(x, y, innerWidth, 40f), "No mod has added a page yet.", GuiStyles.Label);
                return;
            }

            _selected = Mathf.Clamp(_selected, 0, visiblePages.Count - 1);
            y = DrawTabs(visiblePages, x, y, innerWidth);
            y += 6f;

            var content = new Rect(x, y, innerWidth, window.yMax - y - Padding);
            HandleScroll(content);
            GUI.BeginGroup(content);
            _contentHeight = DrawItems(visiblePages[_selected], content.width, -_scroll);
            GUI.EndGroup();
            _scroll = Mathf.Clamp(_scroll, 0f, Mathf.Max(0f, _contentHeight - content.height));
        }

        private static float DrawTabs(List<ModMenuPage> pages, float x, float y, float width)
        {
            float tabX = x;
            for (int i = 0; i < pages.Count; i++)
            {
                string title = pages[i].Title;
                float tabWidth = Mathf.Clamp(title.Length * 8f + 24f, 70f, width);
                if (tabX + tabWidth > x + width && tabX > x)
                {
                    tabX = x;
                    y += TabHeight + 4f;
                }
                var rect = new Rect(tabX, y, tabWidth, TabHeight);
                if (i == _selected)
                    GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), GuiStyles.Accent);
                if (GUI.Button(rect, i == _selected ? $"<b>{title}</b>" : title, GuiStyles.Button) && i != _selected)
                {
                    _selected = i;
                    _scroll = 0f;
                }
                tabX += tabWidth + 4f;
            }
            return y + TabHeight;
        }

        private static float DrawItems(ModMenuPage page, float width, float startY)
        {
            float y = startY;
            foreach (var item in page.Items)
            {
                if (!item.IsVisible)
                    continue;
                var rect = new Rect(0f, y, width, item.Height);
                try
                {
                    DrawItem(item, rect);
                }
                catch (Exception e)
                {
                    GUI.Label(rect, $"<color=#ff7070>error: {e.Message}</color>", GuiStyles.Label);
                }
                y += item.Height + 4f;
            }
            return y - startY;
        }

        private static void DrawItem(ModMenuItem item, Rect rect)
        {
            switch (item.Kind)
            {
                case ModMenuItemKind.Header:
                    GUI.Label(new Rect(rect.x, rect.y + 4f, rect.width, rect.height - 4f), item.SafeText, GuiStyles.Header);
                    break;

                case ModMenuItemKind.Label:
                    GUI.Label(rect, item.SafeText, GuiStyles.Label);
                    break;

                case ModMenuItemKind.Separator:
                    GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height / 2f, rect.width, 1f), GuiStyles.PanelLight);
                    break;

                case ModMenuItemKind.Button:
                    if (GUI.Button(rect, item.SafeText, GuiStyles.Button))
                        Invoke(item, () => item.OnClick());
                    break;

                case ModMenuItemKind.Toggle:
                {
                    bool value = item.GetBool();
                    bool next = GUI.Toggle(rect, value, " " + item.SafeText);
                    if (next != value)
                        Invoke(item, () => item.SetBool(next));
                    break;
                }

                case ModMenuItemKind.Slider:
                    DrawSlider(item, rect);
                    break;
            }
        }

        private static void DrawSlider(ModMenuItem item, Rect rect)
        {
            float value = item.GetFloat();
            string readout;
            try
            {
                readout = value.ToString(item.Format);
            }
            catch
            {
                readout = value.ToString("0.00");
            }
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20f), $"{item.SafeText}: <b>{readout}</b>", GuiStyles.Label);
            var bar = new Rect(rect.x, rect.y + 22f, rect.width, 18f);

            if (!_sliderBroken)
            {
                try
                {
                    float next = GUI.HorizontalSlider(bar, value, item.Min, item.Max);
                    if (!Mathf.Approximately(next, value))
                        Invoke(item, () => item.SetFloat(item.ClampedValue(next)));
                    return;
                }
                catch (Exception e)
                {
                    _sliderBroken = true;
                    FruktLog.Debug("GUI.HorizontalSlider unavailable, using step buttons: " + e.Message);
                }
            }

            // Step-button fallback: [-] [bar] [+]
            float step = (item.Max - item.Min) / 20f;
            if (GUI.Button(new Rect(bar.x, bar.y, 30f, bar.height), "-", GuiStyles.Button))
                Invoke(item, () => item.SetFloat(item.ClampedValue(value - step)));
            var track = new Rect(bar.x + 34f, bar.y + 6f, bar.width - 68f, 6f);
            GUI.DrawTexture(track, GuiStyles.PanelLight);
            float t = Mathf.InverseLerp(item.Min, item.Max, value);
            GUI.DrawTexture(new Rect(track.x, track.y, track.width * t, track.height), GuiStyles.Accent);
            if (GUI.Button(new Rect(bar.xMax - 30f, bar.y, 30f, bar.height), "+", GuiStyles.Button))
                Invoke(item, () => item.SetFloat(item.ClampedValue(value + step)));
        }

        private static void HandleScroll(Rect content)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.ScrollWheel || !content.Contains(e.mousePosition))
                return;
            _scroll += e.delta.y * 24f;
            e.Use();
        }

        private static bool IsPageVisible(ModMenuPage page)
        {
            try
            {
                return page.VisibleWhen == null || page.VisibleWhen();
            }
            catch
            {
                return false;
            }
        }

        private static void Invoke(ModMenuItem item, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                FruktLog.Error($"Mod menu item '{item.SafeText}' threw", e);
                Notifications.Warn($"'{item.SafeText}' failed: {e.Message}");
            }
        }
    }
}
