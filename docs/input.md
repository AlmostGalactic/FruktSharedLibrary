# Input

FRUKT uses Unity's newer Input System, and the old `UnityEngine.Input` class may not work in it. The helpers in
`FruktSharedLibrary.Controls` read the Input System, and only use the old class if they have to. Keys are
`UnityEngine.InputSystem.Key` values.

## FruktInput

| Member | Description |
|--------|-------------|
| `GetKeyDown(key)`, `GetKey(key)`, `GetKeyUp(key)` | Pressed this frame, held down, released this frame. |
| `CtrlHeld`, `ShiftHeld`, `AltHeld` | Whether either Ctrl, Shift or Alt key is down. |
| `GetMouseButtonDown(button)`, `GetMouseButton(button)` | 0 is left, 1 is right, 2 is middle. |
| `MousePosition` | In screen pixels, counted from the bottom left. |
| `ScrollDelta` | How far the mouse wheel moved this frame. |
| `TryGetPressedKey(out key, includeModifiers = false)` | The first key pressed this frame, for "press a key" prompts. Modifier keys are ignored unless you ask for them, so Ctrl+K gives you K. |
| `IsModifier(key)` | Whether the key is Ctrl, Shift, Alt or the Windows/Command key. |
| `TryParseKey(text, out key)` | Reads a key name like `F8`, `Insert`, `Digit1` or `Numpad0`. It also understands `1`, `Ctrl`, `Shift`, `Alt`, `Esc`, `Del`, `Ins`, `PgUp`, `PgDn`, `Return` and `` ` ``. |

```csharp
public override void OnUpdate()
{
    if (FruktInput.GetKeyDown(Key.F9))
        Notifications.Show("F9 pressed");
}
```

## KeyBind

A key with optional Ctrl, Shift or Alt, for hotkeys that players can change. As text it looks like `F8`, `Ctrl+M`
or `Shift+Alt+K`.

| Member | Description |
|--------|-------------|
| `new KeyBind(key, ctrl = false, shift = false, alt = false)` | Makes one in code. |
| `KeyBind.TryParse(text, out bind)` | Reads one from text. Returns `false` if the text isn't valid. |
| `KeyBind.Parse(text, fallback)` | Reads one from text, and uses the `fallback` key if the text isn't valid. |
| `WasPressed()` | True on the frame the key goes down with exactly the right modifiers held. |
| `IsHeld()` | True while the key and its modifiers are held down. |
| `Key`, `Ctrl`, `Shift`, `Alt` | Its parts. |
| `ToString()` | The text version, which you can save. |

The usual way to do it is to store the key as text in your preferences, so players can rebind it in the mod
menu. [`AddPreferencesPage`](mod-menu.md#settings-pages-from-melonpreferences) turns it into a key binding row
for you, or you can add a `KeyBinding` row yourself.

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
