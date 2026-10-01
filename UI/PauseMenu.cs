using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using Il2CppInfrastructure.Project.AssetsHandlers.SFX;
using Il2CppViews.Generic;
using Il2CppViews.Pause;
using UnityEngine;

namespace FruktSharedLibrary.UI
{
    /// <summary>
    /// Adds lines to the game's pause menu (CONTINUE / SETTINGS / ... / QUIT). The lines are real copies of the
    /// game's own buttons, so they look and animate exactly like the rest of the menu.
    /// </summary>
    /// <example>
    /// <code>
    /// PauseMenu.AddButton("My Mod", () => ModMenu.Open());
    /// </code>
    /// </example>
    public static class PauseMenu
    {
        private const string NamePrefix = "FruktSharedLibrary.PauseButton.";

        /// <summary>One line added to the pause menu.</summary>
        public sealed class Entry
        {
            internal Entry(string label, Action onClick)
            {
                Label = label;
                OnClick = onClick;
            }

            /// <summary>The word shown (upper-cased like the game's own lines).</summary>
            public string Label { get; private set; }

            internal Action OnClick { get; }

            /// <summary>When set, the line is only shown while this returns true.</summary>
            public Func<bool> VisibleWhen { get; set; }

            internal MenuLineButton Button;
            internal IDisposable Subscription;
            internal int Id;

            /// <summary>Changes the word shown.</summary>
            public void SetLabel(string label)
            {
                Label = label ?? string.Empty;
                if (Button != null)
                    Button.SetWord(Label.ToUpperInvariant());
            }
        }

        private static readonly List<Entry> Entries = new();
        private static int _nextId;
        private static Transform _column;
        private static GameObject _prototype;
        private static float _nextSearch;

        /// <summary>
        /// Adds a line to the pause menu, placed after SETTINGS (and after lines added by other mods).
        /// </summary>
        public static Entry AddButton(string label, Action onClick)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("The label is empty.", nameof(label));
            var entry = new Entry(label, onClick ?? throw new ArgumentNullException(nameof(onClick))) { Id = _nextId++ };
            Entries.Add(entry);
            _nextSearch = 0f;
            return entry;
        }

        /// <summary>Removes a line added with <see cref="AddButton"/>.</summary>
        public static void RemoveButton(Entry entry)
        {
            if (entry == null || !Entries.Remove(entry))
                return;
            DestroyButton(entry);
        }

        /// <summary>True when the game's pause menu was found and the lines were added.</summary>
        public static bool Attached => _column != null && Entries.TrueForAll(e => e.Button != null || !IsVisible(e));

        internal static void Initialize()
        {
            GameEvents.SandboxReady += _ => _nextSearch = 0f;
            GameEvents.PauseChanged += paused =>
            {
                if (paused)
                    _nextSearch = 0f;
            };
            GameEvents.SandboxExited += Forget;
        }

        internal static void Update()
        {
            if (Entries.Count == 0 || !GameState.InSandbox)
                return;
            // Keep the lines in sync with VisibleWhen while the menu is showing.
            if (_column != null)
            {
                foreach (var entry in Entries)
                {
                    if (entry.Button != null && entry.Button.gameObject.activeSelf != IsVisible(entry))
                        entry.Button.gameObject.SetActive(!entry.Button.gameObject.activeSelf);
                }
            }
            if (Time.unscaledTime < _nextSearch)
                return;
            _nextSearch = Time.unscaledTime + 2f;
            try
            {
                Attach();
            }
            catch (Exception e)
            {
                FruktLog.Warning("Adding the pause menu lines failed: " + e.Message);
                _nextSearch = float.MaxValue; // Don't retry every two seconds; the next scene tries again.
            }
        }

        private static void Attach()
        {
            if (_column == null || _prototype == null)
            {
                Forget();
                if (!FindColumn())
                    return;
            }
            foreach (var entry in Entries)
            {
                if (entry.Button == null)
                    CreateButton(entry);
            }
            Order();
        }

        private static bool FindColumn()
        {
            var view = GameServices.FindObject<PauseView>(true);
            if (view == null)
                return false;
            foreach (var button in view.GetComponentsInChildren<MenuLineButton>(true))
            {
                if (button == null || button.gameObject.name != "Settings")
                    continue;
                // The root screen's SETTINGS line (the settings screen has its own lines named after categories).
                _prototype = button.gameObject;
                _column = button.transform.parent;
                FruktLog.Debug("Pause menu column found: " + _column.name);
                return true;
            }
            return false;
        }

        private static void CreateButton(Entry entry)
        {
            var copy = FruktUi.CloneGameUi(_prototype, _column, NamePrefix + entry.Id);
            if (copy == null)
                return;
            var button = copy.GetComponent<MenuLineButton>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(copy);
                return;
            }
            entry.Button = button;
            button.SetWord(entry.Label.ToUpperInvariant());
            try
            {
                button.ApplySelection(false);
            }
            catch
            {
                // Only cosmetic.
            }
            entry.Subscription = button.OnClicked.Listen(() => Click(entry));
            copy.SetActive(IsVisible(entry));
        }

        private static void Order()
        {
            // Our lines go after SETTINGS and after anything other mods put there, but before BACK TO MENU / QUIT.
            int index = _prototype.transform.GetSiblingIndex() + 1;
            for (int i = index; i < _column.childCount; i++)
            {
                var child = _column.GetChild(i);
                if (child.name == "BackToMenu" || child.name == "Exit")
                    break;
                if (!child.name.StartsWith(NamePrefix, StringComparison.Ordinal))
                    index = i + 1;
            }
            foreach (var entry in Entries)
            {
                if (entry.Button == null)
                    continue;
                var transform = entry.Button.transform;
                if (transform.GetSiblingIndex() < index)
                    index--;
                transform.SetSiblingIndex(index);
                index++;
            }
        }

        private static void Click(Entry entry)
        {
            Sounds.Play(UISFXType.LargeButtonClick, 0.8f);
            try
            {
                entry.OnClick();
            }
            catch (Exception e)
            {
                FruktLog.Error($"Pause menu button '{entry.Label}' threw", e);
            }
        }

        private static bool IsVisible(Entry entry)
        {
            try
            {
                return entry.VisibleWhen == null || entry.VisibleWhen();
            }
            catch
            {
                return false;
            }
        }

        private static void DestroyButton(Entry entry)
        {
            entry.Subscription?.Dispose();
            entry.Subscription = null;
            if (entry.Button != null)
                UnityEngine.Object.Destroy(entry.Button.gameObject);
            entry.Button = null;
        }

        private static void Forget()
        {
            foreach (var entry in Entries)
            {
                entry.Subscription?.Dispose();
                entry.Subscription = null;
                entry.Button = null;
            }
            _column = null;
            _prototype = null;
            _nextSearch = 0f;
        }
    }
}
