# Mod menu and pause menu

All of this is in `FruktSharedLibrary.UI`.

## What players see

Players open the mod menu with F8 (they can change the key), or with the MODS line the library adds to the pause
menu. If FruitLib is installed, that line says MOD MENU instead, since FruitLib already has a MODS line. The menu
looks like the game's settings screens, and the first screen has three entries:

- Mods: every installed mod that uses the library. Each one shows its name, version and author, plus whatever
  pages it added.
- Sandbox Tools: the library's own tools. These are only things the game can't already do: set the game to any
  speed, and heal, kill, launch or blow up whoever is under the crosshair.
- Library settings: the library's options and a couple of debug buttons.

Esc goes back a screen, and closes the menu from the first one. While it's open, the cursor is free and the
game's keys are blocked.

## Adding a page

```csharp
ModMenu.AddPage("My Mod")
    .Header("Cheats")
    .Toggle("God mode", () => _god, v => _god = v)
    .Slider("Bullet time", 0.05f, 1f, () => World.TimeScale, v => World.TimeScale = v)
    .Button("Spawn human", () => Creatures.SpawnHumanInFront());
```

The page shows up under Mods, in your mod's entry. If your mod only has one page, its rows appear right there in
the entry; with more than one, the entry lists them. The library works out which mod a page belongs to from the
assembly that called `AddPage`, so you don't have to tell it.

`AddPage` with a title you've already used gives you the same page back, so you can add to it from different
places in your code.

The menu calls your getters every frame, so it always shows the current values. Your setters run when the player
changes something.

## Rows

Every row method returns the page, so you can chain them.

| Method | Row |
|--------|-----|
| `Header(text)` | A section heading. |
| `Label(text)` / `Label(() => text)` | Some text, either fixed or updated live. |
| `Button(text, onClick)` | A line that runs `onClick` when clicked. |
| `Toggle(text, get, set)` | An on/off box. |
| `Slider(text, min, max, get, set, format = "0.00")` | A slider. `format` is how the number next to it is shown. |
| `Slider(text, int min, int max, get, set)` | A slider for whole numbers. |
| `Choice(text, options, get, set)` | Pick one of a few words, laid out like the game's "flat / shift / free". `get` and `set` use the index. |
| `Choice<TEnum>(text, get, set)` | The same, for an enum. |
| `KeyBinding(text, get, set)` | A key you can rebind: click it and press a key. Esc cancels, Backspace clears it. Uses [`KeyBind`](input.md#keybind). |
| `Separator()` | A thin line. |
| `AddSubPage(title)` | A line that opens another page. Note that this returns the new page, not the one you called it on. |

`OnlyWhen(condition)` and `WithTooltip(text)` change the row you added just before them. `OnlyWhen` hides it
unless the condition is true, and `WithTooltip` puts a line of hint text under it.

```csharp
var page = ModMenu.AddPage("My Mod")
    .Toggle("Enabled", () => _enabled, v => _enabled = v).WithTooltip("Turns the whole mod on or off.")
    .Slider("Strength", 0f, 10f, () => _strength, v => _strength = v).OnlyWhen(() => _enabled);

page.AddSubPage("Advanced")
    .Choice("Mode", new[] { "soft", "hard" }, () => _mode, i => _mode = i);
```

Because `AddSubPage` hands back the sub-page, hold on to the outer page (like `page` above) if you want to keep
adding to it.

Pages also have `Title`, `Clear()`, and `VisibleWhen`, which hides the whole page while a condition is false.
`ModMenu.RemovePage(page)` takes a page out.

## Settings pages from MelonPreferences

If your mod already keeps its settings in a MelonPreferences category, `ModMenu.AddPreferencesPage(category,
title = null)` turns it into a page so players can change them in game:

| Entry | Row |
|-------|-----|
| `bool` | Toggle |
| `int`, `float` or `double` with a `ValueRange<T>` | Slider over that range |
| `int`, `float` or `double` without a range | Read-only text |
| An enum | Choice |
| A `string` with "key" in its identifier or name that parses as a `KeyBind` | Key binding |
| Anything else | Read-only text |

Hidden entries are skipped, and each entry's description is shown as its hint. There's a "Reset to defaults"
button at the bottom. Changes take effect straight away and get saved to `MelonPreferences.cfg` a moment later,
and again when the menu closes.

```csharp
var prefs = MelonPreferences.CreateCategory("MyMod", "My Mod settings");
prefs.CreateEntry("Enabled", true, "Enabled", "Turns the mod on or off.");
prefs.CreateEntry("Strength", 5f, "Strength", "How strong the effect is.", false, false, new ValueRange<float>(0f, 10f));
prefs.CreateEntry("ToggleKey", "F9", "Toggle key");
ModMenu.AddPreferencesPage(prefs);
```

## Controlling the menu from code

| Member | Description |
|--------|-------------|
| `Open()`, `Close()`, `Toggle()`, `IsOpen` | Show or hide the menu. |
| `OpenChanged` | Fires with `true` when the menu opens and `false` when it closes. |
| `ToggleKey` | The key the player set, as a `KeyBind`. |
| `LibraryMods` | The installed mods that use the library, as `MelonBase`, sorted by name. |

## Pause menu lines

`PauseMenu.AddButton(label, onClick)` adds a line to the game's pause menu. It's a copy of the game's SETTINGS
line, so it has the same font, hover arrow and animation. New lines go after SETTINGS (and after any other mods'
lines), above BACK TO MENU.

```csharp
var line = PauseMenu.AddButton("Photo mode", StartPhotoMode);
line.VisibleWhen = () => _photoModeUnlocked;
line.SetLabel("Photo mode");   // change the word later
PauseMenu.RemoveButton(line);  // remove it
```

Labels are shown in capitals, like the game's own. The lines get added when a map loads, since the main menu has
no pause menu. `PauseMenu.Attached` turns true once that's happened. If you add a line later on, it shows up
within a couple of seconds.

## Library settings

The library's own options. Players can change them on the Library settings page, or in
`UserData/MelonPreferences.cfg` under `[FruktSharedLibrary]`.

| Preference | Default | What it does |
|------------|---------|--------------|
| `ModMenuKey` | `F8` | The key that opens and closes the menu, like `F6`, `Insert` or `Ctrl+M`. |
| `PauseMenuButton` | `true` | Whether to add the mod menu line to the pause menu. Needs a restart. |
| `NativeStyle` | `true` | Draws the menu and notifications in the game's style. Turn it off for a plain overlay. |
| `ShowNotifications` | `true` | Whether mods' notifications are shown. |
| `BuiltInMenuPage` | `true` | Whether to show the Sandbox Tools page. Needs a restart. |
| `DebugLogging` | `false` | Writes extra lines to the MelonLoader console. |

If the game's fonts can't be found, or the styled menu breaks for some reason, the library switches to the plain
overlay on its own.
