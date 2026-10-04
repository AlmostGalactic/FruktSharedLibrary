using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FruktSharedLibrary.Assets;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Utilities;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // Mod props: one made from an OBJ mesh, added at startup like a mod would, and one made from the test bundle's
    // crate prefab, added in the middle of the game. Each is put on the toolbar and placed with a real click.
    internal static partial class SelfTest
    {
        private static ModProp _testProp;
        private static readonly List<GameObject> PlacedProps = new();

        private const string TestBoxObj = @"
v -0.3 -0.2 -0.3
v 0.3 -0.2 -0.3
v 0.3 0.2 -0.3
v -0.3 0.2 -0.3
v -0.3 -0.2 0.3
v 0.3 -0.2 0.3
v 0.3 0.2 0.3
v -0.3 0.2 0.3
f 1 4 3 2
f 5 6 7 8
f 1 2 6 5
f 4 8 7 3
f 1 5 8 4
f 2 3 7 6
";

        private static void AddTestProp()
        {
            var mesh = Meshes.ParseObj(TestBoxObj, "Self-test box");
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _testProp = Inventory.AddProp("Self-test box", mesh, mass: 5f)
                .WithDescription("A box from FruktSharedLibrary's self-test.")
                .WithCard("size", "60 cm")
                .OnPlaced(PlacedProps.Add);
        }

        private static IEnumerator TestModProps()
        {
            var prop = _testProp;
            Section("Mod props", () =>
            {
                Check("Inventory.AddProp registered the prop", prop.Registered && !prop.Failed);
                var item = prop.Item;
                Check("It's under Props", item != null && item.CategoryName == "Props", item?.CategoryName);
                Check("It's in Inventory.Items", item != null && Inventory.Items.Contains(item));
            });
            if (!prop.Registered || !Toolbar.Available)
                yield break;

            int count = Toolbar.SlotCount;
            var before = Enumerable.Range(0, count).Select(Toolbar.GetItem).ToList();
            int selectedBefore = Toolbar.SelectedSlot;
            int slot = Enumerable.Range(0, count).Where(s => Toolbar.CanChange(s) && Toolbar.IsEmpty(s)).DefaultIfEmpty(count - 1).First();
            Section("Mod prop on the toolbar", () =>
            {
                Check("Toolbar.Put takes a mod prop", Toolbar.Put(prop.Item, slot));
                Check("Selecting its slot", Toolbar.Select(slot));
            });
            yield return Wait(1.5f);
            float turnBefore = prop.Turn;
            Section("Mod prop held", () =>
            {
                Check("ModProp.IsHeld", prop.IsHeld);
                Shot("mod-prop-hologram");
                Wheel("mod-prop-turn", 2);
                var hologram = prop.Hologram;
                Check("A hologram shows where it will go", hologram != null && hologram.activeInHierarchy, hologram?.name);
                if (hologram == null)
                    return;
                bool aimed = prop.TryGetPlacement(out var where, out _);
                Check("The hologram is at the placement", (hologram.transform.position - where).magnitude < 0.5f,
                    $"{hologram.transform.position} vs {where} (aiming at something: {aimed})");
                Check("The hologram can't be bumped into", hologram.GetComponentInChildren<Collider>() == null);
                var renderer = hologram.GetComponentInChildren<Renderer>();
                Check("The hologram is see-through", renderer != null && renderer.sharedMaterial.renderQueue >= 3000 && renderer.sharedMaterial.shader.isSupported,
                    renderer == null ? "no renderer" : $"{renderer.sharedMaterial.shader.name}, queue {renderer.sharedMaterial.renderQueue}");
            });
            for (float end = Now() + 3f; Mathf.Approximately(prop.Turn, turnBefore) && Now() < end;)
                yield return null;
            yield return Wait(0.5f);
            Section("Mod prop turned", () =>
            {
                Check("The mouse wheel turns it", !Mathf.Approximately(prop.Turn, turnBefore), $"{turnBefore:0} -> {prop.Turn:0} degrees");
                Check("The wheel doesn't switch toolbar slots while it's held", Toolbar.SelectedSlot == slot, Toolbar.SelectedSlot.ToString());
                FruktLog.Msg("[SelfTest] CLICK mod-prop-place 0.5000 0.5000");
            });
            for (float end = Now() + 3f; PlacedProps.Count == 0 && Now() < end;)
                yield return null;
            yield return Wait(1.5f);
            Section("Mod prop placed", () =>
            {
                var placed = PlacedProps.FirstOrDefault();
                Check("A real click places it and raises Placed", placed.Exists(), placed?.name);
                if (placed.Exists())
                {
                    Check("The placed box is a physics prop", placed.GetComponent<Rigidbody>() != null && placed.layer == Meshes.PropLayer);
                    Check("It's in front of the player", (placed.transform.position - LocalPlayer.CameraPosition).magnitude < prop.Reach + 2f,
                        $"{(placed.transform.position - LocalPlayer.CameraPosition).magnitude:0.0} m away");
                }
                Shot("mod-prop-placed");
            });

            // A prefab prop, added after the game has started.
            ModBundle bundle = File.Exists(TestBundlePath) ? ModBundle.Load(TestBundlePath) : null;
            var crate = bundle?.Load<GameObject>("TestCrate");
            ModProp crateProp = null;
            if (crate != null)
            {
                crateProp = Inventory.AddProp("Self-test crate", crate).OnPlaced(PlacedProps.Add);
                yield return null;
                yield return null;
                Section("Prefab prop", () =>
                {
                    Check("A prop added mid-game is registered at once", crateProp.Registered && crateProp.Item?.CategoryName == "Props",
                        crateProp.Item?.CategoryName);
                    Check("Toolbar.Put takes it", crateProp.Registered && Toolbar.Put(crateProp.Item, slot));
                    Toolbar.Select(slot);
                });
                yield return Wait(1.5f);
                Section("Prefab prop held", () =>
                {
                    Check("The prefab prop's hologram shows", crateProp.Hologram != null && crateProp.Hologram.activeInHierarchy);
                    FruktLog.Msg("[SelfTest] CLICK mod-crate-place 0.5000 0.5000");
                });
                int placedBefore = PlacedProps.Count;
                for (float end = Now() + 3f; PlacedProps.Count == placedBefore && Now() < end;)
                    yield return null;
                yield return Wait(1f);
                Section("Prefab prop placed", () =>
                {
                    var placed = PlacedProps.Count > placedBefore ? PlacedProps.Last() : null;
                    Check("Placing a prefab prop copies the prefab", placed.Exists() && placed.name == crate.name, placed?.name);
                    Shot("mod-crate-placed");
                });
            }
            else
            {
                FruktLog.Msg("[SelfTest] No test bundle in UserData; skipping the prefab prop checks.");
            }

            Section("Mod props put away", () => Check("Selecting the cursor", Toolbar.Select(Toolbar.CursorSlot)));
            yield return Wait(1f);
            Section("Mod props put away (deferred)", () =>
            {
                Check("Putting it away removes the hologram", prop.Hologram == null && (crateProp == null || crateProp.Hologram == null));
                foreach (var placed in PlacedProps)
                {
                    if (placed.Exists())
                        Object.Destroy(placed);
                }
                PlacedProps.Clear();
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
            yield return Wait(0.5f);
            bundle?.Unload(true);
        }
    }
}
