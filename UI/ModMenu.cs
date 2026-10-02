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
    /// The shared in-game mod menu (F8 by default, or the line in the pause menu). Every mod that uses the library
    /// is listed under MODS with its version and author; pages a mod adds with <see cref="AddPage(string)"/> or
    /// <see cref="AddPreferencesPage"/> appear in that mod's entry. While open the cursor is freed and the game's
    /// button input is blocked.
    /// </summary>
    public static partial class ModMenu
    {
        private const float Width = 440f;
        private const float Padding = 12f;
        private const float TabHeight = 26f;

        internal static readonly List<ModMenuPage> Pages = new();
        private static bool _native;
        private static Il2CppSystem.Object _cursorOwner;
        private static int _selected;
        private static float _scroll;
        private static float _contentHeight;
        private static ModMenuItem _capturing;
        private static readonly List<ModMenuPage> _subPages = new();
        private static KeyBind _keyBind;
        private static string _keyBindText;
        private static bool _sliderBroken;
        private static bool _drawFailed;

        /// <summary>True while the menu is shown.</summary>
        public static bool IsOpen { get; private set; }

        internal static bool DrawFailed => _drawFailed;

        internal static int DrawCount { get; private set; }

        /// <summary>
        /// True while Esc belongs to the menu: when it is open, and on the frame it closed (so the Esc that
        /// closed it doesn't also open the game's pause menu).
        /// </summary>
        internal static bool OwnsEscape => IsOpen || Time.frameCount - _closedFrame <= 1;

        private static int _closedFrame = -10;

        /// <summary>True while the native (uGUI) menu is the one shown.</summary>
        internal static bool IsNative => IsOpen && _native;

        /// <summary>Self-test switch: open the simple IMGUI menu even when the native one is available.</summary>
        internal static bool ForceSimple { get; set; }

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

        /// <summary>
        /// Adds a page for your mod. It is listed under MODS, in your mod's entry (next to its name, version and
        /// author). Calling it again with the same title returns the same page.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static ModMenuPage AddPage(string title) => AddPage(title, System.Reflection.Assembly.GetCallingAssembly());

        internal static ModMenuPage AddPage(string title, System.Reflection.Assembly caller)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Page title is empty.", nameof(title));
            if (caller == typeof(ModMenu).Assembly)
                caller = null;
            var existing = Pages.FirstOrDefault(p => p.Title == title && p.SourceAssembly == caller);
            if (existing != null)
                return existing;
            var page = new ModMenuPage(title) { SourceAssembly = caller };
            Pages.Add(page);
            return page;
        }

        /// <summary>Removes a page.</summary>
        public static void RemovePage(ModMenuPage page) => Pages.Remove(page);

        public static void Open() => SetOpen(true);

        public static void Close() => SetOpen(false);

        public static void Toggle() => SetOpen(!IsOpen);

        /// <summary>
        /// Opens the menu on just the MODS list (for the main menu's MODS line): Esc closes it from there, and the
        /// rest of the menu isn't reachable.
        /// </summary>
        internal static void OpenModsList()
        {
            if (IsOpen)
                return;
            _openOn = ModsPage;
            SetOpen(true);
        }

        // The page the next SetOpen(true) shows on its own (null: the normal top-level list).
        private static ModMenuPage _openOn;

        /// <summary>The pause-menu line that opens this menu (null when turned off in the preferences).</summary>
        internal static PauseMenu.Entry PauseEntry { get; private set; }

        internal static void Initialize()
        {
            if (FruktConfig.Category != null)
                AddPreferencesPage(FruktConfig.Category, "Library settings").ListLast = true;
            if (!FruktConfig.PauseMenuButton)
                return;
            var entry = PauseEntry = PauseMenu.AddButton("Mods", Open);
            // FruitLib adds its own MODS line; use a different word so the two can be told apart.
            GameEvents.SandboxReady += _ =>
            {
                bool fruitLib = MelonLoader.MelonBase.RegisteredMelons.Any(m => m.Info.Name == "FruitLib");
                entry.SetLabel(fruitLib ? "Mod menu" : "Mods");
            };
        }

        internal static void Update()
        {
            if (_native && NativeModMenu.Failed)
                _native = false;
            if (_native)
            {
                // A key pressed while a binding row is listening belongs to that row, not to the menu.
                bool capturing = NativeModMenu.CapturingKey;
                if (!capturing && FruktInput.GetKeyDown(Key.Escape))
                    NativeModMenu.Back();
                NativeModMenu.Update();
                if (!capturing && IsOpen && ToggleKey.WasPressed())
                    Close();
                return;
            }
            if (_capturing != null)
            {
                if (FruktInput.TryGetPressedKey(out var key))
                {
                    var item = _capturing;
                    _capturing = null;
                    if (key != Key.Escape)
                    {
                        var bind = key == Key.Backspace ? null : new KeyBind(key, FruktInput.CtrlHeld, FruktInput.ShiftHeld, FruktInput.AltHeld);
                        Invoke(item, () => item.SetKey(bind));
                    }
                }
                return;
            }
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
            _capturing = null;
            _subPages.Clear();
            _cursorOwner ??= new Il2CppSystem.Object();
            if (open)
            {
                LocalPlayer.CaptureCursor(_cursorOwner);
                var only = _openOn;
                _openOn = null;
                _native = FruktConfig.NativeStyle && !ForceSimple && NativeModMenu.Open(GameState.InSandbox ? "pause" : "frukt", only, only != null);
                if (!_native && only != null)
                {
                    // The simple menu has no single-page mode; start it on the MODS tab instead.
                    _selected = 0;
                }
            }
            else
            {
                _closedFrame = Time.frameCount;
                LocalPlayer.ReleaseCursor(_cursorOwner);
                NativeModMenu.Close();
                _native = false;
            }

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
            if (!IsOpen || _drawFailed || _native)
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
            var visiblePages = RootPages().Where(IsPageVisible).ToList();
            float height = Mathf.Min(Screen.height - 40f, 680f);
            var window = new Rect(20f, 20f, Width, height);
            GUI.DrawTexture(window, GuiStyles.Panel);
            GUI.DrawTexture(new Rect(window.x, window.y, window.width, 3f), GuiStyles.Accent);

            float x = window.x + Padding;
            float y = window.y + 8f;
            float innerWidth = Width - Padding * 2f;
            GUI.Label(new Rect(x, y, innerWidth, 24f), "<b>MOD MENU</b>", GuiStyles.Title);
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

            // Sub-pages (a mod under MODS, or a page's AddSubPage) open inside the selected tab.
            _subPages.RemoveAll(p => !IsPageVisible(p));
            var current = _subPages.Count > 0 ? _subPages[_subPages.Count - 1] : visiblePages[_selected];
            if (_subPages.Count > 0)
            {
                if (GUI.Button(new Rect(x, y, 90f, 24f), "< Back", GuiStyles.Button))
                {
                    _subPages.RemoveAt(_subPages.Count - 1);
                    _scroll = 0f;
                }
                GUI.Label(new Rect(x + 100f, y, innerWidth - 100f, 24f), $"<b>{current.Title}</b>", GuiStyles.Label);
                y += 30f;
            }

            var content = new Rect(x, y, innerWidth, window.yMax - y - Padding);
            HandleScroll(content);
            GUI.BeginGroup(content);
            _contentHeight = DrawItems(current, content.width, -_scroll);
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
                if (GUI.Button(rect, i == _selected ? $"<b>{title}</b>" : title, GuiStyles.Button) && (i != _selected || _subPages.Count > 0))
                {
                    _selected = i;
                    _subPages.Clear();
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
                float itemHeight = item.Kind switch
                {
                    ModMenuItemKind.Slider or ModMenuItemKind.Choice => 44f,
                    ModMenuItemKind.Button or ModMenuItemKind.Link => 30f,
                    ModMenuItemKind.Separator => 10f,
                    ModMenuItemKind.Label => 22f,
                    _ => 26f,
                };
                var rect = new Rect(0f, y, width, itemHeight);
                try
                {
                    DrawItem(item, rect);
                }
                catch (Exception e)
                {
                    GUI.Label(rect, $"<color=#ff7070>error: {e.Message}</color>", GuiStyles.Label);
                }
                y += itemHeight + 4f;
                if (!string.IsNullOrEmpty(item.Tooltip))
                {
                    GUI.Label(new Rect(0f, y - 2f, width, 18f), $"<size=11><color=#9a9a9a>{item.Tooltip}</color></size>", GuiStyles.Label);
                    y += 18f;
                }
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

                case ModMenuItemKind.Link:
                    if (GUI.Button(rect, item.SafeText + "  >", GuiStyles.Button))
                    {
                        _subPages.Add(item.Target);
                        _scroll = 0f;
                    }
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

                case ModMenuItemKind.Choice:
                {
                    GUI.Label(new Rect(rect.x, rect.y, rect.width, 20f), item.SafeText, GuiStyles.Label);
                    int chosen = item.GetInt();
                    float optionWidth = rect.width / Math.Max(1, item.Options.Count);
                    for (int i = 0; i < item.Options.Count; i++)
                    {
                        var option = new Rect(rect.x + i * optionWidth, rect.y + 20f, optionWidth - 4f, 22f);
                        if (i == chosen)
                            GUI.DrawTexture(new Rect(option.x, option.yMax - 2f, option.width, 2f), GuiStyles.Accent);
                        int index = i;
                        if (GUI.Button(option, i == chosen ? $"<b>{item.Options[i]}</b>" : item.Options[i], GuiStyles.Button) && i != chosen)
                            Invoke(item, () => item.SetInt(index));
                    }
                    break;
                }

                case ModMenuItemKind.KeyBinding:
                {
                    var bind = item.GetKey();
                    string key = _capturing == item ? "press a key..." : bind?.ToString() ?? "none";
                    GUI.Label(new Rect(rect.x, rect.y, rect.width - 130f, rect.height), item.SafeText, GuiStyles.Label);
                    if (GUI.Button(new Rect(rect.xMax - 126f, rect.y, 126f, rect.height), key, GuiStyles.Button))
                        _capturing = item;
                    break;
                }
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
