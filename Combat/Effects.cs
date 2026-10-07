using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Utilities;
using Il2CppServices.VFX;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace FruktSharedLibrary.Combat
{
    /// <summary>
    /// Fire, smoke, sparks and debris for mod weapons, made of the same little cubes as everything else in the game.
    /// <see cref="Explosion"/> is the whole thing at once; <see cref="Burst"/>, <see cref="Smoke"/> and
    /// <see cref="Flame"/> are the pieces to build your own. They need no setup and tidy themselves up.
    /// </summary>
    public static class Effects
    {
        /// <summary>The most cubes alive at once; effects asked for beyond that are skipped.</summary>
        public const int MaxParticles = 900;

        private sealed class Particle
        {
            internal GameObject Object;
            internal Transform Transform;
            internal MeshRenderer Renderer;
            internal Vector3 Velocity;
            internal float Born, Life, SizeFrom, SizeTo, Gravity, Drag, FadeStart;
            internal bool Bounce, Resting;
            internal Material[] Ramp;
            internal int Step;
        }

        private static readonly List<Particle> Live = new();
        private static readonly Stack<Particle> Spare = new();
        private static readonly Dictionary<(Color, Color, bool), Material[]> Ramps = new();
        private static readonly List<(Light Light, float Born, float Life, float Intensity)> Lights = new();
        private static Mesh _cube;

        /// <summary>How many cubes are alive right now.</summary>
        public static int Count => Live.Count;

        // ------------------------------------------------------------ the pieces

        /// <summary>
        /// A burst of cubes thrown out of a point.
        /// </summary>
        /// <param name="color">What they start as; they fade towards <paramref name="endColor"/> (or just darken).</param>
        /// <param name="speed">Metres per second, with some variation.</param>
        /// <param name="size">The cubes' edge, in metres.</param>
        /// <param name="glow">Lights up on its own, like fire and sparks.</param>
        /// <param name="gravity">Pull downwards in m/s² (negative rises, like hot air).</param>
        /// <param name="direction">Where the burst is aimed; without one it goes out in every direction.</param>
        /// <param name="spreadDegrees">How wide the cone round <paramref name="direction"/> is.</param>
        public static void Burst(Vector3 at, Color color, int count = 20, float speed = 4f, float size = 0.08f, float life = 0.5f,
            bool glow = true, float gravity = 0f, Vector3? direction = null, float spreadDegrees = 180f, Color? endColor = null, bool bounce = false)
        {
            var ramp = RampFor(color, endColor ?? color * 0.25f, glow);
            for (int i = 0; i < count; i++)
            {
                var dir = direction.HasValue ? Bullets.Spread(direction.Value, spreadDegrees * 0.5f) : Random.onUnitSphere;
                Spawn(at, dir * speed * Random.Range(0.45f, 1f), size * Random.Range(0.7f, 1.3f), size * Random.Range(0.2f, 0.6f),
                    life * Random.Range(0.6f, 1f), ramp, gravity, 1f, 0.3f, bounce);
            }
        }

        /// <summary>A puff of smoke that swells and thins away.</summary>
        public static void Smoke(Vector3 at, float size = 0.3f, float life = 1.5f, Vector3? velocity = null, float darkness = 0.3f)
        {
            var ramp = RampFor(new Color(darkness, darkness, darkness), new Color(darkness, darkness, darkness) * 0.4f, false);
            var v = (velocity ?? Vector3.up * 0.8f) + Random.insideUnitSphere * 0.25f;
            Spawn(at + Random.insideUnitSphere * size * 0.15f, v, size * 0.4f, size * Random.Range(0.9f, 1.4f), life * Random.Range(0.8f, 1.2f), ramp, -0.2f, 1.2f, 0.45f, false);
        }

        /// <summary>A small piece of flame, white-yellow going to red, for exhaust and torches.</summary>
        public static void Flame(Vector3 at, float size = 0.12f, float life = 0.3f, Vector3? velocity = null)
        {
            var ramp = RampFor(new Color(1f, 0.95f, 0.7f), new Color(0.8f, 0.1f, 0.02f), true);
            var v = (velocity ?? Vector3.zero) + Random.insideUnitSphere * 0.4f;
            Spawn(at, v, size, size * 0.2f, life * Random.Range(0.7f, 1.1f), ramp, 0f, 2f, 0.3f, false);
        }

        /// <summary>A flash of light that dies away over <paramref name="seconds"/>.</summary>
        public static void Flash(Vector3 at, Color color, float range = 8f, float seconds = 0.25f, float intensity = 6f)
        {
            try
            {
                var go = new GameObject("FSL light");
                go.transform.position = at;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.range = range;
                light.intensity = intensity;
                light.shadows = LightShadows.None;
                Lights.Add((light, Time.time, Mathf.Max(0.02f, seconds), intensity));
            }
            catch (Exception e)
            {
                FruktLog.Debug("A flash of light failed: " + e.Message);
            }
        }

        /// <summary>
        /// The flash and smoke at a gun's barrel, using the game's own muzzle effects when it has them.
        /// </summary>
        /// <param name="direction">The way the gun points.</param>
        public static void MuzzleFlash(Vector3 at, Vector3 direction, float size = 1f)
        {
            direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            if (!PlayGameMuzzle(at, direction))
            {
                Burst(at, new Color(1f, 0.8f, 0.35f), Mathf.RoundToInt(7 * size), 7f * size, 0.035f * size, 0.08f, true, 0f, direction, 40f, new Color(1f, 0.4f, 0.05f));
                Smoke(at + direction * 0.05f, 0.1f * size, 0.6f, direction * 1.5f, 0.55f);
            }
            Flash(at, new Color(1f, 0.75f, 0.35f), 4f * size, 0.06f, 5f * size);
        }

        private static bool PlayGameMuzzle(Vector3 at, Vector3 direction)
        {
            try
            {
                var weapon = GameServices.TryGet<INativeParticlesHandler>()?.Weapon;
                var factory = GameServices.TryGet<ISingleShotParticlesFactory>();
                if (weapon == null || factory == null)
                    return false;
                var rotation = Quaternion.LookRotation(direction);
                var none = new Il2CppSystem.Nullable<float>(0f) { hasValue = false };
                factory.Create(weapon.ShootMuzzleFlash, at, rotation, null, true, null, none, none);
                factory.Create(weapon.ShootSmoke, at, rotation, null, true, null, none, none);
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Debug("The game's muzzle effects failed: " + e.Message);
                return false;
            }
        }

        // ------------------------------------------------------------ explosions

        /// <summary>
        /// A proper explosion: a white-hot core, a fireball, a column of smoke, sparks, flung debris, dust along the
        /// ground, a flash of light and a shake of the camera that fades with distance. <paramref name="size"/> is
        /// roughly the radius in metres.
        /// </summary>
        public static void Explosion(Vector3 at, float size = 3f, bool shake = true)
        {
            size = Mathf.Max(0.3f, size);
            float s = size / 2.4f;
            bool onGround = Physics.Raycast(at + Vector3.up * 0.2f, Vector3.down, out var ground, size, Layers.Gameplay, QueryTriggerInteraction.Ignore);

            var core = RampFor(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.45f, 0.08f), true);
            for (int i = 0; i < 10; i++)
                Spawn(at, Random.insideUnitSphere * 2f * s, Random.Range(0.35f, 0.6f) * s, 0.1f * s, Random.Range(0.15f, 0.25f), core, 0f, 3f, 0.4f, false);

            var fire = RampFor(new Color(1f, 0.5f, 0.1f), new Color(0.5f, 0.06f, 0.02f), true);
            for (int i = 0; i < 56; i++)
            {
                var dir = (Random.onUnitSphere + Vector3.up * 0.35f).normalized;
                Spawn(at + Random.insideUnitSphere * 0.15f * s, dir * Random.Range(2.5f, 7f) * s, Random.Range(0.22f, 0.45f) * s, Random.Range(0.06f, 0.18f) * s,
                    Random.Range(0.5f, 1f), fire, -2.5f, 2.2f, 0.4f, false);
            }

            var smoke = RampFor(new Color(0.22f, 0.21f, 0.2f), new Color(0.05f, 0.05f, 0.05f), false);
            for (int i = 0; i < 34; i++)
            {
                var dir = (Random.onUnitSphere * 0.6f + Vector3.up).normalized;
                Spawn(at + Random.insideUnitSphere * 0.3f * s, dir * Random.Range(1.2f, 4f) * s, Random.Range(0.18f, 0.3f) * s, Random.Range(0.45f, 0.8f) * s,
                    Random.Range(1.6f, 3f), smoke, -1.4f, 1.2f, 0.5f, false);
            }

            var sparks = RampFor(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.35f, 0.05f), true);
            for (int i = 0; i < 40; i++)
            {
                var dir = (Random.onUnitSphere + Vector3.up * 0.6f).normalized;
                Spawn(at, dir * Random.Range(8f, 17f) * s, Random.Range(0.035f, 0.07f), 0.02f, Random.Range(0.5f, 1.2f), sparks, 16f, 0.4f, 0.3f, true);
            }

            var debris = RampFor(new Color(0.42f, 0.38f, 0.33f), new Color(0.3f, 0.27f, 0.24f), false);
            for (int i = 0; i < 24; i++)
            {
                var dir = (Random.onUnitSphere + Vector3.up * 1.2f).normalized;
                float edge = Random.Range(0.06f, 0.17f);
                Spawn(at, dir * Random.Range(5f, 13f) * s, edge, edge, Random.Range(2f, 3.5f), debris, 18f, 0.15f, 0.12f, true);
            }

            if (onGround)
            {
                var dust = RampFor(new Color(0.55f, 0.5f, 0.42f), new Color(0.3f, 0.28f, 0.26f), false);
                for (int i = 0; i < 28; i++)
                {
                    float angle = i / 28f * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
                    var dir = new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle));
                    Spawn(ground.point + Vector3.up * 0.1f, dir * Random.Range(5f, 9f) * s, 0.25f * s, Random.Range(0.5f, 0.8f) * s, Random.Range(0.6f, 0.9f), dust, -0.5f, 3.5f, 0.5f, false);
                }
            }

            Flash(at + Vector3.up * 0.3f, new Color(1f, 0.6f, 0.2f), 14f * s, 0.5f, 8f);
            if (shake)
            {
                float distance = Vector3.Distance(LocalPlayer.CameraPosition, at);
                float force = 1.4f * s / (1f + distance * 0.18f);
                if (force > 0.05f)
                    LocalPlayer.ShakeCamera(Mathf.Min(force, 2f));
            }
        }

        // ------------------------------------------------------------ the engine

        private static Particle Spawn(Vector3 position, Vector3 velocity, float sizeFrom, float sizeTo, float life, Material[] ramp,
            float gravity, float drag, float fadeStart, bool bounce)
        {
            if (Live.Count >= MaxParticles)
                return null;
            Particle p = null;
            while (Spare.Count > 0 && (p == null || !p.Object.Exists()))
                p = Spare.Pop();
            if (p == null || !p.Object.Exists())
                p = Create();
            p.Object.SetActive(true);
            p.Transform.position = position;
            p.Transform.rotation = Random.rotation;
            p.Velocity = velocity;
            p.Born = Time.time;
            p.Life = Mathf.Max(0.02f, life);
            p.SizeFrom = sizeFrom;
            p.SizeTo = sizeTo;
            p.Gravity = gravity;
            p.Drag = drag;
            p.FadeStart = Mathf.Max(0.01f, fadeStart);
            p.Bounce = bounce;
            p.Resting = false;
            p.Ramp = ramp;
            p.Step = -1;
            Apply(p, 0f);
            Live.Add(p);
            return p;
        }

        private static Particle Create()
        {
            if (_cube == null)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cube = primitive.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(primitive);
            }
            var go = new GameObject("FSL particle");
            go.AddComponent<MeshFilter>().sharedMesh = _cube;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new Particle { Object = go, Transform = go.transform, Renderer = renderer };
        }

        private static void Apply(Particle p, float t)
        {
            float size = Mathf.Lerp(p.SizeFrom, p.SizeTo, t) * Mathf.Clamp01((1f - t) / p.FadeStart);
            p.Transform.localScale = Vector3.one * Mathf.Max(0.001f, size);
            int step = Mathf.Min(p.Ramp.Length - 1, (int)(t * p.Ramp.Length));
            if (step != p.Step)
            {
                p.Step = step;
                p.Renderer.sharedMaterial = p.Ramp[step];
            }
        }

        private static Material[] RampFor(Color from, Color to, bool glow)
        {
            var key = (from, to, glow);
            if (Ramps.TryGetValue(key, out var ramp) && ramp[0] != null)
                return ramp;
            const int steps = 6;
            ramp = new Material[steps];
            for (int i = 0; i < steps; i++)
            {
                var color = Color.Lerp(from, to, i / (steps - 1f));
                var material = Meshes.CreateMaterial(color: color);
                material.hideFlags = HideFlags.DontUnloadUnusedAsset;
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", 0f);
                if (glow)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 1.5f);
                }
                ramp[i] = material;
            }
            Ramps[key] = ramp;
            return ramp;
        }

        internal static void Update()
        {
            if (Live.Count == 0 && Lights.Count == 0)
                return;
            float dt = Time.deltaTime;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                var p = Live[i];
                float t = (Time.time - p.Born) / p.Life;
                if (!p.Object.Exists() || t >= 1f)
                {
                    Retire(i);
                    continue;
                }
                if (!p.Resting)
                {
                    var from = p.Transform.position;
                    p.Velocity += Vector3.down * p.Gravity * dt;
                    p.Velocity *= Mathf.Max(0f, 1f - p.Drag * dt);
                    var to = from + p.Velocity * dt;
                    if (p.Bounce && Physics.Linecast(from, to, out var hit, Layers.Gameplay, QueryTriggerInteraction.Ignore))
                    {
                        to = hit.point + hit.normal * 0.02f;
                        p.Velocity = Vector3.Reflect(p.Velocity, hit.normal) * 0.35f;
                        if (p.Velocity.sqrMagnitude < 1f)
                        {
                            p.Velocity = Vector3.zero;
                            p.Resting = true;
                        }
                    }
                    p.Transform.position = to;
                }
                Apply(p, t);
            }

            for (int i = Lights.Count - 1; i >= 0; i--)
            {
                var (light, born, life, intensity) = Lights[i];
                float t = (Time.time - born) / life;
                if (!light.Exists() || t >= 1f)
                {
                    if (light.Exists())
                        Object.Destroy(light.gameObject);
                    Lights.RemoveAt(i);
                    continue;
                }
                light.intensity = intensity * (1f - t) * (1f - t);
            }
        }

        private static void Retire(int index)
        {
            var p = Live[index];
            Live.RemoveAt(index);
            if (p.Object.Exists())
            {
                p.Object.SetActive(false);
                Spare.Push(p);
            }
        }

        /// <summary>Takes every cube and light away at once (the library does this when a map is left).</summary>
        public static void Clear()
        {
            foreach (var p in Live)
            {
                if (p.Object.Exists())
                    Object.Destroy(p.Object);
            }
            Live.Clear();
            foreach (var p in Spare)
            {
                if (p.Object.Exists())
                    Object.Destroy(p.Object);
            }
            Spare.Clear();
            foreach (var (light, _, _, _) in Lights)
            {
                if (light.Exists())
                    Object.Destroy(light.gameObject);
            }
            Lights.Clear();
        }
    }
}
