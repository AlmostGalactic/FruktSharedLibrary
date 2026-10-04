using System;
using System.IO;
using FruktSharedLibrary.Core;

namespace FruktSharedLibrary.Utilities
{
    /// <summary>
    /// Runs code when a file changes, for reloading your models, textures or bundles while the game runs instead
    /// of restarting it. The check runs on the main thread, so the callback can use the game directly.
    /// </summary>
    /// <example>
    /// <code>
    /// string path = Path.Combine(MelonEnvironment.UserDataDirectory, "barrel.obj");
    /// var barrel = Inventory.AddProp("Barrel", Meshes.LoadObj(path));
    /// FileWatch.Start(path, () => barrel.SetMesh(Meshes.LoadObj(path)));
    /// </code>
    /// </example>
    public sealed class FileWatch : IDisposable
    {
        private readonly Action _changed;
        private readonly Scheduler.Handle _timer;
        private (bool Exists, long Length, DateTime Written) _seen, _pending;
        private bool _hasPending;

        private FileWatch(string path, Action changed, float interval)
        {
            Path = System.IO.Path.GetFullPath(path);
            _changed = changed;
            _seen = Stamp();
            _timer = Scheduler.Every(Math.Max(0.1f, interval), Check);
        }

        /// <summary>
        /// Starts watching <paramref name="path"/>. <paramref name="changed"/> runs once the file has changed and
        /// then stayed the same for one more check, so a file that's still being written isn't read half-done.
        /// </summary>
        /// <param name="interval">How often to look, in seconds.</param>
        public static FileWatch Start(string path, Action changed, float interval = 0.5f)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("No path given.", nameof(path));
            if (changed == null)
                throw new ArgumentNullException(nameof(changed));
            return new FileWatch(path, changed, interval);
        }

        /// <summary>The full path being watched.</summary>
        public string Path { get; }

        /// <summary>False once <see cref="Stop"/> has been called.</summary>
        public bool IsWatching => _timer.IsActive;

        /// <summary>Stops watching.</summary>
        public void Stop() => _timer.Cancel();

        void IDisposable.Dispose() => Stop();

        private void Check()
        {
            var now = Stamp();
            if (now == _seen)
            {
                _hasPending = false;
                return;
            }
            if (!_hasPending || now != _pending)
            {
                // Changed since the last look: wait one more check to see that it's settled.
                _pending = now;
                _hasPending = true;
                return;
            }
            if (now.Exists && !CanRead())
                return;
            _hasPending = false;
            _seen = now;
            if (!now.Exists)
                return;
            try
            {
                _changed();
            }
            catch (Exception e)
            {
                FruktLog.Error($"Reacting to a change in '{Path}' failed", e);
            }
        }

        private (bool Exists, long Length, DateTime Written) Stamp()
        {
            try
            {
                var info = new FileInfo(Path);
                return info.Exists ? (true, info.Length, info.LastWriteTimeUtc) : (false, 0L, DateTime.MinValue);
            }
            catch
            {
                return (false, 0L, DateTime.MinValue);
            }
        }

        // A program still writing the file usually has it locked.
        private bool CanRead()
        {
            try
            {
                using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
