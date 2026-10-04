using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // A mod tool added the way a mod would (in OnInitializeMelon), then checked in the map: it's in the inventory,
    // it goes on the toolbar, the player holds it, and real mouse clicks reach it. The toolbar is put back after.
    internal static partial class SelfTest
    {
        private static ModTool _testTool;
        private static readonly List<string> ToolEvents = new();

        private static void AddTestTool()
        {
            // A small cube to hold, kept out of the scenes so loading the map doesn't destroy it.
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "Self-test tool model";
            model.transform.localScale = Vector3.one * 0.15f;
            model.SetActive(false);
            Object.DontDestroyOnLoad(model);

            _testTool = Inventory.AddTool("Self-test tool")
                .WithDescription("Added by FruktSharedLibrary's self-test.")
                .WithCard("purpose", "testing")
                .WithModel(model, new Vector3(0.3f, -0.2f, 0.6f), new Vector3(0f, 30f, 0f))
                .OnLeftClick(() => ToolEvents.Add("left click"))
                .OnRightClick(() => ToolEvents.Add("right click"))
                .OnScroll(n => ToolEvents.Add("scroll"));
            _testTool.Selected += () => ToolEvents.Add("selected");
            _testTool.Deselected += () => ToolEvents.Add("deselected");
            _testTool.LeftRelease += () => ToolEvents.Add("left release");
            _testTool.LeftHold += _ =>
            {
                if (!ToolEvents.Contains("left hold"))
                    ToolEvents.Add("left hold");
            };
            _testTool.WhileHeld += () =>
            {
                if (!ToolEvents.Contains("held"))
                    ToolEvents.Add("held");
            };
        }

        private static IEnumerator TestModTools()
        {
            var tool = _testTool;
            InventoryItem item = null;
            Section("Mod tools", () =>
            {
                Check("Inventory.AddTool registered the tool", tool.Registered && !tool.Failed, tool.Failed ? "failed, see the log" : null);
                Check("Inventory.ModTools lists it", Inventory.ModTools.Contains(tool));
                item = tool.Item;
                if (item == null)
                    return;
                Check("It's in Inventory.Items", Inventory.Items.Contains(item), $"{Inventory.Items.Count} items");
                Check("Inventory.Find finds it by name", Inventory.Find(tool.Name) == item);
                Check("It has the right name and category", item.Name == tool.Name && item.CategoryName == "Tools",
                    $"{item.Name} [{item.CategoryName}]");
                Check("It has its description", item.Description == tool.Description, item.Description);
                Check("It has its card row", item.CardRows.Any(r => r.Key == "purpose" && r.Value == "testing"),
                    string.Join(", ", item.CardRows.Select(r => $"{r.Key}={r.Value}")));
                Check("It has an icon", item.Icon != null);
            });
            if (item == null || !Toolbar.Available)
                yield break;

            int count = Toolbar.SlotCount;
            var before = Enumerable.Range(0, count).Select(Toolbar.GetItem).ToList();
            int selectedBefore = Toolbar.SelectedSlot;
            int slot = Enumerable.Range(0, count).Where(s => Toolbar.CanChange(s) && Toolbar.IsEmpty(s)).DefaultIfEmpty(count - 1).First();
            ToolEvents.Clear();
            Section("Mod tool on the toolbar", () =>
            {
                Check("Toolbar.Put takes a mod tool", Toolbar.Put(item, slot) && Toolbar.GetItem(slot) == item, $"slot {slot}");
                Check("Selecting its slot", Toolbar.Select(slot));
            });
            yield return Wait(1.5f);
            Section("Mod tool held", () =>
            {
                Check("ModTool.IsHeld", tool.IsHeld);
                Check("ModTool.Selected", ToolEvents.Contains("selected"), string.Join(", ", ToolEvents));
                Check("ModTool.WhileHeld", ToolEvents.Contains("held"));
                var held = tool.HeldObject;
                Check("ModTool.HeldObject", held != null && held == Toolbar.HeldObject, held?.name);
                var model = held == null ? null : held.transform.Find("Self-test tool model");
                Check("The model is in the hand", model != null && model.gameObject.activeInHierarchy,
                    model == null ? "missing" : model.position.ToString());
                var shader = model == null ? null : model.GetComponentInChildren<Renderer>()?.sharedMaterial?.shader;
                Check("The model in the hand uses a shader the game draws", shader != null && shader.isSupported && shader.name != "Standard", shader?.name);
                Check("The model in the hand has no collider", model != null && model.GetComponentInChildren<Collider>() == null);
                Check("(info) the held tool", true, DescribeHeld(held));
                Shot("mod-tool-held");
                FruktLog.Msg("[SelfTest] CLICK mod-tool-click 0.5000 0.5000");
            });
            for (float end = Now() + 4f; !ToolEvents.Contains("left release") && Now() < end;)
                yield return null;
            Section("Mod tool clicked", () =>
            {
                Check("A real left click reaches ModTool.LeftClick", ToolEvents.Contains("left click"), string.Join(", ", ToolEvents));
                Check("ModTool.LeftRelease", ToolEvents.Contains("left release"));
            });
            ToolEvents.Clear();
            float longestHold = 0f;
            void OnHold(float seconds) => longestHold = Mathf.Max(longestHold, seconds);
            tool.LeftHold += OnHold;
            FruktLog.Msg("[SelfTest] MOUSEDOWN mod-tool-hold");
            for (float end = Now() + 3f; longestHold < 0.5f && Now() < end;)
                yield return null;
            FruktLog.Msg("[SelfTest] MOUSEUP mod-tool-hold");
            for (float end = Now() + 2f; !ToolEvents.Contains("left release") && Now() < end;)
                yield return null;
            tool.LeftHold -= OnHold;
            Section("Mod tool held down", () =>
            {
                Check("ModTool.LeftHold counts the seconds held", longestHold >= 0.5f, $"{longestHold:0.00}s");
                Check("Letting go raises LeftRelease", ToolEvents.Contains("left release"), string.Join(", ", ToolEvents));
            });

            Section("Mod tool put away", () => Check("Selecting the cursor", Toolbar.Select(Toolbar.CursorSlot)));
            yield return Wait(1f);
            Section("Mod tool put away (deferred)", () =>
            {
                Check("ModTool.Deselected", ToolEvents.Contains("deselected"), string.Join(", ", ToolEvents));
                Check("It's no longer held", !tool.IsHeld && tool.HeldObject == null);
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
            yield return Wait(1f);

            var props = TestModProps();
            while (props.MoveNext())
                yield return props.Current;
        }

        private static string DescribeHeld(GameObject held)
        {
            if (held == null)
                return "nothing held";
            var camera = LocalPlayer.Camera;
            var path = new List<string>();
            for (var t = held.transform; t != null; t = t.parent)
                path.Add($"{t.name}[{LayerMask.LayerToName(t.gameObject.layer)}]");
            var lines = new List<string> { "path: " + string.Join(" < ", path) };
            if (camera != null)
                lines.Add($"camera {camera.name} mask {camera.cullingMask:X} at {camera.transform.position}, held at {camera.transform.InverseTransformPoint(held.transform.position)} (camera space)");
            foreach (var r in held.GetComponentsInChildren<Renderer>(true).Take(6))
            {
                string local = camera == null ? "" : camera.transform.InverseTransformPoint(r.bounds.center).ToString();
                lines.Add($"renderer {r.name} [{LayerMask.LayerToName(r.gameObject.layer)}] enabled={r.enabled} active={r.gameObject.activeInHierarchy} " +
                          $"shader={r.sharedMaterial?.shader?.name} size={r.bounds.size} camera-space={local}");
            }
            return string.Join(" | ", lines);
        }
    }
}
