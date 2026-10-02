// A mod built against a "future" FruktSharedLibrary (FutureLibrary/). The real library should refuse to start
// it, because it uses FutureFeature, which doesn't exist yet. If it does start, it says so in the log.
using FruktSharedLibrary;
using FruktSharedLibrary.UI;
using MelonLoader;

[assembly: MelonInfo(typeof(CompatTestMod.CompatTestMod), "FSL Compat Test", "1.0.0", "FruktSharedLibrary self-test")]
[assembly: MelonGame("tripledose", "FRUKT")]
[assembly: MelonAdditionalDependencies("FruktSharedLibrary")]

namespace CompatTestMod
{
    public sealed class CompatTestMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Error("FSL Compat Test started. The library should have stopped it.");
            Notifications.Show("FSL Compat Test started");
            FutureFeature.DoThing();
        }
    }
}
