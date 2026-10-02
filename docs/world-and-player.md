# World, player and sounds

These are in `FruktSharedLibrary.Gameplay`. They all go through the game's own services, so the HUD, audio and
saved settings keep up with whatever you change. Unless it says otherwise, they need a loaded map.

## World

| Member | Description |
|--------|-------------|
| `TimeScale` | Game speed. 1 is normal and 0.25 is the game's slow motion. Sound pitch follows it. While the game is paused, this is the speed it will resume at. |
| `IsPaused`, `Pause()`, `Resume()` | Pausing, same as pressing Esc. |
| `Gravity` | The game's `WorldGravity`: a strength plus tilt and turn angles. Setting it works like the terminal's gravity controls. |
| `GravityStrength` | Just the strength, in m/s² (9.81 by default). The game keeps it within its own limits. |
| `SetGravity(strength, tiltDegrees = 0, turnDegrees = 0)` | Strength and direction together. A tilt of 0 is straight down. |
| `ResetGravity()` | Back to normal gravity. |
| `ResetMap()` | Puts the map's props back where they started, like the terminal's map reset. |
| `DeleteAllCreatures()` | Deletes every creature, alive or dead. |
| `DeleteBodies()` | Deletes the dead bodies and loose parts and leaves the living alone. |
| `KillCount`, `AddKill()` | The kill counter on the HUD. |
| `Maps`, `CurrentMap` | The maps the game has (`Il2CppData.Maps.MapID`) and the one that's loaded. These work in the main menu too. |
| `GetMapDisplayName(map)` | The name the game shows. `MapID.Yard` shows as "SPIRE" and `MapID.Flatland` as "HOMESTEAD". |
| `GetMapSceneName(map)` | The map's Unity scene name. |
| `LoadMap(map)` | Loads a map, from the main menu or from another map. |
| `ReturnToMainMenu()` | Same as "Back to menu" in the pause menu. |
| `Quit()` | Closes the game. |

```csharp
World.TimeScale = 0.1f;                    // very slow motion
World.SetGravity(9.81f, tiltDegrees: 90f); // gravity sideways
World.LoadMap(Il2CppData.Maps.MapID.Flatland);
```

## LocalPlayer

The player is a flying camera with a body attached. This only works in a map; `LocalPlayer.Exists` tells you
whether there is one.

| Member | Description |
|--------|-------------|
| `Position` | Where the player's body is. |
| `Camera`, `CameraPosition`, `CameraRotation`, `Forward` | The view. |
| `FieldOfView` | The camera's field of view in degrees. You can set it too. |
| `AimRay` | A ray through the middle of the screen. |
| `Raycast(out hit, maxDistance = 1000, layerMask)` | Raycasts where the player is looking. Triggers are ignored. |
| `TryGetAimPoint(out point, maxDistance = 1000)` | The point under the crosshair. |
| `GetPointInFront(distance = 3, snapToGround = true, heightAboveGround = 0.05)` | A sensible spot in front of the player to put something. |
| `RotationFacingPlayer(from)` | A rotation pointing from somewhere towards the player. Handy for spawning things that face you. |
| `Teleport(position)`, `ResetToStart()` | Moves the player. |
| `SetFlightMode(GodFlightMode)` | `Flat`, `Shift` or `Free` flying (`Il2CppData.Player.GodFlightMode`). |
| `ShakeCamera(force)` | Shakes the camera. Somewhere between 0.1 and 2 looks reasonable. |
| `HeldObject`, `ReleaseHeldObject()` | Whatever rigidbody the player is dragging. |
| `Pin(body)`, `Unpin(body)` | Pins a rigidbody in place, like the pin tool does. |
| `CaptureCursor(owner)`, `ReleaseCursor(owner)` | For menus you draw yourself. Frees the cursor, tells the game a menu is open (so its tools don't react to clicks behind your UI), and blocks the game's keys, until the same owner releases it. |

The owner for `CaptureCursor` is an `Il2CppSystem.Object`. Make one per menu and pass the same one to
`ReleaseCursor`:

```csharp
private readonly Il2CppSystem.Object _menuOwner = new();

void ShowMyMenu() => LocalPlayer.CaptureCursor(_menuOwner);
void HideMyMenu() => LocalPlayer.ReleaseCursor(_menuOwner);
```

You don't need this for the mod menu; it already does it.

## Sounds

Plays the game's own sound effects through its audio system, so they go through the right mixer and slow down
with slow motion. The sound names are enums in `Il2CppInfrastructure.Project.AssetsHandlers.SFX`.

| Member | Description |
|--------|-------------|
| `Play(UISFXType, volume = 1)` | An interface sound, not positioned in the world. |
| `PlayFor(UISFXType, seconds, volume = 1, fadeOut = 0.15)` | Plays only the start of an interface sound, then fades it out. Some are long: `WindowOpenClose` is two seconds of ticking that the game only plays during screen transitions. |
| `Play(WeaponSFXType, position, volume = 1)` | A weapon sound at a position. |
| `Play(ImpactSFXType, position, volume = 1)` | An impact sound at a position. |
| `Play(ToolsSFXType, position, volume = 1)` | A tool sound at a position. |
| `Play(WhooshSFXType, position, volume = 1)` | A whoosh at a position. |

```csharp
Sounds.Play(UISFXType.SwitchOn);
Sounds.Play(WeaponSFXType.Shoot762, LocalPlayer.Position);
```

The interface sounds are `ToolbarItemSwitch`, `HintButtonClick`, `LargeButtonClick`, `SmallButtonClick`,
`SwitchOn`, `SwitchOff`, `SliderHoldClick`, `WindowOpenClose`, `SlowMotionStart`, `SlowMotionStop` and `ItemSend`.

### Your own sounds

`PlayClip(clip, position, volume, pitch, loop, group)` plays an `AudioClip` you loaded yourself, usually from an
[asset bundle](asset-bundles.md). It goes through the game's mixer, so the player's volume settings apply. With a
position it's 3D, without one it's 2D. It returns the `AudioSource`, so you can stop a looping sound later.
One-shot sounds clean up after themselves.

`group` picks which part of the game's mixer the sound uses:

| `SoundGroup` | For |
|--------------|-----|
| `World` | Things happening in the world. Slows down in slow motion like gunshots and impacts do. The default when you give a position. |
| `Ambient` | World sounds that keep normal speed in slow motion, like ambience or machines. |
| `Interface` | Menus and notifications. The default without a position. |

```csharp
var hum = Sounds.PlayClip(humClip, generator.transform.position, loop: true, group: SoundGroup.Ambient);
// later
UnityEngine.Object.Destroy(hum.gameObject);
```

Sounds you make in code with `AudioClip.Create` and `SetData` don't work in this game (see the
[IL2CPP notes](il2cpp-notes.md#unity-6s-span-based-methods-are-broken)). Put them in a bundle instead.
