using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Interop;
using Il2CppLVA.Creatures;
using Il2CppLVA.Limbs;
using Il2CppMap.Spinner;
using Il2CppPlayer.ContextMenu;
using Il2CppServices.UI;
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

    /// <summary>A registered context-menu action or group. Call <see cref="Remove"/> to take it out of every menu.</summary>
    public sealed class ContextMenuEntry
    {
        internal ContextMenuTarget Target;
        internal Func<ContextMenuContext, string> Label;
        internal Action<ContextMenuContext> OnClick;
        internal Func<ContextMenuContext, bool> ShowIf;
        internal int Priority;
        internal bool Removed;
        internal bool IsGroup;
        internal bool KeepOpen;
        internal ContextMenuEntry Parent;
        internal readonly List<ContextMenuEntry> Children = new();
        internal int Order;

        internal int Depth => Parent == null ? 0 : Parent.Depth + 1;

        /// <summary>Removes the action (or group with everything in it) from every menu, including menus that were already built.</summary>
        public void Remove()
        {
            if (Removed)
                return;
            MarkRemoved(this);
            if (Parent != null)
                Parent.Children.Remove(this);
            else
                ContextMenus.Entries.Remove(this);
            Internal.ContextMenuCarrier.OnEntryRemoved(this);
        }

        private static void MarkRemoved(ContextMenuEntry entry)
        {
            entry.Removed = true;
            foreach (var child in entry.Children)
                MarkRemoved(child);
        }
    }

    /// <summary>
    /// A drop-down group in a context menu: one line ("+ My Mod") that expands in place to show the actions inside
    /// it, indented, and collapses again when clicked or when the menu closes. Groups can contain groups.
    /// Every Add method returns the group itself so calls can be chained, except <see cref="AddGroup"/>, which
    /// returns the new nested group.
    /// </summary>
    /// <example>
    /// <code>
    /// var tools = ContextMenus.AddCreatureGroup("My Mod")
    ///     .AddCreatureAction("Heal", c => c.Heal())
    ///     .AddToggle("Frozen", ctx => IsFrozen(ctx.Creature), (ctx, on) => SetFrozen(ctx.Creature, on));
    /// tools.AddGroup("Launch")
    ///     .AddCreatureAction("Up", c => c.AddForce(Vector3.up * 600f))
    ///     .AddCreatureAction("Forward", c => c.AddForce(LocalPlayer.Forward * 600f));
    /// </code>
    /// </example>
    public sealed class ContextMenuGroup
    {
        internal ContextMenuGroup(ContextMenuEntry entry) => Entry = entry;

        /// <summary>The group's own entry (remove it to remove the whole group).</summary>
        public ContextMenuEntry Entry { get; }

        /// <summary>Which kind of object's menu the group is in.</summary>
        public ContextMenuTarget Target => Entry.Target;

        /// <summary>Adds an action inside the group. Clicking it runs <paramref name="onClick"/> and closes the menu.</summary>
        public ContextMenuGroup AddAction(string label, Action<ContextMenuContext> onClick, Func<ContextMenuContext, bool> showIf = null)
            => AddAction(_ => label, onClick, showIf);

        /// <summary>Adds an action whose label is computed when the menu is built.</summary>
        public ContextMenuGroup AddAction(Func<ContextMenuContext, string> label, Action<ContextMenuContext> onClick, Func<ContextMenuContext, bool> showIf = null)
        {
            ContextMenus.AddChild(Entry, label, onClick ?? throw new ArgumentNullException(nameof(onClick)), showIf, group: false, keepOpen: false);
            return this;
        }

        /// <summary>
        /// Adds an on/off line inside the group ("Label: ON"). Clicking it flips the state and keeps the menu open,
        /// like the game's own switches.
        /// </summary>
        public ContextMenuGroup AddToggle(string label, Func<ContextMenuContext, bool> getState, Action<ContextMenuContext, bool> setState,
            Func<ContextMenuContext, bool> showIf = null)
        {
            if (getState == null)
                throw new ArgumentNullException(nameof(getState));
            if (setState == null)
                throw new ArgumentNullException(nameof(setState));
            ContextMenus.AddChild(Entry, ctx => ContextMenus.ToggleLabel(label, getState, ctx), ctx => setState(ctx, !getState(ctx)), showIf,
                group: false, keepOpen: true);
            return this;
        }

        /// <summary>Adds an action for the creature (only in groups on <see cref="ContextMenuTarget.Limb"/> menus).</summary>
        public ContextMenuGroup AddCreatureAction(string label, Action<AbstractCreature> onClick, Func<AbstractCreature, bool> showIf = null)
        {
            RequireLimbTarget();
            if (onClick == null)
                throw new ArgumentNullException(nameof(onClick));
            return AddAction(label, ctx => { var creature = ctx.Creature; if (creature.IsValid()) onClick(creature); },
                ctx => ctx.Creature.IsValid() && (showIf == null || showIf(ctx.Creature)));
        }

        /// <summary>Adds an action for the right-clicked limb (only in groups on <see cref="ContextMenuTarget.Limb"/> menus).</summary>
        public ContextMenuGroup AddLimbAction(string label, Action<AbstractLimb> onClick, Func<AbstractLimb, bool> showIf = null)
        {
            RequireLimbTarget();
            if (onClick == null)
                throw new ArgumentNullException(nameof(onClick));
            return AddAction(label, ctx => onClick(ctx.Limb), showIf == null ? null : ctx => showIf(ctx.Limb));
        }

        /// <summary>Adds a nested group inside this one and returns it.</summary>
        public ContextMenuGroup AddGroup(string label, Func<ContextMenuContext, bool> showIf = null)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));
            return new ContextMenuGroup(ContextMenus.AddChild(Entry, _ => label, null, showIf, group: true, keepOpen: true));
        }

        /// <summary>Removes the group and everything in it from every menu.</summary>
        public void Remove() => Entry.Remove();

        private void RequireLimbTarget()
        {
            if (Target != ContextMenuTarget.Limb)
                throw new InvalidOperationException("Creature and limb actions only work in groups on ContextMenuTarget.Limb menus.");
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
    ///
    /// // A drop-down group with its own actions (see ContextMenuGroup)
    /// ContextMenus.AddCreatureGroup("My Mod")
    ///     .AddCreatureAction("Heal", c => c.Heal())
    ///     .AddCreatureAction("Kill", c => c.Kill());
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
            return AddTopLevel(new ContextMenuEntry
            {
                Target = target,
                Label = label,
                OnClick = onClick,
                ShowIf = showIf,
                Priority = priority,
            });
        }

        /// <summary>
        /// Adds a drop-down group: a line that expands in place to show the actions you put in it. Fill it with
        /// the returned <see cref="ContextMenuGroup"/>.
        /// </summary>
        public static ContextMenuGroup AddGroup(ContextMenuTarget target, string label, Func<ContextMenuContext, bool> showIf = null,
            int priority = DefaultPriority)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));
            return new ContextMenuGroup(AddTopLevel(new ContextMenuEntry
            {
                Target = target,
                Label = _ => label,
                ShowIf = showIf,
                Priority = priority,
                IsGroup = true,
                KeepOpen = true,
            }));
        }

        /// <summary>Adds a drop-down group to every creature's menu (shown when right-clicking any of its limbs).</summary>
        public static ContextMenuGroup AddCreatureGroup(string label, Func<AbstractCreature, bool> showIf = null, int priority = DefaultPriority)
            => AddGroup(ContextMenuTarget.Limb, label, ctx => ctx.Creature.IsValid() && (showIf == null || showIf(ctx.Creature)), priority);

        private static int _order;

        private static ContextMenuEntry AddTopLevel(ContextMenuEntry entry)
        {
            entry.Order = _order++;
            Entries.Add(entry);
            Internal.ContextMenuCarrier.OnEntryAdded(entry);
            return entry;
        }

        internal static ContextMenuEntry AddChild(ContextMenuEntry parent, Func<ContextMenuContext, string> label, Action<ContextMenuContext> onClick,
            Func<ContextMenuContext, bool> showIf, bool group, bool keepOpen)
        {
            if (parent.Removed)
                throw new InvalidOperationException("The group was removed.");
            var entry = new ContextMenuEntry
            {
                Target = parent.Target,
                Label = label ?? throw new ArgumentNullException(nameof(label)),
                OnClick = onClick,
                ShowIf = showIf,
                Priority = parent.Priority,
                IsGroup = group,
                KeepOpen = keepOpen,
                Parent = parent,
                Order = _order++,
            };
            parent.Children.Add(entry);
            Internal.ContextMenuCarrier.OnEntryAdded(entry);
            return entry;
        }

        internal static string ToggleLabel(string label, Func<ContextMenuContext, bool> getState, ContextMenuContext ctx)
            => $"{label}: {(getState(ctx) ? "ON" : "OFF")}";

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
        /// Adds an on/off action. Its label shows the current state ("Label: ON"); clicking it flips the state and
        /// keeps the menu open, like the game's own switches.
        /// </summary>
        public static ContextMenuEntry AddToggle(ContextMenuTarget target, string label,
            Func<ContextMenuContext, bool> getState, Action<ContextMenuContext, bool> setState,
            Func<ContextMenuContext, bool> showIf = null, int priority = DefaultPriority)
        {
            if (getState == null)
                throw new ArgumentNullException(nameof(getState));
            if (setState == null)
                throw new ArgumentNullException(nameof(setState));
            var entry = AddAction(target, ctx => ToggleLabel(label, getState, ctx), ctx => setState(ctx, !getState(ctx)), showIf, priority);
            entry.KeepOpen = true;
            return entry;
        }

        /// <summary>True while a right-click menu is open.</summary>
        public static bool IsOpen
        {
            get
            {
                try
                {
                    var window = GameServices.TryGet<IContextMenuWindow>();
                    if (window != null)
                        return window.IsOpen;
                    var view = GameServices.FindObject<Il2CppViews.ContextMenu.ContextMenuWindow>();
                    return view != null && view.gameObject.activeInHierarchy;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>Closes the right-click menu if one is open, the same as clicking away from it.</summary>
        public static bool Close()
        {
            var service = GameServices.TryGet<IContextMenuService>();
            if (service == null)
                return false;
            service.CloseMenu();
            return true;
        }

        /// <summary>Removes every action that was added by any mod.</summary>
        public static void RemoveAll()
        {
            foreach (var entry in Entries.ToArray())
                entry.Remove();
        }
    }
}
