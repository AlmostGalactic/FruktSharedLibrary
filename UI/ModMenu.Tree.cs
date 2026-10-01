using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MelonLoader;

namespace FruktSharedLibrary.UI
{
    // How the menu is organised:
    //   MODS               generated: one line per installed mod that uses the library
    //     <mod name>       generated: version and author, then the mod's own page(s)
    //   <library pages>    pages the library adds itself (Sandbox Tools)
    //   LIBRARY SETTINGS   always last
    public static partial class ModMenu
    {
        private static readonly Dictionary<MelonBase, ModMenuPage> ModPages = new();
        private static ModMenuPage _modsPage;
        private static string _treeSignature;

        /// <summary>Installed mods that use the library (reference it, or added a page), sorted by name.</summary>
        public static IReadOnlyList<MelonBase> LibraryMods
        {
            get
            {
                var result = new List<MelonBase>();
                foreach (var melon in MelonBase.RegisteredMelons)
                {
                    if (melon == null || melon == FruktSharedLibraryMod.Instance)
                        continue;
                    if (UsesLibrary(melon) || Pages.Exists(p => p.Owner == melon))
                        result.Add(melon);
                }
                result.Sort((a, b) => string.Compare(a.Info?.Name, b.Info?.Name, StringComparison.OrdinalIgnoreCase));
                return result;
            }
        }

        /// <summary>The top-level lines: MODS, the library's own pages, then Library settings.</summary>
        internal static IEnumerable<ModMenuPage> RootPages()
        {
            RefreshTree();
            yield return _modsPage;
            foreach (var page in Pages)
            {
                if (IsRootPage(page) && !page.ListLast)
                    yield return page;
            }
            foreach (var page in Pages)
            {
                if (IsRootPage(page) && page.ListLast)
                    yield return page;
            }
        }

        // The library's own pages, and pages from code that isn't a registered mod, are listed at the top level.
        private static bool IsRootPage(ModMenuPage page) => page.Owner == null;

        /// <summary>The generated MODS page.</summary>
        internal static ModMenuPage ModsPage
        {
            get
            {
                RefreshTree();
                return _modsPage;
            }
        }

        /// <summary>The generated page of one mod (null if the mod doesn't use the library).</summary>
        internal static ModMenuPage PageOfMod(MelonBase mod)
        {
            RefreshTree();
            return mod != null && ModPages.TryGetValue(mod, out var page) ? page : null;
        }

        /// <summary>Rebuilds the generated pages when mods, their pages, or those pages' contents changed.</summary>
        internal static void RefreshTree()
        {
            var mods = LibraryMods;
            string signature = TreeSignature(mods);
            if (signature == _treeSignature && _modsPage != null)
                return;
            _treeSignature = signature;

            _modsPage ??= new ModMenuPage("Mods");
            _modsPage.Clear();
            if (mods.Count == 0)
                _modsPage.Label("No installed mod uses FruktSharedLibrary yet. Mods built on it show up here with their settings.");
            foreach (var mod in mods)
                _modsPage.Link(mod.Info?.Name ?? "?", BuildModPage(mod));
        }

        private static ModMenuPage BuildModPage(MelonBase mod)
        {
            if (!ModPages.TryGetValue(mod, out var page))
                ModPages[mod] = page = new ModMenuPage(mod.Info?.Name ?? "?") { Owner = mod };
            page.Clear();
            string author = string.IsNullOrWhiteSpace(mod.Info?.Author) ? "" : $" by {mod.Info.Author}";
            page.Label($"version {mod.Info?.Version}{author}");

            var own = Pages.FindAll(p => p.Owner == mod && IsPageVisible(p));
            if (own.Count == 0)
            {
                page.Label("This mod has no settings.");
            }
            else if (own.Count == 1)
            {
                // A single page is shown right here instead of behind another click.
                page.Separator().AddShared(own[0].Items);
            }
            else
            {
                page.Separator();
                foreach (var child in own)
                    page.Link(child.Title, child);
            }
            return page;
        }

        private static string TreeSignature(IReadOnlyList<MelonBase> mods)
        {
            var builder = new StringBuilder();
            foreach (var mod in mods)
            {
                builder.Append(mod.Info?.Name).Append('[');
                foreach (var page in Pages)
                {
                    if (page.Owner == mod)
                        builder.Append(page.Title).Append(':').Append(page.Version).Append(IsPageVisible(page) ? '+' : '-').Append(';');
                }
                builder.Append(']');
            }
            return builder.ToString();
        }

        private static readonly Dictionary<MelonBase, bool> UsesLibraryCache = new();

        private static bool UsesLibrary(MelonBase melon)
        {
            if (UsesLibraryCache.TryGetValue(melon, out bool uses))
                return uses;
            try
            {
                var assembly = melon.MelonAssembly?.Assembly;
                string name = typeof(ModMenu).Assembly.GetName().Name;
                uses = assembly != null && assembly.GetReferencedAssemblies().Any(r => r.Name == name);
            }
            catch
            {
                uses = false;
            }
            UsesLibraryCache[melon] = uses;
            return uses;
        }

        /// <summary>The mod an assembly belongs to; null for the library itself or code that isn't a mod.</summary>
        internal static MelonBase FindOwner(Assembly assembly)
        {
            if (assembly == null || assembly == typeof(ModMenu).Assembly)
                return null;
            foreach (var melon in MelonBase.RegisteredMelons)
            {
                if (melon?.MelonAssembly?.Assembly == assembly)
                    return melon;
            }
            return null;
        }
    }
}
