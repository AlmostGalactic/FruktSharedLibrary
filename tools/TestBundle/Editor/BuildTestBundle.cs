// Builds the asset bundle the FruktSharedLibrary self-test loads. Run it through tools/build-test-bundle.ps1,
// which makes a Unity 6000.3.18f1 URP project, copies this script in and calls Build() in batch mode.
// Everything in the bundle is generated here, so the repository doesn't need to hold any binary assets.
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildTestBundle
{
    private const string Folder = "Assets/FSLTest";

    public static void Build()
    {
        Directory.CreateDirectory(Folder);

        // A 64x64 checkerboard texture.
        var checker = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int x = 0; x < 64; x++)
            for (int y = 0; y < 64; y++)
                checker.SetPixel(x, y, ((x / 8) + (y / 8)) % 2 == 0 ? new Color(0.2f, 0.6f, 1f) : Color.white);
        checker.Apply();
        File.WriteAllBytes(Folder + "/TestChecker.png", checker.EncodeToPNG());

        // A short "thunk": a decaying low sine with a little noise, as a 16-bit mono WAV.
        File.WriteAllBytes(Folder + "/TestHit.wav", Wav(Thunk(44100), 44100));

        AssetDatabase.Refresh();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/TestChecker.png");

        // A URP Lit material using the texture.
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(material, Folder + "/TestMaterial.mat");

        // Our own copy of a cube mesh, so the mesh comes from the bundle rather than Unity's built-in resources.
        var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mesh = Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
        mesh.name = "TestCrateMesh";
        AssetDatabase.CreateAsset(mesh, Folder + "/TestCrateMesh.asset");
        Object.DestroyImmediate(primitive);

        // A crate prefab: mesh, material, box collider and a 15 kg rigidbody, 0.6 m across.
        var crate = new GameObject("TestCrate");
        crate.transform.localScale = Vector3.one * 0.6f;
        crate.AddComponent<MeshFilter>().sharedMesh = mesh;
        crate.AddComponent<MeshRenderer>().sharedMaterial = material;
        crate.AddComponent<BoxCollider>();
        crate.AddComponent<Rigidbody>().mass = 15f;
        PrefabUtility.SaveAsPrefabAsset(crate, Folder + "/TestCrate.prefab");
        Object.DestroyImmediate(crate);

        var build = new AssetBundleBuild
        {
            assetBundleName = "fsltest.bundle",
            assetNames = new[]
            {
                Folder + "/TestCrate.prefab",
                Folder + "/TestMaterial.mat",
                Folder + "/TestChecker.png",
                Folder + "/TestHit.wav",
                Folder + "/TestCrateMesh.asset",
            },
        };
        Directory.CreateDirectory("BundleOut");
        var manifest = BuildPipeline.BuildAssetBundles("BundleOut", new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (manifest == null)
            throw new System.Exception("Building the test bundle failed.");
        Debug.Log("FSL test bundle built: " + Path.GetFullPath("BundleOut/fsltest.bundle"));
    }

    private static float[] Thunk(int rate)
    {
        var samples = new float[rate / 3];
        var random = new System.Random(1);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float decay = Mathf.Exp(-t * 18f);
            samples[i] = (Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.8f + ((float)random.NextDouble() * 2f - 1f) * 0.2f) * decay;
        }
        return samples;
    }

    private static byte[] Wav(float[] samples, int rate)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + samples.Length * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);      // PCM
        writer.Write((short)1);      // mono
        writer.Write(rate);
        writer.Write(rate * 2);      // bytes per second
        writer.Write((short)2);      // block align
        writer.Write((short)16);     // bits per sample
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(samples.Length * 2);
        foreach (var sample in samples)
            writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
        return stream.ToArray();
    }
}
