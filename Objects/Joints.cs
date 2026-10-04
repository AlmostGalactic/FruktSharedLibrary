using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Interop;
using Il2CppLVA.Limbs;
using UnityEngine;

namespace FruktSharedLibrary.Objects
{
    /// <summary>
    /// Connects physics objects together: welds, hinges, ball sockets, springs, ropes and sliders. Works on
    /// anything with a rigidbody, so your own meshes, the game's props and guns, and creature limbs. Pass null as
    /// the second object to connect to a fixed point in the world instead.
    /// </summary>
    /// <remarks>
    /// Positions and directions are in world space. Every joint can be made breakable with <c>breakForce</c>;
    /// the returned <see cref="JointHandle"/> tells you when it breaks.
    /// </remarks>
    /// <example>
    /// <code>
    /// Joints.Weld(hat, head);                                         // stick a hat on a head
    /// Joints.Hinge(door, null, hingePoint, Vector3.up, -90f, 90f);    // a door on a fixed hinge
    /// Joints.Rope(lamp, null, ceilingPoint);                          // hang a lamp from the ceiling
    /// </code>
    /// </example>
    public static class Joints
    {
        private static readonly List<JointHandle> Active = new();

        /// <summary>Joins two things rigidly, so they move as one.</summary>
        public static JointHandle Weld(Component a, Component b, float breakForce = float.PositiveInfinity)
        {
            var (bodyA, bodyB) = Bodies(a, b);
            // A ConfigurableJoint with every axis locked: the game's build only reliably has ConfigurableJoint.
            var joint = bodyA.gameObject.AddComponent<ConfigurableJoint>();
            Connect(joint, bodyA, bodyB, Midpoint(bodyA, bodyB), breakForce);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
            return Track(new JointHandle(joint, bodyA, bodyB, "weld"));
        }

        /// <summary>
        /// A hinge that turns around <paramref name="axis"/> through <paramref name="pivot"/>, like a door or a wheel.
        /// Give both limits to stop it turning past them (in degrees, -180 to 180); leave them out to let it spin freely.
        /// </summary>
        public static JointHandle Hinge(Component a, Component b, Vector3 pivot, Vector3 axis, float? minAngle = null, float? maxAngle = null,
            float breakForce = float.PositiveInfinity)
        {
            // The game's build strips HingeJoint's limits, motor and spring, so hinges are ConfigurableJoints
            // that only turn around their X axis.
            var (bodyA, bodyB) = Bodies(a, b);
            var joint = bodyA.gameObject.AddComponent<ConfigurableJoint>();
            joint.axis = bodyA.transform.InverseTransformDirection(axis.normalized);
            joint.secondaryAxis = bodyA.transform.InverseTransformDirection(Perpendicular(axis));
            Connect(joint, bodyA, bodyB, pivot, breakForce);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            if (minAngle.HasValue && maxAngle.HasValue)
            {
                joint.angularXMotion = ConfigurableJointMotion.Limited;
                joint.lowAngularXLimit = new SoftJointLimit { limit = Mathf.Clamp(Mathf.Min(minAngle.Value, maxAngle.Value), -177f, 177f) };
                joint.highAngularXLimit = new SoftJointLimit { limit = Mathf.Clamp(Mathf.Max(minAngle.Value, maxAngle.Value), -177f, 177f) };
            }
            var handle = new JointHandle(joint, bodyA, bodyB, "hinge");
            handle.StartHingeAngle(axis.normalized, Perpendicular(axis));
            return Track(handle);
        }

        /// <summary>A ball-and-socket joint: free to swing and twist in every direction around <paramref name="pivot"/>.</summary>
        public static JointHandle BallSocket(Component a, Component b, Vector3 pivot, float breakForce = float.PositiveInfinity)
        {
            var (bodyA, bodyB) = Bodies(a, b);
            var joint = bodyA.gameObject.AddComponent<ConfigurableJoint>();
            Connect(joint, bodyA, bodyB, pivot, breakForce);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            return Track(new JointHandle(joint, bodyA, bodyB, "ball socket"));
        }

        /// <summary>
        /// A spring that pulls the two things back to <paramref name="length"/> apart (their current distance if you
        /// leave it out). Higher <paramref name="stiffness"/> is a stronger spring; <paramref name="damping"/> stops it
        /// bouncing forever.
        /// </summary>
        public static JointHandle Spring(Component a, Component b, float stiffness = 200f, float damping = 5f, float? length = null,
            Vector3? pointA = null, Vector3? pointB = null, float breakForce = float.PositiveInfinity)
        {
            // The game's build doesn't include SpringJoint, so a spring is a ConfigurableJoint whose position drives
            // pull A's end back to its resting point: `length` away from B's end, along the line between them.
            var (bodyA, bodyB) = Bodies(a, b);
            var (from, to) = Ends(bodyA, bodyB, pointA, pointB);
            float rest = length ?? Vector3.Distance(from, to);
            var direction = (from - to).sqrMagnitude > 0.0001f ? (from - to).normalized : Vector3.down;
            var restPoint = to + direction * rest;
            var joint = bodyA.gameObject.AddComponent<ConfigurableJoint>();
            ConnectEnds(joint, bodyA, bodyB, from, restPoint, breakForce);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            var drive = new JointDrive { positionSpring = Mathf.Max(0f, stiffness), positionDamper = Mathf.Max(0f, damping), maximumForce = float.MaxValue };
            joint.xDrive = drive;
            joint.yDrive = drive;
            joint.zDrive = drive;
            joint.targetPosition = Vector3.zero;
            return Track(new JointHandle(joint, bodyA, bodyB, "spring"));
        }

        /// <summary>
        /// A rope: the two things can move freely but never get further apart than <paramref name="length"/> (their
        /// current distance if you leave it out). By default a rope line is drawn between the two ends.
        /// </summary>
        /// <param name="pointA">Where the rope is tied on the first object (its centre of mass by default).</param>
        /// <param name="pointB">Where it's tied on the second object, or the fixed world point when <paramref name="b"/> is null.</param>
        public static JointHandle Rope(Component a, Component b, Vector3? pointA = null, Vector3? pointB = null, float? length = null,
            bool visible = true, float thickness = 0.03f, Color? color = null, float breakForce = float.PositiveInfinity)
        {
            var (bodyA, bodyB) = Bodies(a, b);
            var (from, to) = Ends(bodyA, bodyB, pointA, pointB);
            var joint = bodyA.gameObject.AddComponent<ConfigurableJoint>();
            ConnectEnds(joint, bodyA, bodyB, from, to, breakForce);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Limited;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.linearLimit = new SoftJointLimit { limit = Mathf.Max(0.01f, length ?? Vector3.Distance(from, to)) };
            var handle = new JointHandle(joint, bodyA, bodyB, "rope");
            if (visible)
                handle.ShowLine(thickness, color ?? new Color(0.55f, 0.45f, 0.3f));
            return Track(handle);
        }

        /// <summary>
        /// A slider: the first object can only move along <paramref name="axis"/>, up to <paramref name="distance"/>
        /// each way from where it starts, and can't turn. Good for pistons, drawers and rails.
        /// </summary>
        public static JointHandle Slider(Component a, Component b, Vector3 axis, float distance, float breakForce = float.PositiveInfinity)
        {
            var (bodyA, bodyB) = Bodies(a, b);
            var joint = bodyA.gameObject.AddComponent<ConfigurableJoint>();
            joint.axis = bodyA.transform.InverseTransformDirection(axis.normalized);
            joint.secondaryAxis = bodyA.transform.InverseTransformDirection(Perpendicular(axis));
            Connect(joint, bodyA, bodyB, bodyA.worldCenterOfMass, breakForce);
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
            joint.linearLimit = new SoftJointLimit { limit = Mathf.Max(0.001f, distance) };
            return Track(new JointHandle(joint, bodyA, bodyB, "slider"));
        }

        /// <summary>Every joint made through this class that still exists.</summary>
        public static IReadOnlyList<JointHandle> All
        {
            get
            {
                Update();
                return Active.ToArray();
            }
        }

        /// <summary>Removes every joint between the two objects that was made through this class.</summary>
        public static int RemoveBetween(Component a, Component b)
        {
            var bodyA = Body(a);
            var bodyB = b == null ? null : Body(b);
            int removed = 0;
            foreach (var handle in Active.ToArray())
            {
                bool match = (handle.A == bodyA && handle.B == bodyB) || (handle.A == bodyB && handle.B == bodyA);
                if (match)
                {
                    handle.Remove();
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// The rigidbody behind something you can pass to these methods: a rigidbody itself, a creature limb, or any
        /// component or collider on (or under) an object with a rigidbody.
        /// </summary>
        public static Rigidbody Body(Component thing)
        {
            if (thing == null)
                return null;
            var body = thing.TryCast<Rigidbody>();
            if (body != null)
                return body;
            var limb = thing.TryCast<AbstractLimb>();
            if (limb != null)
                return limb.GetRigidbody();
            var collider = thing.TryCast<Collider>();
            if (collider != null && collider.attachedRigidbody != null)
                return collider.attachedRigidbody;
            return thing.GetComponentInParentIl2Cpp<Rigidbody>() ?? thing.GetComponentInChildrenIl2Cpp<Rigidbody>();
        }

        /// <summary>The rigidbody on a GameObject (see <see cref="Body(Component)"/>).</summary>
        public static Rigidbody Body(GameObject thing) => thing == null ? null : Body(thing.transform);

        // ------------------------------------------------------------ internals

        /// <summary>
        /// Makes a joint again from saved settings (see <see cref="Builds"/>): a ConfigurableJoint on
        /// <paramref name="a"/> set up exactly like the saved one, tracked like the joints made above.
        /// </summary>
        internal static JointHandle Restore(Rigidbody a, Rigidbody b, string kind, JointSettings settings)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));
            var joint = a.gameObject.AddComponent<ConfigurableJoint>();
            settings.ApplyTo(joint, b);
            a.WakeUp();
            if (b != null)
                b.WakeUp();
            var handle = new JointHandle(joint, a, b, kind);
            if (kind == "hinge")
                handle.StartHingeAngle(a.transform.TransformDirection(joint.axis), a.transform.TransformDirection(joint.secondaryAxis));
            if (settings.LineThickness > 0f)
                handle.ShowLine(settings.LineThickness, settings.LineColor);
            return Track(handle);
        }

        internal static void Update()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var handle = Active[i];
                if (!handle.Tick())
                    Active.RemoveAt(i);
            }
        }

        internal static void Clear()
        {
            foreach (var handle in Active)
                handle.DestroyLine();
            Active.Clear();
        }

        private static JointHandle Track(JointHandle handle)
        {
            Active.Add(handle);
            return handle;
        }

        private static (Rigidbody A, Rigidbody B) Bodies(Component a, Component b)
        {
            var bodyA = Body(a) ?? throw new ArgumentException($"'{(a == null ? "null" : a.name)}' has no rigidbody to attach a joint to.", nameof(a));
            Rigidbody bodyB = null;
            if (b != null)
            {
                bodyB = Body(b) ?? throw new ArgumentException($"'{b.name}' has no rigidbody to attach a joint to.", nameof(b));
                if (bodyB == bodyA)
                    throw new ArgumentException("Both ends of a joint are the same object.");
            }
            return (bodyA, bodyB);
        }

        private static void Connect(Joint joint, Rigidbody a, Rigidbody b, Vector3 pivot, float breakForce)
            => ConnectEnds(joint, a, b, pivot, pivot, breakForce);

        private static void ConnectEnds(Joint joint, Rigidbody a, Rigidbody b, Vector3 onA, Vector3 onB, float breakForce)
        {
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = b;
            joint.anchor = a.transform.InverseTransformPoint(onA);
            joint.connectedAnchor = b != null ? b.transform.InverseTransformPoint(onB) : onB;
            joint.breakForce = breakForce;
            joint.breakTorque = breakForce;
            joint.enableCollision = false;
            a.WakeUp();
            if (b != null)
                b.WakeUp();
        }

        private static (Vector3 From, Vector3 To) Ends(Rigidbody a, Rigidbody b, Vector3? pointA, Vector3? pointB)
        {
            var from = pointA ?? a.worldCenterOfMass;
            var to = pointB ?? (b != null ? b.worldCenterOfMass : from + Vector3.up);
            return (from, to);
        }

        private static Vector3 Midpoint(Rigidbody a, Rigidbody b)
            => b == null ? a.worldCenterOfMass : (a.worldCenterOfMass + b.worldCenterOfMass) * 0.5f;

        private static Vector3 Perpendicular(Vector3 axis)
        {
            var other = Mathf.Abs(Vector3.Dot(axis.normalized, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            return Vector3.Cross(axis, other).normalized;
        }
    }

    /// <summary>A joint made with <see cref="Joints"/>. Use it to change the joint later, break it, or hear about it breaking.</summary>
    public sealed class JointHandle
    {
        private LineRenderer _line;
        private bool _removed;
        private bool _broken;

        internal JointHandle(Joint joint, Rigidbody a, Rigidbody b, string kind)
        {
            Joint = joint;
            A = a;
            B = b;
            Kind = kind;
        }

        /// <summary>The Unity joint, for anything this class doesn't cover.</summary>
        public Joint Joint { get; }

        /// <summary>The object the joint is on.</summary>
        public Rigidbody A { get; }

        /// <summary>The other object, or null when the joint holds <see cref="A"/> to a point in the world.</summary>
        public Rigidbody B { get; }

        /// <summary>"weld", "hinge", "ball socket", "spring", "rope" or "slider".</summary>
        public string Kind { get; }

        /// <summary>True once the joint has broken (from too much force, or <see cref="Break"/>).</summary>
        public bool IsBroken => _broken || (!_removed && !Joint.Exists());

        /// <summary>True while the joint still exists.</summary>
        public bool IsActive => !_removed && Joint.Exists();

        /// <summary>Raised once when the joint breaks. Not raised for <see cref="Remove"/>.</summary>
        public event Action<JointHandle> Broke;

        /// <summary>Breaks the joint now, as if it had been pulled apart (raises <see cref="Broke"/>).</summary>
        public void Break()
        {
            if (!IsActive)
                return;
            UnityEngine.Object.Destroy(Joint);
            _broken = true;
            RaiseBroke();
            DestroyLine();
        }

        /// <summary>Removes the joint quietly.</summary>
        public void Remove()
        {
            if (_removed)
                return;
            _removed = true;
            if (Joint.Exists())
                UnityEngine.Object.Destroy(Joint);
            DestroyLine();
        }

        /// <summary>
        /// Drives a hinge round on its own, like a motor: <paramref name="speed"/> in degrees per second, with up to
        /// <paramref name="force"/> to push with. Speed 0 turns the motor off.
        /// </summary>
        public JointHandle SetMotor(float speed, float force = 100f)
        {
            var hinge = HingeJoint("motor");
            bool on = !Mathf.Approximately(speed, 0f);
            hinge.angularXDrive = new JointDrive { positionSpring = 0f, positionDamper = on ? force : 0f, maximumForce = on ? force : 0f };
            hinge.targetAngularVelocity = new Vector3(speed * Mathf.Deg2Rad, 0f, 0f);
            A.WakeUp();
            return this;
        }

        /// <summary>
        /// Makes a hinge springy: it pulls back to <paramref name="targetAngle"/> with the given strength, like a
        /// self-closing door.
        /// </summary>
        public JointHandle SetHingeSpring(float strength, float damping = 5f, float targetAngle = 0f)
        {
            var hinge = HingeJoint("hinge spring");
            hinge.angularXDrive = new JointDrive { positionSpring = Mathf.Max(0f, strength), positionDamper = Mathf.Max(0f, damping), maximumForce = float.MaxValue };
            hinge.targetRotation = Quaternion.AngleAxis(-targetAngle, Vector3.right);
            hinge.targetAngularVelocity = Vector3.zero;
            A.WakeUp();
            return this;
        }

        /// <summary>For hinges: how far it has turned from where it started, in degrees (-180 to 180).</summary>
        public float Angle
        {
            get
            {
                if (Kind != "hinge" || !A.Exists())
                    return 0f;
                var axis = ToWorld(_hingeAxisInB);
                var start = ToWorld(_hingeReferenceInB);
                var now = A.transform.TransformDirection(_hingeReferenceInA);
                return Vector3.SignedAngle(start, now, axis);
            }
        }

        /// <summary>Changes how hard the joint can be pulled before it breaks.</summary>
        public JointHandle SetBreakForce(float force)
        {
            if (Joint.Exists())
            {
                Joint.breakForce = force;
                Joint.breakTorque = force;
            }
            return this;
        }

        /// <summary>For ropes: the current length.</summary>
        public float RopeLength
        {
            get => Joint.TryCast<ConfigurableJoint>()?.linearLimit.limit ?? 0f;
            set
            {
                var rope = Joint.TryCast<ConfigurableJoint>();
                if (rope != null && Kind == "rope")
                    rope.linearLimit = new SoftJointLimit { limit = Mathf.Max(0.01f, value) };
            }
        }

        // ------------------------------------------------------------ internals

        private Vector3 _hingeAxisInB, _hingeReferenceInB, _hingeReferenceInA;

        /// <summary>Remembers the hinge's starting orientation, relative to B (or the world), for <see cref="Angle"/>.</summary>
        internal void StartHingeAngle(Vector3 worldAxis, Vector3 worldReference)
        {
            _hingeAxisInB = FromWorld(worldAxis);
            _hingeReferenceInB = FromWorld(worldReference);
            _hingeReferenceInA = A.transform.InverseTransformDirection(worldReference);
        }

        private Vector3 FromWorld(Vector3 direction) => B != null ? B.transform.InverseTransformDirection(direction) : direction;

        private Vector3 ToWorld(Vector3 direction) => B != null && B.Exists() ? B.transform.TransformDirection(direction) : direction;

        private ConfigurableJoint HingeJoint(string feature)
        {
            if (Kind != "hinge" || !Joint.Exists())
                throw new InvalidOperationException($"Only hinges have a {feature}.");
            return Joint.Cast<ConfigurableJoint>();
        }

        /// <summary>The rope's drawn line, if it has one: its thickness and colour (for saving it in a build).</summary>
        internal (float Thickness, Color Color)? Line { get; private set; }

        internal void ShowLine(float thickness, Color color)
        {
            Line = (thickness, color);
            var go = new GameObject("FruktSharedLibrary.Rope");
            _line = go.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.startWidth = _line.endWidth = thickness;
            _line.useWorldSpace = true;
            _line.sharedMaterial = Utilities.Meshes.CreateMaterial(null, color);
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            UpdateLine();
        }

        /// <summary>Per frame: keeps the rope line in place and notices breaks. False when the handle is finished.</summary>
        internal bool Tick()
        {
            if (_removed)
                return false;
            // If an object was deleted, its joint went with it: that's a removal, not a break.
            if (!A.Exists() || (B != null && !B.Exists()))
            {
                _removed = true;
                DestroyLine();
                return false;
            }
            if (!Joint.Exists())
            {
                if (!_broken)
                {
                    _broken = true;
                    RaiseBroke();
                }
                DestroyLine();
                return false;
            }
            UpdateLine();
            return true;
        }

        private void UpdateLine()
        {
            if (!_line.Exists())
                return;
            var from = A.transform.TransformPoint(Joint.anchor);
            var to = B != null ? B.transform.TransformPoint(Joint.connectedAnchor) : Joint.connectedAnchor;
            _line.SetPosition(0, from);
            _line.SetPosition(1, to);
        }

        internal void DestroyLine()
        {
            if (_line.Exists())
                UnityEngine.Object.Destroy(_line.gameObject);
            _line = null;
        }

        private void RaiseBroke()
        {
            var handlers = Broke;
            if (handlers == null)
                return;
            foreach (Action<JointHandle> handler in handlers.GetInvocationList())
            {
                try { handler(this); }
                catch (Exception e) { FruktLog.Error($"A {Kind} joint's Broke handler threw", e); }
            }
        }
    }
}
