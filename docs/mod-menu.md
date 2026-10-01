# Mod menu and pause menu

Namespace `FruktSharedLibrary.UI`.

## How players see it

The mod menu opens with **F8** (configurable) or from the **MODS** line the library adds to the pause menu. That
line is called **MOD MENU** when FruitLib is also installed, because FruitLib adds its own MODS line. The menu is
drawn like the game's settings screens. Its top level has:

- **Mods**: one entry per installed mod that uses the library, showing its name, version and author, and the
  pages that mod added.
- **Sandbox Tools**: the library's own tools, limited to things the base game doesn't have: any game speed, and
  heal, kill, launch or explode for the creature under the crosshair.
- **Library settings**: the library's preferences and a few developer tools.

Esc goes back one level and closes the menu from the top. While the menu is open the cursor is free and the
game's buttons are blocked.

## Adding a page

```csharp
ModMenu.AddPage("My Mod")
    .Header("Cheats")
    .Toggle("God mode", () => _god, v => _god = v)
    .Slider("Bullet time", 0.05f, 1f, () => World.TimeScale, v => World.TimeScale = v)
    .Button("Spawn human", () => Creatures.SpawnHumanInFront());
```

Your page appears under **Mods**, in your mod's entry. If a mod adds one page, the entry shows that page directly
under the version line. With several pages, the entry lists them. The library works out which mod a page belongs
to from the assembly that called `AddPage`.

Calling `AddPage` again with the same title returns the same page, so you can add to it from several places.

Values are read through your getters every frame, so the menu always shows the current state. Setters run when the
player changes something.

## Rows

All row methods return the page, so they chain.

| Method | Row |
|--------|-----|
| `Header(text)` | A section title. |
| `Label(text)` / `Label(() => text)` | A line of text, fixed or updated live. |
| `Button(text, onClick)` | A menu line that runs `onClick`. |
| `Toggle(text, get, set)` | An on/off box. |
| `Slider(text, min, max, get, set, format = "0.00")` | A float slider. `format` is the readout's number format. |
| `Slider(text, int min, int max, get, set)` | A whole-number slider. |
| `Choice(text, options, get, set)` | One of a few words, shown inline like the game's "flat / shift / free". `get`/`set` use the index. |
| `Choice<TEnum>(text, get, set)` | The same for any enum. |
| `KeyBinding(text, get, set)` | A key chip. Click it, then press a key. Esc cancels and Backspace clears. Uses [`KeyBind`](input.md#keybind). |
| `Separator()` | A thin line. |
| `AddSubPage(title)` | A line that opens a nested page. Returns the **new** page. |

Two modifiers apply to the row added just before them:

- `OnlyWhen(condition)` shows the row only while `condition` returns true.
- `WithTooltip(text)` adds a hint line under it.

```csharp
var page = ModMenu.AddPage("My Mod")
    .Toggle("Enabled", () => _enabled, v => _enabled = v).WithTooltip("Turns the whole mod on or off.")
    .Slider("Strength", 0f, 10f, () => _strength, v => _strength = v).OnlyWhen(() => _enabled);

page.AddSubPage("Advanced")
    .Choice("Mode", new[] { "soft", "hard" }, () => _mode, i => _mode = i);
```

Because `AddSubPage` returns the sub-page, keep a reference to the outer page if you want to keep adding to it.

Other page members: `Title`, `VisibleWhen` (hide the whole page while a condition is false), and `Clear()`.
`ModMenu.RemovePage(page)` removes a page.

## Settings pages from MelonPreferences

`ModMenu.AddPreferencesPage(category, title = null)` builds a page from a MelonPreferences category, so players can
change your mod's settings in game:

| Entry type | Row |
|------------|-----|
| `bool` | Toggle |
| `int`, `float`, `double` with a `ValueRange<T>` validator | Slider over that range |
| `int`, `float`, `double` without a range | Read-only line |
| An enum | Choice |
| `string` whose identifier or name contains "key" and that parses as a `KeyBind` | Key binding |
| Anything else | Read-only line |

Hidden entries are left out, and each entry's description becomes its hint line. The page ends with a "Reset to
defaults" button. Changes apply immediately and are saved to `MelonPreferences.cfg` shortly after, and again when
the menu closes.

```csharp
var prefs = MelonPreferences.CreateCategory("MyMod", "My Mod settings");
prefs.CreateEntry("Enabled", true, "Enabled", "Turns the mod on or off.");
prefs.CreateEntry("Strength", 5f, "Strength", "How strong the effect is.", false, false, new ValueRange<float>(0f, 10f));
prefs.CreateEntry("ToggleKey", "F9", "Toggle key");
ModMenu.AddPreferencesPage(prefs);
```

## Controlling the menu

| Member | Description |
|--------|-------------|
| `Open()`, `Close()`, `Toggle()`, `IsOpen` | Show or hide it from code. |
| `OpenChanged` | Event raised with `true` when the menu opens and `false` when it closes. |
| `ToggleKey` | The configured key, as a `KeyBind`. |
| `LibraryMods` | The installed mods that use the library (as `MelonBase`), sorted by name. |

## Pause menu lines

`PauseMenu.AddButton(label, onClick)` adds a line to the game's pause menu. The line is a copy of the game's own
SETTINGS line, so it has the same font, hover arrow and animation. Lines are placed after SETTINGS and after lines
other mods added, before BACK TO MENU.

```csharp
var line = PauseMenu.AddButton("Photo mode", StartPhotoMode);
line.VisibleWhen = () => _photoModeUnlocked;
line.SetLabel("Photo mode");   // change the word later
PauseMenu.RemoveButton(line);  // remove it
```

Labels are shown in upper case like the game's own lines. Lines are added to the pause menu when a map loads (the
main menu has no pause menu); `PauseMenu.Attached` is true once that has happened. Buttons added later are picked
up within a couple of seconds.

## Library settings

The library's own preferences, on the *Library settings* page and in `UserData/MelonPreferences.cfg` under
`[FruktSharedLibrary]`:

| Preference | Default | Meaning |
|------------|---------|---------|
| `ModMenuKey` | `F8` | Key that opens and closes the menu (`F6`, `Insert`, `Ctrl+M`...). |
| `PauseMenuButton` | `true` | Adds the mod menu line to the pause menu. Takes effect after a restart. |
| `NativeStyle` | `true` | Draws the menu and notifications in the game's style. `false` uses a plain overlay. |
| `ShowNotifications` | `true` | Shows notifications posted by mods. |
| `BuiltInMenuPage` | `true` | Shows the *Sandbox Tools* page. Takes effect after a restart. |
| `DebugLogging` | `false` | Extra lines in the MelonLoader console. |

If the game's fonts can't be found, or the native menu fails for some reason, the library falls back to the plain
overlay automatically.
