using System;

namespace JunkToNeon.Core
{
    public static class ArcadeEconomy
    {
        static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        internal static ArcadeState Data(ExpansionState state){return state.ArcadeData??ArcadeState.Empty;}
        public static ArcadeView Project(ModelState confirmed)
        {if(confirmed==null)throw new ArgumentNullException(nameof(confirmed));return new ArcadeView(confirmed.sequence,Data(ExpansionEconomy.Project(confirmed)));}
        public static ArcadeQuote Quote(ModelState confirmed,string action,string id,long expectedInstance=0,int target=-1)
        {
            GameLogic.Validate(confirmed);Need(confirmed.pending==null&&confirmed.sequence<ExpansionResourceReceipt.OrderSequenceMaximum,"Arcade requires confirmed unexhausted sequence");
            Need(!ChapterEconomy.Project(confirmed).TransitionPending,"Acknowledge era transition before Arcade commands");
            Need(ArcadeCatalog.Action(action),"Unknown Arcade action");var expansion=ExpansionEconomy.Project(confirmed);var a=Data(expansion);var run=a.Run;
            int parts=0,potatoes=0,tickets=0,addParts=0,addPotatoes=0,reward=0;long diamonds=0;
            if(action=="buy")
            {
                Need(expectedInstance==0&&target==-1,"Purchase has no run/target payload");var offer=ArcadeCatalog.Get(id);
                if(offer.Kind=="slot")Need(offer.Level==a.SlotLevel+1&&(offer.Level!=3||expansion.BonusUnlocked),"Slot level/prerequisite locked");
                if(offer.ArmsBonusBoost)Need(expansion.BonusUnlocked&&!expansion.BoostArmed,"Single bonus boost is unavailable/already armed");
                Need(expansion.PartsCap-expansion.Parts>=offer.PartsCredit&&expansion.PotatoesCap-expansion.Potatoes>=offer.PotatoesCredit,"Resource pack requires full headroom");
                parts=offer.PartsCost;potatoes=offer.PotatoesCost;diamonds=offer.DiamondsCost;addParts=offer.PartsCredit;addPotatoes=offer.PotatoesCredit;
            }
            else if(action=="start")
            {Need(ArcadeCatalog.Game(id)&&expectedInstance==0&&target==-1&&run==null&&a.Parcels?.Run==null,"Arcade start requires a known game and no active/Ready run");tickets=EconomyRules.ArcadeStartTickets;}
            else
            {
                Need(run!=null&&run.Id==id&&run.Instance==expectedInstance&&expectedInstance>0,"Stale/missing Arcade run");
                if(action=="claim"){Need(target==-1&&run.Ready,"Claim requires explicit Ready result");reward=run.Reward;Need(a.Diamonds<=long.MaxValue-reward,"Diamond reward would overflow");}
                else if(action=="abandon")Need(target==-1,"Abandon has no target payload");
                else run.Move(action,target); // Pure validation: the accepted move is frozen only on execution.
            }
            Need(expansion.Parts>=parts&&expansion.Potatoes>=potatoes&&a.Diamonds>=diamonds&&a.Tickets>=tickets,"Insufficient Arcade resources");
            return new ArcadeQuote(confirmed.sequence,action,id,expectedInstance,run?.Cursor??0,run?.Phase??"none",target,parts,potatoes,diamonds,tickets,addParts,addPotatoes,reward,ExpansionEconomy.Fingerprint(confirmed));
        }
        internal static ExpansionState Apply(ExpansionState before,ArcadeQuote q,long coins)
        {
            var e=new ExpansionDraft(before){FormatVersion=3};if(e.AdoptedSequence==0)e.AdoptedSequence=q.Sequence;
            var a=new ArcadeDraft(Data(before));if(a.AdoptedSequence==0)a.AdoptedSequence=q.Sequence;
            e.Parts=checked(e.Parts-q.PartsCost+q.PartsCredit);e.Potatoes=checked(e.Potatoes-q.PotatoesCost+q.PotatoesCredit);
            a.Diamonds=checked(a.Diamonds-q.DiamondsCost+q.DiamondsCredit);a.Tickets-=q.TicketsCost;
            if(q.Action=="buy")
            {var offer=ArcadeCatalog.Get(q.Id);if(offer.Kind=="slot")a.SlotLevel=offer.Level;if(offer.ArmsBonusBoost)e.BoostArmed=true;}
            else if(q.Action=="start")
            {a.Run=ArcadeRun.Start(q.Id,q.Sequence,a.ChallengeSeed);a.ChallengeSeed=a.Run.SeedAfter;}
            else if(q.Action=="claim"){a.LastClaim=q;a.Run=null;}
            else if(q.Action=="abandon")a.Run=null;
            else a.Run=a.Run.Move(q.Action,q.Target);
            a.LastCommand=q;a.LastEvent=new ArcadeEvent(q.Sequence,q.Action,q.ExpectedInstance,q.PartsCredit,q.PotatoesCredit,0,0,q.DiamondsCredit,0,0,0,0,coins,coins);
            e.ArcadeData=a.Freeze();return e.Freeze();
        }
        public static ModelState Reserve(ModelState confirmed,ArcadeQuote quote,long at=0)
        {
            GameLogic.Validate(confirmed);Need(confirmed.pending==null&&quote!=null,"Arcade requires confirmed retained quote");var a=Data(ExpansionEconomy.Project(confirmed));
            var retained=quote.Action=="claim"?a.LastClaim:a.LastCommand;
            if(retained!=null&&retained.Sequence==quote.Sequence&&retained.Key==quote.Key)
            {Need(ArcadeCodec.QuoteToken(retained)==ArcadeCodec.QuoteToken(quote),"Conflicting retained Arcade replay");return confirmed;}
            Need(confirmed.sequence==quote.ExpectedSequence,"Stale Arcade sequence");
            var expected=Quote(confirmed,quote.Action,quote.Id,quote.ExpectedInstance,quote.Target);Need(expected.Key==quote.Key,"Arcade quote context changed");
            var before=ExpansionEconomy.Project(confirmed);var after=Apply(before,quote,confirmed.coins);
            var op=new ExpansionOperation("arcade",quote.Sequence,at,confirmed.expansion==null,before,after,confirmed.seed,confirmed.seed,confirmed.coins,confirmed.coins,null,quote);
            var next=confirmed.Copy();next.pending=new PendingOperation{kind="arcade",sequence=quote.Sequence,at=at,expansion=op};next.revision=checked(confirmed.revision+1);
            ExpansionEconomy.Validate(next);return next;
        }
        internal static void ValidateDedicated(ModelState state,ExpansionOperation op)
        {
            Need(op.RuleRevision==ExpansionCatalog.ArcadeRuleRevision&&op.ArcadeQuote!=null&&op.Quote==null&&op.CoinsBefore==state.coins&&op.CoinsAfter==state.coins&&op.SeedAfter==state.seed,"Arcade dedicated authority mismatch");
            var confirmed=state.Copy();confirmed.pending=null;confirmed.revision=checked(state.sequence*2);
            var q=Quote(confirmed,op.ArcadeQuote.Action,op.ArcadeQuote.Id,op.ArcadeQuote.ExpectedInstance,op.ArcadeQuote.Target);
            Need(q.Sequence==op.Sequence&&q.Key==op.ArcadeQuote.Key&&ExpansionCodec.EncodeState(Apply(ExpansionEconomy.Project(confirmed),q,confirmed.coins))==ExpansionCodec.EncodeState(op.After),"Frozen Arcade command result mismatch");
        }
        internal static ArcadeState Spin(ArcadeState before,long sequence,ExpansionResourceReceipt credit,int slotParts,int slotPotatoes,ArcadeMoney money)
        {
            var d=new ArcadeDraft(before);if(d.AdoptedSequence==0)d.AdoptedSequence=sequence;
            d.ForegroundSpins=checked(d.ForegroundSpins+1);d.TicketProgress++;int tickets=0;
            if(d.TicketProgress==EconomyRules.ArcadeTicketCadence){d.TicketProgress=0;if(d.Tickets<EconomyRules.ArcadeTicketCap){d.Tickets++;tickets=1;}}
            d.LastResourceReceipt=new ArcadeEvent(sequence,"spin",d.Run?.Instance??d.Parcels?.Run?.Instance??0,credit.Parts,credit.Potatoes,credit.Energy,tickets,0,money.Income,money.Debit,slotParts,slotPotatoes,money.Before,money.After);
            d.LastEvent=d.LastResourceReceipt;return d.Freeze();
        }
        internal static void Bounds(ArcadeState a,ExpansionState expansion,long ceiling)
        {
            Need(a!=null&&a.AdoptedSequence>=expansion.AdoptedSequence&&a.AdoptedSequence<=ceiling&&a.ForegroundSpins<=expansion.ForegroundSpins&&a.ForegroundSpins<=ceiling-a.AdoptedSequence+1,"Arcade adoption/history bound mismatch");
            Need((a.Run==null||a.Run.Instance<=ceiling)&&(a.LastClaim==null||a.LastClaim.Sequence<=ceiling)&&(a.LastCommand==null||a.LastCommand.Sequence<=ceiling)&&
                (a.LastEvent==null||a.LastEvent.Sequence<=ceiling)&&(a.LastResourceReceipt==null||a.LastResourceReceipt.Sequence<=ceiling),"Future Arcade receipt");
            ParcelsEconomy.Bounds(a.Parcels,a,ceiling);
            if(a.LastResourceReceipt!=null)
            {
                var r=expansion.LastResourceReceipt;Need(r!=null&&r.Sequence==a.LastResourceReceipt.Sequence&&r.Parts==a.LastResourceReceipt.PartsAdded&&r.Potatoes==a.LastResourceReceipt.PotatoesAdded&&r.Energy==a.LastResourceReceipt.EnergyAdded,"Arcade totals disagree with the single resource receipt");
            }
        }
        internal static void History(ArcadeState live,ArcadeState spin)
        {
            ParcelsEconomy.History(live.Parcels,spin.Parcels,spin.LastResourceReceipt?.Sequence??0);
            Need(live.AdoptedSequence==spin.AdoptedSequence&&live.ForegroundSpins==spin.ForegroundSpins&&live.TicketProgress==spin.TicketProgress&&live.SlotLevel>=spin.SlotLevel&&ArcadeCodec.SameEvent(live.LastResourceReceipt,spin.LastResourceReceipt),"Arcade history regressed after spin");
            if(spin.LastClaim!=null)Need(live.LastClaim!=null&&(live.LastClaim.Sequence==spin.LastClaim.Sequence?live.LastClaim.Key==spin.LastClaim.Key:live.LastClaim.Sequence>spin.LastResourceReceipt.Sequence),"Arcade claim history regressed");
            if(spin.LastCommand!=null)Need(live.LastCommand!=null&&(live.LastCommand.Sequence==spin.LastCommand.Sequence?live.LastCommand.Key==spin.LastCommand.Key:live.LastCommand.Sequence>spin.LastResourceReceipt.Sequence),"Arcade command history regressed");
        }
    }
    internal sealed class ArcadeMoney
    {
        internal long Before,After,Income,Debit;
        internal static ArcadeMoney Pending(ModelState state,PendingOperation p)
        {
            var m=Components(p.symbols,p.chapter,p.wave,p.kind=="spin"&&state.workshopBuilt==1?1:0);
            m.Before=state.coins;
            if(p.chapter!=null)m.After=p.chapter.CoinsAfter;
            else
            {
                long gross=checked(state.coins+m.Income);long price=state.selectedStage==0?0:p.wave?.Price??GameLogic.TargetPrice(state.selectedStage);
                m.Debit=price>0&&gross>=price?price:0;m.After=checked(gross-m.Debit);
            }
            return m;
        }
        static ArcadeMoney Components(int[] symbols,ChapterOperation chapter,WaveOperation wave,int workshop)
        {
            var m=new ArcadeMoney();
            if(chapter!=null){m.Income=checked((long)chapter.BaseCoins+chapter.WorkshopCoins+chapter.Effects.TotalCoins);m.Debit=chapter.After.Progress.CompletedUpgradeIds.Count>chapter.Before.Progress.CompletedUpgradeIds.Count?chapter.Price:0;}
            else if(wave!=null)m.Income=checked(wave.BaseCoins+wave.WorkshopCoins+wave.SlotCoins+wave.Effects.TotalCoins);
            else m.Income=checked(GameLogic.Payout(symbols)+workshop);
            return m;
        }
        internal static ArcadeMoney Receipt(SpinReceipt receipt,ArcadeEvent frozen)
        {
            var m=Components(receipt.symbols,receipt.chapter,receipt.wave,receipt.bonus);m.Before=frozen.CoinsBefore;m.After=frozen.CoinsAfter;
            if(receipt.chapter!=null)
            {if(m.Before!=receipt.chapter.CoinsBefore||m.After!=receipt.chapter.CoinsAfter)throw new InvalidOperationException("Arcade/lower wallet receipt mismatch");}
            else
            {
                long price=receipt.wave?.Price??0;long debit=checked(m.Before+m.Income-m.After);
                if(debit!=0&&debit!=price)throw new InvalidOperationException("Arcade legacy frozen debit mismatch");m.Debit=debit;
            }
            if(m.Income!=frozen.CoinIncome||m.Debit!=frozen.ConstructionDebit)throw new InvalidOperationException("Arcade income/debit receipt mismatch");return m;
        }
    }
}
