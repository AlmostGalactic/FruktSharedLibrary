using System;
using System.Collections.Generic;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.UI;
using Il2CppData.Icons;
using Il2CppData.Objects;
using Il2CppData.Player.Inventory.God;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppServices.UI;
using Il2CppUI.Terminal;
using Il2CppViews.Terminal;
using UnityEngine;
using UnityEngine.UI;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// Mods' own terminal categories. Each is added to the game's inventory layout (the asset its tabs come from),
    /// and the terminal is given enough tab squares and item tiles to draw them: the game's own prefab has six
    /// squares and 24 tiles, and refuses to draw more. Tabs that don't fit in one column go into more columns, and
    /// the plate widens to make room.
    /// </summary>
    internal static class ModCategories
    {
        private static readonly List<ModCategory> Categories = new();
        private static readonly Dictionary<ModCategory, SerializedGodInventoryCategoryData> Data = new();
        private static readonly Dictionary<ModCategory, SerializedIconData> Icons = new();
        private static SerializedGodInventoryLayout _layout;
        private static float _nextLook;

        internal static IReadOnlyList<ModCategory> All => Categories;

        internal static ModCategory Add(string name, Sprite icon)
        {
            var existing = Find(name);
            if (existing != null)
            {
                if (icon != null)
                    existing.WithIcon(icon);
                return existing;
            }
            var category = new ModCategory(name, icon);
            Categories.Add(category);
            return category;
        }

        internal static ModCategory Find(string name)
            => Categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        internal static void Update()
        {
            if (Categories.Count == 0 || Time.unscaledTime < _nextLook || GameState.Phase == GamePhase.Booting)
                return;
            _nextLook = Time.unscaledTime + 1f;
            AddToLayout();
        }

        /// <summary>The game's data for a category, made the first time it's asked for.</summary>
        internal static SerializedGodInventoryCategoryData DataFor(ModCategory category)
        {
            if (Data.TryGetValue(category, out var data) && data != null && !data.WasCollected)
                return data;
            var icon = ScriptableObject.CreateInstance<SerializedIconData>();
            icon.name = category.Name + " icon";
            icon.hideFlags = HideFlags.HideAndDontSave;
            icon.m_offset = Vector2.zero;
            icon.m_scale = Vector2.one;
            icon.m_color = Color.white;
            var descriptor = ScriptableObject.CreateInstance<SerializedObjectDescriptorWithIcon>();
            descriptor.name = category.Name + " descriptor";
            descriptor.hideFlags = HideFlags.HideAndDontSave;
            descriptor.m_objectName = category.Name;
            descriptor.m_description = "";
            descriptor.m_iconData = icon;
            data = ScriptableObject.CreateInstance<SerializedGodInventoryCategoryData>();
            data.name = category.Name;
            data.hideFlags = HideFlags.HideAndDontSave;
            data.m_descriptor = descriptor;
            data.m_id = "";
            Data[category] = data;
            Icons[category] = icon;
            RefreshIcons();
            return data;
        }

        /// <summary>
        /// Adds the categories the layout doesn't have yet, after the game's own. A category named like one the
        /// game already has (two mods asking for the same one, or "Weapons") isn't added twice.
        /// </summary>
        internal static void AddToLayout()
        {
            try
            {
                var layout = FindLayout();
                if (layout == null)
                    return;
                var current = layout.m_categories;
                var list = new List<SerializedGodInventoryCategoryData>();
                if (current != null)
                {
                    for (int i = 0; i < current.Length; i++)
                        list.Add(current[i]);
                }
                bool changed = false;
                foreach (var category in Categories)
                {
                    var data = DataFor(category);
                    bool there = list.Any(c => c != null && (c.Pointer == data.Pointer
                        || string.Equals(Inventory.CategoryName(c), category.Name, StringComparison.OrdinalIgnoreCase)));
                    if (!there)
                    {
                        list.Add(data);
                        changed = true;
                        FruktLog.Msg($"Added the category '{category.Name}' to the terminal.");
                    }
                    category.Added = true;
                }
                if (changed)
                    layout.m_categories = new Il2CppReferenceArray<SerializedGodInventoryCategoryData>(list.ToArray());
            }
            catch (Exception e)
            {
                FruktLog.Debug("Adding categories to the terminal failed: " + e.Message);
            }
        }

        private static SerializedGodInventoryLayout FindLayout()
        {
            var service = GameServices.TryGet<ITerminalItemsService>()?.TryCast<TerminalItemsService>();
            var layout = service?.Layout;
            if (layout != null)
                return _layout = layout;
            if (_layout != null && !_layout.WasCollected)
                return _layout;
            var found = Resources.FindObjectsOfTypeAll<SerializedGodInventoryLayout>();
            return _layout = found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// Points each category's tab icon at its own sprite, or, without one, the first of its items' icons, or
        /// the Etc tab's icon.
        /// </summary>
        internal static void RefreshIcons()
        {
            if (Icons.Count == 0)
                return;
            List<InventoryItem> items = null;
            SerializedIconData etc = null;
            foreach (var category in Categories)
            {
                if (!Icons.TryGetValue(category, out var icon) || icon == null || icon.WasCollected)
                    continue;
                try
                {
                    if (category.Icon != null)
                    {
                        Set(icon, category.Icon, Vector2.zero, Vector2.one, Color.white);
                        continue;
                    }
                    items ??= Inventory.Items.ToList();
                    var first = items.FirstOrDefault(i => i.Icon != null
                        && string.Equals(i.CategoryName, category.Name, StringComparison.OrdinalIgnoreCase));
                    if (first != null)
                    {
                        var (offset, scale) = FitToBox(first.Icon);
                        Set(icon, first.Icon, offset, scale, Color.white);
                        continue;
                    }
                    etc ??= EtcIcon();
                    if (etc != null)
                        Set(icon, etc.m_sprite, etc.m_offset, etc.m_scale, etc.m_color);
                }
                catch (Exception e)
                {
                    FruktLog.Debug($"Picking an icon for the category '{category.Name}' failed: {e.Message}");
                }
            }
        }

        private static void Set(SerializedIconData icon, Sprite sprite, Vector2 offset, Vector2 scale, Color color)
        {
            icon.m_sprite = sprite;
            icon.m_offset = offset;
            icon.m_scale = scale;
            icon.m_color = color;
        }

        // The icon's box on a tab, in the terminal's units. Read from the terminal once it's drawn.
        private static float _iconBox = 40f;
        // The game draws its own tab icons at nine tenths of the box.
        private const float IconFill = 0.9f;
        // By the sprite's native object and name: GetInstanceID gives every sprite the same number through the
        // interop.
        private static readonly Dictionary<(IntPtr, string), Rect> Content = new();

        /// <summary>
        /// The offset and scale that make the visible part of an item's picture fill a tab's icon box, centred.
        /// Item pictures have empty space around them that the game's tab icons don't.
        /// </summary>
        internal static (Vector2 Offset, Vector2 Scale) FitToBox(Sprite sprite)
        {
            var content = ContentOf(sprite);
            float largest = Mathf.Max(content.width, content.height);
            if (largest <= 0.01f)
                return (Vector2.zero, Vector2.one);
            float scale = Mathf.Min(IconFill / largest, 6f);
            var off = (content.center - new Vector2(0.5f, 0.5f)) * _iconBox * scale;
            return (-off, new Vector2(scale, scale));
        }

        /// <summary>
        /// The part of a sprite that isn't see-through, as fractions of the sprite (0 to 1). Pictures that can't
        /// be read (most from bundles, and the game's own) count as filled.
        /// </summary>
        internal static Rect ContentOf(Sprite sprite)
        {
            var whole = new Rect(0f, 0f, 1f, 1f);
            if (sprite == null || sprite.texture == null)
                return whole;
            if (Content.TryGetValue((sprite.Pointer, sprite.name), out var known))
                return known;
            var result = whole;
            try
            {
                var texture = sprite.texture;
                if (!texture.isReadable)
                    return Remember(sprite, whole);
                // The whole picture as the terminal draws it. A sprite's textureRect can be cropped to its visible
                // part, unless it's packed in an atlas.
                var area = sprite.packed ? sprite.textureRect : sprite.rect;
                int x0 = Mathf.FloorToInt(area.x), y0 = Mathf.FloorToInt(area.y);
                int width = Mathf.Max(1, Mathf.RoundToInt(area.width)), height = Mathf.Max(1, Mathf.RoundToInt(area.height));
                // GetPixel one at a time: the whole-array reads (GetPixels32) corrupt memory through the interop.
                // Every pixel of a small picture, a grid of about 64 x 64 of a big one.
                int step = Math.Max(1, Math.Max(width, height) / 64);
                int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                for (int y = 0; y < height; y += step)
                {
                    for (int x = 0; x < width; x += step)
                    {
                        if (texture.GetPixel(x0 + x, y0 + y).a < 0.16f)
                            continue;
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x + step);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y + step);
                    }
                }
                if (maxX >= 0)
                    result = Rect.MinMaxRect(minX / (float)width, minY / (float)height,
                        Math.Min(maxX, width) / (float)width, Math.Min(maxY, height) / (float)height);
            }
            catch (Exception e)
            {
                FruktLog.Debug($"Measuring the icon '{sprite.name}' failed: {e.Message}");
            }
            return Remember(sprite, result);
        }

        private static Rect Remember(Sprite sprite, Rect content)
        {
            Content[(sprite.Pointer, sprite.name)] = content;
            return content;
        }

        private static SerializedIconData EtcIcon()
        {
            var categories = FindLayout()?.m_categories;
            if (categories == null)
                return null;
            for (int i = 0; i < categories.Length; i++)
            {
                if (string.Equals(Inventory.CategoryName(categories[i]), "Etc", StringComparison.OrdinalIgnoreCase))
                    return categories[i].m_descriptor?.m_iconData;
            }
            return null;
        }

        // ------------------------------------------------------------ fitting the terminal

        private static TerminalItemsPlateView _plate;
        private static Plate _authored;
        private static readonly List<IDisposable> Wiring = new();

        // How the plate looked before anything was moved, so it can go back.
        private sealed class Plate
        {
            internal float StripWidth, PlateWidth, FieldX;
            internal bool Wrapped;
        }

        /// <summary>Gives the plate a square for each of <paramref name="count"/> categories, and lays them out.</summary>
        internal static void FitCategories(TerminalItemsPlateView plate, int count)
        {
            var state = Prepare(plate);
            if (state == null)
                return;
            AddToLayout();
            var squares = plate.m_categorySquares;
            if (squares == null || squares.Length == 0)
                return;
            var box = squares[0].m_iconDrawer?.m_iconOffsetPivot;
            if (box != null && box.rect.width > 1f)
                _iconBox = box.rect.width;
            RefreshIcons();
            if (count > squares.Length)
            {
                var list = new List<TerminalCategorySquareView>();
                for (int i = 0; i < squares.Length; i++)
                    list.Add(squares[i]);
                var prototype = squares[squares.Length - 1];
                for (int i = squares.Length; i < count; i++)
                {
                    var copy = FruktUi.CloneGameUi(prototype.gameObject, prototype.transform.parent, $"CategorySquare ({i})");
                    var square = copy?.GetComponent<TerminalCategorySquareView>();
                    if (square == null)
                        break;
                    square.TakeIndex(i);
                    Wiring.Add(square.OnClicked.Listen(index => plate.CategoryClickedEvent?.Invoke(index)));
                    list.Add(square);
                }
                plate.m_categorySquares = new Il2CppReferenceArray<TerminalCategorySquareView>(list.ToArray());
                squares = plate.m_categorySquares;
            }
            Arrange(plate, state, squares, count);
        }

        /// <summary>Gives the plate enough tiles for <paramref name="count"/> items. The field already scrolls.</summary>
        internal static void FitItems(TerminalItemsPlateView plate, int count)
        {
            if (Prepare(plate) == null)
                return;
            var tiles = plate.m_tiles;
            if (tiles == null || tiles.Length == 0)
                return;
            int columns = Math.Max(1, plate.m_columns);
            // Whole rows, plus one spare row in case the game rounds differently.
            int needed = Math.Max(columns * plate.m_restingRows, (count + columns - 1) / columns * columns + columns);
            if (needed <= tiles.Length)
                return;
            var list = new List<TerminalItemTileView>();
            for (int i = 0; i < tiles.Length; i++)
                list.Add(tiles[i]);
            var prototype = tiles[tiles.Length - 1];
            for (int i = tiles.Length; i < needed; i++)
            {
                var copy = FruktUi.CloneGameUi(prototype.gameObject, prototype.transform.parent, $"ItemTile ({i})");
                var tile = copy?.GetComponent<TerminalItemTileView>();
                if (tile == null)
                    break;
                tile.TakeIndex(i);
                Wiring.Add(tile.OnEntered.Listen(index => plate.TakeTilePointer(index)));
                Wiring.Add(tile.OnLeft.Listen(index => plate.DropTilePointer(index)));
                list.Add(tile);
            }
            plate.m_tiles = new Il2CppReferenceArray<TerminalItemTileView>(list.ToArray());
        }

        private static Plate Prepare(TerminalItemsPlateView plate)
        {
            if (plate == null || plate.WasCollected)
                return null;
            if (_plate != null && !_plate.WasCollected && _plate.Pointer == plate.Pointer && _authored != null)
                return _authored;
            // A new terminal (each map makes one).
            Wiring.Clear();
            _plate = plate;
            _authored = new Plate();
            try
            {
                var squares = plate.m_categorySquares;
                var strip = squares != null && squares.Length > 0 ? squares[0].transform.parent.TryCast<RectTransform>() : null;
                var rect = plate.transform.TryCast<RectTransform>();
                var field = plate.transform.Find("Field")?.TryCast<RectTransform>();
                _authored.StripWidth = strip != null ? strip.sizeDelta.x : 0f;
                _authored.PlateWidth = rect != null ? rect.sizeDelta.x : 0f;
                _authored.FieldX = field != null ? field.anchoredPosition.x : 0f;
                // Long category names shrink to fit the header instead of running off the plate.
                var header = plate.m_header?.m_current;
                if (header != null)
                {
                    float size = header.fontSize;
                    header.enableAutoSizing = true;
                    header.fontSizeMax = size;
                    header.fontSizeMin = Mathf.Min(size, 24f);
                }
            }
            catch (Exception e)
            {
                FruktLog.Debug("Reading the terminal's layout failed: " + e.Message);
            }
            return _authored;
        }

        // One column while they fit; otherwise more columns, filled top to bottom, with the plate made wider.
        private static void Arrange(TerminalItemsPlateView plate, Plate state, Il2CppReferenceArray<TerminalCategorySquareView> squares, int count)
        {
            var strip = squares[0].transform.parent.TryCast<RectTransform>();
            var first = squares[0].transform.TryCast<RectTransform>();
            if (strip == null || first == null)
                return;
            var group = strip.GetComponent<VerticalLayoutGroup>();
            float spacing = group != null ? group.spacing : 10f;
            float size = first.rect.height > 1f ? first.rect.height : 60f;
            float step = size + spacing;
            int perColumn = Math.Max(1, Mathf.FloorToInt((strip.rect.height + spacing + 0.5f) / step));
            int columns = Math.Max(1, (count + perColumn - 1) / perColumn);
            if (columns == 1 && !state.Wrapped)
                return;

            float extra = (columns - 1) * step;
            var rect = plate.transform.TryCast<RectTransform>();
            var field = plate.transform.Find("Field")?.TryCast<RectTransform>();
            SetWidth(strip, state.StripWidth + extra);
            SetWidth(rect, state.PlateWidth + extra);
            if (field != null)
                field.anchoredPosition = new Vector2(state.FieldX + extra, field.anchoredPosition.y);

            if (columns == 1)
            {
                if (group != null)
                    group.enabled = true;
                state.Wrapped = false;
                LayoutRebuilder.MarkLayoutForRebuild(strip);
                return;
            }
            if (group != null)
                group.enabled = false;
            state.Wrapped = true;
            for (int i = 0; i < squares.Length; i++)
            {
                var square = squares[i].transform.TryCast<RectTransform>();
                if (square == null)
                    continue;
                int column = i / perColumn, row = i % perColumn;
                square.anchorMin = square.anchorMax = new Vector2(0f, 1f);
                square.sizeDelta = new Vector2(size, size);
                square.anchoredPosition = new Vector2(column * step + size * square.pivot.x, -(row * step + size * (1f - square.pivot.y)));
            }
        }

        private static void SetWidth(RectTransform rect, float width)
        {
            if (rect != null && width > 0f)
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }
    }
}
