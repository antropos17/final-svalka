using System;

namespace JunkToNeon.Core
{
    public sealed partial class GameStore
    {
        internal ArcadeView ReadArcade()
        {lock(gate){if(blocked||state==null||state.pending!=null)throw new InvalidOperationException("Arcade requires confirmed healthy progress");return ArcadeEconomy.Project(state);}}
        internal ArcadeQuote ReadArcadeQuote(string action,string id,long expectedSequence,long expectedInstance,int target)
        {
            lock(gate)
            {
                if(blocked||state==null||state.pending!=null)throw new InvalidOperationException("Arcade quote requires confirmed healthy progress");
                if(expectedSequence<0||state.sequence!=expectedSequence)throw new InvalidOperationException("Displayed Arcade view is stale");
                return ArcadeEconomy.Quote(state,action,id,expectedInstance,target);
            }
        }
        internal ModelState ExecuteArcade(ArcadeQuote quote,long at)
        {
            lock(gate)
            {
                if(blocked||state==null)throw new InvalidOperationException("Arcade mutation blocked: "+Error);
                var pending=ArcadeEconomy.Reserve(state,quote,at);if(ReferenceEquals(pending,state))return state.Copy();
                try{Write(pending);Write(GameLogic.ApplyPending(pending));return state.Copy();}
                catch(Exception error){blocked=true;Error=error.Message;throw;}
            }
        }
    }
    public sealed partial class GameSession
    {
        public ArcadeView Arcade {get{return store.ReadArcade();}}
        public ArcadeQuote GetArcadeQuote(string action,string id,long expectedSequence,long expectedInstance=0,int target=-1)
        {return store.ReadArcadeQuote(action,id,expectedSequence,expectedInstance,target);}
        public ModelState ExecuteArcade(ArcadeQuote quote,long at=0){return store.ExecuteArcade(quote,at);}
    }
}
