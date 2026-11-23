using JunkToNeon.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        readonly Image[] reelIncoming=new Image[3];
        readonly TMP_Text[] reelIncomingText=new TMP_Text[3];
        readonly Vector2[] reelRest=new Vector2[3],reelTextRest=new Vector2[3];
        readonly int[] reelStartSymbols=new int[3];
        bool reelMotionReady;

        void BeginReelMotion(ModelState before)
        {
            if(!reelMotionReady)
            {
                for(int i=0;i<3;i++)
                {
                    var image=reelImages[i];var frame=(RectTransform)image.transform.parent;
                    if(!frame.TryGetComponent<RectMask2D>(out _))frame.gameObject.AddComponent<RectMask2D>();
                    reelRest[i]=image.rectTransform.anchoredPosition;reelTextRest[i]=reels[i].rectTransform.anchoredPosition;
                    reelIncoming[i]=Instantiate(image,frame);reelIncoming[i].name="Incoming symbol";reelIncoming[i].raycastTarget=false;
                    reelIncomingText[i]=Instantiate(reels[i],frame);reelIncomingText[i].name="Incoming fallback";reelIncomingText[i].raycastTarget=false;
                }
                reelMotionReady=true;
            }
            ResetReelMotion();
            BeginSlotSound();
            for(int i=0;i<3;i++)reelStartSymbols[i]=before.lastSpin==null?i:before.lastSpin.symbols[i];
        }

        // Integral of a velocity ramp, cruise and brake. Sample the existing
        // foreground clock; no new timer, random draw or presentation wait.
        static float ReelTravel(float time,float duration)
        {
            float ramp=Mathf.Min(.20f,duration*.18f),brake=Mathf.Min(.76f,duration*.44f);
            float area=duration-(ramp+brake)*.5f;
            if(time<ramp)
            {
                float t=time/ramp;
                return ramp*t*t*t*(1-t*.5f)/area;
            }
            if(time<duration-brake)return (time-ramp*.5f)/area;
            // The integrated smoothstep meets cruise and rest with continuous acceleration.
            float remaining=Mathf.Max(0,duration-time)/brake;
            return 1-brake*remaining*remaining*remaining*(1-remaining*.5f)/area;
        }

        void AnimateReels(float elapsed,float duration,int[] result)
        {
            // The audio and rendered symbols use the same stop thresholds.
            float firstStop=duration-.28f;
            const float stopGap=.10f;
            UpdateSlotRollSound(elapsed,firstStop+2*stopGap);
            for(int i=0;i<3;i++)
            {
                float delay=i*.045f,stop=firstStop+i*stopGap,rollDuration=stop-delay;
                if(elapsed>=stop)PlaySlotSeat(i);
                var image=reelImages[i];float height=((RectTransform)image.transform.parent).rect.height;
                int steps=4*Mathf.Max(2,Mathf.RoundToInt(duration*2.2f))+(result[i]-reelStartSymbols[i]+4)%4;
                float travel=steps*ReelTravel(Mathf.Clamp(elapsed-delay,0,rollDuration),rollDuration);
                bool rolling=elapsed<stop&&!rewardFeedbackReduced;
                int index=Mathf.FloorToInt(travel);
                SetReel(i,rolling?(reelStartSymbols[i]+index)%4:elapsed>=stop?result[i]:reelStartSymbols[i]);
                float offset=rolling?-(travel-index)*height:0;
                // A short compression and release with no velocity jump at contact.
                float seat=Mathf.Clamp01((elapsed-stop)/.08f);
                float compress=4*seat*(1-seat);
                if(!rewardFeedbackReduced&&elapsed>=stop)offset=-compress*compress*height*.045f;
                image.rectTransform.anchoredPosition=reelRest[i]+Vector2.up*offset;
                reels[i].rectTransform.anchoredPosition=reelTextRest[i]+Vector2.up*offset;
                int incoming=(reelStartSymbols[i]+index+1)%4;
                var sprite=reelSymbols!=null&&incoming<reelSymbols.Length?reelSymbols[incoming]:null;
                reelIncoming[i].sprite=sprite;reelIncoming[i].enabled=rolling&&sprite!=null;
                reelIncomingText[i].text=names[incoming];reelIncomingText[i].enabled=rolling&&sprite==null;
                reelIncoming[i].rectTransform.anchoredPosition=reelRest[i]+Vector2.up*(offset+height);
                reelIncomingText[i].rectTransform.anchoredPosition=reelTextRest[i]+Vector2.up*(offset+height);
            }
        }

        void ResetReelMotion()
        {
            StopSlotRoll();
            if(!reelMotionReady)return;
            for(int i=0;i<3;i++)
            {
                reelImages[i].rectTransform.anchoredPosition=reelRest[i];reels[i].rectTransform.anchoredPosition=reelTextRest[i];
                reelIncoming[i].enabled=false;reelIncomingText[i].enabled=false;
            }
        }
    }
}
