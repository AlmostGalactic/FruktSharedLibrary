using System;
using System.Collections.Generic;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Utilities;
using Il2CppInfrastructure.Project.AssetsHandlers.SFX;
using Il2CppServices.Infrastructure;
using Il2CppServices.Spawnables;
using Il2CppLVA.Creatures;
using Il2CppLVA.Limbs;
using Il2CppSpawnables.Bullets;
using Il2CppSpawnables.Weapons;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace FruktSharedLibrary.Combat
{
    /// <summary>What a bullet from <see cref="Bullets"/> hit.</summary>
    public readonly struct BulletHit
    {
        internal BulletHit(Vector3 end, RaycastHit? hit, AbstractLimb limb)
        {
            End = end;
            Hit = hit.HasValue;
            Raycast = hit ?? default;
            Limb = limb;
        }

        /// <summary>True if it hit something within range.</summary>
        public bool Hit { get; }

        /// <summary>Where it stopped: the point it hit, or the end of its range.</summary>
        public Vector3 End { get; }

        /// <summary>The raycast, when it hit something.</summary>
        public RaycastHit Raycast { get; }

        /// <summary>The body part it hit, or null if it didn't hit a creature.</summary>
        public AbstractLimb Limb { get; }

        /// <summary>The creature it hit, or null.</summary>
        public AbstractCreature Creature => Limb?.GetCreature();

        /// <summary>The rigidbody it hit, or null for the ground and walls.</summary>
        public Rigidbody Body => Hit ? Raycast.rigidbody : null;
    }

    /// <summary>
    /// Bullets for mod guns. <see cref="Launch"/> flies the game's own bullets, which wound people exactly like the
    /// game's guns do. <see cref="Fire"/> and <see cref="Pierce"/> are instant hits along a ray that make a single
    /// wound where they land. Also spread, the point under the crosshair, and tracer lines.
    /// </summary>
    public static class Bullets
    {
        /// <summary>How far bullets go unless you say otherwise, in metres.</summary>
        public const float DefaultRange = 400f;

        /// <summary>
        /// An instant shot along <paramref name="ray"/>. A person gets one wound <paramref name="radiusVoxels"/> across
        /// (see <see cref="Damage.Apply(RaycastHit, int, float, Vector3?)"/>), and anything that moves gets an impulse
        /// of <paramref name="push"/> where it was hit.
        /// </summary>
        public static BulletHit Fire(Ray ray, float range = DefaultRange, int radiusVoxels = 2, float strength = Damage.DefaultStrength,
            float push = 5f, bool sound = true)
        {
            if (!Physics.Raycast(ray, out var hit, range, Layers.Gameplay, QueryTriggerInteraction.Ignore))
                return new BulletHit(ray.GetPoint(range), null, null);
            return Strike(hit, ray.direction, radiusVoxels, strength, push, sound);
        }

        /// <summary>
        /// A bullet that goes through what it hits, up to <paramref name="maxHits"/> things in a line. The ground,
        /// walls and anything else that doesn't move stop it. Returns everything it hit, nearest first.
        /// </summary>
        public static List<BulletHit> Pierce(Ray ray, int maxHits = 8, float range = DefaultRange, int radiusVoxels = 2,
            float strength = Damage.DefaultStrength, float push = 5f, bool sound = true)
        {
            var results = new List<BulletHit>();
            var hits = Physics.RaycastAll(ray, range, Layers.Gameplay, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
            foreach (var hit in hits)
            {
                bool moves = hit.rigidbody != null;
                results.Add(Strike(hit, ray.direction, radiusVoxels, strength, push, sound));
                if (!moves || results.Count >= maxHits)
                    break;
            }
            return results;
        }

        /// <summary>Where a pierced shot ended: the last thing it hit, or the end of its range.</summary>
        public static Vector3 EndOf(List<BulletHit> hits, Ray ray, float range = DefaultRange)
            => hits.Count > 0 ? hits[hits.Count - 1].End : ray.GetPoint(range);

        /// <summary>The same ray, turned a random amount up to <paramref name="degrees"/> off its direction.</summary>
        public static Ray Spread(Ray ray, float degrees) => new(ray.origin, Spread(ray.direction, degrees));

        /// <summary>A direction turned a random amount, up to <paramref name="degrees"/>.</summary>
        public static Vector3 Spread(Vector3 direction, float degrees)
        {
            if (degrees <= 0f)
                return direction.normalized;
            var offset = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            var forward = direction.normalized;
            return (forward + Quaternion.LookRotation(forward) * new Vector3(offset.x, offset.y, 0f)).normalized;
        }

        /// <summary>
        /// The point under the crosshair, up to <paramref name="range"/> away. Projectiles fired from a muzzle
        /// should head here, so they land where the player aimed rather than beside it.
        /// </summary>
        public static Vector3 AimPoint(float range = DefaultRange)
        {
            var ray = LocalPlayer.AimRay;
            return Physics.Raycast(ray, out var hit, range, Layers.Gameplay, QueryTriggerInteraction.Ignore) ? hit.point : ray.GetPoint(range);
        }

        private static BulletHit Strike(RaycastHit hit, Vector3 direction, int radiusVoxels, float strength, float push, bool sound)
        {
            var limb = Creatures.LimbFromCollider(hit.collider);
            if (limb != null)
            {
                if (strength > 0f && radiusVoxels > 0)
                    Damage.Apply(hit, radiusVoxels, strength, direction);
                if (push != 0f)
                    limb.AddForceAtPosition(direction * push, hit.point);
                if (sound)
                    Sounds.Play(ImpactSFXType.Bullet9MMToBodyOrganic, hit.point, 0.7f);
            }
            else
            {
                if (push != 0f && hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    hit.rigidbody.AddForceAtPosition(direction * push, hit.point, ForceMode.Impulse);
                if (sound)
                    Sounds.Play(ImpactSFXType.Bullet9MMHardSurface, hit.point, 0.5f);
            }
            return new BulletHit(hit.point, hit, limb);
        }


        // ------------------------------------------------------------ the game's own bullets

        /// <summary>Which of the game's bullets <see cref="Launch"/> flies.</summary>
        public enum Caliber
        {
            /// <summary>9mm, the Viper-17's round.</summary>
            Pistol,
            /// <summary>7.62, the Lynx-F's round.</summary>
            Rifle,
            /// <summary>One 12-gauge pellet, the Grist-03's round.</summary>
            Pellet,
        }

        private static readonly Dictionary<Caliber, (ShotBus Bus, IShotBusService Service)> Buses = new();

        /// <summary>
        /// Flies one of the game's real bullets from <paramref name="origin"/> along <paramref name="direction"/>. It is
        /// the same bullet the game's guns fire, so it goes into a body, makes the same wound channel, can come out the
        /// other side, and shoves what it hits.
        /// </summary>
        public static Bullet Launch(Vector3 origin, Vector3 direction, Caliber caliber = Caliber.Pistol, float speed = 0f)
        {
            try
            {
                var bullets = GameServices.TryGet<INativeSpawnablesHandler>()?.Weapons?.Bullets;
                var factory = GameServices.TryGet<IUniversalManagedBehaviourFactory>();
                if (bullets == null || factory == null)
                {
                    FruktLog.Warning("Can't launch a bullet: the game's bullets aren't registered yet.");
                    return null;
                }
                direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
                var rotation = new Il2CppSystem.Nullable<Quaternion>(Quaternion.LookRotation(direction));
                Bullet bullet;
                switch (caliber)
                {
                    case Caliber.Rifle:
                        bullet = factory.CreateByPassport(bullets.Bullet762, origin, rotation).Cast<Bullet>();
                        break;
                    case Caliber.Pellet:
                        bullet = factory.CreateByPassport(bullets.Bullet12gaPellet, origin, rotation).Cast<Bullet>();
                        break;
                    default:
                        bullet = factory.CreateByPassport(bullets.Bullet9mm, origin, rotation).Cast<Bullet>();
                        break;
                }
                var bus = BusFor(caliber);
                int shot = bus.Open();
                bus.CountRound(shot);
                bullet.Launch(direction, bus.Cast<IBulletLauncher>(), shot);
                var body = bullet.GetComponent<Rigidbody>();
                if (body != null)
                    body.velocity = direction * (speed > 0f ? speed : DefaultSpeed(caliber));
                return bullet;
            }
            catch (Exception e)
            {
                FruktLog.Warning("Launching a bullet failed: " + e.Message);
                return null;
            }
        }

        private static float DefaultSpeed(Caliber caliber) => caliber switch
        {
            Caliber.Rifle => 400f,
            Caliber.Pellet => 400f,
            _ => 200f,
        };

        private static ShotBus BusFor(Caliber caliber)
        {
            var service = GameServices.TryGet<IShotBusService>();
            if (!Buses.TryGetValue(caliber, out var entry) || entry.Bus == null || entry.Service != service)
            {
                var bus = new ShotBus();
                // The game's service listens to a bus for where its rounds land, and plays the impact sounds.
                service?.Hear(bus);
                entry = (bus, service);
                Buses[caliber] = entry;
            }
            return entry.Bus;
        }

        // ------------------------------------------------------------ tracers

        private static readonly List<(LineRenderer Line, float Born, float Life)> Tracers = new();
        private static readonly Dictionary<Color, Material> TracerMaterials = new();

        /// <summary>A line from <paramref name="from"/> to <paramref name="to"/> that thins away over <paramref name="seconds"/>.</summary>
        public static void Tracer(Vector3 from, Vector3 to, Color color, float width = 0.012f, float seconds = 0.08f)
        {
            if (!TracerMaterials.TryGetValue(color, out var material) || material == null)
            {
                material = Meshes.CreateMaterial(color: color);
                material.hideFlags = HideFlags.DontUnloadUnusedAsset;
                TracerMaterials[color] = material;
            }
            var go = new GameObject("FSL tracer");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            Tracers.Add((line, Time.time, Mathf.Max(0.01f, seconds)));
        }

        /// <summary>How many tracers are showing right now.</summary>
        public static int TracerCount => Tracers.Count;

        internal static void Update()
        {
            for (int i = Tracers.Count - 1; i >= 0; i--)
            {
                var (line, born, life) = Tracers[i];
                float t = (Time.time - born) / life;
                if (!line.Exists() || t >= 1f)
                {
                    if (line.Exists())
                        Object.Destroy(line.gameObject);
                    Tracers.RemoveAt(i);
                    continue;
                }
                line.widthMultiplier = 1f - t;
            }
        }
    }
}
