namespace JunkToNeon.Core
{
    public sealed partial class GameStore
    {
        public ModelState ExecuteJourney(string kind,string id,long instance,long sequence,long completion=0,long at=0)
        {
            lock(gate)
            {
                if(blocked||state==null)throw new System.InvalidOperationException("Journey mutation blocked: "+Error);
                var pending=JourneyEconomy.Reserve(state,kind,id,instance,sequence,completion,at);
                if(ReferenceEquals(pending,state))return state.Copy();
                try{Write(pending);Write(GameLogic.ApplyPending(pending));return state.Copy();}
                catch(System.Exception error){blocked=true;Error=error.Message;throw;}
            }
        }
    }
    public sealed partial class GameSession
    {
        public JourneyState Journey { get { return JourneyEconomy.Project(store.State); } }
        public JunkToNeon.Guidance.GuideEvaluation Guidance { get { return JunkToNeon.Guidance.GuideFlow.Query(Journey.Guidance,JourneyEconomy.GuideContext(Chapter,Journey)); } }
        public ModelState AcceptOrder(string id,long sequence,long at=0){return store.ExecuteJourney("order-accept",id,0,sequence,at:at);}
        public ModelState CancelOrder(string id,long instanceSequence,long sequence,long at=0){return store.ExecuteJourney("order-cancel",id,instanceSequence,sequence,at:at);}
        public ModelState ClaimOrder(string id,long instanceSequence,long sequence,long at=0){return store.ExecuteJourney("order-claim",id,instanceSequence,sequence,at:at);}
        public ModelState AcknowledgeGuidance(string hintId,long sequence,long expectedCompletionSequence=0,long at=0){return store.ExecuteJourney("guide-ack",hintId,0,sequence,expectedCompletionSequence,at);}
    }
}
