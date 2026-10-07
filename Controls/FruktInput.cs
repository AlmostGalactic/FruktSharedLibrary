using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FruktSharedLibrary.Controls
{
    /// <summary>
    /// Keyboard and mouse reading that works in FRUKT. The game uses Unity's new Input System, where the old
    /// <c>UnityEngine.Input</c> class may be disabled; this goes through the Input System and only falls back
    /// to the old API if it has to.
    /// </summary>
    public static class FruktInput
    {
        private static bool _legacyBroken;

        /// <summary>True on the frame the key was pressed.</summary>
        public static bool GetKeyDown(Key key) => Read(key, c => c.wasPressedThisFrame, UnityEngine.Input.GetKeyDown);

        /// <summary>True while the key is held.</summary>
        public static bool GetKey(Key key) => Read(key, c => c.isPressed, UnityEngine.Input.GetKey);

        /// <summary>True on the frame the key was released.</summary>
        public static bool GetKeyUp(Key key) => Read(key, c => c.wasReleasedThisFrame, UnityEngine.Input.GetKeyUp);

        /// <summary>True while either Ctrl key is held.</summary>
        public static bool CtrlHeld => GetKey(Key.LeftCtrl) || GetKey(Key.RightCtrl);

        /// <summary>True while either Shift key is held.</summary>
        public static bool ShiftHeld => GetKey(Key.LeftShift) || GetKey(Key.RightShift);

        /// <summary>True while either Alt key is held.</summary>
        public static bool AltHeld => GetKey(Key.LeftAlt) || GetKey(Key.RightAlt);

        /// <summary>True on the frame a mouse button was pressed (0 = left, 1 = right, 2 = middle).</summary>
        public static bool GetMouseButtonDown(int button)
        {
            var control = MouseButton(button);
            return control != null && control.wasPressedThisFrame;
        }

        /// <summary>True while a mouse button is held (0 = left, 1 = right, 2 = middle).</summary>
        public static bool GetMouseButton(int button)
        {
            var control = MouseButton(button);
            return control != null && control.isPressed;
        }

        /// <summary>Mouse position in screen pixels (origin bottom-left).</summary>
        public static Vector2 MousePosition
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            }
        }

        /// <summary>Mouse wheel movement this frame.</summary>
        public static float ScrollDelta
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null ? mouse.scroll.ReadValue().y : 0f;
            }
        }

        /// <summary>
        /// The first key pressed this frame (for "press a key to bind" prompts). Modifier keys are skipped
        /// unless <paramref name="includeModifiers"/> is set, so Ctrl+K reports K.
        /// </summary>
        public static bool TryGetPressedKey(out Key key, bool includeModifiers = false)
        {
            key = Key.None;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;
            foreach (var candidate in AllKeys)
            {
                if (!includeModifiers && IsModifier(candidate))
                    continue;
                try
                {
                    var control = keyboard[candidate];
                    if (control == null || !control.wasPressedThisFrame)
                        continue;
                }
                catch
                {
                    continue; // Keys this keyboard layout doesn't have.
                }
                key = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The characters typed this frame, for text boxes: letters, digits, space and common punctuation, with
        /// Shift (US keyboard layout). Keys that don't type anything, like Backspace or Enter, aren't included.
        /// </summary>
        public static string GetTypedText()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return string.Empty;
            bool shift = ShiftHeld;
            var typed = new System.Text.StringBuilder();
            foreach (var (key, plain, shifted) in TypingKeys)
            {
                try
                {
                    var control = keyboard[key];
                    if (control != null && control.wasPressedThisFrame)
                        typed.Append(shift ? shifted : plain);
                }
                catch
                {
                    // Keys this keyboard layout doesn't have.
                }
            }
            return typed.ToString();
        }

        private static readonly (Key Key, char Plain, char Shifted)[] TypingKeys = BuildTypingKeys();

        private static (Key, char, char)[] BuildTypingKeys()
        {
            var keys = new List<(Key, char, char)>();
            for (var key = Key.A; key <= Key.Z; key++)
            {
                char letter = (char)('a' + (key - Key.A));
                keys.Add((key, letter, char.ToUpperInvariant(letter)));
            }
            const string digits = "1234567890", shiftedDigits = "!@#$%^&*()";
            for (int i = 0; i < 10; i++)
                keys.Add((Key.Digit1 + i, digits[i], shiftedDigits[i]));
            for (int i = 0; i < 10; i++)
                keys.Add((Key.Numpad0 + i, (char)('0' + i), (char)('0' + i)));
            keys.Add((Key.Space, ' ', ' '));
            keys.Add((Key.Minus, '-', '_'));
            keys.Add((Key.Equals, '=', '+'));
            keys.Add((Key.Period, '.', '>'));
            keys.Add((Key.Comma, ',', '<'));
            keys.Add((Key.Slash, '/', '?'));
            keys.Add((Key.Semicolon, ';', ':'));
            keys.Add((Key.Quote, '\'', '"'));
            keys.Add((Key.LeftBracket, '[', '{'));
            keys.Add((Key.RightBracket, ']', '}'));
            return keys.ToArray();
        }

        /// <summary>True for Ctrl, Shift, Alt and the Windows/Command keys.</summary>
        public static bool IsModifier(Key key)
            => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
                or Key.LeftMeta or Key.RightMeta;

        private static readonly Key[] AllKeys = Array.FindAll((Key[])Enum.GetValues(typeof(Key)), k => k != Key.None && k != Key.IMESelected);

        /// <summary>
        /// Parses a key name. Accepts Input System names (F8, Insert, Digit1, Numpad0, LeftCtrl) plus a few
        /// friendly aliases ("1", "Ctrl", "Shift", "Alt", "Esc", "Del", "PgUp").
        /// </summary>
        public static bool TryParseKey(string text, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            text = text.Trim();
            if (Aliases.TryGetValue(text, out key))
                return true;
            if (text.Length == 1 && char.IsDigit(text[0]))
                return Enum.TryParse("Digit" + text, out key);
            return Enum.TryParse(text, true, out key) && key != Key.None;
        }

        private static readonly Dictionary<string, Key> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ctrl"] = Key.LeftCtrl,
            ["Control"] = Key.LeftCtrl,
            ["Shift"] = Key.LeftShift,
            ["Alt"] = Key.LeftAlt,
            ["Esc"] = Key.Escape,
            ["Del"] = Key.Delete,
            ["Ins"] = Key.Insert,
            ["PgUp"] = Key.PageUp,
            ["PgDn"] = Key.PageDown,
            ["Return"] = Key.Enter,
            ["`"] = Key.Backquote,
            ["~"] = Key.Backquote,
        };

        private static bool Read(Key key, Func<KeyControl, bool> read, Func<KeyCode, bool> legacy)
        {
            if (key == Key.None)
                return false;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                try
                {
                    var control = keyboard[key];
                    return control != null && read(control);
                }
                catch
                {
                    // Fall through to the legacy API.
                }
            }
            if (_legacyBroken)
                return false;
            try
            {
                return Enum.TryParse(key.ToString(), out KeyCode code) && legacy(code);
            }
            catch
            {
                _legacyBroken = true;
                return false;
            }
        }

        private static ButtonControl MouseButton(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return null;
            return button switch
            {
                0 => mouse.leftButton,
                1 => mouse.rightButton,
                2 => mouse.middleButton,
                3 => mouse.backButton,
                4 => mouse.forwardButton,
                _ => null,
            };
        }
    }

    /// <summary>A key plus optional modifiers, parsed from text such as "F8", "Ctrl+M" or "Shift+Alt+K".</summary>
    public sealed class KeyBind
    {
        public Key Key { get; }
        public bool Ctrl { get; }
        public bool Shift { get; }
        public bool Alt { get; }

        public KeyBind(Key key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        /// <summary>Parses "F8", "Ctrl+M", "Shift+Alt+K"... Returns false for invalid text.</summary>
        public static bool TryParse(string text, out KeyBind bind)
        {
            bind = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            bool ctrl = false, shift = false, alt = false;
            Key key = Key.None;
            foreach (var raw in text.Split('+'))
            {
                var part = raw.Trim();
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                    ctrl = true;
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    shift = true;
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    alt = true;
                else if (!FruktInput.TryParseKey(part, out key))
                    return false;
            }
            if (key == Key.None)
                return false;
            bind = new KeyBind(key, ctrl, shift, alt);
            return true;
        }

        /// <summary>Parses text, falling back to <paramref name="fallback"/> when it's invalid.</summary>
        public static KeyBind Parse(string text, Key fallback) => TryParse(text, out var bind) ? bind : new KeyBind(fallback);

        /// <summary>True on the frame the key was pressed with exactly the required modifiers held.</summary>
        public bool WasPressed()
            => FruktInput.GetKeyDown(Key)
               && FruktInput.CtrlHeld == Ctrl
               && FruktInput.ShiftHeld == Shift
               && FruktInput.AltHeld == Alt;

        /// <summary>True while the key (and modifiers) are held.</summary>
        public bool IsHeld()
            => FruktInput.GetKey(Key) && (!Ctrl || FruktInput.CtrlHeld) && (!Shift || FruktInput.ShiftHeld) && (!Alt || FruktInput.AltHeld);

        public override string ToString()
            => (Ctrl ? "Ctrl+" : "") + (Shift ? "Shift+" : "") + (Alt ? "Alt+" : "") + Key;
    }
}
