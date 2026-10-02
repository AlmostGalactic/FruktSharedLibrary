// A stand-in for a future version of FruktSharedLibrary: same assembly name, a higher version, one member that
// really exists and one that doesn't. The compatibility test mod is built against this.
using System.Reflection;

[assembly: AssemblyVersion("9.9.0.0")]

namespace FruktSharedLibrary
{
    public static class FutureFeature
    {
        public static void DoThing() { }
    }
}

namespace FruktSharedLibrary.UI
{
    public static class Notifications
    {
        public static void Show(string text, float seconds = 3f) { }
    }
}
