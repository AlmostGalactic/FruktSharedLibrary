using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using Il2CppTMPro;
using UnityEngine;

namespace FruktSharedLibrary.UI
{
    /// <summary>A line of text that floats over something in the world. Made by <see cref="WorldLabels.Add(Transform, string, Color?, Vector3?, float)"/>.</summary>
    public sealed class WorldLabel
    {
        internal TextMeshProUGUI Label, Shadow;
        internal readonly Transform Target;
        internal readonly Func<Vector3> Position;
        internal readonly bool FollowsTarget;

        internal WorldLabel(Transform target, Func<Vector3> position, string text, Color color, Vector3 offset, float size)
        {
            Target = target;
            FollowsTarget = target != null;
            Position = position;
            Text = text ?? string.Empty;
            Color = color;
            Offset = offset;
            Size = size;
        }

        /// <summary>What it says.</summary>
        public string Text { get; set; }

        /// <summary>The colour of the text.</summary>
        public Color Color { get; set; }

        /// <summary>Where it sits relative to what it follows, in world metres (for example 0.4 m up).</summary>
        public Vector3 Offset { get; set; }

        /// <summary>The text size, on the game's 1920 x 1080 layout.</summary>
        public float Size { get; set; }

        /// <summary>Hidden when the camera is further away than this, in metres.</summary>
        public float MaxDistance { get; set; } = 60f;

        /// <summary>Set to false to hide it for a while without removing it.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>False once it's been removed, or the object it followed was destroyed.</summary>
        public bool Exists { get; internal set; } = true;

        /// <summary>True while it's actually drawn this frame (in front of the camera, close enough, and visible).</summary>
        public bool OnScreen { get; internal set; }

        /// <summary>Takes it away for good.</summary>
        public void Remove() => WorldLabels.Remove(this);
    }

    /// <summary>
    /// Text that floats over things in the world, like names over people's heads, in the game's own display font.
    /// Labels stay the same size on screen however far away the thing is, and go away by themselves when the
    /// object they follow is destroyed.
    /// </summary>
    /// <example>
    /// <code>
    /// var tag = WorldLabels.Add(head.transform, "Red team", Color.red, Vector3.up * 0.4f);
    /// tag.Text = "Blue team";
    /// tag.Remove();
    /// </code>
    /// </example>
    public static class WorldLabels
    {
        private static readonly List<WorldLabel> Labels = new();
        private static Canvas _canvas;
        private static bool _failed;

        /// <summary>Every label that's still around.</summary>
        public static IReadOnlyList<WorldLabel> All => Labels;

        /// <summary>A label that follows <paramref name="target"/>, and goes away when it's destroyed.</summary>
        public static WorldLabel Add(Transform target, string text, Color? color = null, Vector3? offset = null, float size = 30f)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            return Track(new WorldLabel(target, null, text, color ?? FruktTheme.Text, offset ?? Vector3.zero, size));
        }

        /// <summary>
        /// A label at a position you work out each frame, for things that move in ways a transform doesn't show.
        /// Remove it yourself when you're done with it.
        /// </summary>
        public static WorldLabel Add(Func<Vector3> position, string text, Color? color = null, Vector3? offset = null, float size = 30f)
        {
            if (position == null)
                throw new ArgumentNullException(nameof(position));
            return Track(new WorldLabel(null, position, text, color ?? FruktTheme.Text, offset ?? Vector3.zero, size));
        }

        /// <summary>Takes a label away.</summary>
        public static void Remove(WorldLabel label)
        {
            if (label == null || !label.Exists)
                return;
            label.Exists = false;
            label.OnScreen = false;
            Labels.Remove(label);
            if (label.Label != null)
                FruktUi.Destroy(label.Label.transform.parent.gameObject);
        }

        /// <summary>Takes every label away, including other mods' labels.</summary>
        public static void RemoveAll()
        {
            foreach (var label in Labels.ToArray())
                Remove(label);
        }

        private static WorldLabel Track(WorldLabel label)
        {
            Labels.Add(label);
            return label;
        }

        internal static void Update()
        {
            if (Labels.Count == 0 || _failed)
                return;
            try
            {
                Draw();
            }
            catch (Exception e)
            {
                _failed = true;
                FruktLog.Error("World labels stopped working", e);
            }
        }

        private static void Draw()
        {
            if (_canvas == null)
            {
                if (!FruktTheme.Available)
                    return;
                // Under the game's menus and the mod menu, so they cover the labels.
                _canvas = FruktUi.CreateCanvas("FruktSharedLibrary.WorldLabels", 50, blockGameClicks: false);
            }

            var camera = LocalPlayer.Camera;
            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            for (int i = Labels.Count - 1; i >= 0; i--)
            {
                var label = Labels[i];
                if (label.FollowsTarget && !label.Target.Exists())
                {
                    Remove(label);
                    continue;
                }

                Vector3 world;
                try
                {
                    world = (label.FollowsTarget ? label.Target.position : label.Position()) + label.Offset;
                }
                catch
                {
                    world = Vector3.zero;
                    label.Visible = false;
                }

                var screen = camera != null ? camera.WorldToScreenPoint(world) : Vector3.back;
                bool show = label.Visible && GameState.InSandbox && screen.z > 0.05f && screen.z <= label.MaxDistance;
                label.OnScreen = show;
                if (label.Label == null)
                {
                    if (!show)
                        continue;
                    Build(label);
                }
                var root = label.Label.transform.parent.gameObject;
                if (root.activeSelf != show)
                    root.SetActive(show);
                if (!show)
                    continue;

                ((RectTransform)root.transform).anchoredPosition = new Vector2(screen.x / scale, screen.y / scale);
                string text = label.Text ?? string.Empty;
                if (label.Label.text != text)
                {
                    label.Label.text = text;
                    label.Shadow.text = text;
                }
                label.Label.color = label.Color;
                label.Shadow.color = new Color(0f, 0f, 0f, 0.75f * label.Color.a);
                label.Label.fontSize = label.Size;
                label.Shadow.fontSize = label.Size;
            }
        }

        private static void Build(WorldLabel label)
        {
            var root = FruktUi.CreateRect("Label", _canvas.transform, 0f, 0f, 600f, 60f);
            // Placed from the bottom-left corner in screen pixels, centred on the point.
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            label.Shadow = FruktUi.CreateText("Shadow", root, label.Text, FruktTheme.DisplayFont, label.Size, Color.black, 2f, 2f, 600f, 60f, TextAlignmentOptions.Center);
            label.Label = FruktUi.CreateText("Text", root, label.Text, FruktTheme.DisplayFont, label.Size, label.Color, 0f, 0f, 600f, 60f, TextAlignmentOptions.Center);
        }
    }
}
