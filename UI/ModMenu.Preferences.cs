using System;
using System.Collections.Generic;
using FruktSharedLibrary.Controls;
using FruktSharedLibrary.Core;
using MelonLoader;
using MelonLoader.Preferences;

namespace FruktSharedLibrary.UI
{
    public static partial class ModMenu
    {
        private static readonly HashSet<MelonPreferences_Category> UnsavedCategories = new();
        private static bool _saveLoopStarted;

        /// <summary>
        /// Builds a settings page from a MelonPreferences category, so players can change your mod's settings
        /// in game. Every visible entry gets a row that fits its type:
        /// <list type="bullet">
        /// <item><c>bool</c> → toggle</item>
        /// <item><c>int</c>/<c>float</c>/<c>double</c> with a <see cref="ValueRange{T}"/> validator → slider (without one → read-only line)</item>
        /// <item>enums → choice</item>
        /// <item><c>string</c> whose identifier or name contains "key" and that parses as a <see cref="KeyBind"/> → key binding</item>
        /// <item>any other <c>string</c> → text box</item>
        /// <item>anything else → read-only line showing the value</item>
        /// </list>
        /// The entry's description becomes the hint line. Changes are applied immediately and saved to
        /// MelonPreferences.cfg shortly after (and when the menu closes).
        /// </summary>
        /// <param name="category">The category your mod created with <c>MelonPreferences.CreateCategory</c>.</param>
        /// <param name="title">Page title; defaults to the category's display name.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static ModMenuPage AddPreferencesPage(MelonPreferences_Category category, string title = null)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));
            var page = AddPage(string.IsNullOrWhiteSpace(title) ? category.DisplayName ?? category.Identifier : title,
                System.Reflection.Assembly.GetCallingAssembly());
            page.Clear();
            foreach (var entry in category.Entries)
            {
                if (entry == null || entry.IsHidden)
                    continue;
                AddPreferenceRow(page, category, entry);
                if (!string.IsNullOrWhiteSpace(entry.Description))
                    page.WithTooltip(entry.Description);
            }
            page.Separator().Button("Reset to defaults", () =>
            {
                foreach (var entry in category.Entries)
                {
                    if (entry != null && !entry.IsHidden)
                        entry.ResetToDefault();
                }
                MarkUnsaved(category);
                Notifications.Show($"{page.Title}: settings reset to defaults");
            });
            StartSaveLoop();
            return page;
        }

        private static void AddPreferenceRow(ModMenuPage page, MelonPreferences_Category category, MelonPreferences_Entry entry)
        {
            string name = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Identifier : entry.DisplayName;
            var type = entry.GetReflectedType();
            void Set(object value)
            {
                entry.BoxedValue = value;
                MarkUnsaved(category);
            }

            if (type == typeof(bool))
            {
                page.Toggle(name, () => (bool)entry.BoxedValue, v => Set(v));
                return;
            }
            if (type.IsEnum)
            {
                var values = Enum.GetValues(type);
                var names = new string[values.Length];
                for (int i = 0; i < values.Length; i++)
                    names[i] = values.GetValue(i).ToString().ToLowerInvariant();
                page.Choice(name, names, () => Math.Max(0, Array.IndexOf(values, entry.BoxedValue)), i => Set(values.GetValue(i)));
                return;
            }
            if (entry.Validator is IValueRange range && (type == typeof(int) || type == typeof(float) || type == typeof(double)))
            {
                float min = Convert.ToSingle(range.MinValue);
                float max = Convert.ToSingle(range.MaxValue);
                if (type == typeof(int))
                    page.Slider(name, (int)min, (int)max, () => (int)entry.BoxedValue, v => Set(v));
                else if (type == typeof(float))
                    page.Slider(name, min, max, () => (float)entry.BoxedValue, v => Set(v), "0.##");
                else
                    page.Slider(name, min, max, () => (float)(double)entry.BoxedValue, v => Set((double)v), "0.##");
                return;
            }
            if (type == typeof(string) && LooksLikeKey(entry))
            {
                page.KeyBinding(name,
                    () => KeyBind.TryParse((string)entry.BoxedValue, out var bind) ? bind : null,
                    bind => Set(bind?.ToString() ?? string.Empty));
                return;
            }
            if (type == typeof(string))
            {
                page.TextField(name, () => (string)entry.BoxedValue ?? string.Empty, v => Set(v), 64);
                return;
            }
            page.Label(() => $"{name}: {entry.GetValueAsString()}  (edit in MelonPreferences.cfg)");
        }

        private static bool LooksLikeKey(MelonPreferences_Entry entry)
        {
            bool named = (entry.Identifier ?? string.Empty).IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0
                || (entry.DisplayName ?? string.Empty).IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0
                || (entry.Identifier ?? string.Empty).IndexOf("bind", StringComparison.OrdinalIgnoreCase) >= 0;
            var text = entry.BoxedValue as string;
            return named && (string.IsNullOrEmpty(text) || KeyBind.TryParse(text, out _));
        }

        private static void MarkUnsaved(MelonPreferences_Category category) => UnsavedCategories.Add(category);

        /// <summary>Drops pending saves for a category (self-test categories must never reach the file).</summary>
        internal static void ForgetUnsaved(MelonPreferences_Category category) => UnsavedCategories.Remove(category);

        private static void StartSaveLoop()
        {
            if (_saveLoopStarted)
                return;
            _saveLoopStarted = true;
            Scheduler.Every(2f, SaveUnsaved);
            OpenChanged += open =>
            {
                if (!open)
                    SaveUnsaved();
            };
        }

        internal static void SaveUnsaved()
        {
            if (UnsavedCategories.Count == 0)
                return;
            foreach (var category in UnsavedCategories)
            {
                try
                {
                    category.SaveToFile(false);
                }
                catch (Exception e)
                {
                    FruktLog.Warning($"Saving preferences '{category.Identifier}' failed: {e.Message}");
                }
            }
            UnsavedCategories.Clear();
        }
    }
}
