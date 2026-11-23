using System;
using System.Collections;
using System.Collections.Generic;
using JunkToNeon.Core;
using TMPro;
using UnityEngine;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        const int RewardFeedbackCapacity=8;
        const float RewardFeedbackLifetime=.65f;
        [SerializeField] RectTransform rewardFeedbackRoot;
        [SerializeField] TMP_Text[] rewardFeedbackLabels=new TMP_Text[RewardFeedbackCapacity];
        [SerializeField] RectTransform rewardFeedbackCoinTarget,rewardFeedbackDiamondTarget,rewardFeedbackResourceTarget,rewardFeedbackTicketTarget;
        readonly bool[] rewardFeedbackActive=new bool[RewardFeedbackCapacity];
        readonly Vector2[] rewardFeedbackStarts=new Vector2[RewardFeedbackCapacity],rewardFeedbackEnds=new Vector2[RewardFeedbackCapacity];
        readonly float[] rewardFeedbackAges=new float[RewardFeedbackCapacity],rewardFeedbackDelays=new float[RewardFeedbackCapacity];
        readonly float[] rewardFeedbackArcs=new float[RewardFeedbackCapacity];
        readonly bool[] rewardFeedbackCurrencyUsed=new bool[6];
        Coroutine rewardFeedbackAnimation;
        int rewardFeedbackNext;
        long rewardFeedbackGeneration;
        bool rewardFeedbackCreated,rewardFeedbackReduced;
        RewardFeedbackBatch rewardFeedbackPending;
        GameSession rewardFeedbackSession;
        long rewardFeedbackAcceptedSequence=-1;

        enum RewardFeedbackOrigin { Spin, Arcade }
        sealed class RewardFeedbackBatch
        {
            public readonly GameSession Session;
            public readonly long Generation,Sequence,Instance;
            public readonly string Kind;
            public readonly RewardFeedbackItem[] Credits;
            public readonly Vector3 Source;
            public RewardFeedbackBatch(GameSession session,long generation,ArcadeEvent receipt,Vector3 source)
            {
                Session=session;Generation=generation;Sequence=receipt.Sequence;Instance=receipt.InstanceSequence;Kind=receipt.Kind;Source=source;
                // These totals are authoritative credited amounts. Included slot
                // and bonus components are never added to them a second time.
                Credits=new[]{new RewardFeedbackItem(RewardFeedbackCurrency.Coins,receipt.CoinIncome),
                    new RewardFeedbackItem(RewardFeedbackCurrency.Diamonds,receipt.DiamondsAdded),
                    new RewardFeedbackItem(RewardFeedbackCurrency.Parts,receipt.PartsAdded),
                    new RewardFeedbackItem(RewardFeedbackCurrency.Potatoes,receipt.PotatoesAdded),
                    new RewardFeedbackItem(RewardFeedbackCurrency.Energy,receipt.EnergyAdded),
                    new RewardFeedbackItem(RewardFeedbackCurrency.Tickets,receipt.TicketsAdded)};
            }
        }

        enum RewardFeedbackCurrency { Coins, Diamonds, Parts, Potatoes, Energy, Tickets }
        readonly struct RewardFeedbackItem
        {
            public readonly RewardFeedbackCurrency Currency;
            public readonly long Amount;
            public RewardFeedbackItem(RewardFeedbackCurrency currency,long actualCreditedAmount)
            {Currency=currency;Amount=actualCreditedAmount;}
        }

        // No Session, quote, receipt or store access: safe for stopped authoring,
        // fallback UI creation and an existing serialized Canvas alike.
        public void CreateRewardFeedbackUi()
        {
            if(rewardFeedbackCreated&&Application.isPlaying)return;
            if(safe==null||font==null)throw new InvalidOperationException("Create the existing Canvas/font before reward feedback.");
            if(rewardFeedbackRoot==null)rewardFeedbackRoot=Area("RewardFeedback",safe,Vector2.zero,Vector2.one);
            var group=rewardFeedbackRoot.GetComponent<CanvasGroup>();
            if(group==null)group=rewardFeedbackRoot.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts=false;group.interactable=false;
            if(rewardFeedbackLabels==null)rewardFeedbackLabels=new TMP_Text[RewardFeedbackCapacity];
            else if(rewardFeedbackLabels.Length!=RewardFeedbackCapacity)Array.Resize(ref rewardFeedbackLabels,RewardFeedbackCapacity);
            for(int i=0;i<RewardFeedbackCapacity;i++)
            {
                if(rewardFeedbackLabels[i]==null)
                {
                    var text=Label("",rewardFeedbackRoot,new Vector2(.5f,.5f),new Vector2(.5f,.5f),14,ColorOf("F4B85C"),TextAlignmentOptions.Center);
                    text.name="RewardFeedback"+i;rewardFeedbackLabels[i]=text;
                }
                // Applied to serialized labels too: readable size without a scene edit.
                var style=rewardFeedbackLabels[i];style.rectTransform.sizeDelta=new Vector2(200,36);
                style.enableAutoSizing=true;style.fontSizeMin=12;style.fontSizeMax=18;style.fontStyle=FontStyles.Bold;
                rewardFeedbackLabels[i].raycastTarget=false;rewardFeedbackLabels[i].gameObject.SetActive(false);rewardFeedbackActive[i]=false;
            }
            if(rewardFeedbackCoinTarget==null&&wallet!=null)rewardFeedbackCoinTarget=wallet.rectTransform;
            if(rewardFeedbackResourceTarget==null&&expansionHudResources!=null)rewardFeedbackResourceTarget=expansionHudResources.rectTransform;
            rewardFeedbackRoot.gameObject.SetActive(false);rewardFeedbackCreated=true;
        }
        public void ConfigureRewardFeedbackTargets(RectTransform diamonds,RectTransform resources,RectTransform tickets)
        {rewardFeedbackDiamondTarget=diamonds;rewardFeedbackResourceTarget=resources;rewardFeedbackTicketTarget=tickets;}
        public bool RewardFeedbackReduced=>rewardFeedbackReduced;
        public int RewardFeedbackVisibleCount
        {
            get{int count=0;for(int i=0;i<RewardFeedbackCapacity;i++)if(rewardFeedbackActive[i])count++;return count;}
        }
        public void SetRewardFeedbackReduced(bool reduced)
        {
            if(rewardFeedbackReduced==reduced)return;
            rewardFeedbackReduced=reduced;ClearRewardFeedback();ResetReelMotion();
            if(panel!=null&&panel.TryGetComponent<JtnPanelFade>(out var fade))fade.Reduced=reduced;
        }
        public void ClearRewardFeedback()
        {
            StopSlotFeedback();
            rewardFeedbackGeneration++;
            rewardFeedbackPending=null;
            if(rewardFeedbackAnimation!=null){StopCoroutine(rewardFeedbackAnimation);rewardFeedbackAnimation=null;}
            ResetRewardFeedbackVisuals();
        }
        void ResetRewardFeedbackVisuals()
        {
            ResetRewardImpact();
            for(int i=0;i<RewardFeedbackCapacity;i++)
            {rewardFeedbackActive[i]=false;if(rewardFeedbackLabels!=null&&i<rewardFeedbackLabels.Length&&rewardFeedbackLabels[i]!=null)rewardFeedbackLabels[i].gameObject.SetActive(false);}
            if(rewardFeedbackRoot!=null)rewardFeedbackRoot.gameObject.SetActive(false);
            rewardFeedbackNext=0;
        }
        bool QueueRewardFeedback(ModelState before,ModelState after,RewardFeedbackOrigin origin)
        {
            var source=reelImages!=null&&reelImages.Length>1&&reelImages[1]!=null?reelImages[1].transform.position:transform.position;
            return QueueRewardFeedback(before,after,origin,source);
        }
        bool QueueRewardFeedback(ModelState before,ModelState after,RewardFeedbackOrigin origin,Vector3 source)
        {
            if(Session==null||before==null||after==null||before.pending!=null||after.pending!=null||before.sequence==long.MaxValue||after.sequence!=before.sequence+1||
                Session.State==null||Session.State.sequence!=after.sequence||Session.RecoveryRequired||failed||InBackground)return false;
            if(!ReferenceEquals(rewardFeedbackSession,Session))
            {ClearRewardFeedback();rewardFeedbackSession=Session;rewardFeedbackAcceptedSequence=-1;}
            if(after.sequence<=rewardFeedbackAcceptedSequence)return false;
            var receipt=ArcadeEconomy.Project(after).LastEvent;
            if(receipt==null||receipt.Sequence!=after.sequence||origin==RewardFeedbackOrigin.Spin&&receipt.Kind!="spin"||
                origin==RewardFeedbackOrigin.Arcade&&receipt.Kind!="claim"&&receipt.Kind!="buy")return false;
            // Consume even a zero/reduced receipt so later toggles and repeated
            // render hooks cannot turn a retained result into a new animation.
            rewardFeedbackAcceptedSequence=after.sequence;rewardFeedbackPending=null;
            if(rewardFeedbackReduced)return false;
            rewardFeedbackPending=new RewardFeedbackBatch(Session,rewardFeedbackGeneration,receipt,source);
            return true;
        }
        void RevealRewardFeedback(ModelState revealed)
        {
            var batch=rewardFeedbackPending;
            if(batch==null)return;
            if(!ReferenceEquals(batch.Session,Session)||batch.Generation!=rewardFeedbackGeneration||revealed==null||revealed.sequence!=batch.Sequence||
                Session.State==null||Session.State.sequence!=batch.Sequence||Session.RecoveryRequired||failed||InBackground||rewardFeedbackReduced)
            {rewardFeedbackPending=null;return;}
            if(Busy)return; // Normal spin presentation owns when reveal becomes legal.
            var receipt=ArcadeEconomy.Project(revealed).LastEvent;
            if(receipt==null||receipt.Sequence!=batch.Sequence||receipt.Kind!=batch.Kind||receipt.InstanceSequence!=batch.Instance)
            {rewardFeedbackPending=null;return;}
            rewardFeedbackPending=null;
            EmitRewardFeedback(batch.Credits,batch.Source,batch.Kind=="spin"?revealed.coins:(long?)null);
        }
        void EmitRewardFeedback(IReadOnlyList<RewardFeedbackItem> actualCredits,Vector3 sourceWorldPosition,long? spinWalletAfter=null)
        {
            bool spin=spinWalletAfter.HasValue;
            if(!Application.isPlaying||!isActiveAndEnabled||!rewardFeedbackCreated||rewardFeedbackReduced||Busy||failed||InBackground||actualCredits==null)return;
            Array.Clear(rewardFeedbackCurrencyUsed,0,rewardFeedbackCurrencyUsed.Length);
            int emitted=0;
            long coinCredit=0;
            for(int i=0;i<Math.Min(actualCredits.Count,RewardFeedbackCapacity)&&emitted<rewardFeedbackCurrencyUsed.Length;i++)
            {
                var item=actualCredits[i];int currency=(int)item.Currency;
                if(item.Amount<=0||currency<0||currency>=rewardFeedbackCurrencyUsed.Length||rewardFeedbackCurrencyUsed[currency])continue;
                rewardFeedbackCurrencyUsed[currency]=true;
                if(item.Currency==RewardFeedbackCurrency.Coins)coinCredit=item.Amount;
                int slot=rewardFeedbackNext;rewardFeedbackNext=(rewardFeedbackNext+1)%RewardFeedbackCapacity;
                var label=rewardFeedbackLabels[slot];var target=RewardFeedbackTarget(item.Currency);
                Vector2 start=(Vector2)rewardFeedbackRoot.InverseTransformPoint(sourceWorldPosition)-rewardFeedbackRoot.rect.center;
                start+=spin?SpinRewardLabelOffset(item.Currency):new Vector2((emitted%2==0?-1:1)*12,emitted*12);
                bool coinCaption=spin&&item.Currency==RewardFeedbackCurrency.Coins;
                Vector2 end=coinCaption?start+Vector2.up*24:
                    target!=null&&target.gameObject.activeInHierarchy?
                        (Vector2)rewardFeedbackRoot.InverseTransformPoint(target.TransformPoint(target.rect.center))-rewardFeedbackRoot.rect.center:
                        start+Vector2.up*65;
                rewardFeedbackArcs[slot]=coinCaption?0:28;
                rewardFeedbackStarts[slot]=start;rewardFeedbackEnds[slot]=end;rewardFeedbackAges[slot]=0;rewardFeedbackDelays[slot]=emitted*(spin?.055f:.035f);
                label.text=RewardFeedbackName(item.Currency)+" +"+item.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if(spin)label.outlineColor=ColorOf("142638");
                label.outlineWidth=spin?.19f:0;
                // The mobile SDF shader draws an outline only with this keyword enabled.
                if(spin)label.fontMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);else label.fontMaterial.DisableKeyword(ShaderUtilities.Keyword_Outline);
                var color=RewardFeedbackColor(item.Currency);if(rewardFeedbackDelays[slot]>0)color.a=0;
                label.color=color;label.rectTransform.anchoredPosition=start;label.gameObject.SetActive(true);rewardFeedbackActive[slot]=true;emitted++;
            }
            if(emitted==0)return;
            if(spin&&coinCredit>0)
            {
                BeginRewardImpact(sourceWorldPosition,coinCredit,spinWalletAfter.Value);
                if(SlotEffectsAllowed)RequestSlotHaptic(confirmation:true);
            }
            rewardFeedbackRoot.SetAsLastSibling();
            rewardFeedbackRoot.gameObject.SetActive(true);
            if(rewardFeedbackAnimation==null)rewardFeedbackAnimation=StartCoroutine(AnimateRewardFeedback());
        }
        Vector2 SpinRewardLabelOffset(RewardFeedbackCurrency currency)
        {
            float lane=rewardFeedbackRoot.rect.width*.25f;
            switch(currency)
            {
                case RewardFeedbackCurrency.Coins:return new Vector2(-lane,39);
                case RewardFeedbackCurrency.Diamonds:return new Vector2(-lane,17);
                case RewardFeedbackCurrency.Parts:return new Vector2(0,39);
                case RewardFeedbackCurrency.Potatoes:return new Vector2(0,17);
                case RewardFeedbackCurrency.Energy:return new Vector2(lane,39);
                default:return new Vector2(lane,17);
            }
        }
        RectTransform RewardFeedbackTarget(RewardFeedbackCurrency currency)
        {
            if(currency==RewardFeedbackCurrency.Coins)return rewardFeedbackCoinTarget;
            if(currency==RewardFeedbackCurrency.Diamonds)return rewardFeedbackDiamondTarget;
            if(currency==RewardFeedbackCurrency.Tickets)return rewardFeedbackTicketTarget;
            if(currency==RewardFeedbackCurrency.Energy&&productEnergyText!=null)return productEnergyText.rectTransform;
            return rewardFeedbackResourceTarget;
        }
        static string RewardFeedbackName(RewardFeedbackCurrency currency)
        {
            switch(currency)
            {
                case RewardFeedbackCurrency.Coins:return "Coins";
                case RewardFeedbackCurrency.Diamonds:return "Diamonds";
                case RewardFeedbackCurrency.Parts:return "Parts";
                case RewardFeedbackCurrency.Potatoes:return "Potatoes";
                case RewardFeedbackCurrency.Energy:return "Energy";
                default:return "Tickets";
            }
        }
        static Color RewardFeedbackColor(RewardFeedbackCurrency currency)
        {
            switch(currency)
            {
                case RewardFeedbackCurrency.Coins:return ColorOf("F4B85C");
                case RewardFeedbackCurrency.Diamonds:return ColorOf("ABD2D8");
                case RewardFeedbackCurrency.Parts:return ColorOf("B9CCBB");
                case RewardFeedbackCurrency.Potatoes:return ColorOf("C19B64");
                case RewardFeedbackCurrency.Energy:return ColorOf("A98CC7");
                default:return ColorOf("DDA7B6");
            }
        }
        IEnumerator AnimateRewardFeedback()
        {
            // Always yield before completion so StartCoroutine can publish its
            // handle even after a long frame; a completed handle must not stick.
            yield return null;
            while(true)
            {
                if(failed||InBackground||rewardFeedbackReduced)
                {ResetRewardFeedbackVisuals();rewardFeedbackAnimation=null;yield break;}
                // The Coin stream may finish over the next spin. Older receipt labels clear.
                bool any=AnimateRewardImpact(Time.unscaledDeltaTime);
                for(int i=0;i<RewardFeedbackCapacity;i++)
                {
                    if(!rewardFeedbackActive[i])continue;
                    rewardFeedbackAges[i]+=Time.unscaledDeltaTime;
                    float age=rewardFeedbackAges[i]-rewardFeedbackDelays[i];var label=rewardFeedbackLabels[i];
                    if(Busy||age>=RewardFeedbackLifetime){rewardFeedbackActive[i]=false;label.gameObject.SetActive(false);continue;}
                    any=true;if(age<0){var hidden=label.color;hidden.a=0;label.color=hidden;continue;}
                    float t=Mathf.Clamp01(age/RewardFeedbackLifetime);
                    // A flying label stays over the reels while it is read, then leaves as it fades; the Coin caption only rises.
                    float travel=rewardFeedbackArcs[i]>0?Mathf.Clamp01((t-.45f)/.55f):t,eased=travel*travel*(3-2*travel);
                    var point=Vector2.Lerp(rewardFeedbackStarts[i],rewardFeedbackEnds[i],eased);
                    point.y+=Mathf.Sin(travel*Mathf.PI)*rewardFeedbackArcs[i];
                    label.rectTransform.anchoredPosition=point;var color=label.color;
                    // Full strength while it is being read; fade only over the last 40%.
                    color.a=t<.6f?1:(1-t)/.4f;label.color=color;
                }
                if(!any){rewardFeedbackRoot.gameObject.SetActive(false);rewardFeedbackAnimation=null;yield break;}
                yield return null;
            }
        }
    }
}
