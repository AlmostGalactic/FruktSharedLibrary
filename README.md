# FruktSharedLibrary

A shared library for [FRUKT](https://store.steampowered.com/app/3880400/) mods, built on MelonLoader.

FRUKT is an IL2CPP game, which makes modding it a pain. You work with generated proxy classes, some Unity
methods are stripped out, and a few things that look fine will crash the game or quietly do nothing.
FruktSharedLibrary wraps the game's systems so your mod can call ordinary C# methods, and keeps the workarounds in
one place instead of in every mod. An in-game self-test checks all of it against the real game.

## What's in it

- The game's services and events: map loaded, creature died, limb came off, gun fired, and so on
- World and player control: time scale, gravity, pause, maps, the camera and aiming
- Creatures: spawn, find, heal, kill and push them around, walk them somewhere, detach limbs, read blood and pain
- Damage that works like the game's own weapons, and explosions
- Spawning guns, props and anything else the game has registered, and your own 3D models from OBJ files
- The inventory and the toolbar: read and fill the slots, and add your own tools, guns and props to the terminal
- Bullets for your own guns: shots that wound like the game's, go through things, spread, and leave tracers
- Joints (welds, hinges, ropes, springs and more), and events for when objects collide, get grabbed or get shot
- Saving builds (objects and the joints between them) to files, and spawning copies of them in any map
- Asset bundles from Unity: prefabs, textures, materials and sounds, with materials switched to the game's shaders
- A mod menu styled like the game's settings screens, with an entry for every mod that uses the library and a
  switch to turn each one off, also reachable from a MODS line on the main menu
- Mods built for a newer version of the library are kept from starting, with a message saying which version
  they need, instead of crashing halfway
- Your own lines in right-click menus, including drop-down groups
- Notifications, pause-menu buttons, labels over things in the world, and UI helpers that use the game's fonts and
  colours
- Helpers for the IL2CPP problems that trip people up
- A `dotnet new` template for new mods, and reloading of bundles and models while the game runs

## Installing

1. Install [MelonLoader](https://melonwiki.xyz/) 0.7 or newer for FRUKT.
2. Download `FruktSharedLibrary.dll` from the
   [releases page](https://github.com/AlmostGalactic/FruktSharedLibrary/releases) and put it in
   `FRUKT/Mods`. That's the only file you need. The `.xml` next to it on the releases page is for modders (it
   gives code descriptions in their editor), and MelonLoader ignores it if it ends up in `Mods`.

Mods that use the library need it installed. To open the mod menu, press F8 in a map or pick MODS in the pause
menu. MODS on the main menu lists the installed mods, where you can switch them on and off. If you also have FruitLib, that line is called FSL MENU instead, because FruitLib already has one called
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

[Getting started](docs/getting-started.md) walks through the project setup and a full example mod. For a
finished mod to read, see [FSL Party](https://github.com/AlmostGalactic/FslParty), a small demo that uses most of
the library.

## Roadmap

What's coming, and the rules for what goes into each version, are in [ROADMAP.md](ROADMAP.md).

## Documentation

Everything is documented in [`docs/`](docs/README.md):

- [Getting started](docs/getting-started.md)
- [Core: services, events, scheduling, patching](docs/core.md)
- [World, player and sounds](docs/world-and-player.md)
- [Creatures and damage](docs/creatures.md)
- [Spawning](docs/spawning.md)
- [Joints, object events and builds](docs/objects.md)
- [Inventory, toolbar, tools and props](docs/inventory.md)
- [Asset bundles](docs/asset-bundles.md)
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
and it helped in some of the writing of the library's code, tests, and documentation (All of which was fully looked over and debugged). Everything has been run and checked
in the real game with the built-in self-test, but if you find something wrong, please
[open an issue](https://github.com/AlmostGalactic/FruktSharedLibrary/issues).

## License

[MIT](LICENSE). Made by jjlala1313.
