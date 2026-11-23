using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JunkToNeon.Presentation
{
    public enum BackContext { Root, DismissiblePanel, RestoreConfirmation, BlockingError }

    // Routes requests only. The existing game owns its modal state and safe close/cancel actions.
    [DisallowMultipleComponent]
    public sealed class JtnBackNavigation : MonoBehaviour
    {
        Func<BackContext> context;
        Action closePanel, cancelRestore, requestRoot, stopAuto;
        int lastRequestFrame = -1;
        bool paused;

        public void Configure(Func<BackContext> getContext, Action closeDismissiblePanel,
            Action cancelRestoreConfirmation, Action requestRootConfirmation = null, Action stopAutomaticSpins = null)
        {
            context = getContext ?? throw new ArgumentNullException(nameof(getContext));
            closePanel = closeDismissiblePanel ?? throw new ArgumentNullException(nameof(closeDismissiblePanel));
            cancelRestore = cancelRestoreConfirmation ?? throw new ArgumentNullException(nameof(cancelRestoreConfirmation));
            requestRoot = requestRootConfirmation;
            stopAuto = stopAutomaticSpins;
        }

        void Update()
        {
            // Keep the project's installed Input System Escape path; no legacy-input fallback.
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) RequestBack();
        }

        void OnApplicationPause(bool value) { paused = value; }

        public bool RequestBack()
        {
            // Root and restore-cancel callbacks are trusted Runtime paths without
            // their own foreground guard. A background request must reach neither.
            if (paused || !isActiveAndEnabled || context == null || lastRequestFrame == Time.frameCount) return false;
            var current = context();
            lastRequestFrame = Time.frameCount; // Also prevents a reentrant or duplicate same-frame request.
            stopAuto?.Invoke();
            switch (current)
            {
                case BackContext.DismissiblePanel: closePanel(); break;
                case BackContext.RestoreConfirmation: cancelRestore(); break;
                case BackContext.Root: requestRoot?.Invoke(); break;
                // A blocking error must retain its recovery UI. No dismiss, retry or restore callback.
                case BackContext.BlockingError: break;
            }
            return true;
        }
    }
}
