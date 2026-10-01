using FruktSharedLibrary.Interop;
using Il2CppSpawnables.Weapons;
using UnityEngine;

namespace FruktSharedLibrary.Spawning
{
    /// <summary>Controlling firearms that exist in the world.</summary>
    public static class FirearmExtensions
    {
        /// <summary>Fires one shot (with sound, recoil, casing and muzzle flash).</summary>
        public static bool Fire(this Firearm firearm)
        {
            if (!firearm.Exists())
                return false;
            firearm.Shoot();
            return true;
        }

        /// <summary>Turns the game's "loop firing" (auto fire) on or off.</summary>
        public static void SetAutoFire(this Firearm firearm, bool enabled)
        {
            if (firearm.Exists())
                firearm.SetLoopFiring(enabled);
        }

        /// <summary>True while the firearm is auto firing.</summary>
        public static bool IsAutoFiring(this Firearm firearm) => firearm.Exists() && firearm.IsLoopFiring;

        /// <summary>Points the firearm's muzzle at a world position.</summary>
        public static void AimAt(this Firearm firearm, Vector3 target)
        {
            if (!firearm.Exists())
                return;
            var root = firearm.Root.Exists() ? firearm.Root : firearm.transform;
            var direction = target - root.position;
            if (direction.sqrMagnitude > 1e-6f)
                root.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// <summary>The firearm's rigidbody.</summary>
        public static Rigidbody GetRigidbody(this Firearm firearm)
        {
            if (!firearm.Exists())
                return null;
            var root = firearm.Root.Exists() ? firearm.Root : firearm.transform;
            return root.GetComponentInChildrenIl2Cpp<Rigidbody>() ?? root.GetComponentInParentIl2Cpp<Rigidbody>();
        }
    }
}
