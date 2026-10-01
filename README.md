# FruktSharedLibrary

A shared MelonLoader library for modding **FRUKT** (tripledose). FRUKT is an IL2CPP game, which makes modding
painful: no C# source, proxy types instead of real ones, stripped Unity methods, and a few traps that crash
or silently misbehave. This library wraps the game's own systems behind a plain C# API and was verified
against the live game by an automated in-game self-test (115/115 checks).

- **Author:** jjlala1313
- **Game:** FRUKT by tripledose (Unity 6000.3, IL2CPP)
- **Loader:** MelonLoader 0.7.x (.NET 6 / IL2CPP)

---

## For players

Copy `FruktSharedLibrary.dll` into `FRUKT/Mods`. Mods that depend on it need it installed.

In a map, press **F8** to open the mod menu. It includes a *Sandbox Tools* page with time scale, gravity,
spawning, and actions for the creature under your crosshair. The key and the built-in page can be changed in
`UserData/MelonPreferences.cfg` under `[FruktSharedLibrary]`:

| Preference          | Default | Meaning                                   |
|---------------------|---------|-------------------------------------------|
| `ModMenuKey`        | `F8`    | Key that toggles the menu (`Ctrl+M` etc.) |
| `ShowNotifications` | `true`  | On-screen notifications from mods         |
| `BuiltInMenuPage`   | `true`  | The library's own *Sandbox Tools* page    |
| `DebugLogging`      | `false` | Extra diagnostic lines in the console     |

---

## For modders

### 1. Reference the library

In your mod's `.csproj` (SDK style, `net6.0`):

```xml
<PropertyGroup>
  <TargetFramework>net6.0</TargetFramework>
  <FruktGameDir>D:\SteamLibrary\steamapps\common\FRUKT</FruktGameDir>
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
</ItemGroup>
```

Keep `FruktSharedLibrary.xml` next to the DLL (the build copies it into `Mods`) to get IntelliSense docs.

In your `AssemblyInfo.cs`, make MelonLoader load the library first:

```csharp
[assembly: MelonAdditionalDependencies("FruktSharedLibrary")]
```

### 2. A complete example mod

```csharp
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Spawning;
using FruktSharedLibrary.UI;
using Il2CppLVA.NodesHierarchy.Benchmark.Variants; // HumanoidNodeTagValue
using MelonLoader;
using UnityEngine;

public class ExampleMod : MelonMod
{
    private bool _lowGravity;

    public override void OnInitializeMelon()
    {
        // React to the game
        GameEvents.SandboxReady += map => Notifications.Show($"Welcome to {World.GetMapDisplayName(map)}");
        GameEvents.CreatureDied += creature => LoggerInstance.Msg($"{creature.GetDisplayName()} died");

        // Right-click menu entries (shown on every body)
        ContextMenus.AddCreatureAction("Heal", creature => creature.Heal());
        ContextMenus.AddCreatureAction("Launch", creature => creature.AddForce(Vector3.up * 800f));
        ContextMenus.AddLimbAction("Pop", limb => limb.Damage(limb.transform.position, 6));

        // A page in the shared mod menu (F8)
        ModMenu.AddPage("Example")
            .Header("Fun")
            .Toggle("Moon gravity", () => _lowGravity, on =>
            {
                _lowGravity = on;
                World.GravityStrength = on ? 1.62f : 9.81f;
            })
            .Button("Spawn a human", () => Creatures.SpawnHumanInFront())
            .Button("Spawn a shotgun", () => Spawner.SpawnFirearmInFront(FirearmType.Grist03))
            .Button("Behead the closest human", () =>
            {
                var target = Creatures.GetNearest(LocalPlayer.Position, livingOnly: true);
                target?.GetLimb(HumanoidNodeTagValue.Head)?.Detach();
            });
    }
}
```

---

## API overview

All APIs are static classes or extension methods. Gameplay APIs need a loaded map: check
`GameState.InSandbox`, or wait for `GameEvents.SandboxReady`.

### `FruktSharedLibrary.Core`
| Type | What it does |
|------|--------------|
| `GameServices` | Resolves any game service from the Zenject containers: `GameServices.TryGet<IPauseService>()`. Also `FindObject<T>()`/`FindObjects<T>()` and `Inject(gameObject)`. |
| `GameEvents` | `MainMenuEntered`, `MapLoading`, `SandboxReady`, `SandboxExited`, `SceneLoaded`, `PauseChanged`, `CreatureSpawned`, `CreatureDied`, `CreatureRemoved`, `LimbDetached`, `KillAdded`, `FirearmFired`, `Update`, `FixedUpdate`, `LateUpdate`. Each handler is isolated, so one throwing handler doesn't stop the others. |
| `GameState` | `Phase`, `InMainMenu`, `InSandbox`, `CurrentMap`, `ActiveSceneName`. |
| `Scheduler` | `NextFrame`, `Frames`, `After`, `Every` (cancellable handles), `RunOnMainThread` (thread-safe), `StartCoroutine`. |
| `Patcher` | Harmony helpers that apply patches one by one and log failures: `PatchAllSafe(harmony, assembly)`, `TryPatch(...)`. |
| `FruktLog`, `FruktConfig` | The library's logger and preferences. |

### `FruktSharedLibrary.Gameplay`
| Type | What it does |
|------|--------------|
| `World` | `TimeScale`, `Pause()`/`Resume()`/`IsPaused`, `Gravity`/`GravityStrength`/`SetGravity`/`ResetGravity`, `ResetMap`, `DeleteAllCreatures`, `DeleteBodies`, `KillCount`, `Maps`, `GetMapDisplayName`, `LoadMap`, `ReturnToMainMenu`, `Quit`. |
| `LocalPlayer` | `Position`, `Camera`, `CameraPosition`/`CameraRotation`/`Forward`, `AimRay`, `Raycast`, `TryGetAimPoint`, `GetPointInFront`, `Teleport`, `ResetToStart`, `FieldOfView`, `SetFlightMode`, `ShakeCamera`, `HeldObject`, `Pin`/`Unpin`, `CaptureCursor`/`ReleaseCursor` (for your own menus). |
| `Sounds` | Plays the game's own SFX: `Sounds.Play(UISFXType.SwitchOn)`, `Sounds.Play(WeaponSFXType.Shoot762, position)`. |

### `FruktSharedLibrary.Entities`
| Type | What it does |
|------|--------------|
| `Creatures` | `All`, `Humans`, `Living`, `Count`, `GetNearest`, `FromCollider`/`FromGameObject`, `LimbFromCollider`, `GetAimedCreature`/`GetAimedLimb`, `SpawnHuman(pos, rot, onSpawned)`, `SpawnHumanInFront`, `DeleteAll`, `DeleteBodies`. |
| `CreatureExtensions` | `IsLiving`/`IsDead`/`IsValid`/`IsHuman`, `GetDisplayName`, `GetLimbs`, `GetLimb(HumanoidNodeTagValue)`, `GetRootLimb`, `GetPosition`, `GetPain`/`GetCognition`/`GetBalance`, `GetBlood`/`SetBlood`/`RefillBlood`/`DrainBlood`, `StopBleeding`, `Heal`, `Kill`, `Delete`, `AddForce`/`AddExplosionForce`, `SetFrozen`, `TeleportTo`, `SetWalking`/`IsWalking`, `GetPuppeteer`. |
| `LimbExtensions` | `GetCreature`, `GetRigidbody`, `GetVoxelMesh`, `GetAllOrgans`/`GetOrgan<T>`, `GetHumanPart`, `GetParentLimb`/`GetChildLimbs`, `GetWholeness`, `GetBleedingWoundCount`/`StopBleeding`/`AddBleeding`, `Detach`, `Delete`, `AddForce`, `Damage`. |
| `OrganExtensions` | `GetOrganName`, `GetLimb`, `GetCreature`, `GetIntegrity`, `GetEfficiency`. |
| `LvaExtensions` | The simulation layer: `GetParameter<T>()`/`GetParameterValue<T>()` (e.g. `CreaturePain`, `LimbWholeness`), `GetAllParameters()`, `ForceValue`, `GetSystem<T>()` (e.g. `BloodTank`, `BloodSystem`), `GetAllSystems()`. |

### `FruktSharedLibrary.Combat`
`Damage.Apply(limb | collider | raycastHit, point, radiusVoxels, strength)` destroys tissue the way the game's
weapons do (organs, pain, bleeding and dismemberment all react). `Damage.Explosion(center, radius, force)`
does the same for everything in range and pushes rigidbodies.

### `FruktSharedLibrary.Spawning`
`Spawner.Spawn(prefabId, pos)`, `SpawnFirearm(FirearmType, pos)`, `SpawnProp(name, pos)`, the `...InFront`
variants, `GetRegisteredPrefabIds()`, `GetPrefabIds<T>()`, `GetPropNames()`, `GetSpawnedObjects()`,
`GetFirearms()`, `Despawn`. `FirearmExtensions`: `Fire`, `SetAutoFire`, `IsAutoFiring`, `AimAt`, `GetRigidbody`.

### `FruktSharedLibrary.UI`
| Type | What it does |
|------|--------------|
| `ContextMenus` | Adds actions to the game's own right-click menus: `AddCreatureAction`, `AddLimbAction`, `AddAction(ContextMenuTarget, ...)` for props, firearms, the human spawner and spinners, and `AddToggle` (label shows ON/OFF). Actions can be added or removed at any time, including for objects that already exist. Priority: higher numbers are listed first (built-in actions use 995-1000). |
| `ModMenu` | The shared F8 menu. `AddPage(title)` returns a builder with `Header`, `Label`, `Button`, `Toggle`, `Slider`, `Separator`, `OnlyWhen`. |
| `Notifications` | `Show(text, seconds)`, `Warn(text)`. |
| `GuiStyles` | The library's IMGUI styles, if you draw your own overlay. |

### `FruktSharedLibrary.Controls`
`FruktInput.GetKeyDown(Key.F9)`, `GetKey`, `GetMouseButtonDown`, `MousePosition`, `ScrollDelta` use the game's
Input System. The old `UnityEngine.Input` may be disabled in this game. `KeyBind.Parse("Ctrl+M", Key.M).WasPressed()`
handles configurable hotkeys.

### `FruktSharedLibrary.Interop`
`obj.Is<T>()` / `obj.As<T>()` are real IL2CPP type checks and casts. `unityObj.Exists()` checks that an object
hasn't been destroyed. `ToManagedList()` safely copies any game collection. `GetComponentInParentIl2Cpp<T>()`
and `GetComponentInChildrenIl2Cpp<T>()` find components. `gameEvent.Listen(handler)` subscribes C# code to the
game's `IManagedEvent`s (dispose the result to unsubscribe).

### `FruktSharedLibrary.Utilities`
`Layers` (the game's physics layer masks), `Textures` (load PNG/JPG files into textures or sprites, encode PNG),
`DevTools` (`LogCreature`, `LogHierarchy`, `LogPrefabIds`).

---

## IL2CPP traps this library handles for you

These were all hit while building and testing against the real game. Keep them in mind when you go beyond the
library and touch game types directly.

1. **Never Harmony-patch tiny or empty methods.** IL2CPP merges identical native code. `SandboxState.Exit()`
   is empty and shares one native function with hundreds of other empty methods, so patching it hooked
   about 98,000 unrelated calls in two minutes and broke them. The library's own hooks check the instance type and
   report any merged-method calls (zero in testing).
2. **Don't enumerate game collections through their interfaces.** `foreach` over an `IEnumerable<T>` /
   `IReadOnlyCollection<T>` uses a boxed struct enumerator that Il2CppInterop calls with the wrong `this`. You get
   "Collection was modified" or garbage. Use `ToManagedList()`.
3. **Game instance methods hide your extension methods.** `AbstractLimb` has its own `GetOrgans()` (an
   init-time factory), so an extension with that name is silently never called. That's why this library uses
   `GetAllOrgans()`.
4. **`AbstractLimb.GetLimbNode()` builds a new, unattached node.** The live hierarchy node is
   `limb.References.Node`.
5. **Many game `ref` parameters are `out` in the interop**, e.g. `TryGetNativeLimbByTag(tag, out limb)`.
6. **Abstract game methods can't be patched or overridden** (for example `ContextMenuAction.ExecuteLogic`).
   Custom context actions therefore ride on a concrete game action whose method is intercepted only for the
   library's own instances.
7. **Objects build their context menu when they spawn**, not when it opens. The library injects later-registered
   actions into menus that already exist.
8. **`ScreenCapture.CaptureScreenshot(string)` and Unity's `ImageConversion` are broken or stripped.** Use
   `Textures` for images.

---

## Building

Requires the .NET SDK (6 or newer). The project targets `net6.0` x64 and references the interop assemblies
MelonLoader generated in your game folder.

```
dotnet build
```

The game path defaults to `D:\SteamLibrary\steamapps\common\FRUKT`. Override it with
`dotnet build -p:FruktGameDir="C:\path\to\FRUKT"` or an `FruktGameDir` environment variable. Each build copies
the DLL and XML docs into the game's `Mods` folder (skip with `-p:CopyToGameMods=false`).

## Verifying after a game update

The library contains an in-game self-test. Create the file `FRUKT/UserData/FruktSharedLibrary.selftest`
(write `quit` in it to close the game when finished), then start the game. It loads the first map, exercises
every API, and writes a PASS/FAIL report to `UserData/FruktSharedLibrary.selftest.log`. **Delete the file
afterwards**, or the test will run on every launch.

## Project layout

```
FruktSharedLibraryMod.cs   MelonLoader entry point (wires the update loop, GUI and hooks)
Core/                      services (Zenject), events, game state, scheduler, safe patching, logging, config
Interop/                   IL2CPP casting, collection and event helpers
Gameplay/                  World, LocalPlayer, Sounds
Entities/                  Creatures and creature/limb/organ/LVA extension methods
Combat/                    Damage
Spawning/                  Spawner, firearms
UI/                        ModMenu, Notifications, ContextMenus, styles
Controls/                  keyboard/mouse input, key binds
Utilities/                 layers, textures, dev tools
Internal/                  hooks, trackers, built-in menu page, self-test (not public API)
```
