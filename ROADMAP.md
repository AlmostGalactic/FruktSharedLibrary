# Roadmap

## How versions work

| Update | What it needs |
|--------|---------------|
| **0.0.1** (patch, like 0.2.1) | At least one bug fix, security fix or small feature. |
| **0.1.0** (minor, like 0.3.0) | At least five big features that make modders' lives easier. |
| **1.0.0** | Not decided yet. It's too far ahead to plan. |

Whatever the size, a release only goes out when the in-game self-test passes in full (see
[Building and testing](docs/building-and-testing.md)), and the changelog says what changed.

## 0.2.0: released

The big features. That's six, so it meets the bar of five:

- [x] **Inventory and toolbar.** Read the terminal's items and categories, and read, fill, select and empty the
  toolbar's slots, with events.
- [x] **Your own tools.** `Inventory.AddTool`: an item in the terminal that gives you the mouse buttons while the
  player holds it, with a model in the hand.
- [x] **Your own props.** `Inventory.AddProp` from a mesh or a prefab, placed with a hologram like the game's
  props.
- [x] **Automatic icons.** Tools and props without an icon get a picture of their model.
- [x] **Reloading while the game runs.** Rebuilt bundles and changed files are picked up without a restart, and
  props and tool models follow.
- [x] **A mod template.** `dotnet new fruktmod` makes a project that's ready to build into the game.

Also in it: `Shaders.FixMaterial` handles the built-in Standard shader, which drew nothing.

Release steps:

- [x] Set the version to 0.2.0 (`FruktSharedLibraryMod.Version`) and turn the changelog's Unreleased section into 0.2.0.
- [x] A full self-test run on the release build.
- [x] Upload the DLL and XML docs to the GitHub release.

## 0.2.1: released

- [x] The menu is called FSL Menu, so it can be told apart from FruitLib's.

## 0.2.x: patch candidates

Known rough edges, each enough for a patch:

- A toolbar slot that already holds a tool or prop can keep its old icon after the icon changes.
- Loading a `Sprite` from a bundle (for tool icons) isn't covered by the self-test yet.
- Bundles that depend on other bundles aren't covered by the self-test yet.
- Each bundle reload keeps the old version's assets in memory.
- Prop holograms use the library's own see-through material, not the game's hologram look.

## 0.3.0: released

The big features. That's five, so it meets the bar:

- [x] **Saving builds.** Objects and the joints between them, saved to files and spawned again in any map.
- [x] **Your own guns.** `Inventory.AddGun` with a fire rate, automatic fire and a muzzle, and `Bullets` for
  shots that wound, pierce, spread and leave tracers.
- [x] **Walking people about.** Send a person walking towards a point, turn them to face something, and find the
  nearest creature that matches a filter.
- [x] **Labels in the world.** `WorldLabels` puts text over things, like names over heads, in the game's font.
- [x] **Text boxes in the mod menu.** `TextField` rows, and string settings that players can edit in game.

Also in it: `limb.GetPosition()`, `ContextMenus.IsOpen` and `Close()`, `FruktInput.GetTypedText`, and a fix for
wounds made without a direction.

Release steps:

- [x] Set the version to 0.3.0 (`FruktSharedLibraryMod.Version`) and turn the changelog's Unreleased section into 0.3.0.
- [x] A full self-test run on the release build.
- [x] Upload the DLL and XML docs to the GitHub release.

## 0.4.0: released

Everything from 0.3.1 to 0.3.9, which went out as patches, in one update. The big features, which make more than
five:

- [x] **The game's own bullets for mod guns.** `Bullets.Launch`.
- [x] **Effects.** Cube explosions, muzzle flashes, fire, smoke and sparks.
- [x] **Flesh that grows back.** `Tissue.Regrow`.
- [x] **Flesh eaten away.** `Tissue.Dissolve`, from the outside in or from a point.
- [x] **Your own terminal tabs.** `Inventory.AddCategory`, with tabs that wrap into more columns.
- [x] **Several wounds at once.** `Damage.Apply` with a list of wounds.

Also in it: fixes for a crash after `Tissue.Regrow`, `WorldLabels` stopping, and the explosion smoke.

Release steps:

- [x] Set the version to 0.4.0 and merge the 0.3.1 to 0.3.9 changelog entries into it.
- [x] A full self-test run on the release build.
- [x] Upload the DLL and XML docs to the GitHub release.

## Ideas for later

Nothing here is promised:

- **A Unity mod kit.** A ready-made Unity project or package with a "build and copy to FRUKT" button, and a
  checker that warns about custom scripts, unsupported shaders and props without colliders before you build.
- **One-line items from a bundle.** Props and tools that take their prefab and icon from a bundle by name.
- **Self-tests for mods.** The library's in-game test tools (checks, screenshots, real clicks) opened up so mods
  can test themselves.
