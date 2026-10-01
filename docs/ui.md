# Notifications and native UI

Namespace `FruktSharedLibrary.UI`.

## Notifications

Short messages in the top-right corner, drawn like the game's own panels. They fade in and out, and their
timing uses real time, so they aren't affected by pause or slow motion.

| Member | Description |
|--------|-------------|
| `Show(text, seconds = 3)` | A normal message. |
| `Show(text, color, seconds = 3)` | A message whose top strip uses `color`. |
| `Warn(text, seconds = 4)` | An orange warning. |
| `Clear()` | Removes every message. |

```csharp
Notifications.Show("Saved");
Notifications.Warn("Aim at a creature first.");
```

Players can turn notifications off in *Library settings*.

## FruktTheme

The game's colours and fonts, measured from its menus.

| Member | Value |
|--------|-------|
| `Background` | `#1C1C1C`, panel background |
| `Text` | `#CDCDCD`, main text and the strip along the top of panels |
| `Muted` | `#787878`, secondary values such as slider readouts |
| `Dim` | `#4A4A4A`, breadcrumbs, console lines, "off" |
| `Line` | `#2F2F2F`, separators |
| `Accent` | `#FFA600`, the orange of sliders, toggles and selected options |
| `Frame` | `#9E9E9E`, toggle box frames |
| `DisplayFont` | "GNF", the pixel font used for titles and menu lines |
| `MonoFont` | "Departure Mono", used for setting labels, values and console text |
| `BorderSprite` | The thin square border used by toggles and key chips |
| `Available` | True once the game's fonts are loaded (after the first scene) |

## FruktUi

Builders for Unity UI that matches the game. Positions are in a 1920x1080 reference space with the origin at the
top left and y growing downwards, the same as the game's menus. Canvases scale with the screen.

| Member | Description |
|--------|-------------|
| `CreateCanvas(name, sortingOrder = 5000, blockGameClicks = true)` | A screen-space canvas that survives scene loads and sits above the game's menus. |
| `CreatePanel(name, parent, x, y, width, height, capStrip = 15)` | A dark panel with the light strip along its top. |
| `CreateDisplayText(...)`, `CreateMonoText(...)`, `CreateText(...)` | TextMeshPro text in the game's fonts. |
| `CreateImage(...)`, `CreateFrame(...)` | A solid colour block, or the square border. |
| `CreateRect(...)`, `CreateFill(...)`, `Place(...)` | Plain rects and positioning. |
| `IsHovered(rect)`, `TryGetLocalMouse(rect, out local)` | Mouse checks that read the input system directly, so they work on top of any game menu. |
| `MenuLine(word, hovered)` | Text for a game-style menu line: "> WORD_" with the arrow and cursor only shown while hovered. |
| `SettingLabel(text)` | Formats a label like the game's settings: "Mouse sensitivity" becomes "mouse_sensitivity:". |
| `CloneGameUi(prototype, parent, name)` | Copies one of the game's own widgets (a button, a row, a whole screen) with its services connected, so hover effects and click events work on the copy. |

```csharp
var canvas = FruktUi.CreateCanvas("MyMod.Panel");
var panel = FruktUi.CreatePanel("Panel", canvas.transform, 60f, 120f, 520f, 200f);
FruktUi.CreateDisplayText("Title", panel, "MY MOD", 40f, FruktTheme.Text, 24f, 30f, 470f, 60f);
FruktUi.CreateMonoText("Body", panel, "hello from my mod", 24f, FruktTheme.Muted, 24f, 100f, 470f, 40f);
```

If your UI takes the mouse, pair it with [`LocalPlayer.CaptureCursor`](world-and-player.md#localplayer) so the
cursor is free and the game doesn't act on clicks behind it.

A plain `Instantiate` of a game widget gives a copy that never animates or raises clicks, because the game's
components get their services through injection. Use `CloneGameUi` instead. See
[IL2CPP notes](il2cpp-notes.md).

## GuiStyles

If you draw your own overlay with `OnGUI`, `GuiStyles` has the styles and textures the library's plain fallback
menu uses (`Panel`, `Accent`, `Label`, `Header`, `Title`, `Button`, `Notification`, `MakeTexture`). Only use it
inside `OnGUI`.
