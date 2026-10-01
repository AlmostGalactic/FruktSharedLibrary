# World, player and sounds

Namespace `FruktSharedLibrary.Gameplay`. These go through the game's own services, so the HUD, audio and save
state stay in sync with what you change. They need a loaded map unless noted.

## World

| Member | Description |
|--------|-------------|
| `TimeScale` | Game speed (1 = normal, 0.25 = the game's slow motion). Audio pitch follows. While paused, it reads and sets the speed the game resumes at. |
| `IsPaused`, `Pause()`, `Resume()` | The pause state, as if the player pressed Esc. |
| `Gravity` | The game's `WorldGravity` value: a strength plus tilt and turn angles. Setting it applies it like the terminal does. |
| `GravityStrength` | Gravity strength in m/s² (default 9.81). Clamped to the game's range. |
| `SetGravity(strength, tiltDegrees = 0, turnDegrees = 0)` | Strength and direction at once. Tilt 0 is straight down. |
| `ResetGravity()` | Restores the default. |
| `ResetMap()` | Puts the map's props back where they started (the terminal's map reset). |
| `DeleteAllCreatures()` | Deletes every creature, alive or dead. |
| `DeleteBodies()` | Deletes dead bodies and loose parts, keeping living creatures. |
| `KillCount`, `AddKill()` | The HUD's kill counter. |
| `Maps`, `CurrentMap` | Known maps (`Il2CppData.Maps.MapID`) and the loaded one. Works in the main menu. |
| `GetMapDisplayName(map)` | The name the game shows: `MapID.Yard` is "SPIRE", `MapID.Flatland` is "HOMESTEAD". |
| `GetMapSceneName(map)` | The map's Unity scene name. |
| `LoadMap(map)` | Loads a map, from the main menu or from inside another map. |
| `ReturnToMainMenu()` | Same as "Back to menu" in the pause menu. |
| `Quit()` | Closes the game. |

```csharp
World.TimeScale = 0.1f;                    // very slow motion
World.SetGravity(9.81f, tiltDegrees: 90f); // gravity sideways
World.LoadMap(Il2CppData.Maps.MapID.Flatland);
```

## LocalPlayer

The player is a flying "god" camera with a body. Only valid in a map; check `LocalPlayer.Exists`.

| Member | Description |
|--------|-------------|
| `Position` | The player's body position. |
| `Camera`, `CameraPosition`, `CameraRotation`, `Forward` | The view. |
| `FieldOfView` | Camera field of view in degrees (get and set). |
| `AimRay` | Ray through the centre of the screen. |
| `Raycast(out hit, maxDistance = 1000, layerMask)` | Raycast along the view (ignores triggers). |
| `TryGetAimPoint(out point, maxDistance = 1000)` | The world point under the crosshair. |
| `GetPointInFront(distance = 3, snapToGround = true, heightAboveGround = 0.05)` | A good spot to put something in front of the player. |
| `RotationFacingPlayer(from)` | A rotation that faces the player from a point. Useful when spawning. |
| `Teleport(position)`, `ResetToStart()` | Move the player. |
| `SetFlightMode(GodFlightMode)` | `Flat`, `Shift` or `Free` flight (`Il2CppData.Player.GodFlightMode`). |
| `ShakeCamera(force)` | A camera shake. Values around 0.1 to 2 are sensible. |
| `HeldObject`, `ReleaseHeldObject()` | The rigidbody the player is dragging. |
| `Pin(body)`, `Unpin(body)` | Pins a rigidbody in place, like the pin tool. |
| `CaptureCursor(owner)`, `ReleaseCursor(owner)` | For your own menus: frees the mouse cursor, registers an open menu with the game (so its cursor tools don't act behind your UI) and blocks its button input, until the same owner releases it. |

`CaptureCursor` takes an `Il2CppSystem.Object` as the owner. Keep one instance per menu and pass the same one
to `ReleaseCursor`:

```csharp
private readonly Il2CppSystem.Object _menuOwner = new();

void ShowMyMenu() => LocalPlayer.CaptureCursor(_menuOwner);
void HideMyMenu() => LocalPlayer.ReleaseCursor(_menuOwner);
```

The mod menu uses this already; you only need it for UI you draw yourself.

## Sounds

Plays the game's own sound effects through its audio service, so they use the right mixer and follow slow motion.
Sound types are enums in `Il2CppInfrastructure.Project.AssetsHandlers.SFX`.

| Member | Description |
|--------|-------------|
| `Play(UISFXType, volume = 1)` | An interface sound (2D). |
| `PlayFor(UISFXType, seconds, volume = 1, fadeOut = 0.15)` | Plays the start of an interface sound and fades it out. Some UI clips are long: `WindowOpenClose` is two seconds of ticks that the game only plays during screen transitions. |
| `Play(WeaponSFXType, position, volume = 1)` | A weapon sound in 3D. |
| `Play(ImpactSFXType, position, volume = 1)` | An impact sound in 3D. |
| `Play(ToolsSFXType, position, volume = 1)` | A tool sound in 3D. |
| `Play(WhooshSFXType, position, volume = 1)` | A whoosh in 3D. |

```csharp
Sounds.Play(UISFXType.SwitchOn);
Sounds.Play(WeaponSFXType.Shoot762, LocalPlayer.Position);
```

Interface sounds: `ToolbarItemSwitch`, `HintButtonClick`, `LargeButtonClick`, `SmallButtonClick`, `SwitchOn`,
`SwitchOff`, `SliderHoldClick`, `WindowOpenClose`, `SlowMotionStart`, `SlowMotionStop`, `ItemSend`.
