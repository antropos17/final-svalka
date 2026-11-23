using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JunkToNeon
{
    // Nonvisual observer on an existing shared Button. The installed input module
    // decides click eligibility before pointerUp, so its immediate onClick wrapper
    // consumes our independent release verdict instead of changing eventData.
    [DisallowMultipleComponent]
    public sealed class JtnPanelPointerConsent : MonoBehaviour, IPointerDownHandler, IPointerMoveHandler, IPointerUpHandler
    {
        readonly struct Press
        {
            readonly Vector2 position;
            readonly float thresholdSquared;
            public Press(Vector2 position, int threshold)
            { this.position = position; thresholdSquared = (float)threshold * threshold; }
            public bool StillTap(PointerEventData data)
            { return !data.dragging && (data.position - position).sqrMagnitude <= thresholdSquared; }
        }
        readonly Dictionary<int, Press> pressed = new Dictionary<int, Press>();
        bool releasePending, releaseAllowed;
        int releaseFrame = -1;

        public void Invalidate()
        { pressed.Clear(); releasePending = false; releaseFrame = -1; }

        void OnDisable() { Invalidate(); }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            var button = GetComponent<Button>();
            if (button != null && button.isActiveAndEnabled && button.IsInteractable())
                pressed[data.pointerId] = new Press(data.position, Mathf.Max(0, EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10));
        }

        public void OnPointerMove(PointerEventData data)
        {
            // Crossing the threshold cancels this contact even if it returns.
            if (pressed.TryGetValue(data.pointerId, out var press) && !press.StillTap(data)) pressed.Remove(data.pointerId);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            bool sameContext = pressed.TryGetValue(data.pointerId, out var press) && press.StillTap(data);
            pressed.Remove(data.pointerId);
            var button = GetComponent<Button>();
            // Match the installed InputSystemUIInputModule click-candidate test.
            // Outside/drag/canceled releases must leave no token for Submit.
            var hit = ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject);
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable() ||
                !data.eligibleForClick || data.pointerClick != gameObject || hit != gameObject) return;
            releasePending = true; releaseAllowed = sameContext; releaseFrame = Time.frameCount;
        }

        public bool AllowInvocation()
        {
            // Button.OnSubmit has no pointerDown/Up. Keep that path and existing
            // trusted/programmatic calls; this guard only vetoes an actual release.
            bool allowed = !releasePending || releaseFrame != Time.frameCount || releaseAllowed;
            releasePending = false;
            return allowed;
        }
    }
}
