using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.UI;
using Il2CppLVA.Limbs;
using Il2CppServices.UI;
using Il2CppTMPro;
using Il2CppViews.ContextMenu;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // Drop-down groups in the game's right-click menu, driven by real clicks (see SelfTest.Menu.cs for markers).
    internal static partial class SelfTest
    {
        private static IEnumerator TestContextMenuGroups(AbstractLimb head)
        {
            var handler = FindLimbMenuHandler(head);
            var service = GameServices.TryGet<IContextMenuService>();
            if (handler == null || service == null)
            {
                Check("Context menu groups: menu available", false, $"handler={handler != null} service={service != null}");
                yield break;
            }

            int leafClicks = 0;
            bool toggled = false;
            bool deep = false;
            // Registered after the head's menu was built, so this also covers adding groups to live menus.
            var group = ContextMenus.AddCreatureGroup("Self-test group")
                .AddCreatureAction("Group action", _ => leafClicks++)
                .AddToggle("Group toggle", _ => toggled, (_, on) => toggled = on);
            group.AddGroup("Nested group").AddAction("Deep action", _ => deep = true);

            // A real right-click frees the cursor; opening the menu from code doesn't, so do it here.
            var cursorOwner = new Il2CppSystem.Object();
            Gameplay.LocalPlayer.CaptureCursor(cursorOwner);
            try
            {
                Section("Context group open", () => Check("Context menu opened for the group test", service.TryOpen(handler)));
                yield return Wait(1.2f);
                var rows = MenuRows();
                Check("A collapsed group shows only its own line", rows.Contains("+ SELF-TEST GROUP") && !rows.Contains("GROUP ACTION"), string.Join(" | ", rows));
                Shot("context-collapsed");
                yield return Wait(1.5f);

                Click("ctx-group", MenuRow("+ SELF-TEST GROUP"));
                yield return Wait(1.2f);
                rows = MenuRows();
                Check("Clicking a group keeps the menu open", MenuOpen());
                int at = rows.IndexOf("- SELF-TEST GROUP");
                Check("The group expands in place, children right under it",
                    at >= 0 && at + 3 < rows.Count + 1 && rows.IndexOf("GROUP ACTION") == at + 1 && rows.IndexOf("GROUP TOGGLE: OFF") == at + 2 && rows.IndexOf("+ NESTED GROUP") == at + 3,
                    string.Join(" | ", rows));
                Shot("context-expanded");
                yield return Wait(1.5f);

                Click("ctx-toggle", MenuRow("GROUP TOGGLE: OFF"));
                yield return Wait(1.2f);
                rows = MenuRows();
                Check("A toggle in a group flips without closing the menu", toggled && MenuOpen() && rows.Contains("GROUP TOGGLE: ON"), string.Join(" | ", rows));

                Click("ctx-nested", MenuRow("+ NESTED GROUP"));
                yield return Wait(1.2f);
                rows = MenuRows();
                int nestedAt = rows.IndexOf("- NESTED GROUP");
                Check("Groups nest", nestedAt >= 0 && rows.IndexOf("DEEP ACTION") == nestedAt + 1, string.Join(" | ", rows));
                Shot("context-nested");
                yield return Wait(1.5f);

                Click("ctx-deep", MenuRow("DEEP ACTION"));
                yield return Wait(1.2f);
                Check("An action deep in a group runs and closes the menu", deep && !MenuOpen(), $"deep={deep} open={MenuOpen()}");
                Check("Actions in groups only run when clicked", leafClicks == 0);

                Section("Context group reopen", () => service.TryOpen(handler));
                yield return Wait(1.2f);
                rows = MenuRows();
                Check("Groups are collapsed again when the menu reopens", rows.Contains("+ SELF-TEST GROUP") && !rows.Contains("GROUP ACTION"), string.Join(" | ", rows));
                Section("Context group close", () => service.CloseMenu());
                yield return Wait(0.6f);

                group.Remove();
                Check("Removing a group takes it out of built menus", ContextMenuCarrier.ActionFor(handler, group.Entry) == null);
            }
            finally
            {
                group.Remove();
                if (MenuOpen())
                    service.CloseMenu();
                Gameplay.LocalPlayer.ReleaseCursor(cursorOwner);
            }
        }

        private static bool MenuOpen()
        {
            var window = GameServices.TryGet<IContextMenuWindow>();
            if (window != null)
                return window.IsOpen;
            var view = GameServices.FindObject<ContextMenuWindow>();
            return view != null && view.gameObject.activeInHierarchy;
        }

        /// <summary>The open context menu's visible lines, top to bottom, as plain upper-case text.</summary>
        private static List<string> MenuRows()
        {
            var rows = new List<(float Y, string Text)>();
            foreach (var button in MenuButtons())
            {
                if (button == null || !button.gameObject.activeInHierarchy)
                    continue;
                var text = button.GetComponentInChildren<TMP_Text>();
                if (text == null)
                    continue;
                rows.Add((button.transform.position.y, Plain(text.text)));
            }
            rows.Sort((a, b) => b.Y.CompareTo(a.Y));
            return rows.ConvertAll(r => r.Text);
        }

        /// <summary>Row buttons of the context menu window only (other menus reuse the same button type).</summary>
        private static List<ContextMenuActionButton> MenuButtons()
        {
            var result = new List<ContextMenuActionButton>();
            var window = GameServices.FindObject<ContextMenuWindow>();
            if (window == null)
                return result;
            foreach (var button in window.GetComponentsInChildren<ContextMenuActionButton>())
                result.Add(button);
            return result;
        }

        private static RectTransform MenuRow(string text)
        {
            foreach (var button in MenuButtons())
            {
                if (button == null || !button.gameObject.activeInHierarchy)
                    continue;
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null && Plain(label.text) == text)
                    return button.GetComponent<RectTransform>();
            }
            return null;
        }

        private static string Plain(string text)
        {
            text = Regex.Replace(text ?? string.Empty, "<[^>]+>", string.Empty).Trim();
            if (text.StartsWith(">"))
                text = text.Substring(1);
            return text.TrimEnd('_').Trim().ToUpperInvariant();
        }
    }
}
