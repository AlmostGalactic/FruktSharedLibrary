using System;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FruktSharedLibrary.Utilities
{
    /// <summary>
    /// Pictures of 3D models, for icons. Mod tools and props without an icon get one of these automatically; use
    /// it yourself for anything else that needs a picture of a model.
    /// </summary>
    public static class Thumbnails
    {
        /// <summary>
        /// Draws a model on a transparent background, seen from the front, a little above and to the side, lit
        /// from behind the viewer. Returns null if it has nothing to draw or drawing failed (the log says why).
        /// Call it in a map: on the main menu the game's screen effects cover the picture, and you get null.
        /// </summary>
        /// <param name="size">Width and height in pixels.</param>
        public static Sprite Render(GameObject model, int size = 128)
        {
            if (!model.Exists())
                return null;
            var texture = TakePicture(() =>
            {
                var copy = Object.Instantiate(model);
                copy.SetActive(true);
                // Only the picture is wanted: nothing that could run physics or be bumped into while it exists.
                foreach (var joint in copy.GetComponentsInChildren<Joint>(true))
                    Object.DestroyImmediate(joint);
                foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
                    Object.DestroyImmediate(body);
                foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
                Assets.Shaders.FixMaterials(copy);
                return copy;
            }, size, model.name);
            return texture == null ? null : Textures.ToSprite(texture);
        }

        /// <summary>Draws a mesh with a material (a plain grey one if null). See <see cref="Render(GameObject, int)"/>.</summary>
        public static Sprite Render(Mesh mesh, Material material = null, int size = 128)
        {
            if (!mesh.Exists())
                return null;
            var texture = TakePicture(() =>
            {
                var go = new GameObject(mesh.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material.Exists() ? material : DefaultMaterial;
                return go;
            }, size, mesh.name);
            return texture == null ? null : Textures.ToSprite(texture);
        }

        // ------------------------------------------------------------ internals

        private static Material _defaultMaterial;

        private static Material DefaultMaterial => _defaultMaterial.Exists() ? _defaultMaterial : _defaultMaterial = Meshes.CreateMaterial();

        // The model is put far below the map on a layer of its own and drawn twice, over black and over white.
        // Where it covers the background the two pictures match; where it doesn't they differ, which gives each
        // pixel's transparency without depending on how the render pipeline treats alpha.
        private static Texture2D TakePicture(Func<GameObject> build, int size, string what)
        {
            size = Mathf.Clamp(size, 16, 1024);
            int layer = FreeLayer();
            GameObject model = null, cameraObject = null, lightObject = null;
            RenderTexture target = null;
            var previousTarget = UnityEngine.RenderTexture.active;
            var previousSun = RenderSettings.sun;
            bool previousFog = RenderSettings.fog;
            try
            {
                model = build();
                model.transform.position = new Vector3(0f, -5000f, 0f);
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = layer;

                bool any = false;
                var bounds = new Bounds();
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        continue;
                    if (any)
                        bounds.Encapsulate(renderer.bounds);
                    else
                        bounds = renderer.bounds;
                    any = true;
                }
                if (!any || bounds.size == Vector3.zero)
                    return null;

                const float fov = 25f;
                var direction = new Vector3(0.55f, 0.45f, 1f).normalized;
                float radius = bounds.extents.magnitude;
                float distance = radius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) * 1.02f;
                cameraObject = new GameObject("FruktSharedLibrary thumbnail camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << layer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.fieldOfView = fov;
                camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
                camera.farClipPlane = distance + radius * 2f;
                camera.transform.position = bounds.center + direction * distance;
                camera.transform.LookAt(bounds.center);

                lightObject = new GameObject("FruktSharedLibrary thumbnail light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                light.shadows = LightShadows.None;
                light.cullingMask = 1 << layer;
                lightObject.transform.rotation = Quaternion.LookRotation(-(direction + Vector3.up * 0.6f + Vector3.right * 0.3f).normalized);
                RenderSettings.sun = light;
                RenderSettings.fog = false;

                target = UnityEngine.RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                var black = Draw(camera, target, Color.black, size);
                var white = Draw(camera, target, Color.white, size);

                var result = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = what + " thumbnail", wrapMode = TextureWrapMode.Clamp };
                int covered = 0;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        var b = black.GetPixel(x, y);
                        var w = white.GetPixel(x, y);
                        float alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                        if (alpha < 0.02f)
                        {
                            result.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                            continue;
                        }
                        covered++;
                        // Over black, the picture is the model's colour times its alpha.
                        result.SetPixel(x, y, new Color(Mathf.Clamp01(b.r / alpha), Mathf.Clamp01(b.g / alpha), Mathf.Clamp01(b.b / alpha), alpha));
                    }
                }
                Object.Destroy(black);
                Object.Destroy(white);
                // Nothing covered means nothing was drawn; everything covered means something painted over the
                // whole picture (some screen effects do that), so it isn't a picture of the model either.
                if (covered == 0 || covered == size * size)
                {
                    FruktLog.Debug($"The thumbnail of '{what}' came out {(covered == 0 ? "empty" : "without a background")}.");
                    Object.Destroy(result);
                    return null;
                }
                result.Apply();
                result.hideFlags = HideFlags.HideAndDontSave;
                return result;
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Drawing a thumbnail of '{what}' failed: {e.Message}");
                return null;
            }
            finally
            {
                UnityEngine.RenderTexture.active = previousTarget;
                RenderSettings.sun = previousSun;
                RenderSettings.fog = previousFog;
                if (target != null)
                    UnityEngine.RenderTexture.ReleaseTemporary(target);
                if (model.Exists())
                    Object.DestroyImmediate(model);
                if (cameraObject.Exists())
                    Object.DestroyImmediate(cameraObject);
                if (lightObject.Exists())
                    Object.DestroyImmediate(lightObject);
            }
        }

        private static Texture2D Draw(Camera camera, RenderTexture target, Color background, int size)
        {
            camera.backgroundColor = background;
            camera.Render();
            UnityEngine.RenderTexture.active = target;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            texture.Apply();
            return texture;
        }

        // A layer nothing in the game uses, so the camera sees only the model.
        private static int FreeLayer()
        {
            for (int layer = 31; layer >= 8; layer--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
                    return layer;
            }
            return 31;
        }
    }
}
