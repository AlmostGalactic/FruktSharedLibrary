using System;
using System.Collections.Generic;
using FruktSharedLibrary.Controls;
using UnityEngine;

namespace FruktSharedLibrary.UI
{
    /// <summary>
    /// One page of the <see cref="ModMenu"/>. Build it with the chainable methods; values are read through
    /// your getters every frame, so the menu always shows live state.
    /// </summary>
    /// <example>
    /// <code>
    /// ModMenu.AddPage("My Mod")
    ///     .Header("Cheats")
    ///     .Toggle("God mode", () => _god, v => _god = v)
    ///     .Slider("Bullet time", 0.05f, 1f, () => World.TimeScale, v => World.TimeScale = v)
    ///     .Choice("Difficulty", new[] { "easy", "normal", "hard" }, () => _difficulty, i => _difficulty = i)
    ///     .KeyBinding("Panic key", () => _panic, k => _panic = k)
    ///     .Button("Spawn human", () => Creatures.SpawnHumanInFront());
    /// </code>
    /// </example>
    public sealed class ModMenuPage
    {
        internal readonly List<ModMenuItem> Items = new();

        internal ModMenuPage(string title) => Title = title;

        /// <summary>Page title.</summary>
        public string Title { get; }

        /// <summary>When set, the page is only listed while this returns true (e.g. only in the sandbox).</summary>
        public Func<bool> VisibleWhen { get; set; }

        /// <summary>Listed after every other page (used for the library's own settings).</summary>
        internal bool ListLast { get; set; }

        /// <summary>Bumped whenever items are added or removed, so the menu knows to rebuild.</summary>
        internal int Version { get; private set; }

        /// <summary>A section title.</summary>
        public ModMenuPage Header(string text) => Add(new ModMenuItem(ModMenuItemKind.Header, () => text));

        /// <summary>A line of text.</summary>
        public ModMenuPage Label(string text) => Label(() => text);

        /// <summary>A line of text that updates live.</summary>
        public ModMenuPage Label(Func<string> text) => Add(new ModMenuItem(ModMenuItemKind.Label, text ?? throw new ArgumentNullException(nameof(text))));

        /// <summary>A clickable button.</summary>
        public ModMenuPage Button(string text, Action onClick)
            => Add(new ModMenuItem(ModMenuItemKind.Button, () => text) { OnClick = onClick ?? throw new ArgumentNullException(nameof(onClick)) });

        /// <summary>An on/off setting.</summary>
        public ModMenuPage Toggle(string text, Func<bool> get, Action<bool> set)
            => Add(new ModMenuItem(ModMenuItemKind.Toggle, () => text)
            {
                GetBool = get ?? throw new ArgumentNullException(nameof(get)),
                SetBool = set ?? throw new ArgumentNullException(nameof(set)),
            });

        /// <summary>A slider between <paramref name="min"/> and <paramref name="max"/>.</summary>
        /// <param name="format">Number format for the value readout, e.g. "0.00" or "0".</param>
        public ModMenuPage Slider(string text, float min, float max, Func<float> get, Action<float> set, string format = "0.00")
            => Add(new ModMenuItem(ModMenuItemKind.Slider, () => text)
            {
                Min = min,
                Max = max,
                Format = format,
                GetFloat = get ?? throw new ArgumentNullException(nameof(get)),
                SetFloat = set ?? throw new ArgumentNullException(nameof(set)),
            });

        /// <summary>A whole-number slider.</summary>
        public ModMenuPage Slider(string text, int min, int max, Func<int> get, Action<int> set)
        {
            if (get == null)
                throw new ArgumentNullException(nameof(get));
            if (set == null)
                throw new ArgumentNullException(nameof(set));
            return Slider(text, (float)min, max, () => get(), v => set(Mathf.RoundToInt(v)), "0");
        }

        /// <summary>Pick one option out of a few (shown inline like the game's "flat / shift / free").</summary>
        public ModMenuPage Choice(string text, IReadOnlyList<string> options, Func<int> get, Action<int> set)
        {
            if (options == null || options.Count == 0)
                throw new ArgumentException("A choice needs at least one option.", nameof(options));
            return Add(new ModMenuItem(ModMenuItemKind.Choice, () => text)
            {
                Options = options,
                GetInt = get ?? throw new ArgumentNullException(nameof(get)),
                SetInt = set ?? throw new ArgumentNullException(nameof(set)),
            });
        }

        /// <summary>Pick a value of an enum.</summary>
        public ModMenuPage Choice<TEnum>(string text, Func<TEnum> get, Action<TEnum> set) where TEnum : struct, Enum
        {
            var values = (TEnum[])Enum.GetValues(typeof(TEnum));
            var names = Array.ConvertAll(values, v => v.ToString().ToLowerInvariant());
            return Choice(text, names, () => Math.Max(0, Array.IndexOf(values, get())), i => set(values[Mathf.Clamp(i, 0, values.Length - 1)]));
        }

        /// <summary>A rebindable key: click it, then press the new key (Esc cancels, Backspace clears).</summary>
        public ModMenuPage KeyBinding(string text, Func<KeyBind> get, Action<KeyBind> set)
            => Add(new ModMenuItem(ModMenuItemKind.KeyBinding, () => text)
            {
                GetKey = get ?? throw new ArgumentNullException(nameof(get)),
                SetKey = set ?? throw new ArgumentNullException(nameof(set)),
            });

        /// <summary>A thin divider line.</summary>
        public ModMenuPage Separator() => Add(new ModMenuItem(ModMenuItemKind.Separator, () => string.Empty));

        /// <summary>Removes every item from the page.</summary>
        public ModMenuPage Clear()
        {
            Items.Clear();
            Version++;
            return this;
        }

        /// <summary>
        /// Makes the item added just before this call only show while <paramref name="condition"/> is true.
        /// </summary>
        public ModMenuPage OnlyWhen(Func<bool> condition)
        {
            if (Items.Count > 0)
                Items[Items.Count - 1].Visible = condition;
            Version++;
            return this;
        }

        /// <summary>Adds a hint line under the item added just before this call.</summary>
        public ModMenuPage WithTooltip(string text)
        {
            if (Items.Count > 0)
                Items[Items.Count - 1].Tooltip = text;
            return this;
        }

        private ModMenuPage Add(ModMenuItem item)
        {
            Items.Add(item);
            Version++;
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
        Choice,
        KeyBinding,
        Separator,
    }

    internal sealed class ModMenuItem
    {
        public readonly ModMenuItemKind Kind;
        public readonly Func<string> Text;
        public Func<bool> Visible;
        public string Tooltip;
        public Action OnClick;
        public Func<bool> GetBool;
        public Action<bool> SetBool;
        public Func<float> GetFloat;
        public Action<float> SetFloat;
        public Func<int> GetInt;
        public Action<int> SetInt;
        public Func<KeyBind> GetKey;
        public Action<KeyBind> SetKey;
        public IReadOnlyList<string> Options;
        public float Min;
        public float Max;
        public string Format;

        public ModMenuItem(ModMenuItemKind kind, Func<string> text)
        {
            Kind = kind;
            Text = text;
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

        public string FormatValue(float value)
        {
            try
            {
                return value.ToString(Format ?? "0.00");
            }
            catch
            {
                return value.ToString("0.00");
            }
        }
    }
}
