using System;

namespace JunkToNeon.Core
{
    public sealed class ExpansionResourceReceipt
    {
        public long Sequence { get; }
        public int Parts { get; }
        public int Potatoes { get; }
        public int Energy { get; }
        public int BonusSymbol { get; }
        public int BonusParts { get; }
        public int BonusPotatoes { get; }
        public bool Boosted { get; }
        internal ExpansionResourceReceipt(long sequence,int parts,int potatoes,int energy,int bonusSymbol,int bonusParts,int bonusPotatoes,bool boosted)
        {
            if(sequence<1||sequence>OrderSequenceMaximum||parts<0||potatoes<0||energy<0||bonusSymbol < -1||bonusSymbol>3||bonusParts<0||bonusPotatoes<0||
                bonusParts>parts||bonusPotatoes>potatoes||bonusSymbol==-1&&(bonusParts!=0||bonusPotatoes!=0||boosted))throw new ArgumentException("Invalid resource receipt");
            Sequence=sequence;Parts=parts;Potatoes=potatoes;Energy=energy;BonusSymbol=bonusSymbol;BonusParts=bonusParts;BonusPotatoes=bonusPotatoes;Boosted=boosted;
        }
        internal const long OrderSequenceMaximum=(long.MaxValue-2)/2;
    }
    public sealed class ExpansionCommandReceipt
    {
        public long Sequence { get; }
        public string Key { get; }
        public string Action { get; }
        public string Id { get; }
        public int Slot { get; }
        internal ExpansionCommandReceipt(long sequence,string key,string action,string id,int slot)
        {
            if(sequence<1||sequence>ExpansionResourceReceipt.OrderSequenceMaximum||!ExpansionCodec.IsKey(key)||!ExpansionEconomy.IsAction(action)||
                !JunkToNeon.Contracts.StableId.IsValid(id)||slot<0||slot>3)throw new ArgumentException("Invalid expansion command receipt");
            if(action=="buy"){ExpansionCatalog.Get(id);if(slot!=0)throw new ArgumentException("Invalid purchase slot");}
            else if(action=="equip"){if(id!="module.none")ExpansionCatalog.ModuleIndex(id);if(slot<1)throw new ArgumentException("Invalid equipped slot");}
            else if(action=="theme"){ExpansionCatalog.ThemeIndex(id);if(slot!=0)throw new ArgumentException("Invalid theme slot");}
            else if(action=="speed"){if(slot!=0||(id!="speed.normal"&&id!="speed.fast"))throw new ArgumentException("Invalid speed receipt");}
            else if(action=="focus"){if(slot!=0||!ExpansionCatalog.IsFocus(id))throw new ArgumentException("Invalid bonus focus receipt");}
            else if(action=="preset"){ExpansionCatalog.PresetPriority(id);if(slot!=0)throw new ArgumentException("Invalid preset slot");}
            else if(slot!=0||id!=ExpansionCatalog.NextBonusBoost)throw new ArgumentException("Invalid boost receipt");
            Sequence=sequence;Key=key;Action=action;Id=id;Slot=slot;
        }
    }
    public sealed class ExpansionState
    {
        readonly string[] modules;
        readonly int[] equipment;
        internal int ModuleMask { get; }
        internal int ThemeMask { get; }
        public int FormatVersion { get; }
        public int BonusWorkbenchLevel { get; }
        public string BonusFocusId { get; }
        internal ArcadeState ArcadeData { get; }
        public int Parts { get; }
        public int Potatoes { get; }
        public int Energy { get; }
        public int PartsCap { get {return EconomyRules.ExpansionPartsCap+EquipmentLevel("house")*EconomyRules.ExpansionHouseStoragePerLevel;} }
        public int PotatoesCap { get {return EconomyRules.ExpansionPotatoesCap+EquipmentLevel("house")*EconomyRules.ExpansionHouseStoragePerLevel;} }
        public int EnergyCap { get {return EconomyRules.ExpansionEnergyCap+EquipmentLevel("pigeon-loft")*EconomyRules.ExpansionPigeonEnergyPerLevel;} }
        public int SocketCount { get; }
        public int PotatoYieldLevel { get; }
        public bool BonusUnlocked { get; }
        public bool BoostArmed { get; }
        public int BonusProgress { get; }
        public int BonusCadence { get {return EconomyRules.ExpansionBonusCadence;} }
        public long BonusRolls { get; }
        public uint BonusSeed { get; }
        public long ForegroundSpins { get; }
        public string ThemeId { get; }
        public string SpeedId { get; }
        public float SpinDurationSeconds { get {return SpeedId=="speed.fast"?EconomyRules.ExpansionFastSpinSeconds:EconomyRules.ExpansionNormalSpinSeconds;} }
        public long AdoptedSequence { get; }
        public bool IsAdopted { get {return AdoptedSequence!=0;} }
        public ExpansionResourceReceipt LastResourceReceipt { get; }
        public ExpansionCommandReceipt LastCommand { get; }
        public static ExpansionState Empty { get; }=new ExpansionDraft().Freeze();
        internal ExpansionState(ExpansionDraft d)
        {
            if((d.FormatVersion<1||d.FormatVersion>3)||(d.FormatVersion==3?d.ArcadeData==null:d.ArcadeData!=null)||d.ArcadeData?.SlotLevel==3&&!d.BonusUnlocked||
                d.BonusWorkbenchLevel<0||d.BonusWorkbenchLevel>2||!ExpansionCatalog.IsFocus(d.BonusFocusId)||
                d.BonusWorkbenchLevel>0&&!d.BonusUnlocked||d.BonusWorkbenchLevel==0&&d.BonusFocusId!=ExpansionCatalog.FocusBalanced||
                d.FormatVersion==1&&(d.BonusWorkbenchLevel!=0||d.BonusFocusId!=ExpansionCatalog.FocusBalanced||d.ThemeMask>15||
                    d.LastCommand!=null&&ExpansionCatalog.RequiresV2(d.LastCommand.Action,d.LastCommand.Id))||
                d.Modules==null||d.Modules.Length!=3||d.Equipment==null||d.Equipment.Length!=6||d.SocketCount<1||d.SocketCount>3||d.ModuleMask<0||d.ModuleMask>31||
                d.ThemeMask<1||d.ThemeMask>63||(d.ThemeMask&1)==0||d.PotatoYieldLevel<0||d.PotatoYieldLevel>2||d.BonusProgress<0||d.BonusProgress>=EconomyRules.ExpansionBonusCadence||
                d.BonusRolls<0||d.ForegroundSpins<0||d.ForegroundSpins>ExpansionResourceReceipt.OrderSequenceMaximum||d.BonusRolls>d.ForegroundSpins/EconomyRules.ExpansionBonusCadence||
                d.BonusProgress>d.ForegroundSpins-d.BonusRolls*EconomyRules.ExpansionBonusCadence||d.BonusSeed==0||
                d.AdoptedSequence<0||d.AdoptedSequence>ExpansionResourceReceipt.OrderSequenceMaximum||!d.BonusUnlocked&&(d.BoostArmed||d.BonusProgress!=0||d.BonusRolls!=0||d.BonusSeed!=EconomyRules.ExpansionBonusSeed))
                throw new ArgumentException("Invalid expansion checkpoint");
            FormatVersion=d.FormatVersion;BonusWorkbenchLevel=d.BonusWorkbenchLevel;BonusFocusId=d.BonusFocusId;
            ArcadeData=d.ArcadeData;
            ModuleMask=d.ModuleMask;ThemeMask=d.ThemeMask;SocketCount=d.SocketCount;PotatoYieldLevel=d.PotatoYieldLevel;BonusUnlocked=d.BonusUnlocked;BoostArmed=d.BoostArmed;
            BonusProgress=d.BonusProgress;BonusRolls=d.BonusRolls;BonusSeed=d.BonusSeed;ForegroundSpins=d.ForegroundSpins;AdoptedSequence=d.AdoptedSequence;
            modules=(string[])d.Modules.Clone();equipment=(int[])d.Equipment.Clone();
            for(int i=0;i<6;i++)if(equipment[i]<0||equipment[i]>2)throw new ArgumentException("Invalid equipment level");
            int used=0;
            for(int i=0;i<3;i++)if(modules[i]!=null)
            {
                int bit=1<<ExpansionCatalog.ModuleIndex(modules[i]);
                if(i>=SocketCount||(used&bit)!=0||(ModuleMask&bit)==0)throw new ArgumentException("Unowned/duplicate/locked equipped module");used|=bit;
            }
            ThemeId=d.ThemeId;if(!OwnsTheme(ThemeId))throw new ArgumentException("Unowned theme selection");
            SpeedId=d.SpeedId;if(SpeedId!="speed.normal"&&SpeedId!="speed.fast")throw new ArgumentException("Unknown presentation speed");
            Parts=d.Parts;Potatoes=d.Potatoes;Energy=d.Energy;
            if(Parts<0||Parts>PartsCap||Potatoes<0||Potatoes>PotatoesCap||Energy<0||Energy>EnergyCap)throw new ArgumentException("Invalid resource balance/cap");
            LastResourceReceipt=d.LastResourceReceipt;LastCommand=d.LastCommand;
            if((ForegroundSpins==0)!=(LastResourceReceipt==null)||LastResourceReceipt!=null&&LastResourceReceipt.Sequence<AdoptedSequence||
                LastCommand!=null&&LastCommand.Sequence<AdoptedSequence)throw new ArgumentException("Invalid expansion retained history");
        }
        public bool OwnsModule(string id) {return (ModuleMask&(1<<ExpansionCatalog.ModuleIndex(id)))!=0;}
        public bool OwnsTheme(string id) {return (ThemeMask&(1<<ExpansionCatalog.ThemeIndex(id)))!=0;}
        public string ModuleAt(int slot) {if(slot<1||slot>3)throw new ArgumentOutOfRangeException(nameof(slot));return modules[slot-1];}
        public int EquipmentLevel(string familyId) {return equipment[ExpansionCatalog.FamilyIndex(familyId)];}
        internal bool Equipped(string id) {for(int i=0;i<SocketCount;i++)if(modules[i]==id)return true;return false;}
    }
    // Invocation-local builder. Frozen public state never exposes either mutable array.
    internal sealed class ExpansionDraft
    {
        internal int Parts,Potatoes,Energy,ModuleMask,ThemeMask=1,SocketCount=1,PotatoYieldLevel,BonusProgress;
        internal int FormatVersion=1,BonusWorkbenchLevel;
        internal ArcadeState ArcadeData;
        internal string BonusFocusId=ExpansionCatalog.FocusBalanced;
        internal bool BonusUnlocked,BoostArmed;
        internal long BonusRolls,ForegroundSpins,AdoptedSequence;
        internal uint BonusSeed=EconomyRules.ExpansionBonusSeed;
        internal string ThemeId=ExpansionCatalog.DefaultTheme;
        internal string SpeedId="speed.normal";
        internal string[] Modules=new string[3];internal int[] Equipment=new int[6];
        internal ExpansionResourceReceipt LastResourceReceipt;internal ExpansionCommandReceipt LastCommand;
        internal ExpansionDraft(){}
        internal ExpansionDraft(ExpansionState s)
        {
            Parts=s.Parts;Potatoes=s.Potatoes;Energy=s.Energy;ModuleMask=s.ModuleMask;ThemeMask=s.ThemeMask;SocketCount=s.SocketCount;PotatoYieldLevel=s.PotatoYieldLevel;
            BonusProgress=s.BonusProgress;BonusUnlocked=s.BonusUnlocked;BoostArmed=s.BoostArmed;BonusRolls=s.BonusRolls;ForegroundSpins=s.ForegroundSpins;AdoptedSequence=s.AdoptedSequence;
            BonusSeed=s.BonusSeed;ThemeId=s.ThemeId;SpeedId=s.SpeedId;LastResourceReceipt=s.LastResourceReceipt;LastCommand=s.LastCommand;
            FormatVersion=s.FormatVersion;BonusWorkbenchLevel=s.BonusWorkbenchLevel;BonusFocusId=s.BonusFocusId;
            ArcadeData=s.ArcadeData;
            for(int i=0;i<3;i++)Modules[i]=s.ModuleAt(i+1);
            for(int i=0;i<6;i++)Equipment[i]=s.EquipmentLevel(JunkToNeon.Contracts.FamilyIds.All[i]);
        }
        internal ExpansionState Freeze(){return new ExpansionState(this);}
    }
    public sealed class ExpansionQuote
    {
        public string RuleRevision { get; }
        public string Action { get; } public string Id { get; } public int Slot { get; }
        public long ExpectedSequence { get; } public long Sequence { get {return checked(ExpectedSequence+1);} }
        public int PartsCost { get; } public int PotatoesCost { get; } public int EnergyCost { get; }
        public long CoinsCost { get; } public long CoinsReward { get; } public string Key { get; }
        internal string Fingerprint { get; }
        internal ExpansionQuote(string action,string id,int slot,long expectedSequence,int parts,int potatoes,int energy,long coins,long reward,string fingerprint,string ruleRevision=ExpansionCatalog.RuleRevision)
        {
            if(ruleRevision!=ExpansionCatalog.RuleRevision&&ruleRevision!=ExpansionCatalog.NextRuleRevision&&ruleRevision!=ExpansionCatalog.ArcadeRuleRevision)throw new ArgumentException("Unknown expansion quote revision");
            RuleRevision=ruleRevision;
            Action=action;Id=id;Slot=slot;ExpectedSequence=expectedSequence;PartsCost=parts;PotatoesCost=potatoes;EnergyCost=energy;CoinsCost=coins;CoinsReward=reward;Fingerprint=fingerprint;
            Key=ExpansionCodec.Hash(string.Join("|",RuleRevision,action,id,ExpansionCodec.N(slot),ExpansionCodec.N(expectedSequence),ExpansionCodec.N(parts),ExpansionCodec.N(potatoes),ExpansionCodec.N(energy),ExpansionCodec.N(coins),ExpansionCodec.N(reward),fingerprint));
        }
    }
    public sealed class ExpansionOperation
    {
        public string RuleRevision { get {return ParcelsQuote!=null?JunkToNeon.Core.ParcelsQuote.Revision:BatchQuote!=null?ExpansionBatchQuote.Revision:Before.FormatVersion==3||After.FormatVersion==3||ArcadeQuote!=null||Quote?.RuleRevision==ExpansionCatalog.ArcadeRuleRevision?ExpansionCatalog.ArcadeRuleRevision:
            Before.FormatVersion==2||After.FormatVersion==2||Quote?.RuleRevision==ExpansionCatalog.NextRuleRevision?ExpansionCatalog.NextRuleRevision:ExpansionCatalog.RuleRevision;} }
        public string Kind { get; } public long Sequence { get; } public long At { get; }
        public bool BeforeWasAbsent { get; } public ExpansionState Before { get; } public ExpansionState After { get; }
        public uint SeedBefore { get; } public uint SeedAfter { get; }
        public long CoinsBefore { get; } public long CoinsAfter { get; }
        public ExpansionQuote Quote { get; }
        public ArcadeQuote ArcadeQuote { get; }
        public ExpansionBatchQuote BatchQuote { get; }
        public ParcelsQuote ParcelsQuote { get; }
        public string Key { get; }
        internal ExpansionOperation(string kind,long sequence,long at,bool absent,ExpansionState before,ExpansionState after,uint seedBefore,uint seedAfter,long coinsBefore,long coinsAfter,ExpansionQuote quote,ArcadeQuote arcadeQuote=null,ExpansionBatchQuote batchQuote=null,ParcelsQuote parcelsQuote=null)
        {
            if(string.IsNullOrEmpty(kind)||sequence<1||sequence>ExpansionResourceReceipt.OrderSequenceMaximum||at<0||at>OfflineIncome.MaxUtcSeconds||before==null||after==null||seedBefore==0||seedAfter==0||coinsBefore<0||coinsAfter<0)
                throw new ArgumentException("Invalid frozen expansion operation");
            Kind=kind;Sequence=sequence;At=at;BeforeWasAbsent=absent;Before=before;After=after;SeedBefore=seedBefore;SeedAfter=seedAfter;CoinsBefore=coinsBefore;CoinsAfter=coinsAfter;Quote=quote;
            ArcadeQuote=arcadeQuote;BatchQuote=batchQuote;ParcelsQuote=parcelsQuote;
            if(kind=="parcels"?(parcelsQuote==null||quote!=null||arcadeQuote!=null||batchQuote!=null):parcelsQuote!=null)throw new ArgumentException("Unexpected Parcels payload");
            if(kind=="expansion.batch"?(batchQuote==null||quote!=null||arcadeQuote!=null):batchQuote!=null)throw new ArgumentException("Unexpected batch operation payload");
            if(kind=="arcade"?(arcadeQuote==null||quote!=null):arcadeQuote!=null)throw new ArgumentException("Unexpected Arcade operation payload");
            if(quote!=null&&quote.RuleRevision!=RuleRevision)throw new ArgumentException("Conflicting expansion operation/quote revisions");
            Key=ExpansionCodec.Hash(ExpansionCodec.OperationBody(this));
        }
    }
}
