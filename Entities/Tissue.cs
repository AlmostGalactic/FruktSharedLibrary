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
    /// Grows destroyed and damaged flesh back. The tissue comes back from what's left of a limb outwards, as if it
    /// healed, and the organs inside it work again. Limbs that came off stay off.
    /// </summary>
    public static class Tissue
    {
        /// <summary>A regrowth going on in a creature or a limb.</summary>
        public sealed class Regrowth
        {
            internal readonly List<LimbJob> Limbs = new();

            internal Regrowth(AbstractCreature creature, float seconds)
            {
                Creature = creature;
                Seconds = Mathf.Max(0.1f, seconds);
            }

            /// <summary>Who is healing.</summary>
            public AbstractCreature Creature { get; }

            /// <summary>Roughly how long it takes, once the damage has been found.</summary>
            public float Seconds { get; }

            /// <summary>How many voxels have grown back so far.</summary>
            public int Restored { get; internal set; }

            /// <summary>True once everything that can grow back has (or it was stopped).</summary>
            public bool Done { get; internal set; }

            /// <summary>Stops it where it is.</summary>
            public void Stop() => Done = true;
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
        }

        /// <summary>The time all regrowth together may take each frame, in milliseconds.</summary>
        public const double BudgetMs = 2.0;

        private const float Whole = 99.9f;
        private const int MaxBatch = 800;
        private static readonly List<Regrowth> Jobs = new();
        private static readonly Stopwatch Clock = new();
        private static readonly int3[] Around =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
        };
        private static int _turn;

        /// <summary>How many regrowths are going on.</summary>
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

        /// <summary>Stops every regrowth.</summary>
        public static void StopAll() => Jobs.Clear();

        internal static void Update()
        {
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
                        FruktLog.Debug("Regrowing a limb failed: " + e.Message);
                        limb.Finished = true;
                    }
                }
                if (!working)
                    job.Done = true;
            }
            _turn++;
            Jobs.RemoveAll(j => j.Done);
        }

        private static void Step(Regrowth job, LimbJob l, float dt)
        {
            if (!l.Limb.Exists())
            {
                l.Finished = true;
                return;
            }
            if (!l.Ready && !Prepare(l))
            {
                l.Finished = true;
                return;
            }
            if (!l.Scanned)
            {
                Scan(l);
                return;
            }
            if (l.Pending.Count == 0)
            {
                Finish(l);
                return;
            }
            l.Credit += Mathf.Max(1f, l.Found * dt / job.Seconds);
            if (l.Credit < 1f || !SafeToChange(l))
                return;
            var batch = new List<int3>();
            var chosen = new HashSet<int3>();
            int limit = Mathf.Min(MaxBatch, (int)l.Credit);
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
            Send(l, batch);
            job.Restored += batch.Count;
        }

        private static bool Prepare(LimbJob l)
        {
            l.Ready = true;
            l.Mesh = l.Limb.GetVoxelMesh();
            var references = l.Limb.References;
            if (!l.Mesh.Exists() || references == null)
                return false;
            var component = references.Cast<Component>();
            l.Receiver = component.GetComponentInChildren<LimbEffectorReceiver>(true);
            l.Separation = component.GetComponentInChildren<VoxelMeshSeparationModule>(true);
            if (l.Receiver == null)
                return false;
            l.Collectors = new List<Destructibility>();
            foreach (var organ in l.Limb.GetAllOrgans())
            {
                // Organs that are whole have nothing to grow back.
                var progress = organ.GetParameterValue<DestructibilityProgress>();
                if (progress.HasValue && progress.Value >= 99.99f)
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

        private static void Scan(LimbJob l)
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
                        if (progress < Whole)
                            l.Pending.Add(index);
                        break;
                    }
                }
            }
            if (l.Cursor < l.Total)
                return;
            l.Scanned = true;
            l.Found = l.Pending.Count;
            // Nearest the middle of the limb first, so it grows outwards.
            var middle = new float3(l.Size.x, l.Size.y, l.Size.z) * 0.5f;
            l.Pending.Sort((a, b) => math.distancesq(new float3(a.x, a.y, a.z), middle).CompareTo(math.distancesq(new float3(b.x, b.y, b.z), middle)));
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

        private static void Send(LimbJob l, List<int3> voxels)
        {
            var signals = new IndexEffectorSignalsList(voxels.Count, false, Allocator.Temp);
            foreach (var voxel in voxels)
                signals.Add(new IndexEffectorSignal(voxel, 100000f, InfluenceProcessType.Sum));
            var handler = new IndexEffectorSignalsHandler<Destruction>(signals, new IndexEffectorDescription());
            try
            {
                l.Receiver.Receive<Destruction>(handler);
            }
            finally
            {
                handler.Dispose();
            }
        }

        private static void Finish(LimbJob l)
        {
            l.Finished = true;
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
