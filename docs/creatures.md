# Creatures and damage

Namespaces `FruktSharedLibrary.Entities` and `FruktSharedLibrary.Combat`.

FRUKT simulates bodies with its "LVA" system. A creature (`Il2CppLVA.Creatures.AbstractCreature`) is a tree of
limbs (`Il2CppLVA.Limbs.AbstractLimb`), each made of voxels and containing organs
(`Il2CppLVA.Organs.AbstractOrgan`). When a limb is severed, the part becomes a creature of its own.

## Finding and spawning: Creatures

| Member | Description |
|--------|-------------|
| `All`, `Count` | Every registered creature, including severed parts. |
| `Humans`, `Living` | Filtered views. |
| `GetNearest(position, maxDistance = ∞, livingOnly = false)` | The closest creature to a point. |
| `GetAimedCreature(maxDistance = 500)`, `GetAimedLimb(out hit, maxDistance = 500)` | What's under the crosshair. |
| `FromCollider`, `FromGameObject`, `LimbFromCollider`, `LimbFromGameObject` | Map physics objects back to creatures and limbs. |
| `SpawnHuman(position, rotation, onSpawned)` | Spawns a human. Humans are assembled over a few frames, so the creature is passed to `onSpawned` when it's ready. |
| `SpawnHumanInFront(distance = 3, onSpawned)` | Spawns one in front of the player, facing them. |
| `DeleteAll()`, `DeleteBodies()` | The terminal's "delete everyone" and "delete bodies". |

```csharp
Creatures.SpawnHumanInFront(onSpawned: human => human.SetWalking(true));

var target = Creatures.GetAimedCreature();
if (target != null && target.IsLiving())
    target.Kill();
```

## Creature extensions

`using FruktSharedLibrary.Entities;` adds these to `AbstractCreature`:

| Group | Members |
|-------|---------|
| State | `IsValid`, `IsLiving`, `IsDead`, `IsHuman`, `GetDisplayName`, `GetPosition` |
| Body | `GetLimbs`, `GetLimbCount`, `GetRootLimb`, `GetLimb(HumanoidNodeTagValue)`, `HasLimb(part)` |
| Vitals | `GetPain`, `GetCognition`, `GetBalance` |
| Blood | `GetBloodTank`, `GetBlood`, `GetBloodCapacity`, `SetBlood(amount)`, `RefillBlood`, `DrainBlood(amount)`, `StopBleeding` |
| Actions | `Heal` (refills blood and closes wounds; lost tissue and limbs don't grow back), `Kill` (drains the blood, so the body bleeds out naturally), `Delete` |
| Physics | `AddForce(force, mode)` (split by limb mass), `AddExplosionForce(force, center, radius, upwards)`, `SetFrozen(bool)`, `TeleportTo(position)` |
| Movement | `HasPuppeteer`, `GetPuppeteer`, `GetWalkInteraction`, `IsWalking`, `SetWalking(bool)` |

Human body parts are `Il2CppLVA.NodesHierarchy.Benchmark.Variants.HumanoidNodeTagValue` values such as `Head`,
`Pelvis` and so on:

```csharp
var head = human.GetLimb(HumanoidNodeTagValue.Head);
head?.Detach();
```

`SetWalking(true)` only works while the creature still has a puppeteer (the part that animates a whole body) and
is conscious enough to walk.

## Limb extensions

| Group | Members |
|-------|---------|
| Structure | `GetCreature`, `GetHumanPart`, `GetParentLimb`, `GetChildLimbs`, `GetNode` |
| Physics | `GetRigidbody`, `AddForce`, `AddForceAtPosition` |
| Tissue | `GetVoxelMesh`, `GetWholeness`, `Damage(worldPoint, radiusVoxels = 3, strength = 1, direction)` |
| Organs | `GetAllOrgans`, `GetOrgan<T>()` (for example `head.GetOrgan<Brain>()`) |
| Blood | `GetBloodSystem`, `GetBleedingWoundCount`, `StopBleeding`, `AddBleeding(extraForce)` |
| Actions | `Detach` (the game's own detach; the part becomes a new creature), `Delete` |

## Organ extensions

Organ classes are in `Il2CppLVA.Organs.Variants` (`Brain`, `Heart`, `Lung`, `Bone` and others).
`GetOrganName`, `GetLimb`, `GetCreature`, `GetIntegrity` (how much tissue is left), `GetEfficiency` (how well it
works).

## The simulation: LvaExtensions

Every creature, limb and organ is an LVA entity with parameters (numbers such as pain, blood or wholeness) and
systems (the logic that updates them). These extensions work on any of them:

| Member | Description |
|--------|-------------|
| `GetParameter<T>()` | One parameter object, for example `creature.GetParameter<CreaturePain>()` or `limb.GetParameter<LimbWholeness>()`. |
| `GetParameterValue<T>()` | Just its current value, or `null`. |
| `GetAllParameters()` | Snapshots of every parameter (`LvaParameterInfo`: `Name`, `Value`, `Min`, `Max`, `Normalized`). Good for debugging. |
| `ForceValue(value)` | Overwrites a parameter. Most parameters are recalculated from their inputs every update, so the change may not last; re-apply it each frame if needed. |
| `GetSystem<T>()`, `GetAllSystems()` | The entity's systems, for example `creature.GetSystem<BloodTank>()`. |
| `IsLvaActive()` | True while the entity's simulation is running. |

```csharp
foreach (var parameter in creature.GetAllParameters())
    LoggerInstance.Msg(parameter.ToString());
```

## Damage

`Damage` destroys tissue the way the game's weapons do, by sending the voxel "destruction" signal. Organs, pain,
bleeding and dismemberment all respond on their own.

| Member | Description |
|--------|-------------|
| `Apply(limb, worldPoint, radiusVoxels = 3, strength = 1, direction = null)` | A spherical wound on a limb. `strength` 1 fully destroys the core of the sphere. |
| `Apply(collider, worldPoint, ...)` | The same, for whatever creature the collider belongs to. |
| `Apply(raycastHit, ...)` | The same, at a raycast hit. |
| `Explosion(center, radius, force = 30, maxRadiusVoxels = 5, strength = 1)` | Damages every limb in range (more near the centre) and pushes rigidbodies. Returns how many limbs were hit. |

```csharp
if (LocalPlayer.Raycast(out var hit))
    Damage.Apply(hit, radiusVoxels: 4);
```
