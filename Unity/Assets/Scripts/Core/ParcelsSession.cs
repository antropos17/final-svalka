using System;

namespace JunkToNeon.Core
{
    public sealed partial class GameStore
    {
        internal ParcelsView ReadParcels()
        {lock(gate){if(blocked||state==null||state.pending!=null)throw new InvalidOperationException("Parcels requires confirmed healthy progress");return ParcelsEconomy.Project(state);}}
        internal ParcelsQuote ReadParcelsQuote(string action,long sequence,long instance,int token,int slot)
        {
            lock(gate)
            {if(blocked||state==null||state.pending!=null||sequence<0||state.sequence!=sequence)throw new InvalidOperationException("Displayed Parcels view is stale/unavailable");return ParcelsEconomy.Quote(state,action,instance,token,slot);}
        }
        internal ModelState ExecuteParcels(ParcelsQuote quote,long at)
        {
            lock(gate)
            {
                if(blocked||state==null)throw new InvalidOperationException("Parcels mutation blocked: "+Error);
                var pending=ParcelsEconomy.Reserve(state,quote,at);if(ReferenceEquals(pending,state))return state.Copy();
                try{Write(pending);Write(GameLogic.ApplyPending(pending));return state.Copy();}
                catch(Exception error){blocked=true;Error=error.Message;throw;}
            }
        }
    }
    public sealed partial class GameSession
    {
        public ParcelsView Parcels {get{return store.ReadParcels();}}
        public ParcelsQuote GetParcelsQuote(string action,long expectedSequence,long expectedInstance=0,int tokenId=-1,int targetSlot=-1)
        {return store.ReadParcelsQuote(action,expectedSequence,expectedInstance,tokenId,targetSlot);}
        public ModelState ExecuteParcels(ParcelsQuote quote,long at=0){return store.ExecuteParcels(quote,at);}
    }
}
