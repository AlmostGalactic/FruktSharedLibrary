using System;
using System.Collections.Generic;
using System.Text;
using FruktSharedLibrary.Controls;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using Il2CppInfrastructure.Project.AssetsHandlers.SFX;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FruktSharedLibrary.UI
{
    /// <summary>
    /// The mod menu drawn with Unity UI in FRUKT's own style (the layout of the game's settings screens).
    /// Input is polled directly, so it works on top of any game menu.
    /// </summary>
    internal static class NativeModMenu
    {
        // Layout, in the 1920x1080 reference space (measured from the game's settings screens).
        private const float Left = 256f;
        private const float ControlX = Left + 628f;
        private const float RowWidth = 1300f;
        private const float ViewTop = 330f;
        private const float ViewBottom = 860f;
        private const float RowHeight = 72f;
        private const float RowGap = 22f;
        private const float LabelSize = 40f;
        private const float ValueSize = 28f;
        private const float SliderWidth = 548f;

        private sealed class Row
        {
            public ModMenuItem Item;
            public ModMenuPage TargetPage;
            public RectTransform Rect;
            public RectTransform Hit;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Value;
            public TextMeshProUGUI Tooltip;
            public Image ToggleInner;
            public Image ToggleFrame;
            public RectTransform Track;
            public RectTransform Fill;
            public RectTransform Handle;
            public RectTransform KeyChip;
            public Image KeyFrame;
            public readonly List<(TextMeshProUGUI Text, RectTransform Rect)> Options = new();
            public string LastText;
            public bool Hovered;
        }

        private static Canvas _canvas;
        private static CanvasGroup _group;
        private static RectTransform _screen;
        private static RectTransform _viewport;
        private static RectTransform _content;
        private static RectTransform _escChip;
        private static TextMeshProUGUI _trail;
        private static TextMeshProUGUI _title;
        private static TextMeshProUGUI _console;
        private static TextMeshProUGUI _escAction;
        private static Image _scrollHandle;
        private static Image _scrollTrack;
        private static readonly List<Row> Rows = new();

        // Pages opened from the top level, innermost last (empty = the top-level list).
        private static readonly List<ModMenuPage> Stack = new();
        private static ModMenuPage _page => Stack.Count == 0 ? null : Stack[Stack.Count - 1];
        private static int _modCount;
        private static string _origin = "frukt";
        private static string _layoutSignature;
        private static float _scroll;
        private static float _contentHeight;
        private static float _openedAt;
        private static Row _dragging;
        private static Row _capturing;
        private static bool _failed;

        internal static bool IsOpen { get; private set; }

        internal static bool Failed => _failed;

        /// <summary>Opens the menu. <paramref name="origin"/> is the breadcrumb root ("pause" or "frukt").</summary>
        internal static bool Open(string origin, ModMenuPage page = null)
        {
            if (!EnsureBuilt())
                return false;
            _origin = string.IsNullOrEmpty(origin) ? "frukt" : origin;
            Stack.Clear();
            if (page != null)
                Stack.Add(page);
            _scroll = 0f;
            _layoutSignature = null;
            _capturing = null;
            _dragging = null;
            IsOpen = true;
            _openedAt = Time.unscaledTime;
            _canvas.gameObject.SetActive(true);
            _group.alpha = 0f;
            Sounds.Play(UISFXType.WindowOpenClose, 0.6f);
            Update();
            return true;
        }

        internal static void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            _capturing = null;
            _dragging = null;
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
            Sounds.Play(UISFXType.WindowOpenClose, 0.6f);
        }

        /// <summary>Esc behaviour: cancel key capture, leave a page, or close.</summary>
        internal static void Back()
        {
            if (_capturing != null)
            {
                _capturing = null;
                return;
            }
            if (_page != null)
            {
                Stack.RemoveAt(Stack.Count - 1);
                _scroll = 0f;
                _layoutSignature = null;
                Sounds.Play(UISFXType.SmallButtonClick, 0.7f);
                return;
            }
            ModMenu.Close();
        }

        internal static void Update()
        {
            if (!IsOpen || _failed)
                return;
            try
            {
                if (_canvas == null)
                {
                    IsOpen = false;
                    return;
                }
                _group.alpha = Mathf.Clamp01((Time.unscaledTime - _openedAt) / 0.12f);
                ModMenu.RefreshTree();
                while (_page != null && !SafeVisible(_page))
                    Stack.RemoveAt(Stack.Count - 1);

                RebuildIfNeeded();
                HandleKeyCapture();
                HandleScroll();
                HandlePointer();
                Refresh();
            }
            catch (Exception e)
            {
                _failed = true;
                FruktLog.Error("The native mod menu failed; falling back to the simple menu", e);
                Close();
            }
        }

        // ------------------------------------------------------------ building

        private static bool EnsureBuilt()
        {
            if (_canvas != null)
                return true;
            if (_failed || !FruktTheme.Available)
                return false;
            try
            {
                _canvas = FruktUi.CreateCanvas("FruktSharedLibrary.ModMenu", 5000);
                _group = _canvas.gameObject.AddComponent<CanvasGroup>();
                _screen = FruktUi.CreateFill("Screen", _canvas.transform);

                var backing = FruktUi.CreateFill("Backing", _screen).gameObject.AddComponent<Image>();
                backing.color = FruktTheme.Background;
                backing.raycastTarget = true;
                FruktUi.CreateImage("CapStrip", _screen, FruktTheme.Text, 0f, 0f, 1920f, 15f);

                _console = FruktUi.CreateMonoText("ConsoleLine", _screen, "", 26f, FruktTheme.Dim, 60f, 52f, 900f, 40f);
                _trail = FruktUi.CreateMonoText("Trail", _screen, "", 26f, FruktTheme.Dim, 234f, 143f, 1200f, 40f);
                _title = FruktUi.CreateDisplayText("Current", _screen, "MOD MENU", 64f, FruktTheme.Text, 234f, 180f, 1400f, 110f);

                _viewport = FruktUi.CreateRect("Viewport", _screen, 0f, ViewTop, 1920f, ViewBottom - ViewTop);
                _viewport.gameObject.AddComponent<RectMask2D>();
                _content = FruktUi.CreateRect("Content", _viewport, 0f, 0f, 1920f, 10f);

                _scrollTrack = FruktUi.CreateImage("ScrollBar", _screen, FruktTheme.Line, 1920f - 75f, ViewTop, 4f, ViewBottom - ViewTop);
                _scrollHandle = FruktUi.CreateImage("Handle", _screen, FruktTheme.Text, 1920f - 80f, ViewTop, 14f, 100f);

                _escChip = FruktUi.CreateRect("EscChip", _screen, 51f, 875f, 300f, 50f);
                FruktUi.CreateFrame("KeyFrame", _escChip, FruktTheme.Text, 0f, 3f, 70f, 44f);
                FruktUi.CreateText("KeyName", _escChip, "ESC", FruktTheme.DisplayFont, 26f, FruktTheme.Text, 0f, 3f, 70f, 44f, TextAlignmentOptions.Center);
                _escAction = FruktUi.CreateDisplayText("ActionName", _escChip, "CLOSE", 30f, new Color(FruktTheme.Text.r, FruktTheme.Text.g, FruktTheme.Text.b, 0.4f), 88f, 3f, 200f, 44f);

                _canvas.gameObject.SetActive(false);
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                FruktLog.Error("Building the native mod menu failed; using the simple menu instead", e);
                FruktUi.Destroy(_canvas?.gameObject);
                _canvas = null;
                return false;
            }
        }

        private static void RebuildIfNeeded()
        {
            string signature = Signature();
            if (signature == _layoutSignature)
                return;
            _layoutSignature = signature;
            _dragging = null;
            _capturing = null;

            foreach (var row in Rows)
                FruktUi.Destroy(row.Rect.gameObject);
            Rows.Clear();

            float y = 0f;
            _modCount = ModMenu.LibraryMods.Count;
            if (_page == null)
            {
                foreach (var page in VisiblePages())
                {
                    var row = BuildMenuLine(page.Title, y);
                    row.TargetPage = page;
                    Rows.Add(row);
                    y += 66f + 39f;
                }
                if (Rows.Count == 0)
                {
                    var row = BuildLabel(new ModMenuItem(ModMenuItemKind.Label, () => "no mod has added a page yet."), y);
                    Rows.Add(row);
                    y += row.Rect.sizeDelta.y + RowGap;
                }
            }
            else
            {
                foreach (var item in _page.Items)
                {
                    if (!item.IsVisible)
                        continue;
                    var row = Build(item, y);
                    Rows.Add(row);
                    y += row.Rect.sizeDelta.y + RowGap;
                }
            }

            _contentHeight = y;
            _content.sizeDelta = new Vector2(1920f, Math.Max(10f, y));
            ClampScroll();
        }

        private static Row Build(ModMenuItem item, float y)
        {
            switch (item.Kind)
            {
                case ModMenuItemKind.Header:
                {
                    var rect = FruktUi.CreateRect("Header", _content, Left, y + 14f, RowWidth, 44f);
                    var text = FruktUi.CreateMonoText("Text", rect, "", 26f, FruktTheme.Muted, 0f, 0f, RowWidth, 44f);
                    FruktUi.CreateImage("Rule", rect, FruktTheme.Line, 0f, 42f, RowWidth, 2f);
                    rect.sizeDelta = new Vector2(RowWidth, 58f);
                    return new Row { Item = item, Rect = rect, Label = text };
                }
                case ModMenuItemKind.Label:
                    return BuildLabel(item, y);
                case ModMenuItemKind.Separator:
                {
                    var rect = FruktUi.CreateRect("Separator", _content, Left, y, RowWidth, 10f);
                    FruktUi.CreateImage("Line", rect, FruktTheme.Line, 0f, 4f, RowWidth, 2f);
                    return new Row { Item = item, Rect = rect };
                }
                case ModMenuItemKind.Button:
                {
                    var row = BuildMenuLine(item.SafeText, y);
                    row.Item = item;
                    return WithTooltip(row);
                }
                case ModMenuItemKind.Link:
                {
                    var row = BuildMenuLine(item.SafeText, y);
                    row.Item = item;
                    row.TargetPage = item.Target;
                    return WithTooltip(row);
                }
                case ModMenuItemKind.Toggle:
                {
                    var row = BuildSettingRow(item, y);
                    row.ToggleFrame = FruktUi.CreateFrame("Box", row.Rect, FruktTheme.Frame, ControlX - Left, 6f, 60f, 60f);
                    row.ToggleInner = FruktUi.CreateImage("Inner", row.ToggleFrame.transform, FruktTheme.Accent, 15f, 15f, 30f, 30f);
                    row.Value = FruktUi.CreateMonoText("StateWord", row.Rect, "off", ValueSize, FruktTheme.Dim, ControlX - Left + 100f, 0f, 200f, RowHeight);
                    row.Hit = FruktUi.CreateRect("Hit", row.Rect, 0f, 0f, ControlX - Left + 300f, RowHeight);
                    return WithTooltip(row);
                }
                case ModMenuItemKind.Slider:
                {
                    var row = BuildSettingRow(item, y);
                    float cx = ControlX - Left;
                    row.Track = FruktUi.CreateImage("Track", row.Rect, FruktTheme.Text, cx, RowHeight / 2f - 7.5f, SliderWidth, 15f).rectTransform;
                    row.Fill = FruktUi.CreateImage("Fill", row.Rect, FruktTheme.Accent, cx, RowHeight / 2f - 7.5f, 0f, 15f).rectTransform;
                    row.Handle = FruktUi.CreateImage("Handle", row.Rect, FruktTheme.Text, cx, RowHeight / 2f - 22f, 18f, 44f).rectTransform;
                    row.Value = FruktUi.CreateMonoText("Value", row.Rect, "", ValueSize, FruktTheme.Muted, cx + SliderWidth + 40f, 0f, 200f, RowHeight);
                    row.Hit = FruktUi.CreateRect("Hit", row.Rect, cx - 10f, 0f, SliderWidth + 20f, RowHeight);
                    return WithTooltip(row);
                }
                case ModMenuItemKind.Choice:
                {
                    var row = BuildSettingRow(item, y);
                    float x = ControlX - Left;
                    for (int i = 0; i < item.Options.Count; i++)
                    {
                        var word = FruktUi.CreateMonoText("Option" + i, row.Rect, item.Options[i], ValueSize, FruktTheme.Muted, x, 0f, 400f, RowHeight);
                        float width = Mathf.Max(30f, word.preferredWidth);
                        word.rectTransform.sizeDelta = new Vector2(width, RowHeight);
                        row.Options.Add((word, word.rectTransform));
                        x += width + 36f;
                    }
                    return WithTooltip(row);
                }
                case ModMenuItemKind.KeyBinding:
                {
                    var row = BuildSettingRow(item, y);
                    row.KeyChip = FruktUi.CreateRect("KeyChip", row.Rect, ControlX - Left, 14f, 160f, 44f);
                    row.KeyFrame = FruktUi.CreateFrame("Frame", row.KeyChip, FruktTheme.Text, 0f, 0f, 160f, 44f);
                    row.Value = FruktUi.CreateText("Key", row.KeyChip, "", FruktTheme.DisplayFont, 26f, FruktTheme.Text, 0f, 0f, 160f, 44f, TextAlignmentOptions.Center);
                    row.Hit = row.KeyChip;
                    return WithTooltip(row);
                }
                default:
                    return BuildLabel(item, y);
            }
        }

        private static Row BuildSettingRow(ModMenuItem item, float y)
        {
            var rect = FruktUi.CreateRect(item.Kind.ToString(), _content, Left, y, RowWidth, RowHeight);
            var label = FruktUi.CreateMonoText("Label", rect, "", LabelSize, FruktTheme.Text, 0f, 0f, ControlX - Left - 30f, RowHeight);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return new Row { Item = item, Rect = rect, Label = label };
        }

        private static Row BuildLabel(ModMenuItem item, float y)
        {
            var rect = FruktUi.CreateRect("Label", _content, Left, y, RowWidth, 40f);
            var text = FruktUi.CreateText("Text", rect, item.SafeText, FruktTheme.MonoFont, ValueSize, FruktTheme.Muted, 0f, 0f, RowWidth, 40f, TextAlignmentOptions.TopLeft, wrap: true);
            float height = Mathf.Max(40f, text.preferredHeight + 4f);
            rect.sizeDelta = new Vector2(RowWidth, height);
            text.rectTransform.sizeDelta = new Vector2(RowWidth, height);
            return new Row { Item = item, Rect = rect, Label = text };
        }

        private static Row BuildMenuLine(string word, float y)
        {
            var rect = FruktUi.CreateRect("MenuLine", _content, Left, y, 900f, 66f);
            var plate = FruktUi.CreateImage("Plate", rect, Color.clear, 0f, 0f, 900f, 66f);
            var label = FruktUi.CreateDisplayText("Label", rect, FruktUi.MenuLine(word, false), 34f, FruktTheme.Text, 11f, 0f, 880f, 66f);
            float width = Mathf.Min(900f, label.preferredWidth + 22f);
            plate.rectTransform.sizeDelta = new Vector2(width, 66f);
            return new Row { Rect = rect, Label = label, Hit = plate.rectTransform, ToggleFrame = plate, LastText = word };
        }

        private static Row WithTooltip(Row row)
        {
            if (string.IsNullOrEmpty(row.Item?.Tooltip))
                return row;
            float baseHeight = row.Rect.sizeDelta.y;
            row.Tooltip = FruktUi.CreateText("Tooltip", row.Rect, row.Item.Tooltip, FruktTheme.MonoFont, 22f, FruktTheme.Dim, 0f, baseHeight, RowWidth, 30f, TextAlignmentOptions.TopLeft, wrap: true);
            float tipHeight = Mathf.Max(28f, row.Tooltip.preferredHeight);
            row.Tooltip.rectTransform.sizeDelta = new Vector2(RowWidth, tipHeight);
            row.Rect.sizeDelta = new Vector2(row.Rect.sizeDelta.x, baseHeight + tipHeight);
            return row;
        }

        // ------------------------------------------------------------ per-frame

        private static void Refresh()
        {
            _console.text = $"fsl v{FruktSharedLibraryMod.Version} | mods: {_modCount}";
            _trail.text = Trail();
            _title.text = (_page?.Title ?? "MOD MENU").ToUpperInvariant();
            _escAction.text = _page == null ? "CLOSE" : "BACK";

            foreach (var row in Rows)
            {
                if (row.Item == null && row.TargetPage == null)
                    continue;
                if (row.TargetPage != null || row.Item?.Kind == ModMenuItemKind.Button)
                {
                    string word = row.Item != null ? row.Item.SafeText : row.TargetPage.Title;
                    row.Label.text = FruktUi.MenuLine(word, row.Hovered);
                    row.Label.color = FruktTheme.Text;
                    row.ToggleFrame.color = row.Hovered ? new Color(1f, 1f, 1f, 0.05f) : Color.clear;
                    continue;
                }

                var item = row.Item;
                switch (item.Kind)
                {
                    case ModMenuItemKind.Header:
                        SetText(row, item.SafeText.ToLowerInvariant() + " /");
                        break;
                    case ModMenuItemKind.Label:
                        SetText(row, item.SafeText);
                        break;
                    case ModMenuItemKind.Toggle:
                    {
                        SetText(row, FruktUi.SettingLabel(item.SafeText));
                        bool on = Safe(item.GetBool);
                        row.ToggleInner.enabled = on;
                        row.ToggleFrame.color = row.Hovered ? FruktTheme.Text : FruktTheme.Frame;
                        row.Value.text = on ? "on" : "off";
                        row.Value.color = on ? FruktTheme.Text : FruktTheme.Dim;
                        break;
                    }
                    case ModMenuItemKind.Slider:
                    {
                        SetText(row, FruktUi.SettingLabel(item.SafeText));
                        float value = Safe(item.GetFloat);
                        float t = Mathf.InverseLerp(item.Min, item.Max, value);
                        row.Fill.sizeDelta = new Vector2(SliderWidth * t, 15f);
                        row.Handle.anchoredPosition = new Vector2(ControlX - Left + (SliderWidth - 18f) * t, -(RowHeight / 2f - 22f));
                        row.Handle.GetComponent<Image>().color = row.Hovered || _dragging == row ? Color.white : FruktTheme.Text;
                        row.Value.text = item.FormatValue(value);
                        break;
                    }
                    case ModMenuItemKind.Choice:
                    {
                        SetText(row, FruktUi.SettingLabel(item.SafeText));
                        int chosen = Safe(item.GetInt);
                        for (int i = 0; i < row.Options.Count; i++)
                        {
                            bool hovered = FruktUi.IsHovered(row.Options[i].Rect);
                            row.Options[i].Text.color = i == chosen ? FruktTheme.Accent : hovered ? FruktTheme.Text : FruktTheme.Muted;
                        }
                        break;
                    }
                    case ModMenuItemKind.KeyBinding:
                    {
                        SetText(row, FruktUi.SettingLabel(item.SafeText));
                        bool capturing = _capturing == row;
                        var bind = Safe(item.GetKey);
                        row.Value.text = capturing ? "PRESS A KEY" : bind == null ? "NONE" : bind.ToString().ToUpperInvariant();
                        row.Value.color = capturing ? FruktTheme.Accent : FruktTheme.Text;
                        row.KeyFrame.color = capturing ? FruktTheme.Accent : row.Hovered ? Color.white : FruktTheme.Text;
                        float width = Mathf.Max(120f, row.Value.preferredWidth + 32f);
                        row.KeyChip.sizeDelta = new Vector2(width, 44f);
                        row.KeyFrame.rectTransform.sizeDelta = new Vector2(width, 44f);
                        row.Value.rectTransform.sizeDelta = new Vector2(width, 44f);
                        break;
                    }
                }
            }

            float viewHeight = ViewBottom - ViewTop;
            bool scrollable = _contentHeight > viewHeight + 1f;
            _scrollTrack.enabled = scrollable;
            _scrollHandle.enabled = scrollable;
            if (scrollable)
            {
                float handleHeight = Mathf.Max(60f, viewHeight * viewHeight / _contentHeight);
                float t = _scroll / (_contentHeight - viewHeight);
                _scrollHandle.rectTransform.anchoredPosition = new Vector2(1920f - 80f, -(ViewTop + (viewHeight - handleHeight) * t));
                _scrollHandle.rectTransform.sizeDelta = new Vector2(14f, handleHeight);
            }
        }

        private static void HandlePointer()
        {
            bool pressed = FruktInput.GetMouseButtonDown(0);
            bool held = FruktInput.GetMouseButton(0);

            if (_dragging != null)
            {
                if (!held)
                {
                    _dragging = null;
                }
                else
                {
                    DragSlider(_dragging);
                    return;
                }
            }

            foreach (var row in Rows)
                row.Hovered = row.Hit != null && FruktUi.IsHovered(row.Hit) && FruktUi.IsHovered(_viewport);

            if (pressed && FruktUi.IsHovered(_escChip))
            {
                Back();
                return;
            }
            if (!pressed)
                return;

            foreach (var row in Rows)
            {
                if (row.TargetPage != null && row.Hovered)
                {
                    Stack.Add(row.TargetPage);
                    _scroll = 0f;
                    _layoutSignature = null;
                    Sounds.Play(UISFXType.LargeButtonClick, 0.8f);
                    return;
                }
                var item = row.Item;
                if (item == null)
                    continue;

                if (item.Kind == ModMenuItemKind.Choice && FruktUi.IsHovered(_viewport))
                {
                    for (int i = 0; i < row.Options.Count; i++)
                    {
                        if (!FruktUi.IsHovered(row.Options[i].Rect))
                            continue;
                        Invoke(item, () => item.SetInt(i));
                        Sounds.Play(UISFXType.SmallButtonClick, 0.8f);
                        return;
                    }
                    continue;
                }

                if (!row.Hovered)
                    continue;
                switch (item.Kind)
                {
                    case ModMenuItemKind.Button:
                        Sounds.Play(UISFXType.LargeButtonClick, 0.8f);
                        Invoke(item, item.OnClick);
                        return;
                    case ModMenuItemKind.Toggle:
                    {
                        bool next = !Safe(item.GetBool);
                        Invoke(item, () => item.SetBool(next));
                        Sounds.Play(next ? UISFXType.SwitchOn : UISFXType.SwitchOff, 0.8f);
                        return;
                    }
                    case ModMenuItemKind.Slider:
                        _dragging = row;
                        Sounds.Play(UISFXType.SliderHoldClick, 0.6f);
                        DragSlider(row);
                        return;
                    case ModMenuItemKind.KeyBinding:
                        _capturing = row;
                        Sounds.Play(UISFXType.SmallButtonClick, 0.8f);
                        return;
                }
            }
        }

        private static void DragSlider(Row row)
        {
            if (!FruktUi.TryGetLocalMouse(row.Track, out var local))
                return;
            // Track pivot is top-left, so local.x runs 0..width.
            float t = Mathf.Clamp01(local.x / SliderWidth);
            var item = row.Item;
            float value = Mathf.Lerp(item.Min, item.Max, t);
            if (item.Format == "0")
                value = Mathf.Round(value);
            if (!Mathf.Approximately(value, Safe(item.GetFloat)))
                Invoke(item, () => item.SetFloat(item.ClampedValue(value)));
        }

        private static void HandleKeyCapture()
        {
            if (_capturing == null || !FruktInput.TryGetPressedKey(out var key))
                return;
            var item = _capturing.Item;
            _capturing = null;
            if (key == Key.Escape)
                return; // Escape cancels.
            var bind = key == Key.Backspace ? null : new KeyBind(key, FruktInput.CtrlHeld, FruktInput.ShiftHeld, FruktInput.AltHeld);
            Invoke(item, () => item.SetKey(bind));
            Sounds.Play(UISFXType.SwitchOn, 0.8f);
        }

        private static void HandleScroll()
        {
            float delta = FruktInput.ScrollDelta;
            if (Mathf.Abs(delta) < 0.01f || !FruktUi.IsHovered(_viewport))
                return;
            _scroll -= Mathf.Sign(delta) * 90f;
            ClampScroll();
        }

        private static void ClampScroll()
        {
            float max = Mathf.Max(0f, _contentHeight - (ViewBottom - ViewTop));
            _scroll = Mathf.Clamp(_scroll, 0f, max);
            if (_content != null)
                _content.anchoredPosition = new Vector2(0f, _scroll);
        }

        /// <summary>True while a key-binding row waits for a key press.</summary>
        internal static bool CapturingKey => _capturing != null;

        // ------------------------------------------------------------ self-test access

        internal static ModMenuPage CurrentPage => _page;

        internal static float ScrollOffset => _scroll;

        internal static int RowCount => Rows.Count;

        internal static RectTransform EscChip => _escChip;

        internal static RectTransform Viewport => _viewport;

        internal static void ShowPage(ModMenuPage page)
        {
            Stack.Clear();
            if (page != null)
                Stack.Add(page);
            _scroll = 0f;
            _layoutSignature = null;
            Update();
        }

        /// <summary>Scrolls so the row of <paramref name="item"/> sits at the top of the view.</summary>
        internal static void ScrollTo(ModMenuItem item)
        {
            var row = Rows.Find(r => r.Item == item);
            if (row == null)
                return;
            _scroll = -row.Rect.anchoredPosition.y;
            ClampScroll();
        }

        internal static RectTransform HitForPage(ModMenuPage page) => Rows.Find(r => r.TargetPage == page)?.Hit;

        internal static RectTransform HitFor(ModMenuItem item) => Rows.Find(r => r.Item == item)?.Hit;

        internal static RectTransform TrackFor(ModMenuItem item) => Rows.Find(r => r.Item == item)?.Track;

        internal static RectTransform OptionFor(ModMenuItem item, int index)
        {
            var row = Rows.Find(r => r.Item == item);
            return row != null && index >= 0 && index < row.Options.Count ? row.Options[index].Rect : null;
        }

        // ------------------------------------------------------------ helpers

        private static string Signature()
        {
            var builder = new StringBuilder();
            if (_page == null)
            {
                builder.Append("root:");
                foreach (var page in VisiblePages())
                    builder.Append(page.Title).Append('|');
                return builder.ToString();
            }
            builder.Append(_page.Title).Append(':').Append(_page.Version).Append(':');
            foreach (var item in _page.Items)
                builder.Append(item.IsVisible ? '1' : '0');
            return builder.ToString();
        }

        private static IEnumerable<ModMenuPage> VisiblePages()
        {
            foreach (var page in ModMenu.RootPages())
            {
                if (SafeVisible(page))
                    yield return page;
            }
        }

        /// <summary>Breadcrumb like the game's: "pause / mod menu / mods /".</summary>
        private static string Trail()
        {
            if (Stack.Count == 0)
                return _origin + " /";
            var builder = new StringBuilder(_origin).Append(" / mod menu / ");
            for (int i = 0; i < Stack.Count - 1; i++)
                builder.Append(Stack[i].Title.ToLowerInvariant()).Append(" / ");
            return builder.ToString().TrimEnd();
        }

        /// <summary>The pages opened from the top level, outermost first (self-test access).</summary>
        internal static IReadOnlyList<ModMenuPage> PageStack => Stack;

        private static bool SafeVisible(ModMenuPage page)
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

        private static void SetText(Row row, string text)
        {
            if (row.Label == null || row.LastText == text)
                return;
            row.LastText = text;
            row.Label.text = text;
        }

        private static T Safe<T>(Func<T> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return default;
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
