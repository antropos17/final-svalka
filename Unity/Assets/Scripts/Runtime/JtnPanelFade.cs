using UnityEngine;

namespace JunkToNeon
{
    // Fades the panel in each time it opens. Alpha only, so layout and hit targets never move.
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class JtnPanelFade : MonoBehaviour
    {
        const float Seconds=.16f;
        public bool Reduced;
        CanvasGroup group;
        float started=-1;

        void OnEnable()
        {
            group=GetComponent<CanvasGroup>();
            if(Reduced||!Application.isPlaying){group.alpha=1;started=-1;return;}
            started=Time.unscaledTime;group.alpha=0;
        }
        void Update()
        {
            if(started<0)return;
            float t=(Time.unscaledTime-started)/Seconds;
            if(t>=1){group.alpha=1;started=-1;return;}
            group.alpha=1-(1-t)*(1-t);
        }
        void OnDisable()
        {
            if(group!=null)group.alpha=1;
            started=-1;
        }
    }
}
