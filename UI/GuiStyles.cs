using UnityEngine;

namespace FruktSharedLibrary.UI
{
    /// <summary>
    /// Shared IMGUI styles and textures for the library's overlays. Exposed so mods drawing their own
    /// OnGUI overlays can match the look. Only use inside OnGUI.
    /// </summary>
    public static class GuiStyles
    {
        private static Texture2D _panel;
        private static Texture2D _panelLight;
        private static Texture2D _accent;
        private static GUIStyle _label;
        private static GUIStyle _header;
        private static GUIStyle _title;
        private static GUIStyle _button;
        private static GUIStyle _notification;

        /// <summary>Accent colour used by the library UI.</summary>
        public static readonly Color AccentColor = new(0.95f, 0.55f, 0.15f, 1f);

        /// <summary>Dark panel background.</summary>
        public static Texture2D Panel => _panel.Exists() ? _panel : _panel = MakeTexture(new Color(0.06f, 0.06f, 0.07f, 0.92f));

        /// <summary>Slightly lighter background for highlighted rows.</summary>
        public static Texture2D PanelLight => _panelLight.Exists() ? _panelLight : _panelLight = MakeTexture(new Color(0.16f, 0.16f, 0.18f, 0.95f));

        /// <summary>Solid accent colour.</summary>
        public static Texture2D Accent => _accent.Exists() ? _accent : _accent = MakeTexture(AccentColor);

        public static GUIStyle Label => _label ??= new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };

        public static GUIStyle Header => _header ??= MakeHeader();

        public static GUIStyle Title => _title ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = true };

        public static GUIStyle Button => _button ??= new GUIStyle(GUI.skin.button) { fontSize = 14, richText = true };

        public static GUIStyle Notification => _notification ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            richText = true,
        };

        /// <summary>Creates a 1x1 texture of a colour (kept alive across scene loads).</summary>
        public static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static GUIStyle MakeHeader()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
            style.normal.textColor = AccentColor;
            return style;
        }

        private static bool Exists(this Object obj) => obj != null;
    }
}
