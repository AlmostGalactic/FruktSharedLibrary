using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Objects;
using FruktSharedLibrary.Spawning;
using MelonLoader.Utils;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // Builds: two mod props and a game prop joined four ways, captured, saved, loaded and spawned again elsewhere.
    internal static partial class SelfTest
    {
        private static IEnumerator TestBuilds()
        {
            var made = new List<GameObject>();
            var forward = LocalPlayer.Forward;
            forward.y = 0f;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var at = LocalPlayer.Position + forward * 5f + Vector3.up * 1.5f;
            var facing = Quaternion.LookRotation(-forward);

            GameObject a = null, b = null, game = null;
            Section("Builds: setup", () =>
            {
                a = _testProp.Place(at, facing);
                b = _testProp.Place(at + right * 0.7f, facing);
                var props = Spawner.GetPropNames();
                game = props.Count > 0 ? Spawner.SpawnProp(props[0], at + right * 1.6f, facing)?.gameObject : null;
                made.AddRange(new[] { a, b, game }.Where(o => o != null));
                foreach (var o in made)
                    Joints.Body(o).isKinematic = true;
                Check("Builds: the parts are there", a != null && b != null && game != null, game?.name);
            });
            if (a == null || b == null || game == null)
                yield break;
            yield return null;

            JointHandle weld = null, hinge = null, rope = null, spring = null;
            int partsSaved = 0, jointsSaved = 0, partsSpawned = 0, jointsSpawned = 0;
            void OnPartSaving(GameObject o, BuildPart p)
            {
                partsSaved++;
                p.Data["test"] = p.Kind + " part";
            }
            void OnJointSaving(JointHandle j, BuildJoint bj)
            {
                jointsSaved++;
                bj.Data["test"] = bj.Kind;
            }
            void OnPartSpawned(GameObject o, BuildPart p)
            {
                if (p.Data.TryGetValue("test", out var v) && v == p.Kind + " part")
                    partsSpawned++;
            }
            void OnJointSpawned(JointHandle j, BuildJoint bj)
            {
                if (bj.Data.TryGetValue("test", out var v) && v == bj.Kind && j.Kind == bj.Kind)
                    jointsSpawned++;
            }
            Builds.PartSaving += OnPartSaving;
            Builds.JointSaving += OnJointSaving;
            Builds.PartSpawned += OnPartSpawned;
            Builds.JointSpawned += OnJointSpawned;

            Build captured = null, loaded = null;
            string path = Builds.PathFor(Builds.Folder("FruktSharedLibrary builds test"), "Self-test build");
            try
            {
                Section("Builds: capture", () =>
                {
                    var (ba, bb, bg) = (Joints.Body(a), Joints.Body(b), Joints.Body(game));
                    weld = Joints.Weld(ba, bb);
                    hinge = Joints.Hinge(bb, bg, b.transform.position + right * 0.4f, right).SetMotor(120f, 40f);
                    rope = Joints.Rope(ba, null, a.transform.position, a.transform.position + Vector3.up * 2f, 2.5f);
                    spring = Joints.Spring(ba, bg, 150f, 4f);
                    captured = Builds.Capture(a, "Self-test build");
                    Check("Builds.Capture follows the joints to every part", captured.Parts.Count == 3,
                        string.Join(", ", captured.Parts.Select(p => p.ToString())));
                    Check("It knows mod props and the game's props", captured.Parts.Count(p => p.Kind == "prop" && p.Id == _testProp.Name) == 2
                                                                     && captured.Parts.Count(p => p.Kind == "game") == 1);
                    Check("It keeps every joint, including the one to the world", captured.Joints.Count == 4 && captured.Joints.Any(j => j.PartB < 0),
                        string.Join(", ", captured.Joints.Select(j => j.ToString())));
                    Check("Nothing was skipped", captured.Skipped.Count == 0, string.Join("; ", captured.Skipped));
                    Check("PartSaving and JointSaving ran for each", partsSaved == 3 && jointsSaved == 4, $"{partsSaved} parts, {jointsSaved} joints");
                    Check("It measures the build", captured.Size.x > 1.5f, captured.Size.ToString());
                    Check("Build.Save writes a file", captured.Save(path) && File.Exists(path), path);
                    string json = File.Exists(path) ? File.ReadAllText(path) : "";
                    Check("The file is readable JSON, unbreakable joints and all", json.Contains("\"Format\": 1") && json.Contains("Infinity"));
                    loaded = Build.Load(path);
                    Check("Build.Load reads it back", loaded != null && loaded.Name == captured.Name && loaded.Parts.Count == 3 && loaded.Joints.Count == 4,
                        loaded?.ToString());
                    Check("Builds.Files lists it", Builds.Files(Path.GetDirectoryName(path)).Contains(path));
                    if (loaded != null)
                    {
                        bool same = true;
                        for (int i = 0; i < 3; i++)
                            same &= (loaded.Parts[i].Position - captured.Parts[i].Position).magnitude < 0.001f && loaded.Parts[i].Data["test"] == captured.Parts[i].Data["test"];
                        Check("Positions and mod data survive the file", same);
                    }
                });
                if (loaded == null)
                    yield break;
                yield return null;

                BuildInstance copy = null;
                var copyAt = at + right * 4f - Vector3.up * 1.5f;
                var copyTurn = Quaternion.AngleAxis(90f, Vector3.up);
                Section("Builds: spawn", () =>
                {
                    copy = Builds.Spawn(loaded, copyAt, copyTurn * Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
                    made.AddRange(copy.Parts.Where(p => p != null));
                    foreach (var o in copy.Parts.Where(p => p != null))
                        Joints.Body(o).isKinematic = true;
                    Check("Builds.Spawn makes every part", copy.Parts.Count == 3 && copy.Parts.All(p => p != null) && copy.Problems.Count == 0,
                        string.Join("; ", copy.Problems));
                    Check("and every joint", copy.Joints.Count == 4 && copy.Joints.All(j => j.IsActive),
                        string.Join(", ", copy.Joints.Select(j => j.Kind)));
                    Check("PartSpawned and JointSpawned get the mod data back", partsSpawned == 3 && jointsSpawned == 4, $"{partsSpawned} parts, {jointsSpawned} joints");
                    var originals = new[] { a, b, game };
                    bool apart = true;
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = i + 1; j < 3; j++)
                            apart &= Mathf.Abs(Vector3.Distance(originals[i].transform.position, originals[j].transform.position)
                                               - Vector3.Distance(copy.Parts[i].transform.position, copy.Parts[j].transform.position)) < 0.01f;
                    }
                    Check("The copy is laid out like the original", apart);
                    var copyHinge = copy.Joints.FirstOrDefault(j => j.Kind == "hinge")?.Joint.TryCast<ConfigurableJoint>();
                    var original = hinge.Joint.TryCast<ConfigurableJoint>();
                    Check("The hinge's motor comes along", copyHinge != null && copyHinge.targetAngularVelocity == original.targetAngularVelocity
                                                            && Mathf.Approximately(copyHinge.angularXDrive.maximumForce, original.angularXDrive.maximumForce));
                    var copyRope = copy.Joints.FirstOrDefault(j => j.Kind == "rope");
                    Check("The rope keeps its length", copyRope != null && Mathf.Approximately(copyRope.RopeLength, rope.RopeLength), copyRope?.RopeLength.ToString("0.00"));
                    // The rope's world end moved with the copy: it's tied above the copy of `a`, not the original.
                    var worldEnd = copyRope?.Joint.connectedAnchor ?? Vector3.zero;
                    Check("The rope's world end moves with the copy", Vector3.Distance(worldEnd, copy.Parts[0].transform.position + Vector3.up * 2f) < 0.05f,
                        $"{worldEnd} vs {copy.Parts[0].transform.position + Vector3.up * 2f}");
                    foreach (var o in made)
                        Joints.Body(o).isKinematic = false;
                });
                float weldGap = Vector3.Distance(copy.Parts[0].transform.position, copy.Parts[1].transform.position);
                yield return Wait(2f);
                Section("Builds: the copy works", () =>
                {
                    Check("The copy's weld holds", Mathf.Abs(Vector3.Distance(copy.Parts[0].transform.position, copy.Parts[1].transform.position) - weldGap) < 0.05f);
                    Check("The copy's motor turns", Mathf.Abs(copy.Joints.First(j => j.Kind == "hinge").Angle) > 5f,
                        copy.Joints.First(j => j.Kind == "hinge").Angle.ToString("0"));
                    Shot("build-copy");
                });
                yield return Wait(0.5f);
            }
            finally
            {
                Builds.PartSaving -= OnPartSaving;
                Builds.JointSaving -= OnJointSaving;
                Builds.PartSpawned -= OnPartSpawned;
                Builds.JointSpawned -= OnJointSpawned;
                foreach (var o in made)
                {
                    if (o.Exists())
                        Object.Destroy(o);
                }
                if (File.Exists(path))
                    File.Delete(path);
                var folder = Path.Combine(MelonEnvironment.UserDataDirectory, "FruktSharedLibrary builds test");
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
    }
}
