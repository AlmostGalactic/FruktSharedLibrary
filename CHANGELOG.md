# Changelog

## 0.1.0

First release. Tested with MelonLoader 0.7.4 on FRUKT's Unity 6000.3 IL2CPP build, alongside FruitLib,
AverysBoxOfFun, StayinAlive and UnityExplorer. The in-game self-test passes all 166 checks.

### For players

- A shared mod menu in the game's own style. Open it with F8 (configurable) or from the pause menu.
  - **Mods** lists every installed mod that uses the library, with its version, author and settings.
  - **Sandbox Tools**: any game speed, and heal, kill, launch or explode for the creature under the crosshair.
  - **Library settings**: the library's preferences and a few developer tools.
- Notifications from mods, drawn like the game's panels.
- Mods can add their own actions and drop-down groups to the game's right-click menus.

### For modders

- **Core:** resolve any game service; events for map loading, pause, creatures spawning, dying and being removed,
  limbs detaching, kills and gunshots; a scheduler; Harmony helpers that apply patches one at a time.
- **World and player:** time scale (also while paused), pause, gravity, map loading and resets, the kill counter,
  the player's camera, aiming raycasts, teleporting, flight mode, held and pinned objects, cursor control.
- **Sounds:** play the game's own sound effects, including a way to play just the start of long UI sounds.
- **Creatures:** find, spawn and delete creatures; read and change blood, pain, consciousness and balance; heal,
  kill, freeze, push and teleport; make them walk; limbs, organs, detaching; the simulation's parameters and
  systems.
- **Damage:** destroy tissue the way the game's weapons do, and explosions.
- **Spawning:** firearms, props and any registered object through the game's own factory; fire, auto-fire and aim
  guns.
- **Mod menu:** pages with headers, labels, buttons, toggles, sliders, choices, key bindings, separators and
  sub-pages; settings pages built from a MelonPreferences category in one call.
- **Pause menu:** add your own lines that look and animate like the game's.
- **Right-click menus:** actions, toggles that keep the menu open, and nestable drop-down groups, for bodies, props,
  firearms, the human spawner and spinners. They can be added and removed at any time.
- **Native UI:** the game's colours and fonts, builders for Unity UI that match them, and a safe way to copy the
  game's own widgets.
- **Input:** keyboard and mouse through the game's Input System, and rebindable key binds.
- **Interop:** real IL2CPP type checks and casts, safe collection copying, destroyed-object checks, and C#
  handlers for the game's internal events.
- **Utilities:** physics layers, loading and saving textures, debug dumps of creatures and object trees.
- Documentation for all of it in [`docs/`](docs/README.md), and an in-game self-test.

### Known issues

- The mod menu is only tested at 16:9 resolutions.
- Opening and closing the mod menu also plays one click from the game's hint bar, because the menu blocks the
  game's keys while it's open.
- Game updates can break individual features. The self-test shows which ones.

## Planned

Ideas for coming versions. Suggestions and help are welcome; see [CONTRIBUTING.md](CONTRIBUTING.md).

- Custom tools and items in the game's terminal and toolbar.
- Making creatures invulnerable (god mode).
- Events for limb damage and for objects being spawned.
- Knocking creatures out.
- Regrowing destroyed tissue.
- Loading asset bundles (custom models, textures and sounds).
- A simple way for mods to save their own data.
- Testing and fixes for other aspect ratios, such as ultrawide.
