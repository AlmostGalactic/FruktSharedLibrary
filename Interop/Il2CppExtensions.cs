using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using Il2CppCollections = Il2CppSystem.Collections.Generic;

namespace FruktSharedLibrary.Interop
{
    /// <summary>
    /// Helpers that take the pain out of working with IL2CPP proxy objects:
    /// type checks, safe casts, destroyed-object checks and collection conversion.
    /// </summary>
    public static class Il2CppExtensions
    {
        /// <summary>
        /// Real IL2CPP type check. C#'s <c>is</c>/<c>as</c> only see the proxy type you happen to hold,
        /// not the object's actual IL2CPP class, so use this instead.
        /// </summary>
        public static bool Is<T>(this Il2CppObjectBase obj) where T : Il2CppObjectBase
            => obj != null && !obj.WasCollected && obj.TryCast<T>() != null;

        /// <summary>IL2CPP-aware cast that returns null instead of throwing.</summary>
        public static T As<T>(this Il2CppObjectBase obj) where T : Il2CppObjectBase
            => obj == null || obj.WasCollected ? null : obj.TryCast<T>();

        /// <summary>True when the Unity object has not been destroyed (and its proxy is still valid).</summary>
        public static bool Exists(this UnityEngine.Object obj)
            => obj is not null && !obj.WasCollected && obj != null;

        /// <summary>True when the Unity object is null, destroyed, or its proxy was collected.</summary>
        public static bool IsDestroyed(this UnityEngine.Object obj) => !obj.Exists();

        /// <summary>Name of the object's real IL2CPP class, e.g. "Il2CppLVA.Organs.Variants.Brain" → "Brain".</summary>
        public static string GetIl2CppTypeName(this Il2CppObjectBase obj, bool fullName = false)
        {
            if (obj == null || obj.WasCollected)
                return "null";
            try
            {
                var type = obj.Cast<Il2CppSystem.Object>().GetIl2CppType();
                return fullName ? type.FullName : type.Name;
            }
            catch
            {
                return obj.GetType().Name;
            }
        }

        // ---------------------------------------------------------------- collections

        /// <summary>Copies an IL2CPP List into a managed list.</summary>
        public static List<T> ToManagedList<T>(this Il2CppCollections.List<T> list)
        {
            var result = new List<T>();
            if (list == null)
                return result;
            int count = list.Count;
            for (int i = 0; i < count; i++)
                result.Add(list[i]);
            return result;
        }

        /// <summary>Copies an IL2CPP HashSet into a managed list.</summary>
        public static List<T> ToManagedList<T>(this Il2CppCollections.HashSet<T> set)
        {
            var result = new List<T>();
            if (set == null)
                return result;
            foreach (var item in set)
                result.Add(item);
            return result;
        }

        /// <summary>
        /// Copies any IL2CPP collection exposed through an interface (IEnumerable, IReadOnlyCollection,
        /// IReadOnlyList...) into a managed list.
        /// </summary>
        /// <remarks>
        /// Don't enumerate game collections through their interface yourself. Most of them hand out a struct
        /// enumerator boxed behind the interface, and Il2CppInterop calls it with the wrong <c>this</c>
        /// pointer, which shows up as "Collection was modified" or garbage. This helper detects the concrete
        /// collection and reads it safely.
        /// </remarks>
        public static List<T> ToManagedList<T>(this Il2CppCollections.IEnumerable<T> enumerable)
            => enumerable == null ? new List<T>() : ReadCollection<T>(enumerable);

        /// <inheritdoc cref="ToManagedList{T}(Il2CppCollections.IEnumerable{T})"/>
        public static List<T> ToManagedList<T>(this Il2CppCollections.IReadOnlyCollection<T> collection)
            => collection == null ? new List<T>() : ReadCollection<T>(collection);

        /// <inheritdoc cref="ToManagedList{T}(Il2CppCollections.IEnumerable{T})"/>
        public static List<T> ToManagedList<T>(this Il2CppCollections.IReadOnlyList<T> list)
            => list == null ? new List<T>() : ReadCollection<T>(list);

        private static List<T> ReadCollection<T>(Il2CppObjectBase source)
        {
            var list = source.TryCast<Il2CppCollections.List<T>>();
            if (list != null)
                return list.ToManagedList();

            var set = source.TryCast<Il2CppCollections.HashSet<T>>();
            if (set != null)
                return set.ToManagedList();

            var result = new List<T>();
            var readOnlyList = source.TryCast<Il2CppCollections.IReadOnlyList<T>>();
            var readOnlyCollection = source.TryCast<Il2CppCollections.IReadOnlyCollection<T>>();
            if (readOnlyList != null && readOnlyCollection != null)
            {
                int count = readOnlyCollection.Count;
                for (int i = 0; i < count; i++)
                    result.Add(readOnlyList[i]);
                return result;
            }

            // Last resort: the interface enumerator (fine for class-based enumerators such as iterator methods).
            try
            {
                var enumerator = source.Cast<Il2CppCollections.IEnumerable<T>>().GetEnumerator();
                var mover = enumerator.Cast<Il2CppSystem.Collections.IEnumerator>();
                while (mover.MoveNext())
                    result.Add(enumerator.Current);
            }
            catch (Exception e)
            {
                Core.FruktLog.Warning($"Can't enumerate a {source.GetIl2CppTypeName()}: {e.Message}");
            }
            return result;
        }

        /// <summary>Copies a managed sequence into a new IL2CPP List (for passing to game methods).</summary>
        public static Il2CppCollections.List<T> ToIl2CppList<T>(this IEnumerable<T> items)
        {
            var list = new Il2CppCollections.List<T>();
            if (items != null)
            {
                foreach (var item in items)
                    list.Add(item);
            }
            return list;
        }

        // ---------------------------------------------------------------- components

        /// <summary>
        /// GetComponentInParent that works for any IL2CPP component type (including types
        /// Unity's generic overload can't resolve in IL2CPP builds).
        /// </summary>
        public static T GetComponentInParentIl2Cpp<T>(this Component component, bool includeInactive = true) where T : Component
        {
            if (component == null)
                return null;
            var found = component.GetComponentInParent(Il2CppType.Of<T>(), includeInactive);
            return found == null ? null : found.TryCast<T>();
        }

        /// <inheritdoc cref="GetComponentInParentIl2Cpp{T}(Component, bool)"/>
        public static T GetComponentInParentIl2Cpp<T>(this GameObject gameObject, bool includeInactive = true) where T : Component
            => gameObject == null ? null : gameObject.transform.GetComponentInParentIl2Cpp<T>(includeInactive);

        /// <summary>GetComponentInChildren that works for any IL2CPP component type.</summary>
        public static T GetComponentInChildrenIl2Cpp<T>(this Component component, bool includeInactive = true) where T : Component
        {
            if (component == null)
                return null;
            var found = component.GetComponentInChildren(Il2CppType.Of<T>(), includeInactive);
            return found == null ? null : found.TryCast<T>();
        }

        /// <inheritdoc cref="GetComponentInChildrenIl2Cpp{T}(Component, bool)"/>
        public static T GetComponentInChildrenIl2Cpp<T>(this GameObject gameObject, bool includeInactive = true) where T : Component
            => gameObject == null ? null : gameObject.transform.GetComponentInChildrenIl2Cpp<T>(includeInactive);

        /// <summary>Returns the existing component of type T or adds one.</summary>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            var existing = gameObject.GetComponent(Il2CppType.Of<T>());
            if (existing != null)
                return existing.TryCast<T>();
            return gameObject.AddComponent(Il2CppType.Of<T>()).TryCast<T>();
        }

        /// <summary>Runs <paramref name="action"/> and logs instead of throwing. Returns false if it threw.</summary>
        public static bool TryRun(Action action, string context = null)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                Core.FruktLog.Warning($"{context ?? "Action"} failed: {e.Message}");
                Core.FruktLog.Debug(e.ToString());
                return false;
            }
        }
    }
}
