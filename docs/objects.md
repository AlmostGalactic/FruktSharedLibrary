# Joints and object events

`FruktSharedLibrary.Objects` is for building things out of physics objects: connecting them with joints, and
reacting when they collide, get grabbed or get shot. It works on anything with a rigidbody: your own
[meshes](spawning.md#your-own-models), the game's props and guns, and creature limbs.

## Joints

Every method takes the two things to connect. Pass `null` as the second one to fix the first to a point in the
world instead. Positions and directions are in world space.

| Method | What it makes |
|--------|---------------|
| `Joints.Weld(a, b)` | Sticks them together so they move as one. |
| `Joints.Hinge(a, b, pivot, axis, minAngle, maxAngle)` | A hinge turning around `axis` through `pivot`, like a door or a wheel. Leave out the angles to let it spin freely. |
| `Joints.BallSocket(a, b, pivot)` | Free to swing and twist in every direction around `pivot`, like a shoulder. |
| `Joints.Spring(a, b, stiffness, damping, length)` | A spring that pulls the first object back to `length` away from the second. |
| `Joints.Rope(a, b, pointA, pointB, length)` | A rope: they can't get further apart than `length`, but are free otherwise. Draws a line between the two ends unless you pass `visible: false`. |
| `Joints.Slider(a, b, axis, distance)` | The first object can only slide along `axis`, up to `distance` each way, and can't turn. For pistons, drawers and rails. |

They all take an optional `breakForce`. Pull harder than that and the joint snaps.

```csharp
Joints.Weld(hat, head);                                          // a hat that stays on
Joints.Hinge(door, null, hingePoint, Vector3.up, -90f, 90f);     // a door on a fixed frame
Joints.Rope(lamp, null, pointB: ceilingPoint, length: 2f);       // a lamp hanging from the ceiling
Joints.Spring(seat, car, stiffness: 300f, damping: 20f);         // a bouncy seat
```

`a` and `b` can be a rigidbody, a collider, a creature limb, or any component on an object with a rigidbody.
`Joints.Body(thing)` tells you which rigidbody it will use.

### Joint handles

Each method gives back a `JointHandle`:

| Member | Description |
|--------|-------------|
| `Broke` | Event raised when the joint snaps. |
| `Break()` | Snaps it now (raises `Broke`). |
| `Remove()` | Takes it away quietly. |
| `IsActive`, `IsBroken` | Its state. |
| `SetBreakForce(force)` | Changes how strong it is. |
| `SetMotor(speed, force)` | Hinges only: turns on its own at `speed` degrees per second. Speed 0 turns the motor off. |
| `SetHingeSpring(strength, damping, targetAngle)` | Hinges only: pulls back to `targetAngle`, like a self-closing door. |
| `Angle` | Hinges only: how far it has turned from where it started, in degrees. |
| `RopeLength` | Ropes only: get or change the length, for winches and grappling hooks. |
| `Joint`, `A`, `B`, `Kind` | The Unity joint and what it connects, for anything else. |

```csharp
var wheel = Joints.Hinge(wheelMesh, carBody, axlePoint, carBody.transform.right).SetMotor(360f, 500f);
var chain = Joints.Rope(anchor, crate, breakForce: 2000f);
chain.Broke += _ => Notifications.Show("The chain snapped!");
```

A few things to know:

- Joints on an object go away when the object is deleted. That isn't counted as breaking, so `Broke` isn't raised.
- Leaving the map drops all joints without raising `Broke`.
- Big differences in mass between two joined objects make joints wobbly. That's how Unity's physics works; keep
  connected things within about ten times each other's mass.
- Very stiff springs on light objects can jitter. Start low (tens to a few hundred) and go up.

## Object events

`ObjectEvents.For(gameObject)` gives you the events for one object. Ask again and you get the same one back.

| Member | Description |
|--------|-------------|
| `Collided` | Raised whenever it starts touching something. Gives you an `Impact`. |
| `Hit` | Raised when it hits something at `HitSpeed` or faster (3 m/s by default). |
| `ImpactSounds` | Plays an impact sound on every hit, louder for harder hits. |
| `Grabbed`, `Released` | Raised when the player picks it up with the cursor tool, and lets go. |
| `IsHeld` | Whether the player is holding it right now. |
| `Shot` | Raised when a bullet hits it. Each shotgun pellet counts separately. Gives you a `ShotHit`. |
| `Clear()` | Removes all its events. |

An `Impact` has `Other` (what it hit), `OtherBody` (its rigidbody, or null for the ground and other fixed
things), `Point`, `Normal` and `Speed`. A `ShotHit` has the `Firearm` that fired, the `Point` and the `Normal`.

```csharp
var events = ObjectEvents.For(vase);
events.ImpactSounds = true;
events.Hit += impact =>
{
    if (impact.Speed > 8f)
        Smash(vase);
};
events.Shot += hit => Smash(vase);
events.Grabbed += () => Notifications.Show("Careful, it's fragile");
```

Objects spawned with `Spawner.SpawnMesh` already have impact sounds turned on.
