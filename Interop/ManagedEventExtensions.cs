using System;
using Il2CppInterop.Runtime;
using Il2CppTripledoseLibs.Primitives.Patterns.Events;

namespace FruktSharedLibrary.Interop
{
    /// <summary>
    /// Lets managed (C#) code subscribe to the game's own <c>IManagedEvent</c> events
    /// (for example <c>IPauseService.OnChangePauseState</c> or <c>IKillsService.OnKillAdded</c>).
    /// Dispose the returned object to unsubscribe.
    /// </summary>
    public static class ManagedEventExtensions
    {
        /// <summary>Subscribes a C# handler to a parameterless game event.</summary>
        public static IDisposable Listen(this IManagedEvent gameEvent, Action handler)
        {
            if (gameEvent == null)
                throw new ArgumentNullException(nameof(gameEvent));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var bag = new SingleShotActionsBag();
            Action safe = () => Invoke(handler);
            gameEvent.Subscribe(DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(safe), bag);
            return new Subscription(bag);
        }

        /// <summary>Subscribes a C# handler to a game event that carries a value.</summary>
        public static IDisposable Listen<T>(this IManagedEvent<T> gameEvent, Action<T> handler)
        {
            if (gameEvent == null)
                throw new ArgumentNullException(nameof(gameEvent));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var bag = new SingleShotActionsBag();
            Action<T> safe = value => Invoke(() => handler(value));
            gameEvent.Subscribe(DelegateSupport.ConvertDelegate<Il2CppSystem.Action<T>>(safe), bag);
            return new Subscription(bag);
        }

        private static void Invoke(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Core.FruktLog.Error("Game event handler threw", e);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private SingleShotActionsBag _bag;

            public Subscription(SingleShotActionsBag bag) => _bag = bag;

            public void Dispose()
            {
                var bag = _bag;
                _bag = null;
                if (bag == null)
                    return;
                try
                {
                    bag.Fire();
                }
                catch (Exception e)
                {
                    Core.FruktLog.Debug("Unsubscribing a game event failed: " + e.Message);
                }
            }
        }
    }
}
