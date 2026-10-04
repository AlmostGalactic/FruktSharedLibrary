using System;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using Il2CppData.Player.Inventory.God;
using Il2CppPlayer.Appearances.God.InventoryItems;
using Il2CppPlayer.Appearances.God.Toolbar;
using Il2CppServices.Player;
using UnityEngine;

namespace FruktSharedLibrary.Gameplay
{
    /// <summary>
    /// The toolbar along the bottom of the screen: the slots the player picks items from with the number keys.
    /// Slot 0 is the cursor (the hand) and can't be changed. Only exists in a map.
    /// </summary>
    /// <example>
    /// <code>
    /// var pistol = Inventory.Find("Viper-17");
    /// int slot = Toolbar.Give(pistol);
    /// if (slot >= 0) Toolbar.Select(slot);
    /// </code>
    /// </example>
    public static class Toolbar
    {
        /// <summary>An item was put in a slot (by the player, the game or a mod).</summary>
        public static event Action<InventoryItem, int> ItemAdded;

        /// <summary>An item was taken out of a slot.</summary>
        public static event Action<InventoryItem, int> ItemRemoved;

        /// <summary>The player switched to another slot. Gives the new slot's item (null if it's empty) and index.</summary>
        public static event Action<InventoryItem, int> SelectionChanged;

        /// <summary>Whether there's a toolbar right now (there is in a map, not on the main menu).</summary>
        public static bool Available => Service != null;

        /// <summary>How many slots there are, including the cursor's.</summary>
        public static int SlotCount
        {
            get
            {
                var service = Service;
                if (service == null)
                    return 0;
                try
                {
                    return service.Capacity;
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>The cursor's slot, which can't be changed.</summary>
        public static int CursorSlot
        {
            get
            {
                try
                {
                    return GodToolbarService.CURSOR_SLOT_INDEX;
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>The slot the player has selected, or -1 without a toolbar.</summary>
        public static int SelectedSlot
        {
            get
            {
                var service = Service;
                if (service == null)
                    return -1;
                try
                {
                    return service.SelectedSlotIndex;
                }
                catch
                {
                    return -1;
                }
            }
        }

        /// <summary>The item in the selected slot, or null.</summary>
        public static InventoryItem SelectedItem => GetItem(SelectedSlot);

        /// <summary>The item in a slot, or null if it's empty (or there's no such slot).</summary>
        public static InventoryItem GetItem(int slot) => Inventory.Wrap(RawItem(Service, slot));

        /// <summary>Whether a slot is empty.</summary>
        public static bool IsEmpty(int slot) => RawItem(Service, slot) == null;

        /// <summary>Whether a mod (or the player) can put items in a slot. The cursor's slot can't be changed.</summary>
        public static bool CanChange(int slot)
        {
            var service = Service;
            if (service == null || slot < 0 || slot >= SlotCount)
                return false;
            try
            {
                return service.IsSlotWritable(slot);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The number key that selects a slot, as the toolbar shows it.</summary>
        public static int KeyOf(int slot)
        {
            var service = Service;
            if (service == null)
                return -1;
            try
            {
                return service.KeyNumberOf(slot);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>Switches to a slot, like pressing its number key.</summary>
        public static bool Select(int slot)
        {
            var service = Service;
            if (service == null || slot < 0 || slot >= SlotCount)
                return false;
            return Il2CppExtensions.TryRun(() => service.TrySelect(slot), $"Selecting toolbar slot {slot}");
        }

        /// <summary>
        /// Puts an item in a slot, replacing what was there. Returns false if the slot can't be changed (the cursor's)
        /// or the game refused.
        /// </summary>
        public static bool Put(InventoryItem item, int slot)
        {
            var service = Service;
            if (service == null || item == null || !CanChange(slot))
                return false;
            try
            {
                if (RawItem(service, slot) != null && !service.TryRelease(slot))
                    return false;
                return service.TryAddItemAt(item.Data, slot);
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Putting '{item.Name}' in toolbar slot {slot} failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Puts an item in the first empty slot. Returns the slot, or -1 if the toolbar is full.</summary>
        public static int Give(InventoryItem item)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (CanChange(slot) && IsEmpty(slot))
                    return Put(item, slot) ? slot : -1;
            }
            return -1;
        }

        /// <summary>Empties a slot.</summary>
        public static bool Clear(int slot)
        {
            var service = Service;
            if (service == null || !CanChange(slot))
                return false;
            if (RawItem(service, slot) == null)
                return true;
            try
            {
                return service.TryRelease(slot);
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Emptying toolbar slot {slot} failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// The object the player is holding: the gun, the cutter, or the hologram of the prop they're about to place.
        /// Null when nothing's selected.
        /// </summary>
        public static GameObject HeldObject
        {
            get
            {
                try
                {
                    var selector = GameServices.TryGet<GodToolbarItemsSelectorService>();
                    var held = selector?.m_lastSelectedItem;
                    if (held.Exists() && held.gameObject.activeInHierarchy)
                        return held.gameObject;
                    foreach (var item in GameServices.FindObjects<GodInventoryItem>())
                    {
                        if (item.Exists() && item.gameObject.activeInHierarchy)
                            return item.gameObject;
                    }
                }
                catch (Exception e)
                {
                    FruktLog.Debug($"Finding the held item failed: {e.Message}");
                }
                return null;
            }
        }

        // ------------------------------------------------------------ internals

        private static GodToolbarService Service
        {
            get
            {
                var service = GameServices.TryGet<IGodToolbarService>()?.TryCast<GodToolbarService>();
                if (service.Exists())
                    return service;
                service = GameServices.FindObject<GodToolbarService>();
                return service.Exists() ? service : null;
            }
        }

        private static IGodInventoryItemData RawItem(GodToolbarService service, int slot)
        {
            if (service == null || slot < 0)
                return null;
            try
            {
                var model = service.m_toolbarModel;
                if (model == null || slot >= service.Capacity || !model.IsOccupied(slot))
                    return null;
                return model.GetItem(slot);
            }
            catch
            {
                return null;
            }
        }

        // Events come from comparing the slots every frame: it's eight reads, and unlike the game's own events it
        // keeps working when a new map brings a new toolbar.
        private static IntPtr _watched;
        private static IntPtr[] _slots = Array.Empty<IntPtr>();
        private static int _selected = -1;

        internal static void Update()
        {
            if (ItemAdded == null && ItemRemoved == null && SelectionChanged == null && _watched == IntPtr.Zero)
                return;
            var service = GameState.InSandbox ? Service : null;
            if (service == null)
            {
                _watched = IntPtr.Zero;
                return;
            }
            int count = SlotCount;
            bool fresh = service.Pointer != _watched || _slots.Length != count;
            if (fresh)
            {
                // A new toolbar: take a look at it without reporting what the game filled it with.
                _watched = service.Pointer;
                _slots = new IntPtr[count];
            }
            for (int slot = 0; slot < count; slot++)
            {
                var data = RawItem(service, slot);
                var now = data?.Pointer ?? IntPtr.Zero;
                var before = _slots[slot];
                if (now == before)
                    continue;
                _slots[slot] = now;
                if (fresh)
                    continue;
                if (before != IntPtr.Zero)
                    Raise(ItemRemoved, Inventory.Wrap(new IGodInventoryItemData(before)), slot, nameof(ItemRemoved));
                if (data != null)
                    Raise(ItemAdded, Inventory.Wrap(data), slot, nameof(ItemAdded));
            }
            int selected = SelectedSlot;
            if (selected != _selected)
            {
                _selected = selected;
                if (!fresh)
                    Raise(SelectionChanged, GetItem(selected), selected, nameof(SelectionChanged));
            }
        }

        private static void Raise(Action<InventoryItem, int> handlers, InventoryItem item, int slot, string name)
        {
            if (handlers == null)
                return;
            foreach (Action<InventoryItem, int> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(item, slot);
                }
                catch (Exception e)
                {
                    FruktLog.Error($"A Toolbar.{name} handler threw", e);
                }
            }
        }
    }
}
