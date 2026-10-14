using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Events
{
    /// <summary>
    /// Implement on pooled event classes to reset internal state when the event
    /// is fetched from the pool for reuse.
    /// </summary>
    public interface IResetable
    {
        void Reset();
    }

    /// <summary>
    /// Central, type-safe publisher/subscriber with a lightweight object pool for
    /// class-shaped event payloads. Subscriptions are keyed by type so publishers
    /// and subscribers never need to reference each other directly.
    /// </summary>
    /// <remarks>
    /// Static state is cleared automatically when the Unity runtime (re)initializes,
    /// preventing stale delegates from leaking across play-mode sessions when
    /// "Enter Play Mode Options" disables domain reload.
    /// </remarks>
    public static class EventManager
    {
        private static readonly Dictionary<Type, Delegate> _events = new Dictionary<Type, Delegate>();
        private static readonly Dictionary<Type, Queue<object>> _pools = new Dictionary<Type, Queue<object>>();

        #region Standard Events (Structs/Classes)
        /// <summary>
        /// Registers a listener for the exact payload type <typeparamref name="T"/>.
        /// Subscribe in <c>OnEnable</c> and unsubscribe in <c>OnDisable</c> for
        /// scene objects so disabled or destroyed objects do not keep receiving events.
        /// </summary>
        /// <typeparam name="T">The event payload type, for example <see cref="CreateItemEvent"/>.</typeparam>
        /// <param name="action">Callback invoked synchronously when the event is published.</param>
        public static void Subscribe<T>(Action<T> action)
        {
            if (action == null) return;
            Type type = typeof(T);
            if (_events.ContainsKey(type))
                _events[type] = Delegate.Combine(_events[type], action);
            else
                _events.Add(type, action);
        }

        /// <summary>
        /// Removes a listener previously registered with <see cref="Subscribe{T}"/>.
        /// Passing null is allowed and does nothing.
        /// </summary>
        /// <typeparam name="T">The event payload type used during subscription.</typeparam>
        /// <param name="action">The exact callback instance to remove.</param>
        public static void Unsubscribe<T>(Action<T> action)
        {
            if (action == null) return;
            Type type = typeof(T);
            if (_events.ContainsKey(type))
            {
                var currentDel = Delegate.Remove(_events[type], action);
                if (currentDel == null) _events.Remove(type);
                else _events[type] = currentDel;
            }
        }

        /// <summary>
        /// Publishes a payload to all listeners registered for its exact type.
        /// Delivery is synchronous and ordered by delegate registration order.
        /// If nobody is subscribed, publishing is a no-op.
        /// </summary>
        /// <typeparam name="T">The event payload type.</typeparam>
        /// <param name="payload">The event data to deliver.</param>
        public static void Publish<T>(T payload)
        {
            if (_events.TryGetValue(typeof(T), out Delegate currentDel))
                (currentDel as Action<T>)?.Invoke(payload);
        }
        #endregion

        #region Pooled Class Events (lower-GC for classes)
        /// <summary>
        /// Gets a reusable class-shaped event payload. Use this for mutable request/response
        /// events such as <see cref="GetCharacterColorizerEvent"/> where the receiver writes
        /// data back into the payload.
        /// </summary>
        /// <typeparam name="T">A reference type payload with a public parameterless constructor.</typeparam>
        public static T GetEvent<T>() where T : class, new()
        {
            Type type = typeof(T);
            if (!_pools.TryGetValue(type, out var queue))
            {
                queue = new Queue<object>();
                _pools[type] = queue;
            }

            if (queue.Count > 0)
            {
                var evt = (T)queue.Dequeue();
                if (evt is IResetable resetable) resetable.Reset();
                return evt;
            }
            return new T();
        }

        /// <summary>
        /// Publishes a class-shaped payload and returns it to the internal pool immediately
        /// after subscribers have run. Do not store pooled payload references after publishing.
        /// </summary>
        /// <typeparam name="T">A reference type payload.</typeparam>
        /// <param name="payload">The payload instance, usually created by <see cref="GetEvent{T}"/>.</param>
        public static void PublishPooled<T>(T payload) where T : class
        {
            Publish(payload);

            // Auto-release to pool after publish.
            Type type = typeof(T);
            if (_pools.TryGetValue(type, out var queue))
            {
                queue.Enqueue(payload);
            }
        }
        #endregion

        /// <summary>
        /// Drops every subscription and pooled payload. Call when you want a hard reset,
        /// e.g. after unloading all gameplay scenes.
        /// </summary>
        public static void Clear()
        {
            _events.Clear();
            _pools.Clear();
        }

#if UNITY_2019_3_OR_NEWER
        /// <summary>
        /// Ensures static dictionaries are empty whenever the runtime (re)initializes.
        /// Guards against leaked subscribers when "Enter Play Mode Options" disables
        /// domain reload between play sessions.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _events.Clear();
            _pools.Clear();
        }
#endif
    }
}
