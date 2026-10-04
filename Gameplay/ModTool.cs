using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
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

        /// <summary>The terminal category it's filed under: "Weapons", "Tools", "Props" or "Etc".</summary>
        public string Category { get; }

        /// <summary>The text on its card in the terminal.</summary>
        public string Description { get; private set; } = "";

        /// <summary>Its icon in the terminal and on the toolbar. A plain one is used if this is null.</summary>
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

        // ------------------------------------------------------------ setup

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
        /// Set it before the game starts; the model is copied when the tool is added to the inventory.
        /// </summary>
        public ModTool WithModel(GameObject model, Vector3 position = default, Vector3 rotation = default, float scale = 1f)
        {
            if (Registered)
                FruktLog.Warning($"'{Name}' is already in the inventory, so its model can't change any more.");
            Model = model;
            HeldPosition = position;
            HeldRotation = rotation;
            HeldScale = scale;
            return this;
        }

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
