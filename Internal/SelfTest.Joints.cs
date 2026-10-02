using System.Collections;
using System.Collections.Generic;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Objects;
using FruktSharedLibrary.Spawning;
using FruktSharedLibrary.Utilities;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // Joints: one of each kind, hung from points in the air in front of the player.
    internal static partial class SelfTest
    {
        private static IEnumerator TestJoints()
        {
            var mesh = Meshes.ParseObj(CubeObj, "JointCube", 0.4f);
            var spawned = new List<GameObject>();
            var right = Vector3.Cross(Vector3.up, LocalPlayer.Forward).normalized;
            var front = LocalPlayer.Position + Vector3.ProjectOnPlane(LocalPlayer.Forward, Vector3.up).normalized * 7f + Vector3.up * 3f;
            Vector3 Slot(int i) => front + right * ((i - 3.5f) * 1.6f);
            Rigidbody Cube(Vector3 at)
            {
                var go = Spawner.SpawnMesh(mesh, at);
                spawned.Add(go);
                return go.GetComponent<Rigidbody>();
            }

            Rigidbody pendulum = null, roped = null, sprung = null, weldA = null, weldB = null, door = null, wheel = null, slid = null;
            JointHandle doorJoint = null, wheelJoint = null, ropeJoint = null;
            Vector3 pendulumPivot = default, ropeTop = default, springTop = default, slideStart = default;
            float weldGap = 0f;
            Section("Joints: building", () =>
            {
                pendulum = Cube(Slot(0));
                pendulumPivot = Slot(0) + Vector3.up * 1.5f;
                Joints.BallSocket(pendulum, null, pendulumPivot);
                pendulum.AddForce(right * 30f, ForceMode.Impulse);

                roped = Cube(Slot(1));
                ropeTop = Slot(1) + Vector3.up * 2.5f;
                ropeJoint = Joints.Rope(roped, null, pointB: ropeTop, length: 2f);

                sprung = Cube(Slot(2));
                springTop = Slot(2) + Vector3.up;
                Joints.Spring(sprung, null, stiffness: 300f, damping: 20f, length: 1f, pointB: springTop);

                weldA = Cube(Slot(3));
                weldB = Cube(Slot(3) + right * 0.4f);
                weldGap = Vector3.Distance(weldA.position, weldB.position);
                Joints.Weld(weldA, weldB);

                door = Cube(Slot(4));
                doorJoint = Joints.Hinge(door, null, Slot(4) - right * 0.2f, Vector3.up, -30f, 30f);
                door.AddForceAtPosition(LocalPlayer.Forward * 80f, Slot(4) + right * 0.2f, ForceMode.Impulse);

                wheel = Cube(Slot(5));
                wheelJoint = Joints.Hinge(wheel, null, Slot(5), right).SetMotor(180f, 1000f);

                slid = Cube(Slot(6));
                slideStart = slid.position;
                Joints.Slider(slid, null, right, 0.5f);
                slid.AddForce((right + Vector3.up) * 200f, ForceMode.Impulse);
            });
            yield return Wait(2.5f);

            Section("Joints: results", () =>
            {
                float swing = Vector3.Distance(pendulum.worldCenterOfMass, pendulumPivot);
                Check("Ball socket keeps its distance to the pivot", Mathf.Abs(swing - 1.5f) < 0.06f, swing.ToString("0.000"));

                float rope = Vector3.Distance(roped.worldCenterOfMass, ropeTop);
                Check("Rope holds the object up at its length", rope < 2.06f && rope > 1.85f, rope.ToString("0.000"));
                Check("Rope draws a line", ropeJoint.IsActive && GameObject.Find("FruktSharedLibrary.Rope") != null);

                float spring = Vector3.Distance(sprung.worldCenterOfMass, springTop);
                Check("Spring stretches under the weight but holds", spring > 1.05f && spring < 1.8f, spring.ToString("0.000"));

                float gap = Vector3.Distance(weldA.position, weldB.position);
                Check("Welded objects stay together", Mathf.Abs(gap - weldGap) < 0.02f && weldA.position.y < Slot(3).y - 0.5f,
                    $"gap {weldGap:0.000} -> {gap:0.000}, fell {Slot(3).y - weldA.position.y:0.00} m");

                Check("Hinge limits stop it turning further", Mathf.Abs(doorJoint.Angle) <= 31f && Mathf.Abs(doorJoint.Angle) > 5f,
                    doorJoint.Angle.ToString("0.0") + " degrees");

                Check("Hinge motor turns it the way the speed says", wheelJoint.Angle > 30f,
                    $"{wheelJoint.Angle:0.0} degrees, {wheel.angularVelocity.magnitude:0.0} rad/s");

                var moved = slid.position - slideStart;
                float along = Vector3.Dot(moved, right);
                float across = (moved - right * along).magnitude;
                Check("Slider only moves along its axis, within its distance", Mathf.Abs(along) <= 0.52f && Mathf.Abs(along) > 0.2f && across < 0.05f,
                    $"along {along:0.000}, across {across:0.000}");
            });

            // A hinge spring pulls the door back to the middle.
            doorJoint.SetHingeSpring(60f, 8f, 0f);
            wheelJoint.SetMotor(0f);
            yield return Wait(2f);
            Check("Hinge spring pulls it back", Mathf.Abs(doorJoint.Angle) < 6f, doorJoint.Angle.ToString("0.0") + " degrees");
            doorJoint.SetHingeSpring(60f, 8f, 20f);
            yield return Wait(2f);
            Check("Hinge spring holds a target angle", Mathf.Abs(doorJoint.Angle - 20f) < 5f, doorJoint.Angle.ToString("0.0") + " degrees");
            Shot("joints");
            yield return Wait(1.5f);

            // Breaking and removing.
            bool broke = false, removedBroke = false;
            JointHandle weak = null, removed = null;
            Section("Joints: breaking", () =>
            {
                var breakable = Cube(Slot(7));
                weak = Joints.Weld(breakable, null, breakForce: 500f);
                weak.Broke += _ => broke = true;
                breakable.AddForce(Vector3.up * 400f, ForceMode.Impulse);

                var quiet = Cube(Slot(7) + Vector3.up * 1.5f);
                removed = Joints.Weld(quiet, null);
                removed.Broke += _ => removedBroke = true;
                removed.Remove();
            });
            yield return Wait(1f);
            Check("A breakable joint breaks under force and says so", broke && weak.IsBroken);
            Check("Removing a joint doesn't count as breaking", !removedBroke && !removed.IsActive && !removed.IsBroken);

            foreach (var go in spawned)
            {
                if (go.Exists())
                    Object.Destroy(go);
            }
            yield return Wait(0.5f);
            Check("Joints on deleted objects are dropped quietly", Joints.All.Count == 0, Joints.All.Count + " left");
        }
    }
}
