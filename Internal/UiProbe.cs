using System;
using System.Collections;
using System.IO;
using System.Text;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using Il2CppData.Maps;
using Il2CppPresenters.Pause;
using Il2CppTMPro;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// Development probe: dumps the game's UI hierarchies (pause menu, settings screens, context menu) to
    /// UserData/FruktSharedLibrary.probe.txt and requests screenshots. Enabled when the self-test flag file
    /// contains "probe".
    /// </summary>
    internal static class UiProbe
    {
        private static readonly StringBuilder Output = new();

        internal static bool Requested(string flagText) => flagText.IndexOf("probe", StringComparison.OrdinalIgnoreCase) >= 0;

        internal static IEnumerator Run(bool quit)
        {
            Output.Clear();
            Section("Fonts", DumpFonts);

            for (float end = Now() + 5f; Now() < end;) yield return null;
            Section("Pause", () => World.Pause());
            for (float end = Now() + 2f; Now() < end;) yield return null;
            Shot("pause-root");
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Section("PauseView", () =>
            {
                var view = GameServices.FindObject<Il2CppViews.Pause.PauseView>(true);
                Dump(view?.gameObject, 14);
            });

            var presenter = GameServices.FindObject<PausePresenter>(true);
            Section("Open settings", () => presenter?.OpenSettings());
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Shot("pause-settings");
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Section("Open game", () => presenter?.OpenGame());
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Shot("pause-game");
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Section("Open interface", () => presenter?.OpenInterface());
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Shot("pause-interface");
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Section("Open input", () => presenter?.OpenInput());
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Shot("pause-input");
            for (float end = Now() + 1.5f; Now() < end;) yield return null;

            Section("Resume", () => World.Resume());
            for (float end = Now() + 1.5f; Now() < end;) yield return null;

            Il2CppLVA.Creatures.AbstractCreature human = null;
            Creatures.SpawnHumanInFront(3f, c => human = c);
            for (float end = Now() + 10f; human == null && Now() < end;) yield return null;
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Section("Context menu", () =>
            {
                var head = human?.GetLimb(Il2CppLVA.NodesHierarchy.Benchmark.Variants.HumanoidNodeTagValue.Head);
                var handler = head?.GetComponentInChildrenIl2Cpp<Il2CppLVA.LimbContextMenu.LimbContextMenuActionsHandler>();
                GameServices.TryGet<Il2CppServices.UI.IContextMenuService>()?.TryOpen(handler);
            });
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Section("ContextMenuWindow", () =>
            {
                var window = GameServices.FindObject<Il2CppViews.ContextMenu.ContextMenuWindow>(true);
                Dump(window?.transform.root.gameObject, 12);
            });
            Section("HUD", () =>
            {
                var hint = GameServices.FindObject<Il2CppViews.Hints.HintBarView>(true);
                Dump(hint?.gameObject, 8);
                var toolbar = GameServices.FindObject<Il2CppViews.Toolbar.ToolbarView>(true);
                Dump(toolbar?.gameObject, 5);
            });

            File.WriteAllText(Path.Combine(MelonEnvironment.UserDataDirectory, "FruktSharedLibrary.probe.txt"), Output.ToString());
            FruktLog.Msg("[SelfTest] Probe written. [SelfTest] Done");
            if (quit)
                Scheduler.After(2f, World.Quit);
        }

        private static void DumpFonts()
        {
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                Output.AppendLine($"TMP_FontAsset '{font.name}' source={(font.sourceFontFile == null ? "null" : font.sourceFontFile.name)}");
            foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
                Output.AppendLine($"Font '{font.name}' dynamic={font.dynamic} size={font.fontSize}");
        }

        private static void Dump(GameObject root, int depth)
        {
            if (root == null)
            {
                Output.AppendLine("(not found)");
                return;
            }
            Describe(root.transform, 0, depth);
        }

        private static void Describe(Transform t, int depth, int maxDepth)
        {
            var line = new StringBuilder();
            line.Append(new string(' ', depth * 2)).Append(t.gameObject.activeSelf ? "" : "(off) ").Append(t.name);
            var rect = t.TryCast<RectTransform>();
            if (rect != null)
                line.Append($" rect={rect.rect.width:0}x{rect.rect.height:0} anchor=({rect.anchorMin.x:0.##},{rect.anchorMin.y:0.##})-({rect.anchorMax.x:0.##},{rect.anchorMax.y:0.##}) pos={rect.anchoredPosition}");
            line.Append(" [");
            foreach (var component in t.GetComponents<Component>())
            {
                if (component == null)
                    continue;
                string name = component.GetIl2CppTypeName();
                if (name is "RectTransform" or "Transform" or "CanvasRenderer")
                    continue;
                line.Append(name);
                var text = component.TryCast<TMP_Text>();
                if (text != null)
                    line.Append($"(\"{Short(text.text)}\" font={text.font?.name} size={text.fontSize} color={Hex(text.color)} upper={text.fontStyle})");
                var image = component.TryCast<Image>();
                if (image != null)
                    line.Append($"(color={Hex(image.color)} sprite={image.sprite?.name})");
                var layout = component.TryCast<HorizontalOrVerticalLayoutGroup>();
                if (layout != null)
                    line.Append($"(spacing={layout.spacing} pad={layout.padding.left},{layout.padding.right},{layout.padding.top},{layout.padding.bottom})");
                var element = component.TryCast<LayoutElement>();
                if (element != null)
                    line.Append($"(minH={element.minHeight} prefH={element.preferredHeight})");
                line.Append(", ");
            }
            line.Append(']');
            Output.AppendLine(line.ToString());
            if (depth >= maxDepth)
                return;
            for (int i = 0; i < t.childCount; i++)
                Describe(t.GetChild(i), depth + 1, maxDepth);
        }

        private static string Short(string s) => s == null ? "" : (s.Length > 40 ? s.Substring(0, 40) + "…" : s).Replace("\n", "\\n");

        private static string Hex(Color c) => $"#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}{(int)(c.a * 255):X2}";

        private static void Shot(string name) => FruktLog.Msg("[SelfTest] SCREENSHOT " + name);

        private static void Section(string name, Action body)
        {
            Output.AppendLine().AppendLine("===== " + name);
            try
            {
                body();
            }
            catch (Exception e)
            {
                Output.AppendLine("!! " + e);
            }
        }

        private static float Now() => Time.realtimeSinceStartup;
    }
}
