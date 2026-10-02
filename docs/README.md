# FruktSharedLibrary docs

| Page | What's on it |
|------|--------------|
| [Getting started](getting-started.md) | Setting up a mod project, and a full example mod |
| [Core](core.md) | `GameServices`, `GameEvents`, `GameState`, `Scheduler`, `Patcher`, `FruktLog` |
| [World, player and sounds](world-and-player.md) | `World`, `LocalPlayer`, `Sounds` |
| [Creatures and damage](creatures.md) | `Creatures`, creature/limb/organ extensions, the body simulation, `Damage` |
| [Spawning](spawning.md) | `Spawner`, `FirearmType`, firearm extensions, spawning your own models |
| [Joints and object events](objects.md) | `Joints`, `JointHandle`, `ObjectEvents` |
| [Asset bundles](asset-bundles.md) | Making bundles in Unity, `ModBundle`, `Shaders` |
| [Mod menu and pause menu](mod-menu.md) | `ModMenu`, `ModMenuPage`, settings pages, `PauseMenu`, switching mods off, version checks, the library's settings |
| [Right-click menus](context-menus.md) | `ContextMenus`, `ContextMenuGroup`, `ContextMenuContext` |
| [Notifications and native UI](ui.md) | `Notifications`, `FruktTheme`, `FruktUi`, `GuiStyles` |
| [Input](input.md) | `FruktInput`, `KeyBind` |
| [Interop and utilities](interop-and-utilities.md) | IL2CPP casts and collections, game events, `Layers`, `Textures`, `Meshes`, `DevTools` |
| [IL2CPP notes](il2cpp-notes.md) | Things that break when you use game types directly, and what to do instead |
| [Building and testing](building-and-testing.md) | Building the library, the in-game self-test, the project layout |

Everything public also has XML docs, so you get the same descriptions in IntelliSense. Keep
`FruktSharedLibrary.xml` next to the DLL you reference.

## Before you start

- It's all static classes and extension methods. There's nothing to create or set up.
- Namespaces match the folders: `FruktSharedLibrary.Core`, `.Gameplay`, `.Entities`, `.Combat`, `.Spawning`,
  `.Objects`, `.Assets`, `.UI`, `.Controls`, `.Interop` and `.Utilities`.
- Game types keep their interop names, which start with `Il2Cpp` (for example
  `Il2CppLVA.Creatures.AbstractCreature` or `Il2CppData.Maps.MapID`).
- Most gameplay calls need a loaded map. Check `GameState.InSandbox`, or start from `GameEvents.SandboxReady`.
- When something can fail at runtime, it returns `false` or `null` and logs why, instead of throwing.
- Call everything from the main thread. If you're on another thread, go through `Scheduler.RunOnMainThread`.
