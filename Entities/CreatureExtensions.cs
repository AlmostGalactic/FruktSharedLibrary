using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using Il2CppLVA.Creatures;
using Il2CppLVA.Creatures.Implementations;
using Il2CppLVA.Creatures.Parameters;
using Il2CppLVA.Creatures.Systems;
using Il2CppLVA.LimbContextMenu.Actions;
using Il2CppLVA.Limbs;
using Il2CppLVA.NodesHierarchy.Benchmark.Variants;
using Il2CppLVA.NodesHierarchy.Nodes.Tags;
using Il2CppLVA.Puppeteers.Variants.Humanoid;
using UnityEngine;

namespace FruktSharedLibrary.Entities
{
    /// <summary>Everything you can read or do with a creature.</summary>
    public static class CreatureExtensions
    {
        // ------------------------------------------------------------ state

        /// <summary>True while the creature object exists and isn't being torn down.</summary>
        public static bool IsValid(this AbstractCreature creature)
            => creature.Exists() && !creature.BeingDestroyed && !creature.LVADestroyed;

        /// <summary>True while the creature is valid, initialized and not lifeless.</summary>
        public static bool IsLiving(this AbstractCreature creature)
            => creature.IsValid() && creature.Initialized && !creature.Lifeless;

        /// <summary>True when the creature is lifeless (dead).</summary>
        public static bool IsDead(this AbstractCreature creature) => creature.Exists() && creature.Lifeless;

        /// <summary>True for humans (including severed human parts).</summary>
        public static bool IsHuman(this AbstractCreature creature) => creature.Is<Human>();

        /// <summary>True when the creature still has a puppeteer (the "brain" that animates a whole body).</summary>
        public static bool HasPuppeteer(this AbstractCreature creature) => creature.Exists() && creature.HasAssignedPuppeteer;

        /// <summary>The creature's subject name shown in the game's UI (falls back to the object name).</summary>
        public static string GetDisplayName(this AbstractCreature creature)
        {
            if (!creature.Exists())
                return "(destroyed)";
            try
            {
                var name = creature.SubjectName;
                return string.IsNullOrEmpty(name) ? creature.name : name;
            }
            catch
            {
                return creature.name;
            }
        }

        /// <summary>The humanoid puppeteer driving this creature's animation, if any.</summary>
        public static HumanoidPuppeteer GetPuppeteer(this AbstractCreature creature)
            => creature.Exists() && creature.HasAssignedPuppeteer ? creature.AssignedPuppeteer?.TryCast<HumanoidPuppeteer>() : null;

        // ------------------------------------------------------------ behaviour

        private static Il2CppSystem.Object _walkRequester;

        /// <summary>The puppeteer's walk control (what the "Walk" context action toggles), or null.</summary>
        public static WalkInteraction GetWalkInteraction(this AbstractCreature creature)
        {
            if (!creature.Exists() || !creature.HasAssignedPuppeteer)
                return null;
            var interactions = creature.AssignedPuppeteer?.References?.ExternalInteractions?.m_interactions;
            if (interactions == null)
                return null;
            foreach (var interaction in interactions.Values)
            {
                var walk = interaction?.TryCast<WalkInteraction>();
                if (walk != null)
                    return walk;
            }
            return null;
        }

        /// <summary>True while the creature is walking.</summary>
        public static bool IsWalking(this AbstractCreature creature) => creature.GetWalkInteraction()?.Walking ?? false;

        /// <summary>
        /// Makes the creature start or stop walking (like the "Walk" context action). Only creatures that
        /// still have a puppeteer and are conscious enough will actually walk.
        /// </summary>
        public static bool SetWalking(this AbstractCreature creature, bool walking)
        {
            var walk = creature.GetWalkInteraction();
            if (walk == null)
                return false;
            _walkRequester ??= new Il2CppSystem.Object();
            bool holds = walk.HoldsWalkRequest(_walkRequester);
            if (walking && !holds)
                walk.AddWalkRequest(_walkRequester);
            else if (!walking && holds)
                walk.RemoveWalkRequest(_walkRequester);
            return true;
        }

        // ------------------------------------------------------------ body

        /// <summary>All limbs that currently belong to the creature.</summary>
        public static List<AbstractLimb> GetLimbs(this AbstractCreature creature)
        {
            var result = new List<AbstractLimb>();
            var root = creature.GetRootLimb();
            if (root != null)
            {
                // Walk the limb tree from the root; it's exactly the set of limbs the creature owns.
                var pending = new Stack<AbstractLimb>();
                var seen = new HashSet<IntPtr>();
                pending.Push(root);
                while (pending.Count > 0)
                {
                    var limb = pending.Pop();
                    if (limb == null || !seen.Add(limb.Pointer))
                        continue;
                    result.Add(limb);
                    foreach (var child in limb.GetChildLimbs())
                        pending.Push(child);
                }
                return result;
            }

            var navigator = Navigator(creature);
            return navigator == null ? result : navigator.AllLimbs.ToManagedList();
        }

        /// <summary>Number of limbs the creature currently has.</summary>
        public static int GetLimbCount(this AbstractCreature creature) => Navigator(creature)?.LimbsCount ?? 0;

        /// <summary>The root limb of the creature's hierarchy (the pelvis for a whole human).</summary>
        public static AbstractLimb GetRootLimb(this AbstractCreature creature)
        {
            try
            {
                return Navigator(creature)?.GetRoot();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>A specific human body part, or null if the creature doesn't have it (e.g. it was severed).</summary>
        public static AbstractLimb GetLimb(this AbstractCreature creature, HumanoidNodeTagValue part)
        {
            var navigator = Navigator(creature);
            if (navigator == null)
                return null;
            try
            {
                AbstractLimb limb = null;
                return navigator.TryGetNativeLimbByTag(new HumanoidNodeTag(part), out limb) ? limb : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>True when the creature still has that body part attached.</summary>
        public static bool HasLimb(this AbstractCreature creature, HumanoidNodeTagValue part) => creature.GetLimb(part) != null;

        /// <summary>World position of the creature (its root limb's rigidbody).</summary>
        public static Vector3 GetPosition(this AbstractCreature creature)
        {
            var root = creature.GetRootLimb();
            var body = root?.GetRigidbody();
            if (body.Exists())
                return body.worldCenterOfMass;
            return creature.Exists() ? creature.transform.position : Vector3.zero;
        }

        // ------------------------------------------------------------ vitals

        /// <summary>Current pain level (0 when unknown).</summary>
        public static float GetPain(this AbstractCreature creature) => creature.GetParameterValue<CreaturePain>() ?? 0f;

        /// <summary>Current cognition (consciousness) level (0 when unknown).</summary>
        public static float GetCognition(this AbstractCreature creature) => creature.GetParameterValue<CognitionLevel>() ?? 0f;

        /// <summary>Current balance level (0 when unknown).</summary>
        public static float GetBalance(this AbstractCreature creature) => creature.GetParameterValue<Balance>() ?? 0f;

        /// <summary>The blood tank system, which holds the creature's blood.</summary>
        public static BloodTank GetBloodTank(this AbstractCreature creature) => creature.GetSystem<BloodTank>();

        /// <summary>Blood currently in the creature (0 when it has no blood tank).</summary>
        public static float GetBlood(this AbstractCreature creature) => creature.GetBloodTank()?.CurrentBloodAmount ?? 0f;

        /// <summary>Blood capacity of the creature (0 when it has no blood tank).</summary>
        public static float GetBloodCapacity(this AbstractCreature creature) => creature.GetBloodTank()?.m_capacity ?? 0f;

        /// <summary>Sets the amount of blood in the creature (clamped by the game to its capacity).</summary>
        public static bool SetBlood(this AbstractCreature creature, float amount)
        {
            var tank = creature.GetBloodTank();
            if (tank == null)
                return false;
            tank.SetBloodAmount(Mathf.Max(0f, amount));
            return true;
        }

        /// <summary>Refills the creature's blood to full.</summary>
        public static bool RefillBlood(this AbstractCreature creature)
        {
            var tank = creature.GetBloodTank();
            if (tank == null)
                return false;
            tank.SetBloodAmount(tank.m_capacity);
            return true;
        }

        /// <summary>Removes some blood from the creature.</summary>
        public static bool DrainBlood(this AbstractCreature creature, float amount)
        {
            var tank = creature.GetBloodTank();
            if (tank == null)
                return false;
            tank.Drain(Mathf.Max(0f, amount));
            return true;
        }

        /// <summary>Closes every bleeding wound on every limb.</summary>
        public static int StopBleeding(this AbstractCreature creature)
        {
            int closed = 0;
            foreach (var limb in creature.GetLimbs())
            {
                if (limb.StopBleeding())
                    closed++;
            }
            return closed;
        }

        /// <summary>
        /// Patches the creature up: refills its blood and closes all bleeding wounds. Destroyed tissue and
        /// severed limbs are not regrown.
        /// </summary>
        public static void Heal(this AbstractCreature creature)
        {
            creature.StopBleeding();
            creature.RefillBlood();
        }

        /// <summary>
        /// Kills the creature by draining all of its blood; it collapses and goes lifeless over the next
        /// moments, exactly like bleeding out.
        /// </summary>
        public static bool Kill(this AbstractCreature creature)
        {
            if (!creature.IsValid())
                return false;
            return creature.SetBlood(0f);
        }

        /// <summary>Deletes the creature using the game's own "delete" action.</summary>
        public static bool Delete(this AbstractCreature creature)
        {
            if (!creature.IsValid())
                return false;
            var root = creature.GetRootLimb();
            if (root != null)
            {
                try
                {
                    var action = new DeleteCreatureContextMenuAction(root);
                    Internal.ContextMenuCarrier.RunGameLogic(action);
                    return true;
                }
                catch (Exception e)
                {
                    FruktLog.Debug("Delete action failed, destroying directly: " + e.Message);
                }
            }
            try
            {
                creature.MarkBeingDestroyed();
                creature.DestroyAllLimbs();
                UnityEngine.Object.Destroy(creature.gameObject);
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning("Deleting a creature failed: " + e.Message);
                return false;
            }
        }

        // ------------------------------------------------------------ physics

        /// <summary>Applies a force to every limb (the force is split by limb mass when using <see cref="ForceMode.Force"/>/Impulse).</summary>
        public static void AddForce(this AbstractCreature creature, Vector3 force, ForceMode mode = ForceMode.Impulse)
        {
            var limbs = creature.GetLimbs();
            if (limbs.Count == 0)
                return;
            bool split = mode == ForceMode.Force || mode == ForceMode.Impulse;
            float totalMass = 0f;
            if (split)
            {
                foreach (var limb in limbs)
                {
                    var body = limb.GetRigidbody();
                    if (body.Exists())
                        totalMass += body.mass;
                }
            }
            foreach (var limb in limbs)
            {
                var body = limb.GetRigidbody();
                if (!body.Exists())
                    continue;
                var share = split && totalMass > 0f ? force * (body.mass / totalMass) : force;
                body.AddForce(share, mode);
            }
        }

        /// <summary>Pushes every limb away from a point, like an explosion.</summary>
        public static void AddExplosionForce(this AbstractCreature creature, float force, Vector3 center, float radius, float upwardsModifier = 0.5f)
        {
            foreach (var limb in creature.GetLimbs())
            {
                var body = limb.GetRigidbody();
                if (body.Exists())
                    body.AddExplosionForce(force, center, radius, upwardsModifier, ForceMode.Impulse);
            }
        }

        /// <summary>Freezes (kinematic) or unfreezes every limb.</summary>
        public static void SetFrozen(this AbstractCreature creature, bool frozen)
        {
            foreach (var limb in creature.GetLimbs())
            {
                var body = limb.GetRigidbody();
                if (body.Exists())
                    body.isKinematic = frozen;
            }
        }

        /// <summary>Moves the whole creature so its root limb ends up at <paramref name="position"/>.</summary>
        public static void TeleportTo(this AbstractCreature creature, Vector3 position)
        {
            var offset = position - creature.GetPosition();
            foreach (var limb in creature.GetLimbs())
            {
                var body = limb.GetRigidbody();
                if (!body.Exists())
                    continue;
                body.position += offset;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private static AbstractCreatureLimbsNavigator Navigator(AbstractCreature creature)
        {
            if (!creature.Exists())
                return null;
            try
            {
                return creature.LimbsHierarchyHandler?.Navigator;
            }
            catch
            {
                return null;
            }
        }
    }
}
