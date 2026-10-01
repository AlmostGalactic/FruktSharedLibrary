using System;
using FruktSharedLibrary.Core;
using Il2CppAudio;
using Il2CppInfrastructure.Project.AssetsHandlers.SFX;
using Il2CppServices.Audio;
using UnityEngine;

namespace FruktSharedLibrary.Gameplay
{
    /// <summary>
    /// Plays the game's own sound effects through its SFX service (correct mixer group, pooling and
    /// slow-motion pitch). UI sounds are 2D; everything else is played in 3D at a position.
    /// </summary>
    public static class Sounds
    {
        /// <summary>Plays an interface sound (2D).</summary>
        public static bool Play(UISFXType sound, float volume = 1f)
            => Play(p => Service.Play(sound, ref p), null, volume);

        /// <summary>Plays a weapon sound at a world position.</summary>
        public static bool Play(WeaponSFXType sound, Vector3 position, float volume = 1f)
            => Play(p => Service.Play(sound, ref p), position, volume);

        /// <summary>Plays an impact sound at a world position.</summary>
        public static bool Play(ImpactSFXType sound, Vector3 position, float volume = 1f)
            => Play(p => Service.Play(sound, ref p), position, volume);

        /// <summary>Plays a tool sound at a world position.</summary>
        public static bool Play(ToolsSFXType sound, Vector3 position, float volume = 1f)
            => Play(p => Service.Play(sound, ref p), position, volume);

        /// <summary>Plays a whoosh sound at a world position.</summary>
        public static bool Play(WhooshSFXType sound, Vector3 position, float volume = 1f)
            => Play(p => Service.Play(sound, ref p), position, volume);

        private static ISFXPlayerService Service => GameServices.TryGet<ISFXPlayerService>();

        private static bool Play(Action<SFXPlayParams> play, Vector3? position, float volume)
        {
            if (Service == null)
                return false;
            try
            {
                var parameters = new SFXPlayParams
                {
                    Position = position ?? LocalPlayer.CameraPosition,
                    SpatialBlend = position.HasValue ? 1f : 0f,
                    Volume = new Il2CppSystem.Nullable<float>(Mathf.Max(0f, volume)),
                };
                play(parameters);
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning("Playing a sound failed: " + e.Message);
                return false;
            }
        }
    }
}
