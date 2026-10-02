using System.Collections;
using System.IO;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Spawning;
using FruktSharedLibrary.Utilities;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // Meshes.LoadObj / ParseObj and Spawner.SpawnMesh, including a real grab with the game's cursor tool.
    internal static partial class SelfTest
    {
        // A 1 m cube with UVs and normals, one quad per side.
        private const string CubeObj = @"
# test cube
v -0.5 -0.5 -0.5
v  0.5 -0.5 -0.5
v  0.5  0.5 -0.5
v -0.5  0.5 -0.5
v -0.5 -0.5  0.5
v  0.5 -0.5  0.5
v  0.5  0.5  0.5
v -0.5  0.5  0.5
vt 0 0
vt 1 0
vt 1 1
vt 0 1
vn 0 0 -1
vn 0 0 1
vn 0 -1 0
vn 0 1 0
vn -1 0 0
vn 1 0 0
f 1/1/1 4/4/1 3/3/1 2/2/1
f 5/1/2 6/2/2 7/3/2 8/4/2
f 1/1/3 2/2/3 6/3/3 5/4/3
f 4/1/4 8/4/4 7/3/4 3/2/4
f 1/1/5 5/2/5 8/3/5 4/4/5
f 2/1/6 3/4/6 7/3/6 6/2/6
";

        private static IEnumerator TestMeshes()
        {
            Mesh cube = null;
            Section("Meshes: parsing", () =>
            {
                cube = Meshes.ParseObj(CubeObj, "TestCube");
                Check("ParseObj builds a cube", cube != null && cube.vertexCount == 24 && cube.triangles.Length == 36,
                    cube == null ? "null" : $"{cube.vertexCount} vertices, {cube.triangles.Length / 3} triangles");
                Check("The cube is 1 m on each side", cube != null && Vector3.Distance(cube.bounds.size, Vector3.one) < 0.001f, cube?.bounds.size.ToString());
                // Faces must point outwards after the handedness flip: the top face's normal points up.
                bool outwards = false;
                if (cube != null)
                {
                    var vertices = cube.vertices;
                    var triangles = cube.triangles;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                        var normal = Vector3.Cross(b - a, c - a).normalized;
                        var center = (a + b + c) / 3f;
                        if (center.y > 0.49f)
                            outwards = normal.y > 0.9f;
                    }
                }
                Check("Faces point outwards after converting to Unity's axes", outwards);

                var triangle = Meshes.ParseObj("v 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\nf -4 -3 -1 -2", "Negative", 2f);
                Check("Negative indices, n-gons, scale, no UVs or normals",
                    triangle.vertexCount == 4 && triangle.triangles.Length == 6 && Mathf.Abs(triangle.bounds.size.x - 2f) < 0.001f,
                    $"{triangle.vertexCount} vertices, {triangle.triangles.Length / 3} triangles, size {triangle.bounds.size}");

                bool threw = false;
                try { Meshes.ParseObj("v 1 2\nf 1 2 3"); } catch (System.FormatException) { threw = true; }
                Check("Broken OBJ text is reported", threw);

                string file = Path.Combine(Path.GetTempPath(), "FruktSharedLibrary.selftest.obj");
                File.WriteAllText(file, CubeObj);
                var loaded = Meshes.LoadObj(file, 0.5f);
                File.Delete(file);
                Check("LoadObj reads a file", loaded != null && Vector3.Distance(loaded.bounds.size, Vector3.one * 0.5f) < 0.001f, loaded?.bounds.size.ToString());
                Check("LoadObj returns null for a missing file", Meshes.LoadObj(file) == null);
            });
            if (cube == null)
                yield break;

            // A checkerboard texture so the UVs show up in the screenshot.
            var checker = new Texture2D(8, 8) { filterMode = FilterMode.Point, name = "Checker" };
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++)
                    checker.SetPixel(x, y, (x + y) % 2 == 0 ? new Color(1f, 0.65f, 0f) : new Color(0.15f, 0.15f, 0.15f));
            checker.Apply();
            var material = Meshes.CreateMaterial(checker);
            Check("CreateMaterial uses the game's lit shader", material != null && material.shader != null && material.shader.isSupported,
                material?.shader?.name);

            GameObject spawned = null;
            Section("Meshes: spawning", () =>
            {
                spawned = Spawner.SpawnMeshInFront(cube, 4f, material, mass: 20f);
                Check("SpawnMeshInFront", spawned != null);
                if (spawned != null)
                    Check("Mesh objects use the props' physics layer", spawned.layer == Meshes.PropLayer,
                        $"{LayerMask.LayerToName(spawned.layer)} ({spawned.layer})");
            });
            if (spawned == null)
                yield break;

            float startY = spawned.transform.position.y;
            yield return Wait(2.5f);
            var body = spawned.GetComponent<Rigidbody>();
            Check("The mesh falls and comes to rest on the ground",
                spawned.Exists() && body != null && body.linearVelocity.magnitude < 0.2f && spawned.transform.position.y > startY - 1.5f,
                $"y {startY:0.00} -> {spawned.transform.position.y:0.00}, speed {body?.linearVelocity.magnitude:0.00}");
            Shot("mesh-spawned");
            yield return Wait(1.5f);

            // Grab it with the game's cursor tool: float it in front of the camera and hold the mouse button.
            spawned.transform.position = LocalPlayer.CameraPosition + LocalPlayer.Forward * 3f;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            yield return Wait(0.5f);
            FruktLog.Msg("[SelfTest] MOUSEDOWN mesh-grab");
            for (float end = Now() + 2f; LocalPlayer.HeldObject == null && Now() < end;)
                yield return null;
            Check("The player can grab a mesh with the cursor tool", LocalPlayer.HeldObject != null && LocalPlayer.HeldObject.Pointer == body.Pointer,
                LocalPlayer.HeldObject == null ? "nothing held" : LocalPlayer.HeldObject.name);
            Shot("mesh-held");
            yield return Wait(1f);
            FruktLog.Msg("[SelfTest] MOUSEUP mesh-grab");
            yield return Wait(1f);
            body.useGravity = true;
            Check("Releasing it drops it", LocalPlayer.HeldObject == null);
            yield return Wait(1f);
            Object.Destroy(spawned);
        }
    }
}
