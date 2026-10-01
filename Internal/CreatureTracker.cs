using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Interop;
using Il2CppLVA.Creatures;
using Il2CppServices.Creatures;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// Watches the creature registry and raises CreatureSpawned / CreatureDied / CreatureRemoved.
    /// Polling the registry is more robust than patching the creature lifecycle, which is generic and
    /// partly inlined in the IL2CPP build.
    /// </summary>
    internal static class CreatureTracker
    {
        private const float SpawnCallbackTimeout = 15f;
        private const float SpawnMatchDistance = 8f;

        private sealed class Tracked
        {
            public AbstractCreature Creature;
            public bool Lifeless;
        }

        private sealed class ExpectedSpawn
        {
            public Vector3 Position;
            public float Requested;
            public Action<AbstractCreature> Callback;
        }

        private static readonly Dictionary<IntPtr, Tracked> Known = new();
        private static readonly HashSet<IntPtr> Seen = new();
        private static readonly List<IntPtr> Gone = new();
        private static readonly List<ExpectedSpawn> Expected = new();
        private static Il2CppSystem.Collections.Generic.List<AbstractCreature> _buffer;

        internal static void ExpectSpawn(Vector3 position, Action<AbstractCreature> callback)
            => Expected.Add(new ExpectedSpawn { Position = position, Requested = Time.realtimeSinceStartup, Callback = callback });

        internal static void Reset()
        {
            Known.Clear();
            Expected.Clear();
        }

        internal static void Update()
        {
            if (!GameState.InSandbox)
            {
                if (Known.Count > 0 || Expected.Count > 0)
                    Reset();
                return;
            }

            var registry = GameServices.TryGet<ICreatureRegistryService>();
            if (registry == null)
                return;

            _buffer ??= new Il2CppSystem.Collections.Generic.List<AbstractCreature>();
            _buffer.Clear();
            try
            {
                registry.CopyEntitiesTo(_buffer);
            }
            catch (Exception e)
            {
                FruktLog.Debug("Reading the creature registry failed: " + e.Message);
                return;
            }

            Seen.Clear();
            for (int i = 0; i < _buffer.Count; i++)
            {
                var creature = _buffer[i];
                if (!creature.Exists())
                    continue;
                var pointer = creature.Pointer;

                if (Known.TryGetValue(pointer, out var tracked))
                {
                    Seen.Add(pointer);
                    bool lifeless = creature.Lifeless;
                    if (lifeless && !tracked.Lifeless)
                        GameEvents.RaiseCreatureDied(tracked.Creature);
                    tracked.Lifeless = lifeless;
                    continue;
                }

                if (!creature.Initialized || creature.UnderAssembly)
                    continue;

                Seen.Add(pointer);
                Known[pointer] = new Tracked { Creature = creature, Lifeless = creature.Lifeless };
                FruktLog.Debug($"Creature spawned: {creature.GetDisplayName()} ({creature.GetLimbCount()} limbs)");
                GameEvents.RaiseCreatureSpawned(creature);
                ResolveExpectedSpawn(creature);
            }
            _buffer.Clear();

            Gone.Clear();
            foreach (var pair in Known)
            {
                if (!Seen.Contains(pair.Key))
                    Gone.Add(pair.Key);
            }
            foreach (var pointer in Gone)
            {
                var creature = Known[pointer].Creature;
                Known.Remove(pointer);
                GameEvents.RaiseCreatureRemoved(creature);
            }

            if (Expected.Count > 0)
                Expected.RemoveAll(e => Time.realtimeSinceStartup - e.Requested > SpawnCallbackTimeout);
        }

        private static void ResolveExpectedSpawn(AbstractCreature creature)
        {
            if (Expected.Count == 0 || !creature.HasPuppeteer() && creature.GetLimbCount() < 2)
                return;

            var position = creature.GetPosition();
            int match = -1;
            for (int i = 0; i < Expected.Count; i++)
            {
                if ((Expected[i].Position - position).sqrMagnitude <= SpawnMatchDistance * SpawnMatchDistance)
                {
                    match = i;
                    break;
                }
            }
            if (match < 0)
                match = 0;

            var expected = Expected[match];
            Expected.RemoveAt(match);
            try
            {
                expected.Callback(creature);
            }
            catch (Exception e)
            {
                FruktLog.Error("SpawnHuman callback threw", e);
            }
        }
    }
}
