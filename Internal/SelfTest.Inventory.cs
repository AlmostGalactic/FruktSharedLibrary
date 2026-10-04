using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FruktSharedLibrary.Gameplay;

namespace FruktSharedLibrary.Internal
{
    // The inventory and the toolbar: reading the terminal's items, putting one in a slot, selecting it, and
    // emptying the slot again. Whatever was on the toolbar before is put back.
    internal static partial class SelfTest
    {
        private static IEnumerator TestInventory()
        {
            InventoryItem pistol = null;
            Section("Inventory", () =>
            {
                var items = Inventory.Items;
                Check("Inventory.Items lists the terminal's items", items.Count > 0,
                    string.Join(", ", items.Select(i => $"{i.Name} [{i.Category}]")));
                Check("Every item has a name and a category", items.All(i => i.Name.Length > 0 && i.Category.Length > 0));
                var categories = Inventory.Categories;
                Check("Inventory.Categories", categories.Count > 0, string.Join(", ", categories));
                pistol = Inventory.Find("Viper-17") ?? items.FirstOrDefault(i => i.CardRows.Count > 0);
                Check("Inventory.Find", pistol != null, pistol?.Name);
                if (pistol != null)
                {
                    Check("Items have card rows", pistol.CardRows.Count > 0,
                        string.Join(", ", pistol.CardRows.Select(r => $"{r.Key}={r.Value}")));
                    Check("Items have icons", pistol.Icon != null);
                    Check("Inventory.ItemsIn finds an item's category", Inventory.ItemsIn(pistol.Category).Contains(pistol));
                    Check("Finding by ID works too", Inventory.Find(pistol.Id) == pistol, pistol.Id);
                }
            });
            if (pistol == null || !Toolbar.Available)
            {
                Check("The toolbar is there in a map", Toolbar.Available);
                yield break;
            }

            var added = new List<(InventoryItem item, int slot)>();
            var removed = new List<(InventoryItem item, int slot)>();
            var selected = new List<(InventoryItem item, int slot)>();
            void OnAdded(InventoryItem item, int slot) => added.Add((item, slot));
            void OnRemoved(InventoryItem item, int slot) => removed.Add((item, slot));
            void OnSelected(InventoryItem item, int slot) => selected.Add((item, slot));
            Toolbar.ItemAdded += OnAdded;
            Toolbar.ItemRemoved += OnRemoved;
            Toolbar.SelectionChanged += OnSelected;
            yield return null;

            int count = Toolbar.SlotCount;
            var before = Enumerable.Range(0, count).Select(Toolbar.GetItem).ToList();
            int selectedBefore = Toolbar.SelectedSlot;
            int slot = Enumerable.Range(0, count).Where(s => Toolbar.CanChange(s) && Toolbar.IsEmpty(s)).DefaultIfEmpty(count - 1).First();
            Section("Toolbar", () =>
            {
                Check("Toolbar.SlotCount", count > 1, count.ToString());
                Check("The cursor's slot can't be changed", !Toolbar.CanChange(Toolbar.CursorSlot), Toolbar.CursorSlot.ToString());
                Check("Toolbar.GetItem reads the slots", before.Any(i => i != null),
                    string.Join(", ", before.Select((i, s) => $"{s}:{i?.Name ?? "-"} (key {Toolbar.KeyOf(s)})")));
                Check("Toolbar.Put", Toolbar.Put(pistol, slot), $"slot {slot}");
                Check("The slot holds the item", Toolbar.GetItem(slot) == pistol, Toolbar.GetItem(slot)?.Name);
            });
            yield return Wait(0.5f);
            Section("Toolbar (select)", () =>
            {
                Check("Toolbar.ItemAdded", added.Any(a => a.item == pistol && a.slot == slot),
                    string.Join(", ", added.Select(a => $"{a.item?.Name}@{a.slot}")));
                Check("Toolbar.Select", Toolbar.Select(slot));
            });
            yield return Wait(1.5f);
            Section("Toolbar (selected)", () =>
            {
                Check("The slot is selected", Toolbar.SelectedSlot == slot && Toolbar.SelectedItem == pistol, Toolbar.SelectedSlot.ToString());
                Check("Toolbar.SelectionChanged", selected.Any(s => s.slot == slot && s.item == pistol),
                    string.Join(", ", selected.Select(s => $"{s.item?.Name ?? "-"}@{s.slot}")));
                var held = Toolbar.HeldObject;
                Check("Toolbar.HeldObject", held != null, held?.name);
                Shot("toolbar");
            });
            yield return Wait(1f);
            Section("Toolbar (clear)", () => Check("Toolbar.Clear", Toolbar.Clear(slot) && Toolbar.IsEmpty(slot)));
            yield return Wait(0.5f);
            Section("Toolbar (cleared)", () =>
            {
                Check("Toolbar.ItemRemoved", removed.Any(r => r.item == pistol && r.slot == slot),
                    string.Join(", ", removed.Select(r => $"{r.item?.Name}@{r.slot}")));
                for (int s = 0; s < count; s++)
                {
                    if (Toolbar.CanChange(s) && Toolbar.GetItem(s) != before[s])
                    {
                        if (before[s] == null)
                            Toolbar.Clear(s);
                        else
                            Toolbar.Put(before[s], s);
                    }
                }
                Toolbar.Select(selectedBefore);
                Check("The toolbar is back how it was", Enumerable.Range(0, count).All(s => Toolbar.GetItem(s) == before[s]));
            });
            Toolbar.ItemAdded -= OnAdded;
            Toolbar.ItemRemoved -= OnRemoved;
            Toolbar.SelectionChanged -= OnSelected;
            yield return Wait(1f);

            Section("Terminal", () => Check("Inventory.OpenTerminal", Inventory.OpenTerminal()));
            yield return Wait(1.5f);
            Section("Terminal (open)", () =>
            {
                Check("Inventory.TerminalOpen", Inventory.TerminalOpen);
                Shot("terminal");
            });
            yield return Wait(1f);
            Section("Terminal (close)", () => Check("Inventory.CloseTerminal", Inventory.CloseTerminal()));
            yield return Wait(1f);
            Section("Terminal (closed)", () => Check("The terminal closed", !Inventory.TerminalOpen));
        }
    }
}
