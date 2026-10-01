# FruktSharedLibrary documentation

| Page | Covers |
|------|--------|
| [Getting started](getting-started.md) | Project setup, the dependency attribute, a complete example mod |
| [Core](core.md) | `GameServices`, `GameEvents`, `GameState`, `Scheduler`, `Patcher`, `FruktLog` |
| [World, player and sounds](world-and-player.md) | `World`, `LocalPlayer`, `Sounds` |
| [Creatures and damage](creatures.md) | `Creatures`, creature/limb/organ extensions, the LVA simulation, `Damage` |
| [Spawning](spawning.md) | `Spawner`, `FirearmType`, firearm extensions |
| [Mod menu and pause menu](mod-menu.md) | `ModMenu`, `ModMenuPage`, settings pages from preferences, `PauseMenu`, the library's own settings |
| [Right-click menus](context-menus.md) | `ContextMenus`, `ContextMenuGroup`, `ContextMenuContext` |
| [Notifications and native UI](ui.md) | `Notifications`, `FruktTheme`, `FruktUi`, `GuiStyles` |
| [Input](input.md) | `FruktInput`, `KeyBind` |
| [Interop and utilities](interop-and-utilities.md) | IL2CPP casts and collections, game event subscriptions, `Layers`, `Textures`, `DevTools` |
| [IL2CPP notes](il2cpp-notes.md) | Problems you'll hit when touching game types directly, and how to avoid them |
| [Building and testing](building-and-testing.md) | Building the library, the in-game self-test, project layout |

Every public member also has XML documentation, so IntelliSense shows the same descriptions while you code. Keep
`FruktSharedLibrary.xml` next to the DLL you reference.

## Conventions

- Everything is a static class or an extension method. There is nothing to construct or initialise.
- Namespaces follow the folders: `FruktSharedLibrary.Core`, `.Gameplay`, `.Entities`, `.Combat`, `.Spawning`,
  `.UI`, `.Controls`, `.Interop`, `.Utilities`.
- Game types keep their interop names, which start with `Il2Cpp` (`Il2CppLVA.Creatures.AbstractCreature`,
  `Il2CppData.Maps.MapID` and so on).
- Most gameplay calls need a loaded map. Check `GameState.InSandbox`, or do the work from
  `GameEvents.SandboxReady`.
- Methods that can fail at runtime return `bool` or `null` and log the reason instead of throwing.
- Call everything from the main thread. From other threads, use `Scheduler.RunOnMainThread`.
