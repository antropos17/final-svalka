using System;
using JunkToNeon.Guidance;
using JunkToNeon.Orders;

namespace JunkToNeon.Core
{
    public sealed partial class JsonStateCodec
    {
        static string EncodeJourneyState(JourneyState state)
        {
            return "{\"format\":1,\"rule\":"+Q(JourneyState.RuleRevision)+",\"adopted\":"+N(state.AdoptedSequence)+",\"orders\":"+Q(OrderStateCodec.Encode(state.Orders))+
                ",\"guidance\":"+Q(GuideStateCodec.Encode(state.Guidance))+",\"commandSequence\":"+N(state.CommandSequence)+",\"commandKind\":"+Q(state.CommandKind)+
                ",\"commandId\":"+Q(state.CommandId)+",\"commandInstance\":"+N(state.CommandInstance)+",\"commandCompletion\":"+N(state.CommandCompletion)+"}";
        }
        static JourneyState DecodeJourneyState(object raw)
        {
            var data=Object(raw,"format","rule","adopted","orders","guidance","commandSequence","commandKind","commandId","commandInstance","commandCompletion");
            if(Int(data["format"])!=1||Text(data["rule"])!=JourneyState.RuleRevision)throw new FormatException("Unknown journey state revision");
            return new JourneyState(OrderStateCodec.DecodeOrDefault(Text(data["orders"])),GuideStateCodec.Decode(Text(data["guidance"])),Number(data["adopted"]),Number(data["commandSequence"]),
                data["commandKind"]==null?null:Text(data["commandKind"]),data["commandId"]==null?null:Text(data["commandId"]),Number(data["commandInstance"]),Number(data["commandCompletion"]));
        }
        static string EncodeJourneyOperation(JourneyOperation op)
        {
            return "{\"format\":1,\"rule\":"+Q(JourneyState.RuleRevision)+",\"kind\":"+Q(op.Kind)+",\"sequence\":"+N(op.Sequence)+
                ",\"at\":"+N(op.At)+",\"action\":"+Q(op.ActionId)+",\"instance\":"+N(op.InstanceSequence)+",\"completion\":"+N(op.CompletionSequence)+
                ",\"coinsBefore\":"+N(op.CoinsBefore)+",\"coinsAfter\":"+N(op.CoinsAfter)+",\"seed\":"+N(op.Seed)+",\"reward\":"+N(op.Reward)+
                ",\"composed\":"+(op.Composed?"1":"0")+",\"manual\":"+(op.ManualSpin?"1":"0")+",\"tutorial\":"+N(op.TutorialBits)+",\"orderKey\":"+Q(op.OrderProposalKey)+
                ",\"before\":"+EncodeJourneyState(op.Before)+",\"after\":"+EncodeJourneyState(op.After)+"}";
        }
        static JourneyOperation DecodeJourneyOperation(object raw)
        {
            var d=Object(raw,"format","rule","kind","sequence","at","action","instance","completion","coinsBefore","coinsAfter","seed","reward","composed","manual","tutorial","orderKey","before","after");
            int composed=Int(d["composed"]),manual=Int(d["manual"]);
            if(composed<0||composed>1||manual<0||manual>1)throw new FormatException("Invalid journey flag");
            return new JourneyOperation(Int(d["format"]),Text(d["rule"]),Text(d["kind"]),Number(d["sequence"]),Number(d["at"]),d["action"]==null?null:Text(d["action"]),
                Number(d["instance"]),Number(d["completion"]),Number(d["coinsBefore"]),Number(d["coinsAfter"]),UInt(d["seed"]),Number(d["reward"]),
                composed==1,manual==1,Int(d["tutorial"]),Text(d["orderKey"]),DecodeJourneyState(d["before"]),DecodeJourneyState(d["after"]));
        }
    }
}
