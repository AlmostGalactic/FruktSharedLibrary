# Contributing

Thanks for wanting to help. Two things matter for any change: the API should be pleasant for other modders to
use, and it has to work in the actual game, not just compile.

## Setup

1. You need FRUKT on Steam with MelonLoader 0.7.x installed. Start the game once so MelonLoader generates
   `MelonLoader/Il2CppAssemblies`.
2. Install the .NET SDK (6 or newer).
3. Clone the repo and build:
   ```
   dotnet build -p:FruktGameDir="C:\path\to\FRUKT"
   ```
   The build copies the DLL into the game's `Mods` folder. Add `-p:CopyToGameMods=false` if you don't want that.

Never commit the game's assemblies; they belong to the game's developers. That's also why there's no CI build
yet: building needs a local copy of the game.

## Making a change

- Branch from `main` and open a pull request.
- Follow the existing style: a static class per area (`World`, `Creatures` and so on), extension methods for game
  objects, and XML docs on everything public.
- Don't let exceptions escape into the game. Wrap callbacks from mods in try/catch and log with `FruktLog`.
- Keep IL2CPP workarounds inside the library. Mod authors shouldn't have to know about them.
- If you change the public API, update the matching page in [`docs/`](docs/README.md).

### Read the IL2CPP notes first

[docs/il2cpp-notes.md](docs/il2cpp-notes.md) lists the problems we've hit so far. The ones that come up most:

- Don't Harmony-patch tiny or empty methods. IL2CPP merges identical native code, so you end up hooking unrelated
  methods as well. Put new hooks in `Internal/LibraryPatches.cs` with an `Expect<T>` guard.
- Use `ToManagedList()` instead of looping over game collections typed as interfaces.
- Watch for name clashes. If a game class has an instance method with the same name as your extension method, the
  game's method silently wins.

## Testing in the game

Most IL2CPP problems only show up at runtime, so game-facing changes need to be run in the game. Add self-test
checks for what you changed (in `Internal/SelfTest*.cs`), then:

1. Create `FRUKT/UserData/FruktSharedLibrary.selftest` with `quit` in it.
2. Start the game. The test loads a map, runs its checks and quits.
3. Read `UserData/FruktSharedLibrary.selftest.log`, then delete the flag file.

Paste the `Passed: N  Failed: N` line into your pull request, and add a screenshot if you changed any UI.

The mod menu checks click and type for real. Right after starting the game, run
`powershell -File tools\selftest-watch.ps1 -GameDir "C:\path\to\FRUKT"` and leave the game in front. The script
reads the test's instructions from `MelonLoader/Latest.log`, does the clicks and key presses, and saves
screenshots to `tools/shots`. Without it those checks fail; just mention that in the pull request.
[Building and testing](docs/building-and-testing.md) has more detail.

## Reporting bugs

Open an issue with the library version, what you did, what happened, and the relevant part of
`MelonLoader/Latest.log`. Please list your other mods too, since a lot of FRUKT mods patch the same code.
