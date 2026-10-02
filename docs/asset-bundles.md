# Asset bundles

An asset bundle is a file of assets made in the Unity editor: prefabs, models, textures, materials and sounds.
`FruktSharedLibrary.Assets` loads them into FRUKT. For a single model, [OBJ files](spawning.md#your-own-models)
are simpler. Use a bundle when you want a whole prefab with its materials and colliders, or sounds.

## Making a bundle

You need **Unity 6000.3.18f1**, the version FRUKT is built with. Bundles from other versions may not load at all.

1. Make a project from the **Universal 3D** template. The game uses URP, so your materials should too.
2. Put your assets in it and set them up the way you want: prefabs with their meshes, materials, colliders and a
   `Rigidbody` if they should fall and be grabbable.
3. Select each asset (or the folder) and set its AssetBundle name at the bottom of the Inspector, for example
   `mymod.bundle`.
4. Add this script as `Assets/Editor/BuildBundles.cs`, then pick **Assets > Build Mod Bundles**:

```csharp
using System.IO;
using UnityEditor;

public static class BuildBundles
{
    [MenuItem("Assets/Build Mod Bundles")]
    public static void Build()
    {
        Directory.CreateDirectory("Bundles");
        BuildPipeline.BuildAssetBundles("Bundles", BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);
    }
}
```

The bundle ends up in the `Bundles` folder of the project. Unity also writes a `.manifest` file next to it,
which you don't need.

Scripts don't come along. If a prefab has your own `MonoBehaviour` on it, the game doesn't know that class and the
component is dropped. Unity's own components (renderers, colliders, rigidbodies, lights, audio sources, particle
systems) all work. Add behaviour from your mod after spawning, with [`ObjectEvents`](objects.md#object-events) and
[`Joints`](objects.md#joints).

## Loading it

Ship the bundle next to your mod and load it by path:

```csharp
var bundle = ModBundle.Load(Path.Combine(MelonEnvironment.ModsDirectory, "mymod.bundle"));
```

Or put it inside your DLL, so players only have one file to install. In your `.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="mymod.bundle" />
</ItemGroup>
```

```csharp
var bundle = ModBundle.LoadEmbedded(typeof(MyMod).Assembly, "mymod.bundle");
```

`ModBundle.Load(bytes, name)` loads one from bytes you already have.

All three return null and log why if the bundle can't be loaded. Loading the same bundle again gives back the one
that's already loaded, because Unity can't have the same bundle open twice. Load it once, when your mod starts,
and keep it.

## Using what's in it

| Member | Description |
|--------|-------------|
| `Load<T>(name)` | Loads an asset: `Load<GameObject>`, `Load<Texture2D>`, `Load<AudioClip>`, `Load<Material>`, `Load<Mesh>` and so on. Returns null if there isn't one by that name and type. |
| `Spawn(prefab, position, rotation = null, asProp = true)` | Puts a copy of a prefab in the world. |
| `Contains(name)` | Whether the bundle has an asset by that name. |
| `AssetNames` | Everything in the bundle, as the full paths Unity uses. |
| `Unload(unloadAssets = false)` | Unloads the bundle. With `true`, the assets you loaded from it are destroyed too, so spawned copies lose their meshes and textures. Delete those first. |
| `IsLoaded`, `Key`, `Bundle` | Its state, where it came from, and the Unity `AssetBundle`. |
| `ModBundle.All` | Every bundle that's loaded. |

Names don't care about case, and can be the full path (`assets/mymod/crate.prefab`), the file name
(`crate.prefab`) or just the name (`crate`). If two assets have the same name, use more of the path.

```csharp
var crate = bundle.Spawn("crate", LocalPlayer.GetPointInFront(3f));
ObjectEvents.For(crate).ImpactSounds = true;

var hit = bundle.Load<AudioClip>("crate_hit");
ObjectEvents.For(crate).Hit += impact => Sounds.PlayClip(hit, impact.Point);
```

`Spawn` puts the copy and its colliders on the same physics layer as the game's props, so the player can grab,
pin, throw and shoot it like anything else. Pass `asProp: false` to keep the layers from the prefab.

Sounds are played with [`Sounds.PlayClip`](world-and-player.md#your-own-sounds).

## Materials and shaders

A bundle carries its own copy of every shader its materials use. That copy can be missing the variants the game
needs, and then the material shows up pink. So `Load` and `Spawn` switch every material to the game's copy of the
same shader, keeping its textures, colours and render queue. URP Lit is what the game's own objects use, so it's
the safest choice.

A shader the game doesn't have, like one you wrote yourself, is kept if it runs. If it doesn't, the material
falls back to URP Lit. The textures stay, but it won't look the way you made it.

If you get materials some other way, `Shaders.FixMaterial(material)` and `Shaders.FixMaterials(gameObject)` do the
same switch. `Shaders.Find(name)` gives you the game's copy of a shader.
