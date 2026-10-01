using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
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

        /// <summary>
        /// Plays the start of an interface sound and fades it out after <paramref name="seconds"/>. Some of the
        /// game's UI sounds are long: <c>WindowOpenClose</c> is a two-second run of ticks that the game only lets
        /// play while one of its screens wipes in or out, then fades. Play sounds like that this way.
        /// </summary>
        /// <param name="fadeOut">How long the fade at the end takes.</param>
        public static bool PlayFor(UISFXType sound, float seconds, float volume = 1f, float fadeOut = 0.15f)
        {
            var before = new HashSet<int>();
            foreach (var source in PlayingSources(sound))
                before.Add(source.GetInstanceID());
            if (!Play(sound, volume))
                return false;
            var started = PlayingSources(sound).FindAll(s => !before.Contains(s.GetInstanceID()));
            if (started.Count == 0)
                return true; // Couldn't find it; the sound simply plays in full.
            Scheduler.After(Mathf.Max(0f, seconds), () =>
            {
                foreach (var source in started)
                {
                    try
                    {
                        if (source.Exists() && source.Source != null && source.Source.isPlaying)
                            source.FadeOutAndStop(Mathf.Max(0.01f, fadeOut));
                    }
                    catch (Exception e)
                    {
                        FruktLog.Debug("Fading a sound out failed: " + e.Message);
                    }
                }
            });
            return true;
        }

        /// <summary>True while an interface sound is playing (any instance of it).</summary>
        internal static bool IsPlaying(UISFXType sound) => PlayingSources(sound).Count > 0;

        /// <summary>The game's pooled one-shot sources currently playing <paramref name="sound"/>.</summary>
        private static List<SingleShotAudioSource> PlayingSources(UISFXType sound)
        {
            var result = new List<SingleShotAudioSource>();
            string name = sound.ToString();
            foreach (var source in GameServices.FindObjects<SingleShotAudioSource>())
            {
                var audio = source?.Source;
                if (audio == null || !audio.isPlaying)
                    continue;
                string clip = audio.clip != null ? audio.clip.name : audio.resource != null ? audio.resource.name : null;
                if (clip == name || source.gameObject.name == "SingleShot_" + name)
                    result.Add(source);
            }
            return result;
        }

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
