# Interop and utilities

This page covers `FruktSharedLibrary.Interop` and `FruktSharedLibrary.Utilities`.

## IL2CPP objects

In an IL2CPP game, every game object you hold in C# is a stand-in (a proxy) for an object that really lives in
native code. C#'s own `is`, `as` and casts only look at the proxy's type, which isn't always the object's real
class. These extensions on `Il2CppObjectBase` check the real class:

| Member | Description |
|--------|-------------|
| `obj.Is<T>()` | Whether the object really is a `T` (or something derived from it). |
| `obj.As<T>()` | The object as a `T`, or `null` if it isn't one. |
| `obj.GetIl2CppTypeName(fullName = false)` | The real class name, like `"Brain"`. |
| `unityObj.Exists()`, `unityObj.IsDestroyed()` | Whether a Unity object still exists. Anything you stored a reference to can get destroyed at any time. |

```csharp
if (organ.Is<Brain>())
    LoggerInstance.Msg("found the brain");
```

## Collections

Looping over a game collection through one of its interfaces (`IEnumerable<T>`, `IReadOnlyList<T>` and so on)
doesn't work reliably here. You'll get "Collection was modified" errors or junk values. Copy it into a normal
list first:

| Member | Description |
|--------|-------------|
| `ToManagedList()` | Copies an IL2CPP `List<T>`, `HashSet<T>`, or a collection you only have as an interface, into a normal C# `List<T>`. |
| `ToIl2CppList()` | Copies C# items into a new IL2CPP `List<T>`, for passing into game methods. |

```csharp
foreach (var limb in creature.LimbsHierarchyHandler.Navigator.AllLimbs.ToManagedList())
    LoggerInstance.Msg(limb.name);
```

## Components

| Member | Description |
|--------|-------------|
| `GetComponentInParentIl2Cpp<T>(includeInactive = true)` | `GetComponentInParent`, but it works with any of the game's component types. |
| `GetComponentInChildrenIl2Cpp<T>(includeInactive = true)` | The same, looking at children. |
| `GetOrAddComponent<T>()` | Gets the component, or adds one if there isn't one. |
| `Il2CppExtensions.TryRun(action, context)` | Runs something and logs any exception instead of throwing it. |

## Game events

Many of the game's services have events of their own type, `IManagedEvent` (or `IManagedEvent<T>`). `Listen`
lets you hook a C# method up to one. Dispose what it returns when you want to stop listening:

```csharp
using Il2CppServices.Game;

var pause = GameServices.Get<IPauseService>();
IDisposable subscription = pause.OnChangePauseState.Listen(paused => LoggerInstance.Msg($"paused: {paused}"));
// later
subscription.Dispose();
```

Your handler runs inside a try/catch. For the common events you don't need this at all; they're in
[`GameEvents`](core.md#gameevents).

## Layers

The game's physics layers, for raycasts and overlap checks: `Environment`, `Bullet`, `Puppet` (creature limbs),
`Puppeteer` (the invisible rigs that animate creatures) and `Gameplay` (everything except the puppeteer rigs,
which is usually what you want). `Layers.IsInMask(gameObject, mask)` checks a single object.

```csharp
if (Physics.Raycast(LocalPlayer.AimRay, out var hit, 100f, Layers.Gameplay))
    LoggerInstance.Msg(hit.collider.name);
```

## Textures

Unity's normal image loading and saving methods aren't in this build of the game, so `Textures` uses
MelonLoader's image support instead.

| Member | Description |
|--------|-------------|
| `LoadFromFile(path, filter)` | Loads a PNG or JPG file into a texture, or returns `null` if it can't. |
| `LoadFromBytes(data, filter, name)` | The same, from bytes. |
| `EncodeToPng(texture)`, `SavePng(texture, path)` | Saves a texture as PNG. The texture has to be readable. |
| `ToSprite(texture, pixelsPerUnit = 100)` | Makes a sprite from a texture, for UI images. |
| `Solid(color, width = 1, height = 1)` | A texture that's all one colour. |

## DevTools

For finding your way around the game's objects. These print to the MelonLoader console.

| Member | Description |
|--------|-------------|
| `LogCreature(creature)` / `DescribeCreature(creature)` | A creature's vitals, parameters, systems, limbs and organs. |
| `LogHierarchy(root, maxDepth = 6, includeComponents = true)` / `DescribeHierarchy(...)` | An object and everything under it, with their components. |
| `LogPrefabIds()` | Every prefab ID the game has registered (see [Spawning](spawning.md)). |

The Library settings page in the mod menu has buttons for the first and last of these.
