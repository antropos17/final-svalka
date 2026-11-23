using System;
using System.Linq;
using JunkToNeon.Core;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Guidance;
using JunkToNeon.Orders;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        const int JourneyGuide=9,JourneyOrders=10,JourneyOrder=11,JourneyCancel=12;
        [SerializeField] Button guideButton;
        bool journeyLayoutCaptured,orderLayoutCaptured;
        Vector2 originalHeadingMax,orderSelectMin,orderSelectMax,orderPreviousMin,orderPreviousMax;
        bool orderSelectAutoSize;
        float orderSelectFont,orderSelectFontMin,orderSelectFontMax;
        bool guidePreviewObserved,guideImprovementObserved;
        int guidePreviewFrame=-1,guideCompletionFrame=-1;
        int guidePreviewObservedFrame=-1,guideImprovementObservedFrame=-1;
        long guideVisibleCompletion;
        GameSession guideSession;
        GuideState guideRenderProgress;
        bool guideRenderBlocked;
        string guideRenderCompletionText;
        ProjectCompletion narrativePresentedCompletion;
        string narrativePresentedText;
        string headingKeptDuringSpin;
        string shownGuide,shownOrder;
        long shownCompletion,journeyQuoteSequence,shownOrderInstance;
        int TutorialFacts { get { return (guidePreviewObserved&&Time.frameCount>guidePreviewObservedFrame?(int)TutorialSteps.Previewed:0)|(guideImprovementObserved&&Time.frameCount>guideImprovementObservedFrame?(int)TutorialSteps.VisibleImprovement:0); } }

        public void CreateJourneyUi()
        {
            if(Application.isPlaying&&!journeyLayoutCaptured){originalHeadingMax=heading.rectTransform.anchorMax;journeyLayoutCaptured=true;}
            if(guideButton==null)guideButton=Button("Progress guidance","Hint",world,new Vector2(.71f,.56f),new Vector2(.94f,.65f),ColorOf("F2EAD2"),ShowGuidance);
            else guideButton.onClick.AddListener(ShowGuidance);
            AddSectionConsent(guideButton);FitChapterLabel(guideButton);guideButton.gameObject.SetActive(false);
            if(Application.isPlaying){Canvas.willRenderCanvases-=ObserveGuideCanvas;Canvas.willRenderCanvases+=ObserveGuideCanvas;}
        }
        void ResetJourneyActionLayout()
        {
            if(!Application.isPlaying)return;
            var s=selectButton.GetComponent<RectTransform>();var p=chapterPrevious.GetComponent<RectTransform>();var label=selectButton.GetComponentInChildren<TMP_Text>();
            // Capture current authored values once, immediately before the first
            // temporary panel layout. No serialized cache can override later edits.
            if(!orderLayoutCaptured)
            {
                orderSelectMin=s.anchorMin;orderSelectMax=s.anchorMax;orderPreviousMin=p.anchorMin;orderPreviousMax=p.anchorMax;
                orderSelectAutoSize=label.enableAutoSizing;orderSelectFont=label.fontSize;orderSelectFontMin=label.fontSizeMin;orderSelectFontMax=label.fontSizeMax;orderLayoutCaptured=true;
            }
            s.anchorMin=orderSelectMin;s.anchorMax=orderSelectMax;p.anchorMin=orderPreviousMin;p.anchorMax=orderPreviousMax;
            label.enableAutoSizing=orderSelectAutoSize;label.fontSize=orderSelectFont;label.fontSizeMin=orderSelectFontMin;label.fontSizeMax=orderSelectFontMax;
        }
        void RenderJourney(ModelState state)
        {
            var liveSnapshot=Session?.State;
            if(liveSnapshot==null||guideButton==null){guideRenderProgress=null;guideRenderCompletionText=null;return;}
            if(!ReferenceEquals(guideSession,Session))
            {
                guideSession=Session;narrativePresentedCompletion=null;narrativePresentedText=headingKeptDuringSpin=null;shownGuide=null;shownCompletion=guideVisibleCompletion=0;
                guidePreviewObserved=guideImprovementObserved=false;
                guidePreviewFrame=guideCompletionFrame=guidePreviewObservedFrame=guideImprovementObservedFrame=-1;
            }
            var journeyView=JtnJourneyView.FromSnapshot(liveSnapshot);
            // Canvas observation reads only these immutable facts from the latest render.
            guideRenderProgress=journeyView.Progress;
            guideRenderBlocked=journeyView.TransitionPending||Session.RecoveryRequired;
            guideRenderCompletionText=null;
            var guideProgress=guideRenderProgress;
            if(narrativePresentedCompletion!=null&&(guideProgress.LastCompletion==null||
                guideProgress.CompletionAcknowledgedSequence>=narrativePresentedCompletion.Sequence||
                guideProgress.LastCompletion.Sequence<narrativePresentedCompletion.Sequence||
                guideProgress.LastCompletion.Sequence==narrativePresentedCompletion.Sequence&&guideProgress.LastCompletion.ProjectId!=narrativePresentedCompletion.ProjectId))
            {narrativePresentedCompletion=null;narrativePresentedText=null;}
            if(Busy)
            {
                // With the product chrome a visible hint button stays in place, only disabled, so nothing blinks on a spin.
                if(productChrome==null)guideButton.gameObject.SetActive(false);else guideButton.interactable=false;
                if(productChrome==null)heading.rectTransform.anchorMax=originalHeadingMax;
                // Advance this immutable identity only after ordinary presentation.
                // A resize/pause Render may receive the committed after-state while
                // Busy; it must not reveal the new completion ahead of its reels.
                if(narrativePresentedCompletion!=null)heading.text=narrativePresentedText??InlineCompletion(narrativePresentedCompletion.ProjectId);
                // Keep the last presented hint or summary; an empty or switching band reads as a glitch.
                else if(productChrome!=null)heading.text=headingKeptDuringSpin??"";
                guideCompletionFrame=-1;
                return;
            }
            var evaluation=journeyView.Evaluation;var progress=journeyView.Progress;
            bool completion=evaluation.LastCompletion!=null&&progress.CompletionAcknowledgedSequence!=evaluation.LastCompletion.Sequence;
            if(!completion||narrativePresentedCompletion!=null&&
                (narrativePresentedCompletion.Sequence!=evaluation.LastCompletion.Sequence||narrativePresentedCompletion.ProjectId!=evaluation.LastCompletion.ProjectId))
            {narrativePresentedCompletion=null;narrativePresentedText=null;}
            bool expanded=evaluation.Suggestion?.HintId==GuideIds.ExpandedR7;
            shownGuide=expanded?GuideIds.ExpandedR7:completion?GuideIds.Completion:evaluation.Suggestion?.HintId;
            shownCompletion=shownGuide==GuideIds.Completion?evaluation.LastCompletion.Sequence:0;
            bool visible=shownGuide!=null;
            guideButton.gameObject.SetActive(visible);
            guideButton.interactable=!Busy&&!failed&&!InBackground&&!journeyView.TransitionPending;
            // ProductUi owns the same heading's dedicated band and typography.
            if(productChrome==null)
            {
                heading.rectTransform.anchorMax=visible?new Vector2(Mathf.Min(.68f,originalHeadingMax.x),originalHeadingMax.y):originalHeadingMax;
                heading.enableAutoSizing=true;heading.fontSizeMin=12;heading.fontSizeMax=20;
            }
            var chapter=ChapterEconomy.Project(liveSnapshot);
            string kept=EraProgressSummary(chapter);
            if(shownGuide==GuideIds.Completion)
            {
                string payoff=FinalCompletionPayoff(chapter,evaluation.LastCompletion);
                heading.text=payoff.Length==0?InlineCompletion(evaluation.LastCompletion.ProjectId):payoff+"\n"+ProjectName(evaluation.LastCompletion.ProjectId)+": "+CompletionBenefit(evaluation.LastCompletion.ProjectId);
            }
            else if(visible)heading.text=kept=GuideTitle(shownGuide);
            else if(productChrome!=null)heading.text=kept;
            // A presented completion is retained separately; otherwise a spin keeps exactly this line.
            headingKeptDuringSpin=kept;
            if(shownGuide==GuideIds.Completion)guideRenderCompletionText=heading.text;
        }
        bool GuideHeadingVisible=>heading!=null&&heading.isActiveAndEnabled&&heading.canvas!=null&&heading.canvas.isActiveAndEnabled&&
            !heading.canvasRenderer.cull&&heading.color.a>0&&heading.canvasRenderer.GetAlpha()>0&&heading.canvasRenderer.GetInheritedAlpha()>0&&
            heading.rectTransform.rect.width>0&&heading.rectTransform.rect.height>0&&heading.maxVisibleCharacters>0&&heading.maxVisibleLines>0&&heading.maxVisibleWords>0&&
            heading.textInfo!=null&&heading.textInfo.characterCount>0;
        void ObserveGuideCanvas()
        {
            if(!isActiveAndEnabled||Busy||InBackground||failed||Session==null||!ReferenceEquals(guideSession,Session)||guideRenderProgress==null||guideRenderBlocked||
                offlineSummaryPanel||autoHintPanel||exitPanel)
            {guideCompletionFrame=-1;return;}
            if(guidePreviewFrame>=0&&Time.frameCount>guidePreviewFrame&&panel.activeInHierarchy&&wavePanel==ChapterFamily)
            {if(!guidePreviewObserved){guidePreviewObserved=true;guidePreviewObservedFrame=Time.frameCount;}guidePreviewFrame=-1;}
            var progress=guideRenderProgress;var completion=progress.LastCompletion;
            if(completion==null||progress.CompletionAcknowledgedSequence==completion.Sequence)
            {
                narrativePresentedCompletion=null;narrativePresentedText=null;guideCompletionFrame=-1;guideVisibleCompletion=0;return;
            }
            if(narrativePresentedCompletion!=null&&(narrativePresentedCompletion.Sequence!=completion.Sequence||narrativePresentedCompletion.ProjectId!=completion.ProjectId))
            {narrativePresentedCompletion=null;narrativePresentedText=null;}
            if(panel.activeInHierarchy||shownGuide!=GuideIds.Completion||shownCompletion!=completion.Sequence||!GuideHeadingVisible)
            {guideCompletionFrame=-1;return;}
            if(guideImprovementObserved&&narrativePresentedCompletion!=null)return;
            if(guideRenderCompletionText==null||heading.text!=guideRenderCompletionText){guideCompletionFrame=-1;return;}
            // Cache only the completion projected on this uncovered surface. No save is written.
            narrativePresentedCompletion=completion;narrativePresentedText=guideRenderCompletionText;
            if(guideImprovementObserved)return;
            if(guideCompletionFrame<0||guideVisibleCompletion!=completion.Sequence)
            {guideVisibleCompletion=completion.Sequence;guideCompletionFrame=Time.frameCount;return;}
            if(Time.frameCount>guideCompletionFrame)
            {guideImprovementObserved=true;guideImprovementObservedFrame=Time.frameCount;guideCompletionFrame=-1;}
        }
        void ObservePreviewRequest(){guidePreviewFrame=Time.frameCount;}
        static string CompletionBenefit(string project)
        { ChapterCatalog.Rules.TryGetUpgrade(project,out var upgrade);return upgrade==null?"Building complete.":Benefit(upgrade); }
        static string InlineCompletion(string project)
        {
            ChapterCatalog.Rules.TryGetUpgrade(project,out var u);string effect=project==GameIds.HouseImprove?"A step toward the next era.":"New stage.";
            if(u!=null&&u.Effects.Count!=0)
            {
                var e=u.Effects[0];
                effect=e.Kind==ContentEffectKind.UnlockAuto?"AUTO unlocked.":e.Kind==ContentEffectKind.SpinCoins?"+1 Coin per future spin.":
                    e.Kind==ContentEffectKind.TrashSymbolCoins?"+1 Coin per Trash symbol.":e.Kind==ContentEffectKind.PigeonDeliveryCoins?"+3 Coins per delivery.":$"+1 Coin per {new[]{"Potato","Box","Trash","Pigeon"}[e.SymbolId.Value]} symbol.";
            }
            return ProjectName(project)+": "+(ChapterNarrative.TryCompletion(project,out var beat)?beat.Title:"Done.")+" "+effect;
        }
        string GuideTitle(string id)
        {
            switch(id)
            {
                case GuideIds.Welcome:return "Inspect the house. Viewing is free.";
                case GuideIds.ChooseProject:return Session.Chapter.SelectedProjectId==null?"Choose one goal for your Coins.":"Goal selected. Press SPIN.";
                case GuideIds.FirstRepair:
                    switch(FirstRepairGuideMode())
                    {
                        case FirstRepairGuidanceMode.EnterWorkshopYard:return "Junkyard complete. Enter Workshop Yard.";
                        case FirstRepairGuidanceMode.ChapterComplete:return "Chapter complete. Continue if you wish.";
                        default:return "AUTO unlocked. Choose the next goal yourself.";
                    }
                case GuideIds.Skills:return "Skills are optional. Compare their benefits first.";
                case GuideIds.EraReady:return "All six buildings complete. Unlock Workshop Yard.";
                case GuideIds.ExpandedR7:return "Progress retained. The chapter has expanded.";
                case GuideIds.ChapterEnd:return ChapterNarrative.CompletionPayoff+" Continue if you wish.";
                case GuideIds.Orders:
                    switch(OrderGuideMode())
                    {
                        case OrderGuidanceMode.Claim:return "Order ready. Collect the reward.";
                        case OrderGuidanceMode.Continue:return "Order in progress. Keep making ordinary spins.";
                        default:return "Orders are optional. Choose one.";
                    }
                default:return "Repeatable orders are available after the chapter.";
            }
        }
        string GuideBody(string id)
        {
            var chapter=Session.Chapter;
            bool workshopAvailable=chapter.Progress.EraId==EraIds.Junkyard&&chapter.Progress.CompletedUpgradeIds.Contains(GameIds.HouseRepair)&&!chapter.Progress.CompletedUpgradeIds.Contains(GameIds.WorkshopBuild);
            string workshopChoice=workshopAvailable?$"\nInspect Workshop: {ProjectPrice(GameIds.WorkshopBuild)} Coins, +{EconomyRules.WorkshopBonus} per future spin. This building is required.\nOffline: +{60/EconomyRules.OfflineSecondsPerCoin} Coin/full min., up to {EconomyRules.OfflineCapSeconds/EconomyRules.OfflineSecondsPerCoin} per absence ({EconomyRules.OfflineCapSeconds/3600}h).":"";
            if(workshopAvailable&&!chapter.Progress.CompletedUpgradeIds.Contains(GameIds.HouseImprove))workshopChoice+=$"\nHouse upgrade: new selection {ProjectPrice(GameIds.HouseImprove)} Coins; required for the next era, no income increase.";
            if(workshopAvailable&&chapter.SelectedProjectId!=null)workshopChoice+=$"\nSaved goal: {ProjectName(chapter.SelectedProjectId)} · {ChapterEconomy.SelectedProjectPrice(chapter)} Coins. Inspection does not change it.";
            switch(id)
            {
                case GuideIds.Completion:
                    string project=Session.Guidance.LastCompletion.ProjectId;
                    string story=ChapterNarrative.TryCompletion(project,out var beat)?beat.Body+"\n":"";
                    var progress=Session.Chapter.Progress;
                    string payoff=FinalCompletionPayoff(chapter,Session.Guidance.LastCompletion);
                    string next=EraProgressSummary(chapter)+(ChapterCatalog.Rules.IsChapterComplete(progress)?" Orders and remaining skills are optional.":" Choose the next goal yourself.");
                    if(payoff.Length>0)next=payoff+"\n"+next;
                    return story+"Complete: "+ProjectName(project)+".\n"+CompletionBenefit(project)+"\n"+next+(project==GameIds.HouseRepair?workshopChoice:"");
                case GuideIds.Welcome:return (Session.Journey.Guidance.Origin==GuideOrigin.Fresh?ChapterNarrative.Opening+"\n":"")+"Starting Coins are saved. Open the house to see its price. Viewing and closing do not select or buy a project.";
                case GuideIds.ChooseProject:
                    var target=Session.Chapter;
                    if(target.SelectedProjectId==null)return "Choose a project explicitly. Future Coins fund that one goal. It pays once when you have enough; the rest stays in your wallet.";
                    long price=ChapterEconomy.SelectedProjectPrice(target),wallet=Session.State.coins;
                    return $"Goal selected: {ProjectName(target.SelectedProjectId)}.\nSaved price: {price} Coins.\nWallet: {wallet}; still needed: {Math.Max(0,price-wallet)}.\nPress SPIN. The goal pays once when funded. Keep the rest; choose the next goal yourself.";
                case GuideIds.FirstRepair:
                    switch(FirstRepairGuideMode())
                    {
                        case FirstRepairGuidanceMode.EnterWorkshopYard:return "House repaired; all six Junkyard buildings complete. Enter Workshop Yard for free in Buildings. Coins, skills and effects remain. Choose the next goal yourself.";
                        case FirstRepairGuidanceMode.ChapterComplete:return "Both eras complete. Stop here or spin manually. Skills and orders are optional; there is no further era.";
                        default:return "Repair unlocks AUTO. STOP prevents new spins. Choose each new building yourself; no extra projects are bought."+workshopChoice;
                    }
                case GuideIds.Skills:return "Three skill branches show price, benefit and remaining Coins before purchase. Skills do not change odds and are optional for chapter completion.";
                case GuideIds.EraReady:return ChapterNarrative.EraReady+"\n"+"All six Junkyard buildings reached stage 2. Era entry is free. Coins, skills, effects and Pigeon carryover remain.";
                case GuideIds.ExpandedR7:return ChapterCatalog.Rules.IsChapterComplete(Session.Chapter.Progress)?"Previous progress and Coins retained. Both eras of the expanded chapter are complete. Orders and remaining skills are optional; no repeat starting reward.":"You completed the old four-building version. Progress and Coins remain. The expanded chapter adds stages, Cart, Power Source and a second era. Complete them separately; no repeat starting reward.";
                case GuideIds.ChapterEnd:return ChapterEndMilestones();
                case GuideIds.Orders:
                    var orders=Session.Journey.Orders;
                    switch(OrderGuideMode())
                    {
                        case OrderGuidanceMode.Claim:return $"Progress: {orders.Progress}/{OrderCatalog.Get(orders.ActiveOrderId).Target}. Open the current order and press Collect reward. Rewards are not automatic. Further orders are optional.";
                        case OrderGuidanceMode.Continue:return $"Progress: {orders.Progress}/{OrderCatalog.Get(orders.ActiveOrderId).Target}. Make ordinary spins for this order. Finish or explicitly cancel it before taking another. Orders are optional.";
                        default:return "Choose one order. Only new ordinary spins count. Collect the reward explicitly when ready. Then you may take the same type again. Canceling loses progress with no reward. No deadline or entry fee.";
                    }
                default:return "Choose one order. Only new ordinary spins count. Collect the reward explicitly when ready. Then you may take the same type again. Canceling loses progress with no reward. No deadline or entry fee.";
            }
        }
        OrderGuidanceMode OrderGuideMode()
        { return GuideFlow.OrderMode(JourneyEconomy.GuideContext(Session.Chapter,Session.Journey)); }
        FirstRepairGuidanceMode FirstRepairGuideMode()
        { return GuideFlow.FirstRepairMode(JourneyEconomy.GuideContext(Session.Chapter,Session.Journey)); }
        void ShowGuidance()
        {
            if(!CanOpenChapterPanel()||panel.activeSelf||shownGuide==null||guideButton==null||
                !guideButton.isActiveAndEnabled||!guideButton.IsInteractable()||!PointerConsentAllows(guideButton))return;
            string id=shownGuide;long completion=shownCompletion;
            BeginChapterPanel(JourneyGuide,id==GuideIds.Completion?"Building complete":"Progress hint");
            shownGuide=id;shownCompletion=completion;journeyQuoteSequence=Session.State.sequence+1;
            panelBody.text=GuideBody(id);selectButton.GetComponentInChildren<TMP_Text>().text="Got it";selectButton.gameObject.SetActive(true);
        }
        void ShowOrders()
        {
            ShowDirectOrders();
        }
        bool OpenJourneyChoice(int index)
        {
            if(wavePanel!=JourneyOrders)return false;
            if(index==2)
            { BeginChapterPanel(JourneyGuide,"Chapter complete");shownGuide=null;panelBody.text=GuideBody(GuideIds.ChapterEnd);return true; }
            ShowOrder(index==0?"order.sort-salvage":"order.neighborhood-supply");return true;
        }
        void ShowOrder(string id)
        {
            var order=OrderCatalog.Get(id);var state=Session.Journey.Orders;
            BeginChapterPanel(JourneyOrder,id=="order.sort-salvage"?"Sort Trash":"Neighborhood Delivery");
            shownOrder=id;shownOrderInstance=state.InstanceSequence;journeyQuoteSequence=Session.State.sequence+1;
            bool mine=state.ActiveOrderId==id;
            PresentNavigationOrder(id);
            selectButton.GetComponentInChildren<TMP_Text>().text=mine?"Collect reward":"Accept order";
            selectButton.gameObject.SetActive(state.ActiveOrderId==null||mine&&state.Ready);
            if(state.ActiveOrderId!=null)
            {
                chapterPrevious.gameObject.SetActive(true);chapterPrevious.GetComponentInChildren<TMP_Text>().text="Cancel current";
                var s=selectButton.GetComponent<RectTransform>();var p=chapterPrevious.GetComponent<RectTransform>();
                float middle=(orderSelectMin.x+orderSelectMax.x)*.5f,gap=Mathf.Min(.04f,(orderSelectMax.x-orderSelectMin.x)*.1f);
                s.anchorMin=new Vector2(middle+gap*.5f,orderSelectMin.y);s.anchorMax=orderSelectMax;
                p.anchorMin=orderSelectMin;p.anchorMax=new Vector2(middle-gap*.5f,orderSelectMax.y);FitChapterLabel(selectButton);
            }
        }
        bool PreviousJourneyPage()
        {
            if(wavePanel!=JourneyOrder)return false;
            var order=Session.Journey.Orders;
            if(order.ActiveOrderId==null)return true;
            bool returnToOrders=NavigationReturnsToOrders();
            BeginChapterPanel(JourneyCancel,"Cancel order?");shownOrder=order.ActiveOrderId;shownOrderInstance=order.InstanceSequence;journeyQuoteSequence=Session.State.sequence+1;
            var definition=OrderCatalog.Get(order.ActiveOrderId);
            panelBody.text=$"Current order: {NavigationOrderTitle(order.ActiveOrderId)}.\nProgress: {order.Progress}/{definition.Target} {NavigationOrderUnit(order.ActiveOrderId)}.\n"+
                (order.Ready?$"Unclaimed reward: {definition.RewardCoins} Coins. Canceling forfeits it. Wallet Coins are not spent.\n":"Progress will be lost. Canceling gives no reward.\n")+
                "You may start a new instance from zero later.";
            selectButton.GetComponentInChildren<TMP_Text>().text="Cancel without reward";selectButton.gameObject.SetActive(true);
            if(returnToOrders)RememberNavigationOrderCancel();return true;
        }
        bool SelectJourneyPanel()
        {
            if(IsDirectNavigationPanel()||IsExpansionPanel()||IsArcadePanel())return true;
            if(wavePanel<JourneyGuide)return false;
            int mode=wavePanel;
            if(mode==JourneyOrders)return true;
            try
            {
                var s=Session.State;
                if(journeyQuoteSequence!=s.sequence+1){HideWaveChoices();panel.SetActive(false);Render("State changed. Reopen the offer.");return true;}
                bool claimingOrder=false;
                if(mode==JourneyGuide)
                { if(shownGuide==null)return true;Session.AcknowledgeGuidance(shownGuide,journeyQuoteSequence,shownCompletion,UtcSeconds()); }
                else if(mode==JourneyCancel)Session.CancelOrder(shownOrder,shownOrderInstance,journeyQuoteSequence,UtcSeconds());
                else if(mode==JourneyOrder)
                {
                    var o=Session.Journey.Orders;
                    if(o.ActiveOrderId==null)Session.AcceptOrder(shownOrder,journeyQuoteSequence,UtcSeconds());
                    else {claimingOrder=true;Session.ClaimOrder(shownOrder,shownOrderInstance,journeyQuoteSequence,UtcSeconds());}
                }
                var committed=Session.State;
                ObserveCommitted(s,committed);
                string result="Action saved.";
                if(committed.sequence>s.sequence)
                {
                    if(mode==JourneyCancel)result="Order canceled without reward.";
                    else if(mode==JourneyOrder)
                    {
                        var orders=JourneyEconomy.Project(committed).Orders;var claim=orders.LatestClaim;
                        if(claimingOrder&&claim!=null&&claim.ClaimSequence==committed.sequence&&claim.InstanceSequence==shownOrderInstance&&claim.OrderId==shownOrder)
                        {
                            result=$"Order complete: +{claim.RewardCoins} Coins. "+(ChapterNarrative.OrderClaimed(claim.OrderId)??"Continue if you wish.");
                            ShowClaimedOrders(claim,ChapterEconomy.Project(committed));
                            Render(result);return true;
                        }
                        else if(orders.ActiveOrderId==shownOrder&&orders.InstanceSequence==committed.sequence)
                            result=$"Order accepted: {orders.Progress}/{OrderCatalog.Get(shownOrder).Target}. Only new qualifying results count.";
                    }
                }
                HideWaveChoices();panel.SetActive(false);Render(result);
            }
            catch(Exception error)
            {if(Session?.RecoveryRequired==true){ObserveOperationFailure();Error(error.Message);}else{HideWaveChoices();panel.SetActive(false);Render("Offer unavailable. Progress retained.");}}
            return true;
        }
    }
}
