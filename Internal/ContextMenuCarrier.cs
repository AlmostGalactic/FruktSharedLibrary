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
using Il2CppSpawnables.ContextMenus;
using Il2CppList = Il2CppSystem.Collections.Generic.List<Il2CppPlayer.ContextMenu.Actions.ContextMenuAction>;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// Implements custom context-menu actions. The game's base action class has no body for its
    /// "execute" method, so it can't be patched or subclassed from C#. Instead each custom action is an
    /// instance of a concrete game action (<see cref="DeleteCreatureContextMenuAction"/>) whose execute
    /// method is intercepted for the instances this library created; every other instance behaves normally.
    /// </summary>
    internal static class ContextMenuCarrier
    {
        private sealed class Binding
        {
            public ContextMenuAction Action;
            public ContextMenuEntry Entry;
            public ContextMenuContext Context;
            public string Name;
        }

        private static readonly Dictionary<IntPtr, Binding> Bindings = new();

        /// <summary>
        /// Menus that have already been built. The game builds a limb's menu when the limb spawns (not when it
        /// is opened), so entries registered later are pushed into these directly.
        /// </summary>
        private static readonly Dictionary<IntPtr, ContextMenuContext> LiveMenus = new();

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

        /// <summary>Prefix on DeleteCreatureContextMenuAction.ExecuteLogic.</summary>
        internal static bool ExecutePrefix(DeleteCreatureContextMenuAction __instance)
        {
            if (__instance == null || !Bindings.TryGetValue(__instance.Pointer, out var binding))
                return true;

            // Defensive: if the object at this address isn't the action we created, let the game handle it.
            if (!string.Equals(__instance._Name_k__BackingField, binding.Name, StringComparison.Ordinal))
            {
                Bindings.Remove(__instance.Pointer);
                return true;
            }

            try
            {
                binding.Entry.OnClick(binding.Context);
            }
            catch (Exception e)
            {
                FruktLog.Error($"Context menu action '{binding.Name}' threw", e);
            }

            try
            {
                binding.Name = binding.Entry.Label(binding.Context) ?? binding.Name;
                __instance._Name_k__BackingField = binding.Name;
            }
            catch (Exception e)
            {
                FruktLog.Debug("Refreshing an action label failed: " + e.Message);
            }
            return false;
        }

        // ------------------------------------------------------------ building

        private static void Append(ContextMenuActionsHandler handler, Il2CppList result, ContextMenuContext context)
        {
            if (handler == null || result == null)
                return;
            context.Handler = handler;
            LiveMenus[handler.Pointer] = context;

            foreach (var entry in ContextMenus.Entries.ToArray())
            {
                if (entry.Removed || entry.Target != context.Target)
                    continue;
                try
                {
                    if (entry.ShowIf != null && !entry.ShowIf(context))
                        continue;
                    result.Add(Create(entry, context));
                }
                catch (Exception e)
                {
                    FruktLog.Error("Adding a context menu action failed", e);
                }
            }
        }

        private static ContextMenuAction Create(ContextMenuEntry entry, ContextMenuContext context)
        {
            string name = entry.Label(context) ?? "?";
            var action = new DeleteCreatureContextMenuAction((AbstractLimb)null)
            {
                _Name_k__BackingField = name,
                _Priority_k__BackingField = entry.Priority,
            };
            Bindings[action.Pointer] = new Binding { Action = action, Entry = entry, Context = context, Name = name };
            return action;
        }

        /// <summary>Adds a newly registered entry to every menu that was already built.</summary>
        internal static void OnEntryAdded(ContextMenuEntry entry)
        {
            foreach (var context in new List<ContextMenuContext>(LiveMenus.Values))
            {
                if (context.Target != entry.Target || !context.Handler.Exists())
                    continue;
                try
                {
                    if (entry.ShowIf != null && !entry.ShowIf(context))
                        continue;
                    context.Handler.AddContextAction(Create(entry, context));
                }
                catch (Exception e)
                {
                    FruktLog.Debug("Adding an action to a live menu failed: " + e.Message);
                }
            }
        }

        /// <summary>Removes an entry's actions from every menu they were added to.</summary>
        internal static void OnEntryRemoved(ContextMenuEntry entry)
        {
            var remove = new List<IntPtr>();
            foreach (var pair in Bindings)
            {
                if (pair.Value.Entry != entry)
                    continue;
                remove.Add(pair.Key);
                var binding = pair.Value;
                try
                {
                    if (binding.Context.Handler.Exists())
                        binding.Context.Handler.RemoveContextAction(binding.Action);
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

        /// <summary>Drops bindings and menus that no longer exist (keeps memory bounded).</summary>
        internal static void Cleanup()
        {
            var dead = new List<IntPtr>();
            foreach (var pair in Bindings)
            {
                if (!pair.Value.Context.Handler.Exists())
                    dead.Add(pair.Key);
            }
            foreach (var key in dead)
                Bindings.Remove(key);

            dead.Clear();
            foreach (var pair in LiveMenus)
            {
                if (!pair.Value.Handler.Exists())
                    dead.Add(pair.Key);
            }
            foreach (var key in dead)
                LiveMenus.Remove(key);
        }

        internal static void Clear()
        {
            Bindings.Clear();
            LiveMenus.Clear();
        }
    }
}
