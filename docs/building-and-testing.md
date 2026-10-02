# Building and testing

## Building

You need the .NET SDK (6 or newer), and FRUKT with MelonLoader installed and started at least once so that
`MelonLoader/Il2CppAssemblies` exists. The game's assemblies aren't in this repo, and shouldn't ever be committed.

```
dotnet build
```

It builds for `net6.0`, x64. By default it looks for the game in `D:\SteamLibrary\steamapps\common\FRUKT`. If
yours is somewhere else, pass the path:

```
dotnet build -p:FruktGameDir="C:\path\to\FRUKT"
```

or set a `FruktGameDir` environment variable. Each build copies `FruktSharedLibrary.dll` and its XML docs into the
game's `Mods` folder. Add `-p:CopyToGameMods=false` if you don't want that.

## The self-test

In an IL2CPP game, code compiling doesn't tell you much. Most problems only show up once it's running. So the
library has a self-test that plays the actual game and checks everything.

1. Create `FRUKT/UserData/FruktSharedLibrary.selftest`. If you put `quit` in it, the game closes when the test
   finishes.
2. Start the game. From the main menu, the test loads a map, runs its checks, goes back to the menu, and quits if
   you asked it to.
3. The results are in `UserData/FruktSharedLibrary.selftest.log`. The last line is `Passed: N  Failed: N`.
4. Delete the flag file afterwards, or the test will run every time you start the game.

It spawns creatures, changes the time scale and gravity, and opens menus, then puts everything back how it was.
Your `MelonPreferences.cfg` ends up exactly as it started.

### Clicking and typing for real

The checks for the mod menu and right-click menu use real mouse clicks, scrolling and key presses. The test
writes lines like `[SelfTest] CLICK <name> <x> <y>` to `MelonLoader/Latest.log`, and `tools/selftest-watch.ps1`
does those clicks in the game window and takes screenshots:

```
powershell -File tools\selftest-watch.ps1 -GameDir "C:\path\to\FRUKT"
```

Run it right after you start the game, and leave the game in front. It only sends input while FRUKT is the active
window. Screenshots end up in `tools/shots`. If you don't run it, the checks that need clicks will fail, but the
rest of the results still count.

### The test bundle

The asset bundle checks need a real bundle, and the repo doesn't hold binary files, so you build it yourself with
Unity 6000.3.18f1:

```
powershell -File tools\build-test-bundle.ps1 -Unity "C:\path\to\6000.3.18f1\Editor\Unity.exe" -GameDir "C:\path\to\FRUKT"
```

The first run makes a URP project in your temp folder, which takes a few minutes. The script then runs
`tools/TestBundle/Editor/BuildTestBundle.cs`, which generates a texture, a sound, a material and a crate prefab,
and copies the bundle to `UserData/FruktSharedLibrary.testbundle`. Without that file the test still checks that
bad bundles are handled, and skips the rest.

### The UI probe

If the flag file says `probe quit` instead, you get a probe rather than the test. It writes out the layout of the
game's pause and settings screens (positions, fonts, colours) to `UserData/FruktSharedLibrary.probe.txt`, and
takes screenshots. That's how the mod menu was made to match the game's own screens.

## After a game update

1. Start the game once so MelonLoader regenerates `Il2CppAssemblies`, then rebuild.
2. Fix any compile errors. This is where renamed or removed game members show up.
3. Run the self-test. The failed checks tell you which features the update broke.
4. Check near the top of `Latest.log` for `Game hooks: N/N applied`. If fewer than all of them applied, one of
   the game methods the library hooks has changed.

## Where things are

```
FruktSharedLibraryMod.cs   MelonLoader entry point: update loop, GUI and hooks
Core/                      services, events, game state, scheduler, safe patching, logging, preferences
Interop/                   IL2CPP casts, collection copying, game event subscriptions
Gameplay/                  World, LocalPlayer, Sounds
Objects/                   Joints, ObjectEvents
Assets/                    asset bundles and shader fixing
Entities/                  Creatures and creature/limb/organ/LVA extensions
Combat/                    Damage
Spawning/                  Spawner and firearms
UI/                        ModMenu, PauseMenu, Notifications, ContextMenus
UI/Native/                 FruktTheme, FruktUi, the native mod menu
Controls/                  keyboard and mouse input, key binds
Utilities/                 layers, textures, meshes, dev tools
Internal/                  game hooks, trackers, built-in pages, self-test (not public API)
tools/                     self-test input script, test bundle builder
docs/                      this documentation
```
