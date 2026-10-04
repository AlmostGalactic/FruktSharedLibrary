# Getting started

You'll need FRUKT with MelonLoader 0.7 or newer, the .NET SDK (6 or newer), and `FruktSharedLibrary.dll` in
`FRUKT/Mods`. Start the game once after installing MelonLoader so it generates `MelonLoader/Il2CppAssemblies`.

## The quick way: the mod template

The library comes with a `dotnet new` template that sets up everything below for you. Install it once from a copy
of this repository:

```
dotnet new install path\to\FruktSharedLibrary\templates\FruktMod
```

Then make a mod:

```
dotnet new fruktmod -n MyMod --GameDir "D:\SteamLibrary\steamapps\common\FRUKT" --Author "Your name"
cd MyMod
dotnet build
```

The build puts `MyMod.dll` straight into `FRUKT\Mods`. The project:

- references the library, MelonLoader and every game assembly, so you never have to add one by hand
- finds the game in the `FRUKT_DIR` environment variable if it's set, otherwise in the folder you gave
  `--GameDir` (Steam's default folder if you didn't)
- stops with a clear message if it can't find the game or the library
- starts you off with a mod menu page and a tool in the terminal

Build with `-p:CopyToGameMods=false` to leave the Mods folder alone. To remove the template again:
`dotnet new uninstall path\to\FruktSharedLibrary\templates\FruktMod`.

## Project setup

If you'd rather set up the project yourself, make a class library that targets `net6.0`, and reference the library, MelonLoader and the game assemblies:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <FruktGameDir>C:\Program Files (x86)\Steam\steamapps\common\FRUKT</FruktGameDir>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="$(FruktGameDir)\Mods\FruktSharedLibrary.dll" Private="false" />
    <Reference Include="$(FruktGameDir)\MelonLoader\net6\MelonLoader.dll" Private="false" />
    <Reference Include="$(FruktGameDir)\MelonLoader\net6\0Harmony.dll" Private="false" />
    <Reference Include="$(FruktGameDir)\MelonLoader\net6\Il2CppInterop.Runtime.dll" Private="false" />
    <Reference Include="$(FruktGameDir)\MelonLoader\Il2CppAssemblies\Il2CppFRUKT.dll" Private="false" />
    <Reference Include="$(FruktGameDir)\MelonLoader\Il2CppAssemblies\Il2Cppmscorlib.dll" Private="false" />
    <Reference Include="$(FruktGameDir)\MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule.dll" Private="false" />
    <Reference Include="$(FruktGameDir)\MelonLoader\Il2CppAssemblies\UnityEngine.PhysicsModule.dll" Private="false" />
    <Reference Include="$(FruktGameDir)\MelonLoader\Il2CppAssemblies\Unity.InputSystem.dll" Private="false" />
  </ItemGroup>
</Project>
```

Set `FruktGameDir` to wherever your game is installed. `Private="false"` keeps the build from copying these
files next to your mod, since they're already in the game folder.

A few features need more assemblies from `MelonLoader\Il2CppAssemblies`. The compiler will tell you when:

| Assembly | Needed for |
|----------|------------|
| `Il2CppSystem.Core.dll` | `ToManagedList()` on game collections |
| `Il2CppTripledoseLibs.dll` | The game's own events (`Listen`) |
| `UnityEngine.AudioModule.dll` | Working with `AudioSource`s yourself, and `Load<AudioClip>` from a bundle |
| `UnityEngine.AssetBundleModule.dll` | Using a `ModBundle`'s Unity `AssetBundle` directly |
| `UnityEngine.UIModule.dll`, `UnityEngine.UI.dll`, `Unity.TextMeshPro.dll` | Building UI with `FruktUi` |
| `Il2CppZenject.dll` | Using `GameServices` containers directly |

## The dependency attribute

Put this in your `AssemblyInfo.cs`, next to `MelonInfo` and `MelonGame`:

```csharp
[assembly: MelonAdditionalDependencies("FruktSharedLibrary")]
```

It makes MelonLoader load the library before your mod, and warns players who don't have it.

## A full example

This one adds some right-click actions, a drop-down group, a mod menu page and a settings page:

```csharp
using FruktSharedLibrary.Controls;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Spawning;
using FruktSharedLibrary.UI;
using Il2CppLVA.NodesHierarchy.Benchmark.Variants; // HumanoidNodeTagValue
using MelonLoader;
using MelonLoader.Preferences; // ValueRange
using UnityEngine;
using UnityEngine.InputSystem; // Key

public class ExampleMod : MelonMod
{
    private bool _lowGravity;
    private int _blood;
    private KeyBind _panicKey = new KeyBind(Key.P);

    public override void OnInitializeMelon()
    {
        // React to the game
        GameEvents.SandboxReady += map => Notifications.Show($"Welcome to {World.GetMapDisplayName(map)}");
        GameEvents.CreatureDied += creature => LoggerInstance.Msg($"{creature.GetDisplayName()} died");

        // Right-click menu entries (shown on every body)
        ContextMenus.AddCreatureAction("Heal", creature => creature.Heal());
        ContextMenus.AddCreatureAction("Launch", creature => creature.AddForce(Vector3.up * 800f));
        ContextMenus.AddLimbAction("Pop", limb => limb.Damage(limb.transform.position, 6));

        // A drop-down group in the right-click menu, with a nested group inside
        var example = ContextMenus.AddCreatureGroup("Example")
            .AddCreatureAction("Kill", creature => creature.Kill())
            .AddToggle("Walking", ctx => ctx.Creature.IsWalking(), (ctx, on) => ctx.Creature.SetWalking(on));
        example.AddGroup("Throw")
            .AddCreatureAction("Up", creature => creature.AddForce(Vector3.up * 800f))
            .AddCreatureAction("Away", creature => creature.AddForce(LocalPlayer.Forward * 800f));

        // A page in the shared mod menu (F8, or MODS in the pause menu)
        ModMenu.AddPage("Example")
            .Header("Fun")
            .Toggle("Moon gravity", () => _lowGravity, on =>
            {
                _lowGravity = on;
                World.GravityStrength = on ? 1.62f : 9.81f;
            }).WithTooltip("Gravity of the Moon: 1.62 m/s².")
            .Slider("Time scale", 0.05f, 1f, () => World.TimeScale, v => World.TimeScale = v)
            .Choice("Blood", new[] { "normal", "lots", "none" }, () => _blood, i => _blood = i)
            .KeyBinding("Panic key", () => _panicKey, key => _panicKey = key)
            .Button("Spawn a human", () => Creatures.SpawnHumanInFront())
            .Button("Spawn a shotgun", () => Spawner.SpawnFirearmInFront(FirearmType.Grist03))
            .Button("Behead the closest human", () =>
            {
                var target = Creatures.GetNearest(LocalPlayer.Position, livingOnly: true);
                target?.GetLimb(HumanoidNodeTagValue.Head)?.Detach();
            });

        // Your own settings, editable in game on their own page
        var prefs = MelonPreferences.CreateCategory("ExampleMod", "Example settings");
        prefs.CreateEntry("ShowWelcome", true, "Show welcome", "Greet the player when a map loads.");
        prefs.CreateEntry("Volume", 0.8f, "Volume", "How loud the example sounds are.", false, false, new ValueRange<float>(0f, 1f));
        ModMenu.AddPreferencesPage(prefs);
    }
}
```

In the game, the mod shows up under Mods in the mod menu with its version and author, and both of its pages are
listed in its entry.

## A few tips

- Do your registering in `OnInitializeMelon`. You can add menu pages, right-click actions and event handlers at
  any time, but doing it once at start-up is simplest.
- Most gameplay calls need a loaded map. `GameEvents.SandboxReady` is a good place to start anything that needs
  one, and `GameState.InSandbox` tells you whether you're in one.
- If you use Harmony yourself, read the [IL2CPP notes](il2cpp-notes.md) first. Patching a tiny or empty method
  can end up hooking hundreds of unrelated ones.
- Creatures and limbs can be destroyed at any moment. Before using one you stored earlier, check
  `creature.IsValid()` or `obj.Exists()`.
