using System;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.InputSystem
{
    /// <summary>
    /// Service locator for the single <see cref="IInputProvider"/> instance used
    /// across the package. Prevents the previous pattern where every controller
    /// created its own provider (doubled allocations, duplicated event subscriptions
    /// in the new Input System, and conflicting DisableInput() calls).
    ///
    /// Call <see cref="Get"/> whenever you need an input provider. You can inject
    /// a custom implementation via <see cref="SetProvider"/> for tests or alternate
    /// platforms (e.g. gamepad-only builds, network replicated input, etc.).
    /// </summary>
    public static class InputProviderService
    {
        private sealed class ServiceState
        {
            public IInputProvider Provider;
            public InputProviderServiceLifecycle Lifecycle;
        }

        private static readonly ServiceState State = new();

        /// <summary>Returns the shared provider, creating the platform default on first use.</summary>
        public static IInputProvider Get()
        {
            EnsureLifecycleHook();

            if (State.Provider == null)
            {
#if ENABLE_INPUT_SYSTEM
                State.Provider = new NewInputProvider();
#else
                State.Provider = new OldInputProvider();
#endif
            }
            return State.Provider;
        }

        /// <summary>
        /// Replace the active provider. Any previously returned reference is
        /// released via <see cref="IInputProvider.DisableInput"/>. Pass <c>null</c>
        /// to detach and force the next <see cref="Get"/> call to rebuild a default.
        /// </summary>
        public static void SetProvider(IInputProvider provider)
        {
            EnsureLifecycleHook();

            if (State.Provider != null && State.Provider != provider)
                ReleaseProvider();
            State.Provider = provider;
        }

        /// <summary>
        /// Clears the cached provider when the runtime starts. Required when
        /// "Enter Play Mode Options → Reload Domain" is disabled so stale
        /// references from the previous play session don't survive.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseProvider();
            State.Lifecycle = null;
        }

        private static void EnsureLifecycleHook()
        {
            if (State.Lifecycle != null) return;

            var go = new GameObject(nameof(InputProviderService));
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            State.Lifecycle = go.AddComponent<InputProviderServiceLifecycle>();
        }

        private static void ReleaseProvider()
        {
            if (State.Provider == null) return;

            State.Provider.DisableInput();
            if (State.Provider is IDisposable disposable)
                disposable.Dispose();

            State.Provider = null;
        }

        private sealed class InputProviderServiceLifecycle : MonoBehaviour
        {
            private void OnEnable()
            {
                if (State.Lifecycle == null)
                {
                    State.Lifecycle = this;
                    return;
                }

                if (State.Lifecycle != this)
                    Destroy(gameObject);
            }

            private void OnApplicationQuit()
            {
                ReleaseProvider();
            }

            private void OnDestroy()
            {
                // A preserved lifecycle object can be superseded after Fast Enter
                // Play Mode resets the static reference. It must not dispose the
                // provider owned by the replacement lifecycle object.
                if (State.Lifecycle != this)
                    return;

                State.Lifecycle = null;
                ReleaseProvider();
            }
        }
    }
}