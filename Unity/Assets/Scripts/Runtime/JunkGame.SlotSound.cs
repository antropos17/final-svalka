using UnityEngine;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        AudioSource slotRollSource;
        AudioSource slotAccentSource;
        AudioSource slotCoinSource;
        AudioClip slotSeatClip;
        AudioClip slotCoinClip;
        AudioClip slotLargeWinClip;
        int slotSettledReels;

        bool SlotEffectsAllowed => isActiveAndEnabled && !InBackground && !rewardFeedbackReduced && !panel.activeSelf;

        void PrepareSlotSound()
        {
            if (slotRollSource != null) return;

            // The Canvas-only scene needs a listener for these non-spatial cues.
            if (FindAnyObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            slotRollSource = CreateSlotAudioSource();
            slotRollSource.clip = Resources.Load<AudioClip>("Audio/Slot/reel-roll");
            slotRollSource.loop = true;
            slotAccentSource = CreateSlotAudioSource();
            slotCoinSource = CreateSlotAudioSource();
            slotSeatClip = Resources.Load<AudioClip>("Audio/Slot/reel-seat");
            slotCoinClip = Resources.Load<AudioClip>("Audio/Slot/coin-credit");
            slotLargeWinClip = Resources.Load<AudioClip>("Audio/Slot/large-win-impact");
        }

        AudioSource CreateSlotAudioSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            return source;
        }

        void BeginSlotSound()
        {
            StopSlotReelCues();
            slotSettledReels = 0;
        }

        void UpdateSlotRollSound(float elapsed, float lastStop)
        {
            if (!SlotEffectsAllowed || elapsed >= lastStop)
            {
                StopSlotRoll();
                return;
            }

            PrepareSlotSound();
            if (slotRollSource.clip == null) return;
            float ramp = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .16f));
            float braking = Mathf.SmoothStep(0, 1, Mathf.Clamp01((lastStop - elapsed) / .36f));
            slotRollSource.pitch = Mathf.Lerp(.72f, 1.06f, ramp * braking);
            slotRollSource.volume = .23f * ramp * braking;
            if (!slotRollSource.isPlaying) slotRollSource.Play();
        }

        void PlaySlotSeat(int reel)
        {
            int bit = 1 << reel;
            if ((slotSettledReels & bit) != 0) return;
            slotSettledReels |= bit;
            if (!SlotEffectsAllowed) return;

            RequestSlotHaptic(confirmation: false);
            PrepareSlotSound();
            if (slotSeatClip == null) return;
            slotAccentSource.pitch = .9f + reel * .08f;
            slotAccentSource.panStereo = (reel - 1) * .25f;
            slotAccentSource.PlayOneShot(slotSeatClip, .72f);
        }

        void PlaySlotLargeWin()
        {
            if (!SlotEffectsAllowed) return;

            PrepareSlotSound();
            if (slotLargeWinClip == null) return;
            slotAccentSource.pitch = 1;
            slotAccentSource.panStereo = 0;
            slotAccentSource.PlayOneShot(slotLargeWinClip, .66f);
        }

        void PlaySlotCoinArrival(int index)
        {
            if (!SlotEffectsAllowed) return;

            PrepareSlotSound();
            if (slotCoinClip == null) return;
            slotCoinSource.pitch = 1 + index * .028f;
            slotCoinSource.PlayOneShot(slotCoinClip, .26f);
        }

        void StopSlotRoll()
        {
            if (slotRollSource != null && slotRollSource.isPlaying) slotRollSource.Stop();
        }

        void StopSlotFeedback()
        {
            StopSlotReelCues();
            if (slotCoinSource != null && slotCoinSource.isPlaying) slotCoinSource.Stop();
        }

        void StopSlotReelCues()
        {
            CancelQueuedSlotHaptics();
            StopSlotRoll();
            if (slotAccentSource != null && slotAccentSource.isPlaying) slotAccentSource.Stop();
        }
    }
}
