# IL2CPP notes

FRUKT is compiled with IL2CPP: the game's C# was converted to native code, and MelonLoader generates proxy
assemblies so mods can call into it. Most things work as you'd expect. The problems below don't, and each one was
found while building this library against the real game. The library handles them in its own code. You only need
these notes when you work with game types directly.

## Don't Harmony-patch small or empty methods

IL2CPP merges methods whose native code is identical into one function. `SandboxState.Exit()` is empty, and so
are hundreds of other methods, all sharing one native function. Patching it hooked about 98,000 unrelated calls
in two minutes and broke them.

Patch methods that do real work, and check the instance type in your hook so calls from merged methods are
ignored. Every hook in the library does this, and logs a warning if it ever sees a call from the wrong type.

## Don't enumerate game collections through their interfaces

A `foreach` over a game collection typed as `IEnumerable<T>`, `IReadOnlyCollection<T>` or `IReadOnlyList<T>` uses
a boxed struct enumerator that the interop calls with the wrong `this`. You get "Collection was modified" or
garbage. Copy it with [`ToManagedList()`](interop-and-utilities.md#collections) first.

## Game methods hide your extension methods

C# picks an instance method over an extension method with the same name, without a warning. `AbstractLimb` has
its own `GetOrgans()` (a factory used when the limb is set up), so an extension called `GetOrgans` is silently
never called. That's why the library's version is `GetAllOrgans()`. Check for name clashes when you write
extensions for game types.

## `AbstractLimb.GetLimbNode()` creates a new node

It doesn't return the limb's live hierarchy node; it builds a fresh, unattached one. The live node is
`limb.References.Node` (or the library's `limb.GetNode()`).

## Many `ref` parameters are `out` in the interop

For example `TryGetNativeLimbByTag(tag, out limb)`. Follow what the compiler asks for.

## Abstract game methods can't be patched or overridden

There's no code behind an abstract method to hook, and you can't subclass game types from C#. For example
`ContextMenuAction.ExecuteLogic`. The library's custom right-click actions are instances of a concrete game action
whose method is intercepted only for the library's own instances.

## Objects build their right-click menu when they spawn

Not when the menu opens. Actions registered later have to be inserted into menus that already exist. The library
does this.

## Screenshots and image conversion are missing

`ScreenCapture.CaptureScreenshot(string)` and Unity's `ImageConversion` methods are stripped or broken in this
build. Use [`Textures`](interop-and-utilities.md#textures) to load and save images.

## The pause key ignores the game's input block

The game's Esc handler is marked to ignore input blocking, so blocking input isn't enough to keep Esc for your own
menu. The library intercepts it while the mod menu is open, and on the frame it closes.

## Copies of the game's UI need its services

The game's UI widgets get a "core services provider" through injection. A plain `Instantiate` produces a copy that
never animates or raises clicks. [`FruktUi.CloneGameUi`](ui.md#fruktui) copies the widget while it's inactive,
hands over the provider, then activates it.

## Some audio calls crash the game

Two things crashed FRUKT outright while debugging sounds:

- Harmony-patching the game's sound service method that takes its play settings as a `ref` struct
  (`SFXPlayerService.Play(UISFXType, ref SFXPlayParams)`).
- Reading the samples of one of the game's audio clips with `AudioClip.GetData`.

To find out which sounds play, poll active `AudioSource`s and log the ones that just started.

## Some UI sounds are long

`UISFXType.WindowOpenClose` is a two-second run of ticks. The game only plays it during its screen transitions and
fades it out. Played in full, it sounds like the same click repeating. Use another sound, or
[`Sounds.PlayFor`](world-and-player.md#sounds) to play just the start.
