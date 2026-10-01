# FruktSharedLibrary

A MelonLoader library for modding [FRUKT](https://store.steampowered.com/app/3880400/) by tripledose.

FRUKT is an IL2CPP game, so mods work against generated proxy types instead of the game's real C# code, and
several things that look like they should work crash or quietly misbehave. This library wraps the game's own
systems in a plain C# API and deals with those problems in one place, so individual mods don't have to.
Everything in it is checked against the running game by a built-in self-test.

## What it covers

- **Game services and events.** Resolve any of the game's services (pause, time scale, gravity, creatures,
  spawning, audio) and subscribe to events such as map loaded, creature died, limb detached or shot fired.
- **World and player.** Time scale, pause, gravity, map loading and resets, the kill counter, the player's
  camera, aiming raycasts, teleporting and cursor control.
- **Creatures.** Find, spawn and delete creatures; read and change blood, pain and consciousness; walk limbs and
  organs; detach limbs; reach the underlying simulation parameters.
- **Damage and spawning.** Destroy tissue the way the game's weapons do, make explosions, and spawn firearms,
  props and other registered objects through the game's own factory.
- **Mod menu.** A shared in-game menu drawn in the game's style. Each mod gets an entry with its own settings
  pages, and a MelonPreferences category can become a settings page in one call.
- **Right-click menus.** Add actions, toggles and drop-down groups to the game's context menus for bodies, props,
  firearms and more.
- **Native-looking UI.** Notifications, pause-menu lines, and builders that use the game's fonts and colours.
- **IL2CPP helpers.** Real type checks and casts, safe collection copying, destroyed-object checks, and C#
  handlers for the game's internal events.

## Installing

1. Install [MelonLoader](https://melonwiki.xyz/) 0.7 or newer for FRUKT.
2. Put `FruktSharedLibrary.dll` in `FRUKT/Mods`.

Mods built on the library need it installed. In a map, press **F8** (or pick **MODS** in the pause menu) to open
the mod menu. If FruitLib is installed too, the library's pause-menu line is called **MOD MENU** instead.

## Using it in a mod

Reference `FruktSharedLibrary.dll`, declare the dependency, and call the static APIs:

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

[Getting started](docs/getting-started.md) covers the project setup and a complete example.

## Documentation

The full documentation is in [`docs/`](docs/README.md):

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

Built and tested with MelonLoader 0.7.4 on FRUKT's Unity 6000.3 IL2CPP build. Game updates can break individual
features; the self-test shows which ones (see [Building and testing](docs/building-and-testing.md)). It runs
alongside FruitLib, AverysBoxOfFun, StayinAlive and UnityExplorer.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE). Made by jjlala1313.
