using System;
using FruktSharedLibrary.Controls;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FruktSharedLibrary.UI
{
    /// <summary>
    /// Builders for native-looking UI (Unity UI + TextMeshPro with FRUKT's fonts and colours). Everything is
    /// positioned in a 1920x1080 reference space with a top-left origin, like the game's own menus.
    /// </summary>
    public static class FruktUi
    {
        /// <summary>
        /// Creates a screen-space overlay canvas scaled like the game's UI. It survives scene loads and sits
        /// above the game's menus (sort order 5000 by default).
        /// </summary>
        public static Canvas CreateCanvas(string name, int sortingOrder = 5000, bool blockGameClicks = true)
        {
            var go = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.layer = 5; // UI
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (blockGameClicks)
                go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>An empty rect anchored to the parent's top-left corner.</summary>
        public static RectTransform CreateRect(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name);
            go.layer = 5;
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, x, y, width, height);
            return rect;
        }

        /// <summary>A rect that fills its parent.</summary>
        public static RectTransform CreateFill(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = 5;
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Positions a rect relative to its parent's top-left corner (y grows downwards).</summary>
        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>A solid-colour image.</summary>
        public static Image CreateImage(string name, Transform parent, Color color, float x, float y, float width, float height, bool raycastTarget = false)
        {
            var rect = CreateRect(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        /// <summary>A panel in the game's style: dark plate with the light cap strip along the top.</summary>
        public static RectTransform CreatePanel(string name, Transform parent, float x, float y, float width, float height, float capStrip = 15f)
        {
            var plate = CreateImage(name, parent, FruktTheme.Background, x, y, width, height, raycastTarget: true);
            if (capStrip > 0f)
                CreateImage("CapStrip", plate.transform, FruktTheme.Text, 0f, 0f, width, capStrip);
            return plate.rectTransform;
        }

        /// <summary>Text using one of the game's fonts.</summary>
        public static TextMeshProUGUI CreateText(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color,
            float x, float y, float width, float height, TextAlignmentOptions alignment = TextAlignmentOptions.Left, bool wrap = false)
        {
            var rect = CreateRect(name, parent, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.richText = true;
            label.raycastTarget = false;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.text = text ?? string.Empty;
            return label;
        }

        /// <summary>Display-font text (titles, menu lines).</summary>
        public static TextMeshProUGUI CreateDisplayText(string name, Transform parent, string text, float size, Color color, float x, float y, float width, float height)
            => CreateText(name, parent, text, FruktTheme.DisplayFont, size, color, x, y, width, height, TextAlignmentOptions.MidlineLeft);

        /// <summary>Mono-font text (labels, values, console lines).</summary>
        public static TextMeshProUGUI CreateMonoText(string name, Transform parent, string text, float size, Color color, float x, float y, float width, float height)
            => CreateText(name, parent, text, FruktTheme.MonoFont, size, color, x, y, width, height, TextAlignmentOptions.MidlineLeft);

        /// <summary>The thin square frame used by toggles and key chips.</summary>
        public static Image CreateFrame(string name, Transform parent, Color color, float x, float y, float width, float height)
        {
            var image = CreateImage(name, parent, color, x, y, width, height);
            var sprite = FruktTheme.BorderSprite;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
            else
            {
                // No border sprite: draw a hollow square from four thin bars instead.
                image.color = Color.clear;
                const float t = 4f;
                CreateImage("Top", image.transform, color, 0, 0, width, t);
                CreateImage("Bottom", image.transform, color, 0, height - t, width, t);
                CreateImage("Left", image.transform, color, 0, 0, t, height);
                CreateImage("Right", image.transform, color, width - t, 0, t, height);
            }
            return image;
        }

        /// <summary>
        /// True while the mouse is over the rect. Uses the input system directly, so it works regardless of
        /// which event system or raycaster is active.
        /// </summary>
        public static bool IsHovered(RectTransform rect)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy)
                return false;
            try
            {
                return RectTransformUtility.RectangleContainsScreenPoint(rect, FruktInput.MousePosition, null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Converts the mouse position into a rect's local space (pivot-relative).</summary>
        public static bool TryGetLocalMouse(RectTransform rect, out Vector2 local)
        {
            local = default;
            try
            {
                return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, FruktInput.MousePosition, null, out local);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Formats a label the way the game's settings do: "Mouse sensitivity" → "mouse_sensitivity:".</summary>
        public static string SettingLabel(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var trimmed = text.Trim().TrimEnd(':').ToLowerInvariant().Replace(' ', '_');
            return trimmed + ":";
        }

        /// <summary>
        /// A menu-line label like the game's buttons: "&gt; WORD _" with the arrow and cursor only visible when
        /// <paramref name="hovered"/>.
        /// </summary>
        public static string MenuLine(string word, bool hovered)
            => hovered
                ? $"> {word.ToUpperInvariant()}_"
                : $"<alpha=#00>> <alpha=#FF>{word.ToUpperInvariant()}<alpha=#00>_";

        internal static void Destroy(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            try
            {
                UnityEngine.Object.Destroy(obj);
            }
            catch (Exception)
            {
                // Already gone.
            }
        }
    }
}
