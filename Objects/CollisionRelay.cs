using System;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace FruktSharedLibrary.Objects
{
    /// <summary>
    /// A component written in C# and registered with the game's IL2CPP runtime, so Unity calls its collision
    /// messages. It only forwards them to the <see cref="ObjectEvents"/> it belongs to.
    /// </summary>
    internal sealed class CollisionRelay : MonoBehaviour
    {
        private static bool _registered;
        private static bool _failed;

        public CollisionRelay(IntPtr pointer) : base(pointer)
        {
        }

        internal ObjectEvents Owner { get; set; }

        /// <summary>Registers the type with IL2CPP once. False if that isn't possible in this game.</summary>
        internal static bool EnsureRegistered()
        {
            if (_registered || _failed)
                return _registered;
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<CollisionRelay>();
                _registered = true;
            }
            catch (Exception e)
            {
                _failed = true;
                Core.FruktLog.Warning("Collision events are unavailable: registering the relay component failed: " + e.Message);
            }
            return _registered;
        }

        // Unity finds these by name.
        public void OnCollisionEnter(Collision collision) => Owner?.HandleCollision(collision);
    }
}
