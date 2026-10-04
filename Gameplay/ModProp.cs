using System;
using System.Collections.Generic;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Spawning;
using FruktSharedLibrary.Utilities;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FruktSharedLibrary.Gameplay
{
    /// <summary>
    /// A prop a mod adds to the inventory. It's listed under Props in the terminal, the player puts it on the
    /// toolbar like the game's own props, and while they hold it a hologram shows where it will go: left click
    /// puts a copy there and the mouse wheel turns it. Make one with
    /// <see cref="Inventory.AddProp(string, Mesh, Material, float)"/> or
    /// <see cref="Inventory.AddProp(string, GameObject)"/>. Everything a <see cref="ModTool"/> has works too, so
    /// you can also listen for the mouse buttons yourself.
    /// </summary>
    /// <example>
    /// <code>
    /// var barrel = Meshes.LoadObj(Path.Combine(MelonEnvironment.UserDataDirectory, "barrel.obj"));
    /// Inventory.AddProp("Barrel", barrel, Meshes.CreateMaterial(color: Color.red), mass: 40f)
    ///     .WithDescription("A red barrel.")
    ///     .OnPlaced(placed => MelonLogger.Msg($"Placed a barrel at {placed.transform.position}"));
    /// </code>
    /// </example>
    public sealed class ModProp : ModTool
    {
        private GameObject _hologram;
        private bool _hologramFailed;
        private Bounds _bounds;
        private static Material _hologramMaterial;

        // The copies props have put in the world, so a build can tell which prop each one is.
        private static readonly Dictionary<IntPtr, (GameObject Copy, ModProp Prop)> Copies = new();

        internal ModProp(string name, Mesh mesh, Material material, float mass) : base(name, "Props")
        {
            Mesh = mesh;
            Material = material;
            Mass = mass;
        }

        internal ModProp(string name, GameObject prefab) : base(name, "Props")
        {
            Prefab = prefab;
        }

        /// <summary>The mesh it's made of, for a prop made from a mesh. Null for one made from a prefab.</summary>
        public Mesh Mesh { get; private set; }

        /// <summary>The material of a prop made from a mesh (null for the default grey one).</summary>
        public Material Material { get; private set; }

        /// <summary>How heavy a prop made from a mesh is, in kilograms.</summary>
        public float Mass { get; }

        /// <summary>The prefab copied for each placed prop, for one made from a prefab. Null for one made from a mesh.</summary>
        public GameObject Prefab { get; private set; }

        /// <summary>How far away the player can place it, in metres. Further than that, it's put in front of them.</summary>
        public float Reach { get; private set; } = 30f;

        /// <summary>How far the player has turned it with the mouse wheel, in degrees.</summary>
        public float Turn { get; set; }

        /// <summary>How many degrees one notch of the mouse wheel turns it.</summary>
        public float TurnStep { get; private set; } = 15f;

        /// <summary>The hologram showing where it will go, while the player holds it. Otherwise null.</summary>
        public GameObject Hologram => _hologram.Exists() ? _hologram : null;

        /// <summary>A copy was put in the world (by the player, or by <see cref="Place"/>). Gives the copy.</summary>
        public event Action<GameObject> Placed;

        // ------------------------------------------------------------ setup

        /// <summary>Sets the text on its card in the terminal.</summary>
        public new ModProp WithDescription(string description)
        {
            base.WithDescription(description);
            return this;
        }

        /// <summary>Adds a row to its card in the terminal, like ("size", "2 m"). Keys must be different.</summary>
        public new ModProp WithCard(string key, string value)
        {
            base.WithCard(key, value);
            return this;
        }

        /// <summary>Sets its icon in the terminal and on the toolbar. Square images look best.</summary>
        public new ModProp WithIcon(Sprite icon)
        {
            base.WithIcon(icon);
            return this;
        }

        /// <summary>Sets how far away the player can place it, in metres.</summary>
        public ModProp WithReach(float metres)
        {
            Reach = Mathf.Max(1f, metres);
            return this;
        }

        /// <summary>Sets how many degrees one notch of the mouse wheel turns it (0 to stop the wheel turning it).</summary>
        public ModProp WithTurnStep(float degrees)
        {
            TurnStep = degrees;
            return this;
        }

        /// <summary>Runs <paramref name="action"/> with each copy that's put in the world.</summary>
        public ModProp OnPlaced(Action<GameObject> action)
        {
            Placed += action;
            return this;
        }

        /// <summary>
        /// Switches it to another mesh (and material, if you give one), for example after reloading an OBJ file
        /// with <see cref="FileWatch"/>. The hologram and the icon follow; copies already placed stay as they are.
        /// </summary>
        public ModProp SetMesh(Mesh mesh, Material material = null)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            FollowBundle(null, null);
            Mesh = mesh;
            if (material != null)
                Material = material;
            Prefab = null;
            Changed();
            return this;
        }

        /// <summary>Switches it to another prefab. The hologram and the icon follow; copies already placed stay as they are.</summary>
        public ModProp SetPrefab(GameObject prefab)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            FollowBundle(null, null);
            SwapPrefab(prefab);
            return this;
        }

        internal void SwapPrefab(GameObject prefab)
        {
            if (prefab == null)
                return;
            Prefab = prefab;
            Mesh = null;
            Changed();
        }

        private void Changed()
        {
            _bounds = default;
            HideHologram();
            _hologramFailed = false;
            if (Registered)
                Internal.ModItems.ModelChanged(this);
        }

        internal override Sprite RenderThumbnail()
            => Prefab.Exists() ? Thumbnails.Render(Prefab) : Mesh.Exists() ? Thumbnails.Render(Mesh, Material) : null;

        // ------------------------------------------------------------ placing

        /// <summary>
        /// Where it would go if the player clicked now: on whatever they're aiming at, facing them and turned by
        /// <see cref="Turn"/>. Returns false if they aren't aiming at anything within <see cref="Reach"/>; the
        /// position is then in mid-air in front of them (it still works, the prop just falls).
        /// </summary>
        public bool TryGetPlacement(out Vector3 position, out Quaternion rotation)
        {
            EnsureBounds();
            float yaw = LocalPlayer.CameraRotation.eulerAngles.y + 180f + Turn;
            rotation = Quaternion.Euler(0f, yaw, 0f);
            if (LocalPlayer.Raycast(out var hit, Reach, Layers.Gameplay))
            {
                // Push it out along the surface's normal until its box just touches the surface.
                var normal = hit.normal;
                var local = Quaternion.Inverse(rotation) * normal;
                var extents = _bounds.extents;
                float support = Mathf.Abs(local.x) * extents.x + Mathf.Abs(local.y) * extents.y + Mathf.Abs(local.z) * extents.z;
                position = hit.point + normal * (support + 0.01f) - rotation * _bounds.center;
                return true;
            }
            position = LocalPlayer.AimRay.GetPoint(Mathf.Min(4f, Reach)) - rotation * _bounds.center;
            return false;
        }

        /// <summary>
        /// Puts a copy in the world. A mesh becomes a physics object like <see cref="Spawner.SpawnMesh"/> makes; a
        /// prefab is copied as it is, on the props' layer (give it a Rigidbody if it should move). Returns the copy,
        /// or null if it couldn't be made.
        /// </summary>
        public GameObject Place(Vector3 position, Quaternion rotation)
        {
            GameObject placed = null;
            try
            {
                if (Prefab.Exists())
                {
                    placed = Object.Instantiate(Prefab, position, rotation);
                    placed.name = Prefab.name;
                    placed.SetActive(true);
                    Meshes.UsePropLayer(placed);
                    Assets.Shaders.FixMaterials(placed);
                }
                else if (Mesh.Exists())
                {
                    placed = Spawner.SpawnMesh(Mesh, position, rotation, Material, Mass);
                    placed.name = Name;
                }
                else
                {
                    FruktLog.Warning($"'{Name}' has no mesh or prefab any more, so it can't be placed.");
                    return null;
                }
            }
            catch (Exception e)
            {
                FruktLog.Error($"Placing '{Name}' failed", e);
                return null;
            }
            if (Copies.Count > 512)
            {
                foreach (var key in Copies.Where(c => !c.Value.Copy.Exists()).Select(c => c.Key).ToList())
                    Copies.Remove(key);
            }
            Copies[placed.Pointer] = (placed, this);
            if (Placed != null)
            {
                foreach (Action<GameObject> handler in Placed.GetInvocationList())
                {
                    try
                    {
                        handler(placed);
                    }
                    catch (Exception e)
                    {
                        FruktLog.Error($"A Placed handler of '{Name}' threw", e);
                    }
                }
            }
            return placed;
        }

        /// <summary>The prop <paramref name="copy"/> was placed from, or null if it isn't a placed copy of one.</summary>
        public static ModProp CopyOf(GameObject copy)
        {
            if (!copy.Exists() || !Copies.TryGetValue(copy.Pointer, out var entry))
                return null;
            // The game reuses the memory of destroyed objects, so check it's still the same object.
            if (!entry.Copy.Exists() || entry.Copy.Pointer != copy.Pointer)
            {
                Copies.Remove(copy.Pointer);
                return null;
            }
            return entry.Prop;
        }

        internal override void OnInput(ToolInput input, float value)
        {
            switch (input)
            {
                case ToolInput.Held:
                    ShowHologram();
                    break;
                case ToolInput.Deselected:
                    HideHologram();
                    break;
                case ToolInput.LeftClick:
                    TryGetPlacement(out var position, out var rotation);
                    Place(position, rotation);
                    break;
                case ToolInput.Scroll:
                    Turn = Mathf.Repeat(Turn + Mathf.Sign(value) * TurnStep, 360f);
                    break;
            }
        }

        // ------------------------------------------------------------ the hologram

        private void ShowHologram()
        {
            if (!_hologram.Exists())
            {
                // One try: a hologram that can't be made would otherwise fail (and log) every frame.
                if (_hologramFailed)
                    return;
                _hologram = BuildHologram();
                _hologramFailed = _hologram == null;
                if (_hologram == null)
                    return;
            }
            // The terminal covers the view; the game hides its own holograms while it's open too.
            bool show = !Inventory.TerminalOpen;
            if (show)
            {
                TryGetPlacement(out var position, out var rotation);
                _hologram.transform.SetPositionAndRotation(position, rotation);
            }
            if (_hologram.activeSelf != show)
                _hologram.SetActive(show);
        }

        private void HideHologram()
        {
            if (_hologram.Exists())
                Object.Destroy(_hologram);
            _hologram = null;
        }

        private GameObject BuildHologram()
        {
            GameObject hologram = null;
            try
            {
                if (Prefab.Exists())
                {
                    hologram = Object.Instantiate(Prefab);
                    // Only for show: nothing that could collide, fall or hold it together.
                    foreach (var joint in hologram.GetComponentsInChildren<Joint>(true))
                        Object.DestroyImmediate(joint);
                    foreach (var body in hologram.GetComponentsInChildren<Rigidbody>(true))
                        Object.DestroyImmediate(body);
                    foreach (var collider in hologram.GetComponentsInChildren<Collider>(true))
                        Object.DestroyImmediate(collider);
                }
                else if (Mesh.Exists())
                {
                    hologram = new GameObject();
                    hologram.AddComponent<MeshFilter>().sharedMesh = Mesh;
                    hologram.AddComponent<MeshRenderer>();
                }
                else
                {
                    return null;
                }
                hologram.name = Name + " (hologram)";
                var material = HologramMaterial;
                foreach (var renderer in hologram.GetComponentsInChildren<Renderer>(true))
                {
                    int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                    var materials = new Il2CppReferenceArray<Material>(count);
                    for (int i = 0; i < count; i++)
                        materials[i] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                foreach (var t in hologram.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = 2; // Ignore Raycast
                return hologram;
            }
            catch (Exception e)
            {
                FruktLog.Error($"Making the hologram of '{Name}' failed; it's placed without one", e);
                if (hologram.Exists())
                    Object.Destroy(hologram);
                return null;
            }
        }

        // The box around the prop in its own space, for sitting it on surfaces.
        private void EnsureBounds()
        {
            if (_bounds.size != Vector3.zero)
                return;
            if (Mesh.Exists())
            {
                _bounds = Mesh.bounds;
                return;
            }
            if (!Prefab.Exists())
                return;
            bool any = false;
            var bounds = new Bounds();
            var toRoot = Prefab.transform.worldToLocalMatrix;
            void Add(Mesh mesh, Transform where)
            {
                if (!mesh.Exists())
                    return;
                var matrix = toRoot * where.localToWorldMatrix;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var point = matrix.MultiplyPoint3x4(corner);
                    if (!any)
                        bounds = new Bounds(point, Vector3.zero);
                    else
                        bounds.Encapsulate(point);
                    any = true;
                }
            }
            foreach (var filter in Prefab.GetComponentsInChildren<MeshFilter>(true))
                Add(filter.sharedMesh, filter.transform);
            foreach (var skinned in Prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Add(skinned.sharedMesh, skinned.transform);
            _bounds = any ? bounds : new Bounds(Vector3.zero, Vector3.one * 0.5f);
        }

        // See-through and unlit, tinted like the game's placement holograms.
        private static Material HologramMaterial
        {
            get
            {
                if (_hologramMaterial.Exists())
                    return _hologramMaterial;
                var shader = Assets.Shaders.Find("Universal Render Pipeline/Unlit") ?? Assets.Shaders.Find("Universal Render Pipeline/Lit");
                var material = new Material(shader) { name = "FruktSharedLibrary hologram", hideFlags = HideFlags.HideAndDontSave };
                // URP's transparent setup, minus Material.SetOverrideTag: it's span-based and broken in this interop,
                // and URP picks the transparent pass from the render queue anyway.
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.color = new Color(0.55f, 0.85f, 1f, 0.35f);
                _hologramMaterial = material;
                return material;
            }
        }
    }
}
