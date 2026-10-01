# Input

Namespace `FruktSharedLibrary.Controls`. FRUKT uses Unity's newer Input System, and the old `UnityEngine.Input`
class may be disabled in it. These helpers read the Input System and only fall back to the old API if they have
to. Keys are `UnityEngine.InputSystem.Key` values.

## FruktInput

| Member | Description |
|--------|-------------|
| `GetKeyDown(key)`, `GetKey(key)`, `GetKeyUp(key)` | Pressed this frame, held, released this frame. |
| `CtrlHeld`, `ShiftHeld`, `AltHeld` | Either side of the modifier is held. |
| `GetMouseButtonDown(button)`, `GetMouseButton(button)` | 0 = left, 1 = right, 2 = middle. |
| `MousePosition` | Screen pixels, origin at the bottom left. |
| `ScrollDelta` | Mouse wheel movement this frame. |
| `TryGetPressedKey(out key, includeModifiers = false)` | The first key pressed this frame, for "press a key" prompts. Modifiers are skipped by default, so Ctrl+K reports K. |
| `IsModifier(key)` | True for Ctrl, Shift, Alt and the Windows/Command keys. |
| `TryParseKey(text, out key)` | Parses names such as `F8`, `Insert`, `Digit1`, `Numpad0`, plus the aliases `1`, `Ctrl`, `Shift`, `Alt`, `Esc`, `Del`, `Ins`, `PgUp`, `PgDn`, `Return` and `` ` ``. |

```csharp
public override void OnUpdate()
{
    if (FruktInput.GetKeyDown(Key.F9))
        Notifications.Show("F9 pressed");
}
```

## KeyBind

A key plus optional modifiers, for configurable hotkeys. Text form: `F8`, `Ctrl+M`, `Shift+Alt+K`.

| Member | Description |
|--------|-------------|
| `new KeyBind(key, ctrl = false, shift = false, alt = false)` | Build one in code. |
| `KeyBind.TryParse(text, out bind)` | Parse text; `false` if it's invalid. |
| `KeyBind.Parse(text, fallback)` | Parse text, using `fallback` (a `Key`) if it's invalid. |
| `WasPressed()` | True on the frame the key was pressed with exactly the required modifiers held. |
| `IsHeld()` | True while the key and modifiers are held. |
| `Key`, `Ctrl`, `Shift`, `Alt` | The parts. |
| `ToString()` | The text form, suitable for saving. |

A typical setup keeps the bind as text in your preferences and lets players rebind it in the mod menu, either with
a `KeyBinding` row or automatically through [`AddPreferencesPage`](mod-menu.md#settings-pages-from-melonpreferences):

```csharp
private MelonPreferences_Entry<string> _toggleKey;

public override void OnInitializeMelon()
{
    var prefs = MelonPreferences.CreateCategory("MyMod", "My Mod settings");
    _toggleKey = prefs.CreateEntry("ToggleKey", "Ctrl+J", "Toggle key");
    ModMenu.AddPreferencesPage(prefs);
}

public override void OnUpdate()
{
    if (KeyBind.Parse(_toggleKey.Value, Key.J).WasPressed())
        ToggleMyFeature();
}
```
