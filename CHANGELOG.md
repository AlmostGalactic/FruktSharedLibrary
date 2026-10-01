# Changelog

## 0.1.0

The first release. Tested with MelonLoader 0.7.4 on the current FRUKT build (Unity 6000.3), with FruitLib,
AverysBoxOfFun, StayinAlive and UnityExplorer installed. All 166 self-test checks pass.

### For players

- A mod menu that looks like the game's own settings screens. Open it with F8 (you can change the key) or from
  the pause menu. It has three sections:
  - Mods: every installed mod that uses the library, with its version, author and settings
  - Sandbox Tools: set the game to any speed, and heal, kill, launch or blow up whoever is under your crosshair
  - Library settings: the library's own options and a couple of debug buttons
- Notifications from mods, styled like the game's panels.
- Mods can add their own lines and drop-down groups to the right-click menu.

### For modders

Static classes and extension methods for:

- Game services, and events for maps loading, pausing, creatures spawning and dying, limbs coming off, kills
  and gunshots
- Time scale (including while paused), gravity, pausing, maps and map resets, and the kill counter
- The player: camera, aiming raycasts, teleporting, flight mode, held and pinned objects, the cursor
- Playing the game's sounds, including just the start of a long UI sound
- Creatures: finding, spawning and deleting them, blood, pain and consciousness, healing and killing, forces,
  walking, limbs and organs, and the simulation's raw parameters
- Tissue damage that works like the game's weapons, and explosions
- Spawning guns, props and other registered objects, and firing guns
- Mod menu pages (headers, labels, buttons, toggles, sliders, choices, key bindings, sub-pages), and settings
  pages made straight from a MelonPreferences category
- Your own pause-menu lines that look like the game's
- Right-click menu actions, toggles and nested drop-down groups for bodies, props, guns, the human spawner and the
  spinner
- The game's colours and fonts, UI builders that match them, and a safe way to copy the game's own UI
- Keyboard and mouse input, and rebindable keys
- IL2CPP helpers: real type checks and casts, collection copying, checking for destroyed objects, and listening
  to the game's internal events
- Physics layers, loading and saving images, and debug dumps

Plus documentation in [`docs/`](docs/README.md) and an in-game self-test.

### Known issues

- The mod menu has only been tested at 16:9.
- Opening or closing the mod menu also plays a click from the game's hint bar. The menu blocks the game's keys
  while it's open, and the hint bar reacts to that.
- A game update can break parts of the library. Run the self-test to find out which.

## Planned

Things I'd like to add. Suggestions and pull requests are welcome; see [CONTRIBUTING.md](CONTRIBUTING.md).

- Custom tools and items in the terminal and toolbar
- Invulnerable creatures (god mode)
- Events for limb damage and for spawned objects
- Knocking creatures out
- Regrowing destroyed tissue
- Loading asset bundles for custom models, textures and sounds
- An easy way for mods to save their own data
- Support for other aspect ratios, like ultrawide
