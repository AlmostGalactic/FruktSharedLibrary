using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using Il2CppServices.UI;
using Il2CppViews.Terminal;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // Mods' own terminal categories: more than one column of tabs, a category with more items than the game's grid
    // has tiles, a name too long for the header, and the three ways a tab gets its icon.
    internal static partial class SelfTest
    {
        private const string ManyCategory = "Self-test many";
        private const string LongCategory = "Self-test category with a long name";
        private const int ManyItems = 27;
        private static ModCategory _iconCategory, _manyCategory, _emptyCategory, _longCategory, _sameAgain, _weapons;
        private static Sprite _categoryIcon;
        private static ModProp _categoryProp;
        private static ModCategory _fitCategory;

        private static void AddTestCategories()
        {
            var texture = new Texture2D(32, 32) { hideFlags = HideFlags.DontUnloadUnusedAsset };
            var pixels = new Color[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(1f, 0.55f, 0.1f);
            texture.SetPixels(pixels);
            texture.Apply();
            _categoryIcon = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            _categoryIcon.hideFlags = HideFlags.DontUnloadUnusedAsset;

            _iconCategory = Inventory.AddCategory("Self-test icon", _categoryIcon);
            _manyCategory = Inventory.AddCategory(ManyCategory);
            _emptyCategory = Inventory.AddCategory("Self-test empty");
            _longCategory = Inventory.AddCategory(LongCategory);
            _sameAgain = Inventory.AddCategory(ManyCategory.ToUpperInvariant());
            _weapons = Inventory.AddCategory("Weapons");
            var mesh = Utilities.Meshes.ParseObj(TestBoxObj, "Self-test category box");
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _categoryProp = (ModProp)Inventory.AddProp("Self-test category box", mesh).WithCategory("Self-test icon");
            // A picture with a small block off in one corner: its tab should show the block large and centred.
            var padded = new Texture2D(64, 64) { hideFlags = HideFlags.DontUnloadUnusedAsset };
            var clear = new Color[64 * 64];
            for (int y = 40; y < 56; y++)
                for (int x = 8; x < 24; x++)
                    clear[y * 64 + x] = new Color(0.2f, 0.8f, 1f);
            padded.SetPixels(clear);
            padded.Apply();
            var paddedIcon = Sprite.Create(padded, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            paddedIcon.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _fitCategory = Inventory.AddCategory("Self-test fit");
            Inventory.AddTool("Self-test small picture", "Self-test fit").WithIcon(paddedIcon);
            for (int i = 1; i <= ManyItems; i++)
                Inventory.AddTool($"Self-test filler {i}", ManyCategory).WithDescription("Fills a category past the game's 24 tiles.");
        }

        private static IEnumerator TestCategories()
        {
            var service = GameServices.TryGet<ITerminalItemsService>();
            List<string> categories = null;
            Section("Mod categories", () =>
            {
                Check("Adding a category twice gives the same one", ReferenceEquals(_sameAgain, _manyCategory));
                categories = Inventory.Categories.ToList();
                Check("The categories are in the terminal", _iconCategory.Added && _manyCategory.Added && _longCategory.Added,
                    string.Join(", ", categories));
                Check("They come after the game's own", categories.IndexOf("Self-test icon") >= 4);
                Check("A category the game has isn't added again", categories.Count(c => c == "Weapons") == 1 && _weapons.Added);
                Check("Items go into a mod's category", _manyCategory.Items.Count == ManyItems, _manyCategory.Items.Count.ToString());
                Check("WithCategory moves a prop out of Props", _categoryProp.Item?.CategoryName == "Self-test icon"
                    && _iconCategory.Items.Contains(_categoryProp.Item), _categoryProp.Item?.CategoryName);
                _testTool.WithCategory("Weapons");
                Check("WithCategory after the first map leaves it where it is", _testTool.Category == "Tools"
                    && _testTool.Item?.CategoryName == "Tools", _testTool.Category);
                Check("Inventory.OpenTerminal", Inventory.OpenTerminal());
            });
            yield return Wait(1.5f);

            var plate = GameServices.FindObject<TerminalItemsPlateView>(true);
            int count = service?.Categories?.Cast<Il2CppSystem.Collections.Generic.IEnumerable<Il2CppData.Player.Inventory.God.IGodInventoryCategoryData>>().ToManagedList().Count ?? 0;
            int many = categories?.IndexOf(ManyCategory) ?? -1;
            RectTransform manySquare = null;
            Section("Mod categories (tabs)", () =>
            {
                var squares = plate?.m_categorySquares;
                Check("The terminal has a tab for each category", squares != null && squares.Length >= count && count >= 8,
                    $"{squares?.Length} tabs, {count} categories");
                if (squares == null)
                    return;
                var shown = Enumerable.Range(0, squares.Length).Where(i => squares[i].gameObject.activeInHierarchy)
                    .Select(i => squares[i].transform.Cast<RectTransform>()).ToList();
                Check("Every category's tab shows", shown.Count == count, $"{shown.Count} of {count}");
                var area = WorldRect(plate.transform.Cast<RectTransform>());
                var field = WorldRect(plate.transform.Find("Field").Cast<RectTransform>());
                var rects = shown.Select(WorldRect).ToList();
                Check("The tabs stay on the plate", rects.All(r => area.Contains(r.min) && area.Contains(r.max)),
                    $"plate {area}, lowest tab {rects.OrderBy(r => r.yMin).First()}");
                bool overlap = false;
                for (int a = 0; a < rects.Count; a++)
                    for (int b = a + 1; b < rects.Count; b++)
                        overlap |= rects[a].Overlaps(rects[b]);
                Check("No two tabs overlap", !overlap);
                Check("The tabs wrap into a second column", rects.Select(r => Mathf.Round(r.x)).Distinct().Count() == 2,
                    string.Join(" ", rects.Select(r => $"({r.x:0},{r.y:0})")));
                Check("The items sit beside the tabs", rects.All(r => r.xMax <= field.xMin), $"field starts at {field.xMin:0}");
                int fit = categories?.IndexOf("Self-test fit") ?? -1;
                var pivot = fit >= 0 && fit < squares.Length ? squares[fit].m_iconDrawer?.m_iconOffsetPivot : null;
                // The block is a quarter of the picture, so it's drawn 3.6 times bigger, and moved 36 units to the
                // right and down to the middle of the 40-unit box.
                Check("An automatic tab icon is scaled to fit and centred", pivot != null && Mathf.Abs(pivot.localScale.x - 3.6f) < 0.2f
                    && Mathf.Abs(pivot.anchoredPosition.x - 36f) < 3f && Mathf.Abs(pivot.anchoredPosition.y + 36f) < 3f,
                    $"scale {pivot?.localScale.x:0.##}, offset {pivot?.anchoredPosition}");
                if (many >= 0 && many < squares.Length)
                    manySquare = squares[many].transform.Cast<RectTransform>();
                Shot("categories-tabs");
            });
            yield return Wait(1f);
            // A real click on a tab the game's prefab didn't have.
            Section("Mod categories (click)", () => Click("category-click", manySquare));
            yield return Wait(2f);
            Section("Mod categories (many items)", () =>
            {
                Check("Clicking an added tab opens its category", Inventory.CategoryName(service?.Category) == ManyCategory,
                    Inventory.CategoryName(service?.Category));
                var tiles = plate?.m_tiles;
                int filled = tiles == null ? 0 : Enumerable.Range(0, tiles.Length).Count(i => tiles[i].gameObject.activeInHierarchy && tiles[i].HoldsItem);
                Check("Every item gets a tile past the game's 24", filled == ManyItems, $"{filled} of {ManyItems}, {tiles?.Length} tiles");
                Check("A tab without an icon shows its first item's", ManyIconMatches(), _manyCategory.Items.FirstOrDefault()?.Icon?.name);
                Shot("categories-many");
                tiles?[ManyItems - 1].EnteredEvent.Invoke(ManyItems - 1);
            });
            yield return Wait(1f);
            Section("Mod categories (hover)", () =>
            {
                var hovered = service?.Hovered;
                Check("Pointing at an added tile shows its item", hovered != null && Inventory.CategoryName(hovered.Category) == ManyCategory,
                    Inventory.Wrap(hovered)?.Name);
                Shot("categories-card");
                plate?.m_tiles[ManyItems - 1].LeftEvent.Invoke(ManyItems - 1);
                service?.SetCategory(FindCategory(service, LongCategory));
            });
            yield return Wait(1.5f);
            Section("Mod categories (long name)", () =>
            {
                var header = plate?.m_header?.m_current;
                Check("A long name fits the header", header != null && header.text.ToUpperInvariant().Contains("LONG NAME")
                    && !header.isTextOverflowing && header.textBounds.size.x <= header.rectTransform.rect.width + 1f,
                    $"'{header?.text}' size {header?.fontSize:0}");
                Check("A tab can have its own icon", IconOf(_iconCategory) == _categoryIcon);
                Check("A tab with no items or icon uses Etc's", IconOf(_emptyCategory)?.name == "etc_category", IconOf(_emptyCategory)?.name);
                Shot("categories-long-name");
                service?.SetCategory(FindCategory(service, "Weapons"));
            });
            yield return Wait(1f);
            Section("Mod categories (closed)", () => Check("Inventory.CloseTerminal", Inventory.CloseTerminal()));
            yield return Wait(1f);
        }

        private static bool ManyIconMatches()
        {
            var first = _manyCategory.Items.FirstOrDefault(i => i.Icon != null)?.Icon;
            return first != null && IconOf(_manyCategory) == first;
        }

        private static Sprite IconOf(ModCategory category)
        {
            var data = ModCategories.DataFor(category);
            return data?.m_descriptor?.m_iconData?.m_sprite;
        }

        private static Il2CppData.Player.Inventory.God.IGodInventoryCategoryData FindCategory(ITerminalItemsService service, string name)
        {
            var all = service?.Categories?.Cast<Il2CppSystem.Collections.Generic.IEnumerable<Il2CppData.Player.Inventory.God.IGodInventoryCategoryData>>().ToManagedList();
            return all?.FirstOrDefault(c => Inventory.CategoryName(c) == name);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
