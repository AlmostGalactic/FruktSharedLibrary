using System.Collections.Generic;
using UnityEngine;

namespace FruktSharedLibrary.Gameplay
{
    /// <summary>
    /// A tab a mod adds to the terminal, next to Weapons, Tools, Etc and Props. Make one with
    /// <see cref="Inventory.AddCategory"/>, then file your items under its name.
    /// </summary>
    /// <example>
    /// <code>
    /// Inventory.AddCategory("Explosives", bundle.Load&lt;Sprite&gt;("ExplosivesIcon"));
    /// Inventory.AddTool("Boom Stick", "Explosives");
    /// </code>
    /// </example>
    public sealed class ModCategory
    {
        internal ModCategory(string name, Sprite icon)
        {
            Name = name;
            Icon = icon;
        }

        /// <summary>The name on its tab's header, and the one to pass to <see cref="Inventory.AddTool"/>.</summary>
        public string Name { get; }

        /// <summary>
        /// The icon on its tab. Null means it uses the icon of the first item in it, or the Etc icon while it has
        /// none.
        /// </summary>
        public Sprite Icon { get; private set; }

        /// <summary>True once it's in the terminal. That happens when the first map loads.</summary>
        public bool Added { get; internal set; }

        /// <summary>The items filed under it.</summary>
        public IReadOnlyList<InventoryItem> Items => Inventory.ItemsIn(Name);

        /// <summary>Sets the icon on its tab. Can be changed at any time.</summary>
        public ModCategory WithIcon(Sprite icon)
        {
            Icon = icon;
            Internal.ModCategories.RefreshIcons();
            return this;
        }
    }
}
