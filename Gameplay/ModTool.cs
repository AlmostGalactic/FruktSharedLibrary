using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using UnityEngine;

namespace FruktSharedLibrary.Gameplay
{
    /// <summary>
    /// An item a mod adds to the inventory. It's listed in the terminal like the game's own items, the player puts it
    /// on the toolbar the same way, and while it's in their hand the mod gets the mouse buttons. Make one with
    /// <see cref="Inventory.AddTool"/> or <see cref="Inventory.AddProp(string, Mesh, Material, float)"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// Inventory.AddTool("Boom Stick")
    ///     .WithDescription("Blows up whatever you point at.")
    ///     .WithCard("radius", "3 m")
    ///     .OnLeftClick(() =>
    ///     {
    ///         if (LocalPlayer.TryGetAimPoint(out var point))
    ///             Damage.Explosion(point, 3f);
    ///     });
    /// </code>
    /// </example>
    public class ModTool
    {
        internal ModTool(string name, string category)
        {
            Name = name;
            Category = category;
        }

        /// <summary>The name in the terminal and above the toolbar.</summary>
        public string Name { get; }

        /// <summary>
        /// The terminal category it's filed under: "Weapons", "Tools", "Props", "Etc" or one from
        /// <see cref="Inventory.AddCategory"/>.
        /// </summary>
        public string Category { get; internal set; }

        /// <summary>The text on its card in the terminal.</summary>
        public string Description { get; private set; } = "";

        /// <summary>
        /// The icon the mod gave it with <see cref="WithIcon"/>. Without one, the terminal and toolbar show a picture
        /// of its model (a plain square if it has no model).
        /// </summary>
        public Sprite Icon { get; private set; }

        /// <summary>What the player sees in their hand. Nothing (an empty hand) if this is null.</summary>
        public GameObject Model { get; private set; }

        /// <summary>Where the model sits in the hand, relative to where the game holds its own items.</summary>
        public Vector3 HeldPosition { get; private set; }

        /// <summary>How the model is turned in the hand, in degrees.</summary>
        public Vector3 HeldRotation { get; private set; }

        /// <summary>How big the model is in the hand.</summary>
        public float HeldScale { get; private set; } = 1f;

        /// <summary>The rows on its card in the terminal, like ("range", "40 m").</summary>
        public IReadOnlyList<KeyValuePair<string, string>> CardRows => _cardRows;

        private readonly List<KeyValuePair<string, string>> _cardRows = new();

        /// <summary>The game's item for it, once it's been added to the inventory (that happens when the game starts).</summary>
        public InventoryItem Item { get; internal set; }

        /// <summary>Whether it's in the inventory yet.</summary>
        public bool Registered => Item != null;

        /// <summary>True if it couldn't be added to the inventory. The log says why.</summary>
        public bool Failed { get; internal set; }

        /// <summary>Whether the player is holding it right now.</summary>
        public bool IsHeld => Internal.ModItems.HeldTool == this;

        /// <summary>The copy in the player's hand while they hold it, otherwise null.</summary>
        public GameObject HeldObject => IsHeld ? Internal.ModItems.HeldObject : null;

        /// <summary>
        /// The game item this is a copy of, for one made with <see cref="Inventory.AddCopy"/>; null for a mod's own
        /// tool.
        /// </summary>
        public string CopyOf { get; internal set; }

        // ------------------------------------------------------------ setup

        /// <summary>
        /// Files it under another terminal category, such as one from <see cref="Inventory.AddCategory"/>. This is
        /// how props get out of Props. Call it before the first map loads; after that it stays where it is.
        /// </summary>
        public ModTool WithCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return this;
            if (Registered)
                FruktLog.Warning($"'{Name}' is already in the terminal under {Category}, so it stays there.");
            else
                Category = category.Trim();
            return this;
        }

        /// <summary>Sets the text on its card in the terminal.</summary>
        public ModTool WithDescription(string description)
        {
            Description = description ?? "";
            return this;
        }

        /// <summary>Adds a row to its card in the terminal, like ("range", "40 m"). Keys must be different.</summary>
        public ModTool WithCard(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                FruktLog.Warning($"'{Name}': a card row needs both a key and a value, so ('{key}', '{value}') was left out.");
            else if (_cardRows.Exists(r => string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase)))
                FruktLog.Warning($"'{Name}' already has a card row called '{key}'.");
            else
                _cardRows.Add(new KeyValuePair<string, string>(key, value));
            return this;
        }

        /// <summary>Sets its icon in the terminal and on the toolbar. Square images look best.</summary>
        public ModTool WithIcon(Sprite icon)
        {
            Icon = icon;
            Internal.ModItems.Refresh(this);
            return this;
        }

        /// <summary>
        /// Sets what the player sees in their hand: a copy of <paramref name="model"/> (a prefab from a
        /// <see cref="Assets.ModBundle"/>, or any GameObject), placed relative to where the game holds its own items.
        /// It can be changed at any time, also while the player holds it.
        /// </summary>
        public ModTool WithModel(GameObject model, Vector3 position = default, Vector3 rotation = default, float scale = 1f)
        {
            FollowBundle(null, null);
            SetModel(model, position, rotation, scale);
            return this;
        }

        /// <summary>
        /// Sets the model in the hand to a prefab from a bundle, and keeps it up to date when the bundle is reloaded
        /// (see <see cref="Assets.ModBundle.WatchForChanges"/>).
        /// </summary>
        public ModTool WithModel(Assets.ModBundle bundle, string prefab, Vector3 position = default, Vector3 rotation = default, float scale = 1f)
        {
            if (bundle == null)
                throw new ArgumentNullException(nameof(bundle));
            SetModel(bundle.Load<GameObject>(prefab), position, rotation, scale);
            FollowBundle(bundle, b => SetModel(b.Load<GameObject>(prefab), HeldPosition, HeldRotation, HeldScale));
            return this;
        }

        private void SetModel(GameObject model, Vector3 position, Vector3 rotation, float scale)
        {
            Model = model;
            HeldPosition = position;
            HeldRotation = rotation;
            HeldScale = scale;
            if (Registered)
                Internal.ModItems.ModelChanged(this);
        }

        private Assets.ModBundle _source;
        private Action<Assets.ModBundle> _onSourceReloaded;

        // What the item is made of comes from this bundle: follow it when it's reloaded. One bundle at a time.
        internal void FollowBundle(Assets.ModBundle bundle, Action<Assets.ModBundle> onReloaded)
        {
            if (_source != null)
                _source.Reloaded -= _onSourceReloaded;
            _source = bundle;
            _onSourceReloaded = onReloaded;
            if (_source != null)
                _source.Reloaded += _onSourceReloaded;
        }

        /// <summary>A picture of the item for its automatic icon, or null.</summary>
        internal virtual Sprite RenderThumbnail() => Model.Exists() ? Utilities.Thumbnails.Render(Model) : null;

        // ------------------------------------------------------------ events

        /// <summary>The player picked it on the toolbar.</summary>
        public event Action Selected;

        /// <summary>The player put it away (picked something else, or it was taken off the toolbar).</summary>
        public event Action Deselected;

        /// <summary>Every frame while the player holds it.</summary>
        public event Action WhileHeld;

        /// <summary>Left mouse button pressed.</summary>
        public event Action LeftClick;

        /// <summary>Every frame while the left button is held down after a click, with how long it's been held in seconds.</summary>
        public event Action<float> LeftHold;

        /// <summary>Left mouse button let go.</summary>
        public event Action LeftRelease;

        /// <summary>Right mouse button pressed.</summary>
        public event Action RightClick;

        /// <summary>Every frame while the right button is held down, with how long it's been held in seconds.</summary>
        public event Action<float> RightHold;

        /// <summary>Right mouse button let go.</summary>
        public event Action RightRelease;

        /// <summary>Middle mouse button pressed.</summary>
        public event Action MiddleClick;

        /// <summary>The mouse wheel turned, in notches (positive is away from the player).</summary>
        public event Action<float> Scroll;

        /// <summary>Runs <paramref name="action"/> when the left mouse button is pressed.</summary>
        public ModTool OnLeftClick(Action action)
        {
            LeftClick += action;
            return this;
        }

        /// <summary>Runs <paramref name="action"/> when the right mouse button is pressed.</summary>
        public ModTool OnRightClick(Action action)
        {
            RightClick += action;
            return this;
        }

        /// <summary>Runs <paramref name="action"/> with the notches when the mouse wheel turns.</summary>
        public ModTool OnScroll(Action<float> action)
        {
            Scroll += action;
            return this;
        }

        /// <summary>Runs <paramref name="action"/> every frame while the player holds it.</summary>
        public ModTool OnHeld(Action action)
        {
            WhileHeld += action;
            return this;
        }

        internal void Raise(ToolInput input, float value = 0f)
        {
            try
            {
                OnInput(input, value);
            }
            catch (Exception e)
            {
                FruktLog.Error($"'{Name}' failed to handle {input}", e);
            }
            switch (input)
            {
                case ToolInput.Selected: Invoke(Selected, nameof(Selected)); break;
                case ToolInput.Deselected: Invoke(Deselected, nameof(Deselected)); break;
                case ToolInput.Held: Invoke(WhileHeld, nameof(WhileHeld)); break;
                case ToolInput.LeftClick: Invoke(LeftClick, nameof(LeftClick)); break;
                case ToolInput.LeftHold: Invoke(LeftHold, value, nameof(LeftHold)); break;
                case ToolInput.LeftRelease: Invoke(LeftRelease, nameof(LeftRelease)); break;
                case ToolInput.RightClick: Invoke(RightClick, nameof(RightClick)); break;
                case ToolInput.RightHold: Invoke(RightHold, value, nameof(RightHold)); break;
                case ToolInput.RightRelease: Invoke(RightRelease, nameof(RightRelease)); break;
                case ToolInput.MiddleClick: Invoke(MiddleClick, nameof(MiddleClick)); break;
                case ToolInput.Scroll: Invoke(Scroll, value, nameof(Scroll)); break;
            }
        }

        /// <summary>What the item does itself, before the mod's handlers run (a prop places itself, for one).</summary>
        internal virtual void OnInput(ToolInput input, float value)
        {
        }

        private void Invoke(Action handlers, string name)
        {
            if (handlers == null)
                return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception e)
                {
                    FruktLog.Error($"A {name} handler of '{Name}' threw", e);
                }
            }
        }

        private void Invoke(Action<float> handlers, float value, string name)
        {
            if (handlers == null)
                return;
            foreach (Action<float> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(value);
                }
                catch (Exception e)
                {
                    FruktLog.Error($"A {name} handler of '{Name}' threw", e);
                }
            }
        }

        public override string ToString() => Name;
    }

    internal enum ToolInput
    {
        Selected,
        Deselected,
        Held,
        LeftClick,
        LeftHold,
        LeftRelease,
        RightClick,
        RightHold,
        RightRelease,
        MiddleClick,
        Scroll,
    }
}
