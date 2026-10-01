# Core

`FruktSharedLibrary.Core` gets you at the game's services and events, and has helpers for scheduling, patching
and logging.

## GameServices

FRUKT uses Zenject for dependency injection, so nearly every system in the game (pause, time scale, gravity,
creatures, spawning, the player, audio) is a service sitting in a Zenject container. `GameServices` fetches them
for you.

```csharp
using Il2CppServices.Game;

var pause = GameServices.TryGet<IPauseService>();   // null if not available right now
if (GameServices.TryGet<ITimeScaleService>(out var time))
    time.Set(0.5f);
```

| Member | Description |
|--------|-------------|
| `TryGet<T>()` | Gets a service by its interface (best) or its class. Returns `null` if the current scene doesn't have it. |
| `TryGet<T>(out T service)` | The same, but returns `false` instead. |
| `Get<T>()` | Gets it or throws. |
| `Has<T>()` | Whether you can get it right now. |
| `ProjectContainer` | The container that lives for the whole session (null before the game has booted). |
| `SceneContainer` | The current scene's container (null while a scene loads). |
| `Container` | The scene container if there is one, otherwise the project container. |
| `FindObject<T>(includeInactive)` / `FindObjects<T>(includeInactive)` | Finds loaded Unity objects, for components that aren't services. |
| `Inject(gameObject)` | Runs Zenject injection on an object you made, so the game's components on it get their services. |
| `ClearCache()` | Forgets cached services. The library does this itself when the scene changes. |

A lot of services only exist while a map is loaded, so look them up when you need them instead of once at
start-up.

## GameEvents

Plain C# events. Each handler runs in its own try/catch, so one mod throwing doesn't stop the others from getting
the event.

| Event | When |
|-------|------|
| `MainMenuEntered` | The main menu opens. |
| `MapLoading(MapID)` | A map starts loading. |
| `SandboxReady(MapID)` | A map has loaded and the player and services are ready. Start most of your gameplay stuff here. |
| `SandboxExited` | The player leaves the map (back to the menu, or reloading). |
| `SceneLoaded(string)` | A Unity scene finished loading. You get the raw scene name. |
| `PauseChanged(bool)` | The game was paused (`true`) or unpaused (`false`). |
| `CreatureSpawned(AbstractCreature)` | A creature is fully set up. Cut-off body parts count as creatures too. |
| `CreatureDied(AbstractCreature)` | A creature died. |
| `CreatureRemoved(AbstractCreature)` | A creature left the world. It may already be destroyed. |
| `LimbDetached(AbstractCreature, AbstractLimb)` | A limb came off, along with everything attached below it. You get the creature that now owns the piece, and the piece's root limb. |
| `KillAdded(int)` | The kill counter went up. You get the new count. |
| `FirearmFired(Firearm)` | A gun fired. |
| `Update`, `FixedUpdate`, `LateUpdate` | Every frame or physics step, after the library's own update. |

```csharp
GameEvents.CreatureDied += creature => LoggerInstance.Msg($"{creature.GetDisplayName()} died");
GameEvents.LimbDetached += (owner, part) => Notifications.Show($"{part.name} came off");
```

If you want one of the game's own `IManagedEvent`s directly, use [`Listen`](interop-and-utilities.md#game-events).

## GameState

| Member | Description |
|--------|-------------|
| `Phase` | Where the game is: `GamePhase.Booting`, `MainMenu`, `LoadingMap`, `Sandbox` or `Transitioning`. |
| `InMainMenu` | True in the main menu. |
| `InSandbox` | True while a map is loaded and playable. Most gameplay calls need this. |
| `CurrentMap` | The map that's loaded or loading, or `null`. |
| `ActiveSceneName` | The active Unity scene's name. |

## Scheduler

Runs things later, or on repeat, on the main thread. If a callback throws, it gets logged, and a repeating one is
stopped.

| Member | Description |
|--------|-------------|
| `NextFrame(action)` | Next frame. |
| `Frames(count, action)` | After that many frames. |
| `After(seconds, action, realtime = true)` | Once, after a delay. By default the delay is in real time, so pausing and slow motion don't affect it. Pass `false` to follow the game's time scale. |
| `Every(seconds, action, realtime = true)` | Over and over until you cancel it. |
| `RunOnMainThread(action)` | Safe to call from any thread. Use it when background work needs to touch the game. |
| `StartCoroutine(IEnumerator)` / `StopCoroutine(token)` | Runs a coroutine. Keep what `StartCoroutine` returns if you want to stop it. |

`NextFrame`, `Frames`, `After` and `Every` give you a `Scheduler.Handle` with `Cancel()` and `IsActive`.

```csharp
var handle = Scheduler.Every(5f, () => Notifications.Show($"{Creatures.Count} creatures"));
// later
handle.Cancel();
```

## Patcher

Harmony helpers that apply your patches one by one and log the ones that fail, so a single broken patch (say,
after a game update) doesn't take the rest down with it.

| Member | Description |
|--------|-------------|
| `PatchAllSafe(harmony, assembly)` | Applies each `[HarmonyPatch]` class in the assembly separately and returns how many worked. Add `[assembly: HarmonyDontPatchAll]` and call this from `OnInitializeMelon`. |
| `TryPatch(harmony, original, prefix, postfix, description)` | Patches one method. Prefix and postfix are static `MethodInfo`s. |
| `TryPatch(harmony, type, methodName, parameters, prefix, postfix)` | The same, finding the method by name. |

```csharp
[assembly: HarmonyDontPatchAll]

public override void OnInitializeMelon()
{
    Patcher.PatchAllSafe(HarmonyInstance, typeof(MyMod).Assembly);
}
```

Read the [IL2CPP notes](il2cpp-notes.md) before you patch game methods. Patching a small or empty one can hook a
lot of unrelated ones too.

## FruktLog and FruktConfig

`FruktLog` is the library's logger (`Msg`, `Warning`, `Error`, `Debug`). In your own mod, log through your
`LoggerInstance` instead so the lines show your mod's name.

`FruktConfig` holds the library's preferences: `DebugLogging`, `ModMenuKey` and `ShowNotifications`, plus the
read-only `NativeStyle`, `PauseMenuButton` and `BuiltInMenuPage`. Players change them on the
[Library settings](mod-menu.md#library-settings) page of the mod menu.
