# Building and testing

## Building

You need the .NET SDK (6 or newer) and a FRUKT install with MelonLoader that has been started at least once, so
`MelonLoader/Il2CppAssemblies` exists. The game's assemblies aren't in this repository and must not be committed.

```
dotnet build
```

The project targets `net6.0` x64. The game path defaults to `D:\SteamLibrary\steamapps\common\FRUKT`; point it at
your install with:

```
dotnet build -p:FruktGameDir="C:\path\to\FRUKT"
```

or a `FruktGameDir` environment variable. Every build copies `FruktSharedLibrary.dll` and its XML docs into the
game's `Mods` folder. Add `-p:CopyToGameMods=false` to skip that.

## The self-test

Compiling proves very little in an IL2CPP game, because most problems only appear at runtime. The library contains
a self-test that drives the real game and checks every API.

1. Create `FRUKT/UserData/FruktSharedLibrary.selftest`. Put `quit` in it to close the game when the test is done.
2. Start the game. From the main menu the test loads a map, runs its checks, returns to the menu and (with `quit`)
   closes the game.
3. Read the report in `UserData/FruktSharedLibrary.selftest.log`. It ends with `Passed: N  Failed: N`.
4. **Delete the flag file**, or the test runs on every launch.

The test spawns creatures, changes the time scale and gravity, and opens menus. It puts everything back, and it
leaves your `MelonPreferences.cfg` as it found it.

### Real input

The mod menu and right-click menu checks use real mouse clicks, wheel turns and key presses. The test writes
markers such as `[SelfTest] CLICK <name> <x> <y>` to `MelonLoader/Latest.log`, and the script
`tools/selftest-watch.ps1` performs them on the game window and takes screenshots:

```
powershell -File tools\selftest-watch.ps1 -GameDir "C:\path\to\FRUKT"
```

Start it right after launching the game and leave FRUKT in the foreground. Input is only sent while the game is
the active window. Screenshots go to `tools/shots`. Without the script, the checks that need clicks fail and the
rest of the report is still valid.

### UI probe

Writing `probe quit` to the flag file runs a probe instead of the test. It dumps the layout of the game's pause and
settings screens (positions, fonts, colours) to `UserData/FruktSharedLibrary.probe.txt` and takes screenshots.
This is how the native mod menu was matched to the game's own screens.

## After a game update

1. Rebuild against the new `Il2CppAssemblies` (start the game once so MelonLoader regenerates them).
2. Fix compile errors. Renamed or removed game members show up here.
3. Run the self-test. Failed checks point at the features the update broke.
4. Watch the start of `Latest.log` for `Game hooks: N/N applied`. Fewer applied hooks means a hooked game method
   changed.

## Project layout

```
FruktSharedLibraryMod.cs   MelonLoader entry point: update loop, GUI and hooks
Core/                      services, events, game state, scheduler, safe patching, logging, preferences
Interop/                   IL2CPP casts, collection copying, game event subscriptions
Gameplay/                  World, LocalPlayer, Sounds
Entities/                  Creatures and creature/limb/organ/LVA extensions
Combat/                    Damage
Spawning/                  Spawner and firearms
UI/                        ModMenu, PauseMenu, Notifications, ContextMenus
UI/Native/                 FruktTheme, FruktUi, the native mod menu
Controls/                  keyboard and mouse input, key binds
Utilities/                 layers, textures, dev tools
Internal/                  game hooks, trackers, built-in pages, self-test (not public API)
tools/                     self-test input script
docs/                      this documentation
```
