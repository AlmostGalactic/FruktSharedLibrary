using System;
using System.Reflection;
using FruktSharedLibrary.Core;
using HarmonyLib;
using Il2CppData.Maps;
using Il2CppGame;
using Il2CppInfrastructure.Project.States.Variants;
using Il2CppLVA.Creatures;
using Il2CppLVA.Creatures.Hierarchy;
using Il2CppLVA.LimbContextMenu;
using Il2CppLVA.LimbContextMenu.Actions;
using Il2CppLVA.Limbs;
using Il2CppMap.Spinner;
using Il2CppPlayer.GameplayInput.ButtonsActions.MouseKeyboard;
using Il2CppSpawnables.ContextMenus;
using Il2CppSpawnables.Weapons;

namespace FruktSharedLibrary.Internal
{
    /// <summary>The library's own Harmony hooks into the game. Each is applied (and can fail) independently.</summary>
    internal static class LibraryPatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            int ok = 0, total = 0;
            void Patch(Type type, string method, Type[] args, string prefix = null, string postfix = null)
            {
                total++;
                if (Patcher.TryPatch(harmony, type, method, args, Hook(prefix), Hook(postfix)))
                    ok++;
            }

            // Game flow.
            // Note: trivial methods (such as SandboxState.Exit, which is empty) must never be patched. IL2CPP folds
            // identical native code into one function, so patching one would hook every method sharing that body.
            Patch(typeof(MainMenuState), nameof(MainMenuState.Enter), Type.EmptyTypes, postfix: nameof(MainMenuEntered));
            Patch(typeof(SandboxState), nameof(SandboxState.Enter), new[] { typeof(MapID) }, postfix: nameof(SandboxEntered));
            Patch(typeof(PauseService), nameof(PauseService.SetPause), Type.EmptyTypes, postfix: nameof(PauseChanged));
            Patch(typeof(PauseService), nameof(PauseService.CancelPause), Type.EmptyTypes, postfix: nameof(PauseChanged));

            // Gameplay events
            Patch(typeof(KillsService), nameof(KillsService.AddKill), Type.EmptyTypes, postfix: nameof(KillAdded));
            Patch(typeof(Firearm), nameof(Firearm.ShootLogic), Type.EmptyTypes, postfix: nameof(FirearmShot));
            Patch(typeof(LimbDetachModule), nameof(LimbDetachModule.ProcessDetachedLimbs), null, postfix: nameof(LimbsDetached));

            // Custom context-menu actions
            Patch(typeof(LimbContextMenuActionsHandler), nameof(LimbContextMenuActionsHandler.InitializeContextActions), Type.EmptyTypes, postfix: nameof(LimbMenu));
            Patch(typeof(PropContextMenuActionsHandler), nameof(PropContextMenuActionsHandler.InitializeContextActions), Type.EmptyTypes, postfix: nameof(PropMenu));
            Patch(typeof(FirearmContextMenuActionsHandler), nameof(FirearmContextMenuActionsHandler.InitializeContextActions), Type.EmptyTypes, postfix: nameof(FirearmMenu));
            Patch(typeof(NPCSpawnerContextMenuActionsHandler), nameof(NPCSpawnerContextMenuActionsHandler.InitializeContextActions), Type.EmptyTypes, postfix: nameof(SpawnerMenu));
            Patch(typeof(SpinnerContextMenuActionsHandler), nameof(SpinnerContextMenuActionsHandler.InitializeContextActions), Type.EmptyTypes, postfix: nameof(SpinnerMenu));
            Patch(typeof(DeleteCreatureContextMenuAction), nameof(DeleteCreatureContextMenuAction.ExecuteLogic), Type.EmptyTypes, prefix: nameof(CarrierExecute));
            // Every menu click; lets drop-down groups and toggles act without closing the menu.
            Patch(typeof(Il2CppPlayer.ContextMenu.Actions.ContextMenuAction), nameof(Il2CppPlayer.ContextMenu.Actions.ContextMenuAction.Execute), Type.EmptyTypes, prefix: nameof(MenuActionExecute));

            // Esc belongs to the mod menu while it is open (the pause button ignores the input block).
            Patch(typeof(PauseToggleButton), "OnPressInternal", Type.EmptyTypes, prefix: nameof(PauseButtonPressed));
            Patch(typeof(MenuBackButton), "OnPressInternal", Type.EmptyTypes, prefix: nameof(MenuBackPressed));

            FruktLog.Msg($"Game hooks: {ok}/{total} applied.");
            Status = (ok, total);
        }

        internal static (int Applied, int Total) Status { get; private set; }

        private static MethodInfo Hook(string name)
            => name == null ? null : AccessTools.Method(typeof(LibraryPatches), name);

        // ------------------------------------------------------------ hooks

        private static void MainMenuEntered(MainMenuState __instance)
        {
            if (Expect<MainMenuState>(__instance, nameof(MainMenuEntered)))
                Guard(GameFlow.OnMainMenuEntered);
        }

        private static void SandboxEntered(SandboxState __instance, MapID map)
        {
            if (Expect<SandboxState>(__instance, nameof(SandboxEntered)))
                Guard(() => GameFlow.OnMapLoading(map));
        }

        private static void PauseChanged(PauseService __instance)
        {
            if (Expect<PauseService>(__instance, nameof(PauseChanged)))
                Guard(() => GameEvents.RaisePauseChanged(__instance.Paused));
        }

        private static void KillAdded(KillsService __instance)
        {
            if (Expect<KillsService>(__instance, nameof(KillAdded)))
                Guard(() => GameEvents.RaiseKillAdded(__instance.KillsCount));
        }

        private static void FirearmShot(Firearm __instance)
        {
            if (Expect<Firearm>(__instance, nameof(FirearmShot)))
                Guard(() => GameEvents.RaiseFirearmFired(__instance));
        }

        private static void LimbsDetached(LimbDetachModule __instance, AbstractCreature newDetachedLimbsOwner, AbstractLimb detachedLimbRoot)
        {
            if (Expect<LimbDetachModule>(__instance, nameof(LimbsDetached)))
                Guard(() => GameEvents.RaiseLimbDetached(newDetachedLimbsOwner, detachedLimbRoot));
        }

        private static void LimbMenu(LimbContextMenuActionsHandler __instance, Il2CppSystem.Collections.Generic.List<Il2CppPlayer.ContextMenu.Actions.ContextMenuAction> __result)
        {
            if (Expect<LimbContextMenuActionsHandler>(__instance, nameof(LimbMenu)))
                Guard(() => ContextMenuCarrier.LimbHandlerPostfix(__instance, __result));
        }

        private static void PropMenu(PropContextMenuActionsHandler __instance, Il2CppSystem.Collections.Generic.List<Il2CppPlayer.ContextMenu.Actions.ContextMenuAction> __result)
        {
            if (Expect<PropContextMenuActionsHandler>(__instance, nameof(PropMenu)))
                Guard(() => ContextMenuCarrier.PropHandlerPostfix(__instance, __result));
        }

        private static void FirearmMenu(FirearmContextMenuActionsHandler __instance, Il2CppSystem.Collections.Generic.List<Il2CppPlayer.ContextMenu.Actions.ContextMenuAction> __result)
        {
            if (Expect<FirearmContextMenuActionsHandler>(__instance, nameof(FirearmMenu)))
                Guard(() => ContextMenuCarrier.FirearmHandlerPostfix(__instance, __result));
        }

        private static void SpawnerMenu(NPCSpawnerContextMenuActionsHandler __instance, Il2CppSystem.Collections.Generic.List<Il2CppPlayer.ContextMenu.Actions.ContextMenuAction> __result)
        {
            if (Expect<NPCSpawnerContextMenuActionsHandler>(__instance, nameof(SpawnerMenu)))
                Guard(() => ContextMenuCarrier.SpawnerHandlerPostfix(__instance, __result));
        }

        private static void SpinnerMenu(SpinnerContextMenuActionsHandler __instance, Il2CppSystem.Collections.Generic.List<Il2CppPlayer.ContextMenu.Actions.ContextMenuAction> __result)
        {
            if (Expect<SpinnerContextMenuActionsHandler>(__instance, nameof(SpinnerMenu)))
                Guard(() => ContextMenuCarrier.SpinnerHandlerPostfix(__instance, __result));
        }

        private static bool CarrierExecute(DeleteCreatureContextMenuAction __instance)
        {
            if (!Expect<DeleteCreatureContextMenuAction>(__instance, nameof(CarrierExecute)))
                return true;
            try
            {
                return ContextMenuCarrier.ExecutePrefix(__instance);
            }
            catch (Exception e)
            {
                FruktLog.Error("Custom context menu dispatch failed", e);
                return true;
            }
        }

        private static bool MenuActionExecute(Il2CppPlayer.ContextMenu.Actions.ContextMenuAction __instance)
        {
            if (!Expect<Il2CppPlayer.ContextMenu.Actions.ContextMenuAction>(__instance, nameof(MenuActionExecute)))
                return true;
            try
            {
                return ContextMenuCarrier.ActionExecutePrefix(__instance);
            }
            catch (Exception e)
            {
                FruktLog.Error("Context menu click dispatch failed", e);
                return true;
            }
        }

        private static bool PauseButtonPressed(PauseToggleButton __instance)
            => !Expect<PauseToggleButton>(__instance, nameof(PauseButtonPressed)) || !UI.ModMenu.OwnsEscape;

        private static bool MenuBackPressed(MenuBackButton __instance)
            => !Expect<MenuBackButton>(__instance, nameof(MenuBackPressed)) || !UI.ModMenu.OwnsEscape;

        /// <summary>Calls and wrong-instance calls per hook (diagnostics for IL2CPP code folding).</summary>
        internal static readonly System.Collections.Generic.Dictionary<string, (int Calls, int Foreign)> HookStats = new();

        /// <summary>
        /// Verifies a hook was invoked on the class it was written for. If IL2CPP folded the patched method
        /// with another identical method, other classes' calls land here too; those are ignored and reported.
        /// </summary>
        private static bool Expect<T>(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase instance, string hook)
            where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
        {
            HookStats.TryGetValue(hook, out var stats);
            stats.Calls++;
            bool ok;
            try
            {
                ok = instance != null && instance.TryCast<T>() != null;
            }
            catch
            {
                ok = false;
            }
            if (!ok)
            {
                stats.Foreign++;
                if (stats.Foreign == 1)
                    FruktLog.Warning($"Hook {hook} was invoked on a {(instance == null ? "null" : Interop.Il2CppExtensions.GetIl2CppTypeName(instance))} instead of {typeof(T).Name} (IL2CPP code folding?). Ignoring those calls.");
            }
            HookStats[hook] = stats;
            return ok;
        }

        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                FruktLog.Error("Library hook threw", e);
            }
        }
    }
}
