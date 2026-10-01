using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FruktSharedLibrary.UI
{
    /// <summary>
    /// Short on-screen messages that fade out on their own. Drawn as FRUKT-style plates in the top-right
    /// corner (or as simple boxes at the top centre when the native style is off or unavailable).
    /// </summary>
    public static class Notifications
    {
        private const int MaxVisible = 6;
        private const float FadeTime = 0.4f;

        private sealed class Message
        {
            public string Text;
            public Color Color;
            public float Created;
            public float Duration;
            public bool Warning;
            public RectTransform View;
            public CanvasGroup Group;
        }

        // Native layout, in the 1920x1080 reference space.
        private const float PlateWidth = 520f;
        private const float PlateRight = 57f;
        private const float PlateTop = 57f;
        private const float CapHeight = 10f;
        private const float TextPadding = 18f;

        private static readonly List<Message> Messages = new();
        private static bool _drawFailed;

        internal static bool DrawFailed => _drawFailed;

        /// <summary>Shows a message for <paramref name="seconds"/> (real time, unaffected by pause/slow motion).</summary>
        public static void Show(string text, float seconds = 3f) => Show(text, Color.white, seconds);

        /// <summary>Shows a coloured message.</summary>
        public static void Show(string text, Color color, float seconds = 3f)
        {
            if (string.IsNullOrEmpty(text))
                return;
            FruktLog.Debug("Notification: " + text);
            Add(new Message { Text = text, Color = color, Created = Time.realtimeSinceStartup, Duration = Math.Max(0.5f, seconds) });
        }

        /// <summary>Shows a warning-coloured message.</summary>
        public static void Warn(string text, float seconds = 4f)
        {
            if (string.IsNullOrEmpty(text))
                return;
            FruktLog.Debug("Notification (warning): " + text);
            Add(new Message { Text = text, Color = WarningColor, Warning = true, Created = Time.realtimeSinceStartup, Duration = Math.Max(0.5f, seconds) });
        }

        /// <summary>Removes every message.</summary>
        public static void Clear()
        {
            foreach (var message in Messages)
                DestroyView(message);
            Messages.Clear();
        }

        private static readonly Color WarningColor = new(1f, 0.75f, 0.3f);
        private static Canvas _canvas;
        private static bool _nativeFailed;

        /// <summary>True when the messages are drawn natively (game fonts, uGUI).</summary>
        internal static bool UsingNative => !_nativeFailed && FruktConfig.NativeStyle && FruktTheme.Available;

        /// <summary>How many messages currently have a native plate on screen.</summary>
        internal static int NativeViewCount => Messages.FindAll(m => m.View != null).Count;

        private static void Add(Message message)
        {
            Messages.Add(message);
            while (Messages.Count > MaxVisible * 2)
            {
                DestroyView(Messages[0]);
                Messages.RemoveAt(0);
            }
        }

        // ------------------------------------------------------------ native

        internal static void Update()
        {
            if (Messages.Count == 0 && _canvas == null)
                return;
            float now = Time.realtimeSinceStartup;
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                if (now - Messages[i].Created > Messages[i].Duration)
                {
                    DestroyView(Messages[i]);
                    Messages.RemoveAt(i);
                }
            }
            if (!UsingNative || !FruktConfig.ShowNotifications)
            {
                if (_canvas != null)
                    _canvas.gameObject.SetActive(false);
                return;
            }
            try
            {
                LayoutNative(now);
            }
            catch (Exception e)
            {
                _nativeFailed = true;
                FruktLog.Error("Native notifications failed; using the simple style", e);
                foreach (var message in Messages)
                    DestroyView(message);
                FruktUi.Destroy(_canvas?.gameObject);
                _canvas = null;
            }
        }

        private static void LayoutNative(float now)
        {
            if (_canvas == null)
                _canvas = FruktUi.CreateCanvas("FruktSharedLibrary.Notifications", 5100, blockGameClicks: false);
            _canvas.gameObject.SetActive(Messages.Count > 0);
            float y = PlateTop;
            int start = Math.Max(0, Messages.Count - MaxVisible);
            for (int i = 0; i < Messages.Count; i++)
            {
                var message = Messages[i];
                if (i < start)
                {
                    DestroyView(message);
                    continue;
                }
                message.View ??= BuildView(message);
                float age = now - message.Created;
                message.Group.alpha = Mathf.Clamp01(Mathf.Min(age / FadeTime, (message.Duration - age) / FadeTime));
                // Slide in from the right as it fades in.
                float slide = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / FadeTime))) * 40f;
                message.View.anchoredPosition = new Vector2(1920f - PlateRight - PlateWidth + slide, -y);
                y += message.View.sizeDelta.y + 12f;
            }
        }

        private static RectTransform BuildView(Message message)
        {
            var plate = FruktUi.CreatePanel("Notification", _canvas.transform, 0f, 0f, PlateWidth, 60f, CapHeight);
            plate.GetComponent<Image>().raycastTarget = false;
            var cap = plate.Find("CapStrip")?.GetComponent<Image>();
            if (cap != null && (message.Warning || message.Color != Color.white))
                cap.color = message.Warning ? FruktTheme.Accent : message.Color;
            var text = FruktUi.CreateText("Text", plate, message.Text, FruktTheme.MonoFont, 22f,
                message.Warning ? FruktTheme.Accent : FruktTheme.Text,
                TextPadding, CapHeight + 12f, PlateWidth - TextPadding * 2f, 30f, TextAlignmentOptions.TopLeft, wrap: true);
            float textHeight = Mathf.Max(26f, text.preferredHeight);
            text.rectTransform.sizeDelta = new Vector2(PlateWidth - TextPadding * 2f, textHeight);
            plate.sizeDelta = new Vector2(PlateWidth, CapHeight + 12f + textHeight + 14f);
            message.Group = plate.gameObject.AddComponent<CanvasGroup>();
            message.Group.alpha = 0f;
            return plate;
        }

        private static void DestroyView(Message message)
        {
            if (message.View != null)
                FruktUi.Destroy(message.View.gameObject);
            message.View = null;
            message.Group = null;
        }

        // ------------------------------------------------------------ simple (IMGUI) fallback

        internal static void Draw()
        {
            if (_drawFailed || Messages.Count == 0 || UsingNative)
                return;
            float now = Time.realtimeSinceStartup;
            Messages.RemoveAll(m => now - m.Created > m.Duration);
            if (Messages.Count == 0 || !FruktConfig.ShowNotifications)
                return;
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            try
            {
                var oldColor = GUI.color;
                float width = Mathf.Min(560f, Screen.width - 40f);
                float x = (Screen.width - width) / 2f;
                float y = 24f;
                int start = Math.Max(0, Messages.Count - MaxVisible);
                for (int i = start; i < Messages.Count; i++)
                {
                    var message = Messages[i];
                    float age = now - message.Created;
                    float alpha = Mathf.Clamp01(Mathf.Min(age / FadeTime, (message.Duration - age) / FadeTime));
                    float height = MeasureHeight(message.Text, width - 24f);
                    var rect = new Rect(x, y, width, height);

                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    GUI.DrawTexture(rect, GuiStyles.Panel);
                    GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), GuiStyles.Accent);
                    GUI.color = new Color(message.Color.r, message.Color.g, message.Color.b, alpha);
                    GUI.Label(new Rect(rect.x + 12f, rect.y, rect.width - 24f, rect.height), message.Text, GuiStyles.Notification);
                    y += height + 6f;
                }
                GUI.color = oldColor;
            }
            catch (Exception e)
            {
                _drawFailed = true;
                FruktLog.Error("Drawing notifications failed; notifications are disabled", e);
            }
        }

        private static bool _measureFailed;

        private static float MeasureHeight(string text, float width)
        {
            if (!_measureFailed)
            {
                try
                {
                    return Mathf.Max(34f, GuiStyles.Notification.CalcHeight(new GUIContent(text), width) + 12f);
                }
                catch
                {
                    _measureFailed = true;
                }
            }
            int lines = 1 + text.Length / 60 + text.Split('\n').Length - 1;
            return 14f + 20f * lines;
        }
    }
}
