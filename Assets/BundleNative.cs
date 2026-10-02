using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Bindings;

namespace FruktSharedLibrary.Assets
{
    /// <summary>
    /// Calls Unity's native AssetBundle functions directly. In Unity 6 the managed AssetBundle methods pass strings
    /// and byte arrays as spans, which the interop can't do (they throw MissingMethodException in this game), so
    /// the library resolves the internal calls itself and passes a pinned pointer and length instead.
    /// </summary>
    internal static class BundleNative
    {
        /// <summary>Unity's ManagedSpanWrapper: a pointer to the first element and how many there are.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Span
        {
            public IntPtr Begin;
            public int Length;
        }

        private delegate IntPtr LoadFromFileCall(ref Span path, uint crc, ulong offset);
        private delegate IntPtr LoadFromMemoryCall(ref Span bytes, uint crc);
        private delegate IntPtr GetAllAssetNamesCall(IntPtr self);
        private delegate IntPtr LoadAssetCall(IntPtr self, ref Span name, IntPtr type);
        private delegate void UnloadCall(IntPtr self, [MarshalAs(UnmanagedType.U1)] bool unloadAllLoadedObjects);

        private static LoadFromFileCall _loadFromFile;
        private static LoadFromMemoryCall _loadFromMemory;
        private static GetAllAssetNamesCall _getAllAssetNames;
        private static LoadAssetCall _loadAsset;
        private static UnloadCall _unload;

        internal static AssetBundle LoadFromFile(string path)
        {
            var call = Resolve(ref _loadFromFile, "LoadFromFile_Internal_Injected");
            return WithPinned(path, span => Unwrap<AssetBundle>(call(ref span, 0, 0)));
        }

        internal static AssetBundle LoadFromMemory(byte[] bytes)
        {
            var call = Resolve(ref _loadFromMemory, "LoadFromMemory_Internal_Injected");
            return WithPinned(bytes, bytes.Length, span => Unwrap<AssetBundle>(call(ref span, 0)));
        }

        internal static string[] GetAllAssetNames(AssetBundle bundle)
        {
            var result = Resolve(ref _getAllAssetNames, "GetAllAssetNames_Injected")(Self(bundle));
            if (result == IntPtr.Zero)
                return Array.Empty<string>();
            var names = new Il2CppStringArray(result);
            var copy = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
                copy[i] = names[i];
            return copy;
        }

        internal static UnityEngine.Object LoadAsset(AssetBundle bundle, string name, Il2CppSystem.Type type)
        {
            var call = Resolve(ref _loadAsset, "LoadAsset_Internal_Injected");
            var self = Self(bundle);
            return WithPinned(name, span => Unwrap<UnityEngine.Object>(call(self, ref span, type.Pointer)));
        }

        internal static void Unload(AssetBundle bundle, bool unloadAllLoadedObjects)
            => Resolve(ref _unload, "Unload_Injected")(Self(bundle), unloadAllLoadedObjects);

        // ------------------------------------------------------------ helpers

        private static T Resolve<T>(ref T cache, string name) where T : Delegate
            => cache ??= IL2CPP.ResolveICall<T>("UnityEngine.AssetBundle::" + name)
                         ?? throw new MissingMethodException($"Unity's internal call AssetBundle::{name} isn't in this game.");

        private static TResult WithPinned<TResult>(string text, Func<Span, TResult> call)
        {
            var handle = GCHandle.Alloc(text, GCHandleType.Pinned);
            try
            {
                return call(new Span { Begin = handle.AddrOfPinnedObject(), Length = text.Length });
            }
            finally
            {
                handle.Free();
            }
        }

        private static TResult WithPinned<TResult>(byte[] bytes, int length, Func<Span, TResult> call)
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return call(new Span { Begin = handle.AddrOfPinnedObject(), Length = length });
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>The native object pointer Unity's internal calls expect for "this".</summary>
        private static IntPtr Self(AssetBundle bundle)
        {
            var pointer = bundle != null ? bundle.m_CachedPtr : IntPtr.Zero;
            if (pointer == IntPtr.Zero)
                throw new ObjectDisposedException("AssetBundle", "The bundle was unloaded.");
            return pointer;
        }

        /// <summary>Unity 6's internal calls return objects as GC handles; turn one back into the object.</summary>
        private static T Unwrap<T>(IntPtr gcHandle) where T : Il2CppObjectBase
        {
            if (gcHandle == IntPtr.Zero)
                return null;
            var target = Unmarshal.FromIntPtrUnsafe(gcHandle).Target;
            return target?.TryCast<T>();
        }
    }
}
