using System;
using System.Collections.Generic;
using System.Diagnostics;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using Il2CppEffectors;
using Il2CppEffectors.ReceiveMethods;
using Il2CppEffectors.ReceiveMethods.Index;
using Il2CppEffectors.Types;
using Il2CppLVA.Creatures;
using Il2CppLVA.Limbs;
using Il2CppLVA.Limbs.EffectorsPerception;
using Il2CppLVA.Limbs.References;
using Il2CppLVA.Organs;
using Il2CppLVA.Organs.EffectorsPerception.Collectors;
using Il2CppVoxelMeshGeneration;
using Il2CppVoxelMeshGeneration.Painting;
using Il2CppVoxelMeshGeneration.Separation;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FruktSharedLibrary.Entities
{
    /// <summary>
    /// Grows destroyed and damaged flesh back, or eats it away. <see cref="Regrow(AbstractCreature, float)"/> brings
    /// tissue back from what's left of a limb outwards, as if it healed, and the organs inside work again; limbs that
    /// came off stay off. <see cref="Dissolve(AbstractCreature, float, float, Func{AbstractOrgan, bool})"/> eats the
    /// flesh of some or all of the organs away, from the outside in.
    /// </summary>
    public static class Tissue
    {
        /// <summary>Flesh growing back or being eaten away in a creature or a limb.</summary>
        public abstract class Change
        {
            internal readonly List<LimbJob> Limbs = new();

            internal Change(AbstractCreature creature, float seconds)
            {
                Creature = creature;
                Seconds = Mathf.Max(0.1f, seconds);
            }

            internal abstract bool Grows { get; }
            internal Func<AbstractOrgan, bool> Organs;
            internal float Amount = 1f;
            internal int Count;

            /// <summary>Whose flesh it is.</summary>
            public AbstractCreature Creature { get; }

            /// <summary>Roughly how long it takes, once the flesh to change has been found.</summary>
            public float Seconds { get; }

            /// <summary>True once it has finished (or it was stopped).</summary>
            public bool Done { get; internal set; }

            /// <summary>Stops it where it is.</summary>
            public void Stop() => Done = true;
        }

        /// <summary>A regrowth going on in a creature or a limb.</summary>
        public sealed class Regrowth : Change
        {
            internal Regrowth(AbstractCreature creature, float seconds) : base(creature, seconds) { }

            internal override bool Grows => true;

            /// <summary>How many voxels have grown back so far.</summary>
            public int Restored => Count;
        }

        /// <summary>Flesh being eaten away in a creature or a limb.</summary>
        public sealed class Dissolving : Change
        {
            internal Dissolving(AbstractCreature creature, float seconds) : base(creature, seconds) { }

            internal override bool Grows => false;

            /// <summary>How many voxels have been eaten away so far.</summary>
            public int Destroyed => Count;
        }

        internal sealed class LimbJob
        {
            internal AbstractLimb Limb;
            internal VoxelMesh Mesh;
            internal LimbEffectorReceiver Receiver;
            internal VoxelMeshSeparationModule Separation;
            internal List<Destructibility> Collectors;
            internal int3 Size;
            internal int Cursor, Total;
            internal bool Ready, Scanned, Finished;
            internal readonly List<int3> Pending = new();
            internal float Credit;
            internal int Found;
            internal int SentFrame = -100;
        }

        /// <summary>The time all regrowth together may take each frame, in milliseconds.</summary>
        public const double BudgetMs = 2.0;

        private const float Whole = 99.9f;
        private const int MaxBatch = 800;
        private const float KeepSeconds = 30f;
        // The game picks a change up a frame or so after it's sent, so a mesh that looks idle may still have the last
        // one waiting. Changes sent too close together get merged into one update, which the game gets wrong and which
        // can crash it later, so each limb waits a few frames between changes.
        private const int FramesBetween = 5;
        private static readonly Queue<(IndexEffectorSignalsHandler<Destruction> Handler, IndexEffectorSignalsList List, float FreeAt)> Sent = new();
        private static readonly List<Change> Jobs = new();
        private static readonly Stopwatch Clock = new();
        private static readonly int3[] Around =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
        };
        private static int _turn;

        /// <summary>How many regrowths and dissolvings are going on.</summary>
        public static int Active => Jobs.Count;

        /// <summary>Grows back the destroyed flesh on every limb the creature still has, over about
        /// <paramref name="seconds"/>.</summary>
        public static Regrowth Regrow(AbstractCreature creature, float seconds = 3f)
        {
            if (!creature.IsValid())
                return null;
            var job = new Regrowth(creature, seconds);
            foreach (var limb in creature.GetLimbs())
                job.Limbs.Add(new LimbJob { Limb = limb });
            Jobs.Add(job);
            return job;
        }

        /// <summary>Grows back the destroyed flesh on one limb, over about <paramref name="seconds"/>.</summary>
        public static Regrowth Regrow(AbstractLimb limb, float seconds = 3f)
        {
            if (!limb.Exists())
                return null;
            var job = new Regrowth(limb.GetCreature(), seconds);
            job.Limbs.Add(new LimbJob { Limb = limb });
            Jobs.Add(job);
            return job;
        }

        /// <summary>
        /// Eats away the flesh of the organs that <paramref name="organs"/> picks (all of them without it), on every
        /// limb the creature has, from the outside in, over about <paramref name="seconds"/>. <paramref name="amount"/>
        /// is how much of it goes, from 0 to 1. <c>Tissue.Dissolve(person, 5f, organs: o =&gt; o.TryCast&lt;Bone&gt;() != null)</c>
        /// takes their bones.
        /// </summary>
        public static Dissolving Dissolve(AbstractCreature creature, float seconds = 3f, float amount = 1f, Func<AbstractOrgan, bool> organs = null)
        {
            if (!creature.IsValid())
                return null;
            var job = new Dissolving(creature, seconds) { Organs = organs, Amount = Mathf.Clamp01(amount) };
            foreach (var limb in creature.GetLimbs())
                job.Limbs.Add(new LimbJob { Limb = limb });
            Jobs.Add(job);
            return job;
        }

        /// <summary>The same on one limb.</summary>
        public static Dissolving Dissolve(AbstractLimb limb, float seconds = 3f, float amount = 1f, Func<AbstractOrgan, bool> organs = null)
        {
            if (!limb.Exists())
                return null;
            var job = new Dissolving(limb.GetCreature(), seconds) { Organs = organs, Amount = Mathf.Clamp01(amount) };
            job.Limbs.Add(new LimbJob { Limb = limb });
            Jobs.Add(job);
            return job;
        }

        /// <summary>Stops every regrowth and dissolving.</summary>
        public static void StopAll() => Jobs.Clear();

        internal static void Update()
        {
            if (Sent.Count > 0)
                FreeOld();
            if (Jobs.Count == 0)
                return;
            Clock.Restart();
            float dt = Time.deltaTime;
            for (int n = 0; n < Jobs.Count && Clock.Elapsed.TotalMilliseconds < BudgetMs; n++)
            {
                var job = Jobs[(_turn + n) % Jobs.Count];
                if (job.Done)
                    continue;
                bool working = false;
                foreach (var limb in job.Limbs)
                {
                    if (limb.Finished)
                        continue;
                    working = true;
                    if (Clock.Elapsed.TotalMilliseconds >= BudgetMs)
                        break;
                    try
                    {
                        Step(job, limb, dt);
                    }
                    catch (Exception e)
                    {
                        FruktLog.Debug("Changing a limb's flesh failed: " + e.Message);
                        limb.Finished = true;
                    }
                }
                if (!working)
                    job.Done = true;
            }
            _turn++;
            Jobs.RemoveAll(j => j.Done);
        }

        private static void Step(Change job, LimbJob l, float dt)
        {
            if (!l.Limb.Exists())
            {
                l.Finished = true;
                return;
            }
            if (!l.Ready && !Prepare(job, l))
            {
                l.Finished = true;
                return;
            }
            if (!l.Scanned)
            {
                Scan(job, l);
                return;
            }
            if (l.Pending.Count == 0)
            {
                l.Finished = true;
                if (job.Grows)
                    Healed(l);
                return;
            }
            l.Credit += Mathf.Max(1f, l.Found * dt / job.Seconds);
            if (l.Credit < 1f || Time.frameCount - l.SentFrame < FramesBetween || !SafeToChange(l))
                return;
            // A piece coming off can make the game cut the limb's voxel grid down to a new size. Voxels found in the
            // old grid would then point outside it, so look again.
            var size = l.Mesh.Data.Size;
            if (size.x != l.Size.x || size.y != l.Size.y || size.z != l.Size.z)
            {
                l.Size = size;
                l.Total = size.x * size.y * size.z;
                l.Cursor = 0;
                l.Scanned = false;
                l.Pending.Clear();
                return;
            }
            var batch = new List<int3>();
            var chosen = new HashSet<int3>();
            int limit = Mathf.Min(MaxBatch, (int)l.Credit);
            if (!job.Grows)
            {
                // Eaten from the outside in: the list is already in that order.
                batch.AddRange(l.Pending.GetRange(0, Mathf.Min(limit, l.Pending.Count)));
                l.Pending.RemoveRange(0, batch.Count);
                l.Credit -= batch.Count;
                Send(l, batch, -100000f);
                job.Count += batch.Count;
                return;
            }
            // Only voxels next to flesh that's there (or growing back this frame), so it grows out from the stump
            // and never makes loose bits.
            for (int i = 0; i < l.Pending.Count && batch.Count < limit; i++)
            {
                var index = l.Pending[i];
                if (!Touches(l, index, chosen))
                    continue;
                batch.Add(index);
                chosen.Add(index);
            }
            if (batch.Count == 0)
            {
                // What's left doesn't join on to anything: it's a piece that came off.
                l.Pending.Clear();
                return;
            }
            l.Pending.RemoveAll(chosen.Contains);
            l.Credit -= batch.Count;
            Send(l, batch, 100000f);
            job.Count += batch.Count;
        }

        private static bool Prepare(Change job, LimbJob l)
        {
            l.Ready = true;
            l.Mesh = l.Limb.GetVoxelMesh();
            var references = l.Limb.References;
            if (!l.Mesh.Exists() || references == null)
                return false;
            var component = references.Cast<Component>();
            l.Receiver = component.GetComponentInChildren<LimbEffectorReceiver>(true);
            l.Separation = FindSeparation(component, l.Mesh);
            if (l.Receiver == null)
                return false;
            l.Collectors = new List<Destructibility>();
            foreach (var organ in l.Limb.GetAllOrgans())
            {
                if (organ == null || (job.Organs != null && !job.Organs(organ)))
                    continue;
                // Organs that are whole have nothing to grow back.
                var progress = organ.GetParameterValue<DestructibilityProgress>();
                if (job.Grows && progress.HasValue && progress.Value >= 99.99f)
                    continue;
                var handler = organ?.m_referencesPrivate?.m_effectorCollectorsHandler;
                if (handler == null)
                    continue;
                foreach (var collector in handler.m_disposableEffectorCollectors)
                {
                    var destructibility = collector?.TryCast<Destructibility>();
                    if (destructibility != null)
                    {
                        l.Collectors.Add(destructibility);
                        break;
                    }
                }
            }
            if (l.Collectors.Count == 0)
                return false;
            l.Size = l.Mesh.Data.Size;
            l.Total = l.Size.x * l.Size.y * l.Size.z;
            return true;
        }

        private static void Scan(Change job, LimbJob l)
        {
            float progress = 0f;
            int stop = Mathf.Min(l.Total, l.Cursor + 2048);
            int yz = l.Size.y * l.Size.z;
            for (; l.Cursor < stop; l.Cursor++)
            {
                var index = new int3(l.Cursor / yz, l.Cursor / l.Size.z % l.Size.y, l.Cursor % l.Size.z);
                foreach (var collector in l.Collectors)
                {
                    if (collector.Cast<EffectorCollector<Destruction>>().TryGetVoxelProgress(index, out progress))
                    {
                        if (job.Grows ? progress < Whole : progress > 0.1f)
                            l.Pending.Add(index);
                        break;
                    }
                }
            }
            if (l.Cursor < l.Total)
                return;
            l.Scanned = true;
            // Growing goes from the middle of the limb outwards; eating goes from the outside in.
            var middle = new float3(l.Size.x, l.Size.y, l.Size.z) * 0.5f;
            int sign = job.Grows ? 1 : -1;
            l.Pending.Sort((a, b) => sign * math.distancesq(new float3(a.x, a.y, a.z), middle).CompareTo(math.distancesq(new float3(b.x, b.y, b.z), middle)));
            if (!job.Grows && job.Amount < 1f)
            {
                int keep = Mathf.RoundToInt(l.Pending.Count * job.Amount);
                l.Pending.RemoveRange(keep, l.Pending.Count - keep);
            }
            l.Found = l.Pending.Count;
        }

        private static bool Touches(LimbJob l, int3 index, HashSet<int3> chosen)
        {
            var data = l.Mesh.Data;
            foreach (var step in Around)
            {
                var next = index + step;
                if (chosen.Contains(next))
                    return true;
                if (next.x >= 0 && next.y >= 0 && next.z >= 0 && next.x < l.Size.x && next.y < l.Size.y && next.z < l.Size.z && data[next].enabled)
                    return true;
            }
            return false;
        }

        // The part that works out whether a piece has come off sits beside the mesh rather than under the limb.
        private static VoxelMeshSeparationModule FindSeparation(Component references, VoxelMesh mesh)
        {
            return mesh.GetComponent<VoxelMeshSeparationModule>()
                   ?? mesh.GetComponentInChildren<VoxelMeshSeparationModule>(true)
                   ?? mesh.GetComponentInParent<VoxelMeshSeparationModule>(true)
                   ?? references.GetComponentInChildren<VoxelMeshSeparationModule>(true)
                   ?? references.GetComponentInParent<VoxelMeshSeparationModule>(true);
        }

        // Two changes to the same voxels merged into one mesh update can cancel out, so only change a mesh that
        // has finished its last update and isn't working out whether a piece has come off.
        private static bool SafeToChange(LimbJob l)
        {
            var mesh = l.Mesh;
            if (!mesh.Exists() || !mesh.MeshCreated)
                return false;
            var current = mesh.m_currentUpdateRequest;
            if (current != null && !current.IsCompleted)
                return false;
            var queue = mesh.m_updateRequestsQueue;
            if (queue != null && queue.Count > 0)
                return false;
            var separation = l.Separation;
            return separation == null || (separation.m_separationsCalculationProcess == null && !separation.m_redetectPending);
        }

        private static void Send(LimbJob l, List<int3> voxels, float influence)
        {
            l.SentFrame = Time.frameCount;
            // The game keeps reading the list after Receive returns (it works out later whether a piece has come
            // off), so it lives in lasting memory and is only freed once the game is long done with it. Memory that
            // only lasts the frame gets reused under it and crashes the game.
            var signals = new IndexEffectorSignalsList(voxels.Count, false, Allocator.Persistent);
            foreach (var voxel in voxels)
                signals.Add(new IndexEffectorSignal(voxel, influence, InfluenceProcessType.Sum));
            var handler = new IndexEffectorSignalsHandler<Destruction>(signals, new IndexEffectorDescription());
            Sent.Enqueue((handler, signals, Time.realtimeSinceStartup + KeepSeconds));
            l.Receiver.Receive<Destruction>(handler);
            if (influence > 0f)
                Forget(l, voxels);
        }

        // The game keeps track of the holes in a limb, to tell when a piece has come off. It never fills holes itself,
        // so it has to be told that these voxels aren't holes any more.
        private static void Forget(LimbJob l, List<int3> voxels)
        {
            var detection = l.Separation?.m_separationDetection;
            if (detection == null)
                return;
            try
            {
                var map = detection.m_disabledVoxelsIndexesToGroupsMap;
                foreach (var voxel in voxels)
                {
                    if (map != null && map.ContainsKey(voxel))
                    {
                        detection.ClearDisabledVoxelIndex(voxel);
                    }
                }
                detection.ClearEmptyDisabledGroups();
            }
            catch (Exception e)
            {
                FruktLog.Debug("Forgetting the holes failed: " + e.Message);
            }
        }

        private static void FreeOld()
        {
            float now = Time.realtimeSinceStartup;
            while (Sent.Count > 0 && Sent.Peek().FreeAt <= now)
            {
                var (handler, list, _) = Sent.Dequeue();
                try
                {
                    if (list.m_signals.IsCreated)
                        handler.Dispose();
                }
                catch
                {
                    // Already gone.
                }
            }
        }

        private static void Healed(LimbJob l)
        {
            l.Limb.StopBleeding();
            // Wipe the blood and wound marks off the healed skin.
            foreach (var paint in l.Limb.References.Cast<Component>().GetComponentsInChildren<VoxelMeshPaintModule>(true))
            {
                if (paint != null && paint.HoldsPaint)
                    paint.ClearPaint();
            }
        }
    }
}
