using System;

namespace JunkToNeon.Core
{
    // A separate immutable command: old single-purchase quote bytes remain unchanged.
    public sealed class ExpansionBatchQuote
    {
        public const string Revision="expansion.batch.v1";
        public string RuleRevision { get {return Revision;} }
        public string Id { get; }
        public int BatchCount { get; }
        public long ExpectedSequence { get; }
        public long Sequence { get {return checked(ExpectedSequence+1);} }
        public int PartsCost { get; }
        public int PotatoesCost { get; }
        public long CoinsReward { get; }
        public string Key { get; }
        internal string Fingerprint { get; }
        internal ExpansionBatchQuote(string id,int count,long expected,int parts,int potatoes,long reward,string fingerprint)
        {
            if((id!="trade.parts"&&id!="trade.potatoes")||count<2||expected<0||expected>=ExpansionResourceReceipt.OrderSequenceMaximum||!ExpansionCodec.IsKey(fingerprint))
                throw new ArgumentException("Invalid batch exchange quote");
            int units=checked(EconomyRules.ExpansionTradeUnits*count);
            long coins=checked((long)EconomyRules.ExpansionTradeCoins*count);
            if(parts!=(id=="trade.parts"?units:0)||potatoes!=(id=="trade.potatoes"?units:0)||reward!=coins)
                throw new ArgumentException("Invalid batch exchange totals");
            Id=id;BatchCount=count;ExpectedSequence=expected;PartsCost=parts;PotatoesCost=potatoes;CoinsReward=reward;Fingerprint=fingerprint;
            Key=ExpansionCodec.Hash(string.Join("|",Revision,id,ExpansionCodec.N(count),ExpansionCodec.N(expected),ExpansionCodec.N(parts),ExpansionCodec.N(potatoes),ExpansionCodec.N(reward),fingerprint));
        }
    }
    public static partial class ExpansionEconomy
    {
        public static int MaximumTradeBatches(ModelState state,string id)
        {
            GameLogic.Validate(state);Need(state.pending==null,"Exchange requires confirmed progress");
            Need(id=="trade.parts"||id=="trade.potatoes","Unknown exchange resource");
            var e=Project(state);int stock=id=="trade.parts"?e.Parts:e.Potatoes;
            return (int)Math.Min(stock/EconomyRules.ExpansionTradeUnits,(long.MaxValue-state.coins)/EconomyRules.ExpansionTradeCoins);
        }
        public static ExpansionBatchQuote QuoteBatch(ModelState state,string id,int batchCount)
        {
            GameLogic.Validate(state);Need(state.pending==null,"Batch exchange requires confirmed progress");
            Need(state.sequence<ExpansionResourceReceipt.OrderSequenceMaximum,"Expansion sequence exhausted");
            Need(!ChapterEconomy.Project(state).TransitionPending,"Acknowledge era transition before expansion commands");
            Need(batchCount>=2&&batchCount<=MaximumTradeBatches(state,id),"Invalid/unavailable exchange quantity");
            int units=checked(EconomyRules.ExpansionTradeUnits*batchCount);
            return new ExpansionBatchQuote(id,batchCount,state.sequence,id=="trade.parts"?units:0,id=="trade.potatoes"?units:0,
                checked((long)EconomyRules.ExpansionTradeCoins*batchCount),Fingerprint(state));
        }
        static ExpansionState ApplyBatch(ExpansionState before,ExpansionBatchQuote q)
        {
            var d=new ExpansionDraft(before);if(d.AdoptedSequence==0)d.AdoptedSequence=q.Sequence;
            d.Parts=checked(d.Parts-q.PartsCost);d.Potatoes=checked(d.Potatoes-q.PotatoesCost);
            d.LastCommand=new ExpansionCommandReceipt(q.Sequence,q.Key,"buy",q.Id,0);return d.Freeze();
        }
        public static ModelState ReserveBatch(ModelState state,ExpansionBatchQuote quote,long at=0)
        {
            GameLogic.Validate(state);Need(state.pending==null&&quote!=null,"Batch exchange requires a confirmed request");
            var last=state.expansion?.LastCommand;
            if(last!=null&&last.Sequence==quote.Sequence&&last.Key==quote.Key)
            {Need(last.Action=="buy"&&last.Id==quote.Id&&last.Slot==0,"Conflicting batch replay");return state;}
            Need(quote.ExpectedSequence==state.sequence,"Stale batch confirmation");
            var expected=QuoteBatch(state,quote.Id,quote.BatchCount);Need(expected.Key==quote.Key,"Batch confirmation context changed");
            var before=Project(state);var after=ApplyBatch(before,quote);
            var op=new ExpansionOperation("expansion.batch",quote.Sequence,at,state.expansion==null,before,after,state.seed,state.seed,state.coins,
                checked(state.coins+quote.CoinsReward),null,null,quote);
            var next=state.Copy();next.pending=new PendingOperation{kind=op.Kind,sequence=op.Sequence,at=at,expansion=op};next.revision=checked(state.revision+1);
            Validate(next);return next;
        }
        static void ValidateBatch(ModelState state,ExpansionOperation op)
        {
            Need(op.BatchQuote!=null&&op.RuleRevision==ExpansionBatchQuote.Revision&&op.CoinsBefore==state.coins&&op.SeedAfter==state.seed,"Invalid batch authority");
            var confirmed=state.Copy();confirmed.pending=null;confirmed.revision=checked(state.sequence*2);
            var q=QuoteBatch(confirmed,op.BatchQuote.Id,op.BatchQuote.BatchCount);
            Need(q.Key==op.BatchQuote.Key&&q.Sequence==op.Sequence&&Same(ApplyBatch(Project(confirmed),q),op.After)&&op.CoinsAfter==checked(state.coins+q.CoinsReward),
                "Frozen batch request/result mismatch");
        }
    }
}
