using System;
using System.Collections.Generic;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Interop;
using Il2CppLVA.Creatures;
using Il2CppLVA.Limbs;
using Il2CppMap.Spinner;
using Il2CppPlayer.ContextMenu;
using Il2CppSpawnables.Misc;
using Il2CppSpawnables.Props;
using Il2CppSpawnables.Weapons;
using UnityEngine;

namespace FruktSharedLibrary.UI
{
    /// <summary>The kinds of objects that have a right-click context menu in the game.</summary>
    public enum ContextMenuTarget
    {
        /// <summary>Any limb of a creature (the menu you get when right-clicking a body).</summary>
        Limb,
        /// <summary>Spawned or map props.</summary>
        Prop,
        /// <summary>Firearms.</summary>
        Firearm,
        /// <summary>The human spawner device.</summary>
        HumanSpawner,
        /// <summary>The map spinner.</summary>
        Spinner,
    }

    /// <summary>What a context menu was opened on. Only the field matching <see cref="Target"/> is set.</summary>
    public sealed class ContextMenuContext
    {
        public ContextMenuTarget Target { get; internal set; }

        /// <summary>The game's menu handler component for the object.</summary>
        public ContextMenuActionsHandler Handler { get; internal set; }

        /// <summary>The limb that was right-clicked (Limb target).</summary>
        public AbstractLimb Limb { get; internal set; }

        /// <summary>The creature owning <see cref="Limb"/> at the moment it's read (Limb target).</summary>
        public AbstractCreature Creature => Limb.GetCreature();

        /// <summary>The prop (Prop target).</summary>
        public Prop Prop { get; internal set; }

        /// <summary>The firearm (Firearm target).</summary>
        public Firearm Firearm { get; internal set; }

        /// <summary>The spawner (HumanSpawner target).</summary>
        public NPCSpawner Spawner { get; internal set; }

        /// <summary>The spinner (Spinner target).</summary>
        public Spinner Spinner { get; internal set; }

        /// <summary>The GameObject the menu belongs to.</summary>
        public GameObject GameObject => Handler.Exists() ? Handler.gameObject : null;
    }

    /// <summary>A registered context-menu action. Call <see cref="Remove"/> to stop adding it to new menus.</summary>
    public sealed class ContextMenuEntry
    {
        internal ContextMenuTarget Target;
        internal Func<ContextMenuContext, string> Label;
        internal Action<ContextMenuContext> OnClick;
        internal Func<ContextMenuContext, bool> ShowIf;
        internal int Priority;
        internal bool Removed;

        /// <summary>Removes the action from every menu, including menus that were already built.</summary>
        public void Remove()
        {
            if (Removed)
                return;
            Removed = true;
            ContextMenus.Entries.Remove(this);
            Internal.ContextMenuCarrier.OnEntryRemoved(this);
        }
    }

    /// <summary>
    /// Adds your own actions to the game's right-click context menus (limbs, props, firearms...).
    /// Actions look and behave exactly like the built-in ones.
    /// </summary>
    /// <example>
    /// <code>
    /// ContextMenus.AddCreatureAction("Heal", creature => creature.Heal());
    /// ContextMenus.AddLimbAction("Launch", limb => limb.AddForce(Vector3.up * 50f));
    /// ContextMenus.AddToggle(ContextMenuTarget.Limb, "Frozen",
    ///     ctx => frozen.Contains(ctx.Creature.Pointer),
    ///     (ctx, on) => { ctx.Creature.SetFrozen(on); ... });
    /// </code>
    /// </example>
    public static class ContextMenus
    {
        internal static readonly List<ContextMenuEntry> Entries = new();

        /// <summary>
        /// Default sort priority. Higher numbers are listed first; the game's own actions use 995-1000,
        /// so 500 puts yours below them.
        /// </summary>
        public const int DefaultPriority = 500;

        /// <summary>Adds an action to a context menu.</summary>
        /// <param name="target">Which kind of object's menu gets the action.</param>
        /// <param name="label">Text shown in the menu.</param>
        /// <param name="onClick">Called when the action is clicked.</param>
        /// <param name="showIf">
        /// Optional filter, evaluated once per object when its menu is built (objects build their menu when they
        /// spawn) or, for objects that already exist, when the action is registered.
        /// </param>
        /// <param name="priority">Sort order: higher numbers are listed first (built-in actions use 995–1000).</param>
        public static ContextMenuEntry AddAction(ContextMenuTarget target, string label, Action<ContextMenuContext> onClick,
            Func<ContextMenuContext, bool> showIf = null, int priority = DefaultPriority)
            => AddAction(target, _ => label, onClick, showIf, priority);

        /// <summary>Adds an action whose label is computed each time the menu is built (and after each click).</summary>
        public static ContextMenuEntry AddAction(ContextMenuTarget target, Func<ContextMenuContext, string> label, Action<ContextMenuContext> onClick,
            Func<ContextMenuContext, bool> showIf = null, int priority = DefaultPriority)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));
            if (onClick == null)
                throw new ArgumentNullException(nameof(onClick));
            var entry = new ContextMenuEntry
            {
                Target = target,
                Label = label,
                OnClick = onClick,
                ShowIf = showIf,
                Priority = priority,
            };
            Entries.Add(entry);
            Internal.ContextMenuCarrier.OnEntryAdded(entry);
            return entry;
        }

        /// <summary>Adds an action to every limb's menu.</summary>
        public static ContextMenuEntry AddLimbAction(string label, Action<AbstractLimb> onClick,
            Func<AbstractLimb, bool> showIf = null, int priority = DefaultPriority)
            => AddAction(ContextMenuTarget.Limb, label, ctx => onClick(ctx.Limb),
                showIf == null ? null : ctx => showIf(ctx.Limb), priority);

        /// <summary>Adds an action to every creature's menu (shown when right-clicking any of its limbs).</summary>
        public static ContextMenuEntry AddCreatureAction(string label, Action<AbstractCreature> onClick,
            Func<AbstractCreature, bool> showIf = null, int priority = DefaultPriority)
            => AddAction(ContextMenuTarget.Limb, label,
                ctx => { var creature = ctx.Creature; if (creature.IsValid()) onClick(creature); },
                ctx => ctx.Creature.IsValid() && (showIf == null || showIf(ctx.Creature)), priority);

        /// <summary>
        /// Adds an on/off action. Its label shows the current state ("Label: ON") and clicking it flips the state.
        /// </summary>
        public static ContextMenuEntry AddToggle(ContextMenuTarget target, string label,
            Func<ContextMenuContext, bool> getState, Action<ContextMenuContext, bool> setState,
            Func<ContextMenuContext, bool> showIf = null, int priority = DefaultPriority)
        {
            if (getState == null)
                throw new ArgumentNullException(nameof(getState));
            if (setState == null)
                throw new ArgumentNullException(nameof(setState));
            return AddAction(target, ctx => $"{label}: {(getState(ctx) ? "ON" : "OFF")}",
                ctx => setState(ctx, !getState(ctx)), showIf, priority);
        }

        /// <summary>Removes every action that was added by any mod.</summary>
        public static void RemoveAll()
        {
            foreach (var entry in Entries.ToArray())
                entry.Remove();
        }
    }
}
