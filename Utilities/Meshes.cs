using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using Il2CppSpawnables.Props;
using UnityEngine;
using UnityEngine.Rendering;

namespace FruktSharedLibrary.Utilities
{
    /// <summary>
    /// Loading 3D models from Wavefront OBJ files (what Blender and most 3D tools export) into Unity meshes, and
    /// making materials that are lit like the game's own objects. Spawn the result with
    /// <see cref="Spawning.Spawner.SpawnMesh"/>.
    /// </summary>
    public static class Meshes
    {
        /// <summary>
        /// Loads an OBJ file into a mesh, or returns null if it can't be read. All objects and groups in the file
        /// become one mesh. Faces with more than three corners are split into triangles.
        /// </summary>
        /// <param name="scale">Multiplies every position, for models made in other units (0.01 for centimetres).</param>
        public static Mesh LoadObj(string path, float scale = 1f)
        {
            try
            {
                return ParseObj(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path), scale);
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Loading the model '{path}' failed: {e.Message}");
                return null;
            }
        }

        /// <summary>Builds a mesh from the text of an OBJ file (for models embedded in your mod).</summary>
        public static Mesh ParseObj(string objText, string name = "Mesh", float scale = 1f)
        {
            if (objText == null)
                throw new ArgumentNullException(nameof(objText));

            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var vertices = new List<Vector3>();
            var meshUvs = new List<Vector2>();
            var meshNormals = new List<Vector3>();
            var triangles = new List<int>();
            var corners = new Dictionary<(int, int, int), int>();
            bool hasUvs = false, hasNormals = false;

            using var reader = new StringReader(objText);
            string line;
            int lineNumber = 0;
            var polygon = new List<int>();
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                int comment = line.IndexOf('#');
                if (comment >= 0)
                    line = line.Substring(0, comment);
                var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;
                switch (parts[0])
                {
                    case "v":
                        // OBJ is right-handed and Unity is left-handed: mirror X (winding is reversed below).
                        positions.Add(new Vector3(-Float(parts, 1, lineNumber), Float(parts, 2, lineNumber), Float(parts, 3, lineNumber)) * scale);
                        break;
                    case "vt":
                        uvs.Add(new Vector2(Float(parts, 1, lineNumber), parts.Length > 2 ? Float(parts, 2, lineNumber) : 0f));
                        break;
                    case "vn":
                        normals.Add(new Vector3(-Float(parts, 1, lineNumber), Float(parts, 2, lineNumber), Float(parts, 3, lineNumber)));
                        break;
                    case "f":
                        polygon.Clear();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            var refs = parts[i].Split('/');
                            int v = Index(refs[0], positions.Count, lineNumber);
                            int vt = refs.Length > 1 && refs[1].Length > 0 ? Index(refs[1], uvs.Count, lineNumber) : -1;
                            int vn = refs.Length > 2 && refs[2].Length > 0 ? Index(refs[2], normals.Count, lineNumber) : -1;
                            hasUvs |= vt >= 0;
                            hasNormals |= vn >= 0;
                            if (!corners.TryGetValue((v, vt, vn), out int index))
                            {
                                index = vertices.Count;
                                corners[(v, vt, vn)] = index;
                                vertices.Add(positions[v]);
                                meshUvs.Add(vt >= 0 ? uvs[vt] : Vector2.zero);
                                meshNormals.Add(vn >= 0 ? normals[vn] : Vector3.zero);
                            }
                            polygon.Add(index);
                        }
                        // Fan triangulation, with the winding flipped to match the mirrored X axis.
                        for (int i = 1; i + 1 < polygon.Count; i++)
                        {
                            triangles.Add(polygon[0]);
                            triangles.Add(polygon[i + 1]);
                            triangles.Add(polygon[i]);
                        }
                        break;
                }
            }

            if (triangles.Count == 0)
                throw new FormatException("The model has no faces.");

            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices.ToArray();
            if (hasUvs)
                mesh.uv = meshUvs.ToArray();
            mesh.triangles = triangles.ToArray();
            if (hasNormals)
                mesh.normals = meshNormals.ToArray();
            else
                mesh.RecalculateNormals();
            if (hasUvs)
                mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A material lit and shaded like the game's objects (the game's URP Lit shader), optionally with a
        /// texture and a colour tint.
        /// </summary>
        public static Material CreateMaterial(Texture2D texture = null, Color? color = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Template.Shader ?? Shader.Find("Standard");
            var material = new Material(shader) { name = texture != null ? texture.name : "FruktSharedLibrary.Material" };
            if (texture != null)
                material.mainTexture = texture;
            material.color = color ?? (texture != null ? Color.white : new Color(0.75f, 0.75f, 0.75f));
            return material;
        }

        /// <summary>The physics layer the game's props use (grabbable, shootable, collides with everything).</summary>
        public static int PropLayer => Template.Layer;

        /// <summary>
        /// Puts an object, and every child with a collider, on <see cref="PropLayer"/>, so the player can grab
        /// and shoot it like the game's props.
        /// </summary>
        public static void UsePropLayer(GameObject root)
        {
            if (root == null)
                return;
            int layer = PropLayer;
            root.layer = layer;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                collider.gameObject.layer = layer;
        }

        // ------------------------------------------------------------ internals

        private static (int Layer, Shader Shader, int Scene) _template = (-1, null, -1);

        /// <summary>Layer and shader copied from one of the map's own props (cached per scene).</summary>
        private static (int Layer, Shader Shader) Template
        {
            get
            {
                int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
                if (_template.Layer >= 0 && _template.Scene == scene)
                    return (_template.Layer, _template.Shader);
                int layer = 0;
                Shader shader = null;
                try
                {
                    foreach (var prop in GameServices.FindObjects<Prop>())
                    {
                        var body = prop.GetComponentInChildrenIl2Cpp<Rigidbody>();
                        var renderer = prop.GetComponentInChildrenIl2Cpp<MeshRenderer>();
                        if (body == null)
                            continue;
                        layer = body.gameObject.layer;
                        shader = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.shader : null;
                        FruktLog.Debug($"Mesh objects copy layer {LayerMask.LayerToName(layer)} ({layer}) and shader {shader?.name} from prop '{prop.name}'");
                        break;
                    }
                }
                catch (Exception e)
                {
                    FruktLog.Debug("Reading a prop's layer failed: " + e.Message);
                }
                _template = (layer, shader, scene);
                return (layer, shader);
            }
        }

        private static float Float(string[] parts, int index, int line)
        {
            if (index >= parts.Length || !float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                throw new FormatException($"Line {line}: expected a number.");
            return value;
        }

        /// <summary>OBJ indices start at 1; negative ones count back from the end.</summary>
        private static int Index(string text, int count, int line)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value == 0)
                throw new FormatException($"Line {line}: bad index '{text}'.");
            int index = value > 0 ? value - 1 : count + value;
            if (index < 0 || index >= count)
                throw new FormatException($"Line {line}: index {value} is out of range.");
            return index;
        }
    }
}
