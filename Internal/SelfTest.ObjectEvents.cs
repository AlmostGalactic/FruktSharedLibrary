using System.Collections;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Objects;
using FruktSharedLibrary.Spawning;
using FruktSharedLibrary.Utilities;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // ObjectEvents: collisions and hits (through the injected relay component), grabbing, and being shot.
    internal static partial class SelfTest
    {
        private static IEnumerator TestObjectEvents()
        {
            var mesh = Meshes.ParseObj(CubeObj, "EventCube", 0.5f);
            var dropPoint = LocalPlayer.GetPointInFront(4f) + Vector3.up * 4f;
            var cube = Spawner.SpawnMesh(mesh, dropPoint);
            var events = ObjectEvents.For(cube);
            Impact? firstHit = null;
            int collisions = 0;
            events.Collided += _ => collisions++;
            events.Hit += impact => firstHit ??= impact;
            WatchSounds("cube landing", 3f);
            yield return Wait(2.5f);

            Check("Collided fires when the object lands", collisions > 0, collisions + " collisions");
            Check("Hit fires for a hard landing, with speed and contact point",
                firstHit.HasValue && firstHit.Value.Speed > 4f && firstHit.Value.Other != null && firstHit.Value.Point != Vector3.zero,
                firstHit.HasValue ? $"{firstHit.Value.Speed:0.0} m/s on '{firstHit.Value.Other?.name}'" : "no hit");
            Check("Asking again gives the same events object", ObjectEvents.For(cube) == events && ObjectEvents.For(cube.transform) == events);

            // Grab it for real with the cursor tool.
            bool grabbed = false, released = false;
            events.Grabbed += () => grabbed = true;
            events.Released += () => released = true;
            var body = cube.GetComponent<Rigidbody>();
            // Move it through the rigidbody: setting the transform of an interpolated body doesn't always stick.
            body.position = LocalPlayer.CameraPosition + LocalPlayer.Forward * 3f;
            cube.transform.position = body.position;
            Physics.SyncTransforms();
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            yield return Wait(0.5f);
            bool aimed = LocalPlayer.Raycast(out var aim, 50f);
            FruktLog.Msg($"[SelfTest] before grab: cube at {cube.transform.position}, camera at {LocalPlayer.CameraPosition} looking {LocalPlayer.Forward}, " +
                         $"crosshair on '{(aimed ? aim.collider.name : "nothing")}', held '{LocalPlayer.HeldObject?.name}'");
            FruktLog.Msg("[SelfTest] MOUSEDOWN events-grab");
            for (float end = Now() + 2f; !grabbed && Now() < end;)
                yield return null;
            Check("Grabbed fires when the player picks it up", grabbed && events.IsHeld);
            FruktLog.Msg("[SelfTest] MOUSEUP events-grab");
            for (float end = Now() + 2f; !released && Now() < end;)
                yield return null;
            Check("Released fires when the player lets go", released && !events.IsHeld);

            // Shoot it with a pistol floating next to it.
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            yield return Wait(0.5f);
            ShotHit? shot = null;
            events.Shot += hit => shot = hit;
            Il2CppSpawnables.Weapons.Firearm gun = null;
            Section("Shot: gun", () =>
            {
                var gunAt = body.worldCenterOfMass + Vector3.Cross(Vector3.up, LocalPlayer.Forward).normalized * 2.5f;
                gun = Spawner.SpawnFirearm(FirearmType.Viper17, gunAt);
                var gunBody = gun.GetRigidbody();
                if (gunBody != null)
                    gunBody.isKinematic = true; // hold it still in the air
            });
            // Give the game a moment to finish setting the gun up before firing it.
            yield return Wait(0.5f);
            Section("Shot: fire", () =>
            {
                gun.AimAt(body.worldCenterOfMass);
                gun.Fire();
            });
            for (float end = Now() + 1.5f; !shot.HasValue && Now() < end;)
                yield return null;
            Check("Shot fires when a bullet hits it, and knows the gun", shot.HasValue && shot.Value.Firearm != null,
                shot.HasValue ? $"hit at {shot.Value.Point}, gun known: {shot.Value.Firearm != null}" : "not hit");
            Shot("events");
            yield return Wait(1.5f);

            events.Clear();
            yield return Wait(0.1f);
            Check("Clear removes the collision relay", cube.GetComponent<CollisionRelay>() == null);
            Object.Destroy(cube);
        }
    }
}
