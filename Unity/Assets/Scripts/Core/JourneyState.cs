using System;
using JunkToNeon.Orders;
using JunkToNeon.Guidance;

namespace JunkToNeon.Core
{
    // Metadata shares the economic snapshot; it never owns a file or wallet.
    public sealed class JourneyState
    {
        public const string RuleRevision="journey.rules.v1";
        public OrderState Orders { get; }
        public GuideState Guidance { get; }
        public long AdoptedSequence { get; }
        public long CommandSequence { get; }
        public string CommandKind { get; }
        public string CommandId { get; }
        public long CommandInstance { get; }
        public long CommandCompletion { get; }
        public JourneyState(OrderState orders,GuideState guidance,long adoptedSequence,long commandSequence=0,string commandKind=null,string commandId=null,long commandInstance=0,long commandCompletion=0)
        {
            Orders=orders??throw new ArgumentNullException(nameof(orders));Guidance=guidance??throw new ArgumentNullException(nameof(guidance));
            if(adoptedSequence<0||commandSequence<0||commandInstance<0||commandCompletion<0||
                (commandSequence==0?(commandKind!=null||commandId!=null||commandInstance!=0||commandCompletion!=0):
                (commandKind!="order-accept"&&commandKind!="order-cancel"&&commandKind!="order-claim"&&commandKind!="guide-ack")||string.IsNullOrEmpty(commandId)))
                throw new InvalidOperationException("Invalid journey adoption/command marker");
            AdoptedSequence=adoptedSequence;CommandSequence=commandSequence;CommandKind=commandKind;CommandId=commandId;CommandInstance=commandInstance;CommandCompletion=commandCompletion;
        }
        public static JourneyState Fresh(){return new JourneyState(OrderState.Empty,GuideState.Create(GuideOrigin.Fresh),0);}
    }

    public sealed class JourneyOperation
    {
        public const int FormatVersion=1;
        public string Kind { get; }
        public long Sequence { get; }
        public long At { get; }
        public string ActionId { get; }
        public long InstanceSequence { get; }
        public long CompletionSequence { get; }
        public long CoinsBefore { get; }
        public long CoinsAfter { get; }
        public uint Seed { get; }
        public long Reward { get; }
        public bool Composed { get; }
        public bool ManualSpin { get; }
        public int TutorialBits { get; }
        public string OrderProposalKey { get; }
        public JourneyState Before { get; }
        public JourneyState After { get; }
        public JourneyOperation(int format,string rule,string kind,long sequence,long at,string actionId,long instanceSequence,long completionSequence,
            long coinsBefore,long coinsAfter,uint seed,long reward,bool composed,bool manualSpin,int tutorialBits,string orderProposalKey,JourneyState before,JourneyState after)
        {
            if(format!=FormatVersion||rule!=JourneyState.RuleRevision||string.IsNullOrEmpty(kind)||sequence<1||sequence>(long.MaxValue-2)/2||
                at<0||at>OfflineIncome.MaxUtcSeconds||instanceSequence<0||completionSequence<0||coinsBefore<0||coinsAfter<0||seed==0||reward<0||
                tutorialBits<0||tutorialBits>31||orderProposalKey==null||orderProposalKey.Length>256||before==null||after==null)
                throw new InvalidOperationException("Invalid frozen journey envelope");
            Kind=kind;Sequence=sequence;At=at;ActionId=actionId;InstanceSequence=instanceSequence;CompletionSequence=completionSequence;
            CoinsBefore=coinsBefore;CoinsAfter=coinsAfter;Seed=seed;Reward=reward;Composed=composed;ManualSpin=manualSpin;TutorialBits=tutorialBits;
            OrderProposalKey=orderProposalKey;Before=before;After=after;
        }
    }
}
