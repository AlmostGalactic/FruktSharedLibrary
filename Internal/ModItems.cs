using System;
using System.Collections.Generic;
using System.Reflection;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using Il2CppData.Icons;
using Il2CppData.Player.Inventory.God;
using Il2CppInfrastructure.Project.Registration.Native;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Runtime;
using Il2CppPlayer.Appearances.God.InventoryItems;
using Il2CppServices.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// Puts mods' <see cref="ModTool"/>s in the game's inventory, the way the game registers its own items: a prefab
    /// (a <see cref="ModToolBehaviour"/> with the tool's model), a card (name, description, icon, rows) and a
    /// category, handed to the game's item registry. Also follows which tool the player is holding.
    /// </summary>
    internal static class ModItems
    {
        private static readonly List<ModTool> Tools = new();
        private static readonly Dictionary<string, ModTool> ByTemplate = new();
        private static readonly Dictionary<ModTool, SerializedItemDescriptor> Cards = new();
        private static GameObject _templates;
        private static bool _injected, _injectionFailed;
        private static Sprite _defaultIcon;

        internal static IReadOnlyList<ModTool> All => Tools;
        internal static ModTool HeldTool { get; private set; }
        internal static GameObject HeldObject { get; private set; }

        internal static ModTool Add(ModTool tool)
        {
            if (Tools.Exists(t => string.Equals(t.Name, tool.Name, StringComparison.OrdinalIgnoreCase)))
                FruktLog.Warning($"Two mod tools are called '{tool.Name}'. Players won't be able to tell them apart.");
            Tools.Add(tool);
            return tool;
        }

        internal static void Update()
        {
            if (Tools.Count == 0)
                return;
            RegisterPending();
            FollowHeld();
        }

        // ------------------------------------------------------------ registering

        private static void RegisterPending()
        {
            if (!Tools.Exists(t => !t.Registered && !t.Failed))
                return;
            // The game files its own items group by group while it boots (weapons first), so mod tools wait for the
            // main menu: by then every group is in, the categories all exist, and the game's items come first.
            if (GameState.Phase == GamePhase.Booting)
                return;
            var registry = GameServices.TryGet<INativeGodInventoryItemsHandler>()?.TryCast<NativeGodInventoryItemsRegistration>();
            if (!registry.Exists())
                return;
            var categories = FindCategories(registry);
            if (categories.Count == 0)
                return;
            foreach (var tool in Tools)
            {
                if (!tool.Registered && !tool.Failed)
                    Register(tool, registry, categories);
            }
        }

        private static Dictionary<string, SerializedGodInventoryCategoryData> FindCategories(NativeGodInventoryItemsRegistration registry)
        {
            var result = new Dictionary<string, SerializedGodInventoryCategoryData>(StringComparer.OrdinalIgnoreCase);
            void Add(SerializedGodInventoryCategoryData category)
            {
                string name = Inventory.CategoryName(category);
                if (category != null && name.Length > 0 && !result.ContainsKey(name))
                    result[name] = category;
            }
            try
            {
                foreach (var data in registry.GodInventoryItemsData.ToManagedList())
                    Add(data?.Category?.TryCast<SerializedGodInventoryCategoryData>());
            }
            catch (Exception e)
            {
                FruktLog.Debug($"Reading the inventory's categories failed: {e.Message}");
            }
            // The game's own registration assets, in case a category has no registered item yet.
            try
            {
                var weapons = registry.m_weapons;
                var tools = registry.m_tools;
                foreach (var registration in new[] { weapons?.m_viper17, tools?.m_cursor, tools?.m_cutter, tools?.m_humanSpawner })
                    Add(registration?.m_category);
                var props = registry.m_props?.m_props;
                if (props != null)
                {
                    foreach (var registration in props)
                        Add(registration?.m_category);
                }
            }
            catch (Exception e)
            {
                FruktLog.Debug($"Reading the game's item registrations failed: {e.Message}");
            }
            return result;
        }

        private static void Register(ModTool tool, NativeGodInventoryItemsRegistration registry,
            Dictionary<string, SerializedGodInventoryCategoryData> categories)
        {
            try
            {
                if (!EnsureInjected())
                {
                    tool.Failed = true;
                    return;
                }
                if (!categories.TryGetValue(tool.Category, out var category))
                {
                    string fallback = categories.ContainsKey("Etc") ? "Etc" : new List<string>(categories.Keys)[0];
                    FruktLog.Warning($"'{tool.Name}' asks for the inventory category '{tool.Category}', which doesn't exist " +
                                     $"(there's {string.Join(", ", categories.Keys)}). It's under {fallback} instead.");
                    category = categories[fallback];
                }
                string categoryName = Inventory.CategoryName(category);
                NativeGIIGroupHandler group = categoryName.Equals("Weapons", StringComparison.OrdinalIgnoreCase) ? registry.m_weapons
                    : categoryName.Equals("Tools", StringComparison.OrdinalIgnoreCase) ? registry.m_tools
                    : registry.m_props;
                group ??= registry.m_props;

                var template = BuildTemplate(tool);
                var card = BuildCard(tool);
                var registration = ScriptableObject.CreateInstance<SerializedGIIRegistrationData>();
                registration.name = template.name;
                registration.hideFlags = HideFlags.HideAndDontSave;
                registration.m_objectDescriptor = card;
                registration.m_category = category;
                registration.m_prefab = template.GetComponent<ModToolBehaviour>();

                var data = group.RegisterGIIPrefab(registration);
                if (data == null)
                {
                    FruktLog.Warning($"The game didn't accept '{tool.Name}' into the inventory.");
                    tool.Failed = true;
                    return;
                }
                Cards[tool] = card;
                tool.Item = Inventory.Wrap(data);
                FruktLog.Msg($"Added '{tool.Name}' to the inventory under {categoryName}.");
            }
            catch (Exception e)
            {
                tool.Failed = true;
                FruktLog.Error($"Adding '{tool.Name}' to the inventory failed", e);
            }
        }

        private static GameObject BuildTemplate(ModTool tool)
        {
            if (!_templates.Exists())
            {
                // Inactive, so the templates never run; the game activates its copies.
                _templates = new GameObject("FruktSharedLibrary mod tools");
                _templates.SetActive(false);
                Object.DontDestroyOnLoad(_templates);
            }
            string name = "FSLTool" + Tools.IndexOf(tool);
            var template = new GameObject(name);
            template.transform.SetParent(_templates.transform, false);
            ModToolBehaviour.Prepare(template.AddComponent<ModToolBehaviour>());
            if (tool.Model.Exists())
            {
                var model = Object.Instantiate(tool.Model, template.transform, false);
                model.name = tool.Model.name;
                model.transform.localPosition = tool.HeldPosition;
                model.transform.localRotation = Quaternion.Euler(tool.HeldRotation);
                model.transform.localScale *= tool.HeldScale;
                model.SetActive(true);
                Assets.Shaders.FixMaterials(model);
                // In the hand it's only for show: no physics, or it would shove things the player walks past.
                foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(collider);
                foreach (var body in model.GetComponentsInChildren<Rigidbody>(true))
                    Object.Destroy(body);
            }
            ByTemplate[name] = tool;
            return template;
        }

        private static SerializedItemDescriptor BuildCard(ModTool tool)
        {
            var icon = ScriptableObject.CreateInstance<SerializedIconData>();
            icon.name = tool.Name + " icon";
            icon.hideFlags = HideFlags.HideAndDontSave;
            icon.m_sprite = tool.Icon ?? DefaultIcon;
            icon.m_offset = Vector2.zero;
            icon.m_scale = Vector2.one;
            icon.m_color = Color.white;

            var card = ScriptableObject.CreateInstance<SerializedItemDescriptor>();
            card.name = tool.Name + " card";
            card.hideFlags = HideFlags.HideAndDontSave;
            card.m_objectName = tool.Name;
            card.m_description = tool.Description ?? "";
            card.m_iconData = icon;
            var rows = new Il2CppSystem.Collections.Generic.List<ItemCardRow>();
            foreach (var row in tool.CardRows)
                rows.Add(new ItemCardRow(row.Key, row.Value));
            card.m_freeRows = rows;
            return card;
        }

        /// <summary>Shows a changed icon in the terminal and on the toolbar.</summary>
        internal static void Refresh(ModTool tool)
        {
            if (!Cards.TryGetValue(tool, out var card) || !card.Exists())
                return;
            try
            {
                var icon = card.m_iconData;
                if (icon.Exists())
                    icon.m_sprite = tool.Icon ?? DefaultIcon;
                var data = tool.Item?.Data.TryCast<GodInventoryItemData>();
                if (data != null)
                    data._IconData_k__BackingField = card.IconData;
            }
            catch (Exception e)
            {
                FruktLog.Debug($"Updating the icon of '{tool.Name}' failed: {e.Message}");
            }
        }

        // A light square outline, like the props tab's icon.
        private static Sprite DefaultIcon
        {
            get
            {
                if (_defaultIcon.Exists())
                    return _defaultIcon;
                const int size = 64, border = 5, margin = 12;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var clear = new Color(0f, 0f, 0f, 0f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        bool inside = x >= margin && x < size - margin && y >= margin && y < size - margin;
                        bool edge = inside && (x < margin + border || x >= size - margin - border || y < margin + border || y >= size - margin - border);
                        texture.SetPixel(x, y, edge ? Color.white : clear);
                    }
                }
                texture.Apply();
                texture.hideFlags = HideFlags.HideAndDontSave;
                _defaultIcon = Utilities.Textures.ToSprite(texture);
                _defaultIcon.hideFlags = HideFlags.HideAndDontSave;
                return _defaultIcon;
            }
        }

        // ------------------------------------------------------------ the injected class

        private static bool EnsureInjected()
        {
            if (_injected || _injectionFailed)
                return _injected;
            try
            {
                if (!ClassInjector.IsTypeRegisteredInIl2Cpp<ModToolBehaviour>())
                    ClassInjector.RegisterTypeInIl2Cpp<ModToolBehaviour>();
                KeepGcFlag();
                _injected = true;
            }
            catch (Exception e)
            {
                _injectionFailed = true;
                FruktLog.Error("Mod tools are unavailable: registering their class with IL2CPP failed", e);
            }
            return _injected;
        }

        // Il2CppInterop can register a subclass without the "has references" flag of its game base class. The garbage
        // collector then doesn't look inside the item, and frees what only the item points to (its feature list,
        // event delegates...), which crashes the game a while later. The flag is copied over from the base class.
        private static void KeepGcFlag()
        {
            try
            {
                var ours = Wrap(Il2CppClassPointerStore<ModToolBehaviour>.NativeClassPtr);
                var theirs = Wrap(Il2CppClassPointerStore<GodInventoryItem>.NativeClassPtr);
                var flag = ours?.GetType().GetProperty("HasReferences", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (flag == null || theirs == null)
                {
                    FruktLog.Warning("Can't check the mod tool class's garbage collector flag in this version of Il2CppInterop.");
                    return;
                }
                bool oursHas = (bool)flag.GetValue(ours), theirsHas = (bool)flag.GetValue(theirs);
                if (theirsHas && !oursHas)
                {
                    flag.SetValue(ours, true);
                    FruktLog.Debug("Mod tool class: copied the garbage collector's has-references flag from the game's item class.");
                }
                else
                {
                    FruktLog.Debug($"Mod tool class: has-references is {oursHas} (the game's item class: {theirsHas}).");
                }
            }
            catch (Exception e)
            {
                FruktLog.Warning("Checking the mod tool class's garbage collector flag failed: " + e.Message);
            }
        }

        // The class struct's layout depends on the Unity version; Il2CppInterop picks the right one.
        private static unsafe object Wrap(IntPtr klass) => klass == IntPtr.Zero ? null : UnityVersionHandler.Wrap((Il2CppClass*)klass);

        // ------------------------------------------------------------ holding

        internal static void Input(ModToolBehaviour behaviour, ToolInput input, float value = 0f)
            => ToolOf(behaviour?.gameObject)?.Raise(input, value);

        private static ModTool ToolOf(GameObject held)
        {
            if (!held.Exists())
                return null;
            // The game's copies are called like "FSLTool3(Clone)_6".
            string name = held.name;
            int cut = name.IndexOf('(');
            return ByTemplate.TryGetValue(cut < 0 ? name : name.Substring(0, cut), out var tool) ? tool : null;
        }

        private static void FollowHeld()
        {
            var held = GameState.InSandbox ? Toolbar.HeldObject : null;
            var tool = held != null && held.GetComponent<ModToolBehaviour>() != null ? ToolOf(held) : null;
            if (tool != HeldTool || (tool != null && held != HeldObject))
            {
                var previous = HeldTool;
                HeldTool = null;
                HeldObject = null;
                previous?.Raise(ToolInput.Deselected);
                HeldTool = tool;
                HeldObject = tool != null ? held : null;
                tool?.Raise(ToolInput.Selected);
            }
            HeldTool?.Raise(ToolInput.Held);
        }
    }
}
