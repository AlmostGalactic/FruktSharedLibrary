using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.UI;
using Il2CppLVA.LimbContextMenu;
using Il2CppLVA.LimbContextMenu.Actions;
using Il2CppLVA.Limbs;
using Il2CppMap.Spinner;
using Il2CppPlayer.ContextMenu;
using Il2CppPlayer.ContextMenu.Actions;
using Il2CppServices.UI;
using Il2CppSpawnables.ContextMenus;
using Il2CppUI.ContextMenu;
using Il2CppList = Il2CppSystem.Collections.Generic.List<Il2CppPlayer.ContextMenu.Actions.ContextMenuAction>;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// Implements custom context-menu actions. The game's base action class has no body for its
    /// "execute" method, so it can't be patched or subclassed from C#. Instead each custom action is an
    /// instance of a concrete game action (<see cref="DeleteCreatureContextMenuAction"/>) whose execute
    /// method is intercepted for the instances this library created; every other instance behaves normally.
    ///
    /// Groups: the game's menu is a flat list sorted by priority that redraws whenever an action becomes
    /// (un)available. A group is a line whose children sit right below it (the library assigns its own actions
    /// consecutive priorities) and are only available while the group is expanded. Clicks on groups and toggles
    /// are handled before the game's <c>Execute</c> runs, so the menu stays open.
    /// </summary>
    internal static class ContextMenuCarrier
    {
        private const string Indent = "   ";

        private sealed class Binding
        {
            public ContextMenuAction Action;
            public ContextMenuEntry Entry;
            public MenuState Menu;
            public Binding Parent;
            public string Name;
            public bool Visible;
        }

        /// <summary>The library's actions in one object's menu.</summary>
        private sealed class MenuState
        {
            public ContextMenuContext Context;
            public readonly List<Binding> Bindings = new();
            public readonly HashSet<ContextMenuEntry> Expanded = new();
            public int BuiltInMin = 995;
            public int BuiltInMax = 1000;
        }

        private static readonly Dictionary<IntPtr, Binding> Bindings = new();

        /// <summary>
        /// Menus that have already been built. The game builds a limb's menu when the limb spawns (not when it
        /// is opened), so entries registered later are pushed into these directly.
        /// </summary>
        private static readonly Dictionary<IntPtr, MenuState> LiveMenus = new();

        private static IDisposable _closedSubscription;
        private static IntPtr _closedService;

        /// <summary>Runs a game action's logic directly (used for delete/detach helpers).</summary>
        internal static void RunGameLogic(ContextMenuAction action)
        {
            try
            {
                action.ExecuteLogic();
            }
            finally
            {
                try
                {
                    action.Dispose();
                }
                catch
                {
                    // Some actions don't need disposing.
                }
            }
        }

        // ------------------------------------------------------------ Harmony callbacks

        internal static void LimbHandlerPostfix(LimbContextMenuActionsHandler __instance, Il2CppList __result)
            => Append(__instance, __result, new ContextMenuContext { Target = ContextMenuTarget.Limb, Limb = __instance?.m_assignedLimb });

        internal static void PropHandlerPostfix(PropContextMenuActionsHandler __instance, Il2CppList __result)
            => Append(__instance, __result, new ContextMenuContext { Target = ContextMenuTarget.Prop, Prop = __instance?.m_prop });

        internal static void FirearmHandlerPostfix(FirearmContextMenuActionsHandler __instance, Il2CppList __result)
            => Append(__instance, __result, new ContextMenuContext { Target = ContextMenuTarget.Firearm, Firearm = __instance?.m_firearm });

        internal static void SpawnerHandlerPostfix(NPCSpawnerContextMenuActionsHandler __instance, Il2CppList __result)
            => Append(__instance, __result, new ContextMenuContext { Target = ContextMenuTarget.HumanSpawner, Spawner = __instance?.m_spawner });

        internal static void SpinnerHandlerPostfix(SpinnerContextMenuActionsHandler __instance, Il2CppList __result)
            => Append(__instance, __result, new ContextMenuContext { Target = ContextMenuTarget.Spinner, Spinner = __instance?.m_spinner });

        /// <summary>
        /// Prefix on ContextMenuAction.Execute (every menu click). Groups and toggles are handled here so the menu
        /// stays open; plain actions continue into the game's Execute, which calls ExecuteLogic and closes the menu.
        /// </summary>
        internal static bool ActionExecutePrefix(ContextMenuAction __instance)
        {
            var binding = Find(__instance);
            if (binding == null || !binding.Entry.KeepOpen)
                return true;
            FruktLog.Debug($"Context menu click on '{binding.Name.Trim()}' (keeps the menu open)");
            try
            {
                if (binding.Entry.IsGroup)
                    ToggleGroup(binding);
                else
                    RunClick(binding);
                Redraw(binding.Menu);
            }
            catch (Exception e)
            {
                FruktLog.Error($"Context menu line '{binding.Name}' threw", e);
            }
            return false;
        }

        /// <summary>Prefix on DeleteCreatureContextMenuAction.ExecuteLogic (plain actions).</summary>
        internal static bool ExecutePrefix(DeleteCreatureContextMenuAction __instance)
        {
            var binding = Find(__instance);
            if (binding == null)
                return true;
            if (binding.Entry.IsGroup)
            {
                // Only reached if the Execute hook is missing: expand anyway (the game closes the menu after this).
                ToggleGroup(binding);
                return false;
            }
            RunClick(binding);
            return false;
        }

        private static Binding Find(ContextMenuAction action)
        {
            if (action == null || !Bindings.TryGetValue(action.Pointer, out var binding))
                return null;
            // Defensive: if the object at this address isn't the action we created, let the game handle it.
            if (!string.Equals(action._Name_k__BackingField, binding.Name, StringComparison.Ordinal))
            {
                Bindings.Remove(action.Pointer);
                return null;
            }
            return binding;
        }

        private static void RunClick(Binding binding)
        {
            try
            {
                binding.Entry.OnClick?.Invoke(binding.Menu.Context);
            }
            catch (Exception e)
            {
                FruktLog.Error($"Context menu action '{binding.Name}' threw", e);
            }
            Relabel(binding);
        }

        // ------------------------------------------------------------ groups

        private static void ToggleGroup(Binding group)
        {
            var menu = group.Menu;
            if (menu.Expanded.Contains(group.Entry))
                Collapse(menu, group.Entry);
            else
                menu.Expanded.Add(group.Entry);
            Relabel(group);
            Layout(menu, live: true);
            ListenForClose();
        }

        private static void Collapse(MenuState menu, ContextMenuEntry entry)
        {
            menu.Expanded.Remove(entry);
            foreach (var child in entry.Children)
                Collapse(menu, child);
        }

        /// <summary>Collapses every group when a menu closes, so the next menu starts tidy.</summary>
        private static void CollapseAll()
        {
            foreach (var menu in LiveMenus.Values)
            {
                if (menu.Expanded.Count == 0)
                    continue;
                menu.Expanded.Clear();
                try
                {
                    foreach (var binding in menu.Bindings)
                        Relabel(binding);
                    Layout(menu, live: true);
                }
                catch (Exception e)
                {
                    FruktLog.Debug("Collapsing a context menu failed: " + e.Message);
                }
            }
        }

        private static void ListenForClose()
        {
            var service = GameServices.TryGet<IContextMenuService>();
            if (service == null || (_closedSubscription != null && service.Pointer == _closedService))
                return;
            _closedSubscription?.Dispose();
            _closedSubscription = service.OnClosed.Listen(CollapseAll);
            _closedService = service.Pointer;
        }

        /// <summary>Makes the open menu redraw its rows (labels changed; the action list may not have).</summary>
        private static void Redraw(MenuState menu)
        {
            try
            {
                GameServices.TryGet<IContextMenuService>()?.TryCast<ContextMenuService>()?.ProcessChangeAvailableActions();
            }
            catch (Exception e)
            {
                FruktLog.Debug("Redrawing the context menu failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------ layout

        private static string DisplayName(MenuState menu, ContextMenuEntry entry)
        {
            string text;
            try
            {
                text = entry.Label(menu.Context) ?? "?";
            }
            catch (Exception e)
            {
                FruktLog.Debug("A context menu label threw: " + e.Message);
                text = "?";
            }
            if (entry.IsGroup)
                text = (menu.Expanded.Contains(entry) ? "- " : "+ ") + text;
            for (int i = 0; i < entry.Depth; i++)
                text = Indent + text;
            return text;
        }

        private static void Relabel(Binding binding)
        {
            try
            {
                binding.Name = DisplayName(binding.Menu, binding.Entry);
                binding.Action._Name_k__BackingField = binding.Name;
            }
            catch (Exception e)
            {
                FruktLog.Debug("Refreshing an action label failed: " + e.Message);
            }
        }

        /// <summary>
        /// Gives the library's actions in a menu consecutive priorities in tree order (so a group's children sit
        /// right under it) on the same side of the game's own actions as their top-level entry asked for, and shows
        /// only the children of expanded groups.
        /// </summary>
        private static void Layout(MenuState menu, bool live)
        {
            var ordered = new List<Binding>();
            void Walk(Binding parent)
            {
                var level = menu.Bindings.FindAll(b => b.Parent == parent);
                if (parent == null)
                    level.Sort((a, b) => a.Entry.Priority != b.Entry.Priority ? b.Entry.Priority.CompareTo(a.Entry.Priority) : a.Entry.Order.CompareTo(b.Entry.Order));
                else
                    level.Sort((a, b) => a.Entry.Order.CompareTo(b.Entry.Order));
                foreach (var binding in level)
                {
                    ordered.Add(binding);
                    Walk(binding);
                }
            }
            Walk(null);

            var above = ordered.FindAll(b => Root(b).Entry.Priority > menu.BuiltInMax);
            var below = ordered.FindAll(b => Root(b).Entry.Priority <= menu.BuiltInMax);
            for (int i = 0; i < above.Count; i++)
                above[i].Action._Priority_k__BackingField = menu.BuiltInMax + above.Count - i;
            for (int i = 0; i < below.Count; i++)
                below[i].Action._Priority_k__BackingField = menu.BuiltInMin - 1 - i;

            foreach (var binding in ordered)
            {
                bool visible = IsShown(menu, binding);
                if (!live)
                {
                    binding.Action._IsAvailable_k__BackingField = visible;
                    binding.Visible = visible;
                }
                else if (visible != binding.Visible)
                {
                    binding.Visible = visible;
                    binding.Action.UpdateAvailableValue(visible);
                }
            }
        }

        private static Binding Root(Binding binding)
        {
            while (binding.Parent != null)
                binding = binding.Parent;
            return binding;
        }

        private static bool IsShown(MenuState menu, Binding binding)
        {
            for (var parent = binding.Parent; parent != null; parent = parent.Parent)
            {
                if (!menu.Expanded.Contains(parent.Entry))
                    return false;
            }
            return true;
        }

        // ------------------------------------------------------------ building

        private static void Append(ContextMenuActionsHandler handler, Il2CppList result, ContextMenuContext context)
        {
            if (handler == null || result == null)
                return;
            context.Handler = handler;
            var menu = new MenuState { Context = context };
            MeasureBuiltIns(menu, result);

            foreach (var entry in ContextMenus.Entries.ToArray())
            {
                if (!entry.Removed && entry.Target == context.Target)
                    AddTree(menu, entry, null, action => result.Add(action));
            }
            Layout(menu, live: false);

            // The first build of a handler is the live one; later calls (e.g. code asking for the list again)
            // return extra copies that never reach the menu.
            bool initialized = false;
            try
            {
                initialized = handler.Initialized;
            }
            catch
            {
                // Treat as a first build.
            }
            if (!initialized || !LiveMenus.ContainsKey(handler.Pointer))
                LiveMenus[handler.Pointer] = menu;
        }

        private static void MeasureBuiltIns(MenuState menu, Il2CppList actions)
        {
            int min = int.MaxValue, max = int.MinValue;
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null || Bindings.ContainsKey(action.Pointer))
                    continue;
                min = Math.Min(min, action.Priority);
                max = Math.Max(max, action.Priority);
            }
            if (min <= max)
            {
                menu.BuiltInMin = min;
                menu.BuiltInMax = max;
            }
        }

        private static void AddTree(MenuState menu, ContextMenuEntry entry, Binding parent, Action<ContextMenuAction> add)
        {
            if (entry.Removed)
                return;
            try
            {
                if (entry.ShowIf != null && !entry.ShowIf(menu.Context))
                    return;
                var binding = Create(menu, entry, parent);
                add(binding.Action);
                foreach (var child in entry.Children.ToArray())
                    AddTree(menu, child, binding, add);
            }
            catch (Exception e)
            {
                FruktLog.Error("Adding a context menu action failed", e);
            }
        }

        private static Binding Create(MenuState menu, ContextMenuEntry entry, Binding parent)
        {
            string name = DisplayName(menu, entry);
            var action = new DeleteCreatureContextMenuAction((AbstractLimb)null)
            {
                _Name_k__BackingField = name,
                _Priority_k__BackingField = entry.Priority,
            };
            var binding = new Binding { Action = action, Entry = entry, Menu = menu, Parent = parent, Name = name };
            binding.Visible = IsShown(menu, binding);
            action._IsAvailable_k__BackingField = binding.Visible;
            menu.Bindings.Add(binding);
            Bindings[action.Pointer] = binding;
            return binding;
        }

        /// <summary>Adds a newly registered entry to every menu that was already built.</summary>
        internal static void OnEntryAdded(ContextMenuEntry entry)
        {
            foreach (var menu in new List<MenuState>(LiveMenus.Values))
            {
                if (menu.Context.Target != entry.Target || !menu.Context.Handler.Exists())
                    continue;
                try
                {
                    var parents = entry.Parent == null
                        ? new List<Binding> { null }
                        : menu.Bindings.FindAll(b => b.Entry == entry.Parent);
                    var handler = menu.Context.Handler;
                    foreach (var parent in parents)
                        AddTree(menu, entry, parent, action => handler.AddContextAction(action));
                    Layout(menu, live: true);
                }
                catch (Exception e)
                {
                    FruktLog.Debug("Adding an action to a live menu failed: " + e.Message);
                }
            }
        }

        /// <summary>Removes an entry's actions (and those of everything inside it) from every menu.</summary>
        internal static void OnEntryRemoved(ContextMenuEntry entry)
        {
            var remove = new List<IntPtr>();
            foreach (var pair in Bindings)
            {
                if (!IsWithin(pair.Value.Entry, entry))
                    continue;
                remove.Add(pair.Key);
                var binding = pair.Value;
                binding.Menu.Bindings.Remove(binding);
                binding.Menu.Expanded.Remove(binding.Entry);
                try
                {
                    if (binding.Menu.Context.Handler.Exists())
                        binding.Menu.Context.Handler.RemoveContextAction(binding.Action);
                    binding.Action.Dispose();
                }
                catch (Exception e)
                {
                    FruktLog.Debug("Removing an action from a menu failed: " + e.Message);
                }
            }
            foreach (var key in remove)
                Bindings.Remove(key);
        }

        private static bool IsWithin(ContextMenuEntry entry, ContextMenuEntry ancestor)
        {
            for (var e = entry; e != null; e = e.Parent)
            {
                if (e == ancestor)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------ self-test access

        /// <summary>The library's action for <paramref name="entry"/> in a handler's live menu.</summary>
        internal static ContextMenuAction ActionFor(ContextMenuActionsHandler handler, ContextMenuEntry entry)
            => handler != null && LiveMenus.TryGetValue(handler.Pointer, out var menu)
                ? menu.Bindings.Find(b => b.Entry == entry)?.Action
                : null;

        // ------------------------------------------------------------ housekeeping

        /// <summary>Drops bindings and menus that no longer exist (keeps memory bounded).</summary>
        internal static void Cleanup()
        {
            var dead = new List<IntPtr>();
            foreach (var pair in Bindings)
            {
                if (!pair.Value.Menu.Context.Handler.Exists())
                    dead.Add(pair.Key);
            }
            foreach (var key in dead)
                Bindings.Remove(key);

            dead.Clear();
            foreach (var pair in LiveMenus)
            {
                if (!pair.Value.Context.Handler.Exists())
                    dead.Add(pair.Key);
            }
            foreach (var key in dead)
                LiveMenus.Remove(key);
        }

        internal static void Clear()
        {
            Bindings.Clear();
            LiveMenus.Clear();
            _closedSubscription?.Dispose();
            _closedSubscription = null;
            _closedService = IntPtr.Zero;
        }
    }
}
