# IL2CPP notes

FRUKT is built with IL2CPP, which means the game's C# code was turned into native code before it shipped.
MelonLoader generates stand-in assemblies so mods can still call into it. Most of the time that works fine.
These are the times it didn't, all found while building this library against the real game.

The library already works around every one of these. You only need to know about them if you use game types
directly.

## Don't Harmony-patch small or empty methods

When two methods compile to exactly the same native code, IL2CPP keeps just one copy and points both at it.
`SandboxState.Exit()` is empty, and so are hundreds of other methods, so they all share a single function.
Patching it hooked about 98,000 unrelated calls in two minutes, and broke them.

Patch methods that actually do something, and check the instance type inside your hook so you can ignore calls
that come from the wrong class. The library's own hooks all do this, and log a warning if one ever gets called on
the wrong type.

## Don't loop over game collections through their interfaces

If a game collection is typed as `IEnumerable<T>`, `IReadOnlyCollection<T>` or `IReadOnlyList<T>` and you
`foreach` over it, the interop gets the enumerator wrong. You'll see "Collection was modified" errors or junk
values. Copy it with [`ToManagedList()`](interop-and-utilities.md#collections) first.

## Game methods win over your extension methods

If a game class has a method with the same name as your extension method, C# calls the game's method and doesn't
warn you. `AbstractLimb` has its own `GetOrgans()`, which is a setup method, so an extension called `GetOrgans`
just never runs. That's why the library's one is named `GetAllOrgans()`. Watch for this whenever you write
extensions for game classes.

## `AbstractLimb.GetLimbNode()` makes a new node

You'd expect it to return the limb's node in the body's hierarchy. It doesn't; it creates a new one that isn't
attached to anything. The real node is `limb.References.Node`, or use the library's `limb.GetNode()`.

## A lot of `ref` parameters are `out` in the interop

For example `TryGetNativeLimbByTag(tag, out limb)`. Just do what the compiler tells you.

## You can't patch or override abstract game methods

An abstract method has no code to hook, and you can't make subclasses of game classes from C#.
`ContextMenuAction.ExecuteLogic` is one of these. To get custom right-click actions anyway, the library makes
instances of one of the game's real actions and intercepts that action's method, but only for its own instances.

## Right-click menus are built when things spawn

Not when you open them. So if you add an action after something has spawned, it has to be put into that object's
existing menu by hand. The library does that for you.

## Screenshots and image conversion don't work

`ScreenCapture.CaptureScreenshot(string)` and Unity's `ImageConversion` methods are missing or broken in this
build. Use [`Textures`](interop-and-utilities.md#textures) to load and save images.

## Blocking input doesn't block the pause key

The game's Esc handler ignores the game's own input blocking, so blocking input won't stop Esc from opening the
pause menu over yours. The library catches Esc itself while the mod menu is open, including on the frame it
closes.

## Copies of the game's UI need its services

The game's UI pieces are given a "core services provider" when they're created. If you copy one with
`Instantiate`, the copy doesn't get it, so it never animates or responds to clicks.
[`FruktUi.CloneGameUi`](ui.md#fruktui) makes the copy while it's switched off, gives it the provider, and then
switches it on.

## Parts of Unity are missing from the build

Unity leaves out whatever a game doesn't use when it builds it. FRUKT's build has no `SpringJoint` at all, and
`HingeJoint` is there but without its limits, motor or spring. Code that uses them compiles fine and then fails
in the game. `ConfigurableJoint` can do everything those can, which is why every joint in `Joints` is one.

## Bullets are physical objects

The game's bullets are real rigidbodies that fly and collide, not raycasts. So a shot hitting something arrives
as an ordinary collision with a `Bullet`. That's how `ObjectEvents.Shot` works.

## Your own components need registering first

To get Unity messages like `OnCollisionEnter`, the component has to be a class the game's IL2CPP runtime knows
about. Il2CppInterop's `ClassInjector.RegisterTypeInIl2Cpp<T>()` registers a C# `MonoBehaviour` subclass at
runtime; it needs a constructor that takes an `IntPtr`. Members it can't translate, such as properties of your
own managed types, are skipped with a warning in the log, which is harmless. The library does this for its
collision events.

## Some audio calls crash the game

Two things crashed FRUKT straight to desktop while tracking down a sound bug:

- Harmony-patching the game's sound method that takes its settings as a `ref` struct
  (`SFXPlayerService.Play(UISFXType, ref SFXPlayParams)`)
- Reading a game audio clip's samples with `AudioClip.GetData`

If you need to know which sounds are playing, check the active `AudioSource`s every frame and look for ones that
just started.

## Some UI sounds are long

`UISFXType.WindowOpenClose` is two seconds of ticking. The game only plays it during its screen transitions and
fades it out quickly, but played in full it sounds like the same click going off over and over. Pick a different
sound, or use [`Sounds.PlayFor`](world-and-player.md#sounds) to play just the start of it.
