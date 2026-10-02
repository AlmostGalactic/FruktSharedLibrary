using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using Il2CppInfrastructure.Project.AssetsHandlers.SFX;
using Il2CppSpawnables.Bullets;
using Il2CppSpawnables.Weapons;
using UnityEngine;

namespace FruktSharedLibrary.Objects
{
    /// <summary>One collision: what was hit, where, and how hard.</summary>
    public readonly struct Impact
    {
        internal Impact(GameObject other, Rigidbody otherBody, Vector3 point, Vector3 normal, float speed)
        {
            Other = other;
            OtherBody = otherBody;
            Point = point;
            Normal = normal;
            Speed = speed;
        }

        /// <summary>The object it hit.</summary>
        public GameObject Other { get; }

        /// <summary>The other object's rigidbody, or null for static things like the ground.</summary>
        public Rigidbody OtherBody { get; }

        /// <summary>Where they touched.</summary>
        public Vector3 Point { get; }

        /// <summary>The surface direction at the contact.</summary>
        public Vector3 Normal { get; }

        /// <summary>How fast the two were moving towards each other, in metres per second.</summary>
        public float Speed { get; }
    }

    /// <summary>A shot that hit the object.</summary>
    public readonly struct ShotHit
    {
        internal ShotHit(Firearm firearm, Vector3 point, Vector3 normal)
        {
            Firearm = firearm;
            Point = point;
            Normal = normal;
        }

        /// <summary>The gun that fired, when the game says which one it was (null otherwise).</summary>
        public Firearm Firearm { get; }

        /// <summary>Where the shot hit.</summary>
        public Vector3 Point { get; }

        /// <summary>The surface direction where it hit.</summary>
        public Vector3 Normal { get; }
    }

    /// <summary>
    /// Events for one object in the world: it collided with something, hit something hard, was grabbed or let go
    /// of by the player, or was shot. Get one with <see cref="For(GameObject)"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var events = ObjectEvents.For(crate);
    /// events.ImpactSounds = true;
    /// events.Hit += impact => { if (impact.Speed > 12f) Explode(crate); };
    /// events.Grabbed += () => Notifications.Show("Careful, it's fragile");
    /// </code>
    /// </example>
    public sealed class ObjectEvents
    {
        private static readonly Dictionary<IntPtr, ObjectEvents> All = new();
        private static Rigidbody _held;
        private static bool _hooked;

        private CollisionRelay _relay;
        private float _lastSound;
        private Action<Impact> _collided;
        private Action<Impact> _hit;

        private ObjectEvents(GameObject gameObject)
        {
            GameObject = gameObject;
        }

        /// <summary>The events for an object, created the first time you ask.</summary>
        public static ObjectEvents For(GameObject gameObject)
        {
            if (gameObject == null)
                throw new ArgumentNullException(nameof(gameObject));
            // Events belong to the object with the rigidbody, so collisions on child colliders count too.
            var body = gameObject.GetComponentInParentIl2Cpp<Rigidbody>();
            var root = body != null ? body.gameObject : gameObject;
            if (!All.TryGetValue(root.Pointer, out var events) || !events.GameObject.Exists())
            {
                events = new ObjectEvents(root);
                All[root.Pointer] = events;
                Hook();
            }
            return events;
        }

        /// <summary>The events for the object a component is on.</summary>
        public static ObjectEvents For(Component component)
            => For(component != null ? component.gameObject : throw new ArgumentNullException(nameof(component)));

        /// <summary>The object these events are for.</summary>
        public GameObject GameObject { get; }

        /// <summary>Raised whenever the object starts touching something.</summary>
        public event Action<Impact> Collided
        {
            add { _collided += value; EnsureRelay(); }
            remove => _collided -= value;
        }

        /// <summary>Raised when the object hits something at <see cref="HitSpeed"/> or faster.</summary>
        public event Action<Impact> Hit
        {
            add { _hit += value; EnsureRelay(); }
            remove => _hit -= value;
        }

        /// <summary>How fast a collision has to be to count as a <see cref="Hit"/> (metres per second, 3 by default).</summary>
        public float HitSpeed { get; set; } = 3f;

        /// <summary>Plays an impact sound on every <see cref="Hit"/>, louder for harder hits, like the game's own props.</summary>
        public bool ImpactSounds
        {
            get => _impactSounds;
            set
            {
                _impactSounds = value;
                if (value)
                    EnsureRelay();
            }
        }

        private bool _impactSounds;

        /// <summary>Raised when the player picks the object up with the cursor tool.</summary>
        public event Action Grabbed;

        /// <summary>Raised when the player lets go of it.</summary>
        public event Action Released;

        /// <summary>Raised when a bullet hits the object. Each pellet of a shotgun blast counts separately.</summary>
        public event Action<ShotHit> Shot
        {
            add { _shot += value; EnsureRelay(); }
            remove => _shot -= value;
        }

        private Action<ShotHit> _shot;

        /// <summary>True while the player is holding the object.</summary>
        public bool IsHeld => _held != null && _held.Exists() && _held.gameObject.Pointer == GameObject.Pointer;

        /// <summary>Stops all events for this object.</summary>
        public void Clear()
        {
            _collided = null;
            _hit = null;
            Grabbed = null;
            Released = null;
            _shot = null;
            _impactSounds = false;
            if (_relay.Exists())
                UnityEngine.Object.Destroy(_relay);
            _relay = null;
            All.Remove(GameObject.Pointer);
        }

        // ------------------------------------------------------------ internals

        private void EnsureRelay()
        {
            if (_relay.Exists() || !GameObject.Exists() || !CollisionRelay.EnsureRegistered())
                return;
            _relay = GameObject.AddComponent<CollisionRelay>();
            _relay.Owner = this;
        }

        internal void HandleCollision(Collision collision)
        {
            try
            {
                var contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
                // The game's bullets are physical objects, so a shot arrives as a collision with one.
                var bullet = collision.rigidbody != null ? collision.rigidbody.GetComponent<Bullet>() : null;
                if (bullet != null)
                {
                    var firearm = GunThatFired(bullet);
                    Raise(_shot, new ShotHit(firearm, contact.point, contact.normal), "Shot");
                    return;
                }
                var other = collision.collider != null ? collision.collider.gameObject : null;
                var impact = new Impact(other, collision.rigidbody, contact.point, contact.normal, collision.relativeVelocity.magnitude);
                Raise(_collided, impact, "Collided");
                if (impact.Speed < HitSpeed)
                    return;
                Raise(_hit, impact, "Hit");
                if (_impactSounds && Time.unscaledTime - _lastSound > 0.08f)
                {
                    _lastSound = Time.unscaledTime;
                    Sounds.Play(ImpactSFXType.RbHit, impact.Point, Mathf.Clamp(impact.Speed / 12f, 0.15f, 1f));
                }
            }
            catch (Exception e)
            {
                FruktLog.Debug("Handling a collision failed: " + e.Message);
            }
        }

        /// <summary>
        /// A bullet's launcher is the ShotBus of the gun that fired it; find the gun that owns it. A bullet can
        /// tidy itself up in its own collision handler before ours runs, so fall back to the gun that fired last.
        /// </summary>
        private static Firearm GunThatFired(Bullet bullet)
        {
            var launcher = bullet.m_launcher;
            if (launcher != null)
            {
                foreach (var gun in Spawning.Spawner.GetFirearms())
                {
                    var bus = gun.ShotBus;
                    if (bus != null && bus.Pointer == launcher.Pointer)
                        return gun;
                }
            }
            return _lastFired.Exists() && Time.time - _lastFiredAt < 2f ? _lastFired : null;
        }

        private static Firearm _lastFired;
        private static float _lastFiredAt;

        private void Raise<T>(Action<T> handlers, T value, string name)
        {
            if (handlers == null)
                return;
            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try { handler(value); }
                catch (Exception e) { FruktLog.Error($"An ObjectEvents.{name} handler for '{GameObject.name}' threw", e); }
            }
        }

        private void Raise(Action handlers, string name)
        {
            if (handlers == null)
                return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception e) { FruktLog.Error($"An ObjectEvents.{name} handler for '{GameObject.name}' threw", e); }
            }
        }

        private static void Hook()
        {
            if (_hooked)
                return;
            _hooked = true;
            GameEvents.Update += WatchHeldObject;
            GameEvents.FirearmFired += gun =>
            {
                _lastFired = gun;
                _lastFiredAt = Time.time;
            };
            GameEvents.SandboxExited += () =>
            {
                All.Clear();
                _held = null;
            };
        }

        /// <summary>Compares the player's held object with last frame's to raise Grabbed and Released.</summary>
        private static void WatchHeldObject()
        {
            if (All.Count == 0)
                return;
            var held = GameState.InSandbox ? LocalPlayer.HeldObject : null;
            var before = _held;
            if (held == before || (held != null && before != null && held.Pointer == before.Pointer))
                return;
            _held = held;
            if (before.Exists() && All.TryGetValue(before.gameObject.Pointer, out var released))
                released.Raise(released.Released, "Released");
            if (held != null && All.TryGetValue(held.gameObject.Pointer, out var grabbed))
                grabbed.Raise(grabbed.Grabbed, "Grabbed");
        }
    }
}
