using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Spawning;
using Il2CppInfrastructure.Project.Registration;
using Il2CppLVA.Limbs;
using MelonLoader.Utils;
using UnityEngine;

namespace FruktSharedLibrary.Objects
{
    /// <summary>
    /// Saving and loading builds: objects joined together with <see cref="Joints"/>, like a car. Capture one,
    /// save it to a file, and spawn copies of it later, in any map.
    /// </summary>
    /// <remarks>
    /// A build can hold mod props (<see cref="Gameplay.ModProp"/>), the game's own spawnable objects (props, guns),
    /// and anything a mod teaches it about with <see cref="AddKind"/>. People are left out: they're whole ragdolls,
    /// not single objects. Mods keep their own extras with a part or joint through <see cref="PartSaving"/> and
    /// <see cref="PartSpawned"/> (and the joint versions).
    /// </remarks>
    /// <example>
    /// <code>
    /// // Save whatever the player is aiming at, and everything joined to it.
    /// if (LocalPlayer.Raycast(out var hit) &amp;&amp; hit.rigidbody != null)
    ///     Builds.Capture(hit.rigidbody.gameObject, "Car").Save(Builds.PathFor(Builds.Folder("MyMod"), "Car"));
    ///
    /// // Put it back in front of the player.
    /// var car = Build.Load(Builds.PathFor(Builds.Folder("MyMod"), "Car"));
    /// Builds.Spawn(car, LocalPlayer.GetPointInFront(4f), LocalPlayer.RotationFacingPlayer(LocalPlayer.GetPointInFront(4f)));
    /// </code>
    /// </example>
    public static class Builds
    {
        private sealed class Kind
        {
            public string Name;
            public Func<GameObject, string> IdOf;
            public Func<string, Vector3, Quaternion, GameObject> Spawn;
        }

        private static readonly List<Kind> Kinds = new();

        /// <summary>A part is being captured: put anything your mod wants to keep in <see cref="BuildPart.Data"/>.</summary>
        public static event Action<GameObject, BuildPart> PartSaving;

        /// <summary>A part was spawned from a build: read your <see cref="BuildPart.Data"/> back.</summary>
        public static event Action<GameObject, BuildPart> PartSpawned;

        /// <summary>A joint is being captured: put anything your mod wants to keep in <see cref="BuildJoint.Data"/>.</summary>
        public static event Action<JointHandle, BuildJoint> JointSaving;

        /// <summary>A joint was made again from a build.</summary>
        public static event Action<JointHandle, BuildJoint> JointSpawned;

        // ------------------------------------------------------------ capturing

        /// <summary>
        /// Captures <paramref name="part"/> and everything joined to it through <see cref="Joints"/>, and
        /// everything joined to those, and so on: the whole contraption.
        /// </summary>
        public static Build Capture(GameObject part, string name = null)
        {
            var start = Joints.Body(part);
            if (start == null)
                throw new ArgumentException($"'{(part == null ? "null" : part.name)}' isn't a physics object.", nameof(part));
            var bodies = new List<Rigidbody> { start };
            var seen = new HashSet<IntPtr> { start.Pointer };
            var handles = Joints.All;
            for (int i = 0; i < bodies.Count; i++)
            {
                foreach (var handle in handles)
                {
                    foreach (var (from, to) in new[] { (handle.A, handle.B), (handle.B, handle.A) })
                    {
                        if (from != null && to != null && from.Pointer == bodies[i].Pointer && to.Exists() && seen.Add(to.Pointer))
                            bodies.Add(to);
                    }
                }
            }
            return Make(bodies, name);
        }

        /// <summary>Captures exactly these objects, and the joints among them (and to the world). Nothing else is followed.</summary>
        public static Build Capture(IEnumerable<GameObject> parts, string name = null)
        {
            var bodies = new List<Rigidbody>();
            var seen = new HashSet<IntPtr>();
            foreach (var part in parts ?? throw new ArgumentNullException(nameof(parts)))
            {
                var body = Joints.Body(part);
                if (body != null && seen.Add(body.Pointer))
                    bodies.Add(body);
            }
            return Make(bodies, name);
        }

        private static Build Make(List<Rigidbody> bodies, string name)
        {
            var build = new Build { Name = string.IsNullOrWhiteSpace(name) ? "Build" : name.Trim() };
            var roots = new List<GameObject>();
            var parts = new Dictionary<IntPtr, int>(); // root -> part index
            var bodyAt = new Dictionary<IntPtr, (int Part, int Body)>();
            foreach (var body in bodies)
            {
                var (root, kind, id, why) = Identify(body);
                if (root == null)
                {
                    build.SkippedList.Add($"{body.name}: {why}");
                    continue;
                }
                if (!parts.TryGetValue(root.Pointer, out int index))
                {
                    index = roots.Count;
                    parts[root.Pointer] = index;
                    roots.Add(root);
                    build.PartList.Add(new BuildPart { Kind = kind, Id = id, Scale = root.transform.localScale });
                }
                var paths = build.PartList[index].Bodies;
                bodyAt[body.Pointer] = (index, paths.Count);
                paths.Add(PathTo(root.transform, body.transform));
            }
            if (roots.Count == 0)
                return build;

            // The build's own space: the bottom of everything, turned the way the first part faces.
            var bounds = BoundsOf(roots);
            var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var turn = Quaternion.Euler(0f, roots[0].transform.eulerAngles.y, 0f);
            var toBuild = Quaternion.Inverse(turn);
            build.Size = bounds.size;
            for (int i = 0; i < roots.Count; i++)
            {
                var part = build.PartList[i];
                part.Position = toBuild * (roots[i].transform.position - origin);
                part.Rotation = toBuild * roots[i].transform.rotation;
                Raise(PartSaving, roots[i], part, nameof(PartSaving));
            }

            foreach (var handle in Joints.All)
            {
                if (!handle.IsActive || handle.A == null || !bodyAt.TryGetValue(handle.A.Pointer, out var a))
                    continue;
                (int Part, int Body) b = (-1, 0);
                if (handle.B != null && !bodyAt.TryGetValue(handle.B.Pointer, out b))
                    continue;
                var joint = new BuildJoint
                {
                    Kind = handle.Kind,
                    PartA = a.Part,
                    BodyA = a.Body,
                    PartB = b.Part,
                    BodyB = b.Body,
                    Settings = JointSettings.From(handle, world => toBuild * (world - origin)),
                };
                build.JointList.Add(joint);
                Raise(JointSaving, handle, joint, nameof(JointSaving));
            }
            return build;
        }

        // How a body's object can be made again: a mod prop, a mod's own kind, or one of the game's objects.
        private static (GameObject Root, string Kind, string Id, string Why) Identify(Rigidbody body)
        {
            if (body.GetComponentInParent<AbstractLimb>() != null)
                return (null, null, null, "people can't be saved in a build");
            for (var t = body.transform; t != null; t = t.parent)
            {
                var prop = ModProp.CopyOf(t.gameObject);
                if (prop != null)
                    return (t.gameObject, "prop", prop.Name, null);
                foreach (var kind in Kinds)
                {
                    string id = null;
                    try
                    {
                        id = kind.IdOf(t.gameObject);
                    }
                    catch (Exception e)
                    {
                        FruktLog.Error($"Build kind '{kind.Name}' failed to look at '{t.name}'", e);
                    }
                    if (!string.IsNullOrEmpty(id))
                        return (t.gameObject, kind.Name, id, null);
                }
            }
            var registered = body.GetComponentInParent<RegistrableBehaviour>();
            string prefab = null;
            try
            {
                prefab = registered?.PrefabID?.ID;
            }
            catch
            {
                // Not a registered object after all.
            }
            if (!string.IsNullOrEmpty(prefab))
                return (registered.gameObject, "game", prefab, null);
            return (null, null, null, "the library doesn't know how to make it again");
        }

        // ------------------------------------------------------------ spawning

        /// <summary>
        /// Puts a copy of a build in the world, with its bottom at <paramref name="position"/>, turned by
        /// <paramref name="rotation"/>, and joins its parts like the original. Needs a map.
        /// </summary>
        public static BuildInstance Spawn(Build build, Vector3 position, Quaternion rotation)
        {
            if (build == null)
                throw new ArgumentNullException(nameof(build));
            var objects = new List<GameObject>();
            var problems = new List<string>();
            Vector3 World(Vector3 inBuild) => position + rotation * inBuild;

            foreach (var part in build.PartList)
            {
                GameObject made = null;
                try
                {
                    made = Make(part, World(part.Position), rotation * part.Rotation);
                }
                catch (Exception e)
                {
                    FruktLog.Error($"Making build part '{part}' failed", e);
                }
                if (made == null)
                    problems.Add($"{part}: couldn't be made");
                else
                {
                    made.transform.localScale = part.Scale;
                    foreach (var body in made.GetComponentsInChildren<Rigidbody>())
                    {
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                }
                objects.Add(made);
            }
            Physics.SyncTransforms();

            var handles = new List<JointHandle>();
            foreach (var joint in build.JointList)
            {
                var a = BodyOf(objects, build, joint.PartA, joint.BodyA);
                var b = joint.PartB < 0 ? null : BodyOf(objects, build, joint.PartB, joint.BodyB);
                if (a == null || (joint.PartB >= 0 && b == null))
                {
                    problems.Add($"{joint}: one of its parts is missing");
                    continue;
                }
                try
                {
                    var settings = joint.PartB < 0 ? joint.Settings.WithWorldAnchor(World) : joint.Settings;
                    var handle = Joints.Restore(a, b, joint.Kind, settings);
                    handles.Add(handle);
                    Raise(JointSpawned, handle, joint, nameof(JointSpawned));
                }
                catch (Exception e)
                {
                    problems.Add($"{joint}: {e.Message}");
                }
            }
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null)
                    Raise(PartSpawned, objects[i], build.PartList[i], nameof(PartSpawned));
            }
            if (problems.Count > 0)
                FruktLog.Warning($"Spawning build '{build.Name}': {string.Join("; ", problems)}");
            return new BuildInstance(build, objects, handles, problems);
        }

        private static GameObject Make(BuildPart part, Vector3 position, Quaternion rotation)
        {
            switch (part.Kind)
            {
                case "prop":
                    var prop = Inventory.ModTools.OfType<ModProp>().FirstOrDefault(p => p.Name == part.Id);
                    if (prop == null)
                        FruktLog.Warning($"No mod prop called '{part.Id}' is installed.");
                    return prop?.Place(position, rotation);
                case "game":
                    return Spawner.Spawn(part.Id, position, rotation)?.gameObject;
                default:
                    var kind = Kinds.FirstOrDefault(k => k.Name == part.Kind);
                    if (kind == null)
                    {
                        FruktLog.Warning($"Nothing knows how to make a '{part.Kind}' (is the mod that adds it installed?).");
                        return null;
                    }
                    return kind.Spawn(part.Id, position, rotation);
            }
        }

        private static Rigidbody BodyOf(List<GameObject> objects, Build build, int part, int body)
        {
            if (part < 0 || part >= objects.Count || objects[part] == null)
                return null;
            var paths = build.PartList[part].Bodies;
            string path = body >= 0 && body < paths.Count ? paths[body] : "";
            var t = path.Length == 0 ? objects[part].transform : objects[part].transform.Find(path);
            return t == null ? null : t.GetComponent<Rigidbody>();
        }

        // ------------------------------------------------------------ mods' own objects

        /// <summary>
        /// Teaches builds about a kind of object your mod makes that isn't a <see cref="Gameplay.ModProp"/>.
        /// <paramref name="idOf"/> is asked about each object (and its parents) in a build, and gives the ID to
        /// save it under, or null if it isn't one of yours. <paramref name="spawn"/> makes one again from that ID.
        /// </summary>
        public static void AddKind(string kind, Func<GameObject, string> idOf, Func<string, Vector3, Quaternion, GameObject> spawn)
        {
            if (string.IsNullOrWhiteSpace(kind) || kind == "prop" || kind == "game")
                throw new ArgumentException("Give the kind a name of its own (not \"prop\" or \"game\").", nameof(kind));
            Kinds.RemoveAll(k => k.Name == kind);
            Kinds.Add(new Kind { Name = kind, IdOf = idOf ?? throw new ArgumentNullException(nameof(idOf)), Spawn = spawn ?? throw new ArgumentNullException(nameof(spawn)) });
        }

        // ------------------------------------------------------------ files

        /// <summary>A folder for a mod's saved builds: <c>UserData/&lt;mod&gt;/Builds</c>. It's made if it doesn't exist.</summary>
        public static string Folder(string modName)
        {
            if (string.IsNullOrWhiteSpace(modName))
                throw new ArgumentException("Give the mod's name.", nameof(modName));
            var folder = Path.Combine(MelonEnvironment.UserDataDirectory, SafeName(modName), "Builds");
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>The file a build called <paramref name="name"/> is saved as in <paramref name="folder"/>.</summary>
        public static string PathFor(string folder, string name) => Path.Combine(folder, SafeName(name) + ".json");

        /// <summary>The builds saved in a folder, newest first.</summary>
        public static IReadOnlyList<string> Files(string folder)
        {
            if (!Directory.Exists(folder))
                return Array.Empty<string>();
            return Directory.GetFiles(folder, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).ToList();
        }

        // ------------------------------------------------------------ internals

        private static string SafeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string((name ?? "").Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return safe.Length == 0 ? "Build" : safe;
        }

        private static string PathTo(Transform root, Transform t)
        {
            var names = new List<string>();
            for (; t != null && t != root; t = t.parent)
                names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static Bounds BoundsOf(List<GameObject> roots)
        {
            bool any = false;
            var bounds = new Bounds(roots[0].transform.position, Vector3.zero);
            foreach (var root in roots)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled)
                        continue;
                    if (any)
                        bounds.Encapsulate(renderer.bounds);
                    else
                        bounds = renderer.bounds;
                    any = true;
                }
                if (!any)
                    bounds.Encapsulate(root.transform.position);
            }
            return bounds;
        }

        private static void Raise<T>(Action<T, BuildPart> handlers, T thing, BuildPart part, string name) => RaiseAll(handlers, h => h(thing, part), name);

        private static void Raise<T>(Action<T, BuildJoint> handlers, T thing, BuildJoint joint, string name) => RaiseAll(handlers, h => h(thing, joint), name);

        private static void RaiseAll<TDelegate>(TDelegate handlers, Action<TDelegate> call, string name) where TDelegate : Delegate
        {
            if (handlers == null)
                return;
            foreach (TDelegate handler in handlers.GetInvocationList())
            {
                try
                {
                    call(handler);
                }
                catch (Exception e)
                {
                    FruktLog.Error($"A Builds.{name} handler threw", e);
                }
            }
        }
    }
}
