using System;
using System.Collections.Generic;
using System.Linq;
using JunkToNeon.Contracts;

namespace JunkToNeon.Core
{
    // Module capabilities only: independent of stocks, caps, ordinary production and saved spins.
    public readonly struct ExpansionModuleBonuses
    {
        public int PartsPerTrash { get; }
        public int PartsPerBox { get; }
        public int PotatoesPerPotato { get; }
        public int PartsPerMatchingSpin { get; }
        public int PotatoesPerMatchingSpin { get; }
        public int EnergyPerPigeonSpin { get; }
        internal ExpansionModuleBonuses(bool salvage,bool packing,bool harvest,bool combo,bool dynamo)
        {
            PartsPerTrash=salvage?EconomyRules.ExpansionModuleSymbolYield:0;
            PartsPerBox=packing?EconomyRules.ExpansionModuleSymbolYield:0;
            PotatoesPerPotato=harvest?EconomyRules.ExpansionModuleSymbolYield:0;
            PartsPerMatchingSpin=PotatoesPerMatchingSpin=combo?EconomyRules.ExpansionComboYield:0;
            EnergyPerPigeonSpin=dynamo?EconomyRules.ExpansionDynamoEnergy:0;
        }
    }
    public static partial class ExpansionEconomy
    {
        static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static bool IsModern(ModelState s){return s?.expansion!=null||s?.pending?.expansion!=null||s?.lastSpin?.expansion!=null;}
        public static ExpansionState Project(ModelState s){if(s==null)throw new ArgumentNullException(nameof(s));return s.expansion??ExpansionState.Empty;}
        internal static bool IsAction(string action){return action=="buy"||action=="equip"||action=="theme"||action=="boost"||action=="speed"||action=="focus"||action=="preset";}
        public static IReadOnlyList<string> PreviewPreset(ExpansionState snapshot,string presetId)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            var slots=new string[3];int slot=0;
            foreach(string id in ExpansionCatalog.PresetPriority(presetId))
                if(snapshot.OwnsModule(id)&&slot<snapshot.SocketCount)slots[slot++]=id;
            return Array.AsReadOnly(slots);
        }
        public static ExpansionModuleBonuses ModuleBonuses(ExpansionState snapshot)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            return new ExpansionModuleBonuses(snapshot.Equipped(ExpansionCatalog.Salvage),snapshot.Equipped(ExpansionCatalog.Packing),
                snapshot.Equipped(ExpansionCatalog.Harvest),snapshot.Equipped(ExpansionCatalog.Combo),snapshot.Equipped(ExpansionCatalog.Dynamo));
        }
        public static ExpansionModuleBonuses ModuleBonuses(ExpansionState snapshot,IReadOnlyList<string> modules)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            Need(modules!=null&&modules.Count==3,"Module preview requires exactly three slots");
            var copied=new string[3];
            for(int i=0;i<3;i++)
            {
                string id=modules[i];Need(id==null||i<snapshot.SocketCount&&snapshot.OwnsModule(id),"Module preview requires owned modules and unlocked slots");
                Need(id==null||!copied.Take(i).Contains(id),"Module preview cannot duplicate a module");copied[i]=id;
            }
            return new ExpansionModuleBonuses(copied.Contains(ExpansionCatalog.Salvage),copied.Contains(ExpansionCatalog.Packing),
                copied.Contains(ExpansionCatalog.Harvest),copied.Contains(ExpansionCatalog.Combo),copied.Contains(ExpansionCatalog.Dynamo));
        }
        static bool NoChange(ExpansionState before,ExpansionQuote quote)
        {
            if(quote.Action=="speed")return before.SpeedId==quote.Id;
            if(quote.Action=="focus")return before.BonusFocusId==quote.Id;
            if(quote.Action=="preset")return PreviewPreset(before,quote.Id).SequenceEqual(Enumerable.Range(1,3).Select(before.ModuleAt));
            return false;
        }
        internal static ModelState EconomicShadow(ModelState state)
        {
            if(state==null)throw new ArgumentNullException(nameof(state));
            var shadow=state.Copy();shadow.expansion=null;
            if(shadow.pending!=null)shadow.pending.expansion=null;
            if(shadow.lastSpin!=null)shadow.lastSpin.expansion=null;
            return shadow;
        }
        internal static string Fingerprint(ModelState state){return ExpansionCodec.Hash(new JsonStateCodec().Encode(state));}
        static bool Same(ExpansionState a,ExpansionState b){return a!=null&&b!=null&&ExpansionCodec.EncodeState(a)==ExpansionCodec.EncodeState(b);}
        static bool FamilyOwned(ModelState s,string family)
        {return ChapterCatalog.Rules.Stages(ChapterEconomy.Project(s).Progress).AbsoluteLevel(family)>=1;}
        static void StateBounds(ModelState s,ExpansionState expansion,long ceiling)
        {
            Need(expansion.AdoptedSequence>=1&&expansion.AdoptedSequence<=ceiling&&expansion.ForegroundSpins<=ceiling-expansion.AdoptedSequence+1,
                "Invalid expansion adoption/history bounds");
            Need((expansion.LastResourceReceipt==null||expansion.LastResourceReceipt.Sequence<=ceiling)&&
                (expansion.LastCommand==null||expansion.LastCommand.Sequence<=ceiling),"Future expansion receipt");
            if(expansion.FormatVersion==3)ArcadeEconomy.Bounds(expansion.ArcadeData,expansion,ceiling);
            foreach(string family in FamilyIds.All)Need(expansion.EquipmentLevel(family)==0||FamilyOwned(s,family),"Equipment requires actual family ownership");
            var command=expansion.LastCommand;
            if(command!=null)
            {
                if(command.Action=="equip")Need(command.Slot<=expansion.SocketCount&&expansion.ModuleAt(command.Slot)==(command.Id=="module.none"?null:command.Id),"Equipped state disagrees with latest command");
                else if(command.Action=="theme")Need(expansion.ThemeId==command.Id,"Theme disagrees with latest command");
                else if(command.Action=="speed")Need(expansion.SpeedId==command.Id,"Speed disagrees with latest command");
                else if(command.Action=="focus")Need(expansion.BonusWorkbenchLevel>=1&&expansion.BonusFocusId==command.Id,"Focus disagrees with latest command");
                else if(command.Action=="preset")Need(PreviewPreset(expansion,command.Id).SequenceEqual(Enumerable.Range(1,3).Select(expansion.ModuleAt)),"Preset disagrees with latest command");
                else if(command.Action=="buy")
                {
                    var offer=ExpansionCatalog.Get(command.Id);
                    Need(offer.Kind=="module"?expansion.OwnsModule(command.Id):offer.Kind=="theme"?expansion.OwnsTheme(command.Id):offer.Kind=="socket"?expansion.SocketCount>=offer.Level:
                        offer.Kind=="bonus"?expansion.BonusUnlocked:offer.Kind=="yield"?expansion.PotatoYieldLevel>=offer.Level:offer.Kind=="equipment"?expansion.EquipmentLevel(offer.FamilyId)>=offer.Level:
                        offer.Kind=="workbench"?expansion.BonusWorkbenchLevel>=offer.Level:offer.Kind=="trade",
                        "Owned state disagrees with latest purchase");
                }
            }
        }
        public static ExpansionQuote Quote(ModelState state,string action,string id,int slot=0)
        {
            GameLogic.Validate(state);Need(state.pending==null,"Expansion requires confirmed progress");
            Need(state.sequence<ExpansionResourceReceipt.OrderSequenceMaximum,"Expansion sequence exhausted");
            Need(!ChapterEconomy.Project(state).TransitionPending,"Acknowledge era transition before expansion commands");
            Need(IsAction(action)&&StableId.IsValid(id),"Unknown expansion action/ID");
            var s=Project(state);int parts=0,potatoes=0,energy=0;long coins=0,reward=0;
            if(action=="buy")
            {
                Need(slot==0,"Purchase has no slot payload");var offer=ExpansionCatalog.Get(id);
                if(offer.Kind=="module")Need(!s.OwnsModule(id),"Module already owned");
                else if(offer.Kind=="theme")Need(!s.OwnsTheme(id),"Theme already owned");
                else if(offer.Kind=="socket")Need(offer.Level==s.SocketCount+1,"Socket unavailable/already owned");
                else if(offer.Kind=="bonus")Need(!s.BonusUnlocked,"Bonus already unlocked");
                else if(offer.Kind=="yield")Need(offer.Level==s.PotatoYieldLevel+1,"Yield level unavailable/already owned");
                else if(offer.Kind=="equipment")Need(FamilyOwned(state,offer.FamilyId)&&offer.Level==s.EquipmentLevel(offer.FamilyId)+1,"Equipment family/level locked");
                else if(offer.Kind=="workbench")Need(s.BonusUnlocked&&offer.Level==s.BonusWorkbenchLevel+1,"Bonus workbench prerequisite/level locked");
                else Need(offer.Kind=="trade","Unknown purchase kind");
                parts=offer.PartsCost;potatoes=offer.PotatoesCost;coins=offer.CoinsCost;reward=offer.CoinsReward;
            }
            else if(action=="equip")
            {
                Need(slot>=1&&slot<=s.SocketCount,"Socket not owned");
                if(id=="module.none")Need(s.ModuleAt(slot)!=null,"Socket already empty");
                else
                {
                    Need(s.OwnsModule(id),"Module not owned");
                    for(int other=1;other<=s.SocketCount;other++)Need(s.ModuleAt(other)!=id,"Module already equipped; explicitly clear its old socket first");
                }
            }
            else if(action=="theme")Need(slot==0&&s.OwnsTheme(id)&&s.ThemeId!=id,"Theme unavailable/already selected");
            else if(action=="speed")Need(slot==0&&(id=="speed.normal"||id=="speed.fast"),"Unknown presentation speed");
            else if(action=="focus")Need(slot==0&&ExpansionCatalog.IsFocus(id)&&s.BonusWorkbenchLevel>=1,"Bonus focus requires workbenchI and a known focus");
            else if(action=="preset"){Need(slot==0,"Preset has no slot payload");PreviewPreset(s,id);}
            else
            {
                Need(slot==0&&id==ExpansionCatalog.NextBonusBoost&&s.BonusUnlocked&&!s.BoostArmed,"Boost unavailable/already armed");energy=EconomyRules.ExpansionBoostCost;
            }
            Need(s.Parts>=parts&&s.Potatoes>=potatoes&&s.Energy>=energy&&state.coins>=coins,"Insufficient expansion resources");
            Need(state.coins-coins<=long.MaxValue-reward,"Expansion coin credit would overflow");
            return new ExpansionQuote(action,id,slot,state.sequence,parts,potatoes,energy,coins,reward,Fingerprint(state),ExpansionCatalog.RevisionFor(s,action,id));
        }
        static ExpansionState ApplyCommand(ExpansionState before,ExpansionQuote q)
        {
            var d=new ExpansionDraft(before);if(d.AdoptedSequence==0)d.AdoptedSequence=q.Sequence;
            if(q.RuleRevision==ExpansionCatalog.NextRuleRevision)d.FormatVersion=2;
            d.Parts=checked(d.Parts-q.PartsCost);d.Potatoes=checked(d.Potatoes-q.PotatoesCost);d.Energy=checked(d.Energy-q.EnergyCost);
            if(q.Action=="buy")
            {
                var offer=ExpansionCatalog.Get(q.Id);
                if(offer.Kind=="module")d.ModuleMask|=1<<ExpansionCatalog.ModuleIndex(q.Id);
                else if(offer.Kind=="theme")d.ThemeMask|=1<<ExpansionCatalog.ThemeIndex(q.Id);
                else if(offer.Kind=="socket")d.SocketCount=offer.Level;
                else if(offer.Kind=="bonus"){d.BonusUnlocked=true;d.BonusProgress=0;}
                else if(offer.Kind=="yield")d.PotatoYieldLevel=offer.Level;
                else if(offer.Kind=="equipment")d.Equipment[ExpansionCatalog.FamilyIndex(offer.FamilyId)]=offer.Level;
                else if(offer.Kind=="workbench")d.BonusWorkbenchLevel=offer.Level;
            }
            else if(q.Action=="equip")d.Modules[q.Slot-1]=q.Id=="module.none"?null:q.Id;
            else if(q.Action=="theme")d.ThemeId=q.Id;
            else if(q.Action=="speed")d.SpeedId=q.Id;
            else if(q.Action=="focus")d.BonusFocusId=q.Id;
            else if(q.Action=="preset")d.Modules=PreviewPreset(before,q.Id).ToArray();
            else d.BoostArmed=true;
            d.LastCommand=new ExpansionCommandReceipt(q.Sequence,q.Key,q.Action,q.Id,q.Slot);return d.Freeze();
        }
        // New reserve opts into V3. Replay always supplies its stored revision. The
        // parameterless pure helper preserves its before-format's historical behavior.
        internal static ExpansionState Observe(ExpansionState before,long sequence,int[] symbols,string revision=null,ArcadeMoney money=null)
        {
            revision=revision??(before.FormatVersion==3?ExpansionCatalog.ArcadeRuleRevision:before.FormatVersion==2?ExpansionCatalog.NextRuleRevision:ExpansionCatalog.RuleRevision);
            if(revision!=ExpansionCatalog.ArcadeRuleRevision)
            {Need(before.FormatVersion<=2&&(revision==ExpansionCatalog.RuleRevision||revision==ExpansionCatalog.NextRuleRevision),"Historical spin revision/context mismatch");return ObserveResources(before,sequence,symbols,0);}
            var adopted=new ExpansionDraft(before){FormatVersion=3};
            if(adopted.AdoptedSequence==0)adopted.AdoptedSequence=sequence;
            if(adopted.ArcadeData==null)adopted.ArcadeData=ArcadeState.Empty;
            var basis=adopted.Freeze();int level=basis.ArcadeData.SlotLevel;
            var actual=ObserveResources(basis,sequence,symbols,level);
            var baseline=level==0?actual:ObserveResources(basis,sequence,symbols,0);
            var result=new ExpansionDraft(actual){ArcadeData=ArcadeEconomy.Spin(basis.ArcadeData,sequence,actual.LastResourceReceipt,actual.LastResourceReceipt.Parts-baseline.LastResourceReceipt.Parts,
                actual.LastResourceReceipt.Potatoes-baseline.LastResourceReceipt.Potatoes,money??new ArcadeMoney())};return result.Freeze();
        }
        static ExpansionState ObserveResources(ExpansionState before,long sequence,int[] symbols,int slotLevel)
        {
            Need(symbols!=null&&symbols.Length==3,"Expansion requires exact foreground symbols");
            var attributes=ArcadeCatalog.SlotAttributes(slotLevel);
            var outcome=new SpinOutcome(symbols[0],symbols[1],symbols[2]);
            var d=new ExpansionDraft(before);if(d.AdoptedSequence==0)d.AdoptedSequence=sequence;
            d.ForegroundSpins=checked(d.ForegroundSpins+1);
            int trash=outcome.Count(SymbolIds.Trash),boxes=outcome.Count(SymbolIds.Box),potatoes=outcome.Count(SymbolIds.Potato);
            var modules=ModuleBonuses(before);
            int parts=checked(trash*(1+modules.PartsPerTrash+before.EquipmentLevel("recycler"))+
                boxes*(1+modules.PartsPerBox+before.EquipmentLevel("cart"))+before.EquipmentLevel("workshop"));
            int harvest=checked(potatoes*(1+before.PotatoYieldLevel+modules.PotatoesPerPotato));
            int energy=checked(EconomyRules.ExpansionEnergyPerSpin+before.EquipmentLevel("power-source"));
            bool matchingPair=outcome.Left==outcome.Middle||outcome.Left==outcome.Right||outcome.Middle==outcome.Right;
            if(matchingPair){parts+=modules.PartsPerMatchingSpin;harvest+=modules.PotatoesPerMatchingSpin;}
            if(outcome.Count(SymbolIds.Pigeon)>0)energy+=modules.EnergyPerPigeonSpin;
            if(boxes>0)parts+=attributes.BoxPresencePartBonus;
            if(potatoes>0)harvest+=attributes.PotatoPresencePotatoBonus;
            int baseParts=Math.Min(before.PartsCap-before.Parts,parts),basePotatoes=Math.Min(before.PotatoesCap-before.Potatoes,harvest);
            int creditedEnergy=Math.Min(before.EnergyCap-before.Energy,energy);
            d.Parts+=baseParts;d.Potatoes+=basePotatoes;d.Energy+=creditedEnergy;
            int bonusSymbol=-1,bonusParts=0,bonusPotatoes=0;bool boosted=false;
            if(before.BonusUnlocked)
            {
                d.BonusProgress++;
                if(d.BonusProgress==EconomyRules.ExpansionBonusCadence)
                {
                    uint seed=before.BonusSeed;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;
                    d.BonusSeed=seed;bonusSymbol=(int)(seed>>30);d.BonusProgress=0;d.BonusRolls=checked(d.BonusRolls+1);
                    boosted=before.BoostArmed;d.BoostArmed=false;int multiplier=boosted?2:1;
                    int rawParts=EconomyRules.ExpansionBonusPartsFor(bonusSymbol),rawPotatoes=EconomyRules.ExpansionBonusPotatoesFor(bonusSymbol);
                    if(before.BonusWorkbenchLevel==2){rawParts+=EconomyRules.ExpansionWorkbenchBonusPerResource;rawPotatoes+=EconomyRules.ExpansionWorkbenchBonusPerResource;}
                    rawParts+=attributes.RawBonusPartsAddition;rawPotatoes+=attributes.RawBonusPotatoesAddition;
                    if(before.BonusFocusId==ExpansionCatalog.FocusParts){rawParts+=rawPotatoes;rawPotatoes=0;}
                    else if(before.BonusFocusId==ExpansionCatalog.FocusPotatoes){rawPotatoes+=rawParts;rawParts=0;}
                    bonusParts=Math.Min(before.PartsCap-d.Parts,rawParts*multiplier);
                    bonusPotatoes=Math.Min(before.PotatoesCap-d.Potatoes,rawPotatoes*multiplier);
                    d.Parts+=bonusParts;d.Potatoes+=bonusPotatoes;
                }
            }
            d.LastResourceReceipt=new ExpansionResourceReceipt(sequence,baseParts+bonusParts,basePotatoes+bonusPotatoes,creditedEnergy,bonusSymbol,bonusParts,bonusPotatoes,boosted);
            return d.Freeze();
        }
        // All public historical reserve routes enter here once. Historical implementations
        // call only one another, on a fully validated lower shadow, preventing redispatch.
        internal static ModelState Compose(ModelState state,Func<ModelState,ModelState> reserve)
        {
            GameLogic.Validate(state);var shadow=EconomicShadow(state);var reserved=reserve(shadow);
            if(ReferenceEquals(reserved,shadow))return state;
            Need(reserved.pending!=null,"Historical reserve returned no pending operation");
            if(state.expansion==null&&reserved.pending.kind!="spin")return reserved;
            var before=Project(state);var p=reserved.pending;
            var after=p.kind=="spin"?Observe(before,p.sequence,p.symbols,ExpansionCatalog.ArcadeRuleRevision,ArcadeMoney.Pending(state,p)):before;
            reserved.expansion=state.expansion;
            if(reserved.lastSpin!=null)reserved.lastSpin.expansion=state.lastSpin?.expansion;
            p.expansion=new ExpansionOperation(p.kind,p.sequence,p.at,state.expansion==null,before,after,state.seed,p.kind=="spin"?p.seedAfter:state.seed,0,0,null);
            Validate(reserved);return reserved;
        }
        public static ModelState Reserve(ModelState state,ExpansionQuote quote,long at=0)
        {
            GameLogic.Validate(state);Need(state.pending==null&&quote!=null,"Expansion requires a confirmed request");
            var last=state.expansion?.LastCommand;
            if(last!=null&&last.Sequence==quote.Sequence&&last.Key==quote.Key)
            {Need(last.Action==quote.Action&&last.Id==quote.Id&&last.Slot==quote.Slot,"Conflicting expansion replay");return state;}
            Need(quote.ExpectedSequence==state.sequence,"Stale expansion confirmation");
            var expected=Quote(state,quote.Action,quote.Id,quote.Slot);
            Need(expected.Key==quote.Key,"Expansion confirmation context/cost changed");
            if(NoChange(Project(state),quote))return state;
            var after=ApplyCommand(Project(state),quote);var next=state.Copy();
            var op=new ExpansionOperation("expansion",quote.Sequence,at,state.expansion==null,Project(state),after,state.seed,state.seed,state.coins,checked(state.coins-quote.CoinsCost+quote.CoinsReward),quote);
            next.pending=new PendingOperation{kind="expansion",sequence=op.Sequence,at=at,expansion=op};next.revision=checked(state.revision+1);
            Validate(next);return next;
        }
        static void FrozenComposed(ExpansionOperation op,int[] symbols,uint liveSeed,bool isReceipt,ArcadeMoney money=null)
        {
            Need(op.Quote==null&&op.CoinsBefore==0&&op.CoinsAfter==0,"Unexpected composed expansion spend");
            Need(op.BeforeWasAbsent?(op.Kind=="spin"&&Same(op.Before,ExpansionState.Empty)):
                op.Before.AdoptedSequence>=1&&op.Before.AdoptedSequence<op.Sequence,"Invalid frozen expansion adoption boundary");
            ExpansionState expected;
            if(op.Kind=="spin")
            {
                var draw=GameLogic.Draw(op.SeedBefore,op.Sequence);
                Need(symbols!=null&&draw.symbols.SequenceEqual(symbols)&&draw.seedAfter==op.SeedAfter&&(!isReceipt||liveSeed==op.SeedAfter),"Expansion primary RNG/outcome mismatch");
                expected=Observe(op.Before,op.Sequence,symbols,op.RuleRevision,money);
            }
            else{Need(!isReceipt&&op.SeedBefore==op.SeedAfter,"Invalid nonspin expansion descriptor");expected=op.Before;}
            Need(Same(expected,op.After),"Frozen resource delta/cap/bonus mismatch");
        }
        public static void Validate(ModelState state)
        {
            Need(state!=null,"Missing expansion model");var shadow=EconomicShadow(state);var pending=state.pending;var op=pending?.expansion;
            bool dedicated=pending?.kind=="expansion"||pending?.kind=="arcade"||pending?.kind=="expansion.batch"||pending?.kind=="parcels";
            if(dedicated)
            {
                Need(op!=null&&pending.chapter==null&&pending.journey==null&&pending.wave==null&&pending.target==0&&pending.symbols==null&&pending.seedAfter==0,"Invalid dedicated expansion envelope");
                shadow.pending=null;shadow.revision=checked(shadow.revision-1);
            }
            GameLogic.Validate(shadow);
            Need(state.revision==checked(state.sequence*2+(pending==null?0:1)),"Invalid expansion revision");
            var live=state.expansion;
            if(live!=null)StateBounds(state,live,state.sequence);
            var receipt=state.lastSpin?.expansion;
            if(receipt!=null)
            {
                Need(live!=null&&receipt.Kind=="spin"&&receipt.Sequence==state.lastSpin.sequence&&receipt.Sequence<=state.sequence,"Missing/conflicting expansion spin receipt");
                var money=receipt.RuleRevision==ExpansionCatalog.ArcadeRuleRevision?ArcadeMoney.Receipt(state.lastSpin,receipt.After.ArcadeData.LastResourceReceipt):null;
                FrozenComposed(receipt,state.lastSpin.symbols,state.seed,true,money);
                StateBounds(state,receipt.After,receipt.Sequence);
                Need(live.ForegroundSpins==receipt.After.ForegroundSpins&&live.BonusRolls==receipt.After.BonusRolls&&live.BonusSeed==receipt.After.BonusSeed&&
                    live.BonusProgress==receipt.After.BonusProgress&&ExpansionCodec.ReceiptToken(live.LastResourceReceipt)==ExpansionCodec.ReceiptToken(receipt.After.LastResourceReceipt),"Expansion history regressed/diverged after spin");
                Need(live.FormatVersion>=receipt.After.FormatVersion&&live.BonusWorkbenchLevel>=receipt.After.BonusWorkbenchLevel&&
                    (live.ModuleMask&receipt.After.ModuleMask)==receipt.After.ModuleMask&&(live.ThemeMask&receipt.After.ThemeMask)==receipt.After.ThemeMask&&
                    live.SocketCount>=receipt.After.SocketCount&&live.PotatoYieldLevel>=receipt.After.PotatoYieldLevel&&(!receipt.After.BonusUnlocked||live.BonusUnlocked)&&
                    FamilyIds.All.All(f=>live.EquipmentLevel(f)>=receipt.After.EquipmentLevel(f)),"Expansion ownership regressed after retained spin");
                if(receipt.After.LastCommand!=null)Need(live.LastCommand!=null&&(live.LastCommand.Sequence==receipt.After.LastCommand.Sequence?live.LastCommand.Key==receipt.After.LastCommand.Key:
                    live.LastCommand.Sequence>receipt.Sequence),"Expansion command history regressed after spin");
                if(live.FormatVersion==3)
                {if(receipt.After.FormatVersion==3)ArcadeEconomy.History(live.ArcadeData,receipt.After.ArcadeData);else Need(live.ArcadeData.ForegroundSpins==0&&live.ArcadeData.AdoptedSequence>receipt.Sequence,"Missing Arcade foreground receipt");}
            }
            else if(live!=null)Need(live.ForegroundSpins==0&&(state.lastSpin==null||state.lastSpin.sequence<live.AdoptedSequence),"Missing expansion foreground history");
            if(pending==null){Need(live!=null,"Orphan expansion receipt without aggregate");return;}
            Need(op!=null&&pending.sequence==state.sequence+1&&op.Sequence==pending.sequence&&op.Kind==pending.kind&&op.At==pending.at&&op.SeedBefore==state.seed&&
                op.BeforeWasAbsent==(live==null)&&Same(op.Before,Project(state)),"Missing/conflicting expansion pending checkpoint");
            Need(!op.BeforeWasAbsent||Same(op.Before,ExpansionState.Empty),"Absent expansion contains historical resources");
            StateBounds(state,op.After,op.Sequence);
            if(dedicated)
            {
                if(pending.kind=="arcade"){ArcadeEconomy.ValidateDedicated(state,op);return;}
                if(pending.kind=="parcels"){ParcelsEconomy.ValidateDedicated(state,op);return;}
                if(pending.kind=="expansion.batch"){ValidateBatch(state,op);return;}
                Need(op.Quote!=null&&op.SeedAfter==state.seed&&op.CoinsBefore==state.coins,"Dedicated expansion changed primary context");
                var confirmed=state.Copy();confirmed.pending=null;confirmed.revision=checked(state.sequence*2);
                var q=Quote(confirmed,op.Quote.Action,op.Quote.Id,op.Quote.Slot);
                Need(q.Key==op.Quote.Key&&q.Sequence==op.Sequence&&Same(ApplyCommand(Project(state),q),op.After)&&
                    op.CoinsAfter==checked(state.coins-q.CoinsCost+q.CoinsReward),"Frozen expansion request/result mismatch");
                Need(!NoChange(Project(state),q),"No-op expansion action must not be journaled");
            }
            else
            {
                Need(live!=null||pending.kind=="spin","Only a new foreground spin adopts implicitly");
                Need(op.SeedAfter==(pending.kind=="spin"?pending.seedAfter:state.seed),"Pending expansion seed mismatch");
                FrozenComposed(op,pending.symbols,state.seed,false,op.Kind=="spin"&&op.RuleRevision==ExpansionCatalog.ArcadeRuleRevision?ArcadeMoney.Pending(state,pending):null);
            }
        }
        public static ModelState ApplyPending(ModelState state)
        {
            Validate(state);if(state.pending==null)return state;
            var op=state.pending.expansion;ModelState next;
            if(op.Kind=="expansion"||op.Kind=="arcade"||op.Kind=="expansion.batch"||op.Kind=="parcels")
            {
                next=state.Copy();next.pending=null;next.sequence=op.Sequence;next.revision=checked(next.sequence*2);next.coins=op.CoinsAfter;
                if(op.At>next.lastSettledAt)next.lastSettledAt=op.At;
            }
            else
            {
                next=GameLogic.ApplyPending(EconomicShadow(state));
                if(next.lastSpin!=null)next.lastSpin.expansion=op.Kind=="spin"?op:state.lastSpin?.expansion;
            }
            next.expansion=op.After;Validate(next);return next;
        }
    }
}
