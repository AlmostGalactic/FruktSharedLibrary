using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using UnityEngine;

namespace FruktSharedLibrary.UI
{
    /// <summary>Short on-screen messages (top centre of the screen) that fade out on their own.</summary>
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
        }

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
            Messages.Add(new Message { Text = text, Color = color, Created = Time.realtimeSinceStartup, Duration = Math.Max(0.5f, seconds) });
            if (Messages.Count > MaxVisible * 2)
                Messages.RemoveRange(0, Messages.Count - MaxVisible * 2);
        }

        /// <summary>Shows a warning-coloured message.</summary>
        public static void Warn(string text, float seconds = 4f) => Show(text, new Color(1f, 0.75f, 0.3f), seconds);

        /// <summary>Removes every message.</summary>
        public static void Clear() => Messages.Clear();

        internal static void Draw()
        {
            if (_drawFailed || Messages.Count == 0)
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
