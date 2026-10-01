using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruktSharedLibrary.UI
{
    /// <summary>
    /// One tab in the <see cref="ModMenu"/>. Build it with the chainable methods; values are read through
    /// your getters every frame, so the menu always shows live state.
    /// </summary>
    /// <example>
    /// <code>
    /// ModMenu.AddPage("My Mod")
    ///     .Header("Cheats")
    ///     .Toggle("God mode", () => _god, v => _god = v)
    ///     .Slider("Bullet time", 0.05f, 1f, () => World.TimeScale, v => World.TimeScale = v)
    ///     .Button("Spawn human", () => Creatures.SpawnHumanInFront());
    /// </code>
    /// </example>
    public sealed class ModMenuPage
    {
        internal readonly List<ModMenuItem> Items = new();

        internal ModMenuPage(string title) => Title = title;

        /// <summary>Tab title.</summary>
        public string Title { get; }

        /// <summary>When set, the page only shows while this returns true (e.g. only in the sandbox).</summary>
        public Func<bool> VisibleWhen { get; set; }

        /// <summary>A bold section title.</summary>
        public ModMenuPage Header(string text) => Add(new ModMenuItem(ModMenuItemKind.Header, () => text, 26f));

        /// <summary>A line of text.</summary>
        public ModMenuPage Label(string text) => Label(() => text);

        /// <summary>A line of text that updates live.</summary>
        public ModMenuPage Label(Func<string> text) => Add(new ModMenuItem(ModMenuItemKind.Label, text, 22f));

        /// <summary>A clickable button.</summary>
        public ModMenuPage Button(string text, Action onClick)
            => Add(new ModMenuItem(ModMenuItemKind.Button, () => text, 30f) { OnClick = onClick ?? throw new ArgumentNullException(nameof(onClick)) });

        /// <summary>A checkbox.</summary>
        public ModMenuPage Toggle(string text, Func<bool> get, Action<bool> set)
            => Add(new ModMenuItem(ModMenuItemKind.Toggle, () => text, 26f)
            {
                GetBool = get ?? throw new ArgumentNullException(nameof(get)),
                SetBool = set ?? throw new ArgumentNullException(nameof(set)),
            });

        /// <summary>A slider between <paramref name="min"/> and <paramref name="max"/>.</summary>
        /// <param name="format">Number format for the value readout, e.g. "0.00" or "0".</param>
        public ModMenuPage Slider(string text, float min, float max, Func<float> get, Action<float> set, string format = "0.00")
            => Add(new ModMenuItem(ModMenuItemKind.Slider, () => text, 44f)
            {
                Min = min,
                Max = max,
                Format = format,
                GetFloat = get ?? throw new ArgumentNullException(nameof(get)),
                SetFloat = set ?? throw new ArgumentNullException(nameof(set)),
            });

        /// <summary>A thin divider line.</summary>
        public ModMenuPage Separator() => Add(new ModMenuItem(ModMenuItemKind.Separator, () => string.Empty, 10f));

        /// <summary>Removes every item from the page.</summary>
        public ModMenuPage Clear()
        {
            Items.Clear();
            return this;
        }

        /// <summary>
        /// Makes the item added just before this call only show while <paramref name="condition"/> is true.
        /// </summary>
        public ModMenuPage OnlyWhen(Func<bool> condition)
        {
            if (Items.Count > 0)
                Items[Items.Count - 1].Visible = condition;
            return this;
        }

        private ModMenuPage Add(ModMenuItem item)
        {
            Items.Add(item);
            return this;
        }
    }

    internal enum ModMenuItemKind
    {
        Header,
        Label,
        Button,
        Toggle,
        Slider,
        Separator,
    }

    internal sealed class ModMenuItem
    {
        public readonly ModMenuItemKind Kind;
        public readonly Func<string> Text;
        public readonly float Height;
        public Func<bool> Visible;
        public Action OnClick;
        public Func<bool> GetBool;
        public Action<bool> SetBool;
        public Func<float> GetFloat;
        public Action<float> SetFloat;
        public float Min;
        public float Max;
        public string Format;

        public ModMenuItem(ModMenuItemKind kind, Func<string> text, float height)
        {
            Kind = kind;
            Text = text;
            Height = height;
        }

        public bool IsVisible
        {
            get
            {
                try
                {
                    return Visible == null || Visible();
                }
                catch
                {
                    return false;
                }
            }
        }

        public string SafeText
        {
            get
            {
                try
                {
                    return Text() ?? string.Empty;
                }
                catch (Exception e)
                {
                    return "<error: " + e.Message + ">";
                }
            }
        }

        public float ClampedValue(float value) => Mathf.Clamp(value, Math.Min(Min, Max), Math.Max(Min, Max));
    }
}
