# Notifications and native UI

Everything here is in `FruktSharedLibrary.UI`.

## Notifications

Short messages that pop up in the top-right corner, drawn like the game's panels. They fade in and out on their
own. The timing is in real seconds, so pausing or slow motion doesn't make them hang around longer.

| Member | Description |
|--------|-------------|
| `Show(text, seconds = 3)` | A normal message. |
| `Show(text, color, seconds = 3)` | A message with the strip along its top in `color`. |
| `Warn(text, seconds = 4)` | An orange warning. |
| `Clear()` | Gets rid of all of them. |

```csharp
Notifications.Show("Saved");
Notifications.Warn("Aim at a creature first.");
```

Players can turn notifications off under Library settings.

## FruktTheme

The game's colours and fonts, taken from its own menus.

| Member | What it is |
|--------|------------|
| `Background` | `#1C1C1C`, the dark panel colour |
| `Text` | `#CDCDCD`, normal text, and the strip across the top of panels |
| `Muted` | `#787878`, less important text like the numbers next to sliders |
| `Dim` | `#4A4A4A`, breadcrumbs, console text, "off" |
| `Line` | `#2F2F2F`, divider lines |
| `Accent` | `#FFA600`, the orange on sliders, toggles and selected options |
| `Frame` | `#9E9E9E`, the border of toggle boxes |
| `DisplayFont` | GNF, the pixel font for titles and menu lines |
| `MonoFont` | Departure Mono, for setting names, values and console text |
| `BorderSprite` | The thin square border around toggles and key buttons |
| `Available` | Whether the fonts have loaded yet. They load with the first scene. |

## FruktUi

Helpers for building Unity UI that fits in with the game. Positions use a 1920x1080 layout measured from the top
left, with y going down, the same way the game lays out its menus. The canvas scales to the real screen size.

| Member | Description |
|--------|-------------|
| `CreateCanvas(name, sortingOrder = 5000, blockGameClicks = true)` | A canvas that sits on top of the game's menus and survives scene changes. |
| `CreatePanel(name, parent, x, y, width, height, capStrip = 15)` | A dark panel with the light strip across the top. |
| `CreateDisplayText(...)`, `CreateMonoText(...)`, `CreateText(...)` | TextMeshPro text in the game's fonts. |
| `CreateImage(...)`, `CreateFrame(...)` | A block of colour, or the square border. |
| `CreateRect(...)`, `CreateFill(...)`, `Place(...)` | Empty rects, and positioning. |
| `IsHovered(rect)`, `TryGetLocalMouse(rect, out local)` | Mouse checks that read the input directly, so they still work on top of the game's own menus. |
| `MenuLine(word, hovered)` | Text for a line like the game's menu buttons: "> WORD_", with the arrow and underscore only while hovered. |
| `SettingLabel(text)` | Turns "Mouse sensitivity" into "mouse_sensitivity:", the way the game labels its settings. |
| `CloneGameUi(prototype, parent, name)` | Copies a piece of the game's own UI (a button, a row, a whole screen) and hooks it up so hovering and clicking work on the copy. |

```csharp
var canvas = FruktUi.CreateCanvas("MyMod.Panel");
var panel = FruktUi.CreatePanel("Panel", canvas.transform, 60f, 120f, 520f, 200f);
FruktUi.CreateDisplayText("Title", panel, "MY MOD", 40f, FruktTheme.Text, 24f, 30f, 470f, 60f);
FruktUi.CreateMonoText("Body", panel, "hello from my mod", 24f, FruktTheme.Muted, 24f, 100f, 470f, 40f);
```

If your UI needs the mouse, call [`LocalPlayer.CaptureCursor`](world-and-player.md#localplayer) while it's open.
That frees the cursor and stops the game reacting to clicks behind your UI.

Don't copy the game's UI with a plain `Instantiate`. The copy won't animate or respond to clicks, because the
game's UI pieces get their services handed to them when they're created. `CloneGameUi` takes care of that. The
[IL2CPP notes](il2cpp-notes.md) explain why.

## World labels

`WorldLabels` puts text over things in the world, like a name over someone's head, in the game's display font. A
label stays the same size on screen however far away the thing is, hides when it's behind the camera or further
than its `MaxDistance` (60 m), and sits under the game's menus.

```csharp
var head = human.GetLimb(HumanoidNodeTagValue.Head);
var tag = WorldLabels.Add(head.GetMovingTransform(), "Red", Color.red, Vector3.up * 0.45f);
tag.Text = "Blue";
tag.Color = Color.blue;
tag.Remove();
```

| Member | Description |
|--------|-------------|
| `WorldLabels.Add(transform, text, color, offset, size = 30)` | A label that follows a transform, and goes away by itself when the transform is destroyed. |
| `WorldLabels.Add(() => position, text, color, offset, size = 30)` | A label at a position you work out each frame. Remove it yourself. |
| `WorldLabels.All`, `RemoveAll()` | Every label, and taking them all away. |
| `label.Text`, `Color`, `Offset`, `Size`, `MaxDistance`, `Visible` | Change any of these at any time. |
| `label.Exists`, `OnScreen`, `Remove()` | Whether it's still around, whether it's drawn this frame, and taking it away. |

To follow a body part, use its `GetMovingTransform()`, not `limb.transform`, which doesn't move with the body.

## GuiStyles

If you're drawing an overlay in `OnGUI`, `GuiStyles` has the styles and textures the library's plain fallback menu
uses: `Panel`, `Accent`, `Label`, `Header`, `Title`, `Button`, `Notification` and `MakeTexture`. They only work
inside `OnGUI`.
