using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MelonLoader;

// MelonLoader mod information
[assembly: MelonInfo(typeof(FruktSharedLibrary.FruktSharedLibraryMod), "FruktSharedLibrary", FruktSharedLibrary.FruktSharedLibraryMod.Version, "jjlala1313")]
[assembly: MelonGame("tripledose", "FRUKT")]
// Initialize before any mod that depends on this library
[assembly: MelonPriority(-10000)]
// Patches are applied one at a time by the library so a single broken patch can't take the rest down
[assembly: HarmonyDontPatchAll]

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("FruktSharedLibrary")]
[assembly: AssemblyDescription("Shared modding library for FRUKT (MelonLoader, IL2CPP)")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("FruktSharedLibrary")]
[assembly: AssemblyCopyright("Copyright ©  2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("5fb6ca29-bb77-4b77-8744-bc7a5fe7abed")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
