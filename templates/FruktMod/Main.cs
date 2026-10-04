using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.UI;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(FruktMod.Main), "FruktMod", FruktMod.Main.Version, "MOD_AUTHOR")]
[assembly: MelonGame("tripledose", "FRUKT")]
// Loads FruktSharedLibrary first, and warns players who don't have it.
[assembly: MelonAdditionalDependencies("FruktSharedLibrary")]

namespace FruktMod
{
    public class Main : MelonMod
    {
        public const string Version = "1.0.0";

        public override void OnInitializeMelon()
        {
            // A page in the mod menu (F8, or MODS in the pause menu).
            ModMenu.AddPage("FruktMod")
                .Label("Your mod's settings and buttons go here.")
                .Button("Say hello", () => Notifications.Show("Hello from FruktMod!"));

            // A tool in the terminal. Put it on the toolbar and click.
            Inventory.AddTool("FruktMod tool")
                .WithDescription("Launches whatever you point at.")
                .OnLeftClick(() =>
                {
                    if (LocalPlayer.Raycast(out var hit) && hit.rigidbody != null)
                        hit.rigidbody.AddForce(Vector3.up * 15f, ForceMode.VelocityChange);
                });

            GameEvents.SandboxReady += map => LoggerInstance.Msg($"A map is ready: {map}");
        }
    }
}
