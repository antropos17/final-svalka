using System;
using System.Collections;
using System.Linq;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        const int ChapterHome=1,ChapterFamilies=2,ChapterBranches=3,ChapterFamily=4,ChapterSkill=5,ChapterEra=6,ChapterTransition=7,ChapterTiers=8;
        [SerializeField] Button chapterPrevious,chapterNext;
        [SerializeField] Image cartDraft,powerDraft;
        [SerializeField] TMP_Text chapterBanner;
        [SerializeField] RectTransform goalProgressTrack,goalProgressFill;
        [SerializeField] Button[] chapterWorldButtons=new Button[6];
        [SerializeField] bool chapterUiAuthored;
        int chapterPage,chapterBranch;
        string chapterFamily,chapterProject;
        SkillQuote chapterQuote;

        public void CreateChapterUi()
        {
            if(chapterBanner==null)chapterBanner=GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.text=="01 / НОВОЕ НАЧАЛО"||t.text=="01 / A NEW START");
            if(!chapterUiAuthored)
            {
                waveStatus.rectTransform.anchorMin=new Vector2(.06f,.75f);waveStatus.rectTransform.anchorMax=new Vector2(.94f,.785f);
                for(int i=0;i<waveChoices.Length;i++)
                {
                    var r=waveChoices[i].GetComponent<RectTransform>();r.anchorMin=new Vector2(.07f,.60f-i*.14f);r.anchorMax=new Vector2(.93f,.70f-i*.14f);
                    FitChapterLabel(waveChoices[i]);
                }
                chapterUiAuthored=true;
            }
            if(chapterPrevious==null)chapterPrevious=Button("Previous chapter page","Previous",panel.transform,new Vector2(.07f,.18f),new Vector2(.48f,.29f),ColorOf("B9CCBB"),PreviousChapterPage);
            else chapterPrevious.onClick.AddListener(PreviousChapterPage);
            if(chapterNext==null)chapterNext=Button("Next chapter page","Next",panel.transform,new Vector2(.52f,.18f),new Vector2(.93f,.29f),ColorOf("B9CCBB"),NextChapterPage);
            else chapterNext.onClick.AddListener(NextChapterPage);
            FitChapterLabel(chapterPrevious);FitChapterLabel(chapterNext);HideChapterNavigation();
            if(cartDraft==null)cartDraft=ChapterDraft("Cart draft","CART",.14f);
            if(powerDraft==null)powerDraft=ChapterDraft("Power source draft","ENERGY",.29f);
            var images=new[]{house,workshopDraft,recyclerDraft,pigeonDraft,cartDraft,powerDraft};
            if(chapterWorldButtons==null||chapterWorldButtons.Length!=6)chapterWorldButtons=new Button[6];
            for(int i=0;i<6;i++)
            {
                string family=FamilyIds.All[i];var image=images[i];image.raycastTarget=true;
                if(chapterWorldButtons[i]==null)chapterWorldButtons[i]=image.GetComponent<Button>()??image.gameObject.AddComponent<Button>();
                chapterWorldButtons[i].targetGraphic=image;chapterWorldButtons[i].onClick.AddListener(()=>ShowChapterFamily(family));
                // Busy disables inspection; the building itself must not fade on every spin.
                var colors=chapterWorldButtons[i].colors;colors.disabledColor=Color.white;chapterWorldButtons[i].colors=colors;
            }
            CreateChapterHudUi();
        }
        public void CreateChapterHudUi()
        {
            if(goalProgressTrack==null)
                goalProgressTrack=Box("Selected goal progress",stageLabel.transform,Vector2.zero,new Vector2(1,.08f),ColorOf("B9CCBB"));
            if(goalProgressFill==null)
                goalProgressFill=Box("Saved price progress",goalProgressTrack,Vector2.zero,Vector2.one,ColorOf("F4B85C"));
            goalProgressTrack.gameObject.SetActive(false);
        }
        Image ChapterDraft(string name,string label,float x)
        {
            float ground=house.rectTransform.anchorMin.y+.14f;
            var r=Box(name,world,new Vector2(x,ground),new Vector2(x,ground),ColorOf("8B9588"));r.pivot=new Vector2(.5f,0);r.sizeDelta=new Vector2(45,40);
            Label(label,r,new Vector2(0,.2f),new Vector2(1,.8f),7,ColorOf("203A30"),TextAlignmentOptions.Center);return r.GetComponent<Image>();
        }
        static void FitChapterLabel(Button button)
        { var t=button.GetComponentInChildren<TMP_Text>();t.enableAutoSizing=true;t.fontSizeMin=8;t.fontSizeMax=14;t.fontSize=14; }
        void HideChapterNavigation()
        { if(chapterPrevious!=null)chapterPrevious.gameObject.SetActive(false);if(chapterNext!=null)chapterNext.gameObject.SetActive(false); }
        bool CanOpenChapterPanel() { return !Busy&&!failed&&!InBackground&&Session?.State!=null; }
        void BeginChapterPanel(int mode,string title)
        {
            StopAuto();HideWaveChoices();wavePanel=mode;exitPanel=false;autoHintPanel=false;offlineSummaryPanel=false;
            panelTitle.text=title;panelBody.text="";selectButton.gameObject.SetActive(false);reloadButton.gameObject.SetActive(false);closeButton.gameObject.SetActive(true);panel.SetActive(true);
            RefreshProductNavigation();
        }
        void ChoiceLabels(params string[] labels)
        {
            for(int i=0;i<waveChoices.Length;i++)
            { waveChoices[i].gameObject.SetActive(i<labels.Length);if(i<labels.Length)waveChoices[i].GetComponentInChildren<TMP_Text>().text=labels[i]; }
        }
        void ShowChapterHome()
        {
            if(!CanOpenChapterPanel()||panel.activeSelf)return;
            if(ShowChapterTransition())return;
            ShowDirectConstruction();
        }
        void OpenChapterChoice(int index)
        {
            if(!CanOpenChapterPanel())return;
            if(OpenJourneyChoice(index))return;
            if(wavePanel==ChapterHome)
            { if(index==0){chapterPage=0;ShowChapterFamilies();}else if(index==1)ShowChapterBranches();else ShowChapterEra(); }
            else if(wavePanel==ChapterFamilies)ShowChapterFamily(FamilyIds.All[chapterPage*3+index],true);
            else if(wavePanel==ChapterBranches){chapterBranch=index;ShowChapterTiers();}
            else if(wavePanel==ChapterTiers){if(index==2)ShowChapterBranches();else ShowChapterSkill(SkillTree.Nodes[chapterBranch*2+index].Id);}
        }
        void ShowChapterFamilies()
        {
            var c=Session.Chapter;var stages=ChapterCatalog.Rules.Stages(c.Progress);
            BeginChapterPanel(ChapterFamilies,$"Buildings · {chapterPage+1}/2");
            chapterPrevious.GetComponentInChildren<TMP_Text>().text="Previous";chapterNext.GetComponentInChildren<TMP_Text>().text="Next";
            ChoiceLabels(Enumerable.Range(chapterPage*3,3).Select(i=>FamilyName(FamilyIds.All[i])+" · "+stages.LocalLevel(FamilyIds.All[i])+"/2").ToArray());
            chapterPrevious.gameObject.SetActive(chapterPage>0);chapterNext.gameObject.SetActive(chapterPage<1);
        }
        void PreviousChapterPage(){if(PageSectionRules(-1))return;if(OpenProgressClearGoal())return;if(PreviousJourneyPage())return;if(wavePanel==ChapterFamilies&&chapterPage>0){chapterPage--;ShowChapterFamilies();}}
        void NextChapterPage(){if(PageSectionRules(1))return;if(OpenProgressGoal())return;if(wavePanel==ChapterFamilies&&chapterPage<1){chapterPage++;ShowChapterFamilies();}}
        void ShowChapterBranches()
        { BeginChapterPanel(ChapterBranches,"Skill tree");ChoiceLabels("All Coins · I → II","Recycling · I → II","Deliveries · I → II"); }
        void ShowChapterTiers()
        {
            var skills=Session.Chapter.Skills;
            BeginChapterPanel(ChapterTiers,new[]{"All Coins","Recycling","Deliveries"}[chapterBranch]);
            var first=SkillTree.Nodes[chapterBranch*2];var next=SkillTree.Nodes[chapterBranch*2+1];
            ChoiceLabels($"I · {(skills.Owns(first.Id)?"owned":first.Price+" Coins")}",$"II · {(skills.Owns(next.Id)?"owned":next.Price+" Coins")}","All branches");
        }
        public void ShowChapterFamily(string family,bool fromBrowser=false)
        {
            if(!CanOpenChapterPanel()||(!fromBrowser&&panel.activeSelf))return;
            if(ShowChapterTransition())return;
            var c=Session.Chapter;var view=ChapterCatalog.Rules.FamilyStage(c.Progress,family);chapterFamily=family;
            BeginChapterPanel(ChapterFamily,FamilyName(family));
            ObservePreviewRequest();
            int level=view.Stage.Level;
            if(level==2)
            {
                chapterProject=null;
                string completed=ChapterIds.Project(c.Progress.EraId,family,2);
                string story=JunkToNeon.Guidance.ChapterNarrative.TryCompletion(completed,out var beat)?beat.Title+"\n":"";
                panelBody.text=$"{EraName(c.Progress.EraId)} · stage 2/2.\n"+story+OwnedConstructionBenefits(c,family)+"\n"+
                    (ChapterCatalog.Rules.IsChapterComplete(c.Progress)?"Chapter complete. Skills and orders are optional.":
                        ChapterCatalog.Rules.CanUnlockWorkshopYard(c.Progress)?"Junkyard complete. Enter Workshop Yard for free before choosing your next project.":"Choose the next goal yourself.");
                ShowYardFamilyActivity(family);
                return;
            }
            chapterProject=ChapterIds.Project(c.Progress.EraId,family,level+1);ChapterCatalog.Rules.TryGetUpgrade(chapterProject,out var upgrade);
            long offerPrice=ProjectPrice(chapterProject);
            bool selected=c.SelectedProjectId==chapterProject;
            long savedPrice=selected?ChapterEconomy.SelectedProjectPrice(c):0,coins=Session.State.coins;
            bool reprice=selected&&savedPrice!=offerPrice,ready=selected&&coins>=offerPrice;
            string prices=reprice?$"Saved goal: {savedPrice} Coins. Price: {offerPrice} Coins.":$"Price: {offerPrice} Coins.";
            string consequence=coins>=offerPrice?$"Builds now. Pay {offerPrice}: {coins} → {coins-offerPrice} Coins.":$"Select without spending: {coins} → {coins} Coins. Need {offerPrice-coins} more.";
            const string laterPayment="Pays once on a spin or selection when you have enough Coins.";
            string action=reprice?consequence:selected&&!ready?$"Goal selected. Need {savedPrice-coins} Coins more. No spending now. "+laterPayment:
                (c.SelectedProjectId!=null&&!selected?"Replaces the current goal. ":"")+consequence+(coins<offerPrice?" "+laterPayment:"");
            string benefit=Benefit(upgrade);
            if(chapterProject==GameIds.WorkshopBuild)
                benefit+=$"\nOffline: +{60/EconomyRules.OfflineSecondsPerCoin} Coin/full min.; up to {EconomyRules.OfflineCapSeconds/EconomyRules.OfflineSecondsPerCoin} per absence ({EconomyRules.OfflineCapSeconds/3600}h). Credited on return. Partial minutes do not carry between returns. Later upgrades do not raise this income.";
            string purpose=JunkToNeon.Guidance.ChapterNarrative.FamilyOverview(family);
            panelBody.text=$"{EraName(c.Progress.EraId)} · stage {level} → {level+1}\n<b>{prices}</b>\n"+
                (ChapterCatalog.Rules.AvailableUpgrades(c.Progress).Contains(chapterProject)?action:"Repair the house first.")+"\n\n"+
                benefit+(purpose==null?"":"\n\nBuilding purpose: "+purpose);
            selectButton.GetComponentInChildren<TMP_Text>().text=reprice?(coins>=offerPrice?$"Build for {offerPrice} Coins":"Update goal · no spend"):ready?"Build saved goal":
                !selected&&coins>=offerPrice&&ChapterCatalog.Rules.AvailableUpgrades(c.Progress).Contains(chapterProject)?$"Build for {offerPrice} Coins":"Choose project";
            selectButton.gameObject.SetActive(ChapterCatalog.Rules.AvailableUpgrades(c.Progress).Contains(chapterProject)&&(c.SelectedProjectId!=chapterProject||ready||reprice));
            ShowYardFamilyActivity(family);
        }
        static string OwnedConstructionBenefits(ChapterState chapter,string family)
        {
            bool auto=false,offline=false;long spin=0,delivery=0;int cadence=0;var symbols=new long[4];
            // Explicit inspection only: sum this family's actually owned catalog
            // effects across both eras. Skill bonuses and free era entry add nothing.
            foreach(var era in ChapterCatalog.Rules.Definition.Eras)for(int level=1;level<=2;level++)
            {
                string id=ChapterIds.Project(era.Id,family,level);
                if(!chapter.Progress.CompletedUpgradeIds.Contains(id))continue;
                ChapterCatalog.Rules.TryGetUpgrade(id,out var upgrade);offline|=id==GameIds.WorkshopBuild;
                foreach(var effect in upgrade.Effects)
                    switch(effect.Kind)
                    {
                        case ContentEffectKind.UnlockAuto:auto=true;break;
                        case ContentEffectKind.SpinCoins:spin+=effect.Amount.Value;break;
                        case ContentEffectKind.TrashSymbolCoins:symbols[SymbolIds.Trash]+=effect.Amount.Value;break;
                        case ContentEffectKind.PigeonDeliveryCoins:delivery+=effect.Amount.Value;cadence=effect.Threshold.Value;break;
                        case ContentEffectKind.SymbolCoins:symbols[effect.SymbolId.Value]+=effect.Amount.Value;break;
                    }
            }
            var parts=new System.Collections.Generic.List<string>();
            if(auto)parts.Add("AUTO unlocked");if(spin>0)parts.Add($"+{spin} {(spin==1?"Coin":"Coins")} per spin");
            string[] triggers={"Potato","Box","Trash","Pigeon"};
            for(int i=0;i<symbols.Length;i++)if(symbols[i]>0)parts.Add($"+{symbols[i]} {(symbols[i]==1?"Coin":"Coins")} per {triggers[i]} symbol");
            if(delivery>0)parts.Add($"+{delivery} Coins per delivery ({cadence} Pigeons accumulated; carry 0–2)");
            return "Building benefits: "+string.Join("; ",parts)+". Skills excluded."+
                (offline?$"\nOffline: +{60/EconomyRules.OfflineSecondsPerCoin} Coin/full min., up to {EconomyRules.OfflineCapSeconds/EconomyRules.OfflineSecondsPerCoin} per absence ({EconomyRules.OfflineCapSeconds/3600}h). Partial minutes do not carry between returns. Later upgrades do not raise this income.":"");
        }
        void ShowChapterSkill(string id)
        {
            var state=Session.State;var c=Session.Chapter;chapterQuote=ChapterEconomy.QuoteSkill(state,id);var q=chapterQuote;
            BeginChapterPanel(ChapterSkill,$"Skill · {new[]{"Coins","recycling","delivery"}[q.Node.Branch]} {q.Node.Tier}");
            long before=q.Node.Branch==0?q.CurrentEffects.UniversalCoins:q.Node.Branch==1?q.CurrentEffects.CoinsPerTrash:q.CurrentEffects.CoinsPerDelivery;
            long after=q.Node.Branch==0?q.NextEffects.UniversalCoins:q.Node.Branch==1?q.NextEffects.CoinsPerTrash:q.NextEffects.CoinsPerDelivery;
            string trigger=q.Node.Branch==0?"per spin":q.Node.Branch==1?"per Trash":"per delivery (3 Pigeons)";
            bool missingPriorSkill=!q.Owned&&q.Node.Tier==2&&q.Node.RequiredSkillId!=null&&!c.Skills.Owns(q.Node.RequiredSkillId);
            string effect=missingPriorSkill?$"This skill adds +{q.Node.AdditiveEffect} Coin {trigger} after purchase. Odds unchanged.":$"Branch bonus: +{before} → +{after} Coins {trigger}. Odds unchanged.";
            string target=c.SelectedProjectId==null?"No building selected.":$"Goal: {ProjectName(c.SelectedProjectId)} · {state.coins}/{ChapterEconomy.SelectedProjectPrice(c)} Coins.";
            var receipt=q.Owned?c.Skills.Receipts.FirstOrDefault(r=>r.Request.NodeId==q.Node.Id):null;
            string price=q.Owned?(receipt==null?"Already owned; past payment unknown.":$"Previously paid: {receipt.FrozenPrice} Coins."):$"Price: {q.Node.Price} Coins.";
            string afterPurchase=$"After purchase: {state.coins-q.Node.Price} Coins.";
            if(q.Status==OfferStatus.Available&&c.SelectedProjectId!=null)
                afterPurchase+=$" Goal still needs {Math.Max(0,ChapterEconomy.SelectedProjectPrice(c)-(state.coins-q.Node.Price))} Coins. Goal retained.";
            string investment=q.Node.Branch==0?"Optional skill.":"Optional investment in future results.";
            if(!q.Owned)investment+=ChapterCatalog.Rules.IsChapterComplete(c.Progress)?" For free play after the chapter.":$" Buildings left: {24-c.Progress.CompletedUpgradeIds.Count}.";
            panelBody.text=$"{effect}\n<b>{price}</b>\n\n{investment}\n\n{target}\n"+
                (q.Status==OfferStatus.Available?$"<b>{afterPurchase}</b>":q.Status==OfferStatus.Maxed?"This node is already owned (1/1).":q.Status==OfferStatus.InsufficientFunds?$"Need {q.Node.Price-state.coins} more Coins.":q.Node.Tier==1?$"Build first: {FamilyName(q.Node.RequiredFamilyId)}.":$"Requires tier I, Workshop Yard and {FamilyName(q.Node.RequiredFamilyId)} stage 1.");
            selectButton.GetComponentInChildren<TMP_Text>().text=$"Buy for {q.Node.Price} Coins";selectButton.gameObject.SetActive(q.Status==OfferStatus.Available);
        }
        void ShowChapterEra()
        {
            if(ChapterCatalog.Rules.IsChapterComplete(Session.Chapter.Progress)){ShowOrders();return;}
            var c=Session.Chapter;BeginChapterPanel(ChapterEra,EraName(c.Progress.EraId));
            var milestones=Session.Milestones;
            if(ChapterCatalog.Rules.CanUnlockWorkshopYard(c.Progress))
            {
                panelBody.text=JunkToNeon.Guidance.ChapterNarrative.EraReady+"\n"+MilestoneFacts(milestones)+"\nEnter for free? Coins, skills, effects and Pigeon carryover remain. Choose the next goal yourself.";
                selectButton.GetComponentInChildren<TMP_Text>().text="Enter for free";selectButton.gameObject.SetActive(true);return;
            }
            panelBody.text=MilestoneFacts(milestones)+"\n"+MilestoneAdvice(milestones)+" Skills are optional.";
        }
        bool SelectChapterPanel()
        {
            if(SelectSectionGoalClear())return true;
            if(SelectJourneyPanel())return true;
            if(wavePanel==0)return false;
            if(wavePanel==ChapterTransition){CloseChapterTransition();return true;}
            if(wavePanel!=ChapterFamily&&wavePanel!=ChapterSkill&&wavePanel!=ChapterEra)return true;
            try
            {
                var state=Session.State;
                string selectedProject=null;
                SkillPurchaseRequest skillRequest=null;
                if(wavePanel==ChapterFamily)
                { if(chapterProject==null||!ChapterCatalog.Rules.AvailableUpgrades(Session.Chapter.Progress).Contains(chapterProject))return true;selectedProject=chapterProject;Session.SelectChapterProject(chapterProject,state.sequence+1,UtcSeconds(),TutorialFacts); }
                else if(wavePanel==ChapterSkill)
                {
                    var c=Session.Chapter;var proposed=SkillTree.Propose(state.sequence,state.coins,ChapterEconomy.Context(c),c.Skills,chapterQuote?.Request);
                    if(proposed.Status!=PurchaseStatus.Proposed){HideWaveChoices();panel.SetActive(false);Render("Offer changed. Reopen the skill.");return true;}
                    skillRequest=chapterQuote.Request;Session.BuySkill(skillRequest,UtcSeconds());
                }
                else
                { if(!ChapterCatalog.Rules.CanUnlockWorkshopYard(Session.Chapter.Progress))return true;Session.EnterWorkshopYard(state.sequence+1,UtcSeconds());ObserveCommitted(state,Session.State);HideWaveChoices();ShowChapterTransition();Render();return true; }
                var committed=Session.State;ObserveCommitted(state,committed,selectedProject);
                var current=ChapterEconomy.Project(committed);
                string result=skillRequest!=null?CommittedSkillResult(state,committed,skillRequest):current.SelectedProjectId==selectedProject?
                    $"Goal selected: {ProjectName(selectedProject)} · {committed.coins}/{ChapterEconomy.SelectedProjectPrice(current)}. Pays when you have enough Coins.":
                    CommittedConstructionResult(state,committed,selectedProject);
                HideWaveChoices();panel.SetActive(false);Render(result);if(!ShowOfflineSummary())ShowAutoHint();
            }
            catch(Exception error){if(Session?.RecoveryRequired==true){ObserveOperationFailure();Error(error.Message);}else{HideWaveChoices();panel.SetActive(false);Render("Action unavailable. Review the requirements again.");}}
            return true;
        }
        static string CommittedConstructionResult(ModelState before,ModelState committed,string project)
        {
            const string unchanged="Action saved.";
            if(project==null||committed.sequence!=before.sequence+1)return unchanged;
            var previous=ChapterEconomy.Project(before);var current=ChapterEconomy.Project(committed);
            if(previous.Progress.CompletedUpgradeIds.Contains(project)||!current.Progress.CompletedUpgradeIds.Contains(project)||
                current.Progress.CompletedUpgradeIds.Count!=previous.Progress.CompletedUpgradeIds.Count+1||current.SelectedProjectId!=null)return unchanged;
            long debit=before.coins-committed.coins;
            if(debit<=0||!ChapterCatalog.Rules.TryGetUpgrade(project,out var built))return unchanged;
            var stage=ChapterCatalog.Rules.Definition.Eras.SelectMany(era=>era.Catalog.Stages).First(item=>item.Id==built.ToStageId);
            string ending=FinalConstructionPayoff(before,committed,project);
            return $"{FamilyName(stage.FamilyId)} {stage.Level}/2: paid {debit}; balance {committed.coins}."+
                (ending.Length==0?" Choose the next goal yourself.":"\n"+ending);
        }
        static string CommittedSkillResult(ModelState before,ModelState committed,SkillPurchaseRequest request)
        {
            const string unchanged="Action saved.";
            if(committed.sequence<=before.sequence||committed.sequence!=request.Sequence)return unchanged;
            var previous=ChapterEconomy.Project(before);var current=ChapterEconomy.Project(committed);
            if(previous.Skills.Owns(request.NodeId)||previous.SelectedProjectId!=current.SelectedProjectId)return unchanged;
            if(current.SelectedProjectId!=null&&ChapterEconomy.SelectedProjectPrice(previous)!=ChapterEconomy.SelectedProjectPrice(current))return unchanged;
            var receipt=current.Skills.Receipts.FirstOrDefault(r=>!r.Legacy&&r.Request.Sequence==committed.sequence&&
                r.Request.NodeId==request.NodeId&&r.Request.OperationId==request.OperationId&&r.PricingRevision==request.PricingRevision&&
                r.Request.ExpectedWallet==request.ExpectedWallet&&r.Request.ExpectedOwnership==request.ExpectedOwnership&&ChapterEconomy.SameContext(r.Request.Context,request.Context));
            if(receipt==null||before.coins-receipt.FrozenPrice!=committed.coins)return unchanged;
            var node=SkillTree.Find(receipt.Request.NodeId,receipt.PricingRevision);if(node==null)return unchanged;
            string name=new[]{"Coins","Recycling","Deliveries"}[node.Branch]+(node.Tier==1?" I":" II");
            string target=current.SelectedProjectId==null?" No building selected.":$" Same goal: {ProjectName(current.SelectedProjectId)} {ChapterEconomy.SelectedProjectPrice(current)}.";
            string earned=previous.Skills.Receipts.Count==5&&current.Skills.Receipts.Count==6?" All skills owned (optional).":"";
            var oldEffects=SkillTree.EvaluateModifiers(ChapterEconomy.Context(previous),previous.Skills);
            var newEffects=SkillTree.EvaluateModifiers(ChapterEconomy.Context(current),current.Skills);
            long oldContribution=node.Branch==0?oldEffects.UniversalCoins:node.Branch==1?oldEffects.CoinsPerTrash:oldEffects.CoinsPerDelivery;
            long newContribution=node.Branch==0?newEffects.UniversalCoins:node.Branch==1?newEffects.CoinsPerTrash:newEffects.CoinsPerDelivery;
            string trigger=node.Branch==0?"per spin":node.Branch==1?"per Trash":"per 3 Pigeons";
            return $"{name} −{receipt.FrozenPrice}; balance {committed.coins}."+target+
                $"\nSkill: +{oldContribution} → +{newContribution} Coins {trigger}."+earned;
        }
        bool ShowChapterTransition()
        {
            if(!CanOpenChapterPanel()||!Session.Chapter.TransitionPending)return false;
            if(wavePanel==ChapterTransition&&panel.activeSelf)return true;
            BeginChapterPanel(ChapterTransition,"Workshop Yard unlocked");
            var milestones=Session.Milestones;
            panelBody.text=JunkToNeon.Guidance.ChapterNarrative.EraEntry+"\n"+MilestoneFacts(milestones)+"\n"+MilestoneAdvice(milestones)+" Coins, skills, effects and Pigeon carryover retained. You may skip this.";
            selectButton.GetComponentInChildren<TMP_Text>().text="Continue";selectButton.gameObject.SetActive(true);closeButton.GetComponentInChildren<TMP_Text>().text="Skip introduction";
            StartCoroutine(ChapterTransitionPresentation());return true;
        }
        IEnumerator ChapterTransitionPresentation()
        {
            yield return new WaitForSecondsRealtime(.75f);
            if(wavePanel==ChapterTransition&&Session?.Chapter.TransitionPending==true)panelTitle.text="Workshop Yard · continue?";
        }
        bool CloseChapterTransition()
        {
            if(wavePanel!=ChapterTransition)return false;
            try{Session.AcknowledgeEraTransition(Session.State.sequence+1,UtcSeconds());}
            catch(Exception error){ObserveOperationFailure();Error(error.Message);return true;}
            HideWaveChoices();panel.SetActive(false);Render("New era saved. Choose one goal.");if(!ShowOfflineSummary())ShowAutoHint();return true;
        }
        static bool ChapterStopsAuto(ModelState s)
        { var c=ChapterEconomy.Project(s);return c.TransitionPending||ChapterCatalog.Rules.CanUnlockWorkshopYard(c.Progress)||ChapterCatalog.Rules.IsChapterComplete(c.Progress); }
        static string FamilyName(string id)
        { return id==FamilyIds.House?"House":id==FamilyIds.Workshop?"Workshop":id==FamilyIds.Recycler?"Recycler":id==FamilyIds.PigeonLoft?"Pigeon Loft":id==FamilyIds.Cart?"Cart":"Power Source"; }
        static string EraName(string id) { return id==EraIds.Junkyard?"Junkyard":"Workshop Yard"; }
        static string ProjectName(string id)
        { ChapterCatalog.Rules.TryGetUpgrade(id,out var u);return u==null?"—":FamilyName(ChapterCatalog.Rules.Definition.Eras.SelectMany(e=>e.Catalog.Stages).First(x=>x.Id==u.ToStageId).FamilyId); }
        static long ProjectPrice(string id) { return ChapterEconomy.ProjectPrice(id); }
        static string Benefit(UpgradeDefinition u)
        {
            if(u.Effects.Count==0)return "Completes this building for the next era.";
            var e=u.Effects[0];
            return e.Kind==ContentEffectKind.UnlockAuto?"Unlocks AUTO.":e.Kind==ContentEffectKind.SpinCoins?"+1 Coin per future spin.":e.Kind==ContentEffectKind.TrashSymbolCoins?"+1 Coin per Trash symbol on future spins.":e.Kind==ContentEffectKind.PigeonDeliveryCoins?"Future spins: +3 Coins per delivery of 3 accumulated Pigeons; carry 0–2.":$"+1 Coin per {new[]{"Potato","Box","Trash","Pigeon"}[e.SymbolId.Value]} symbol on future spins.";
        }
        void RenderChapter(ModelState s,string status)
        {
            if(moreProjectsButton==null)return;
            var c=ChapterEconomy.Project(s);var stages=ChapterCatalog.Rules.Stages(c.Progress);var w=WaveProgress.Of(s);bool complete=ChapterCatalog.Rules.IsChapterComplete(c.Progress),ready=ChapterCatalog.Rules.CanUnlockWorkshopYard(c.Progress);
            heading.text=complete?"Chapter complete.":ready?"Junkyard complete. Unlock the next era.":EraName(c.Progress.EraId)+" · a place with a future.";
            if(chapterBanner!=null)chapterBanner.text=(c.Progress.EraId==EraIds.Junkyard?"01 / A NEW START":"02 / WORKSHOP YARD")+$" · {c.Progress.CompletedUpgradeIds.Count}/24";
            stageLabel.text=c.SelectedProjectId==null?(complete?"TWO ERAS · ALL BUILDINGS COMPLETE":$"ERA {(c.Progress.EraId==EraIds.Junkyard?1:2)}/2 · CHOOSE ONE GOAL"):$"IN PROGRESS: {ProjectName(c.SelectedProjectId)} · {Math.Min(s.coins,ChapterEconomy.SelectedProjectPrice(c))}/{ChapterEconomy.SelectedProjectPrice(c)}";
            if(goalProgressTrack!=null)
            {
                bool selected=c.SelectedProjectId!=null;
                goalProgressTrack.gameObject.SetActive(selected);
                if(selected)goalProgressFill.anchorMax=new Vector2((float)Math.Min(1d,(double)s.coins/ChapterEconomy.SelectedProjectPrice(c)),1);
            }
            projectLabel.text=$"House · {stages.LocalLevel(FamilyIds.House)}/2";workshopLabel.text=$"Workshop · {stages.LocalLevel(FamilyIds.Workshop)}/2";
            projectPrice.text=stages.LocalLevel(FamilyIds.House)==2?"DONE":ProjectPrice(ChapterIds.Project(c.Progress.EraId,FamilyIds.House,stages.LocalLevel(FamilyIds.House)+1)).ToString();
            workshopMarker.text=$"Workshop {stages.LocalLevel(FamilyIds.Workshop)}/2"+(s.workshopBuilt==1?" · +1 per spin":" · 120");
            waveStatus.text=$"Skills {c.Skills.Receipts.Count}/6 · Pigeons {(w.PigeonLoftBuilt==1?w.Delivery.PigeonProgress+"/3":"no")}";
            var order=JourneyEconomy.Project(s).Orders;if(order.ActiveOrderId!=null)waveStatus.text+=$" · order {order.Progress}/{JunkToNeon.Orders.OrderCatalog.Get(order.ActiveOrderId).Target}";
            moreProjectsButton.GetComponentInChildren<TMP_Text>().text="Buildings";moreProjectsButton.interactable=!Busy&&!failed&&!InBackground&&!c.TransitionPending;
            if(chapterWorldButtons!=null)foreach(var button in chapterWorldButtons)if(button!=null)button.interactable=!Busy&&!failed&&!InBackground&&!c.TransitionPending;
            string receipt="";
            if(s.lastSpin?.chapter?.Effects!=null)
            {
                var r=s.lastSpin;var e=r.chapter.Effects;
                receipt=$"Base +{r.reward} · Workshop +{r.bonus} · skills +{e.UniversalCoins}\nTrash +{e.Baseline.RecyclerCoins+e.RecyclingSkillCoins+e.TrashCoins} · delivery +{e.Baseline.DeliveryCoins+e.DeliverySkillCoins} · symbols +{e.PotatoCoins+e.BoxCoins+e.PigeonCoins}. ";
            }
            else if(s.lastSpin!=null)receipt=$"Base +{s.lastSpin.reward} · Workshop +{s.lastSpin.bonus}. ";
            // A purchase/claim has its own feedback; an older spin receipt would
            // misattribute that action and crowd out the useful result text.
            if(status!=null&&s.lastSpin?.sequence!=s.sequence)receipt="";
            message.text=Busy?status??"Gathering materials…":receipt+(status??(complete?"Chapter complete. Skills are optional.":ready?"Open the era in Buildings.":c.SelectedProjectId==null?"No next goal selected.":"Goal selected. Pays when you have enough Coins."));
        }
    }
}
