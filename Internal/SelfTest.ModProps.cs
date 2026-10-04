using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FruktSharedLibrary.Assets;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Utilities;
using MelonLoader.Utils;
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
                string problem = IconProblem(item);
                Check("Without an icon it gets a picture of its mesh", problem == null, problem ?? IconDetail(item));
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

            // Reloading an OBJ file with FileWatch, while the player holds the prop.
            string objPath = Path.Combine(MelonEnvironment.UserDataDirectory, "FruktSharedLibrary.selftest.obj");
            File.WriteAllText(objPath, TestBoxObj);
            int changes = 0;
            var oldIcon = prop.Item.Icon;
            var watch = FileWatch.Start(objPath, () =>
            {
                changes++;
                prop.SetMesh(Meshes.LoadObj(objPath));
            });
            yield return Wait(1.2f);
            File.WriteAllText(objPath, TestBoxObj.Replace("0.3", "0.5"));
            for (float end = Now() + 4f; changes == 0 && Now() < end;)
                yield return null;
            yield return Wait(0.5f);
            Section("FileWatch", () =>
            {
                Check("FileWatch runs once when the file changes", changes == 1, $"{changes} times");
                Check("ModProp.SetMesh takes the new mesh", prop.Mesh.Exists() && Mathf.Abs(prop.Mesh.bounds.size.x - 1f) < 0.01f,
                    prop.Mesh.Exists() ? prop.Mesh.bounds.size.ToString() : "no mesh");
                Check("The hologram follows the new mesh", prop.Hologram != null && prop.Hologram.GetComponent<MeshFilter>()?.sharedMesh == prop.Mesh);
                Check("So does the icon", prop.Item.Icon != null && oldIcon != null && prop.Item.Icon.Pointer != oldIcon.Pointer && IconProblem(prop.Item) == null);
                watch.Stop();
                File.WriteAllText(objPath, TestBoxObj);
            });
            yield return Wait(1.5f);
            Check("A stopped FileWatch doesn't run", changes == 1, $"{changes} times");
            File.Delete(objPath);

            // A prefab prop from a bundle, added after the game has started, and the bundle reloaded from its file.
            string bundlePath = Path.Combine(MelonEnvironment.UserDataDirectory, "FruktSharedLibrary.selftest-reload.bundle");
            ModBundle bundle = null;
            ModProp crateProp = null;
            if (File.Exists(TestBundlePath))
            {
                File.Copy(TestBundlePath, bundlePath, true);
                bundle = ModBundle.Load(bundlePath)?.WatchForChanges();
            }
            if (bundle != null)
            {
                crateProp = Inventory.AddProp("Self-test crate", bundle, "TestCrate").OnPlaced(PlacedProps.Add);
                yield return null;
                yield return null;
                Section("Prefab prop", () =>
                {
                    Check("A prop added mid-game is registered at once", crateProp.Registered && crateProp.Item?.CategoryName == "Props",
                        crateProp.Item?.CategoryName);
                    string problem = IconProblem(crateProp.Item);
                    Check("A prefab prop gets a picture of its prefab", problem == null, problem ?? IconDetail(crateProp.Item));
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
                GameObject placedCrate = null;
                Section("Prefab prop placed", () =>
                {
                    placedCrate = PlacedProps.Count > placedBefore ? PlacedProps.Last() : null;
                    Check("Placing a prefab prop copies the prefab", placedCrate.Exists() && placedCrate.name == crateProp.Prefab.name, placedCrate?.name);
                    Shot("mod-crate-placed");
                });

                var oldPrefab = crateProp.Prefab;
                int reloads = 0;
                bundle.Reloaded += _ => reloads++;
                yield return Wait(1f);
                File.Copy(TestBundlePath, bundlePath, true);
                File.SetLastWriteTimeUtc(bundlePath, System.DateTime.UtcNow);
                for (float end = Now() + 5f; reloads == 0 && Now() < end;)
                    yield return null;
                yield return Wait(0.5f);
                Section("Bundle reload", () =>
                {
                    Check("WatchForChanges reloads the bundle when its file changes", reloads == 1 && bundle.IsLoaded, $"{reloads} reloads");
                    Check("The prop follows the reloaded bundle", crateProp.Prefab.Exists() && oldPrefab != null && crateProp.Prefab.Pointer != oldPrefab.Pointer);
                    Check("Its hologram is rebuilt from the new prefab", crateProp.Hologram != null && crateProp.Hologram.activeInHierarchy);
                    var filter = placedCrate.Exists() ? placedCrate.GetComponentInChildren<MeshFilter>() : null;
                    Check("A crate placed before the reload keeps its mesh", filter != null && filter.sharedMesh.Exists());
                    Check("Load gives assets from the reloaded bundle", bundle.Load<GameObject>("TestCrate").Exists());
                });
            }
            else
            {
                FruktLog.Msg("[SelfTest] No test bundle in UserData; skipping the prefab prop and reload checks.");
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

            // The terminal's Props tab, with the mod props' icons.
            var terminal = GameServices.TryGet<Il2CppServices.UI.ITerminalItemsService>();
            Section("Mod props in the terminal", () => Check("Opening the terminal", Inventory.OpenTerminal()));
            yield return Wait(1f);
            try
            {
                terminal?.SetCategory(prop.Item.Data.Category);
            }
            catch (System.Exception e)
            {
                FruktLog.Debug("Switching the terminal to Props failed: " + e.Message);
            }
            yield return Wait(1f);
            Shot("terminal-mod-props");
            yield return Wait(0.5f);
            Inventory.CloseTerminal();
            yield return Wait(1f);

            bundle?.Unload(true);
            if (File.Exists(bundlePath))
                File.Delete(bundlePath);
        }

        // Null if the item's icon is an automatic picture: see-through around the edge, solid in the middle.
        private static string IconProblem(InventoryItem item)
        {
            var icon = item?.Icon;
            if (icon == null)
                return "no icon";
            if (ModItems.IsDefaultIcon(icon))
                return "the plain square";
            var texture = icon.texture;
            float corner = texture.GetPixel(1, 1).a, centre = texture.GetPixel(texture.width / 2, texture.height / 2).a;
            return corner < 0.05f && centre > 0.5f ? null : $"corner alpha {corner:0.00}, centre alpha {centre:0.00}";
        }

        private static string IconDetail(InventoryItem item)
        {
            var texture = item?.Icon?.texture;
            return texture == null ? "" : $"{texture.width}x{texture.height}, centre {texture.GetPixel(texture.width / 2, texture.height / 2)}";
        }
    }
}
