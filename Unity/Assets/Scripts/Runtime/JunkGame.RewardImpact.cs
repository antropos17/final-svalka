using System;
using JunkToNeon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        const int RewardCoinCapacity=6,RewardSparkCapacity=12;
        const float CoinLiftDelay=.03f,CoinStagger=.065f,CoinFlightTime=.43f,CoinReceiveTime=.16f;
        readonly Image[] rewardImpact=new Image[RewardCoinCapacity+RewardSparkCapacity];
        RectTransform rewardImpactRoot,rewardImpactTarget,rewardImpactIcon;
        Image rewardBurst,rewardImpactGlint;
        Vector2 rewardImpactOrigin,rewardImpactEnd;
        Vector3 rewardImpactIconScale;
        float rewardImpactAge;
        int rewardImpactCount,rewardImpactSparkCount,rewardImpactLanded;
        long rewardImpactCoinsToCount;
        long? rewardImpactDisplayedCoins;

        void BeginRewardImpact(Vector3 source,long coinCredit,long walletAfter)
        {
            if(panel.activeSelf)return;
            ResetRewardImpact();
            if(rewardImpactRoot==null)
            {
                rewardImpactRoot=Area("Spin reward impact",safe,Vector2.zero,Vector2.one);
                rewardBurst=CreateRewardImpactImage("Golden burst",StyleC("Effects/gold-burst"),104);
                var coin=StyleC("Icons/coin");
                var spark=StyleC("Effects/gold-spark");
                rewardImpactGlint=CreateRewardImpactImage("Coin wallet glint",spark,25);
                for(int i=0;i<RewardCoinCapacity;i++)
                    rewardImpact[i]=CreateRewardImpactImage("Reward gold",coin,i==0?29:22);
                for(int i=0;i<RewardSparkCapacity;i++)
                    rewardImpact[i+RewardCoinCapacity]=CreateRewardImpactImage("Golden spark",spark,16);
            }
            var reel=reelImages!=null&&reelImages.Length>1&&reelImages[1]!=null?reelImages[1].rectTransform:null;
            rewardImpactOrigin=RewardImpactPoint(reel!=null?reel.TransformPoint(reel.rect.center):source);
            rewardImpactTarget=rewardFeedbackCoinTarget;
            if(productCoins!=null&&rewardImpactTarget==productCoins.rectTransform)
            {
                var icon=productCoins.transform.parent.GetComponentInChildren<JtnProductIcon>();
                if(icon!=null)
                {
                    rewardImpactIcon=rewardImpactTarget=icon.rectTransform;
                    rewardImpactIconScale=rewardImpactIcon.localScale;
                }
            }
            rewardImpactEnd=rewardImpactOrigin+Vector2.up*65;
            rewardImpactAge=0;
            bool large=coinCredit>=EconomyRules.TripleReward;
            rewardImpactCount=large?RewardCoinCapacity:coinCredit>=EconomyRules.PairReward?4:2;
            rewardImpactSparkCount=large?RewardSparkCapacity:rewardImpactCount;
            rewardImpactLanded=0;
            // A simultaneous construction debit can consume part or all of this income.
            rewardImpactCoinsToCount=Math.Min(coinCredit,walletAfter);
            if(rewardImpactCoinsToCount>0)
                rewardImpactCount=(int)Math.Min(rewardImpactCount,rewardImpactCoinsToCount);
            if(productCoins!=null&&productCoins.gameObject.activeInHierarchy&&
                rewardImpactTarget!=null&&rewardImpactTarget.gameObject.activeInHierarchy&&rewardImpact[0].sprite!=null)
            {
                rewardImpactDisplayedCoins=walletAfter-rewardImpactCoinsToCount;
                RenderProductCoinCounter();
            }
            rewardImpactRoot.SetAsLastSibling();
            rewardImpactRoot.gameObject.SetActive(true);
            rewardBurst.rectTransform.anchoredPosition=rewardImpactOrigin;
            rewardBurst.enabled=rewardBurst.sprite!=null;
            for(int i=0;i<rewardImpact.Length;i++)
            {
                bool used=i<RewardCoinCapacity?i<rewardImpactCount:i-RewardCoinCapacity<rewardImpactSparkCount;
                rewardImpact[i].gameObject.SetActive(used&&rewardImpact[i].sprite!=null);
            }
            AnimateRewardImpact(0);
            if(large&&rewardBurst.enabled)PlaySlotLargeWin();
        }

        Vector2 RewardImpactPoint(Vector3 point)=>(Vector2)rewardImpactRoot.InverseTransformPoint(point)-rewardImpactRoot.rect.center;

        Image CreateRewardImpactImage(string name,Sprite sprite,float size)
        {
            var rect=Area(name,rewardImpactRoot,Vector2.one*.5f,Vector2.one*.5f);
            rect.sizeDelta=Vector2.one*size;
            var image=rect.gameObject.AddComponent<Image>();
            image.raycastTarget=false;
            image.preserveAspect=true;
            image.sprite=sprite;
            return image;
        }

        bool AnimateRewardImpact(float delta)
        {
            if(rewardImpactRoot==null||!rewardImpactRoot.gameObject.activeSelf)return false;
            rewardImpactAge+=delta;
            float arrival=CoinArrivalTime(rewardImpactCount-1);
            if(panel.activeSelf){StopSlotFeedback();ResetRewardImpact();return false;}
            if(rewardImpactAge>=arrival+CoinReceiveTime){ResetRewardImpact();return false;}

            bool targetVisible=rewardImpactTarget!=null&&rewardImpactTarget.gameObject.activeInHierarchy;
            if(targetVisible)rewardImpactEnd=RewardImpactPoint(rewardImpactTarget.TransformPoint(rewardImpactTarget.rect.center));
            while(rewardImpactLanded<rewardImpactCount&&rewardImpactAge>=CoinArrivalTime(rewardImpactLanded))
            {
                int index=rewardImpactLanded++;
                if(rewardImpactDisplayedCoins.HasValue)
                {
                    long step=rewardImpactCoinsToCount/rewardImpactCount;
                    if(index<rewardImpactCoinsToCount%rewardImpactCount)step++;
                    rewardImpactDisplayedCoins+=step;
                    RenderProductCoinCounter();
                }
                if(targetVisible&&rewardImpact[index].sprite!=null)PlaySlotCoinArrival(index);
            }

            bool large=rewardImpactSparkCount==RewardSparkCapacity;
            float burst=1-Mathf.Pow(1-Mathf.Clamp01(rewardImpactAge/(large?.21f:.18f)),3);
            float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.04f,large?.34f:.24f,rewardImpactAge));
            float burstScale=large?1.66f:.88f+rewardImpactSparkCount*.04f;
            rewardBurst.rectTransform.localScale=Vector3.one*Mathf.Lerp(.58f,burstScale,burst);
            rewardBurst.color=new Color(1,1,1,fade*(large?.64f:.58f));

            Vector2 span=rewardImpactEnd-rewardImpactOrigin;
            for(int i=0;i<rewardImpactCount;i++)
            {
                var coin=rewardImpact[i];
                float flight=(rewardImpactAge-CoinLiftDelay-i*CoinStagger)/CoinFlightTime;
                coin.enabled=flight>=0&&i>=rewardImpactLanded;
                if(coin.enabled)
                {
                    float lane=i==0?0:(i%2==0?-1:1)*Mathf.Ceil(i*.5f);
                    float t=Mathf.SmoothStep(0,1,flight),remaining=1-t;
                    Vector2 start=rewardImpactOrigin+new Vector2(lane*6,Mathf.Abs(lane)*2);
                    Vector2 bend=rewardImpactOrigin+new Vector2(span.x*.1f+lane*22,span.y*.62f);
                    coin.rectTransform.anchoredPosition=remaining*remaining*start+2*remaining*t*bend+t*t*rewardImpactEnd;
                    coin.rectTransform.localRotation=Quaternion.Euler(0,0,lane*6+Mathf.Sin(flight*5+i)*19*remaining);
                    float size=Mathf.Lerp(1,.56f,Mathf.InverseLerp(.55f,1,flight));
                    float turn=.64f+.36f*Mathf.Abs(Mathf.Cos(flight*7+i*.55f));
                    coin.rectTransform.localScale=new Vector3(size*turn,size,1);
                    coin.color=new Color(1,1,1,Mathf.InverseLerp(0,.08f,flight)*(1-Mathf.InverseLerp(.91f,1,flight)));
                }
            }

            // Glints stay at the source. The coins are the only objects that cross the screen.
            float tSpark=Mathf.Clamp01(rewardImpactAge/(large?.39f:.30f));
            for(int i=0;i<rewardImpactSparkCount;i++)
            {
                var spark=rewardImpact[i+RewardCoinCapacity];
                float angle=(i*137.5f+25)*Mathf.Deg2Rad;
                var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                float spread=large?65+7*(i%4):49;
                spark.rectTransform.anchoredPosition=rewardImpactOrigin+direction*(7+spread*burst);
                spark.rectTransform.localRotation=Quaternion.Euler(0,0,i*41+tSpark*32);
                spark.rectTransform.localScale=Vector3.one*Mathf.Lerp(.85f,.35f,tSpark);
                spark.color=new Color(1,1,1,1-Mathf.SmoothStep(0,1,tSpark));
            }

            bool receiving=targetVisible&&rewardImpactAge>=arrival;
            float receive=Mathf.Clamp01((rewardImpactAge-arrival)/CoinReceiveTime);
            if(rewardImpactIcon!=null)
            {
                float press=4*receive*(1-receive);
                rewardImpactIcon.localScale=rewardImpactIconScale*(receiving?1+.17f*press:1);
            }
            rewardImpactGlint.enabled=receiving&&rewardImpactGlint.sprite!=null;
            if(receiving)
            {
                rewardImpactGlint.rectTransform.anchoredPosition=rewardImpactEnd;
                rewardImpactGlint.rectTransform.localScale=Vector3.one*Mathf.Lerp(.65f,1.32f,receive);
                rewardImpactGlint.color=new Color(1,1,1,.72f*(1-receive));
            }
            return true;
        }

        static float CoinArrivalTime(int index)=>CoinLiftDelay+index*CoinStagger+CoinFlightTime;

        void ResetRewardImpact()
        {
            if(rewardImpactDisplayedCoins.HasValue)
            {
                rewardImpactDisplayedCoins=null;
                RenderProductCoinCounter();
            }
            if(rewardImpactIcon!=null)rewardImpactIcon.localScale=rewardImpactIconScale;
            rewardImpactIcon=rewardImpactTarget=null;
            if(rewardImpactRoot!=null)rewardImpactRoot.gameObject.SetActive(false);
            foreach(var image in rewardImpact)if(image!=null)image.gameObject.SetActive(false);
        }
    }
}
