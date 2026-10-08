# Changelog

## 0.3.5

Tested with MelonLoader 0.7.4 on the current FRUKT build

- `Tissue.Regrow` grows destroyed and damaged flesh back on a creature or a limb, out from what's left, over a few
  seconds. Limbs that came off stay off.

## 0.3.4

Tested with MelonLoader 0.7.4 on the current FRUKT build

- `Damage.Apply(collider, wounds)` makes several wounds on a part in one go, which is much cheaper than one call
  for each.
- `Damage` cuts wound radii down to 16 voxels (`Damage.MaxRadiusVoxels`). A sphere that size already reaches across
  a whole limb, and bigger ones could take a tenth of a second each.

## 0.3.3

Tested with MelonLoader 0.7.4 on the current FRUKT build

- `Effects` runs much faster. Its cubes are made a few at a time ahead of need instead of all at once when
  something explodes, lights are reused and only a few shine at once, and `Explosion` uses fewer, bigger cubes.

## 0.3.2

Tested with MelonLoader 0.7.4 on the current FRUKT build

- Fixed: `Effects.Explosion` smoke was nearly black and far too big up close. It is now grey and smaller.

## 0.3.1

Tested with MelonLoader 0.7.4 on the current FRUKT build

- `Bullets.Launch` flies the game's own bullets (9mm, 7.62 or a 12-gauge pellet), so a mod gun wounds people
  exactly like the game's guns do: through the body, out the other side, with blood and the same impact sound.
  `Bullets.Fire` and `Pierce` are still there for instant single wounds, and their documentation now says so.
- `Effects`: explosions, muzzle flashes, fire, smoke, sparks and debris made of cubes, with `Explosion`,
  `MuzzleFlash`, `Burst`, `Smoke`, `Flame` and `Flash`.
- Fixed: `WorldLabels` could stop working with an `InvalidCastException` after a label was shown for a while.

## 0.3.0

Tested with MelonLoader 0.7.4 on the current FRUKT build

- Your own guns: `Inventory.AddGun` adds a gun under Weapons that fires on left click (or while held, if it's
  automatic) at the rate you set, from a muzzle that follows its model in the hand.
- `Bullets`: shots along a ray that wound people like the game's guns, push what they hit and play the impact
  sound, shots that go through several things, spread, the point under the crosshair, and tracer lines.
- Walking people about: `WalkTowards`, `SetFacing`, `FaceTowards` and `GetFacing` on creatures, and
  `Creatures.GetNearest` with a filter.
- `WorldLabels`: text over things in the world, like names over heads, in the game's display font.
- Text boxes in the mod menu (`TextField`). String settings on a MelonPreferences page are now text boxes instead
  of read-only lines.
- `FruktInput.GetTypedText` gives you the characters typed this frame.
- `limb.GetPosition()` and `limb.GetMovingTransform()`: where a body part really is. A limb's own `transform` stays
  where the body was put together.
- `ContextMenus.IsOpen` and `ContextMenus.Close()`.
- Fixed: `Damage.Apply` on a limb without a direction pointed the wound from where the limb was when the body was
  put together, not from where it is. The "Pop" example in the docs had the same mistake.
- Builds: `Builds.Capture` saves objects and the joints between them, like a car, and `Builds.Spawn` puts copies in
  the world in any map. Builds go to JSON files with `Build.Save` and `Build.Load`. They hold mod props, the game's
  own props and guns, and anything a mod adds with `Builds.AddKind`, and mods can keep their own extras with each
  part and joint.
- `ModProp.CopyOf` tells you which prop a placed object came from.

## 0.2.1

Tested with MelonLoader 0.7.4 on the current FRUKT build

- The menu is now called FSL Menu: in its title, in its breadcrumb, and on the pause menu line when FruitLib is
  installed (it said "Mod menu", which didn't say whose menu it was next to FruitLib's).

## 0.2.0

Tested with MelonLoader 0.7.4 on the current FRUKT build

- `Inventory` lists the terminal's items and categories (other mods' items too), and opens and closes the terminal.
- `Toolbar` reads, fills, selects and empties the slots, gives you the object in the player's hand, and has
  events for items being added, removed and picked. It counts the slots other mods add.
- `Inventory.AddTool` adds your own item to the terminal. While the player holds it you get the mouse buttons
  (click, hold, release, wheel) and selected/put-away events. It can have a description, card rows, an icon and a
  model in the hand.
- `Inventory.AddProp` adds your own prop, made from a mesh or a prefab. The player places it like the game's
  props: a hologram shows where it will go, left click puts it there and the mouse wheel turns it.
- Mod tools and props without an icon get a picture of their model, taken once a map has loaded.
  `Thumbnails.Render` takes such pictures for anything else.
- Rebuild a bundle in Unity and see it in the running game: `ModBundle.WatchForChanges` (or `Reload`) loads the
  new version, and props and tool models made from it switch over by themselves. `FileWatch` does the same for
  any file, like an OBJ model.
- A tool's model can now be changed at any time with `WithModel`, and a prop's with `SetMesh` and `SetPrefab`.
- A `dotnet new fruktmod` template makes a ready-to-build mod project that references everything it needs and
  builds into the game's Mods folder.
- `Shaders.FixMaterial` now also replaces shaders the game has but can't draw, like the built-in Standard shader
  that `GameObject.CreatePrimitive` uses.

## 0.1.5

Tested with MelonLoader 0.7.4 on the current FRUKT build

- Load your own 3D models from OBJ files with `Meshes.LoadObj`, and put them in the world with
  `Spawner.SpawnMesh`. They're lit like the game's objects, and the player can grab, pin, throw and shoot them.
- Joints: welds, hinges (with limits, a motor or a spring), ball sockets, springs, ropes and sliders, all
  breakable, on anything with a rigidbody.
- Object events: collisions, hard hits, impact sounds, being grabbed and released, and being shot.
- Spawned meshes play impact sounds when they hit things.
- Load asset bundles made in Unity 6000.3.18f1 with `ModBundle`, from a file, from bytes or from inside your DLL.
  Prefabs spawn as grabbable props, and their materials are switched to the game's shaders so they don't show up
  pink.
- `Sounds.PlayClip` plays your own sounds through the game's mixer, as world, ambient or interface sounds.
- `ObjectEvents.Shot` now knows which gun fired more often.
- A MODS line on the main menu that lists the installed mods that use the library.
- Mods can be switched off from their entry in the mod menu. A switched-off mod doesn't start the next time the
  game runs.
- Mods that use parts of the library the installed version doesn't have aren't started. A notification when a
  map opens says which version they need, and so do their entries in the mod menu. Mods MelonLoader couldn't
  start are reported the same way.
- The library's assembly version now follows its release version, so mods record which version they were built
  against.

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
- An easy way for mods to save their own data
- Support for other aspect ratios, like ultrawide
