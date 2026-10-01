# Core

Namespace `FruktSharedLibrary.Core`. Access to the game's services and events, plus scheduling, patching and
logging helpers.

## GameServices

FRUKT is built on Zenject dependency injection. Almost every system (pause, time scale, gravity, creatures,
spawning, the player, audio) is a service bound in a Zenject container. `GameServices` resolves them.

```csharp
using Il2CppServices.Game;

var pause = GameServices.TryGet<IPauseService>();   // null if not available right now
if (GameServices.TryGet<ITimeScaleService>(out var time))
    time.Set(0.5f);
```

| Member | Description |
|--------|-------------|
| `TryGet<T>()` | Resolves a service by interface (recommended) or concrete type. Returns `null` if it isn't bound in the current scene. |
| `TryGet<T>(out T service)` | Same, returning `false` when unavailable. |
| `Get<T>()` | Resolves or throws. |
| `Has<T>()` | True when the service can be resolved now. |
| `ProjectContainer` | The game-wide container (null before boot). |
| `SceneContainer` | The current scene's container (null while loading). |
| `Container` | The scene container, falling back to the project container. |
| `FindObject<T>(includeInactive)` / `FindObjects<T>(includeInactive)` | Finds loaded Unity objects of a type, for components that aren't bound as services. |
| `Inject(gameObject)` | Runs Zenject injection on a GameObject you created, so the game's components on it get their services. |
| `ClearCache()` | Forgets cached services. The library calls this on scene changes. |

Many services only exist inside a map (they live in the scene container). Resolve them when you need them rather
than once at start-up.

## GameEvents

Static C# events. Every handler runs inside its own try/catch, so an exception in one mod's handler doesn't stop
the others.

| Event | Raised when |
|-------|-------------|
| `MainMenuEntered` | The main menu becomes active. |
| `MapLoading(MapID)` | A map starts loading. |
| `SandboxReady(MapID)` | A map has finished loading and the player and services are ready. Most gameplay setup belongs here. |
| `SandboxExited` | The map is being left (back to the menu or reloading). |
| `SceneLoaded(string)` | A Unity scene finished loading (raw scene name). |
| `PauseChanged(bool)` | The game was paused (`true`) or unpaused. |
| `CreatureSpawned(AbstractCreature)` | A creature finished initialising. Severed body parts become creatures too. |
| `CreatureDied(AbstractCreature)` | A creature became lifeless. |
| `CreatureRemoved(AbstractCreature)` | A creature left the world. The object may already be destroyed. |
| `LimbDetached(AbstractCreature, AbstractLimb)` | A limb and everything below it came off. Arguments: the creature that now owns the part, and the part's root limb. |
| `KillAdded(int)` | The kill counter went up. Argument: the new count. |
| `FirearmFired(Firearm)` | A firearm fired a shot. |
| `Update`, `FixedUpdate`, `LateUpdate` | Every frame / physics step, after the library's own update. |

```csharp
GameEvents.CreatureDied += creature => LoggerInstance.Msg($"{creature.GetDisplayName()} died");
GameEvents.LimbDetached += (owner, part) => Notifications.Show($"{part.name} came off");
```

To subscribe to one of the game's own `IManagedEvent` events directly, see
[`Listen`](interop-and-utilities.md#game-events).

## GameState

| Member | Description |
|--------|-------------|
| `Phase` | `GamePhase.Booting`, `MainMenu`, `LoadingMap`, `Sandbox` or `Transitioning`. |
| `InMainMenu` | True while the main menu is active. |
| `InSandbox` | True while a map is loaded and playable. Most gameplay APIs need this. |
| `CurrentMap` | The loaded (or loading) map, or `null`. |
| `ActiveSceneName` | Name of the active Unity scene. |

## Scheduler

Delayed and repeating work on the main thread. Callbacks are exception-safe: a throwing callback is logged, and a
repeating one is cancelled.

| Member | Description |
|--------|-------------|
| `NextFrame(action)` | Runs on the next frame. |
| `Frames(count, action)` | Runs after a number of frames. |
| `After(seconds, action, realtime = true)` | Runs once after a delay. With `realtime` the delay ignores pause and slow motion; with `false` it follows the game's time scale. |
| `Every(seconds, action, realtime = true)` | Runs repeatedly until cancelled. |
| `RunOnMainThread(action)` | Queues work from any thread onto the main thread. Use it when async or background work needs to touch the game. |
| `StartCoroutine(IEnumerator)` / `StopCoroutine(token)` | Runs a managed coroutine. `StartCoroutine` returns the token to stop it with. |

`NextFrame`, `Frames`, `After` and `Every` return a `Scheduler.Handle` with `Cancel()` and `IsActive`.

```csharp
var handle = Scheduler.Every(5f, () => Notifications.Show($"{Creatures.Count} creatures"));
// later
handle.Cancel();
```

## Patcher

Harmony helpers that apply patches one at a time and log failures, so one broken patch (for example after a game
update) doesn't stop the rest.

| Member | Description |
|--------|-------------|
| `PatchAllSafe(harmony, assembly)` | Applies every `[HarmonyPatch]` class in the assembly individually and returns how many succeeded. Add `[assembly: HarmonyDontPatchAll]` to your mod and call this from `OnInitializeMelon`. |
| `TryPatch(harmony, original, prefix, postfix, description)` | Patches one method. Prefix and postfix are static `MethodInfo`s. |
| `TryPatch(harmony, type, methodName, parameters, prefix, postfix)` | Same, looking the method up by name. |

```csharp
[assembly: HarmonyDontPatchAll]

public override void OnInitializeMelon()
{
    Patcher.PatchAllSafe(HarmonyInstance, typeof(MyMod).Assembly);
}
```

Read [IL2CPP notes](il2cpp-notes.md) before patching game methods. Patching a small or empty method can hook many
unrelated ones.

## FruktLog and FruktConfig

`FruktLog` is the library's logger (`Msg`, `Warning`, `Error`, `Debug`). Your mod should normally log through its
own `LoggerInstance` so lines are attributed to it.

`FruktConfig` exposes the library's preferences (`DebugLogging`, `ModMenuKey`, `ShowNotifications` and the
read-only `NativeStyle`, `PauseMenuButton`, `BuiltInMenuPage`). Players change them on the mod menu's
*Library settings* page; see [Mod menu](mod-menu.md#library-settings).
