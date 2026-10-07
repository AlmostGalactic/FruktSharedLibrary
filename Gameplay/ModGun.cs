using System;
using FruktSharedLibrary.Core;
using UnityEngine;

namespace FruktSharedLibrary.Gameplay
{
    /// <summary>
    /// A gun a mod adds to the inventory. It's a <see cref="ModTool"/> that fires on left click (or for as long as the
    /// button is held, if it's automatic), no faster than its fire rate, and tells you each time through
    /// <see cref="Fired"/>. What a shot does is up to you; <see cref="Combat.Bullets"/> has the usual pieces. Make one
    /// with <see cref="Inventory.AddGun"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// Inventory.AddGun("Wasp-9")
    ///     .WithFireRate(12f, automatic: true)
    ///     .WithMuzzle(new Vector3(0f, 0.05f, 0.34f))
    ///     .OnFire(gun =>
    ///     {
    ///         var hit = Bullets.Fire(Bullets.Spread(LocalPlayer.AimRay, 1.5f));
    ///         Bullets.Tracer(gun.Muzzle, hit.End, Color.yellow);
    ///         Sounds.Play(WeaponSFXType.Shoot9MM, gun.Muzzle);
    ///     });
    /// </code>
    /// </example>
    public sealed class ModGun : ModTool
    {
        private float _ready;

        internal ModGun(string name, string category) : base(name, category)
        {
        }

        /// <summary>The shortest time between two shots, in seconds.</summary>
        public float FireInterval { get; private set; } = 0.25f;

        /// <summary>True if it keeps firing while the left button is held.</summary>
        public bool Automatic { get; private set; }

        /// <summary>The end of the barrel, in the model's own coordinates (before <see cref="ModTool.HeldScale"/>).</summary>
        public Vector3 MuzzleOffset { get; private set; }

        /// <summary>How many times it has fired since the game started.</summary>
        public int ShotsFired { get; private set; }

        /// <summary>Raised each time it fires, with the gun.</summary>
        public event Action<ModGun> Fired;

        /// <summary>How fast it can fire, in shots per second, and whether holding the button keeps it firing.</summary>
        public ModGun WithFireRate(float shotsPerSecond, bool automatic = false)
        {
            FireInterval = shotsPerSecond > 0f ? 1f / shotsPerSecond : 0f;
            Automatic = automatic;
            return this;
        }

        /// <summary>The time between shots, in seconds, for slow guns: <c>WithCooldown(1.5f)</c> for a rocket launcher.</summary>
        public ModGun WithCooldown(float seconds, bool automatic = false)
        {
            FireInterval = Mathf.Max(0f, seconds);
            Automatic = automatic;
            return this;
        }

        /// <summary>Where the barrel ends, in the model's own coordinates. <see cref="Muzzle"/> follows it in the hand.</summary>
        public ModGun WithMuzzle(Vector3 offset)
        {
            MuzzleOffset = offset;
            return this;
        }

        /// <summary>Runs <paramref name="action"/> each time it fires.</summary>
        public ModGun OnFire(Action<ModGun> action)
        {
            Fired += action;
            return this;
        }

        /// <summary>True when it's ready to fire again.</summary>
        public bool Ready => Time.time >= _ready;

        /// <summary>
        /// The end of the barrel in the world. While it isn't in the player's hand, a point a little below and in
        /// front of the camera.
        /// </summary>
        public Vector3 Muzzle
        {
            get
            {
                var held = HeldObject;
                if (held != null && held.transform.childCount > 0)
                    return held.transform.GetChild(0).TransformPoint(MuzzleOffset);
                return LocalPlayer.CameraPosition + LocalPlayer.CameraRotation * new Vector3(0.15f, -0.15f, 0.4f);
            }
        }

        /// <summary>
        /// Fires now, as if the player clicked, unless it's still cooling down. True if it fired. It works while the
        /// player isn't holding it too, from <see cref="Muzzle"/>'s fallback point.
        /// </summary>
        public bool TryFire()
        {
            if (!Ready)
                return false;
            _ready = Time.time + FireInterval;
            ShotsFired++;
            if (Fired == null)
                return true;
            foreach (Action<ModGun> handler in Fired.GetInvocationList())
            {
                try
                {
                    handler(this);
                }
                catch (Exception e)
                {
                    FruktLog.Error($"A Fired handler of '{Name}' threw", e);
                }
            }
            return true;
        }

        internal override void OnInput(ToolInput input, float value)
        {
            if (input == ToolInput.LeftClick || (input == ToolInput.LeftHold && Automatic))
                TryFire();
        }
    }
}
