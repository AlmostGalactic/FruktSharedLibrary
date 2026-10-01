# Getting started

## Requirements

- FRUKT with MelonLoader 0.7 or newer. Start the game once after installing MelonLoader so it generates
  `MelonLoader/Il2CppAssemblies`.
- The .NET SDK (6 or newer).
- `FruktSharedLibrary.dll` in `FRUKT/Mods`.

## Project setup

Create an SDK-style class library targeting `net6.0` and reference the library, MelonLoader and the game
assemblies you use:

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

Change `FruktGameDir` to your install. `Private="false"` stops the build from copying these files next to your
mod; they are already in the game folder.

Some APIs use types from other assemblies in `MelonLoader\Il2CppAssemblies`. Add them when the compiler asks
for them:

| Assembly | Needed for |
|----------|------------|
| `Il2CppSystem.Core.dll` | `ToManagedList()` on game collections |
| `Il2CppTripledoseLibs.dll` | The game's own events (`Listen`) |
| `UnityEngine.AudioModule.dll` | Working with `AudioSource`s yourself |
| `UnityEngine.UIModule.dll`, `UnityEngine.UI.dll`, `Unity.TextMeshPro.dll` | Building UI with `FruktUi` |
| `Il2CppZenject.dll` | Using `GameServices` containers directly |

## Declaring the dependency

In your `AssemblyInfo.cs`, next to `MelonInfo` and `MelonGame`:

```csharp
[assembly: MelonAdditionalDependencies("FruktSharedLibrary")]
```

MelonLoader then loads the library before your mod and tells players if it's missing.

## A complete example

This mod adds right-click actions, a drop-down group, a mod menu page and a settings page:

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

In game, the mod shows up under **MODS** in the mod menu with its version and author, and both pages are listed
inside its entry.

## Things to know early

- **Register in `OnInitializeMelon`.** Context-menu actions, menu pages and event handlers can be added at any
  time, but registering once at start-up keeps things simple.
- **Wait for a map.** Most gameplay calls need `GameState.InSandbox`. Event handlers such as
  `GameEvents.SandboxReady` are the easiest place to start work.
- **Don't patch tiny game methods.** If you use Harmony yourself, read [IL2CPP notes](il2cpp-notes.md) first.
  Patching an empty or trivial method in an IL2CPP game can hook hundreds of unrelated methods.
- **Check objects before using them.** Creatures and limbs can be destroyed at any moment. Use
  `creature.IsValid()` or `obj.Exists()` before touching something you stored earlier.
