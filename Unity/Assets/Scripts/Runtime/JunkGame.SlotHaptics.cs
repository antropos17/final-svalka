#if UNITY_ANDROID
using System.Threading;
using UnityEngine;
using UnityEngine.Android;
#endif

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
#if UNITY_EDITOR
        // The simulator can observe the cue without invoking a physical actuator.
        event System.Action<bool> SlotHapticRequested;
#endif
#if UNITY_ANDROID
        // Values of android.view.HapticFeedbackConstants available on supported SDKs.
        const int AndroidClockTick = 4;
        const int AndroidVirtualKey = 1;
        const int AndroidConfirm = 16;
        int slotHapticGeneration;
        int androidRewardHaptic;
#endif

        void RequestSlotHaptic(bool confirmation)
        {
#if UNITY_EDITOR
            SlotHapticRequested?.Invoke(confirmation);
#endif
#if UNITY_ANDROID
            if (Application.platform != RuntimePlatform.Android) return;
            int generation = Volatile.Read(ref slotHapticGeneration);
            AndroidApplication.InvokeOnUIThread(() =>
            {
                if (generation != Volatile.Read(ref slotHapticGeneration)) return;
                try
                {
                    var activity = AndroidApplication.currentActivity;
                    using var window = activity?.Call<AndroidJavaObject>("getWindow");
                    using var view = window?.Call<AndroidJavaObject>("getDecorView");
                    if (view == null || !view.Call<bool>("hasWindowFocus")) return;

                    if (confirmation && androidRewardHaptic == 0)
                    {
                        using var version = new AndroidJavaClass("android.os.Build$VERSION");
                        androidRewardHaptic = version.GetStatic<int>("SDK_INT") >= 30 ? AndroidConfirm : AndroidVirtualKey;
                    }

                    // Use the system touch-feedback preference without override flags.
                    if (generation == Volatile.Read(ref slotHapticGeneration))
                        view.Call<bool>("performHapticFeedback", confirmation ? androidRewardHaptic : AndroidClockTick);
                }
                catch (AndroidJavaException error)
                {
                    Debug.LogWarning("Slot haptic unavailable: " + error.Message);
                }
            });
#endif
        }

        void CancelQueuedSlotHaptics()
        {
#if UNITY_ANDROID
            Interlocked.Increment(ref slotHapticGeneration);
#endif
        }
    }
}
