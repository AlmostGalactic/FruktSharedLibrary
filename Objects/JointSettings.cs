using System;
using UnityEngine;

namespace FruktSharedLibrary.Objects
{
    /// <summary>
    /// Everything that makes up one of <see cref="Joints"/>' joints: they're all ConfigurableJoints, so this is
    /// that joint's setup, plus the rope line if there is one. Saved in builds, and put on a new joint when a
    /// build is spawned.
    /// </summary>
    internal sealed class JointSettings
    {
        public float[] Anchor, ConnectedAnchor, Axis, SecondaryAxis;
        public int[] Motion; // x, y, z, angular x, angular y, angular z
        public float LinearLimit, LowAngularX, HighAngularX, AngularY, AngularZ;
        public float[] LinearDrive, AngularDrive; // spring, damper, maximum force
        public float[] TargetPosition, TargetRotation, TargetAngularVelocity;
        public float BreakForce, BreakTorque;
        public bool EnableCollision;
        public float LineThickness;
        public float[] LineColour;

        internal Color LineColor => LineColour is { Length: 4 } c ? new Color(c[0], c[1], c[2], c[3]) : Color.white;

        /// <summary>
        /// Reads a joint. A world-held joint's other end is a point in the world; <paramref name="toBuild"/> turns
        /// it into the build's own space so it moves with the build.
        /// </summary>
        internal static JointSettings From(JointHandle handle, Func<Vector3, Vector3> toBuild)
        {
            var joint = handle.Joint.Cast<ConfigurableJoint>();
            var settings = new JointSettings
            {
                Anchor = V(joint.anchor),
                ConnectedAnchor = V(handle.B == null ? toBuild(joint.connectedAnchor) : joint.connectedAnchor),
                Axis = V(joint.axis),
                SecondaryAxis = V(joint.secondaryAxis),
                Motion = new[]
                {
                    (int)joint.xMotion, (int)joint.yMotion, (int)joint.zMotion,
                    (int)joint.angularXMotion, (int)joint.angularYMotion, (int)joint.angularZMotion,
                },
                LinearLimit = joint.linearLimit.limit,
                LowAngularX = joint.lowAngularXLimit.limit,
                HighAngularX = joint.highAngularXLimit.limit,
                AngularY = joint.angularYLimit.limit,
                AngularZ = joint.angularZLimit.limit,
                LinearDrive = D(joint.xDrive),
                AngularDrive = D(joint.angularXDrive),
                TargetPosition = V(joint.targetPosition),
                TargetRotation = Q(joint.targetRotation),
                TargetAngularVelocity = V(joint.targetAngularVelocity),
                BreakForce = joint.breakForce,
                BreakTorque = joint.breakTorque,
                EnableCollision = joint.enableCollision,
            };
            if (handle.Line is { } line)
            {
                settings.LineThickness = line.Thickness;
                settings.LineColour = new[] { line.Color.r, line.Color.g, line.Color.b, line.Color.a };
            }
            return settings;
        }

        /// <summary>Sets <paramref name="joint"/> up like the saved one, in the order Joints does it.</summary>
        internal void ApplyTo(ConfigurableJoint joint, Rigidbody connected)
        {
            joint.axis = V(Axis);
            joint.secondaryAxis = V(SecondaryAxis);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = connected;
            joint.anchor = V(Anchor);
            joint.connectedAnchor = V(ConnectedAnchor);
            joint.xMotion = (ConfigurableJointMotion)Motion[0];
            joint.yMotion = (ConfigurableJointMotion)Motion[1];
            joint.zMotion = (ConfigurableJointMotion)Motion[2];
            joint.angularXMotion = (ConfigurableJointMotion)Motion[3];
            joint.angularYMotion = (ConfigurableJointMotion)Motion[4];
            joint.angularZMotion = (ConfigurableJointMotion)Motion[5];
            joint.linearLimit = new SoftJointLimit { limit = LinearLimit };
            joint.lowAngularXLimit = new SoftJointLimit { limit = LowAngularX };
            joint.highAngularXLimit = new SoftJointLimit { limit = HighAngularX };
            joint.angularYLimit = new SoftJointLimit { limit = AngularY };
            joint.angularZLimit = new SoftJointLimit { limit = AngularZ };
            var linear = D(LinearDrive);
            joint.xDrive = linear;
            joint.yDrive = linear;
            joint.zDrive = linear;
            joint.angularXDrive = D(AngularDrive);
            joint.targetPosition = V(TargetPosition);
            joint.targetRotation = Q(TargetRotation);
            joint.targetAngularVelocity = V(TargetAngularVelocity);
            joint.breakForce = BreakForce;
            joint.breakTorque = BreakTorque;
            joint.enableCollision = EnableCollision;
        }

        // A world-held joint's other end, moved into the world where the build was spawned.
        internal JointSettings WithWorldAnchor(Func<Vector3, Vector3> fromBuild)
        {
            var copy = (JointSettings)MemberwiseClone();
            copy.ConnectedAnchor = V(fromBuild(V(ConnectedAnchor)));
            return copy;
        }

        private static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
        private static Vector3 V(float[] a) => a is { Length: 3 } ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
        private static float[] Q(Quaternion q) => new[] { q.x, q.y, q.z, q.w };
        private static Quaternion Q(float[] a) => a is { Length: 4 } ? new Quaternion(a[0], a[1], a[2], a[3]) : Quaternion.identity;
        private static float[] D(JointDrive d) => new[] { d.positionSpring, d.positionDamper, d.maximumForce };
        private static JointDrive D(float[] a) => a is { Length: 3 } ? new JointDrive { positionSpring = a[0], positionDamper = a[1], maximumForce = a[2] } : default;
    }
}
