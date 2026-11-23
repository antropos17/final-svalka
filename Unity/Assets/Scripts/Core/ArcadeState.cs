using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkToNeon.Core
{
    public sealed class ArcadeQuote
    {
        public string RuleRevision {get{return ExpansionCatalog.ArcadeRuleRevision;}}
        public long ExpectedSequence {get;} public long Sequence {get{return checked(ExpectedSequence+1);}}
        public string Action {get;} public string Id {get;} public long ExpectedInstance {get;}
        public int ExpectedCursor {get;} public string ExpectedPhase {get;} public int Target {get;}
        public int PartsCost {get;} public int PotatoesCost {get;} public long DiamondsCost {get;} public int TicketsCost {get;}
        public int PartsCredit {get;} public int PotatoesCredit {get;} public int DiamondsCredit {get;} public string Key {get;}
        internal string Fingerprint {get;}
        internal ArcadeQuote(long sequence,string action,string id,long instance,int cursor,string phase,int target,int parts,int potatoes,long diamonds,int tickets,int addParts,int addPotatoes,int addDiamonds,string fingerprint)
        {
            if(sequence<0||sequence>=ExpansionResourceReceipt.OrderSequenceMaximum||!ArcadeCatalog.Action(action)||!JunkToNeon.Contracts.StableId.IsValid(id)||instance<0||instance>sequence||
                cursor<0||cursor>27||!ArcadeRun.PhaseKnown(phase)||target < -1||target>5||parts<0||potatoes<0||diamonds<0||tickets<0||tickets>1||addParts<0||addPotatoes<0||addDiamonds<0||addDiamonds>3||!ExpansionCodec.IsKey(fingerprint))throw new ArgumentException("Invalid Arcade quote");
            ExpectedSequence=sequence;Action=action;Id=id;ExpectedInstance=instance;ExpectedCursor=cursor;ExpectedPhase=phase;Target=target;PartsCost=parts;PotatoesCost=potatoes;DiamondsCost=diamonds;TicketsCost=tickets;PartsCredit=addParts;PotatoesCredit=addPotatoes;DiamondsCredit=addDiamonds;Fingerprint=fingerprint;
            if(action=="buy"){ArcadeCatalog.Get(id);if(instance!=0||target!=-1)throw new ArgumentException("Invalid Arcade purchase identity");}
            else if(!ArcadeCatalog.Game(id)||(action=="start"?(instance!=0||target!=-1):instance<1))throw new ArgumentException("Invalid Arcade run identity");
            if(action=="pick"&&(id!=ArcadeCatalog.Memory||target<0||target>5)||action=="sort"&&(id!=ArcadeCatalog.Sorting||target<0||target>3)||
                action=="continue"&&(id!=ArcadeCatalog.Memory||target!=-1)||(action=="claim"||action=="abandon")&&target!=-1)throw new ArgumentException("Invalid Arcade target");
            Key=ExpansionCodec.Hash(ArcadeCodec.QuoteBody(this));
        }
    }
    public sealed class ArcadeEvent
    {
        public long Sequence {get;} public string Kind {get;} public long InstanceSequence {get;}
        public int PartsAdded {get;} public int PotatoesAdded {get;} public int EnergyAdded {get;} public int TicketsAdded {get;} public int DiamondsAdded {get;}
        public long CoinIncome {get;} public long ConstructionDebit {get;}
        public int SlotPartsIncluded {get;} public int SlotPotatoesIncluded {get;}
        internal long CoinsBefore {get;} internal long CoinsAfter {get;}
        internal ArcadeEvent(long seq,string kind,long instance,int parts,int potatoes,int energy,int tickets,int diamonds,long income,long debit,int slotParts,int slotPotatoes,long coinsBefore,long coinsAfter)
        {
            if(seq<1||seq>ExpansionResourceReceipt.OrderSequenceMaximum||(kind!="spin"&&!ArcadeCatalog.Action(kind))||instance<0||instance>seq||parts<0||potatoes<0||energy<0||tickets<0||tickets>1||diamonds<0||diamonds>3||income<0||debit<0||slotParts<0||slotParts>parts||slotPotatoes<0||slotPotatoes>potatoes||coinsBefore<0||coinsAfter<0||
                checked(coinsBefore+income-debit)!=coinsAfter||kind!="spin"&&(income!=0||debit!=0||energy!=0||tickets!=0||slotParts!=0||slotPotatoes!=0))throw new ArgumentException("Invalid Arcade event receipt");
            Sequence=seq;Kind=kind;InstanceSequence=instance;PartsAdded=parts;PotatoesAdded=potatoes;EnergyAdded=energy;TicketsAdded=tickets;DiamondsAdded=diamonds;CoinIncome=income;ConstructionDebit=debit;SlotPartsIncluded=slotParts;SlotPotatoesIncluded=slotPotatoes;CoinsBefore=coinsBefore;CoinsAfter=coinsAfter;
        }
    }
    internal sealed class ArcadeRun
    {
        internal string Id {get;} internal long Instance {get;} internal uint SeedBefore {get;} internal uint SeedAfter {get;}
        internal string Transcript {get;} internal string Phase {get;} internal int Attempts {get;} internal int Correct {get;} internal int MatchedMask {get;}
        internal int First {get;} internal int Second {get;} internal int Reward {get;}
        readonly int[] board;
        internal int Cursor {get{return Transcript.Length;}} internal bool Ready {get{return Phase=="ready";}}
        internal int Symbol(int index){return board[index];}
        internal static bool PhaseKnown(string phase){return phase=="none"||phase=="first"||phase=="second"||phase=="mismatch"||phase=="sorting"||phase=="ready";}
        internal ArcadeRun(string id,long instance,uint seedBefore,uint seedAfter,string boardToken,string transcript)
        {
            if(!ArcadeCatalog.Game(id)||instance<1||instance>ExpansionResourceReceipt.OrderSequenceMaximum||seedBefore==0||seedAfter==0||transcript==null||transcript.Length>27)throw new ArgumentException("Invalid Arcade run");
            board=ArcadeCatalog.Shuffle(id,seedBefore,out uint expectedSeed);
            if(expectedSeed!=seedAfter||string.Concat(board.Select(x=>ExpansionCodec.N(x)))!=boardToken)throw new ArgumentException("Frozen challenge disagrees with seed");
            string phase=id==ArcadeCatalog.Memory?"first":"sorting";int attempts=0,correct=0,mask=0,first=-1,second=-1;
            foreach(char move in transcript)
            {
                if(phase=="ready")throw new ArgumentException("Input after terminal Arcade run");
                if(id==ArcadeCatalog.Sorting)
                {if(move<'0'||move>'3'||correct>8)throw new ArgumentException("Invalid sorting input");int index=attempts++;if(board[index]==move-'0')correct++;if(attempts==8)phase="ready";continue;}
                if(move=='c')
                {if(phase!="mismatch")throw new ArgumentException("Continue outside mismatch");first=second=-1;phase="first";continue;}
                int card=move-'0';if(card<0||card>5||phase=="mismatch"||(mask&(1<<card))!=0||card==first)throw new ArgumentException("Invalid memory input");
                if(phase=="first"){first=card;phase="second";}
                else
                {
                    second=card;attempts++;
                    if(board[first]==board[second]){mask|=(1<<first)|(1<<second);correct++;first=second=-1;phase="first";}else phase="mismatch";
                    if(mask==63||attempts==9)phase="ready";
                }
            }
            Id=id;Instance=instance;SeedBefore=seedBefore;SeedAfter=seedAfter;Transcript=transcript;Phase=phase;Attempts=attempts;Correct=correct;MatchedMask=mask;First=first;Second=second;
            Reward=phase!="ready"?-1:id==ArcadeCatalog.Memory?EconomyRules.ArcadeMemoryReward(attempts,mask==63):EconomyRules.ArcadeSortingReward(correct);
        }
        internal static ArcadeRun Start(string id,long instance,uint seed)
        {var board=ArcadeCatalog.Shuffle(id,seed,out uint after);return new ArcadeRun(id,instance,seed,after,string.Concat(board.Select(x=>ExpansionCodec.N(x))),"");}
        internal string BoardToken {get{return string.Concat(board.Select(x=>ExpansionCodec.N(x)));}}
        internal ArcadeRun Move(string action,int target)
        {
            if(Ready)throw new InvalidOperationException("Run is ready and unpaid");
            if(action=="pick"&&Id==ArcadeCatalog.Memory&&target>=0&&target<6&&Phase!="mismatch")return Append((char)('0'+target));
            if(action=="continue"&&Id==ArcadeCatalog.Memory&&target==-1&&Phase=="mismatch")return Append('c');
            if(action=="sort"&&Id==ArcadeCatalog.Sorting&&target>=0&&target<4)return Append((char)('0'+target));
            throw new InvalidOperationException("Arcade action does not match run phase");
        }
        ArcadeRun Append(char move){return new ArcadeRun(Id,Instance,SeedBefore,SeedAfter,BoardToken,Transcript+move);}
    }
    internal sealed class ArcadeState
    {
        internal long AdoptedSequence {get;} internal long ForegroundSpins {get;} internal long Diamonds {get;}
        internal int Tickets {get;} internal int TicketProgress {get;} internal int SlotLevel {get;} internal uint ChallengeSeed {get;}
        internal ParcelsState Parcels {get;}
        internal ArcadeRun Run {get;} internal ArcadeQuote LastCommand {get;} internal ArcadeQuote LastClaim {get;}
        internal ArcadeEvent LastResourceReceipt {get;} internal ArcadeEvent LastEvent {get;}
        internal static ArcadeState Empty {get;}=new ArcadeDraft().Freeze();
        internal ArcadeState(ArcadeDraft d)
        {
            if(d.AdoptedSequence<0||d.AdoptedSequence>ExpansionResourceReceipt.OrderSequenceMaximum||d.ForegroundSpins<0||d.ForegroundSpins>ExpansionResourceReceipt.OrderSequenceMaximum||d.Diamonds<0||d.Tickets<0||d.Tickets>3||d.TicketProgress<0||d.TicketProgress>7||d.SlotLevel<0||d.SlotLevel>3||d.ChallengeSeed==0||
                (d.ForegroundSpins==0)!=(d.LastResourceReceipt==null)||d.LastResourceReceipt!=null&&d.LastResourceReceipt.Kind!="spin"||d.Run!=null&&(d.Run.Instance<d.AdoptedSequence||d.Run.SeedAfter!=d.ChallengeSeed)||
                d.LastClaim!=null&&(d.LastClaim.Action!="claim"||d.LastClaim.Sequence<d.AdoptedSequence)||d.LastCommand!=null&&d.LastCommand.Sequence<d.AdoptedSequence||
                d.LastEvent!=null&&d.LastEvent.Sequence<d.AdoptedSequence||d.LastResourceReceipt!=null&&d.LastResourceReceipt.Sequence<d.AdoptedSequence||
                d.TicketProgress!=d.ForegroundSpins%EconomyRules.ArcadeTicketCadence||d.Tickets>Math.Min(EconomyRules.ArcadeTicketCap,d.ForegroundSpins/EconomyRules.ArcadeTicketCadence)||
                d.AdoptedSequence==0&&(d.ForegroundSpins!=0||d.Diamonds!=0||d.Tickets!=0||d.TicketProgress!=0||d.SlotLevel!=0||d.Run!=null||d.Parcels!=null||d.LastCommand!=null||d.LastClaim!=null||d.LastEvent!=null||d.ChallengeSeed!=EconomyRules.ArcadeInitialSeed))throw new ArgumentException("Invalid Arcade checkpoint");
            if(d.Run!=null&&d.Parcels?.Run!=null)throw new ArgumentException("Only one Arcade round may be active");
            Parcels=d.Parcels;
            AdoptedSequence=d.AdoptedSequence;ForegroundSpins=d.ForegroundSpins;Diamonds=d.Diamonds;Tickets=d.Tickets;TicketProgress=d.TicketProgress;SlotLevel=d.SlotLevel;ChallengeSeed=d.ChallengeSeed;Run=d.Run;LastCommand=d.LastCommand;LastClaim=d.LastClaim;LastResourceReceipt=d.LastResourceReceipt;LastEvent=d.LastEvent;
            if(AdoptedSequence>0)
            {
                long command=LastCommand?.Sequence??0,spin=LastResourceReceipt?.Sequence??0;
                if(LastEvent==null||LastEvent.Sequence!=Math.Max(command,spin)||command==spin||
                    LastClaim!=null&&(LastCommand==null||LastClaim.Sequence>command||Run!=null&&Run.Instance<=LastClaim.Sequence))throw new ArgumentException("Incomplete Arcade retained history");
                if(spin>command)
                {if(!ArcadeCodec.SameEvent(LastEvent,LastResourceReceipt))throw new ArgumentException("Latest Arcade event is not its frozen spin");}
                else
                {
                    var q=LastCommand;
                    if(LastEvent.Kind!=q.Action||LastEvent.InstanceSequence!=q.ExpectedInstance||LastEvent.PartsAdded!=q.PartsCredit||LastEvent.PotatoesAdded!=q.PotatoesCredit||LastEvent.DiamondsAdded!=q.DiamondsCredit)throw new ArgumentException("Latest Arcade event/command mismatch");
                }
                if(LastCommand!=null)
                {
                    var q=LastCommand;
                    if(q.Action=="claim"&&(LastClaim==null||LastClaim.Key!=q.Key)||
                        (q.Action=="claim"||q.Action=="abandon")&&Run!=null||
                        q.Action=="start"&&(Run==null||Run.Id!=q.Id||Run.Instance!=q.Sequence||Run.Cursor!=0)||
                        (q.Action=="pick"||q.Action=="continue"||q.Action=="sort")&&(Run==null||Run.Id!=q.Id||Run.Instance!=q.ExpectedInstance||Run.Cursor!=q.ExpectedCursor+1))throw new ArgumentException("Arcade run disagrees with latest command");
                }
            }
        }
    }
    internal sealed class ArcadeDraft
    {
        internal long AdoptedSequence,ForegroundSpins,Diamonds;internal int Tickets,TicketProgress,SlotLevel;internal uint ChallengeSeed=EconomyRules.ArcadeInitialSeed;
        internal ParcelsState Parcels;
        internal ArcadeRun Run;internal ArcadeQuote LastCommand,LastClaim;internal ArcadeEvent LastResourceReceipt,LastEvent;
        internal ArcadeDraft(){}
        internal ArcadeDraft(ArcadeState s){Parcels=s.Parcels;AdoptedSequence=s.AdoptedSequence;ForegroundSpins=s.ForegroundSpins;Diamonds=s.Diamonds;Tickets=s.Tickets;TicketProgress=s.TicketProgress;SlotLevel=s.SlotLevel;ChallengeSeed=s.ChallengeSeed;Run=s.Run;LastCommand=s.LastCommand;LastClaim=s.LastClaim;LastResourceReceipt=s.LastResourceReceipt;LastEvent=s.LastEvent;}
        internal ArcadeState Freeze(){return new ArcadeState(this);}
    }
    public sealed class ArcadeRunView
    {
        public string Id {get;} public long InstanceSequence {get;} public string Phase {get;} public int Cursor {get;} public int Attempts {get;} public int Correct {get;}
        public int MatchedMask {get;} public int MatchedPairs {get;} public int ReadyReward {get;} public bool Ready {get;}
        public IReadOnlyList<int> MemoryFaces {get;} public int CurrentSortingSymbol {get;}
        internal ArcadeRunView(ArcadeRun run)
        {
            Id=run.Id;InstanceSequence=run.Instance;Phase=run.Phase;Cursor=run.Cursor;Attempts=run.Attempts;Correct=run.Correct;MatchedMask=run.MatchedMask;MatchedPairs=run.Id==ArcadeCatalog.Memory?run.Correct:0;ReadyReward=run.Reward;Ready=run.Ready;
            int[] faces=run.Id==ArcadeCatalog.Memory?Enumerable.Range(0,6).Select(i=>(run.MatchedMask&(1<<i))!=0||i==run.First||i==run.Second?run.Symbol(i):-1).ToArray():new int[0];
            MemoryFaces=Array.AsReadOnly(faces);CurrentSortingSymbol=run.Id==ArcadeCatalog.Sorting&&!run.Ready?run.Symbol(run.Attempts):-1;
        }
    }
    public sealed class ArcadeClaimView
    {
        public long Sequence {get;} public long InstanceSequence {get;} public string GameId {get;} public int Diamonds {get;} public string Key {get;}
        internal ArcadeClaimView(ArcadeQuote q){Sequence=q.Sequence;InstanceSequence=q.ExpectedInstance;GameId=q.Id;Diamonds=q.DiamondsCredit;Key=q.Key;}
    }
    public sealed class ArcadeView
    {
        public long Sequence {get;} public bool IsAdopted {get;} public long AdoptedSequence {get;} public long ForegroundSpins {get;} public long Diamonds {get;}
        public int Tickets {get;} public int TicketCap {get{return EconomyRules.ArcadeTicketCap;}} public int TicketProgress {get;} public int TicketCadence {get{return EconomyRules.ArcadeTicketCadence;}}
        public int SlotLevel {get;} public int BoxPresencePartBonus {get{return ArcadeCatalog.SlotAttributes(SlotLevel).BoxPresencePartBonus;}} public int PotatoPresencePotatoBonus {get{return ArcadeCatalog.SlotAttributes(SlotLevel).PotatoPresencePotatoBonus;}}
        public int RawBonusPartsAddition {get{return ArcadeCatalog.SlotAttributes(SlotLevel).RawBonusPartsAddition;}} public int RawBonusPotatoesAddition {get{return ArcadeCatalog.SlotAttributes(SlotLevel).RawBonusPotatoesAddition;}}
        public string AnyActiveRunId {get;}
        public ArcadeRunView ActiveRun {get;} public ArcadeClaimView LastClaim {get;} public ArcadeEvent LastResourceReceipt {get;} public ArcadeEvent LastEvent {get;}
        internal ArcadeView(long sequence,ArcadeState s){AnyActiveRunId=s.Run?.Id??(s.Parcels?.Run==null?null:ParcelsCatalog.Id);Sequence=sequence;IsAdopted=s.AdoptedSequence!=0;AdoptedSequence=s.AdoptedSequence;ForegroundSpins=s.ForegroundSpins;Diamonds=s.Diamonds;Tickets=s.Tickets;TicketProgress=s.TicketProgress;SlotLevel=s.SlotLevel;ActiveRun=s.Run==null?null:new ArcadeRunView(s.Run);LastClaim=s.LastClaim==null?null:new ArcadeClaimView(s.LastClaim);LastResourceReceipt=s.LastResourceReceipt;LastEvent=s.LastEvent;}
    }
}
