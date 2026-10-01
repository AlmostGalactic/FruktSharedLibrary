using System;
using MelonLoader;

namespace FruktSharedLibrary.Core
{
    /// <summary>
    /// Logging used by the library. Mods can use it too, but should normally prefer their own
    /// <c>LoggerInstance</c> so log lines are attributed to them.
    /// </summary>
    public static class FruktLog
    {
        private static MelonLogger.Instance _logger = new MelonLogger.Instance("FruktSharedLibrary");

        internal static void Initialize(MelonLogger.Instance logger) => _logger = logger ?? _logger;

        /// <summary>True when the "DebugLogging" preference is enabled.</summary>
        public static bool DebugEnabled => ForceDebug || FruktConfig.DebugLogging;

        /// <summary>Turns debug output on without touching the saved preference (used by the self-test).</summary>
        internal static bool ForceDebug { get; set; }

        public static void Msg(string message) => _logger.Msg(message);

        public static void Warning(string message) => _logger.Warning(message);

        public static void Error(string message) => _logger.Error(message);

        public static void Error(string message, Exception exception) => _logger.Error($"{message}: {exception}");

        /// <summary>Only written when debug logging is enabled in the preferences.</summary>
        public static void Debug(string message)
        {
            if (DebugEnabled)
                _logger.Msg("[debug] " + message);
        }
    }
}
