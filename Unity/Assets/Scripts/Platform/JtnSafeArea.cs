using System;

namespace JunkToNeon.Platform
{
    public readonly struct SafeAreaAnchors
    {
        public readonly float MinX, MinY, MaxX, MaxY;
        public SafeAreaAnchors(float minX, float minY, float maxX, float maxY)
        { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
    }

    // Pixel coordinates use Unity's bottom-left origin. Invalid samples leave the last fit intact.
    public static class SafeAreaGeometry
    {
        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }

        public static bool TryCalculate(double screenWidth, double screenHeight,
            double x, double y, double width, double height, out SafeAreaAnchors anchors)
        {
            anchors = default;
            if (!Finite(screenWidth) || !Finite(screenHeight) || screenWidth <= 0 || screenHeight <= 0 ||
                !Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) || width <= 0 || height <= 0 ||
                !Finite(x + width) || !Finite(y + height)) return false;

            double left = Math.Max(0, x), bottom = Math.Max(0, y);
            double right = Math.Min(screenWidth, x + width), top = Math.Min(screenHeight, y + height);
            if (right <= left || top <= bottom) return false;
            anchors = new SafeAreaAnchors((float)(left / screenWidth), (float)(bottom / screenHeight),
                (float)(right / screenWidth), (float)(top / screenHeight));
            return true;
        }
    }

#if UNITY_5_3_OR_NEWER
    // Owns anchors only. Attach once to content directly under a root overlay Canvas,
    // replacing any other safe-area writer; existing offsets remain authored padding.
    [UnityEngine.DisallowMultipleComponent]
    [UnityEngine.RequireComponent(typeof(UnityEngine.RectTransform))]
    [UnityEngine.AddComponentMenu("Junk to Neon/Safe Area")]
    public sealed class JtnSafeArea : UnityEngine.MonoBehaviour
    {
        UnityEngine.RectTransform content;
        UnityEngine.Canvas canvas;

        void OnEnable()
        {
            content = GetComponent<UnityEngine.RectTransform>();
            canvas = content.parent == null ? null : content.parent.GetComponent<UnityEngine.Canvas>();
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode != UnityEngine.RenderMode.ScreenSpaceOverlay)
            {
                UnityEngine.Debug.LogWarning("JtnSafeArea requires content directly under a root Screen Space Overlay Canvas; remove nested fitters.", this);
                enabled = false;
                return;
            }
            Refresh();
        }

        void LateUpdate() { Refresh(); }
        void OnApplicationPause(bool paused) { if (!paused) Refresh(); }
        void OnApplicationFocus(bool focused) { if (focused) Refresh(); }

        public void Refresh()
        {
            if (content == null || canvas == null || content.parent != canvas.transform ||
                !canvas.isRootCanvas || canvas.renderMode != UnityEngine.RenderMode.ScreenSpaceOverlay) return;
            var safe = UnityEngine.Screen.safeArea;
            if (!SafeAreaGeometry.TryCalculate(UnityEngine.Screen.width, UnityEngine.Screen.height,
                safe.x, safe.y, safe.width, safe.height, out var anchors)) return;
            Apply(anchors);
        }

        void Apply(SafeAreaAnchors anchors)
        {
            var min = new UnityEngine.Vector2(anchors.MinX, anchors.MinY);
            var max = new UnityEngine.Vector2(anchors.MaxX, anchors.MaxY);
            if (content.anchorMin.Equals(min) && content.anchorMax.Equals(max)) return;
            var paddingMin = content.offsetMin;
            var paddingMax = content.offsetMax;
            content.anchorMin = min;
            content.anchorMax = max;
            content.offsetMin = paddingMin;
            content.offsetMax = paddingMax;
        }
    }
#endif
}
