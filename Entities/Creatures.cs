using System;
using System.Collections.Generic;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using Il2CppEffectors;
using Il2CppEffectors.ReceiveMethods;
using Il2CppLVA.Creatures;
using Il2CppLVA.Creatures.Implementations;
using Il2CppLVA.Limbs;
using Il2CppLVA.Limbs.References;
using Il2CppServices.Creatures;
using UnityEngine;

namespace FruktSharedLibrary.Entities
{
    /// <summary>
    /// Finding, spawning and deleting creatures (humans, and the separate "creatures" that severed body parts
    /// become). See <see cref="CreatureExtensions"/> for what you can do with a creature once you have it.
    /// </summary>
    public static class Creatures
    {
        private static Il2CppSystem.Collections.Generic.List<AbstractCreature> _buffer;

        /// <summary>Every creature currently registered in the world (including severed parts).</summary>
        public static List<AbstractCreature> All
        {
            get
            {
                var registry = GameServices.TryGet<ICreatureRegistryService>();
                if (registry == null)
                    return GameServices.FindObjects<AbstractCreature>();

                _buffer ??= new Il2CppSystem.Collections.Generic.List<AbstractCreature>();
                _buffer.Clear();
                registry.CopyEntitiesTo(_buffer);
                var result = new List<AbstractCreature>(_buffer.Count);
                for (int i = 0; i < _buffer.Count; i++)
                {
                    var creature = _buffer[i];
                    if (creature.Exists())
                        result.Add(creature);
                }
                _buffer.Clear();
                return result;
            }
        }

        /// <summary>Number of registered creatures.</summary>
        public static int Count => GameServices.TryGet<ICreatureRegistryService>()?.Count ?? All.Count;

        /// <summary>All humans (whole or partial).</summary>
        public static IEnumerable<Human> Humans => All.Select(c => c.TryCast<Human>()).Where(h => h != null);

        /// <summary>All creatures that are still alive.</summary>
        public static IEnumerable<AbstractCreature> Living => All.Where(c => c.IsLiving());

        /// <summary>The creature closest to a point (measured to its root limb).</summary>
        public static AbstractCreature GetNearest(Vector3 position, float maxDistance = float.PositiveInfinity, bool livingOnly = false)
        {
            AbstractCreature best = null;
            float bestDistance = maxDistance * maxDistance;
            foreach (var creature in All)
            {
                if (livingOnly && !creature.IsLiving())
                    continue;
                float distance = (creature.GetPosition() - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = creature;
                }
            }
            return best;
        }

        // ------------------------------------------------------------ lookups from physics

        /// <summary>The limb a collider belongs to, or null if it isn't part of a creature.</summary>
        public static AbstractLimb LimbFromCollider(Collider collider)
        {
            if (!collider.Exists())
                return null;

            var references = collider.GetComponentInParentIl2Cpp<LimbReferencesPublic>();
            if (references.Exists())
                return references.Limb;

            try
            {
                IIndexEffectorSignalReceiver receiver = null;
                if (EffectorsTools.TryGetIndexEffectorSignalReceiver(collider, out receiver) && receiver != null)
                {
                    var limbReceiver = receiver.TryCast<LimbEffectorReceiver>();
                    if (limbReceiver != null)
                        return limbReceiver.m_limbReferences?.Limb;
                }
            }
            catch (Exception e)
            {
                FruktLog.Debug("Effector receiver lookup failed: " + e.Message);
            }
            return null;
        }

        /// <summary>The limb a GameObject belongs to (searches up the hierarchy).</summary>
        public static AbstractLimb LimbFromGameObject(GameObject gameObject)
        {
            var references = gameObject.GetComponentInParentIl2Cpp<LimbReferencesPublic>();
            return references.Exists() ? references.Limb : null;
        }

        /// <summary>The creature a collider belongs to.</summary>
        public static AbstractCreature FromCollider(Collider collider) => LimbFromCollider(collider)?.GetCreature();

        /// <summary>The creature a GameObject belongs to.</summary>
        public static AbstractCreature FromGameObject(GameObject gameObject)
        {
            if (!gameObject.Exists())
                return null;
            var creature = gameObject.GetComponentInParentIl2Cpp<AbstractCreature>();
            return creature.Exists() ? creature : LimbFromGameObject(gameObject)?.GetCreature();
        }

        /// <summary>The limb under the player's crosshair, if any.</summary>
        public static AbstractLimb GetAimedLimb(out RaycastHit hit, float maxDistance = 500f)
        {
            if (LocalPlayer.Raycast(out hit, maxDistance))
                return LimbFromCollider(hit.collider);
            return null;
        }

        /// <summary>The creature under the player's crosshair, if any.</summary>
        public static AbstractCreature GetAimedCreature(float maxDistance = 500f)
            => GetAimedLimb(out _, maxDistance)?.GetCreature();

        // ------------------------------------------------------------ spawning & deleting

        /// <summary>
        /// Spawns a human. Humans are assembled over a few frames, so the creature is handed to
        /// <paramref name="onSpawned"/> once it has finished initializing.
        /// </summary>
        public static bool SpawnHuman(Vector3 position, Quaternion rotation, Action<AbstractCreature> onSpawned = null)
        {
            var factory = GameServices.TryGet<IHumanFactory>();
            if (factory == null)
            {
                FruktLog.Warning("Can't spawn a human: the human factory isn't available (not in a map?).");
                return false;
            }
            try
            {
                if (onSpawned != null)
                    Internal.CreatureTracker.ExpectSpawn(position, onSpawned);
                factory.CreateHuman(position, rotation);
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning("Spawning a human failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Spawns a human in front of the player, facing the player.</summary>
        public static bool SpawnHumanInFront(float distance = 3f, Action<AbstractCreature> onSpawned = null)
        {
            var position = LocalPlayer.GetPointInFront(distance);
            return SpawnHuman(position, LocalPlayer.RotationFacingPlayer(position), onSpawned);
        }

        /// <summary>Deletes every creature (same as the terminal's "delete everyone").</summary>
        public static bool DeleteAll() => World.DeleteAllCreatures();

        /// <summary>Deletes dead bodies and loose parts (same as the terminal's "delete bodies").</summary>
        public static bool DeleteBodies() => World.DeleteBodies();
    }
}
