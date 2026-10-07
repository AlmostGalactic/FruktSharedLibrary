# Creatures and damage

These live in `FruktSharedLibrary.Entities` and `FruktSharedLibrary.Combat`.

FRUKT's bodies are simulated by a system the game calls LVA. A creature (`Il2CppLVA.Creatures.AbstractCreature`)
is a tree of limbs (`Il2CppLVA.Limbs.AbstractLimb`). Each limb is made of voxels and has organs inside it
(`Il2CppLVA.Organs.AbstractOrgan`). When a limb gets cut off, the piece becomes a creature of its own.

## Finding and spawning creatures

The `Creatures` class:

| Member | Description |
|--------|-------------|
| `All`, `Count` | Every creature in the world, cut-off pieces included. |
| `Humans`, `Living` | Just the humans, or just the ones still alive. |
| `GetNearest(position, maxDistance = ∞, livingOnly = false)` | The creature closest to a point. |
| `GetNearest(position, filter, maxDistance = ∞)` | The closest one `filter` accepts, like `c => c.IsHuman() && c != me`. |
| `GetAimedCreature(maxDistance = 500)`, `GetAimedLimb(out hit, maxDistance = 500)` | Whatever is under the crosshair. |
| `FromCollider`, `FromGameObject`, `LimbFromCollider`, `LimbFromGameObject` | Find the creature or limb a collider or object belongs to. |
| `SpawnHuman(position, rotation, onSpawned)` | Spawns a human. Humans take a few frames to put together, so you get the creature in `onSpawned` once it's ready. |
| `SpawnHumanInFront(distance = 3, onSpawned)` | Spawns one in front of the player, facing them. |
| `DeleteAll()`, `DeleteBodies()` | The terminal's "delete everyone" and "delete bodies". |

```csharp
Creatures.SpawnHumanInFront(onSpawned: human => human.SetWalking(true));

var target = Creatures.GetAimedCreature();
if (target != null && target.IsLiving())
    target.Kill();
```

## Creature extensions

With `using FruktSharedLibrary.Entities;` you get these on any `AbstractCreature`:

| | Members |
|-------|---------|
| State | `IsValid`, `IsLiving`, `IsDead`, `IsHuman`, `GetDisplayName`, `GetPosition` |
| Body | `GetLimbs`, `GetLimbCount`, `GetRootLimb`, `GetLimb(HumanoidNodeTagValue)`, `HasLimb(part)` |
| Vitals | `GetPain`, `GetCognition`, `GetBalance` |
| Blood | `GetBloodTank`, `GetBlood`, `GetBloodCapacity`, `SetBlood(amount)`, `RefillBlood`, `DrainBlood(amount)`, `StopBleeding` |
| Actions | `Heal`, `Kill`, `Delete` |
| Physics | `AddForce(force, mode)`, `AddExplosionForce(force, center, radius, upwards)`, `SetFrozen(bool)`, `TeleportTo(position)` |
| Walking | `HasPuppeteer`, `GetPuppeteer`, `GetWalkInteraction`, `IsWalking`, `SetWalking(bool)`, `WalkTowards(point)` |
| Facing | `GetFacing`, `SetFacing(degrees)`, `FaceTowards(point)` |

A few of these are worth knowing more about:

- `Heal` refills the blood and closes bleeding wounds. Destroyed tissue and missing limbs stay gone.
- `Kill` drains all the blood, so the body bleeds out the normal way rather than dropping instantly.
- `AddForce` splits the force across the limbs by their mass.
- `SetWalking(true)` only does anything while the creature still has its puppeteer (the part that animates the
  whole body) and is awake enough to walk.
- `SetFacing` turns them on their feet to a compass direction (0 is +Z, 90 is +X), and while they walk they walk
  that way. `WalkTowards(point)` does both; call it again as the point moves, and `SetWalking(false)` to stop.

```csharp
// Walk the nearest other person over to the player.
var other = Creatures.GetNearest(LocalPlayer.Position, c => c.IsLiving() && c.IsHuman());
other?.WalkTowards(LocalPlayer.Position);
```

Human body parts are `HumanoidNodeTagValue` values (in `Il2CppLVA.NodesHierarchy.Benchmark.Variants`): `Head`,
`Spine`, `Pelvis`, `LeftArm`, `LeftForearm`, `LeftHand`, `LeftLeg`, `LeftKnee`, `LeftFoot`, and the same on the
right.

```csharp
var head = human.GetLimb(HumanoidNodeTagValue.Head);
head?.Detach();
```

## Limb extensions

| | Members |
|-------|---------|
| Structure | `GetCreature`, `GetHumanPart`, `GetParentLimb`, `GetChildLimbs`, `GetNode` |
| Position | `GetPosition`, `GetMovingTransform` |
| Physics | `GetRigidbody`, `AddForce`, `AddForceAtPosition` |
| Tissue | `GetVoxelMesh`, `GetWholeness`, `Damage(worldPoint, radiusVoxels = 3, strength = 1, direction)` |
| Organs | `GetAllOrgans`, `GetOrgan<T>()`, for example `head.GetOrgan<Brain>()` |
| Blood | `GetBloodSystem`, `GetBleedingWoundCount`, `StopBleeding`, `AddBleeding(extraForce)` |
| Actions | `Detach`, `Delete` |

A limb's own `transform` stays where the body was put together and doesn't follow it as it moves. Use
`GetPosition()` for where a limb really is, and `GetMovingTransform()` for something to follow it with (a label, an
effect).

`Detach` uses the game's own detach action, so the piece that comes off becomes its own creature, just like when
it happens in normal play.

## Organ extensions

The organ classes are in `Il2CppLVA.Organs.Variants`: `Brain`, `Heart`, `Lung`, `Bone` and more. On any organ
you can call `GetOrganName`, `GetLimb`, `GetCreature`, `GetIntegrity` (how much of it is left) and
`GetEfficiency` (how well it's working).

## The simulation underneath

Every creature, limb and organ is an LVA entity. Each one has parameters, which are numbers like pain, blood or
wholeness, and systems, which are the logic that updates those numbers. `LvaExtensions` works on all of them:

| Member | Description |
|--------|-------------|
| `GetParameter<T>()` | A parameter object, for example `creature.GetParameter<CreaturePain>()` or `limb.GetParameter<LimbWholeness>()`. |
| `GetParameterValue<T>()` | Just its value, or `null` if the entity doesn't have it. |
| `GetAllParameters()` | A snapshot of every parameter (`LvaParameterInfo` with `Name`, `Value`, `Min`, `Max` and `Normalized`). Useful when you're poking around. |
| `ForceValue(value)` | Overwrites a parameter. Most of them get recalculated every update, so your value might not stick; set it every frame if you need it to. |
| `GetSystem<T>()`, `GetAllSystems()` | The entity's systems, for example `creature.GetSystem<BloodTank>()`. |
| `IsLvaActive()` | Whether the entity's simulation is still running. |

```csharp
foreach (var parameter in creature.GetAllParameters())
    LoggerInstance.Msg(parameter.ToString());
```

## Damage

`Damage` destroys tissue the same way the game's weapons do: it sends the voxels the game's "destruction"
signal. Organs, pain, bleeding and limbs falling off all follow from that on their own.

| Member | Description |
|--------|-------------|
| `Apply(limb, worldPoint, radiusVoxels = 3, strength = 1, direction = null)` | A round wound on a limb. At strength 1 the middle of it is completely destroyed. |
| `Apply(collider, worldPoint, ...)` | The same, on whatever creature the collider belongs to. |
| `Apply(raycastHit, ...)` | The same, where a raycast hit. |
| `Explosion(center, radius, force = 30, maxRadiusVoxels = 5, strength = 1)` | Damages every limb in range (worse near the middle) and pushes things away. Returns how many limbs it hit. |

```csharp
if (LocalPlayer.Raycast(out var hit))
    Damage.Apply(hit, radiusVoxels: 4);
```
