using System;
using System.Collections.Generic;
using FruktSharedLibrary.Combat;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using Il2CppLVA.Creatures;
using Il2CppLVA.LimbContextMenu.Actions;
using Il2CppLVA.Limbs;
using Il2CppLVA.Limbs.Parameters;
using Il2CppLVA.Limbs.Systems.Blood;
using Il2CppLVA.NodesHierarchy.Benchmark.Variants;
using Il2CppLVA.NodesHierarchy.Nodes;
using Il2CppLVA.NodesHierarchy.Nodes.Tags;
using Il2CppLVA.Organs;
using Il2CppVoxelMeshGeneration;
using UnityEngine;

namespace FruktSharedLibrary.Entities
{
    /// <summary>Everything you can read or do with a single limb (body part).</summary>
    public static class LimbExtensions
    {
        /// <summary>The creature the limb currently belongs to.</summary>
        public static AbstractCreature GetCreature(this AbstractLimb limb)
            => limb.Exists() ? limb.References?.AssignedCreature : null;

        /// <summary>The limb's rigidbody.</summary>
        public static Rigidbody GetRigidbody(this AbstractLimb limb)
        {
            if (!limb.Exists())
                return null;
            var physics = limb.References?.Physics;
            return physics.Exists() ? physics.m_rb : null;
        }

        /// <summary>The voxel mesh the limb is made of (what bullets and cuts carve into).</summary>
        public static VoxelMesh GetVoxelMesh(this AbstractLimb limb) => limb.Exists() ? limb.References?.Mesh : null;

        /// <summary>The organs inside the limb (bones, muscle, skin, brain, heart...).</summary>
        public static List<AbstractOrgan> GetAllOrgans(this AbstractLimb limb)
        {
            var organs = limb.Exists() ? limb.References?.OrgansHandler?.Organs : null;
            return organs == null ? new List<AbstractOrgan>() : organs.ToManagedList();
        }

        /// <summary>The first organ of a type inside the limb, e.g. <c>head.GetOrgan&lt;Brain&gt;()</c>.</summary>
        public static T GetOrgan<T>(this AbstractLimb limb) where T : AbstractOrgan
        {
            foreach (var organ in limb.GetAllOrgans())
            {
                var cast = organ?.TryCast<T>();
                if (cast != null)
                    return cast;
            }
            return null;
        }

        /// <summary>The limb's hierarchy node (parent/children links).</summary>
        public static LimbNode GetNode(this AbstractLimb limb)
        {
            if (!limb.Exists())
                return null;
            try
            {
                // References.Node is the live node in the creature's hierarchy. (AbstractLimb.GetLimbNode() is a
                // factory used while the limb is being built and returns a new, unattached node.)
                return limb.References?.Node;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Which human body part this is, or null for non-human limbs.</summary>
        public static HumanoidNodeTagValue? GetHumanPart(this AbstractLimb limb)
        {
            var tag = limb.GetNode()?.nodeTag?.TryCast<HumanoidNodeTag>();
            return tag == null ? null : tag.Tag;
        }

        /// <summary>The limb this one is attached to, or null for a root limb.</summary>
        public static AbstractLimb GetParentLimb(this AbstractLimb limb)
        {
            var node = limb.GetNode();
            return node?.Parent?.assignedLimb;
        }

        /// <summary>Limbs directly attached to this one.</summary>
        public static List<AbstractLimb> GetChildLimbs(this AbstractLimb limb)
        {
            var result = new List<AbstractLimb>();
            var children = limb.GetNode()?.m_children;
            if (children == null)
                return result;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i]?.assignedLimb;
                if (child != null)
                    result.Add(child);
            }
            return result;
        }

        /// <summary>How intact the limb is (drops as tissue is destroyed).</summary>
        public static float GetWholeness(this AbstractLimb limb) => limb.GetParameterValue<LimbWholeness>() ?? 0f;

        /// <summary>The limb's blood system (open wounds, drains).</summary>
        public static BloodSystem GetBloodSystem(this AbstractLimb limb) => limb.GetSystem<BloodSystem>();

        /// <summary>Number of open bleeding wounds on the limb.</summary>
        public static int GetBleedingWoundCount(this AbstractLimb limb) => limb.GetBloodSystem()?.DrainsCount ?? 0;

        /// <summary>Closes every bleeding wound on the limb. Returns false if the limb has no blood system.</summary>
        public static bool StopBleeding(this AbstractLimb limb)
        {
            var drains = limb.GetBloodSystem()?.m_drainsHandler;
            if (drains == null)
                return false;
            drains.CloseEveryDrain();
            return true;
        }

        /// <summary>Makes the limb's wounds bleed harder.</summary>
        public static void AddBleeding(this AbstractLimb limb, float extraForce)
            => limb.GetBloodSystem()?.AddExtraDrainForce(extraForce);

        /// <summary>
        /// Detaches the limb (and everything attached below it) from its creature, using the game's own
        /// "detach" action. The detached part becomes its own creature.
        /// </summary>
        public static bool Detach(this AbstractLimb limb)
        {
            var node = limb.GetNode();
            if (node?.Parent == null)
                return false;
            try
            {
                var action = new DetachLimbContextAction(limb, node.externalEvents);
                Internal.ContextMenuCarrier.RunGameLogic(action);
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning("Detaching a limb failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Deletes the limb using the game's own "delete limb" action.</summary>
        public static bool Delete(this AbstractLimb limb)
        {
            if (!limb.Exists())
                return false;
            try
            {
                Internal.ContextMenuCarrier.RunGameLogic(new DeleteLimbContextMenuAction(limb));
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning("Deleting a limb failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Applies a force to the limb's rigidbody.</summary>
        public static void AddForce(this AbstractLimb limb, Vector3 force, ForceMode mode = ForceMode.Impulse)
        {
            var body = limb.GetRigidbody();
            if (body.Exists())
                body.AddForce(force, mode);
        }

        /// <summary>Applies a force at a world position on the limb (adds spin).</summary>
        public static void AddForceAtPosition(this AbstractLimb limb, Vector3 force, Vector3 position, ForceMode mode = ForceMode.Impulse)
        {
            var body = limb.GetRigidbody();
            if (body.Exists())
                body.AddForceAtPosition(force, position, mode);
        }

        /// <summary>
        /// Destroys tissue in a sphere around a world point on this limb. See <see cref="Damage.Apply(AbstractLimb, Vector3, int, float, Vector3?)"/>.
        /// </summary>
        public static bool Damage(this AbstractLimb limb, Vector3 worldPoint, int radiusVoxels = 3, float strength = Combat.Damage.DefaultStrength, Vector3? direction = null)
            => Combat.Damage.Apply(limb, worldPoint, radiusVoxels, strength, direction);
    }
}
