using System;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using Il2CppInterop.Runtime.Attributes;
using Il2CppPlayer.Appearances.God.InventoryItems;
using Il2CppServices.Audio;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// The held item behind every <see cref="ModTool"/>: a subclass of the game's own held-item class, registered
    /// with IL2CPP, so the game's toolbar creates, selects and destroys it like its own items and passes it the mouse
    /// buttons. It only forwards those to the tool it belongs to.
    /// </summary>
    internal sealed class ModToolBehaviour : GodInventoryItem
    {
        public ModToolBehaviour(IntPtr pointer) : base(pointer)
        {
            Prepare(this);
        }

        // The game's items get their feature list while their prefab is built in the editor; a fresh component has
        // none, and the game iterates it when the item is picked or put away.
        [HideFromIl2Cpp]
        internal static void Prepare(GodInventoryItem item)
        {
            try
            {
                if (item != null && item._Features_k__BackingField == null)
                    item._Features_k__BackingField = new Il2CppSystem.Collections.Generic.List<GIIFeature>();
            }
            catch (Exception e)
            {
                FruktLog.Warning("Setting up a mod tool failed: " + e.Message);
            }
        }

        // The game's items are handed this service by dependency injection, which skips injected classes.
        public override ISFXPlayerService SfxPlayerService => GameServices.TryGet<ISFXPlayerService>();

        // Overrides don't call the base methods: from an injected class that call dispatches virtually and would land
        // back here. Mod tools have no game features for the base versions to pass input on to anyway.
        public override void OnLeftMouseButtonClick() => ModItems.Input(this, ToolInput.LeftClick);
        public override void OnLeftMouseButtonHold(float holdTime) => ModItems.Input(this, ToolInput.LeftHold, holdTime);
        public override void OnLeftMouseButtonRelease() => ModItems.Input(this, ToolInput.LeftRelease);
        public override void OnRightMouseButtonClick() => ModItems.Input(this, ToolInput.RightClick);
        public override void OnRightMouseButtonHold(float holdTime) => ModItems.Input(this, ToolInput.RightHold, holdTime);
        public override void OnRightMouseButtonRelease() => ModItems.Input(this, ToolInput.RightRelease);
        public override void OnMiddleMouseButtonClick() => ModItems.Input(this, ToolInput.MiddleClick);
        public override void OnMiddleMouseButtonScroll(float notches) => ModItems.Input(this, ToolInput.Scroll, notches);
    }
}
