# Spawning

`FruktSharedLibrary.Spawning` spawns things through the game's own factory. That means they get set up and
registered with the map like anything the player spawns, and "reset map" clears them away as usual. For humans,
use [`Creatures.SpawnHuman`](creatures.md#finding-and-spawning-creatures) instead.

## Spawner

| Member | Description |
|--------|-------------|
| `SpawnFirearm(FirearmType, position, rotation = null)` | One of the game's guns. |
| `SpawnFirearmInFront(FirearmType, distance = 2)` | A gun in front of the player. |
| `SpawnProp(name, position, rotation = null)` | A map prop, by the name the game registered it under. |
| `Spawn(prefabId, position, rotation = null)` | Anything the game has registered, by ID string or `PrefabID`. |
| `SpawnInFront(prefabId, distance = 2, height = 0.5)` | The same, in front of the player. |
| `GetRegisteredPrefabIds()` | Every prefab ID the game knows: weapons, bullets, props, particles and so on. |
| `GetPrefabIds<T>()` | Only the IDs whose prefab has a `T` on it, like `GetPrefabIds<Firearm>()`. |
| `GetPropNames()` | The names you can pass to `SpawnProp`. |
| `GetSpawnedObjects()`, `GetFirearms()` | What's out in the world right now. |
| `Despawn(spawned)` | Removes something you spawned. |

`FirearmType` has the three guns that come with the game: `Viper17` (pistol), `LynxF` (rifle) and `Grist03`
(pump shotgun).

```csharp
var gun = Spawner.SpawnFirearmInFront(FirearmType.LynxF);

foreach (var name in Spawner.GetPropNames())
    LoggerInstance.Msg(name);
```

While you're working on a mod, `DevTools.LogPrefabIds()` prints every ID to the console
(see [Interop and utilities](interop-and-utilities.md#devtools)).

## Firearm extensions

On any `Il2CppSpawnables.Weapons.Firearm`:

| Member | Description |
|--------|-------------|
| `Fire()` | Fires once, with the sound, recoil, ejected casing and muzzle flash. |
| `SetAutoFire(bool)`, `IsAutoFiring()` | The game's "loop firing" mode. |
| `AimAt(target)` | Points the muzzle at a position. |
| `GetRigidbody()` | The gun's rigidbody. |

```csharp
var gun = Spawner.SpawnFirearmInFront(FirearmType.Viper17);
var target = gun == null ? null : Creatures.GetNearest(gun.transform.position, livingOnly: true);
if (target != null)
{
    gun.AimAt(target.GetPosition());
    gun.Fire();
}
```
