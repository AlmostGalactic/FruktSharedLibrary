using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Interop;
using Il2CppEffectors;
using Il2CppEffectors.ReceiveMethods;
using Il2CppEffectors.ReceiveMethods.Index;
using Il2CppEffectors.Types;
using Il2CppLVA.Limbs;
using Il2CppVoxelMeshGeneration;
using Il2CppVoxelMeshGeneration.Tools;
using Unity.Mathematics;
using UnityEngine;

namespace FruktSharedLibrary.Combat
{
    /// <summary>
    /// Destroys creature tissue the same way the game's weapons do: by sending a "Destruction" effector
    /// signal to the voxels of a limb. Organs, pain, bleeding and dismemberment all react naturally.
    /// </summary>
    public static class Damage
    {
        /// <summary>Strength that fully destroys the voxels in the sphere's core.</summary>
        public const float DefaultStrength = 1f;

        /// <summary>
        /// The biggest wound sphere, in voxels. A sphere this big already reaches across a whole limb, and the work
        /// grows with the cube of the radius, so larger radii are cut down to it.
        /// </summary>
        public const int MaxRadiusVoxels = 16;

        private static Il2CppSystem.Object _hitSource;
        private static int _hitNumber;

        /// <summary>
        /// Destroys tissue in a sphere on <paramref name="limb"/> centred on the voxel nearest to
        /// <paramref name="worldPoint"/>.
        /// </summary>
        /// <param name="radiusVoxels">Sphere radius in voxels (a human limb is roughly 6-12 voxels across), at most <see cref="MaxRadiusVoxels"/>.</param>
        /// <param name="strength">How hard the tissue is hit; 1 destroys the core, lower values only weaken it.</param>
        /// <param name="direction">Direction the damage travels (used by wounds/blood); defaults to towards the limb.</param>
        public static bool Apply(AbstractLimb limb, Vector3 worldPoint, int radiusVoxels = 3, float strength = DefaultStrength, Vector3? direction = null)
            => ApplySignal(limb, worldPoint, radiusVoxels, -Mathf.Abs(strength), direction);

        /// <summary>Sends a raw signed destruction signal (used by the self-test to verify the sign convention).</summary>
        internal static bool ApplySignal(AbstractLimb limb, Vector3 worldPoint, int radiusVoxels, float signal, Vector3? direction = null)
        {
            if (!limb.Exists())
                return false;
            var receiver = limb.GetComponentInChildrenIl2Cpp<LimbEffectorReceiver>()
                           ?? limb.GetComponentInParentIl2Cpp<LimbEffectorReceiver>();
            if (receiver == null)
            {
                FruktLog.Debug($"{limb.name} has no effector receiver.");
                return false;
            }
            return Send(receiver.Cast<IIndexEffectorSignalReceiver>(), receiver.VoxelMesh, worldPoint, radiusVoxels, signal,
                direction ?? (limb.GetPosition() - worldPoint));
        }

        /// <summary>Destroys tissue at a point on whatever creature the collider belongs to (e.g. a raycast hit).</summary>
        public static bool Apply(Collider collider, Vector3 worldPoint, int radiusVoxels = 3, float strength = DefaultStrength, Vector3? direction = null)
        {
            if (!collider.Exists())
                return false;
            IIndexEffectorSignalReceiver receiver = null;
            try
            {
                if (!EffectorsTools.TryGetIndexEffectorSignalReceiver(collider, out receiver) || receiver == null)
                {
                    var limb = Creatures.LimbFromCollider(collider);
                    return limb != null && Apply(limb, worldPoint, radiusVoxels, strength, direction);
                }
            }
            catch (Exception e)
            {
                FruktLog.Debug("Receiver lookup failed: " + e.Message);
                return false;
            }
            return Send(receiver, receiver.VoxelMesh, worldPoint, radiusVoxels, -Mathf.Abs(strength),
                direction ?? (collider.bounds.center - worldPoint));
        }

        /// <summary>
        /// Several wounds on the creature part <paramref name="collider"/> belongs to, sent as one. Much cheaper than
        /// calling <c>Apply</c> once for each, because the part is only worked out once: use it for blasts.
        /// </summary>
        /// <param name="wounds">Each wound's point, radius in voxels (at most <see cref="MaxRadiusVoxels"/>) and strength.</param>
        public static bool Apply(Collider collider, IReadOnlyList<(Vector3 Point, int RadiusVoxels, float Strength)> wounds, Vector3? direction = null)
        {
            if (!collider.Exists() || wounds == null || wounds.Count == 0)
                return false;
            IIndexEffectorSignalReceiver receiver = null;
            try
            {
                if (!EffectorsTools.TryGetIndexEffectorSignalReceiver(collider, out receiver) || receiver == null)
                {
                    var limb = Creatures.LimbFromCollider(collider);
                    var found = limb.Exists() ? limb.GetComponentInChildrenIl2Cpp<LimbEffectorReceiver>() ?? limb.GetComponentInParentIl2Cpp<LimbEffectorReceiver>() : null;
                    if (found == null)
                        return false;
                    receiver = found.Cast<IIndexEffectorSignalReceiver>();
                }
            }
            catch (Exception e)
            {
                FruktLog.Debug("Receiver lookup failed: " + e.Message);
                return false;
            }
            var mesh = receiver.VoxelMesh;
            if (!mesh.Exists())
                return false;
            AbstractIndexEffectorSignalsHandler handler = null;
            try
            {
                var spheres = new Il2CppStructArray<SphereSignalDescription>(wounds.Count);
                for (int i = 0; i < wounds.Count; i++)
                {
                    var index = VoxelTools.PositionToVoxelIndex(mesh, wounds[i].Point);
                    spheres[i] = new SphereSignalDescription(new int3(index.x, index.y, index.z),
                        Mathf.Clamp(wounds[i].RadiusVoxels, 1, MaxRadiusVoxels), -Mathf.Abs(wounds[i].Strength));
                }
                var dir = direction ?? (collider.bounds.center - wounds[0].Point);
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.down;
                var signals = SphereEffectorSignalsSamples.RadialFalloffNoisedSphereEffector<Destruction>(
                    spheres, 0.35f, 0, new IndexEffectorDescription(dir, NextHit()), SphereOverlapRule.StrongestWins);
                handler = signals;
                receiver.Receive(signals);
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning("Applying damage failed: " + e.Message);
                return false;
            }
            finally
            {
                try
                {
                    handler?.Dispose();
                }
                catch
                {
                    // Already disposed by the receiver.
                }
            }
        }

        /// <summary>Destroys tissue where a raycast hit a creature.</summary>
        public static bool Apply(RaycastHit hit, int radiusVoxels = 3, float strength = DefaultStrength, Vector3? direction = null)
            => Apply(hit.collider, hit.point, radiusVoxels, strength, direction ?? -hit.normal);

        /// <summary>
        /// An explosion: damages every limb within <paramref name="radius"/> (more at the centre) and pushes
        /// rigidbodies away. Returns how many limbs were damaged.
        /// </summary>
        /// <param name="force">Impulse applied to rigidbodies at the centre.</param>
        /// <param name="maxRadiusVoxels">Wound size on limbs right at the centre (shrinks with distance).</param>
        public static int Explosion(Vector3 center, float radius, float force = 30f, int maxRadiusVoxels = 5, float strength = DefaultStrength)
        {
            int damaged = 0;
            var limbsHit = new HashSet<IntPtr>();
            var bodiesPushed = new HashSet<IntPtr>();
            var colliders = Physics.OverlapSphere(center, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore);

            foreach (var collider in colliders)
            {
                if (!collider.Exists())
                    continue;

                var body = collider.attachedRigidbody;
                if (body.Exists() && !body.isKinematic && bodiesPushed.Add(body.Pointer))
                    body.AddExplosionForce(force, center, radius, 0.4f, ForceMode.Impulse);

                var limb = Creatures.LimbFromCollider(collider);
                if (limb == null || !limbsHit.Add(limb.Pointer))
                    continue;

                var closest = collider.ClosestPoint(center);
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(center, closest) / radius);
                int woundRadius = Mathf.Max(1, Mathf.RoundToInt(maxRadiusVoxels * falloff));
                if (Apply(collider, closest, woundRadius, strength * Mathf.Lerp(0.4f, 1f, falloff), closest - center))
                    damaged++;
            }
            return damaged;
        }

        private static bool Send(IIndexEffectorSignalReceiver receiver, VoxelMesh mesh, Vector3 worldPoint, int radiusVoxels, float signal, Vector3 direction)
        {
            if (receiver == null || !mesh.Exists())
                return false;
            AbstractIndexEffectorSignalsHandler handler = null;
            try
            {
                var index = VoxelTools.PositionToVoxelIndex(mesh, worldPoint);
                var center = new int3(index.x, index.y, index.z);
                var dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.down;
                var description = new IndexEffectorDescription(dir, NextHit());

                var signals = SphereEffectorSignalsSamples.RadialFalloffNoisedSphereEffector<Destruction>(
                    center, Mathf.Clamp(radiusVoxels, 1, MaxRadiusVoxels), signal, 0.35f, 0, description);
                handler = signals;
                receiver.Receive(signals);
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning("Applying damage failed: " + e.Message);
                return false;
            }
            finally
            {
                try
                {
                    handler?.Dispose();
                }
                catch
                {
                    // Already disposed by the receiver.
                }
            }
        }

        private static EffectorHit NextHit()
        {
            _hitSource ??= new Il2CppSystem.Object();
            return new EffectorHit(_hitSource, ++_hitNumber);
        }
    }
}
