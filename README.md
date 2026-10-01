# FruktSharedLibrary

A shared library for [FRUKT](https://store.steampowered.com/app/3880400/) mods, built on MelonLoader.

FRUKT is an IL2CPP game, which makes modding it a pain. You work with generated proxy classes, some Unity
methods are stripped out, and a few things that look fine will crash the game or quietly do nothing.
FruktSharedLibrary wraps the game's systems so your mod can call ordinary C# methods, and keeps the workarounds in
one place instead of in every mod. An in-game self-test checks all of it against the real game.

## What's in it

- The game's services and events: map loaded, creature died, limb came off, gun fired, and so on
- World and player control: time scale, gravity, pause, maps, the camera and aiming
- Creatures: spawn, find, heal, kill and push them around, detach limbs, read blood and pain
- Damage that works like the game's own weapons, and explosions
- Spawning guns, props and anything else the game has registered
- A mod menu styled like the game's settings screens, with an entry for every mod that uses the library
- Your own lines in right-click menus, including drop-down groups
- Notifications, pause-menu buttons and UI helpers that use the game's fonts and colours
- Helpers for the IL2CPP problems that trip people up

## Installing

1. Install [MelonLoader](https://melonwiki.xyz/) 0.7 or newer for FRUKT.
2. Download `FruktSharedLibrary.dll` from the
   [latest release](https://github.com/AlmostGalactic/FruktSharedLibrary/releases/latest) and put it in
   `FRUKT/Mods`.

Mods that use the library need it installed. To open the mod menu, press F8 in a map or pick MODS in the pause
menu. If you also have FruitLib, that line is called MOD MENU instead, because FruitLib already has one called
MODS.

## Using it in a mod

Reference `FruktSharedLibrary.dll`, add the dependency attribute, and call the static classes:

```csharp
[assembly: MelonAdditionalDependencies("FruktSharedLibrary")]

public class MyMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        GameEvents.SandboxReady += map => Notifications.Show($"Loaded {World.GetMapDisplayName(map)}");
        ContextMenus.AddCreatureAction("Heal", creature => creature.Heal());
        ModMenu.AddPage("My Mod")
            .Slider("Time scale", 0.05f, 1f, () => World.TimeScale, v => World.TimeScale = v);
    }
}
```

[Getting started](docs/getting-started.md) walks through the project setup and a full example mod.

## Documentation

Everything is documented in [`docs/`](docs/README.md):

- [Getting started](docs/getting-started.md)
- [Core: services, events, scheduling, patching](docs/core.md)
- [World, player and sounds](docs/world-and-player.md)
- [Creatures and damage](docs/creatures.md)
- [Spawning](docs/spawning.md)
- [Mod menu and pause menu](docs/mod-menu.md)
- [Right-click menus](docs/context-menus.md)
- [Notifications and native UI](docs/ui.md)
- [Input](docs/input.md)
- [Interop and utilities](docs/interop-and-utilities.md)
- [IL2CPP notes](docs/il2cpp-notes.md)
- [Building and testing](docs/building-and-testing.md)

## Compatibility

Tested with MelonLoader 0.7.4 on the current FRUKT build (Unity 6000.3), alongside FruitLib, AverysBoxOfFun,
StayinAlive and UnityExplorer. A game update can break parts of it; running the
[self-test](docs/building-and-testing.md) shows which.

## Changes

[CHANGELOG.md](CHANGELOG.md) lists what's in each version and what's planned.

## Contributing

Help is welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## AI disclosure

This library was made with AI assistance (Claude). It was used to decompile and analyse the game's IL2CPP code,
and it also wrote much of the library's code, tests and documentation. Everything has been run and checked in the
real game with the built-in self-test, but if you find something wrong, please
[open an issue](https://github.com/AlmostGalactic/FruktSharedLibrary/issues).

## License

[MIT](LICENSE). Made by jjlala1313.
