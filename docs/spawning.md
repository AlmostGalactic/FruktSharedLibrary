# Spawning

Namespace `FruktSharedLibrary.Spawning`. Objects are spawned through the game's own factory, so they are set up,
registered with the map and removed by "reset map" exactly like items the player spawns. Humans are spawned with
[`Creatures.SpawnHuman`](creatures.md#finding-and-spawning-creatures).

## Spawner

| Member | Description |
|--------|-------------|
| `SpawnFirearm(FirearmType, position, rotation = null)` | One of the game's firearms. |
| `SpawnFirearmInFront(FirearmType, distance = 2)` | In front of the player. |
| `SpawnProp(name, position, rotation = null)` | A map prop by its registered name. |
| `Spawn(prefabId, position, rotation = null)` | Any registered prefab, by ID string or `PrefabID`. |
| `SpawnInFront(prefabId, distance = 2, height = 0.5)` | In front of the player. |
| `GetRegisteredPrefabIds()` | Every prefab ID the game registered (weapons, bullets, props, particles...). |
| `GetPrefabIds<T>()` | IDs whose prefab has component `T`, for example `GetPrefabIds<Firearm>()`. |
| `GetPropNames()` | Names accepted by `SpawnProp`. |
| `GetSpawnedObjects()`, `GetFirearms()` | What's in the world now. |
| `Despawn(spawned)` | Removes a spawned object. |

`FirearmType` covers the guns that ship with the game: `Viper17` (pistol), `LynxF` (rifle) and `Grist03` (pump
shotgun).

```csharp
var gun = Spawner.SpawnFirearmInFront(FirearmType.LynxF);

foreach (var name in Spawner.GetPropNames())
    LoggerInstance.Msg(name);
```

To see every ID while developing, call `DevTools.LogPrefabIds()` (see
[Interop and utilities](interop-and-utilities.md#devtools)).

## Firearm extensions

For `Il2CppSpawnables.Weapons.Firearm`:

| Member | Description |
|--------|-------------|
| `Fire()` | One shot, with sound, recoil, casing and muzzle flash. |
| `SetAutoFire(bool)`, `IsAutoFiring()` | The game's "loop firing". |
| `AimAt(target)` | Points the muzzle at a world position. |
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
