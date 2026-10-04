using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FruktSharedLibrary.Core;
using UnityEngine;

namespace FruktSharedLibrary.Objects
{
    /// <summary>
    /// A saved build: some objects and the joints between them, like a car made of a body, four wheels and their
    /// axles. Make one with <see cref="Builds.Capture(GameObject, string)"/>, keep it in a file with
    /// <see cref="Save"/> and <see cref="Load"/>, and put copies in the world with
    /// <see cref="Builds.Spawn"/>. Positions are relative to the bottom of the build, so a copy can go anywhere.
    /// </summary>
    public sealed class Build
    {
        /// <summary>What the build is called.</summary>
        public string Name { get; set; } = "Build";

        /// <summary>The library version that saved it.</summary>
        public string SavedWith { get; internal set; } = FruktSharedLibraryMod.Version;

        /// <summary>When it was captured.</summary>
        public DateTime SavedAt { get; internal set; } = DateTime.Now;

        /// <summary>The objects in it.</summary>
        public IReadOnlyList<BuildPart> Parts => PartList;

        /// <summary>The joints between them (or between one of them and the world).</summary>
        public IReadOnlyList<BuildJoint> Joints => JointList;

        /// <summary>Things that were joined to the build but couldn't be saved, and why (people, for one).</summary>
        public IReadOnlyList<string> Skipped => SkippedList;

        /// <summary>How big it is, in metres.</summary>
        public Vector3 Size { get; internal set; }

        internal readonly List<BuildPart> PartList = new();
        internal readonly List<BuildJoint> JointList = new();
        internal readonly List<string> SkippedList = new();

        public override string ToString() => $"{Name} ({Parts.Count} parts, {Joints.Count} joints)";

        // ------------------------------------------------------------ files

        /// <summary>The build as JSON text.</summary>
        public string ToJson() => JsonSerializer.Serialize(BuildFile.From(this), BuildFile.Options);

        /// <summary>A build from JSON text made by <see cref="ToJson"/>. Throws if the text isn't a build.</summary>
        public static Build FromJson(string json)
        {
            var file = JsonSerializer.Deserialize<BuildFile>(json, BuildFile.Options) ?? throw new FormatException("That isn't a build.");
            return file.ToBuild();
        }

        /// <summary>Writes the build to a file (making its folder if needed). Returns false and logs why if it can't.</summary>
        public bool Save(string path)
        {
            try
            {
                var folder = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);
                File.WriteAllText(path, ToJson());
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Saving build '{Name}' to '{path}' failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Reads a build from a file. Returns null and logs why if it can't.</summary>
        public static Build Load(string path)
        {
            try
            {
                return FromJson(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Loading the build '{path}' failed: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>One object in a <see cref="Build"/>.</summary>
    public sealed class BuildPart
    {
        /// <summary>
        /// What sort of object it is, which decides how it's made again: "prop" for a mod's
        /// <see cref="Gameplay.ModProp"/>, "game" for one of the game's own objects, or a kind a mod added with
        /// <see cref="Builds.AddKind"/>.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>Which one: the prop's name, the game's prefab ID, or whatever the mod's kind uses.</summary>
        public string Id { get; set; }

        /// <summary>Where it is, relative to the bottom of the build.</summary>
        public Vector3 Position { get; set; }

        /// <summary>How it's turned, relative to the build.</summary>
        public Quaternion Rotation { get; set; } = Quaternion.identity;

        /// <summary>Its scale.</summary>
        public Vector3 Scale { get; set; } = Vector3.one;

        /// <summary>
        /// Anything else your mod wants to keep with it, as text. Fill it in <see cref="Builds.PartSaving"/> and read
        /// it back in <see cref="Builds.PartSpawned"/>.
        /// </summary>
        public Dictionary<string, string> Data { get; set; } = new();

        /// <summary>Where each of its rigidbodies is, as a path from the object down ("" for the object itself).</summary>
        internal List<string> Bodies { get; set; } = new();

        public override string ToString() => $"{Kind}:{Id}";
    }

    /// <summary>One joint in a <see cref="Build"/>.</summary>
    public sealed class BuildJoint
    {
        /// <summary>"weld", "hinge", "ball socket", "spring", "rope" or "slider".</summary>
        public string Kind { get; set; }

        /// <summary>The part the joint is on (an index into <see cref="Build.Parts"/>).</summary>
        public int PartA { get; set; }

        /// <summary>The part on its other end, or -1 when it holds the first part to a point in the world.</summary>
        public int PartB { get; set; } = -1;

        /// <summary>Anything else your mod wants to keep with it. See <see cref="Builds.JointSaving"/>.</summary>
        public Dictionary<string, string> Data { get; set; } = new();

        internal int BodyA { get; set; }
        internal int BodyB { get; set; }
        internal JointSettings Settings { get; set; }

        public override string ToString() => PartB < 0 ? $"{Kind} {PartA}-world" : $"{Kind} {PartA}-{PartB}";
    }

    /// <summary>A build put in the world by <see cref="Builds.Spawn"/>.</summary>
    public sealed class BuildInstance
    {
        internal BuildInstance(Build build, List<GameObject> parts, List<JointHandle> joints, List<string> problems)
        {
            Build = build;
            Parts = parts;
            Joints = joints;
            Problems = problems;
        }

        /// <summary>The build it's a copy of.</summary>
        public Build Build { get; }

        /// <summary>The objects, in the same order as <see cref="Build.Parts"/>. Null for any that couldn't be made.</summary>
        public IReadOnlyList<GameObject> Parts { get; }

        /// <summary>The joints that were made.</summary>
        public IReadOnlyList<JointHandle> Joints { get; }

        /// <summary>What couldn't be made, and why. Empty when everything worked.</summary>
        public IReadOnlyList<string> Problems { get; }
    }

    // The file format: plain numbers instead of Unity types, so it's readable and stable across versions.
    internal sealed class BuildFile
    {
        public int Format { get; set; } = 1;
        public string Name { get; set; }
        public string SavedWith { get; set; }
        public DateTime SavedAt { get; set; }
        public float[] Size { get; set; }
        public List<PartFile> Parts { get; set; } = new();
        public List<JointFile> Joints { get; set; } = new();
        public List<string> Skipped { get; set; } = new();

        internal static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            IncludeFields = true,
            // Unbreakable joints have an infinite break force.
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        internal sealed class PartFile
        {
            public string Kind { get; set; }
            public string Id { get; set; }
            public float[] Position { get; set; }
            public float[] Rotation { get; set; }
            public float[] Scale { get; set; }
            public List<string> Bodies { get; set; }
            public Dictionary<string, string> Data { get; set; }
        }

        internal sealed class JointFile
        {
            public string Kind { get; set; }
            public int PartA { get; set; }
            public int BodyA { get; set; }
            public int PartB { get; set; }
            public int BodyB { get; set; }
            public JointSettings Settings { get; set; }
            public Dictionary<string, string> Data { get; set; }
        }

        internal static BuildFile From(Build build) => new()
        {
            Name = build.Name,
            SavedWith = build.SavedWith,
            SavedAt = build.SavedAt,
            Size = V(build.Size),
            Parts = build.PartList.Select(p => new PartFile
            {
                Kind = p.Kind,
                Id = p.Id,
                Position = V(p.Position),
                Rotation = new[] { p.Rotation.x, p.Rotation.y, p.Rotation.z, p.Rotation.w },
                Scale = V(p.Scale),
                Bodies = p.Bodies,
                Data = p.Data.Count > 0 ? p.Data : null,
            }).ToList(),
            Joints = build.JointList.Select(j => new JointFile
            {
                Kind = j.Kind,
                PartA = j.PartA,
                BodyA = j.BodyA,
                PartB = j.PartB,
                BodyB = j.BodyB,
                Settings = j.Settings,
                Data = j.Data.Count > 0 ? j.Data : null,
            }).ToList(),
            Skipped = build.SkippedList.Count > 0 ? build.SkippedList : null,
        };

        internal Build ToBuild()
        {
            if (Format > 1)
                FruktLog.Warning($"Build '{Name}' was saved by a newer version of the library ({SavedWith}); some of it may not load.");
            var build = new Build { Name = Name ?? "Build", SavedWith = SavedWith, SavedAt = SavedAt, Size = V(Size) };
            foreach (var p in Parts ?? new List<PartFile>())
            {
                build.PartList.Add(new BuildPart
                {
                    Kind = p.Kind,
                    Id = p.Id,
                    Position = V(p.Position),
                    Rotation = p.Rotation is { Length: 4 } r ? new Quaternion(r[0], r[1], r[2], r[3]) : Quaternion.identity,
                    Scale = p.Scale is { Length: 3 } ? V(p.Scale) : Vector3.one,
                    Bodies = p.Bodies ?? new List<string> { "" },
                    Data = p.Data ?? new Dictionary<string, string>(),
                });
            }
            foreach (var j in Joints ?? new List<JointFile>())
            {
                if (j.Settings == null || j.PartA < 0 || j.PartA >= build.PartList.Count || j.PartB >= build.PartList.Count)
                    continue;
                build.JointList.Add(new BuildJoint
                {
                    Kind = j.Kind,
                    PartA = j.PartA,
                    BodyA = j.BodyA,
                    PartB = j.PartB,
                    BodyB = j.BodyB,
                    Settings = j.Settings,
                    Data = j.Data ?? new Dictionary<string, string>(),
                });
            }
            build.SkippedList.AddRange(Skipped ?? new List<string>());
            return build;
        }

        private static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
        private static Vector3 V(float[] a) => a is { Length: 3 } ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
    }
}
