using System;
using System.Collections;
using System.IO;
using FruktSharedLibrary.Assets;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Utilities;
using MelonLoader.Utils;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // Asset bundles and custom sounds. The full bundle test needs UserData/FruktSharedLibrary.testbundle,
    // built from tools/TestBundle (see docs/building-and-testing.md); without it only the basics run.
    internal static partial class SelfTest
    {
        private static string TestBundlePath => Path.Combine(MelonEnvironment.UserDataDirectory, "FruktSharedLibrary.testbundle");

        private static IEnumerator TestAssetBundles()
        {
            Section("Asset bundles: bad input", () =>
            {
                string junk = Path.Combine(Path.GetTempPath(), "FruktSharedLibrary.selftest.bundle");
                File.WriteAllBytes(junk, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
                try
                {
                    Check("ModBundle.Load returns null for a file that isn't a bundle", ModBundle.Load(junk) == null);
                    Check("ModBundle.Load returns null for bytes that aren't a bundle", ModBundle.Load(File.ReadAllBytes(junk), "junk") == null);
                    Check("ModBundle.Load returns null for a missing file", ModBundle.Load(junk + ".missing") == null);
                }
                finally
                {
                    File.Delete(junk);
                }
            });

            if (!File.Exists(TestBundlePath))
            {
                FruktLog.Msg("[SelfTest] No test bundle in UserData; skipping the bundle loading checks.");
                yield break;
            }

            ModBundle bundle = null;
            GameObject crate = null;
            AudioSource hitSound = null;
            Section("Asset bundles: loading", () =>
            {
                bundle = ModBundle.Load(TestBundlePath);
                Check("ModBundle.Load loads a real bundle", bundle != null && bundle.IsLoaded,
                    bundle == null ? "null" : string.Join(", ", bundle.AssetNames));
                if (bundle == null)
                    return;
                Check("Loading it again gives the same bundle", ModBundle.Load(TestBundlePath) == bundle);
                Check("Assets can be found by short name", bundle.Contains("TestCrate") && bundle.Contains("testchecker.png"));

                var texture = bundle.Load<Texture2D>("TestChecker");
                Check("Load<Texture2D>", texture != null && texture.width > 0, texture == null ? "null" : $"{texture.width}x{texture.height}");

                var clip = bundle.Load<AudioClip>("TestHit");
                Check("Load<AudioClip>", clip != null && clip.length > 0f, clip == null ? "null" : $"{clip.length:0.00}s");
                if (clip != null)
                {
                    // (Sounds made in code can't be tested: AudioClip.SetData is span-based and broken in this interop.)
                    hitSound = Sounds.PlayClip(clip, LocalPlayer.Position);
                    Check("Sounds.PlayClip plays a bundle sound through the game's world mixer group",
                        hitSound != null && hitSound.isPlaying && hitSound.outputAudioMixerGroup?.name == "TimeScaled", hitSound?.outputAudioMixerGroup?.name);
                    var ui = Sounds.PlayClip(clip, volume: 0.2f);
                    Check("Sounds without a position use the interface group", ui.outputAudioMixerGroup?.name == "UI", ui.outputAudioMixerGroup?.name);
                }

                var material = bundle.Load<Material>("TestMaterial");
                Check("Load<Material> uses the game's shader", material != null && material.shader.isSupported && material.mainTexture != null,
                    material == null ? "null" : $"{material.shader.name}, texture {material.mainTexture?.name}");

                crate = bundle.Spawn("TestCrate", LocalPlayer.GetPointInFront(4f) + Vector3.up * 2f);
                Check("Spawn puts a prefab in the world", crate != null);
                if (crate != null)
                {
                    var renderer = crate.GetComponentInChildren<Renderer>();
                    Check("Spawned prefab renders with the game's shader", renderer != null && renderer.sharedMaterial.shader.isSupported,
                        renderer?.sharedMaterial.shader.name);
                    Check("Spawned prefab is on the props' layer", crate.layer == Meshes.PropLayer);
                }
            });
            if (crate == null)
                yield break;
            float startY = crate.transform.position.y;
            yield return Wait(2f);
            Check("A one-shot sound cleans itself up", !hitSound.Exists());
            Check("The prefab's rigidbody falls", crate.transform.position.y < startY - 1f, $"{startY:0.0} -> {crate.transform.position.y:0.0}");
            Shot("bundle-crate");
            yield return Wait(1.5f);

            Section("Asset bundles: unloading", () =>
            {
                UnityEngine.Object.Destroy(crate);
                bundle.Unload(unloadAssets: true);
                Check("Unload", !bundle.IsLoaded && ModBundle.All.Count == 0);
                var again = ModBundle.Load(TestBundlePath);
                Check("A bundle can be loaded again after unloading", again != null && again.IsLoaded);
                again?.Unload(true);

                var fromBytes = ModBundle.Load(File.ReadAllBytes(TestBundlePath), "selftest-bytes");
                Check("ModBundle.Load loads a bundle from bytes", fromBytes != null && fromBytes.IsLoaded && fromBytes.Contains("TestCrate"));
                var prefab = fromBytes?.Load<GameObject>("TestCrate");
                Check("Assets load from a bundle loaded from bytes", prefab != null);
                fromBytes?.Unload(true);
            });
        }
    }
}
