# Inventory, toolbar, tools and props

`FruktSharedLibrary.Gameplay` reads and changes the game's inventory: the items the terminal lists, and the
toolbar the player picks them from with the number keys. Mods can also add their own items: tools, which give
you the mouse buttons while the player holds them, and props, which the player places like the game's own.

## The inventory

`Inventory.Items` lists every item in the terminal as an `InventoryItem`, including other mods' items.

| Member | Description |
|--------|-------------|
| `Inventory.Items` | Every item, in the order the game registered them. |
| `Inventory.Categories` | The terminal's categories, in the order its tabs show them once a map has loaded. Mods' own come after the game's. |
| `Inventory.Find(nameOrId)` | An item by its name ("Viper-17") or ID, ignoring case. Null if there's none. |
| `Inventory.ItemsIn(category)` | The items in one category, by ID or name. |
| `Inventory.TerminalOpen`, `OpenTerminal()`, `CloseTerminal()` | The terminal (the inventory screen). Needs a map. |

Each `InventoryItem` has its `Name`, `Description`, `Id`, `Category` and `CategoryName`, the `CardRows` on its
card (like `("caliber", "9mm")`), its `Icon`, and the game's own data as `Data`. Two `InventoryItem`s for the same
item are equal.

## The toolbar

Slot 0 is the cursor and can't be changed. The toolbar only exists in a map; check `Toolbar.Available`.

| Member | Description |
|--------|-------------|
| `SlotCount`, `CursorSlot` | How many slots there are (other mods can add some), and the cursor's. |
| `GetItem(slot)`, `IsEmpty(slot)`, `CanChange(slot)` | What's in a slot. |
| `KeyOf(slot)` | The number key that picks it. |
| `SelectedSlot`, `SelectedItem` | What the player has picked. |
| `Select(slot)` | Picks a slot, like pressing its key. |
| `Put(item, slot)`, `Give(item)`, `Clear(slot)` | Fills a slot, the first empty one, or empties one. |
| `HeldObject` | The object in the player's hand: the gun, the cutter, or a prop's hologram. |
| `ItemAdded`, `ItemRemoved`, `SelectionChanged` | Events with the item and the slot. |

```csharp
var pistol = Inventory.Find("Viper-17");
int slot = Toolbar.Give(pistol);
if (slot >= 0) Toolbar.Select(slot);
```

## Your own tools

`Inventory.AddTool(name, category)` adds an item to the terminal. The player puts it on the toolbar like any
other item, and while they hold it you get the mouse buttons. Call it from `OnInitializeMelon`. It's added to the
game's inventory once the game has set up its own items, which is before the main menu shows. Calling it later
works too; the item is added straight away.

```csharp
Inventory.AddTool("Boom Stick", "Weapons")
    .WithDescription("Blows up whatever you point at.")
    .WithCard("radius", "3 m")
    .WithIcon(bundle.Load<Sprite>("BoomStickIcon"))
    .WithModel(bundle.Load<GameObject>("BoomStick"), position: new Vector3(0.2f, -0.15f, 0.4f))
    .OnLeftClick(() =>
    {
        if (LocalPlayer.TryGetAimPoint(out var point))
            Damage.Explosion(point, 3f);
    });
```

The category is one of the terminal's tabs: "Weapons", "Tools", "Props", "Etc", or [your own](#your-own-categories).
If it doesn't exist, the item goes under Etc and the log says so.

| Setup | Description |
|-------|-------------|
| `WithCategory(name)` | Files it under another category, such as [your own](#your-own-categories). Props use it to leave Props. Call it before the first map loads. |
| `WithDescription(text)` | The text on its card. |
| `WithCard(key, value)` | A row on its card, like ("range", "40 m"). |
| `WithIcon(sprite)` | Its icon in the terminal and on the toolbar. Without one it gets a picture of its model (see below), or a plain square if it has no model. Can be changed at any time. |
| `WithModel(prefab, position, rotation, scale)` | What the player sees in their hand, relative to where the game holds its own items. Without one the hand is empty. The copy in the hand has no colliders or rigidbodies, and its materials are switched to the game's shaders. It can be changed at any time, even while the player holds the tool. |
| `WithModel(bundle, prefab, position, rotation, scale)` | The same with a prefab from a bundle, kept up to date when the bundle is [reloaded](asset-bundles.md#reloading-while-the-game-runs). |

| Event | When |
|-------|------|
| `Selected`, `Deselected` | The player picked it, or put it away. |
| `WhileHeld` | Every frame while they hold it. |
| `LeftClick`, `LeftHold(seconds)`, `LeftRelease` | The left mouse button. `LeftHold` runs every frame while it's down. |
| `RightClick`, `RightHold(seconds)`, `RightRelease` | The right mouse button. |
| `MiddleClick`, `Scroll(notches)` | The middle button and the wheel. |

`OnLeftClick`, `OnRightClick`, `OnScroll` and `OnHeld` subscribe and return the tool, for chaining. A handler that
throws is logged and doesn't stop the others.

### Icons

A tool or prop without an icon of its own gets a picture of its model, on a transparent background, the first
time a map loads (until then, and if the picture can't be taken, it's a plain square). The picture is taken again
when the model, mesh or prefab changes. Pass your own with `WithIcon` to skip all that. To take pictures of models
yourself, use [`Thumbnails`](interop-and-utilities.md#thumbnails).

A new icon shows in the terminal straight away. A toolbar slot that already holds the item can keep showing the
old one.

### What else a tool has

`IsHeld` and `HeldObject` tell you whether the player is holding it and give you the copy in their hand.
`Registered` turns true once it's in the inventory and `Item` is then its `InventoryItem`, so you can put it on
the toolbar yourself. `Failed` means it couldn't be added; the log says why.

## Your own categories

`Inventory.AddCategory(name, icon)` adds a tab to the terminal, after the game's own. File items under it by name.
Call it from `OnInitializeMelon`, like the items; it's in the terminal from the first map on.

```csharp
Inventory.AddCategory("Explosives", bundle.Load<Sprite>("ExplosivesIcon"));
Inventory.AddTool("Boom Stick", "Explosives");
Inventory.AddProp("Barrel", bundle, "Barrel").WithCategory("Explosives");
```

- Props go under Props unless you call `WithCategory` on them, as above. Tools and guns take the category when you
  add them, or with `WithCategory` too.
- Without an icon, the tab shows the icon of its first item, or the Etc icon while it's empty. `WithIcon(sprite)`
  changes it at any time.
- Mods that add a category with the same name share one tab. Adding one the game already has, like "Weapons", gives
  you the game's.
- The terminal has room for seven tabs in a column. With more, they go into more columns, filled top to bottom, and
  the panel widens to fit them. Names too long for the header are made smaller.
- A category can hold any number of items. The grid scrolls, as it does for the game's own.

The returned `ModCategory` has its `Name`, `Icon`, `Items`, and `Added`, which turns true once it's in the terminal.

## Your own guns

`Inventory.AddGun(name)` adds a gun under Weapons. It's a tool that fires on left click, or for as long as the
button is held if it's automatic, no faster than its fire rate. Each shot raises `Fired`; what the shot does is up
to you, and [`Bullets`](#bullets) has the usual pieces.

```csharp
var smg = Inventory.AddGun("Wasp-9")
    .WithFireRate(12f, automatic: true)
    .WithMuzzle(new Vector3(0f, 0.05f, 0.34f))
    .OnFire(gun =>
    {
        var shot = Bullets.Fire(Bullets.Spread(LocalPlayer.AimRay, 1.5f));
        Bullets.Tracer(gun.Muzzle, shot.End, Color.yellow);
        Sounds.Play(WeaponSFXType.Shoot9MM, gun.Muzzle);
    });
smg.WithDescription("Submachine gun.").WithModel(model);
```

The gun's own methods return the gun, and the ones it shares with every tool (`WithDescription`, `WithModel` and so
on) return a `ModTool`, so set up the gun part first, or keep a reference like `smg` above.

| Member | Description |
|--------|-------------|
| `WithFireRate(shotsPerSecond, automatic = false)`, `WithCooldown(seconds, automatic = false)` | How fast it fires, and whether holding the button keeps it firing. |
| `WithMuzzle(offset)` | The end of the barrel, in the model's own coordinates. |
| `OnFire(gun => ...)`, `Fired` | Runs each time it fires. |
| `Muzzle` | The end of the barrel in the world, following the model in the hand. |
| `TryFire()` | Fires now if it's ready, as if the player clicked. |
| `Ready`, `FireInterval`, `Automatic`, `ShotsFired` | Its state. |

### Bullets

`Bullets` (in `FruktSharedLibrary.Combat`) has two kinds of shot. `Launch` flies the game's own bullets, which
are the ones to use when a gun should hurt like the game's guns. `Fire` and `Pierce` are instant hits along a ray
that make one wound where they land, which suits tools and odd weapons.

`Launch` makes a real 9mm, 7.62 or 12-gauge pellet. It goes into a body, makes the same channel through it, can
come out the other side, wounds what's behind, shoves what it hits and plays the impact sound.

```csharp
.OnFire(gun =>
{
    var direction = (Bullets.AimPoint() - gun.Muzzle).normalized;
    Bullets.Launch(gun.Muzzle, direction, Bullets.Caliber.Pistol);
    Effects.MuzzleFlash(gun.Muzzle, direction);
})
```

| Member | Description |
|--------|-------------|
| `Launch(origin, direction, caliber = Pistol, speed = 0)` | One of the game's bullets. `Caliber` is `Pistol` (9mm), `Rifle` (7.62) or `Pellet` (12-gauge); the speed is the game's own for that round unless you give one. Returns the bullet. Fire a shotgun by launching several pellets with `Spread`. |
| `Fire(ray, range = 400, radiusVoxels = 2, strength = 1, push = 5, sound = true)` | An instant hit that makes one wound. Gives you a `BulletHit` with `Hit`, `End`, `Limb`, `Creature` and `Body`. |
| `Pierce(ray, maxHits = 8, ...)` | A bullet that goes through bodies and loose objects, up to `maxHits` of them. The ground and walls stop it. |
| `EndOf(hits, ray)` | Where a pierced shot ended. |
| `Spread(ray, degrees)`, `Spread(direction, degrees)` | Turns a shot a random amount, up to `degrees`. |
| `AimPoint(range = 400)` | The point under the crosshair. Send projectiles from the muzzle towards it, so they land where the player aimed. |
| `Tracer(from, to, color, width = 0.012, seconds = 0.08)` | A line that thins away, for showing where a shot went. |

### Effects

`Effects` (in `FruktSharedLibrary.Combat`) is fire, smoke, sparks and debris made of small cubes, like the rest of
the game. There's nothing to set up, and it tidies itself up and clears when a map is left. At most
`Effects.MaxParticles` cubes are alive at once.

| Member | Description |
|--------|-------------|
| `Explosion(at, size = 3, shake = true)` | A white-hot core, a fireball, a column of smoke, sparks, debris that bounces off the ground, dust along the ground, a flash of light and a camera shake that fades with distance. `size` is about the radius in metres. It makes the picture only: use `Damage.Explosion` for the harm. |
| `MuzzleFlash(at, direction, size = 1)` | The flash and smoke at a barrel, using the game's own muzzle effects. |
| `Burst(at, color, count, speed, size, life, glow, gravity, direction, spreadDegrees, endColor, bounce)` | Cubes thrown out of a point, in every direction or in a cone. `glow` makes them light up like sparks; a negative `gravity` makes them rise. |
| `Smoke(at, size, life, velocity, darkness)` | A puff that swells and thins away. |
| `Flame(at, size, life, velocity)` | A piece of flame, white-yellow going to red. Emit one or two a frame behind a rocket. |
| `Flash(at, color, range, seconds, intensity)` | A flash of light that dies away. |
| `Count`, `Clear()` | How many cubes are alive, and take them all away. |

## Your own props

`Inventory.AddProp` adds an item under Props that the player places in the world. While they hold it, a
see-through hologram shows where it will go: on whatever they're aiming at, facing them. Left click puts a copy
there, and the mouse wheel turns it.

```csharp
var barrel = Meshes.LoadObj(Path.Combine(MelonEnvironment.UserDataDirectory, "barrel.obj"));
Inventory.AddProp("Barrel", barrel, Meshes.CreateMaterial(color: Color.red), mass: 40f)
    .WithDescription("A red barrel.")
    .WithCard("size", "1 m");

Inventory.AddProp("Crate", bundle, "Crate")
    .OnPlaced(crate => ObjectEvents.For(crate).ImpactSounds = true);
```

`AddProp(name, bundle, prefab)` takes the prefab from a bundle and follows it when the bundle is
[reloaded](asset-bundles.md#reloading-while-the-game-runs). `AddProp(name, prefab)` takes any GameObject.

- From a mesh, each copy is a physics object like `Spawner.SpawnMesh` makes: the player can grab, throw and shoot
  it.
- From a prefab, each copy is the prefab as it is, on the props' layer. Give it a rigidbody and colliders if it
  should move.
- Copies aren't part of the map, so resetting the map leaves them alone. Destroy them yourself if you need to.

A `ModProp` is a `ModTool`, so it has all of the above, plus:

| Member | Description |
|--------|-------------|
| `Placed` / `OnPlaced(action)` | A copy was put in the world. Gives you the copy. |
| `Place(position, rotation)` | Puts a copy in the world from code. Raises `Placed`. |
| `TryGetPlacement(out position, out rotation)` | Where it would go if the player clicked now. False if they aren't aiming at anything within reach; the position is then in mid-air in front of them. |
| `Hologram` | The hologram while the player holds it. |
| `Turn`, `WithTurnStep(degrees)` | How far the wheel has turned it, and how much one notch turns it (15 degrees). |
| `Reach`, `WithReach(metres)` | How far away it can be placed (30 m). |
| `Mesh`, `Material`, `Mass`, `Prefab` | What it's made of. |
| `SetMesh(mesh, material)`, `SetPrefab(prefab)` | Switches what it's made of. The hologram and the icon follow; copies already placed stay as they are. |

To work on a model without restarting the game, watch its file and swap it in when it changes:

```csharp
string path = Path.Combine(MelonEnvironment.UserDataDirectory, "barrel.obj");
var barrel = Inventory.AddProp("Barrel", Meshes.LoadObj(path));
FileWatch.Start(path, () => barrel.SetMesh(Meshes.LoadObj(path)));
```

## Living with other mods

Items from FruitLib, StayinAlive and AverysBoxOfFun show up in `Inventory.Items` like the game's own, and the
library's tools and props sit next to theirs in the terminal. Toolbar slots other mods add are counted in
`SlotCount`.
