# Interop and utilities

Namespaces `FruktSharedLibrary.Interop` and `FruktSharedLibrary.Utilities`.

## IL2CPP objects

Game objects in an IL2CPP game are C# proxies around native objects. C#'s `is`, `as` and casts only see the proxy
type you happen to hold, not the object's real class. These extensions on `Il2CppObjectBase` check the real one:

| Member | Description |
|--------|-------------|
| `obj.Is<T>()` | True if the object's real class is `T` or derives from it. |
| `obj.As<T>()` | Cast to `T`, or `null` if it isn't one. |
| `obj.GetIl2CppTypeName(fullName = false)` | The real class name, such as `"Brain"`. |
| `unityObj.Exists()`, `unityObj.IsDestroyed()` | Whether a Unity object is still alive. Stored references can point at destroyed objects at any time. |

```csharp
if (organ.Is<Brain>())
    LoggerInstance.Msg("found the brain");
```

## Collections

Enumerating a game collection through one of its interfaces (`IEnumerable<T>`, `IReadOnlyList<T>` and so on) is
unreliable in this interop and can throw "Collection was modified" or return garbage. Copy it first:

| Member | Description |
|--------|-------------|
| `ToManagedList()` | Copies an IL2CPP `List<T>`, `HashSet<T>` or any interface-typed collection into a normal `List<T>`. |
| `ToIl2CppList()` | Copies a C# sequence into a new IL2CPP `List<T>`, for passing to game methods. |

```csharp
foreach (var limb in creature.LimbsHierarchyHandler.Navigator.AllLimbs.ToManagedList())
    LoggerInstance.Msg(limb.name);
```

## Components

| Member | Description |
|--------|-------------|
| `GetComponentInParentIl2Cpp<T>(includeInactive = true)` | `GetComponentInParent` that works for any IL2CPP component type. |
| `GetComponentInChildrenIl2Cpp<T>(includeInactive = true)` | The same for children. |
| `GetOrAddComponent<T>()` | The existing component, or a new one. |
| `Il2CppExtensions.TryRun(action, context)` | Runs an action and logs instead of throwing. |

## Game events

The game has its own event type, `IManagedEvent` (and `IManagedEvent<T>`), used by many services. `Listen`
subscribes a C# handler; dispose the result to unsubscribe:

```csharp
using Il2CppServices.Game;

var pause = GameServices.Get<IPauseService>();
IDisposable subscription = pause.OnChangePauseState.Listen(paused => LoggerInstance.Msg($"paused: {paused}"));
// later
subscription.Dispose();
```

Handlers run inside a try/catch. [`GameEvents`](core.md#gameevents) already covers the common cases.

## Layers

The game's physics layer masks, for raycasts and overlap queries: `Environment`, `Bullet`, `Puppet` (creature
limbs), `Puppeteer` (the animation rigs that drive creatures), and `Gameplay` (everything except the puppeteer
rigs, which is what you usually want). `Layers.IsInMask(gameObject, mask)` checks one object.

```csharp
if (Physics.Raycast(LocalPlayer.AimRay, out var hit, 100f, Layers.Gameplay))
    LoggerInstance.Msg(hit.collider.name);
```

## Textures

Unity's own image conversion methods are missing from this build, so `Textures` goes through MelonLoader's
image-conversion support instead.

| Member | Description |
|--------|-------------|
| `LoadFromFile(path, filter)` | Loads a PNG or JPG into a texture (`null` if it can't). |
| `LoadFromBytes(data, filter, name)` | Decodes PNG or JPG bytes. |
| `EncodeToPng(texture)`, `SavePng(texture, path)` | Encodes a readable texture. |
| `ToSprite(texture, pixelsPerUnit = 100)` | Wraps a texture in a sprite for UI images. |
| `Solid(color, width = 1, height = 1)` | A solid colour texture. |

## DevTools

Text dumps for finding your way around the game's objects. They write to the MelonLoader console.

| Member | Description |
|--------|-------------|
| `LogCreature(creature)` / `DescribeCreature(creature)` | Vitals, parameters, systems, limbs and organs. |
| `LogHierarchy(root, maxDepth = 6, includeComponents = true)` / `DescribeHierarchy(...)` | A GameObject tree with its components. |
| `LogPrefabIds()` | Every prefab ID the game registered (see [Spawning](spawning.md)). |

The *Library settings* page has buttons for the creature under the crosshair and for prefab IDs.
