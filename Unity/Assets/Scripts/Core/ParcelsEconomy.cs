using System;
using System.Linq;

namespace JunkToNeon.Core
{
    public static class ParcelsEconomy
    {
        static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static bool Unlocked(ModelState state)
        {if(state==null)throw new ArgumentNullException(nameof(state));return ChapterCatalog.Rules.Stages(ChapterEconomy.Project(state).Progress).AbsoluteLevel("pigeon-loft")>=1;}
        public static ParcelsView Project(ModelState state){if(state==null)throw new ArgumentNullException(nameof(state));return new ParcelsView(state);}
        public static ParcelsQuote Quote(ModelState state,string action,long expectedInstance=0,int tokenId=-1,int targetSlot=-1)
        {
            GameLogic.Validate(state);Need(state.pending==null&&state.sequence<ExpansionResourceReceipt.OrderSequenceMaximum,"Parcels requires confirmed unexhausted progress");
            Need(!ChapterEconomy.Project(state).TransitionPending,"Acknowledge era transition before Parcels");
            Need(ParcelsCatalog.Action(action),"Unknown Parcels action");var a=ArcadeEconomy.Data(ExpansionEconomy.Project(state));var p=a.Parcels;var run=p?.Run;
            string board,result;int credit=0;
            if(action=="start")
            {
                Need(expectedInstance==0&&tokenId==-1&&targetSlot==-1&&Unlocked(state)&&a.Tickets>=1&&a.Run==null&&run==null,"Parcels Start requires Pigeon Loft I, one Ticket and no active round");
                board=ParcelsCatalog.BoardIds[p?.NextBoardIndex??0];result="----";
            }
            else
            {
                Need(run!=null&&expectedInstance>0&&run.Instance==expectedInstance,"Stale/missing Parcels round");board=run.BoardId;result=ParcelsCatalog.SlotsToken(run.Slots);
                if(action=="place")result=ParcelsCatalog.SlotsToken(run.Place(tokenId,targetSlot).Slots);
                else
                {
                    Need(tokenId==-1&&targetSlot==-1,"Parcels action has no placement payload");
                    if(action=="finish")run.Finish();
                    else if(action=="claim"){Need(run.Ready,"Claim requires Ready Parcels result");credit=run.ReadyReward;Need(a.Diamonds<=long.MaxValue-credit,"Diamond reward would overflow");}
                }
            }
            return new ParcelsQuote(action,state.sequence,expectedInstance,tokenId,targetSlot,board,result,credit,ExpansionEconomy.Fingerprint(state));
        }
        static ExpansionState Apply(ExpansionState before,ParcelsQuote q)
        {
            var e=new ExpansionDraft(before){FormatVersion=3};if(e.AdoptedSequence==0)e.AdoptedSequence=q.Sequence;
            var a=new ArcadeDraft(ArcadeEconomy.Data(before));if(a.AdoptedSequence==0)a.AdoptedSequence=q.Sequence;
            var p=a.Parcels;var run=p?.Run;int next=p?.NextBoardIndex??0;var claim=p?.LastClaim;
            if(q.Action=="start"){run=new ParcelsRun(q.Sequence,q.BoardId,new[]{-1,-1,-1,-1},false,-1);next=(next+1)%4;a.Tickets--;}
            else if(q.Action=="place")run=run.Place(q.TokenId,q.TargetSlot);
            else if(q.Action=="finish")run=run.Finish();
            else if(q.Action=="claim"){a.Diamonds=checked(a.Diamonds+q.DiamondsCredit);claim=q;run=null;}
            else run=null;
            a.Parcels=new ParcelsState(next,run,q,claim);e.ArcadeData=a.Freeze();return e.Freeze();
        }
        public static ModelState Reserve(ModelState state,ParcelsQuote quote,long at=0)
        {
            GameLogic.Validate(state);Need(state.pending==null&&quote!=null,"Parcels requires retained confirmed quote");
            var p=ArcadeEconomy.Data(ExpansionEconomy.Project(state)).Parcels;var retained=quote.Action=="claim"?p?.LastClaim:p?.LastCommand;
            if(retained!=null&&retained.Sequence==quote.Sequence&&retained.Key==quote.Key)
            {Need(ParcelsCodec.QuoteToken(retained)==ParcelsCodec.QuoteToken(quote),"Conflicting Parcels replay");return state;}
            Need(state.sequence==quote.ExpectedSequence,"Stale Parcels confirmation");
            var expected=Quote(state,quote.Action,quote.ExpectedInstance,quote.TokenId,quote.TargetSlot);Need(expected.Key==quote.Key,"Parcels confirmation context changed");
            if(quote.Action=="place"&&ParcelsCatalog.SlotsToken(p.Run.Slots)==quote.ResultSlots)return state;
            var before=ExpansionEconomy.Project(state);var after=Apply(before,quote);
            var op=new ExpansionOperation("parcels",quote.Sequence,at,state.expansion==null,before,after,state.seed,state.seed,state.coins,state.coins,null,null,null,quote);
            var next=state.Copy();next.pending=new PendingOperation{kind="parcels",sequence=op.Sequence,at=at,expansion=op};next.revision=checked(state.revision+1);
            ExpansionEconomy.Validate(next);return next;
        }
        internal static void ValidateDedicated(ModelState state,ExpansionOperation op)
        {
            Need(op.ParcelsQuote!=null&&op.RuleRevision==ParcelsQuote.Revision&&op.CoinsBefore==state.coins&&op.CoinsAfter==state.coins&&op.SeedAfter==state.seed,"Invalid Parcels authority");
            var confirmed=state.Copy();confirmed.pending=null;confirmed.revision=checked(state.sequence*2);var frozen=op.ParcelsQuote;
            var q=Quote(confirmed,frozen.Action,frozen.ExpectedInstance,frozen.TokenId,frozen.TargetSlot);
            Need(q.Sequence==op.Sequence&&q.Key==frozen.Key&&ExpansionCodec.EncodeState(Apply(ExpansionEconomy.Project(confirmed),q))==ExpansionCodec.EncodeState(op.After),"Frozen Parcels result mismatch");
            Need(q.Action!="place"||ParcelsCatalog.SlotsToken(ArcadeEconomy.Data(ExpansionEconomy.Project(confirmed)).Parcels.Run.Slots)!=q.ResultSlots,"No-op Parcels placement must not be journaled");
        }
        internal static void Bounds(ParcelsState p,ArcadeState a,long ceiling)
        {
            if(p==null)return;
            Need(p.LastCommand.Sequence>=a.AdoptedSequence&&p.LastCommand.Sequence<=ceiling&&(p.LastClaim==null||p.LastClaim.Sequence>=a.AdoptedSequence&&p.LastClaim.Sequence<=ceiling)&&
                (p.Run==null||p.Run.Instance>=a.AdoptedSequence&&p.Run.Instance<=ceiling),"Parcels history bounds mismatch");
        }
        internal static void History(ParcelsState live,ParcelsState old,long spinSequence)
        {
            if(old==null)
            {Need(live==null||live.LastCommand.Sequence>spinSequence,"Parcels history predates its absent retained spin");return;}
            Need(live!=null,"Parcels history regressed");
            Need(live.LastCommand.Sequence==old.LastCommand.Sequence?live.LastCommand.Key==old.LastCommand.Key:live.LastCommand.Sequence>spinSequence,"Parcels command history regressed");
            if(old.LastClaim!=null)Need(live.LastClaim!=null&&(live.LastClaim.Sequence==old.LastClaim.Sequence?live.LastClaim.Key==old.LastClaim.Key:live.LastClaim.Sequence>spinSequence),"Parcels claim history regressed");
        }
    }
}
