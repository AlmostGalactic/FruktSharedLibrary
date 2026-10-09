using System.Collections;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using Il2CppPlayer.Appearances.God.InventoryItems;

namespace FruktSharedLibrary.Internal
{
    // A copy of the game's Human Spawner: it's in the terminal under the original's category with the original's
    // icon, and holding it holds a working spawner that the copy knows is held.
    internal static partial class SelfTest
    {
        private static ModTool _testCopy, _missingCopy;

        private static void AddTestCopy()
        {
            _testCopy = Inventory.AddCopy("Self-test spawner copy", "Human").WithDescription("A copy made by the self-test.");
            _missingCopy = Inventory.AddCopy("Self-test copy of nothing", "No Such Item");
        }

        private static IEnumerator TestCopies()
        {
            var copy = _testCopy;
            var original = Inventory.Find("Human");
            int slot = -1;
            Section("Copies of game items", () =>
            {
                Check("Inventory.AddCopy registered the copy", copy.Registered && !copy.Failed && copy.CopyOf == "Human",
                    copy.Failed ? "failed, see the log" : copy.Item?.Name);
                Check("A copy of an item that isn't there fails", _missingCopy.Failed && !_missingCopy.Registered);
                Check("It's under the original's category", original != null && copy.Item?.CategoryName == original.CategoryName,
                    $"{copy.Item?.CategoryName} (original {original?.CategoryName})");
                Check("It has the original's icon", original?.Icon != null && copy.Item?.Icon == original.Icon, copy.Item?.Icon?.name);
                Check("It has its own description", copy.Item?.Description == "A copy made by the self-test.", copy.Item?.Description);
                Check("The original is still there", original != null && original != copy.Item);
                if (copy.Item != null && Toolbar.Available)
                {
                    slot = Toolbar.Give(copy.Item);
                    Toolbar.Select(slot);
                }
            });
            yield return Wait(1.5f);
            Section("Copy held", () =>
            {
                var held = Toolbar.HeldObject;
                Check("Holding the copy holds a working game item", held != null && held.GetComponent<HumanSpawnerGII>() != null, held?.name);
                Check("The copy knows it's held", copy.IsHeld && copy.HeldObject == held);
                if (slot >= 0)
                    Toolbar.Clear(slot);
            });
            yield return Wait(0.5f);
        }
    }
}
