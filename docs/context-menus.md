# Right-click menus

`ContextMenus` (in `FruktSharedLibrary.UI`) lets you add your own lines to the game's right-click menus. They're
real game menu actions, so they look and act just like the built-in ones.

## Which menus

| `ContextMenuTarget` | Menu |
|---------------------|------|
| `Limb` | Right-clicking any body part. Use this one for anything to do with creatures. |
| `Prop` | Props, whether spawned or part of the map. |
| `Firearm` | Guns. |
| `HumanSpawner` | The human spawner. |
| `Spinner` | The spinner on the map. |

## Actions

```csharp
ContextMenus.AddCreatureAction("Heal", creature => creature.Heal());
ContextMenus.AddLimbAction("Pop", limb => limb.Damage(limb.transform.position, 6));
ContextMenus.AddAction(ContextMenuTarget.Firearm, "Empty magazine", ctx => EmptyMagazine(ctx.Firearm));
```

| Method | Description |
|--------|-------------|
| `AddAction(target, label, onClick, showIf = null, priority = 500)` | Adds a line to one kind of menu. `onClick` gets a `ContextMenuContext`. |
| `AddAction(target, ctx => label, onClick, ...)` | The same, but the label is worked out per object, when its menu is built and again after each click. |
| `AddLimbAction(label, limb => ..., showIf, priority)` | A shortcut for limb menus. |
| `AddCreatureAction(label, creature => ..., showIf, priority)` | A shortcut that hands you the creature the clicked limb belongs to. It isn't added to things that aren't a proper creature. |
| `AddToggle(target, label, getState, setState, showIf, priority)` | An on/off line that reads "Label: ON". Clicking it flips it and leaves the menu open, like the game's own switches. |
| `RemoveAll()` | Removes everything every mod has added. Mainly for testing. |

Each `Add` method gives you back a `ContextMenuEntry`. Call `entry.Remove()` on it to take the line out of every
menu again.

A normal action runs when clicked and then closes the menu, the same as the game's actions.

### The context

`ContextMenuContext` tells you what the menu is for. Only the field that matches `Target` is filled in: `Limb`
(along with `Creature`, which is looked up from the limb when you read it), `Prop`, `Firearm`, `Spawner` or
`Spinner`. You also get `Handler`, the game's menu component, and `GameObject`.

### When `showIf` is checked

Things build their right-click menu when they spawn, not when you open it. So `showIf` is checked once per
object, at spawn time, or at the moment you register the action for things that already exist. Use it to decide
whether a kind of object should have the line at all, like "humans only". If you need to react to something that
changes, check it inside the action when it's clicked, or use a label that's worked out per object.

### Order

Lines are sorted by priority, highest at the top. The game's own actions use 995 to 1000, so the default of 500
puts yours underneath them. Use more than 1000 to go above.

## Drop-down groups

A group is a single line, like `+ MY MOD`. Clicking it opens it up right there, with its own lines indented
underneath, and the menu stays open. Clicking it again closes it, and all groups close up when the menu does.
You can put groups inside groups.

```csharp
var tools = ContextMenus.AddCreatureGroup("My Mod")
    .AddCreatureAction("Kill", creature => creature.Kill())
    .AddToggle("Walking", ctx => ctx.Creature.IsWalking(), (ctx, on) => ctx.Creature.SetWalking(on));

tools.AddGroup("Throw")
    .AddCreatureAction("Up", creature => creature.AddForce(Vector3.up * 800f))
    .AddCreatureAction("Away", creature => creature.AddForce(LocalPlayer.Forward * 800f));
```

| Method | Description |
|--------|-------------|
| `ContextMenus.AddGroup(target, label, showIf = null, priority = 500)` | A group on any kind of menu. |
| `ContextMenus.AddCreatureGroup(label, showIf, priority)` | A group on creature (limb) menus. |
| `group.AddAction(label, onClick, showIf)` | A line inside the group. It runs and closes the menu. |
| `group.AddToggle(label, getState, setState, showIf)` | An on/off line inside the group. The menu stays open. |
| `group.AddCreatureAction(...)`, `group.AddLimbAction(...)` | Shortcuts, only for groups on `Limb` menus. |
| `group.AddGroup(label, showIf)` | A group inside this one. |
| `group.Remove()` | Takes out the group and everything in it. |

`AddGroup` gives you the new inner group, while the other methods give you back the group you called them on.
That's the same as `AddSubPage` in the mod menu. If you want to add more lines to the outer group afterwards,
keep a reference to it, like `tools` above.

## Adding and removing later

You can add or remove actions and groups whenever you want. Things that already exist in the world get the change
too, not just new ones.
