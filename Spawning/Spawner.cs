using System;
using System.Collections.Generic;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using Il2CppInfrastructure.Project.Registration;
using Il2CppInfrastructure.Project.Registration.Native;
using Il2CppInfrastructure.Scenes.Factories;
using Il2CppServices.Game;
using Il2CppServices.Infrastructure;
using Il2CppServices.Spawnables;
using Il2CppSpawnables.Props;
using Il2CppSpawnables.Weapons;
using UnityEngine;

namespace FruktSharedLibrary.Spawning
{
    /// <summary>The firearms that ship with the game.</summary>
    public enum FirearmType
    {
        /// <summary>Viper-17 pistol (9mm).</summary>
        Viper17,
        /// <summary>Lynx-F rifle (7.62).</summary>
        LynxF,
        /// <summary>Grist-03 pump shotgun (12ga).</summary>
        Grist03,
    }

    /// <summary>
    /// Spawning the game's registered objects (weapons, props, spawners...) through its own factory, so
    /// they are injected, registered with the map and cleaned up by "reset map" exactly like in-game items.
    /// Humans are spawned with <see cref="Entities.Creatures.SpawnHuman"/>.
    /// </summary>
    public static class Spawner
    {
        /// <summary>
        /// IDs of every prefab registered with the game (weapons, bullets, props, particles...).
        /// Use them with <see cref="Spawn(string, Vector3, Quaternion?)"/>.
        /// </summary>
        public static List<string> GetRegisteredPrefabIds()
        {
            var result = new List<string>();
            var registration = Registration;
            var map = registration?.m_idToGetPrefabMethodsMap;
            if (map == null)
                return result;
            foreach (var id in map.Keys)
            {
                if (id != null)
                    result.Add(id.ID);
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>
        /// IDs of the registered prefabs whose component is (or derives from) <typeparamref name="T"/>,
        /// e.g. <c>GetPrefabIds&lt;Firearm&gt;()</c> or <c>GetPrefabIds&lt;Prop&gt;()</c>.
        /// </summary>
        public static List<string> GetPrefabIds<T>() where T : RegistrableBehaviour
        {
            var result = new List<string>();
            var types = Registration?.m_idToPrefabTypesMap;
            if (types == null)
                return result;
            Il2CppSystem.Type wanted;
            try
            {
                wanted = Il2CppInterop.Runtime.Il2CppType.Of<T>();
            }
            catch
            {
                return result;
            }
            foreach (var pair in types)
            {
                try
                {
                    if (pair.Key != null && pair.Value != null && wanted.IsAssignableFrom(pair.Value))
                        result.Add(pair.Key.ID);
                }
                catch
                {
                    // Skip entries whose type can't be inspected.
                }
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>Registered names of the map props you can spawn with <see cref="SpawnProp"/>.</summary>
        public static List<string> GetPropNames()
        {
            var result = new List<string>();
            var registration = GameServices.TryGet<INativeMapPropsHandler>()?.TryCast<NativeMapPropsRegistration>();
            var passports = registration?.Props?.Passports;
            if (passports != null)
            {
                foreach (var name in passports.Keys)
                    result.Add(name);
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>Spawns a registered prefab by ID (see <see cref="GetRegisteredPrefabIds"/>).</summary>
        public static RegistrableBehaviour Spawn(string prefabId, Vector3 position, Quaternion? rotation = null)
        {
            if (string.IsNullOrEmpty(prefabId))
                throw new ArgumentException("Prefab id is empty.", nameof(prefabId));
            return Spawn(new PrefabID(prefabId), position, rotation);
        }

        /// <summary>Spawns a registered prefab.</summary>
        public static RegistrableBehaviour Spawn(PrefabID prefabId, Vector3 position, Quaternion? rotation = null)
        {
            var factory = GameServices.TryGet<ISpawnablesFactory>();
            if (factory == null)
            {
                FruktLog.Warning("Can't spawn: the spawnables factory isn't available (not in a map?).");
                return null;
            }
            try
            {
                return factory.CreateSpawnable(prefabId, position, rotation ?? Quaternion.identity, null);
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Spawning '{prefabId?.ID}' failed: {e.Message}");
                return null;
            }
        }

        /// <summary>Spawns a prefab in front of the player.</summary>
        public static RegistrableBehaviour SpawnInFront(string prefabId, float distance = 2f, float height = 0.5f)
        {
            var position = LocalPlayer.GetPointInFront(distance) + Vector3.up * height;
            return Spawn(prefabId, position, LocalPlayer.RotationFacingPlayer(position));
        }

        /// <summary>Spawns one of the game's firearms.</summary>
        public static Firearm SpawnFirearm(FirearmType type, Vector3 position, Quaternion? rotation = null)
        {
            var weapons = GameServices.TryGet<INativeSpawnablesHandler>()?.Weapons;
            if (weapons == null)
            {
                FruktLog.Warning("Can't spawn a firearm: weapons aren't registered yet.");
                return null;
            }
            PrefabID passport = type switch
            {
                FirearmType.Viper17 => weapons.Viper17,
                FirearmType.LynxF => weapons.LynxF,
                FirearmType.Grist03 => weapons.Grist03,
                _ => null,
            };
            return passport == null ? null : Spawn(passport, position, rotation)?.TryCast<Firearm>();
        }

        /// <summary>Spawns a firearm in front of the player.</summary>
        public static Firearm SpawnFirearmInFront(FirearmType type, float distance = 2f)
        {
            var position = LocalPlayer.GetPointInFront(distance) + Vector3.up * 0.5f;
            return SpawnFirearm(type, position, LocalPlayer.RotationFacingPlayer(position));
        }

        /// <summary>Spawns a prop by its registered name (see <see cref="GetPropNames"/>).</summary>
        public static Prop SpawnProp(string registeredName, Vector3 position, Quaternion? rotation = null)
        {
            var handler = GameServices.TryGet<INativeMapPropsHandler>();
            if (handler == null)
                return null;
            try
            {
                var passport = handler.PassportOf(registeredName);
                return passport == null ? null : Spawn(passport, position, rotation)?.TryCast<Prop>();
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Spawning prop '{registeredName}' failed: {e.Message}");
                return null;
            }
        }

        /// <summary>Objects spawned by the player (or mods) that are currently in the world.</summary>
        public static List<RegistrableBehaviour> GetSpawnedObjects()
        {
            var registry = GameServices.TryGet<IMapObjectsRegistryService>();
            if (registry == null)
                return new List<RegistrableBehaviour>();
            var buffer = new Il2CppSystem.Collections.Generic.List<RegistrableBehaviour>();
            registry.CopySpawnedTo(buffer);
            return buffer.ToManagedList().Where(o => o.Exists()).ToList();
        }

        /// <summary>All firearms currently in the world.</summary>
        public static List<Firearm> GetFirearms() => GameServices.FindObjects<Firearm>();

        /// <summary>Removes a spawned object from the world.</summary>
        public static void Despawn(RegistrableBehaviour spawned)
        {
            if (spawned.Exists())
                UnityEngine.Object.Destroy(spawned.gameObject);
        }

        private static PrefabsRegistration Registration
        {
            get
            {
                var viaService = GameServices.TryGet<IRegisteredPrefabsHandler>()?.TryCast<PrefabsRegistration>();
                return viaService ?? GameServices.FindObject<PrefabsRegistration>(includeInactive: true);
            }
        }
    }
}
