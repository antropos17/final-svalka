using System;
using JunkToNeon.Core;
using TMPro;
using UnityEngine;
using ArcadeGlyph=JunkToNeon.JtnProductIcon.Symbol;

namespace JunkToNeon
{
    public sealed partial class JunkGame
    {
        RectTransform parcelsRoot;
        readonly UnityEngine.UI.Button[] parcelsTokenButtons=new UnityEngine.UI.Button[6],parcelsSlotButtons=new UnityEngine.UI.Button[4];
        TMP_Text parcelsRequestA,parcelsRequestB,parcelsInstruction;
        int parcelsSelectedToken=-1;
        bool parcelsFinishPreview;
        static string ParcelsDiamonds(long amount)=>amount+(amount==1?" Diamond":" Diamonds");

        static long? SavedArcadeReward(ModelState state,ArcadeView arcade)
        {
            if(arcade.ActiveRun!=null)return arcade.ActiveRun.Ready?arcade.ActiveRun.ReadyReward:-1;
            var run=ParcelsEconomy.Project(state).ActiveRun;
            return run==null?(long?)null:run.Ready?run.ReadyReward:-1;
        }
        static string SavedArcadeActivity(ModelState state,ArcadeView arcade)
        {
            var reward=SavedArcadeReward(state,arcade);
            return !reward.HasValue?null:reward.Value<0?"Round saved":reward.Value==0?"Ready: 0 · finish":$"Ready to claim: {reward.Value}";
        }
        string ParcelsEntryStatus(ParcelsView view)
        {
            if(view.ActiveRun!=null)return view.ActiveRun.Ready?(view.ActiveRun.ReadyReward==0?"Finish · 0 Diamonds":"Claim "+ParcelsDiamonds(view.ActiveRun.ReadyReward)):"Resume free · assignment saved";
            if(Session.Arcade.ActiveRun!=null)return "Another round is saved · inspect";
            if(!view.Unlocked)return "Requires Pigeon Loft I · inspect";
            return Session.Arcade.Tickets>0?"1 Ticket · up to 3 Diamonds":"No Ticket · rules available";
        }
        void ResetParcelsPresentation()
        {
            parcelsSelectedToken=-1;parcelsFinishPreview=false;
            if(parcelsRoot!=null)parcelsRoot.gameObject.SetActive(false);
            foreach(var button in parcelsTokenButtons)if(button!=null)InvalidateExpansionConsent(button);
            foreach(var button in parcelsSlotButtons)if(button!=null)InvalidateExpansionConsent(button);
        }
        void EnsureParcelsPresentation()
        {
            if(parcelsRoot!=null)return;
            parcelsRoot=Area("Parcels",arcadeRoot,Vector2.zero,Vector2.one);
            var divider=Area("Parcels Request Divider",parcelsRoot,new Vector2(.498f,.505f),new Vector2(.502f,.755f));
            Surface(divider.gameObject,ColorOf("C8D8DB"),1);
            parcelsRequestA=ExpansionLabel("ParcelsRequestA",parcelsRoot,new Vector2(.02f,.645f),new Vector2(.48f,.755f),13);
            parcelsRequestB=ExpansionLabel("ParcelsRequestB",parcelsRoot,new Vector2(.52f,.645f),new Vector2(.98f,.755f),13);
            parcelsInstruction=ExpansionLabel("ParcelsInstruction",parcelsRoot,new Vector2(.02f,.452f),new Vector2(.98f,.492f),11);
            parcelsRequestA.alignment=parcelsRequestB.alignment=TextAlignmentOptions.TopLeft;
            parcelsInstruction.alignment=TextAlignmentOptions.Center;
            for(int i=0;i<4;i++)
            {
                float left=i/2*.50f+.02f+(i%2)*.24f;
                parcelsSlotButtons[i]=ParcelsControl("ParcelsSlot"+i,new Vector2(left,.505f),new Vector2(left+.22f,.635f));
            }
            for(int i=0;i<6;i++)
            {
                float left=.02f+(i%3)*.325f,top=i<3?.44f:.315f;
                parcelsTokenButtons[i]=ParcelsControl("ParcelsToken"+i,new Vector2(left,top-.11f),new Vector2(left+.31f,top));
            }
            parcelsRoot.gameObject.SetActive(false);
        }
        UnityEngine.UI.Button ParcelsControl(string name,Vector2 min,Vector2 max)
        {
            var button=CreateSectionControl(name,"",parcelsRoot,min,max,ColorOf("C8D8DB"),()=>{});
            AddSectionConsent(button);ExpansionFit(button,10,12);ArcadeActionStyle(button,false,true);
            var text=button.GetComponentInChildren<TMP_Text>(true);
            Place(text.rectTransform,button.transform,new Vector2(.04f,.03f),new Vector2(.96f,.50f));
            text.alignment=TextAlignmentOptions.Center;
            ArcadeIcon(button.transform,"ParcelsIcon",ArcadeGlyph.Box,new Vector2(.34f,.52f),new Vector2(.66f,.96f),ColorOf("263C50"));
            return button;
        }
        void ParcelsFace(UnityEngine.UI.Button button,string caption,int symbol,bool selected=false,bool reference=false)
        {
            button.GetComponentInChildren<TMP_Text>(true).text=caption;
            button.targetGraphic.color=ColorOf(selected?"F4B85C":"C8D8DB");
            ArcadeVisibleSymbol(button.transform,"ParcelsSymbol","ParcelsIcon",reference?-1:symbol);
            if(reference)
            {
                button.transform.Find("ParcelsIcon").gameObject.SetActive(false);
                Place(button.GetComponentInChildren<TMP_Text>(true).rectTransform,button.transform,new Vector2(.04f,.05f),new Vector2(.96f,.95f));
            }
            else Place(button.GetComponentInChildren<TMP_Text>(true).rectTransform,button.transform,new Vector2(.04f,.03f),new Vector2(.96f,.50f));
        }
        void BindParcels(UnityEngine.UI.Button button,string caption,ParcelsView view,Action action,bool enabled=true)
        {
            long version=arcadeContextVersion,sequence=view.Sequence;int mode=wavePanel;var session=Session;
            var visible=new UnityEngine.UI.Button.ButtonClickedEvent();button.onClick=visible;
            button.GetComponentInChildren<TMP_Text>(true).text=caption;button.interactable=enabled;button.gameObject.SetActive(true);
            visible.AddListener(()=>
            {
                if(!enabled||!ReferenceEquals(button.onClick,visible)||version!=arcadeContextVersion||!arcadeContextValid||!ReferenceEquals(session,Session)||
                    !ArcadeCanOpen()||!SectionAction(button,mode))return;
                if(Session.State.sequence!=sequence){ShowParcelsRefusal();return;}
                action();
            });
            InvalidateExpansionConsent(button);
            if(button==arcadePrimaryButton||button==arcadeSecondaryButton)ArcadeActionStyle(button,button==arcadePrimaryButton,enabled);
            if(button==arcadePrimaryButton&&!enabled)
            {
                button.targetGraphic.color=ColorOf("E1E6DC");
                button.GetComponentInChildren<TMP_Text>(true).color=ColorOf("52656A");
            }
        }
        ParcelsQuote ParcelsTryQuote(ParcelsView view,string action,long instance=0,int token=-1,int slot=-1)
        {
            try{return Session.GetParcelsQuote(action,view.Sequence,instance,token,slot);}
            catch(InvalidOperationException){return null;}
        }
        void BindParcelsQuote(UnityEngine.UI.Button button,string caption,ParcelsView view,ParcelsQuote quote)
        {BindParcels(button,caption,view,()=>ExecuteParcelsQuote(quote),quote!=null);}
        void ShowParcelsGame()
        {
            if(!ArcadeCanOpen())return;var view=Session.Parcels;
            if(view.ActiveRun!=null){ShowParcelsRound();return;}
            BeginArcade(ArcadePreview,"Pigeon's Parcels",Session.Arcade);arcadeCategory=-1;
            ArcadeHeading(ArcadeGlyph.Box,"OPTIONAL PACKING PUZZLE","emblem-parcels");arcadeBody.gameObject.SetActive(true);
            arcadeBody.text="The Pigeon has two requests. Pick four of six puzzle tokens; two stay unused.\nTap a token, then a position. Moves and corrections are free. These pieces do not spend your inventory.\n\nSatisfy 0 / 1 / 2 requests for 0 / 1 / 3 Diamonds. Finish freezes the result; Claim credits it separately. No timer or move limit.\nStart: 1 Ticket. Requires Pigeon Loft I.";
            if(Session.Arcade.ActiveRun!=null)
            {
                arcadeBody.text+="\nAnother round is saved. Finish or abandon it first.";
                BindParcels(arcadePrimaryButton,"To saved round",view,ShowArcadeRound);
            }
            else
            {
                var quote=ParcelsTryQuote(view,"start");
                BindParcelsQuote(arcadePrimaryButton,"Start · 1 Ticket",view,quote);
                if(!view.Unlocked)arcadeBody.text+="\nPigeon Loft I is not owned yet.";
                else if(Session.Arcade.Tickets==0)arcadeBody.text+="\nNo Ticket. Resume an existing round free.";
            }
            BindParcels(arcadeSecondaryButton,"To game",view,ReturnArcadeToGame);
        }
        static string ParcelsShortRequest(string board,bool second)
        {
            if(board=="parcels.box-priority")return second?"2 Boxes":"2 different\nInclude Potato";
            if(board=="parcels.matching-pair")return second?"Potato + Pigeon":"2 matching symbols";
            if(board=="parcels.keep-the-pair")return second?"Box + Trash":"2 different symbols";
            return second?"2 different\nNo Boxes":"At least 1 Box";
        }
        static string ParcelsSlotName(int slot)=>(slot<2?"A":"B")+(slot%2+1);
        void ShowParcelsRound(int selectedToken=-1)
        {
            if(!ArcadeCanOpen())return;var view=Session.Parcels;var run=view.ActiveRun;
            if(run==null){ShowArcadeHome();return;}
            BeginArcade(run.Ready?ArcadeResult:ArcadeRound,"Pigeon's Parcels",Session.Arcade);arcadeCategory=-1;
            ArcadeHeading(ArcadeGlyph.Box,run.Ready?"SAVED RESULT · NOT CREDITED":"TWO REQUESTS · FREE CORRECTIONS","emblem-parcels");
            if(run.Ready)
            {
                arcadeBody.gameObject.SetActive(true);
                arcadeBody.text=$"Requests satisfied: {run.SatisfiedRequests}/2.\nThe assignment is final. No new Ticket is needed.\n"+
                    (run.ReadyReward==0?"Finish explicitly to clear this zero result.":"Claim explicitly to credit the saved reward.");
                ArcadeResultPresentation(run.ReadyReward==0?"ROUND COMPLETE":"READY TO CLAIM · NOT CREDITED",run.ReadyReward==0?"No Diamonds earned":ParcelsDiamonds(run.ReadyReward),run.ReadyReward==0?ArcadeGlyph.Check:ArcadeGlyph.Diamond);
                var claim=ParcelsTryQuote(view,"claim",run.InstanceSequence);
                BindParcelsQuote(arcadePrimaryButton,run.ReadyReward==0?"Finish · 0 Diamonds":"Claim "+ParcelsDiamonds(run.ReadyReward),view,claim);
                if(claim==null)arcadeBody.text+="\nReward does not fit the wallet yet. Result remains saved.";
            }
            else
            {
                EnsureParcelsPresentation();parcelsRoot.gameObject.SetActive(true);parcelsSelectedToken=selectedToken;
                parcelsRequestA.text="A · "+(run.RequestASatisfied?"Met":"Not met")+"\n"+ParcelsShortRequest(run.BoardId,false);
                parcelsRequestB.text="B · "+(run.RequestBSatisfied?"Met":"Not met")+"\n"+ParcelsShortRequest(run.BoardId,true);
                parcelsInstruction.text=selectedToken<0?$"{run.SatisfiedRequests}/2 met · {ParcelsDiamonds(run.PredictedReward)} · select a token":$"Selected #{selectedToken+1} · tap slot to place / replace";
                for(int i=0;i<6;i++)
                {
                    int token=i,location=-1;for(int s=0;s<4;s++)if(run.Slots[s]==i)location=s;
                    bool selected=selectedToken==i;
                    string name=ArcadeCopy.SymbolLabel(run.TokenSymbols[i]);
                    string caption=(selected?"Selected ":"")+"#"+(i+1)+" "+name+(location<0?"":"\n→ "+ParcelsSlotName(location));
                    BindParcels(parcelsTokenButtons[i],caption,view,()=>ShowParcelsRound(selected?-1:token));
                    ParcelsFace(parcelsTokenButtons[i],caption,run.TokenSymbols[i],selected,location>=0);
                }
                for(int i=0;i<4;i++)
                {
                    int token=run.Slots[i],slot=i;
                    string caption=ParcelsSlotName(i)+"\n"+(token<0?"Empty":"#"+(token+1)+" "+ArcadeCopy.SymbolLabel(run.TokenSymbols[token]));
                    var move=selectedToken>=0||token>=0?ParcelsTryQuote(view,"place",run.InstanceSequence,selectedToken,slot):null;
                    BindParcels(parcelsSlotButtons[i],caption,view,()=>ExecuteParcelsQuote(move),move!=null);
                    ParcelsFace(parcelsSlotButtons[i],caption,token<0?-1:run.TokenSymbols[token]);
                    if(token<0)parcelsSlotButtons[i].transform.Find("ParcelsIcon").gameObject.SetActive(false);
                }
                var finish=run.Complete?ParcelsTryQuote(view,"finish",run.InstanceSequence):null;
                BindParcels(arcadePrimaryButton,"Finish · "+ParcelsDiamonds(run.PredictedReward),view,()=>
                {if(run.SatisfiedRequests==2)ExecuteParcelsQuote(finish);else ShowParcelsFinishConfirmation(finish);},finish!=null);
            }
            BindParcels(arcadeSecondaryButton,"Abandon…",view,ShowParcelsAbandon);
            closeButton.GetComponentInChildren<TMP_Text>(true).text="Save and return to mini-games";
        }
        void ShowParcelsFinishConfirmation(ParcelsQuote quote)
        {
            if(!ArcadeCanOpen()||quote==null)return;var view=Session.Parcels;var run=view.ActiveRun;
            if(run==null||view.Sequence!=quote.ExpectedSequence||run.InstanceSequence!=quote.ExpectedInstance)return;
            BeginArcade(ArcadePreview,"Finish this layout?",Session.Arcade);arcadeCategory=-1;parcelsFinishPreview=true;
            ArcadeHeading(ArcadeGlyph.Box,"CONFIRM PARTIAL RESULT","emblem-parcels");arcadeBody.gameObject.SetActive(true);
            arcadeBody.text=$"Only {run.SatisfiedRequests} of 2 requests are satisfied.\nFinish for {ParcelsDiamonds(quote.ResultReward)}?\nYou cannot rearrange after finishing. This freezes the result; it does not credit the reward.";
            BindParcelsQuote(arcadePrimaryButton,"Finish · "+ParcelsDiamonds(quote.ResultReward),view,quote);
            BindParcels(arcadeSecondaryButton,"Keep rearranging",view,()=>ShowParcelsRound());
            closeButton.GetComponentInChildren<TMP_Text>(true).text="Cancel · to round";
        }
        void ShowParcelsAbandon()
        {
            if(!ArcadeCanOpen())return;var view=Session.Parcels;var run=view.ActiveRun;if(run==null)return;
            var quote=ParcelsTryQuote(view,"abandon",run.InstanceSequence);
            BeginArcade(ArcadePreview,"Abandon Parcels?",Session.Arcade);arcadeCategory=-1;arcadeAbandonPreview=true;
            ArcadeHeading(ArcadeGlyph.Box,"NO REWARD OR TICKET REFUND","emblem-parcels");arcadeBody.gameObject.SetActive(true);
            arcadeBody.text=run.Ready&&run.ReadyReward>0?$"Forfeit the unclaimed {ParcelsDiamonds(run.ReadyReward)}?\nThe Ticket is not returned. Your wallet is unchanged.":"Close this round without a reward or Ticket refund?\nThe current assignment will be lost.";
            BindParcelsQuote(arcadePrimaryButton,"Abandon without reward",view,quote);
            BindParcels(arcadeSecondaryButton,"Cancel · return",view,()=>ShowParcelsRound());
            closeButton.GetComponentInChildren<TMP_Text>(true).text="Cancel · to round";
        }
        void ShowParcelsRefusal()
        {
            if(!ArcadeCanOpen())return;var view=Session.Parcels;
            BeginArcade(ArcadeResult,"Action unavailable",Session.Arcade);arcadeCategory=-1;arcadeBody.gameObject.SetActive(true);
            arcadeBody.text="Progress changed. No action was performed. Reopen the saved round; there is no automatic retry.";
            BindParcels(arcadeSecondaryButton,"To saved round",view,ShowArcadeRound,view.ActiveRun!=null||Session.Arcade.ActiveRun!=null);
        }
        void ExecuteParcelsQuote(ParcelsQuote quote)
        {
            if(quote==null||!arcadeContextValid||!ArcadeCanOpen())return;
            if(Session.State.sequence!=quote.ExpectedSequence){ShowParcelsRefusal();return;}
            var before=Session.State;var source=arcadeRoot.TransformPoint(arcadeRoot.rect.center);HideWaveChoices();
            try
            {
                var after=Session.ExecuteParcels(quote,UtcSeconds());var view=Session.Parcels;var receipt=view.LastCommand;
                bool fresh=after.sequence>before.sequence&&after.sequence==quote.Sequence&&receipt!=null&&receipt.Key==quote.Key&&receipt.Sequence==after.sequence;
                if(fresh)ObserveCommitted(before,after);
                if((fresh||after.sequence==before.sequence)&&view.ActiveRun!=null){ShowParcelsRound();}
                else
                {
                    BeginArcade(ArcadeResult,fresh?(quote.Action=="claim"?"Result confirmed":"Round abandoned"):"No new action",Session.Arcade);arcadeCategory=-1;
                    arcadeBody.gameObject.SetActive(true);
                    arcadeBody.text=!fresh?"No repeat charge or credit. Reopen Events.":quote.Action=="claim"?
                        (quote.DiamondsCredit==0?"Round finished. No Diamonds earned.":$"Credited: {ParcelsDiamonds(quote.DiamondsCredit)}."):"Round closed without a reward or Ticket refund.";
                    ArcadeResultPresentation(fresh&&quote.Action=="claim"?"RESULT CONFIRMED":"NO REWARD OR TICKET REFUND",fresh&&quote.Action=="claim"&&quote.DiamondsCredit>0?ParcelsDiamonds(quote.DiamondsCredit):"Round closed",ArcadeGlyph.Check);
                    BindParcels(arcadeSecondaryButton,"To mini-games",view,ShowArcadeHome);
                }
                Render(fresh?"Action saved.":"No new action.");
                if(fresh&&quote.Action=="claim")RevealParcelsClaim(before,after,quote,source);
            }
            catch(Exception error)
            {
                if(Session.RecoveryRequired){ClearRewardFeedback();ObserveOperationFailure();Error(error.Message);return;}
                ShowParcelsRefusal();Render("Check the saved round.");
            }
        }
        void RevealParcelsClaim(ModelState before,ModelState after,ParcelsQuote quote,Vector3 source)
        {
            if(Session==null||Session.RecoveryRequired||failed||InBackground||before.pending!=null||after.pending!=null||
                after.sequence!=before.sequence+1||after.sequence!=quote.Sequence||Session.State.sequence!=after.sequence)return;
            var receipt=ParcelsEconomy.Project(after).LastClaim;
            if(receipt==null||receipt.Key!=quote.Key||receipt.Sequence!=after.sequence||receipt.ExpectedInstance!=quote.ExpectedInstance||receipt.Action!="claim")return;
            long credit=ArcadeEconomy.Project(after).Diamonds-ArcadeEconomy.Project(before).Diamonds;
            if(credit!=quote.DiamondsCredit||credit<=0)return;
            if(!ReferenceEquals(rewardFeedbackSession,Session))
            {ClearRewardFeedback();rewardFeedbackSession=Session;rewardFeedbackAcceptedSequence=-1;}
            if(after.sequence<=rewardFeedbackAcceptedSequence)return;
            rewardFeedbackAcceptedSequence=after.sequence;rewardFeedbackPending=null;
            if(rewardFeedbackReduced)return;
            EmitRewardFeedback(new[]{new RewardFeedbackItem(RewardFeedbackCurrency.Diamonds,credit)},source);
        }
    }
}
