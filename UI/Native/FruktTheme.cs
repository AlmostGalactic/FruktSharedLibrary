using System;
using Il2CppTMPro;
using UnityEngine;

namespace FruktSharedLibrary.UI
{
    /// <summary>
    /// FRUKT's visual language, measured from the game's own menus: colours, fonts and sprites.
    /// Use it to make mod UI that looks native.
    /// </summary>
    public static class FruktTheme
    {
        /// <summary>Panel background (#1C1C1C).</summary>
        public static readonly Color Background = Hex(0x1C1C1C);
        /// <summary>Main text and the cap strip on top of panels (#CDCDCD).</summary>
        public static readonly Color Text = Hex(0xCDCDCD);
        /// <summary>Secondary values such as slider readouts (#787878).</summary>
        public static readonly Color Muted = Hex(0x787878);
        /// <summary>Dim text: breadcrumbs, console lines, "off" (#4A4A4A).</summary>
        public static readonly Color Dim = Hex(0x4A4A4A);
        /// <summary>Darker separators (#2F2F2F).</summary>
        public static readonly Color Line = Hex(0x2F2F2F);
        /// <summary>The orange accent used by sliders, toggles and chosen options (#FFA600).</summary>
        public static readonly Color Accent = Hex(0xFFA600);
        /// <summary>Toggle box frame (#9E9E9E).</summary>
        public static readonly Color Frame = Hex(0x9E9E9E);

        /// <summary>Big pixel font used for titles and menu lines ("GNF").</summary>
        public static TMP_FontAsset DisplayFont => _display ??= FindFont("GNF SDF");

        /// <summary>Monospace font used for setting labels, values and console text ("Departure Mono").</summary>
        public static TMP_FontAsset MonoFont => _mono ??= FindFont("Departure Mono SDF");

        /// <summary>The thin square border sprite used by toggles and key chips (null if unavailable).</summary>
        public static Sprite BorderSprite => _border ??= FindSprite("Free Flat Border Single 4pt Icon");

        /// <summary>True when the game's fonts were found (they are loaded with the first scene).</summary>
        public static bool Available => DisplayFont != null && MonoFont != null;

        private static TMP_FontAsset _display;
        private static TMP_FontAsset _mono;
        private static Sprite _border;

        internal static void Reset()
        {
            if (_display == null) _display = null;
            if (_mono == null) _mono = null;
            if (_border == null) _border = null;
        }

        private static TMP_FontAsset FindFont(string name)
        {
            try
            {
                TMP_FontAsset fallback = null;
                foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                {
                    if (font == null)
                        continue;
                    if (font.name == name)
                        return font;
                    if (font.name.StartsWith("LiberationSans", StringComparison.Ordinal) && fallback == null)
                        fallback = font;
                }
                return fallback;
            }
            catch
            {
                return null;
            }
        }

        private static Sprite FindSprite(string name)
        {
            try
            {
                foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (sprite != null && sprite.name == name)
                        return sprite;
                }
            }
            catch
            {
                // Sprites aren't critical.
            }
            return null;
        }

        private static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
