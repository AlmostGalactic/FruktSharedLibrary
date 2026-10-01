using System;
using System.Collections.Generic;
using FruktSharedLibrary.Interop;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppZenject;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FruktSharedLibrary.Core
{
    /// <summary>
    /// Access to the game's services. FRUKT is built on Zenject dependency injection, so almost every
    /// system (pause, time scale, gravity, creatures, spawning, the player, audio...) is a service
    /// bound in a Zenject container. This class resolves them for you.
    /// </summary>
    /// <example>
    /// <code>
    /// var pause = GameServices.TryGet&lt;IPauseService&gt;();
    /// if (pause != null &amp;&amp; !pause.Paused) pause.SetPause();
    /// </code>
    /// </example>
    public static class GameServices
    {
        private static readonly Dictionary<Type, Il2CppObjectBase> Cache = new();
        private static SceneContext _sceneContext;
        private static float _nextSceneContextSearch;

        /// <summary>The project-wide container (lives for the whole game session), or null before boot.</summary>
        public static DiContainer ProjectContainer
        {
            get
            {
                try
                {
                    return ProjectContext.HasInstance ? ProjectContext.Instance.Container : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>The container of the currently loaded scene (main menu or map), or null while loading.</summary>
        public static DiContainer SceneContainer => FindSceneContext()?.Container;

        /// <summary>The most specific container available (scene, falling back to project).</summary>
        public static DiContainer Container => SceneContainer ?? ProjectContainer;

        /// <summary>
        /// Resolves a game service by its interface (recommended, e.g. <c>IPauseService</c>) or concrete type.
        /// Returns null if it isn't bound in the current scene.
        /// </summary>
        public static T TryGet<T>() where T : Il2CppObjectBase
        {
            if (Cache.TryGetValue(typeof(T), out var cached) && IsUsable(cached))
                return (T)cached;

            Il2CppSystem.Type il2CppType;
            try
            {
                il2CppType = Il2CppType.Of<T>();
            }
            catch (Exception e)
            {
                FruktLog.Debug($"{typeof(T).Name} has no IL2CPP type: {e.Message}");
                return null;
            }

            var result = Resolve<T>(SceneContainer, il2CppType) ?? Resolve<T>(ProjectContainer, il2CppType);
            if (result != null)
                Cache[typeof(T)] = result;
            else
                Cache.Remove(typeof(T));
            return result;
        }

        /// <summary>Resolves a game service, returning false if it isn't available.</summary>
        public static bool TryGet<T>(out T service) where T : Il2CppObjectBase
        {
            service = TryGet<T>();
            return service != null;
        }

        /// <summary>Resolves a game service or throws <see cref="InvalidOperationException"/>.</summary>
        public static T Get<T>() where T : Il2CppObjectBase
        {
            var service = TryGet<T>();
            if (service == null)
                throw new InvalidOperationException(
                    $"Game service {typeof(T).Name} is not available in scene '{SceneManager.GetActiveScene().name}'.");
            return service;
        }

        /// <summary>True when the service can currently be resolved.</summary>
        public static bool Has<T>() where T : Il2CppObjectBase => TryGet<T>() != null;

        /// <summary>
        /// Runs Zenject injection on a GameObject you created, so the game's components on it
        /// receive their services (their [Inject]/Construct methods get called).
        /// </summary>
        public static bool Inject(GameObject gameObject)
        {
            var container = Container;
            if (container == null || gameObject == null)
                return false;
            try
            {
                container.InjectGameObject(gameObject);
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Injecting '{gameObject.name}' failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Finds the first loaded object of a Unity type (e.g. a MonoBehaviour that isn't a bound service).</summary>
        public static T FindObject<T>(bool includeInactive = false) where T : Object
        {
            var all = FindObjects<T>(includeInactive);
            return all.Count > 0 ? all[0] : null;
        }

        /// <summary>Finds all loaded objects of a Unity type.</summary>
        public static List<T> FindObjects<T>(bool includeInactive = false) where T : Object
        {
            var result = new List<T>();
            try
            {
                var found = Object.FindObjectsByType(Il2CppType.Of<T>(),
                    includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                foreach (var obj in found)
                {
                    var cast = obj?.TryCast<T>();
                    if (cast != null)
                        result.Add(cast);
                }
            }
            catch (Exception e)
            {
                FruktLog.Debug($"FindObjects<{typeof(T).Name}> failed: {e.Message}");
            }
            return result;
        }

        /// <summary>Forgets cached services. Called automatically on scene changes.</summary>
        public static void ClearCache()
        {
            Cache.Clear();
            _sceneContext = null;
            _nextSceneContextSearch = 0f;
        }

        private static T Resolve<T>(DiContainer container, Il2CppSystem.Type type) where T : Il2CppObjectBase
        {
            if (container == null)
                return null;
            try
            {
                var resolved = container.TryResolve(type);
                return resolved?.TryCast<T>();
            }
            catch (Exception e)
            {
                FruktLog.Debug($"Resolving {typeof(T).Name} failed: {e.Message}");
                return null;
            }
        }

        private static bool IsUsable(Il2CppObjectBase obj)
        {
            if (obj == null || obj.WasCollected)
                return false;
            var unityObject = obj.TryCast<Object>();
            return unityObject == null || unityObject.Exists();
        }

        private static SceneContext FindSceneContext()
        {
            if (_sceneContext.Exists() && _sceneContext.HasResolved)
                return _sceneContext;

            _sceneContext = null;
            if (Time.unscaledTime < _nextSceneContextSearch)
                return null;
            _nextSceneContextSearch = Time.unscaledTime + 0.25f;

            var active = SceneManager.GetActiveScene();
            SceneContext fallback = null;
            foreach (var context in FindObjects<SceneContext>())
            {
                if (!context.Exists() || !context.HasResolved)
                    continue;
                if (context.gameObject.scene.handle == active.handle)
                {
                    _sceneContext = context;
                    return context;
                }
                fallback = context;
            }
            _sceneContext = fallback;
            return fallback;
        }
    }
}
