# Contributing to FruktSharedLibrary

Thanks for helping. The library's job is to make FRUKT modding easy and safe, so changes are judged on two
things: is the API pleasant for other modders, and does it work in the real game?

## Setup

1. Own FRUKT on Steam and install MelonLoader 0.7.x. Start the game once so MelonLoader generates
   `MelonLoader/Il2CppAssemblies`.
2. Install the .NET SDK (6 or newer).
3. Clone the repo and build:
   ```
   dotnet build -p:FruktGameDir="C:\path\to\FRUKT"
   ```
   The build copies the DLL into the game's `Mods` folder. Add `-p:CopyToGameMods=false` to skip that.

The game's assemblies are not in this repo and must never be committed. They belong to the game's developers,
which is also why there is no CI build: it needs your local game install.

## Making a change

- Branch from `main` and open a pull request.
- Match the existing style: one public static class per area (`World`, `Creatures`, ...), extension methods
  for game objects, XML docs on everything public, and no exceptions thrown into the game. Wrap callbacks
  from mods in try/catch and log with `FruktLog`.
- Keep game-specific workarounds inside the library. Mod authors shouldn't need to know about IL2CPP.
- Public API changes need a line in the README's API overview.

### Before touching game types, read the IL2CPP traps

The README lists them. The ones that bite most often:

- **Don't Harmony-patch tiny or empty methods.** IL2CPP merges identical native code, so the patch hooks
  unrelated methods too. Add new hooks to `Internal/LibraryPatches.cs` with an `Expect<T>` guard.
- **Use `ToManagedList()`** instead of `foreach` over game collections typed as interfaces.
- **Check for name clashes**: a game instance method with the same name as your extension method silently wins.

## Testing in game (required for game-facing changes)

Compiling proves very little here, because most IL2CPP problems only show up at runtime. Add checks to the
self-test (`Internal/SelfTest*.cs`) for what you changed, then run it:

1. Create `FRUKT/UserData/FruktSharedLibrary.selftest` containing `quit`.
2. Start the game. The test loads a map, runs every check and quits.
3. Read `UserData/FruktSharedLibrary.selftest.log`, then **delete the flag file**.

Put the `Passed: N  Failed: 0` line in your pull request. If you changed UI, attach a screenshot.

The mod-menu checks use real mouse clicks and key presses. Right after starting the game, run
`powershell -File tools\selftest-watch.ps1 -GameDir "C:\path\to\FRUKT"` and leave the game in the foreground.
The script watches `MelonLoader/Latest.log` for `[SelfTest] CLICK/WHEEL/KEY/SCREENSHOT` lines, performs them
on the game window, and saves screenshots to `tools/shots`. Without it, those few checks fail; if so, say so in
the pull request.

## Reporting bugs

Open an issue with the library version, what you did, what happened, and the relevant part of
`MelonLoader/Latest.log`. Please also list your other installed mods, since many FRUKT mods patch the same
code.
