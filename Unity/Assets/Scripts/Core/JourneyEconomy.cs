using System;
using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Guidance;
using JunkToNeon.Orders;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Core
{
    public static class JourneyEconomy
    {
        static void Need(bool condition,string detail){if(!condition)throw new InvalidOperationException(detail);}
        public static bool IsModern(ModelState s){return s?.journey!=null||s?.pending?.journey!=null||s?.lastSpin?.journey!=null;}
        public static JourneyState Project(ModelState s)
        {
            if(s.journey!=null)return s.journey;
            var w=WaveProgress.Of(s);
            bool expanded=s.chapter==null&&s.houseStage==2&&s.workshopBuilt==1&&w.RecyclerBuilt==1&&w.PigeonLoftBuilt==1;
            return new JourneyState(OrderState.Empty,GuideState.Create(expanded?GuideOrigin.LegacyExpanded:GuideOrigin.Existing),checked(s.sequence+1));
        }
        public static bool Same(JourneyState a,JourneyState b)
        {
            return a!=null&&b!=null&&a.AdoptedSequence==b.AdoptedSequence&&a.CommandSequence==b.CommandSequence&&a.CommandKind==b.CommandKind&&
                a.CommandId==b.CommandId&&a.CommandInstance==b.CommandInstance&&a.CommandCompletion==b.CommandCompletion&&
                OrderStateCodec.Encode(a.Orders)==OrderStateCodec.Encode(b.Orders)&&GuideStateCodec.Encode(a.Guidance)==GuideStateCodec.Encode(b.Guidance);
        }
        static JourneyState Next(JourneyState before,OrderState orders,GuideState guide,string kind=null,string id=null,long sequence=0,long instance=0,long completion=0)
        {
            return new JourneyState(orders,guide,before.AdoptedSequence,sequence==0?before.CommandSequence:sequence,kind??before.CommandKind,
                sequence==0?before.CommandId:id,sequence==0?before.CommandInstance:instance,sequence==0?before.CommandCompletion:completion);
        }
        static OrderContext OrderContext(ChapterState chapter){return new OrderContext(ChapterCatalog.Rules.Stages(chapter.Progress));}
        public static GuideContext GuideContext(ChapterState chapter,JourneyState journey)
        {
            return new GuideContext(ChapterCatalog.Rules.Stages(chapter.Progress),chapter.SelectedProjectId,chapter.Skills.Receipts.Count,
                journey.Guidance.Origin==GuideOrigin.LegacyExpanded,ChapterCatalog.Rules.IsChapterComplete(chapter.Progress),journey.Orders.ActiveOrderId!=null,journey.Orders.Ready);
        }
        static ModelState EconomicShadow(ModelState state)
        {
            var economic=state.Copy();economic.journey=null;
            if(economic.pending!=null)economic.pending.journey=null;
            if(economic.lastSpin!=null)economic.lastSpin.journey=null;
            return economic;
        }
        static void StateBounds(JourneyState state,long sequence,long adoptedCeiling)
        {
            var o=state.Orders;var g=state.Guidance;
            Need(state.AdoptedSequence<=adoptedCeiling&&state.CommandSequence<=sequence&&state.CommandInstance<=sequence&&state.CommandCompletion<=sequence,"Future journey command/adoption marker");
            Need(o.InstanceSequence<=sequence&&o.Cursor<=sequence&&o.LastCommandSequence<=sequence&&(o.LastEvent==null||o.LastEvent.OperationSequence<=sequence)&&
                (o.LatestClaim==null||o.LatestClaim.ClaimSequence<=sequence),"Future order history");
            Need(g.LastObservedSequence<=sequence&&g.LastAckSequence<=sequence&&(g.LastCompletion==null||g.LastCompletion.Sequence<=sequence),"Future guidance history");
            if(state.CommandSequence!=0)
            {
                if(state.CommandKind=="guide-ack")
                {
                    Need(state.CommandInstance==0&&state.CommandSequence==g.LastAckSequence&&state.CommandId==g.LastAckHintId,"Conflicting guidance command marker");
                    Need(state.CommandId==GuideIds.Completion?state.CommandCompletion>0:state.CommandCompletion==0,"Invalid completion command identity");
                }
                else
                {
                    OrderCatalog.Get(state.CommandId);Need(state.CommandCompletion==0,"Order command has completion metadata");
                    if(state.CommandKind=="order-claim")Need(o.LatestClaim!=null&&o.LatestClaim.ClaimSequence==state.CommandSequence&&o.LatestClaim.OrderId==state.CommandId&&o.LatestClaim.InstanceSequence==state.CommandInstance,"Conflicting claim marker");
                    else if(state.CommandKind=="order-accept")Need(o.InstanceSequence==state.CommandSequence&&o.ActiveOrderId==state.CommandId&&state.CommandInstance==0,"Conflicting acceptance marker");
                    else Need(o.ActiveOrderId==null&&o.InstanceSequence==state.CommandInstance&&o.LastCommandSequence==state.CommandSequence,"Conflicting cancellation marker");
                }
            }
        }
        static string Completed(ChapterOperation op)
        {
            if(op.Kind!="spin"&&op.Kind!="select")return null;
            var a=ChapterCatalog.Rules.Stages(op.Before.Progress);var b=ChapterCatalog.Rules.Stages(op.After.Progress);
            foreach(string family in FamilyIds.All)if(b.AbsoluteLevel(family)>a.AbsoluteLevel(family))return ChapterIds.Project(a.EraId,family,b.LocalLevel(family));
            return null;
        }
        static JourneyState Compose(ChapterOperation chapter,JourneyState before,bool manual,int tutorial,out string orderKey)
        {
            orderKey="";var orders=before.Orders;var guide=before.Guidance;
            if(chapter.Kind=="spin"&&orders.ActiveOrderId!=null)
            {
                int deliveries=checked((int)(chapter.Effects.Baseline.DeliveryCoins/3));
                var symbols=chapter.Symbols;
                var observed=new OrderEvent(chapter.Sequence,orders.Cursor,new SpinOutcome(symbols[0],symbols[1],symbols[2]),deliveries);
                var proposed=OrderEngine.PrepareObserve(OrderContext(chapter.Before),orders,observed,chapter.Sequence,chapter.CoinsAfter);
                Need(proposed.RewardCoins==0&&proposed.CoinsBefore==chapter.CoinsAfter&&proposed.CoinsAfter==chapter.CoinsAfter,"Order observation changed economic credit");
                orders=proposed.After;orderKey=proposed.Key;
            }
            GuideOperationKind kind;
            if(chapter.Kind=="spin")kind=GuideOperationKind.ForegroundSpin;
            else if(chapter.Kind=="select")kind=GuideOperationKind.ProjectSelection;
            else if(chapter.Kind=="skill")kind=GuideOperationKind.SkillPurchase;
            else if(chapter.Kind=="enter-era")kind=GuideOperationKind.EraEntry;
            else{Need(!manual&&tutorial==0,"Unexpected guidance facts on protocol command");return Next(before,orders,guide);}
            var afterOrders=Next(before,orders,guide);
            var observation=new GuideObservation(chapter.Sequence,kind,GuideContext(chapter.Before,before),GuideContext(chapter.After,afterOrders),
                chapter.ActionId??"spin.free",Completed(chapter),manual,tutorial);
            guide=GuideFlow.Observe(guide,observation).After;
            return Next(before,orders,guide);
        }
        static JourneyState Dedicated(ModelState s,JourneyOperation op,out long reward,out string key,out bool noOp)
        {
            var before=op.Before;reward=0;key="";noOp=false;var chapter=ChapterEconomy.Project(s);var context=OrderContext(chapter);
            if(op.Kind=="guide-ack")
            {
                Need(op.InstanceSequence==0,"Unexpected guidance order identity");
                var p=GuideFlow.Acknowledge(before.Guidance,GuideContext(chapter,before),op.ActionId,op.Sequence,op.CompletionSequence);
                noOp=!p.Changed;return Next(before,before.Orders,p.After,op.Kind,op.ActionId,op.Sequence,0,op.CompletionSequence);
            }
            Need(op.CompletionSequence==0,"Unexpected order completion acknowledgement");
            OrderProposal proposal;
            if(op.Kind=="order-accept")
            {
                Need(op.InstanceSequence==0&&before.Orders.ActiveOrderId==null,"An order is already active");
                proposal=OrderEngine.PrepareAccept(context,before.Orders,OrderEngine.QuoteAccept(context,before.Orders,op.ActionId),op.Sequence,op.CoinsBefore);
            }
            else if(op.Kind=="order-cancel")
            {
                Need(before.Orders.ActiveOrderId==op.ActionId&&before.Orders.InstanceSequence==op.InstanceSequence,"Stale cancellation instance");
                proposal=OrderEngine.PrepareCancel(context,before.Orders,OrderEngine.QuoteCancel(context,before.Orders),op.Sequence,op.CoinsBefore);
            }
            else
            {
                Need(op.Kind=="order-claim","Unknown journey command");
                proposal=OrderEngine.PrepareClaim(context,before.Orders,op.ActionId,op.InstanceSequence,op.Sequence,op.CoinsBefore);
            }
            reward=proposal.RewardCoins;key=proposal.Key;noOp=proposal.NoOp;
            Need(proposal.CoinsBefore==op.CoinsBefore&&proposal.CoinsAfter==checked(op.CoinsBefore+reward),"Invalid order wallet delta");
            return Next(before,proposal.After,before.Guidance,op.Kind,op.ActionId,op.Sequence,op.InstanceSequence,0);
        }
        static void Frozen(ModelState state,JourneyOperation op,ChapterOperation chapter)
        {
            StateBounds(op.Before,op.Sequence-1,op.Sequence);StateBounds(op.After,op.Sequence,op.Sequence);
            JourneyState expected;string key;long reward=0;
            if(op.Composed)
            {
                Need(chapter!=null&&op.Kind==chapter.Kind&&op.Sequence==chapter.Sequence&&op.At==chapter.At&&op.ActionId==chapter.ActionId&&
                    op.InstanceSequence==0&&op.CompletionSequence==0&&op.CoinsBefore==chapter.CoinsBefore&&op.CoinsAfter==chapter.CoinsAfter&&op.Seed==chapter.SeedBefore,"Conflicting journey/chapter descriptor");
                Need((!op.ManualSpin||op.Kind=="spin")&&(op.Before.Guidance.Origin==GuideOrigin.Fresh||op.TutorialBits==0),"Invented tutorial facts");
                expected=Compose(chapter,op.Before,op.ManualSpin,op.TutorialBits,out key);
            }
            else
            {
                Need(chapter==null&&!op.ManualSpin&&op.TutorialBits==0&&op.Seed==state.seed,"Dedicated journey command changed RNG/context");
                expected=Dedicated(state,op,out reward,out key,out bool noOp);Need(!noOp,"No-op command must not be journaled");
                Need(op.CoinsAfter==checked(op.CoinsBefore+reward),"Frozen journey credit mismatch");
            }
            Need(op.Reward==reward&&op.OrderProposalKey==key&&Same(op.After,expected),"Frozen journey metadata delta mismatch");
        }
        public static void Validate(ModelState state)
        {
            if(ExpansionEconomy.IsModern(state)){ExpansionEconomy.Validate(state);return;}
            Need(state?.journey!=null,"Missing journey aggregate");var economic=EconomicShadow(state);var pending=state.pending;
            bool dedicated=pending?.journey!=null&&!pending.journey.Composed;
            if(dedicated){Need(pending.chapter==null&&pending.wave==null,"Dedicated journey has economic descriptor");economic.pending=null;economic.revision=checked(economic.revision-1);}
            GameLogic.Validate(economic);
            Need(state.revision==state.sequence*2+(pending==null?0:1),"Invalid journey revision");
            StateBounds(state.journey,state.sequence,state.sequence+(pending==null?0:1));
            var context=OrderContext(ChapterEconomy.Project(state));
            Need(context.Unlocked||state.journey.Orders.ActiveOrderId==null&&state.journey.Orders.LatestClaim==null,"Orders before chapter completion");
            if(state.journey.Guidance.LastCompletion!=null)Need(ChapterEconomy.Project(state).Progress.CompletedUpgradeIds.Contains(state.journey.Guidance.LastCompletion.ProjectId),"Guidance completion is not owned");
            if(state.lastSpin?.journey!=null)
            {
                var receipt=state.lastSpin;Need(receipt.chapter!=null&&receipt.journey.Composed&&receipt.journey.Kind=="spin"&&receipt.journey.Sequence==receipt.sequence,"Invalid journey spin receipt");
                Frozen(state,receipt.journey,receipt.chapter);
                Need(state.journey.AdoptedSequence==receipt.journey.After.AdoptedSequence&&state.journey.Guidance.Origin==receipt.journey.After.Guidance.Origin&&
                    state.journey.Guidance.LastObservedSequence>=receipt.journey.After.Guidance.LastObservedSequence,"Journey history regressed after spin");
            }
            else if(state.lastSpin!=null)Need(state.lastSpin.sequence<state.journey.AdoptedSequence,"Missing journey spin descriptor");
            if(pending==null)return;
            var op=pending.journey;
            Need(op!=null&&pending.sequence==state.sequence+1&&op.Sequence==pending.sequence&&op.Kind==pending.kind&&op.At==pending.at&&op.CoinsBefore==state.coins&&op.Seed==state.seed&&Same(state.journey,op.Before),"Missing/conflicting journey pending descriptor");
            Need(op.Before.AdoptedSequence<=state.sequence||op.Before.AdoptedSequence==op.Sequence,"Invalid adoption boundary");
            if(dedicated)Need(pending.target==0&&pending.symbols==null&&pending.seedAfter==0,"Dedicated journey has spin/target payload");
            Frozen(state,op,pending.chapter);
        }
        public static ModelState ReserveChapter(ModelState state,string kind,string action,long sequence,long at=0,SkillPurchaseRequest request=null,bool manualSpin=false,int tutorialBits=0)
        {return ExpansionEconomy.Compose(state,s=>ReserveChapterHistorical(s,kind,action,sequence,at,request,manualSpin,tutorialBits));}
        internal static ModelState ReserveChapterHistorical(ModelState state,string kind,string action,long sequence,long at=0,SkillPurchaseRequest request=null,bool manualSpin=false,int tutorialBits=0)
        {
            GameLogic.Validate(state);var economic=EconomicShadow(state);
            var reserved=ChapterEconomy.ReserveHistorical(economic,kind,action,sequence,at,request);
            if(ReferenceEquals(reserved,economic))return state;
            var before=Project(state);manualSpin=kind=="spin"&&manualSpin;
            if(kind!="spin"&&kind!="select"&&kind!="skill"&&kind!="enter-era")tutorialBits=0;
            else if(before.Guidance.Origin==GuideOrigin.Fresh&&state.autoHintSeen==1)tutorialBits|=(int)TutorialSteps.AutoInformed;
            if(before.Guidance.Origin!=GuideOrigin.Fresh)tutorialBits=0;
            var chapter=reserved.pending.chapter;var after=Compose(chapter,before,manualSpin,tutorialBits,out string key);
            reserved.journey=before;
            if(reserved.lastSpin!=null)reserved.lastSpin.journey=state.lastSpin?.journey;
            reserved.pending.journey=new JourneyOperation(1,JourneyState.RuleRevision,kind,sequence,at,chapter.ActionId,0,0,chapter.CoinsBefore,chapter.CoinsAfter,chapter.SeedBefore,0,true,manualSpin,tutorialBits,key,before,after);
            Validate(reserved);return reserved;
        }
        public static ModelState ReserveLegacy(ModelState state,string kind,int target,long sequence,long at)
        {return ExpansionEconomy.Compose(state,s=>ReserveLegacyHistorical(s,kind,target,sequence,at));}
        internal static ModelState ReserveLegacyHistorical(ModelState state,string kind,int target,long sequence,long at)
        {
            if(kind=="select"){Need(ChapterEconomy.Project(state).Progress.EraId==EraIds.Junkyard,"Legacy target does not name an era2 project");return ReserveChapterHistorical(state,kind,ChapterCatalog.LegacyProject(target),sequence,at);}
            if(kind=="upgrade")throw new InvalidOperationException("Legacy quote is stale; use the current skill offer");
            return ReserveChapterHistorical(state,kind,null,sequence,at,manualSpin:kind=="spin");
        }
        public static ModelState Reserve(ModelState state,string kind,string id,long instance,long sequence,long completion=0,long at=0)
        {return ExpansionEconomy.Compose(state,s=>ReserveHistorical(s,kind,id,instance,sequence,completion,at));}
        internal static ModelState ReserveHistorical(ModelState state,string kind,string id,long instance,long sequence,long completion=0,long at=0)
        {
            GameLogic.Validate(state);Need(state.pending==null,"Pending recovery required");var before=Project(state);
            // A latest exact command replay never modifies a newer live aggregate.
            if(sequence<=state.sequence&&sequence>=1&&before.CommandSequence==sequence)
            {Need(before.CommandKind==kind&&before.CommandId==id&&before.CommandInstance==instance&&before.CommandCompletion==completion,"Conflicting journey command replay");return state;}
            var placeholder=new JourneyOperation(1,JourneyState.RuleRevision,kind,sequence,at,id,instance,completion,state.coins,state.coins,state.seed,0,false,false,0,"",before,before);
            var after=Dedicated(state,placeholder,out long reward,out string key,out bool noOp);
            if(noOp)return state;
            Need(sequence==state.sequence+1,"Journey command sequence gap/stale identity");
            var op=new JourneyOperation(1,JourneyState.RuleRevision,kind,sequence,at,id,instance,completion,state.coins,checked(state.coins+reward),state.seed,reward,false,false,0,key,before,after);
            var next=state.Copy();next.journey=before;next.pending=new PendingOperation{kind=kind,sequence=sequence,at=at,journey=op};next.revision=checked(state.revision+1);
            Validate(next);return next;
        }
        public static ModelState ApplyPending(ModelState state)
        {
            if(ExpansionEconomy.IsModern(state))return ExpansionEconomy.ApplyPending(state);
            Validate(state);if(state.pending==null)return state;var op=state.pending.journey;ModelState next;
            if(op.Composed)next=GameLogic.ApplyPending(EconomicShadow(state));
            else
            {
                next=state.Copy();next.coins=checked(state.coins+op.Reward);next.pending=null;next.sequence=op.Sequence;next.revision=checked(next.sequence*2);
                if(op.At>next.lastSettledAt)next.lastSettledAt=op.At;
            }
            next.journey=op.After;
            if(next.lastSpin!=null)next.lastSpin.journey=op.Kind=="spin"?op:state.lastSpin?.journey;
            Validate(next);return next;
        }
    }
}
