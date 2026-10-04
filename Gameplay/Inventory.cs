using System;
using System.Collections.Generic;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using Il2CppData.Player.Inventory.God;
using Il2CppServices.Player;
using Il2CppServices.UI;
using UnityEngine;

namespace FruktSharedLibrary.Gameplay
{
    /// <summary>
    /// One item in the game's inventory: what the terminal lists and what goes in the toolbar's slots (the guns,
    /// the cutter, the human spawner, every prop). Get them from <see cref="Inventory"/> or <see cref="Toolbar"/>.
    /// </summary>
    public sealed class InventoryItem
    {
        internal InventoryItem(IGodInventoryItemData data)
        {
            Data = data;
        }

        /// <summary>The game's own item data.</summary>
        public IGodInventoryItemData Data { get; }

        /// <summary>The name the terminal shows, like "Viper-17".</summary>
        public string Name
        {
            get
            {
                try
                {
                    string name = Data.TryCast<GodInventoryItemData>()?.ObjectName;
                    return string.IsNullOrEmpty(name) ? Id : name;
                }
                catch
                {
                    return Id;
                }
            }
        }

        /// <summary>The description on the item's card in the terminal.</summary>
        public string Description
        {
            get
            {
                try
                {
                    return Data.TryCast<GodInventoryItemData>()?.Description ?? "";
                }
                catch
                {
                    return "";
                }
            }
        }

        /// <summary>The game's ID for the item, which stays the same across languages and versions.</summary>
        public string Id
        {
            get
            {
                try
                {
                    return Data.ID?.ID ?? "";
                }
                catch
                {
                    return "";
                }
            }
        }

        /// <summary>
        /// The terminal category it's filed under, like "weapons" (see <see cref="Inventory.Categories"/>). This is the
        /// category's ID when it has one; the game's own categories don't, so it's their name.
        /// </summary>
        public string Category => Inventory.CategoryId(SafeCategory());

        /// <summary>The name of that category, as the terminal shows it.</summary>
        public string CategoryName => Inventory.CategoryName(SafeCategory());

        /// <summary>The rows on the item's card in the terminal, like ("caliber", "9mm").</summary>
        public IReadOnlyList<KeyValuePair<string, string>> CardRows
        {
            get
            {
                var rows = new List<KeyValuePair<string, string>>();
                try
                {
                    var source = Data.CardRows;
                    if (source != null)
                    {
                        foreach (var row in source.ToManagedList())
                            rows.Add(new KeyValuePair<string, string>(row.Key, row.Value));
                    }
                }
                catch (Exception e)
                {
                    FruktLog.Debug($"Reading the card of '{Id}' failed: {e.Message}");
                }
                return rows;
            }
        }

        /// <summary>The item's icon in the terminal and toolbar.</summary>
        public Sprite Icon
        {
            get
            {
                try
                {
                    return Data.TryCast<GodInventoryItemData>()?.IconData?.Sprite;
                }
                catch
                {
                    return null;
                }
            }
        }

        private IGodInventoryCategoryData SafeCategory()
        {
            try
            {
                return Data.Category;
            }
            catch
            {
                return null;
            }
        }

        public override bool Equals(object obj) => obj is InventoryItem other && other.Data.Pointer == Data.Pointer;

        public override int GetHashCode() => Data.Pointer.GetHashCode();

        public override string ToString() => Name;
    }

    /// <summary>
    /// The game's inventory: every item the terminal can hand out (guns, tools and props), filed under its
    /// categories. To give the player an item, put it on the <see cref="Toolbar"/>. The items are registered when
    /// the game starts, so this works on the main menu too; opening and closing the terminal needs a map.
    /// </summary>
    public static class Inventory
    {
        private static readonly Dictionary<IntPtr, InventoryItem> Wrappers = new();

        /// <summary>Every item in the inventory, in the order the game registered them.</summary>
        public static IReadOnlyList<InventoryItem> Items
        {
            get
            {
                var result = new List<InventoryItem>();
                var registry = GameServices.TryGet<INativeGodInventoryItemsHandler>();
                if (registry == null)
                    return result;
                try
                {
                    foreach (var data in registry.GodInventoryItemsData.ToManagedList())
                    {
                        var item = Wrap(data);
                        if (item != null)
                            result.Add(item);
                    }
                }
                catch (Exception e)
                {
                    FruktLog.Warning($"Reading the inventory failed: {e.Message}");
                }
                return result;
            }
        }

        /// <summary>
        /// The terminal's categories (see <see cref="InventoryItem.Category"/>), in the order its tabs show them once
        /// a map has loaded; before that, in the order the items use them.
        /// </summary>
        public static IReadOnlyList<string> Categories
        {
            get
            {
                var result = new List<string>();
                try
                {
                    var terminal = GameServices.TryGet<ITerminalItemsService>();
                    if (terminal?.Categories != null)
                    {
                        foreach (var category in terminal.Categories.ToManagedList())
                            AddOnce(result, CategoryId(category));
                    }
                }
                catch (Exception e)
                {
                    FruktLog.Debug($"Reading the terminal's categories failed: {e.Message}");
                }
                foreach (var item in Items)
                    AddOnce(result, item.Category);
                return result;
            }
        }

        /// <summary>
        /// Finds an item by its name ("Viper-17") or ID, ignoring case. Returns null if there's no such item.
        /// </summary>
        public static InventoryItem Find(string nameOrId)
        {
            if (string.IsNullOrWhiteSpace(nameOrId))
                return null;
            var items = Items;
            return items.FirstOrDefault(i => string.Equals(i.Name, nameOrId, StringComparison.OrdinalIgnoreCase))
                ?? items.FirstOrDefault(i => string.Equals(i.Id, nameOrId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The items filed under a category (by its ID or name, ignoring case).</summary>
        public static IReadOnlyList<InventoryItem> ItemsIn(string category)
            => Items.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(i.CategoryName, category, StringComparison.OrdinalIgnoreCase)).ToList();

        /// <summary>
        /// Adds your own item to the inventory. It shows up in the terminal under <paramref name="category"/>
        /// ("Weapons", "Tools", "Props" or "Etc"), the player puts it on the toolbar like any other item, and while
        /// they hold it you get the mouse buttons through the returned <see cref="ModTool"/>. Call it from
        /// <c>OnInitializeMelon</c>; the tool is added to the game's inventory as soon as the game has set its own
        /// up.
        /// </summary>
        public static ModTool AddTool(string name, string category = "Tools")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A tool needs a name.", nameof(name));
            return Internal.ModItems.Add(new ModTool(name, string.IsNullOrWhiteSpace(category) ? "Tools" : category));
        }

        /// <summary>
        /// Adds a prop made from a mesh (for example one from <see cref="Utilities.Meshes.LoadObj"/>) to the
        /// inventory, under Props. The player places it like the game's props; each copy is a physics object they
        /// can grab, throw and shoot. Call it from <c>OnInitializeMelon</c>, like <see cref="AddTool"/>.
        /// </summary>
        /// <param name="material">Defaults to a plain grey <see cref="Utilities.Meshes.CreateMaterial"/>.</param>
        /// <param name="mass">In kilograms.</param>
        public static ModProp AddProp(string name, Mesh mesh, Material material = null, float mass = 10f)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A prop needs a name.", nameof(name));
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            return (ModProp)Internal.ModItems.Add(new ModProp(name, mesh, material, mass));
        }

        /// <summary>
        /// Adds a prop made from a prefab (for example from a <see cref="Assets.ModBundle"/>) to the inventory, under
        /// Props. Each placed copy is the prefab as it is, on the props' layer: give it a Rigidbody and colliders if
        /// the player should be able to grab and throw it.
        /// </summary>
        public static ModProp AddProp(string name, GameObject prefab)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A prop needs a name.", nameof(name));
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            return (ModProp)Internal.ModItems.Add(new ModProp(name, prefab));
        }

        /// <summary>The tools and props mods have added with <see cref="AddTool"/> and <see cref="AddProp(string, Mesh, Material, float)"/>.</summary>
        public static IReadOnlyList<ModTool> ModTools => Internal.ModItems.All;

        /// <summary>Whether the terminal (the inventory screen) is open.</summary>
        public static bool TerminalOpen
        {
            get
            {
                try
                {
                    return GameServices.TryGet<ITerminalService>()?.Open?.IsRunning ?? false;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>Opens the terminal on its items screen. Needs a map.</summary>
        public static bool OpenTerminal()
        {
            var terminal = GameServices.TryGet<ITerminalService>();
            if (terminal == null)
                return false;
            return Il2CppExtensions.TryRun(() =>
            {
                terminal.SetMode(Il2CppData.UI.TerminalMode.Items);
                if (!TerminalOpen)
                    terminal.Show();
            }, "Opening the terminal");
        }

        /// <summary>Closes the terminal.</summary>
        public static bool CloseTerminal()
        {
            var terminal = GameServices.TryGet<ITerminalService>();
            if (terminal == null)
                return false;
            return !TerminalOpen || Il2CppExtensions.TryRun(terminal.Hide, "Closing the terminal");
        }

        internal static InventoryItem Wrap(IGodInventoryItemData data)
        {
            if (data == null || data.Pointer == IntPtr.Zero)
                return null;
            if (Wrappers.TryGetValue(data.Pointer, out var item) && !item.Data.WasCollected)
                return item;
            item = new InventoryItem(data);
            Wrappers[data.Pointer] = item;
            return item;
        }

        // The game's categories have an Id field, but the shipped ones leave it empty, so the name stands in for it.
        internal static string CategoryId(IGodInventoryCategoryData category)
        {
            if (category == null)
                return "";
            try
            {
                if (!string.IsNullOrEmpty(category.Id))
                    return category.Id;
            }
            catch
            {
                // Fall through to the name.
            }
            return CategoryName(category);
        }

        internal static string CategoryName(IGodInventoryCategoryData category)
        {
            if (category == null)
                return "";
            try
            {
                return CategoryName(category.TryCast<SerializedGodInventoryCategoryData>());
            }
            catch
            {
                return "";
            }
        }

        internal static string CategoryName(SerializedGodInventoryCategoryData asset)
        {
            if (asset == null)
                return "";
            try
            {
                return !string.IsNullOrEmpty(asset.ObjectName) ? asset.ObjectName : asset.name ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static void AddOnce(List<string> list, string value)
        {
            if (!string.IsNullOrEmpty(value) && !list.Contains(value, StringComparer.OrdinalIgnoreCase))
                list.Add(value);
        }
    }
}
