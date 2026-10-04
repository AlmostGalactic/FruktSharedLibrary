using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace FruktSharedLibrary.Assets
{
    /// <summary>
    /// An asset bundle made in the Unity editor, loaded into the game: prefabs, models, textures, materials and
    /// sounds. Build bundles with the same Unity version as the game (6000.3.18f1).
    /// </summary>
    /// <example>
    /// <code>
    /// var bundle = ModBundle.Load(Path.Combine(MelonEnvironment.ModsDirectory, "mymod.bundle"));
    /// var crate = bundle.Spawn("crate", LocalPlayer.GetPointInFront(3f));
    /// var hit = bundle.Load&lt;AudioClip&gt;("hit");
    /// </code>
    /// </example>
    public sealed class ModBundle
    {
        private static readonly Dictionary<string, ModBundle> Loaded = new(StringComparer.OrdinalIgnoreCase);
        private static bool _managedLoadAssetBroken;

        private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);

        private Utilities.FileWatch _watch;

        private ModBundle(string key, AssetBundle bundle, bool fromFile)
        {
            Key = key;
            FromFile = fromFile;
            Index(bundle);
        }

        private void Index(AssetBundle bundle)
        {
            Bundle = bundle;
            AssetNames = BundleNative.GetAllAssetNames(bundle);
            _names.Clear();
            foreach (var path in AssetNames)
            {
                // Find assets by full path ("assets/mymod/crate.prefab"), file name ("crate.prefab") or plain name ("crate").
                _names[path] = path;
                var file = Path.GetFileName(path);
                _names.TryAdd(file, path);
                _names.TryAdd(Path.GetFileNameWithoutExtension(file), path);
            }
        }

        /// <summary>Where the bundle came from: its file path, or the name it was loaded under.</summary>
        public string Key { get; }

        /// <summary>The Unity bundle, for anything this class doesn't cover.</summary>
        public AssetBundle Bundle { get; private set; }

        /// <summary>Every asset in the bundle, as the full lower-case paths Unity uses ("assets/mymod/crate.prefab").</summary>
        public IReadOnlyList<string> AssetNames { get; private set; }

        /// <summary>Whether it was loaded from a file (so it can be reloaded), not from bytes or your DLL.</summary>
        public bool FromFile { get; }

        /// <summary>
        /// The bundle was loaded again from its file (see <see cref="Reload"/> and <see cref="WatchForChanges"/>).
        /// Load your assets again: <see cref="Load{T}"/> now gives the new versions. Tools and props made from it
        /// with <see cref="Gameplay.Inventory.AddProp(string, ModBundle, string)"/> or
        /// <see cref="Gameplay.ModTool.WithModel(ModBundle, string, Vector3, Vector3, float)"/> update themselves.
        /// </summary>
        public event Action<ModBundle> Reloaded;

        /// <summary>Whether <see cref="WatchForChanges"/> is on.</summary>
        public bool IsWatching => _watch != null && _watch.IsWatching;

        /// <summary>True until <see cref="Unload"/> is called.</summary>
        public bool IsLoaded => Bundle != null && Bundle.m_CachedPtr != IntPtr.Zero;

        // ------------------------------------------------------------ loading bundles

        /// <summary>
        /// Loads a bundle file, or returns null if it can't be loaded. Loading the same file again gives back the
        /// same bundle (Unity can't have one bundle loaded twice).
        /// </summary>
        public static ModBundle Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("No path given.", nameof(path));
            var full = Path.GetFullPath(path);
            if (Loaded.TryGetValue(full, out var existing) && existing.IsLoaded)
                return existing;
            if (!File.Exists(full))
            {
                FruktLog.Warning($"Asset bundle '{full}' doesn't exist.");
                return null;
            }
            return Register(full, Try(() => BundleNative.LoadFromFile(full), full), true);
        }

        /// <summary>Loads a bundle from bytes you already have, under a name of your choice.</summary>
        public static ModBundle Load(byte[] data, string name)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("No bundle data given.", nameof(data));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Give the bundle a name.", nameof(name));
            if (Loaded.TryGetValue(name, out var existing) && existing.IsLoaded)
                return existing;
            return Register(name, Try(() => BundleNative.LoadFromMemory(data), name), false);
        }

        /// <summary>
        /// Loads a bundle embedded in your mod's DLL (an EmbeddedResource in your .csproj). <paramref name="resourceName"/>
        /// can be the end of the resource's name, like "mymod.bundle".
        /// </summary>
        public static ModBundle LoadEmbedded(Assembly assembly, string resourceName)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            string match = null;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase))
                {
                    match = name;
                    break;
                }
            }
            if (match == null)
            {
                FruktLog.Warning($"'{assembly.GetName().Name}' has no embedded resource ending in '{resourceName}'.");
                return null;
            }
            using var stream = assembly.GetManifestResourceStream(match);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return Load(memory.ToArray(), assembly.GetName().Name + "/" + match);
        }

        /// <summary>Every bundle loaded through this class that's still loaded.</summary>
        public static IReadOnlyList<ModBundle> All
        {
            get
            {
                var list = new List<ModBundle>();
                foreach (var bundle in Loaded.Values)
                {
                    if (bundle.IsLoaded)
                        list.Add(bundle);
                }
                return list;
            }
        }

        // ------------------------------------------------------------ assets

        /// <summary>Whether the bundle has an asset with this name (full path, file name, or name without extension).</summary>
        public bool Contains(string name) => name != null && _names.ContainsKey(name);

        /// <summary>
        /// Loads an asset by name, or returns null. Materials (and the materials on prefabs) are switched to the
        /// game's own shaders so they render correctly.
        /// </summary>
        public T Load<T>(string name) where T : UnityEngine.Object
        {
            if (!IsLoaded)
                throw new ObjectDisposedException(Key, "The bundle was unloaded.");
            if (name == null || !_names.TryGetValue(name, out var path))
            {
                FruktLog.Warning($"Bundle '{Key}' has no asset called '{name}'.");
                return null;
            }
            var asset = LoadAsset(path, Il2CppType.Of<T>());
            var typed = asset?.TryCast<T>();
            if (typed == null)
            {
                FruktLog.Warning($"'{path}' in bundle '{Key}' isn't a {typeof(T).Name}.");
                return null;
            }
            var material = typed.TryCast<Material>();
            if (material != null)
                Shaders.FixMaterial(material);
            var prefab = typed.TryCast<GameObject>();
            if (prefab != null)
                Shaders.FixMaterials(prefab);
            return typed;
        }

        /// <summary>
        /// Puts a copy of a prefab in the world. Its materials use the game's shaders, and anything with a
        /// rigidbody goes on the same physics layer as the game's props, so the player can grab and shoot it.
        /// </summary>
        /// <param name="asProp">False to keep the prefab's own layers.</param>
        public GameObject Spawn(string prefab, Vector3 position, Quaternion? rotation = null, bool asProp = true)
        {
            var source = Load<GameObject>(prefab);
            if (source == null)
                return null;
            var copy = UnityEngine.Object.Instantiate(source, position, rotation ?? Quaternion.identity);
            copy.name = source.name;
            if (asProp)
                Utilities.Meshes.UsePropLayer(copy);
            return copy;
        }

        /// <summary>
        /// Unloads the bundle. With <paramref name="unloadAssets"/>, the assets loaded from it are destroyed too, so
        /// spawned copies lose their meshes and textures; delete those first.
        /// </summary>
        public void Unload(bool unloadAssets = false)
        {
            _watch?.Stop();
            _watch = null;
            if (!IsLoaded)
                return;
            BundleNative.Unload(Bundle, unloadAssets);
            Bundle = null;
            Loaded.Remove(Key);
        }

        // ------------------------------------------------------------ reloading

        /// <summary>
        /// Loads the bundle again from its file, after you've rebuilt it in Unity, and raises <see cref="Reloaded"/>.
        /// What you already loaded or spawned from the old one keeps working; it isn't updated. Only bundles loaded
        /// from a file can be reloaded. Returns false if the new file can't be loaded (the log says why); the
        /// bundle is then unloaded until a reload works.
        /// </summary>
        public bool Reload()
        {
            if (!FromFile)
            {
                FruktLog.Warning($"Bundle '{Key}' was loaded from bytes, so it can't be reloaded.");
                return false;
            }
            // Unity can't have two copies of a bundle open, so the old one goes first. Its assets stay in memory
            // (unloadAssets: false), so what was spawned from it doesn't lose its meshes and textures.
            if (IsLoaded)
                BundleNative.Unload(Bundle, false);
            Bundle = null;
            var fresh = Try(() => BundleNative.LoadFromFile(Key), Key);
            if (fresh == null)
            {
                FruktLog.Warning($"Reloading bundle '{Key}' failed; it stays unloaded until the file can be loaded again.");
                return false;
            }
            Index(fresh);
            Loaded[Key] = this;
            FruktLog.Msg($"Reloaded asset bundle '{System.IO.Path.GetFileName(Key)}' ({AssetNames.Count} assets).");
            if (Reloaded != null)
            {
                foreach (Action<ModBundle> handler in Reloaded.GetInvocationList())
                {
                    try
                    {
                        handler(this);
                    }
                    catch (Exception e)
                    {
                        FruktLog.Error($"A Reloaded handler of bundle '{Key}' threw", e);
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Reloads the bundle by itself whenever its file changes, so you can rebuild it in Unity and see the result
        /// without restarting the game. Meant for while you're making your mod; it checks the file twice a second.
        /// Returns the bundle, for chaining after <see cref="Load(string)"/>.
        /// </summary>
        public ModBundle WatchForChanges()
        {
            if (!FromFile)
            {
                FruktLog.Warning($"Bundle '{Key}' was loaded from bytes, so there's no file to watch.");
                return this;
            }
            if (!IsWatching)
                _watch = Utilities.FileWatch.Start(Key, () => Reload());
            return this;
        }

        // ------------------------------------------------------------ internals

        private UnityEngine.Object LoadAsset(string path, Il2CppSystem.Type type)
        {
            if (!_managedLoadAssetBroken)
            {
                try
                {
                    return Bundle.LoadAsset(path, type);
                }
                catch (Exception e) when (e is MissingMethodException || e is TypeLoadException)
                {
                    _managedLoadAssetBroken = true;
                    FruktLog.Debug("AssetBundle.LoadAsset needs the native call in this game: " + e.Message);
                }
            }
            return BundleNative.LoadAsset(Bundle, path, type);
        }

        private static AssetBundle Try(Func<AssetBundle> load, string what)
        {
            try
            {
                return load();
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Loading asset bundle '{what}' failed: {e.Message}");
                return null;
            }
        }

        private static ModBundle Register(string key, AssetBundle bundle, bool fromFile)
        {
            if (bundle == null)
            {
                FruktLog.Warning($"'{key}' isn't an asset bundle this version of Unity can read (build it with Unity 6000.3.18f1), " +
                                 "or another bundle with the same contents is already loaded.");
                return null;
            }
            var loaded = new ModBundle(key, bundle, fromFile);
            Loaded[key] = loaded;
            FruktLog.Debug($"Loaded asset bundle '{key}' ({loaded.AssetNames.Count} assets)");
            return loaded;
        }
    }
}
