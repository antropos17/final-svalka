using System;

namespace JunkToNeon.Core
{
    public sealed partial class GameStore
    {
        internal ExpansionBatchQuote ReadExpansionBatchQuote(string id,int count)
        {
            lock(gate)
            {
                if(blocked||state==null||state.pending!=null)throw new InvalidOperationException("Batch quote requires confirmed healthy progress");
                return ExpansionEconomy.QuoteBatch(state,id,count);
            }
        }
        internal ModelState ExecuteExpansionBatch(ExpansionBatchQuote quote,long at)
        {
            lock(gate)
            {
                if(blocked||state==null)throw new InvalidOperationException("Batch exchange blocked: "+Error);
                var pending=ExpansionEconomy.ReserveBatch(state,quote,at);
                if(ReferenceEquals(pending,state))return state.Copy();
                try{Write(pending);Write(GameLogic.ApplyPending(pending));return state.Copy();}
                catch(Exception error){blocked=true;Error=error.Message;throw;}
            }
        }
    }
    public sealed partial class GameSession
    {
        public ExpansionBatchQuote GetExpansionBatchOffer(string offerId,int batchCount){return store.ReadExpansionBatchQuote(offerId,batchCount);}
        public ModelState BuyExpansionBatch(ExpansionBatchQuote quote,long at=0){return store.ExecuteExpansionBatch(quote,at);}
    }
}
