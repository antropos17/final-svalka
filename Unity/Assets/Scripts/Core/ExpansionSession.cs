using System;

namespace JunkToNeon.Core
{
    public sealed partial class GameStore
    {
        internal ExpansionState ReadExpansion()
        {
            lock(gate){if(blocked||state==null||state.pending!=null)throw new InvalidOperationException("Expansion requires a confirmed healthy snapshot");return ExpansionEconomy.Project(state);}
        }
        internal ExpansionQuote ReadExpansionQuote(string action,string id,int slot=0,long? expectedSequence=null)
        {
            lock(gate)
            {
                if(blocked||state==null||state.pending!=null)throw new InvalidOperationException("Expansion quote requires confirmed healthy progress");
                if(expectedSequence.HasValue&&(expectedSequence.Value<0||state.sequence!=expectedSequence.Value))throw new InvalidOperationException("Expansion view is stale");
                return ExpansionEconomy.Quote(state,action,id,slot);
            }
        }
        internal ModelState ExecuteExpansion(ExpansionQuote quote,long at)
        {
            lock(gate)
            {
                if(blocked||state==null)throw new InvalidOperationException("Expansion mutation blocked: "+Error);
                var pending=ExpansionEconomy.Reserve(state,quote,at);
                if(ReferenceEquals(pending,state))return state.Copy();
                try{Write(pending);Write(GameLogic.ApplyPending(pending));return state.Copy();}
                catch(Exception error){blocked=true;Error=error.Message;throw;}
            }
        }
    }
    public sealed partial class GameSession
    {
        public ExpansionState Expansion { get {return store.ReadExpansion();} }
        public ExpansionQuote GetExpansionOffer(string offerId){return store.ReadExpansionQuote("buy",offerId);}
        public ExpansionQuote GetExpansionAction(string action,string id,int slot=0){return store.ReadExpansionQuote(action,id,slot);}
        public ModelState BuyExpansion(ExpansionQuote quote,long at=0)
        {if(quote==null||quote.Action!="buy")throw new ArgumentException("Buy requires a retained purchase quote");return store.ExecuteExpansion(quote,at);}
        public ModelState ExecuteExpansion(ExpansionQuote quote,long at=0){return store.ExecuteExpansion(quote,at);}
        public ModelState EquipExpansionModule(int slot,string id,long expectedSequence,long at=0)
        {return store.ExecuteExpansion(store.ReadExpansionQuote("equip",id,slot,expectedSequence),at);}
        public ModelState SelectExpansionTheme(string id,long expectedSequence,long at=0)
        {return store.ExecuteExpansion(store.ReadExpansionQuote("theme",id,0,expectedSequence),at);}
        public ModelState SelectExpansionSpeed(string id,long expectedSequence,long at=0)
        {return store.ExecuteExpansion(store.ReadExpansionQuote("speed",id,0,expectedSequence),at);}
        public ModelState ArmExpansionBoost(long expectedSequence,long at=0)
        {return store.ExecuteExpansion(store.ReadExpansionQuote("boost",ExpansionCatalog.NextBonusBoost,0,expectedSequence),at);}
        public ModelState SelectExpansionFocus(string id,long expectedSequence,long at=0)
        {return store.ExecuteExpansion(store.ReadExpansionQuote("focus",id,0,expectedSequence),at);}
        public ModelState ApplyExpansionPreset(string id,long expectedSequence,long at=0)
        {return store.ExecuteExpansion(store.ReadExpansionQuote("preset",id,0,expectedSequence),at);}
    }
}
