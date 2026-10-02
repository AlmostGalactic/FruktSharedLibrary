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
    /// <summary>Which of the game's mixer groups a sound plays through (see <see cref="Sounds.PlayClip"/>).</summary>
    public enum SoundGroup
    {
        /// <summary>Things happening in the world. Slows down with slow motion, like gunshots and impacts.</summary>
        World,
        /// <summary>World sounds that keep normal speed in slow motion, like ambience and machines.</summary>
        Ambient,
        /// <summary>Interface sounds: menus and notifications.</summary>
        Interface,
    }

    /// <summary>
    /// Plays the game's own sound effects through its SFX service (correct mixer group, pooling and
    /// slow-motion pitch), and your own sounds with <see cref="PlayClip"/>. UI sounds are 2D; everything else is
    /// played in 3D at a position.
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

        /// <summary>
        /// Plays your own sound (for example one from an asset bundle) through the game's mixer, so the player's
        /// volume settings apply. With a position it's 3D; without, it's 2D. Returns the AudioSource so you can stop
        /// it; one-shot sounds clean themselves up when they finish.
        /// </summary>
        /// <param name="group">Which of the game's mixer groups to use. By default, sounds with a position are
        /// <see cref="SoundGroup.World"/> and sounds without one are <see cref="SoundGroup.Interface"/>.</param>
        public static AudioSource PlayClip(AudioClip clip, Vector3? position = null, float volume = 1f, float pitch = 1f, bool loop = false,
            SoundGroup? group = null)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));
            var go = new GameObject("FruktSharedLibrary.Sound " + clip.name);
            go.transform.position = position ?? LocalPlayer.CameraPosition;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Max(0f, volume);
            source.pitch = pitch;
            source.loop = loop;
            source.spatialBlend = position.HasValue ? 1f : 0f;
            source.minDistance = 1f;
            source.maxDistance = 60f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            var mixerGroup = MixerGroup(group ?? (position.HasValue ? SoundGroup.World : SoundGroup.Interface));
            if (mixerGroup != null)
                source.outputAudioMixerGroup = mixerGroup;
            source.Play();
            if (!loop)
                UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.05f, Mathf.Abs(pitch)) + 0.2f);
            return source;
        }

        private static readonly Dictionary<SoundGroup, UnityEngine.Audio.AudioMixerGroup> Groups = new();

        /// <summary>The game's mixer group for a kind of sound (cached; looked up by name in the game's mixer).</summary>
        private static UnityEngine.Audio.AudioMixerGroup MixerGroup(SoundGroup kind)
        {
            if (Groups.TryGetValue(kind, out var cached) && cached != null)
                return cached;
            string name = kind switch { SoundGroup.World => "TimeScaled", SoundGroup.Ambient => "Game", _ => "UI" };
            foreach (var group in Resources.FindObjectsOfTypeAll<UnityEngine.Audio.AudioMixerGroup>())
            {
                if (group != null && group.name == name)
                    return Groups[kind] = group;
            }
            return null;
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
