using System;
using JunkToNeon.Core;
using TMPro;
using UnityEngine;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        UnityEngine.UI.Button expansionBatchMinusButton,expansionBatchPlusButton,expansionBatchMaxButton;
        int expansionBatchCount=1;
        ExpansionBatchQuote expansionBatchQuote;

        void EnsureExpansionBatchControls()
        {
            if(expansionBatchMinusButton!=null)return;
            expansionBatchMinusButton=CreateSectionControl("ExpansionBatchMinus","−",expansionRoot,new Vector2(0,.20f),new Vector2(.28f,.35f),ColorOf("263C50"),()=>{});
            expansionBatchPlusButton=CreateSectionControl("ExpansionBatchPlus","+",expansionRoot,new Vector2(.31f,.20f),new Vector2(.59f,.35f),ColorOf("263C50"),()=>{});
            expansionBatchMaxButton=CreateSectionControl("ExpansionBatchMax","Max",expansionRoot,new Vector2(.62f,.20f),new Vector2(1,.35f),ColorOf("263C50"),()=>{});
            foreach(var button in ExpansionBatchControls())
            {ExpansionFit(button,10,13);ExpansionStyleControl(button,false);AddSectionConsent(button);button.gameObject.SetActive(false);}
        }
        UnityEngine.UI.Button[] ExpansionBatchControls()=>new[]{expansionBatchMinusButton,expansionBatchPlusButton,expansionBatchMaxButton};
        void ResetExpansionBatchPresentation()
        {
            expansionBatchQuote=null;expansionBatchCount=1;
            foreach(var button in ExpansionBatchControls())if(button!=null){InvalidateExpansionConsent(button);button.gameObject.SetActive(false);}
            if(expansionBody!=null)Place(expansionBody.rectTransform,expansionRoot,new Vector2(.015f,.20f),new Vector2(.985f,.78f));
        }
        void InvalidateExpansionBatch()=>ResetExpansionBatchPresentation();

        void ShowExpansionBatchOffer(string id,int category,int page,int returnMode,int count)
        {
            if(!ExpansionCanOpen())return;
            var session=Session;var state=session.State;var view=ExpansionEconomy.Project(state);
            int maximum=ExpansionEconomy.MaximumTradeBatches(state,id);
            ExpansionQuote single=null;ExpansionBatchQuote batch=null;
            string refusal=null;
            try
            {
                if(count==1)single=session.GetExpansionOffer(id);
                else batch=session.GetExpansionBatchOffer(id,count);
            }
            catch(InvalidOperationException){refusal="Unavailable. Reopen after stock or Coin capacity changes.";}
            BeginExpansion(ExpansionOffer,ExpansionName(id),view);
            expansionCategory=category;expansionPage=page;expansionReturnMode=returnMode;expansionInspectedId=id;
            expansionQuote=single;expansionBatchQuote=batch;expansionBatchCount=count;
            EnsureExpansionBatchControls();
            Place(expansionBody.rectTransform,expansionRoot,new Vector2(.015f,.37f),new Vector2(.985f,.78f));
            expansionBody.gameObject.SetActive(true);
            bool parts=id=="trade.parts",valid=single!=null||batch!=null;
            long debit=valid?(single!=null?(parts?single.PartsCost:single.PotatoesCost):(parts?batch.PartsCost:batch.PotatoesCost)):0;
            long credit=valid?(single!=null?single.CoinsReward:batch.CoinsReward):0;
            string resource=parts?"parts":"potatoes";
            expansionBody.text=$"Batches: {count} · available: {maximum}\n";
            if(valid)
            {
                expansionBody.text+=$"Exchange {debit} {resource} → {credit} coins.\nParts left: {view.Parts-(parts?debit:0)} · Potatoes left: {view.Potatoes-(parts?0:debit)}\nCoins: {state.coins} → {checked(state.coins+credit)}.\nConstruction uses separate consent.";
                expansionBody.text+=ExpansionBatchGoal(state,credit);
                ExpansionAction(expansionPrimaryButton,1,$"{debit} {resource}\n→ {credit} coins");
                var visibleEvent=new UnityEngine.UI.Button.ButtonClickedEvent();
                var frozenSingle=single;var frozenBatch=batch;long sequence=state.sequence;
                visibleEvent.AddListener(()=>
                {
                    if(!ReferenceEquals(expansionPrimaryButton.onClick,visibleEvent)||!ReferenceEquals(Session,session)||
                        wavePanel!=ExpansionOffer||!expansionContextValid||!SectionAction(expansionPrimaryButton,ExpansionOffer))return;
                    if(Session.State.sequence!=sequence){RefuseExpansionBatchOffer(category,page,returnMode);return;}
                    if(frozenSingle!=null){if(!ReferenceEquals(expansionQuote,frozenSingle))return;ExecuteExpansionQuote();}
                    else{if(!ReferenceEquals(expansionBatchQuote,frozenBatch))return;ExecuteExpansionBatch(frozenBatch);}
                });
                expansionPrimaryButton.onClick=visibleEvent;InvalidateExpansionConsent(expansionPrimaryButton);
            }
            else expansionBody.text+=(refusal??"At least 20 resources and safe Coin capacity are required.")+ExpansionTradeGoal(state);
            BindExpansionBatchQuantity(expansionBatchMinusButton,count>1,count-1);
            BindExpansionBatchQuantity(expansionBatchPlusButton,count<maximum,count+1);
            BindExpansionBatchQuantity(expansionBatchMaxButton,maximum>0&&count!=maximum,maximum);
            closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;

            void BindExpansionBatchQuantity(UnityEngine.UI.Button button,bool available,int chosen)
            {
                var visibleEvent=new UnityEngine.UI.Button.ButtonClickedEvent();
                button.onClick=visibleEvent;button.gameObject.SetActive(true);button.interactable=available;
                button.targetGraphic.color=ColorOf(available?"263C50":"C8D8DB");
                button.GetComponentInChildren<TMP_Text>(true).color=ColorOf(available?"F6F1E3":"142638");
                var colors=button.colors;colors.disabledColor=Color.white;button.colors=colors;
                visibleEvent.AddListener(()=>
                {
                    if(!available||!ReferenceEquals(button.onClick,visibleEvent)||!ReferenceEquals(Session,session)||
                        wavePanel!=ExpansionOffer||!expansionContextValid||!SectionAction(button,ExpansionOffer))return;
                    if(Session.State.sequence!=state.sequence){RefuseExpansionBatchOffer(category,page,returnMode);return;}
                    ShowExpansionBatchOffer(id,category,page,returnMode,chosen);
                });
                InvalidateExpansionConsent(button);
            }
        }
        void RefuseExpansionBatchOffer(int category,int page,int returnMode)
        {
            BeginExpansion(ExpansionResult,"Offer changed",Session.Expansion);
            expansionCategory=category;expansionPage=page;expansionReturnMode=returnMode;
            expansionBody.gameObject.SetActive(true);
            expansionBody.text="Action not performed. Progress changed. Reopen the offer to inspect current stock and Coin capacity. There is no automatic retry.";
            closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;
        }
        string ExpansionBatchGoal(ModelState state,long credit)
        {
            var chapter=ChapterEconomy.Project(state);if(chapter.SelectedProjectId==null)return "";
            long price=ChapterEconomy.SelectedProjectPrice(chapter),remaining=Math.Max(0,price-checked(state.coins+credit));
            return $"\nGoal: {ProjectName(chapter.SelectedProjectId)} · saved {price} coins.\nAfter exchange: "+
                (remaining>0?$"need {remaining} "+(remaining==1?"coin.":"coins."):"funded, unpaid · Slot → SPIN.");
        }
        void ExecuteExpansionBatch(ExpansionBatchQuote quote)
        {
            int category=expansionCategory,page=expansionPage,returnMode=expansionReturnMode;
            int filteredCategory=expansionFilteredCategory,ownedCategory=expansionOwnedCategory;
            var before=Session.State;HideWaveChoices();
            try
            {
                var after=Session.BuyExpansionBatch(quote,UtcSeconds());var view=ExpansionEconomy.Project(after);var receipt=view.LastCommand;
                bool fresh=after.sequence>before.sequence&&after.sequence==quote.Sequence&&
                    receipt!=null&&receipt.Key==quote.Key&&receipt.Sequence==after.sequence&&receipt.Action=="buy"&&receipt.Id==quote.Id&&receipt.Slot==0&&
                    after.coins-before.coins==quote.CoinsReward&&ExpansionEconomy.Project(before).Parts-view.Parts==quote.PartsCost&&
                    ExpansionEconomy.Project(before).Potatoes-view.Potatoes==quote.PotatoesCost;
                if(fresh)ObserveCommitted(before,after);
                BeginExpansion(ExpansionResult,fresh?"Action saved":"No new action",view);
                expansionCategory=category;expansionPage=page;expansionReturnMode=returnMode;expansionBody.gameObject.SetActive(true);
                expansionFilteredCategory=filteredCategory;expansionOwnedCategory=ownedCategory;
                expansionBody.text=fresh?$"Exchange completed · {quote.BatchCount} batches.\nSpent: "+
                    (quote.PartsCost>0?$"{quote.PartsCost} parts.":$"{quote.PotatoesCost} potatoes.")+
                    $"\nCredited: {quote.CoinsReward} coins.\nCoins: {before.coins} → {after.coins}.\nParts: {view.Parts}; potatoes: {view.Potatoes}; energy: {view.Energy}.":
                    "No new charge or credit was confirmed. Reopen the category.";
                if(fresh)PrepareNextExpansionTrade(quote.Id,after,view);
                closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;Render(fresh?"Shop action saved.":"No new shop action.");
            }
            catch(Exception error)
            {
                if(Session.RecoveryRequired){ObserveOperationFailure();Error(error.Message);return;}
                BeginExpansion(ExpansionResult,"Offer changed",Session.Expansion);expansionCategory=category;expansionPage=page;expansionReturnMode=returnMode;
                expansionFilteredCategory=filteredCategory;expansionOwnedCategory=ownedCategory;
                expansionBody.gameObject.SetActive(true);expansionBody.text="Action not performed. Reopen the offer; there is no automatic retry.";
                closeButton.GetComponentInChildren<TMP_Text>(true).text=BackCaption;Render("Offer changed. Check your resources.");
            }
        }
    }
}
