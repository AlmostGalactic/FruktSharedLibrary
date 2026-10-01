using System;
using Il2CppData.Maps;
using Il2CppLVA.Creatures;
using Il2CppLVA.Limbs;
using Il2CppSpawnables.Weapons;

namespace FruktSharedLibrary.Core
{
    /// <summary>
    /// Game-wide events you can subscribe to from your mod. Every handler is called inside its own
    /// try/catch, so one broken mod can't stop the others from receiving the event.
    /// </summary>
    /// <example>
    /// <code>
    /// GameEvents.SandboxReady += map => LoggerInstance.Msg($"Loaded {map}");
    /// GameEvents.CreatureDied += creature => Notifications.Show($"{creature.GetDisplayName()} died");
    /// </code>
    /// </example>
    public static class GameEvents
    {
        // ------------------------------------------------------------ flow

        /// <summary>The main menu became active.</summary>
        public static event Action MainMenuEntered;

        /// <summary>A map started loading (the loading curtain is up).</summary>
        public static event Action<MapID> MapLoading;

        /// <summary>A map finished loading and the player and services are ready to use.</summary>
        public static event Action<MapID> SandboxReady;

        /// <summary>The sandbox is being left (going back to the menu or reloading).</summary>
        public static event Action SandboxExited;

        /// <summary>A Unity scene finished loading (raw scene name).</summary>
        public static event Action<string> SceneLoaded;

        /// <summary>The game was paused (true) or unpaused (false).</summary>
        public static event Action<bool> PauseChanged;

        // ------------------------------------------------------------ creatures

        /// <summary>
        /// A creature finished initializing. Note that severed body parts also become their own creature.
        /// </summary>
        public static event Action<AbstractCreature> CreatureSpawned;

        /// <summary>A creature became lifeless.</summary>
        public static event Action<AbstractCreature> CreatureDied;

        /// <summary>A creature left the world (deleted, swept, or merged). The object may already be destroyed.</summary>
        public static event Action<AbstractCreature> CreatureRemoved;

        /// <summary>
        /// A limb (and everything attached below it) was detached. Arguments: the creature that now owns the
        /// detached part, and the root limb of the detached part.
        /// </summary>
        public static event Action<AbstractCreature, AbstractLimb> LimbDetached;

        /// <summary>The kill counter went up. Argument: the new kill count.</summary>
        public static event Action<int> KillAdded;

        // ------------------------------------------------------------ weapons

        /// <summary>A firearm fired a shot.</summary>
        public static event Action<Firearm> FirearmFired;

        // ------------------------------------------------------------ loop

        /// <summary>Called every frame (MelonLoader OnUpdate), after the library's own update.</summary>
        public static event Action Update;

        /// <summary>Called every physics step.</summary>
        public static event Action FixedUpdate;

        /// <summary>Called every frame after Update.</summary>
        public static event Action LateUpdate;

        // ------------------------------------------------------------ raising

        internal static void RaiseMainMenuEntered() => Raise(MainMenuEntered, nameof(MainMenuEntered));
        internal static void RaiseMapLoading(MapID map) => Raise(MapLoading, map, nameof(MapLoading));
        internal static void RaiseSandboxReady(MapID map) => Raise(SandboxReady, map, nameof(SandboxReady));
        internal static void RaiseSandboxExited() => Raise(SandboxExited, nameof(SandboxExited));
        internal static void RaiseSceneLoaded(string scene) => Raise(SceneLoaded, scene, nameof(SceneLoaded));
        internal static void RaisePauseChanged(bool paused) => Raise(PauseChanged, paused, nameof(PauseChanged));
        internal static void RaiseCreatureSpawned(AbstractCreature c) => Raise(CreatureSpawned, c, nameof(CreatureSpawned));
        internal static void RaiseCreatureDied(AbstractCreature c) => Raise(CreatureDied, c, nameof(CreatureDied));
        internal static void RaiseCreatureRemoved(AbstractCreature c) => Raise(CreatureRemoved, c, nameof(CreatureRemoved));
        internal static void RaiseKillAdded(int kills) => Raise(KillAdded, kills, nameof(KillAdded));
        internal static void RaiseFirearmFired(Firearm f) => Raise(FirearmFired, f, nameof(FirearmFired));
        internal static void RaiseUpdate() => Raise(Update, nameof(Update));
        internal static void RaiseFixedUpdate() => Raise(FixedUpdate, nameof(FixedUpdate));
        internal static void RaiseLateUpdate() => Raise(LateUpdate, nameof(LateUpdate));

        internal static void RaiseLimbDetached(AbstractCreature owner, AbstractLimb root)
        {
            var handlers = LimbDetached;
            if (handlers == null)
                return;
            foreach (Action<AbstractCreature, AbstractLimb> handler in handlers.GetInvocationList())
            {
                try { handler(owner, root); }
                catch (Exception e) { Report(nameof(LimbDetached), handler, e); }
            }
        }

        private static void Raise(Action handlers, string name)
        {
            if (handlers == null)
                return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception e) { Report(name, handler, e); }
            }
        }

        private static void Raise<T>(Action<T> handlers, T value, string name)
        {
            if (handlers == null)
                return;
            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try { handler(value); }
                catch (Exception e) { Report(name, handler, e); }
            }
        }

        private static void Report(string eventName, Delegate handler, Exception e)
        {
            string owner = handler.Method.DeclaringType?.Assembly.GetName().Name ?? "?";
            FruktLog.Error($"{eventName} handler {handler.Method.DeclaringType?.Name}.{handler.Method.Name} ({owner}) threw", e);
        }
    }
}
