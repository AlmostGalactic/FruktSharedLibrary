using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace FruktSharedLibrary.Core
{
    /// <summary>
    /// Delayed and repeating work on the main thread, plus a thread-safe way to get back onto it.
    /// All callbacks are exception-safe: a throwing callback is logged and (if repeating) cancelled.
    /// </summary>
    public static class Scheduler
    {
        private static readonly List<Job> Jobs = new();
        private static readonly List<Job> Pending = new();
        private static readonly ConcurrentQueue<Action> MainThreadQueue = new();

        /// <summary>A scheduled job. Call <see cref="Cancel"/> to stop it.</summary>
        public sealed class Handle
        {
            internal readonly Job Job;

            internal Handle(Job job) => Job = job;

            /// <summary>False once the job has run (one-shot) or been cancelled.</summary>
            public bool IsActive => !Job.Done;

            public void Cancel() => Job.Done = true;
        }

        internal sealed class Job
        {
            public Action Action;
            public string Name;
            public float Due;
            public float Interval;
            public bool Realtime;
            public int FramesLeft;
            public bool Done;
        }

        /// <summary>Runs the action on the next frame.</summary>
        public static Handle NextFrame(Action action) => Frames(1, action);

        /// <summary>Runs the action after the given number of frames.</summary>
        public static Handle Frames(int frames, Action action)
            => Add(new Job { Action = action, FramesLeft = Math.Max(1, frames), Due = float.MinValue, Name = action?.Method.Name });

        /// <summary>
        /// Runs the action once after a delay. With <paramref name="realtime"/> true (default) the delay ignores
        /// pause and slow motion; with false it follows the game's time scale.
        /// </summary>
        public static Handle After(float seconds, Action action, bool realtime = true)
            => Add(new Job { Action = action, Realtime = realtime, Due = Now(realtime) + seconds, Name = action?.Method.Name });

        /// <summary>Runs the action repeatedly every <paramref name="seconds"/> until cancelled.</summary>
        public static Handle Every(float seconds, Action action, bool realtime = true)
            => Add(new Job { Action = action, Realtime = realtime, Interval = Math.Max(0f, seconds), Due = Now(realtime) + seconds, Name = action?.Method.Name });

        /// <summary>
        /// Queues an action to run on the main thread. Safe to call from any thread
        /// (use this when finishing async/background work that needs to touch the game).
        /// </summary>
        public static void RunOnMainThread(Action action)
        {
            if (action != null)
                MainThreadQueue.Enqueue(action);
        }

        /// <summary>Starts a coroutine (managed IEnumerator). Yield null for a frame or a WaitForSeconds.</summary>
        public static object StartCoroutine(IEnumerator routine) => MelonCoroutines.Start(routine);

        /// <summary>Stops a coroutine started with <see cref="StartCoroutine"/>.</summary>
        public static void StopCoroutine(object token)
        {
            if (token != null)
                MelonCoroutines.Stop(token);
        }

        internal static void Tick()
        {
            while (MainThreadQueue.TryDequeue(out var queued))
                Run(queued, "main-thread action");

            if (Pending.Count > 0)
            {
                Jobs.AddRange(Pending);
                Pending.Clear();
            }

            for (int i = 0; i < Jobs.Count; i++)
            {
                var job = Jobs[i];
                if (job.Done)
                    continue;

                if (job.FramesLeft > 0)
                {
                    if (--job.FramesLeft > 0)
                        continue;
                }
                else if (Now(job.Realtime) < job.Due)
                {
                    continue;
                }

                bool ok = Run(job.Action, job.Name);
                if (job.Interval > 0f && ok && !job.Done)
                    job.Due = Now(job.Realtime) + job.Interval;
                else
                    job.Done = true;
            }

            Jobs.RemoveAll(j => j.Done);
        }

        internal static void Clear()
        {
            Jobs.Clear();
            Pending.Clear();
        }

        private static Handle Add(Job job)
        {
            if (job.Action == null)
                throw new ArgumentNullException("action");
            Pending.Add(job);
            return new Handle(job);
        }

        private static float Now(bool realtime) => realtime ? Time.realtimeSinceStartup : Time.time;

        private static bool Run(Action action, string name)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Error($"Scheduled action '{name}' threw", e);
                return false;
            }
        }
    }
}
