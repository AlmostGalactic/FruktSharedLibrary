# Right-click menus

Namespace `FruktSharedLibrary.UI`. `ContextMenus` adds your own lines to the game's right-click menus. They are
real game menu actions, so they look and behave like the built-in ones.

## Targets

| `ContextMenuTarget` | Menu |
|---------------------|------|
| `Limb` | Right-clicking any body part. Use this for creature actions. |
| `Prop` | Spawned and map props. |
| `Firearm` | Guns. |
| `HumanSpawner` | The human spawner device. |
| `Spinner` | The map's spinner. |

## Actions

```csharp
ContextMenus.AddCreatureAction("Heal", creature => creature.Heal());
ContextMenus.AddLimbAction("Pop", limb => limb.Damage(limb.transform.position, 6));
ContextMenus.AddAction(ContextMenuTarget.Firearm, "Empty magazine", ctx => EmptyMagazine(ctx.Firearm));
```

| Method | Description |
|--------|-------------|
| `AddAction(target, label, onClick, showIf = null, priority = 500)` | A line on one kind of menu. `onClick` gets a `ContextMenuContext`. |
| `AddAction(target, ctx => label, onClick, ...)` | The same with a label computed per object (when the menu is built, and after each click). |
| `AddLimbAction(label, limb => ..., showIf, priority)` | Shorthand for limb menus. |
| `AddCreatureAction(label, creature => ..., showIf, priority)` | Shorthand for the creature that owns the clicked limb. Skipped for objects that aren't valid creatures. |
| `AddToggle(target, label, getState, setState, showIf, priority)` | An on/off line ("Label: ON"). Clicking flips it and keeps the menu open, like the game's own switches. |
| `RemoveAll()` | Removes every action that any mod added. Mostly useful in tests. |

Every `Add` method returns a `ContextMenuEntry`; call `entry.Remove()` to take it out of every menu again.

Clicking a normal action runs it and closes the menu, like the built-in actions.

### What the context gives you

`ContextMenuContext` describes the object the menu belongs to. Only the field matching `Target` is set:
`Limb` (and `Creature`, read live from the limb), `Prop`, `Firearm`, `Spawner` or `Spinner`. `Handler` is the
game's menu component and `GameObject` its object.

### When `showIf` runs

Objects build their right-click menu **when they spawn**, not when the menu opens, so `showIf` runs once per object
at that point. For objects that already exist when you register an action, it runs at registration. Use it to
decide whether a kind of object gets the line at all (for example humans only). To react to changing state, have
the action check its own conditions when clicked, or use a computed label.

### Order

Lines are sorted by priority, highest first. The game's own actions use 995 to 1000, so the default of 500 puts
yours below them. A priority above 1000 puts a line above them.

## Drop-down groups

A group is one line, such as `+ MY MOD`, that expands in place when clicked to show its own lines indented
underneath. The menu stays open. Clicking it again collapses it, and every group is collapsed again whenever the
menu closes. Groups can contain groups.

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
| `group.AddAction(label, onClick, showIf)` | A line inside the group. Runs and closes the menu. |
| `group.AddToggle(label, getState, setState, showIf)` | An on/off line inside the group. Keeps the menu open. |
| `group.AddCreatureAction(...)`, `group.AddLimbAction(...)` | Shorthands, for groups on `Limb` menus only. |
| `group.AddGroup(label, showIf)` | A nested group. Returns the **new** group. |
| `group.Remove()` | Removes the group and everything in it. |

Like `AddSubPage` in the mod menu, `AddGroup` returns the new nested group while the other methods return the group
you called them on. Keep a reference if you want to add more lines to the outer group afterwards.

## Adding and removing at any time

Actions and groups can be added or removed whenever you like. The library also inserts them into menus of objects
that already exist, and removes them from those menus again.
