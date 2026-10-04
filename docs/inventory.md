# Inventory, toolbar, tools and props

`FruktSharedLibrary.Gameplay` reads and changes the game's inventory: the items the terminal lists, and the
toolbar the player picks them from with the number keys. Mods can also add their own items: tools, which give
you the mouse buttons while the player holds them, and props, which the player places like the game's own.

## The inventory

`Inventory.Items` lists every item in the terminal as an `InventoryItem`, including other mods' items.

| Member | Description |
|--------|-------------|
| `Inventory.Items` | Every item, in the order the game registered them. |
| `Inventory.Categories` | The terminal's categories, in the order its tabs show them once a map has loaded. |
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

The category is one of the terminal's tabs: "Weapons", "Tools", "Props" or "Etc". If it doesn't exist, the item
goes under Etc and the log says so.

| Setup | Description |
|-------|-------------|
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
