using System;
using System.Collections.Generic;
using System.Linq;

namespace JunkToNeon.Core
{
    public static class ParcelsCatalog
    {
        public const string Id="arcade.parcels", Revision="parcels.rules.v1";
        static readonly string[] ids={"parcels.box-priority","parcels.matching-pair","parcels.keep-the-pair","parcels.no-boxes"};
        static readonly int[][] tokens={new[]{0,1,1,2,3,3},new[]{0,0,1,2,2,3},new[]{0,0,0,3,1,2},new[]{1,1,1,0,2,2}};
        static readonly string[][] requests={new[]{"Two different symbols, including Potato","Two Boxes"},new[]{"Two matching symbols","One Potato and one Pigeon"},new[]{"Two different symbols","One Box and one Trash"},new[]{"At least one Box","Two different symbols, neither a Box"}};
        public static IReadOnlyList<string> BoardIds {get;}=Array.AsReadOnly(ids);
        internal static int Index(string id){int i=Array.IndexOf(ids,id);if(i<0)throw new ArgumentException("Unknown Parcels board");return i;}
        public static IReadOnlyList<int> TokenSymbols(string boardId){return Array.AsReadOnly(tokens[Index(boardId)]);}
        public static string RequestText(string boardId,int requestIndex)
        {if(requestIndex<0||requestIndex>1)throw new ArgumentOutOfRangeException(nameof(requestIndex));return requests[Index(boardId)][requestIndex];}
        internal static void ValidateSlots(IReadOnlyList<int> slots)
        {
            if(slots==null||slots.Count!=4)throw new ArgumentException("Parcels requires four slots");int mask=0;
            foreach(int token in slots){if(token < -1||token>5||token>=0&&(mask&(1<<token))!=0)throw new ArgumentException("Invalid/duplicate Parcels token");if(token>=0)mask|=1<<token;}
        }
        public static bool RequestSatisfied(string boardId,int requestIndex,IReadOnlyList<int> slots)
        {
            ValidateSlots(slots);int b=Index(boardId);if(requestIndex<0||requestIndex>1)throw new ArgumentOutOfRangeException(nameof(requestIndex));
            int left=slots[requestIndex*2],right=slots[requestIndex*2+1];if(left<0||right<0)return false;
            int x=tokens[b][left],y=tokens[b][right];
            if(b==0)return requestIndex==0?x!=y&&(x==0||y==0):x==1&&y==1;
            if(b==1)return requestIndex==0?x==y:(x==0&&y==3||x==3&&y==0);
            if(b==2)return requestIndex==0?x!=y:(x==1&&y==2||x==2&&y==1);
            return requestIndex==0?x==1||y==1:x!=y&&x!=1&&y!=1;
        }
        public static int SatisfiedRequests(string boardId,IReadOnlyList<int> slots)
        {return (RequestSatisfied(boardId,0,slots)?1:0)+(RequestSatisfied(boardId,1,slots)?1:0);}
        public static int Reward(int satisfied){if(satisfied<0||satisfied>2)throw new ArgumentOutOfRangeException(nameof(satisfied));return satisfied==2?3:satisfied;}
        internal static bool Action(string action){return action=="start"||action=="place"||action=="finish"||action=="claim"||action=="abandon";}
        internal static string SlotsToken(IReadOnlyList<int> slots){ValidateSlots(slots);return string.Concat(slots.Select(x=>x<0?"-":ExpansionCodec.N(x)));}
        internal static int[] ReadSlots(string token)
        {if(token==null||token.Length!=4)throw new FormatException("Invalid Parcels slot token");var a=token.Select(c=>c=='-'?-1:c-'0').ToArray();ValidateSlots(a);return a;}
    }
    internal sealed class ParcelsRun
    {
        internal long Instance {get;} internal string BoardId {get;} internal bool Ready {get;} internal int ReadyReward {get;}
        internal IReadOnlyList<int> Slots {get;}
        internal ParcelsRun(long instance,string boardId,IReadOnlyList<int> slots,bool ready,int reward)
        {
            ParcelsCatalog.Index(boardId);ParcelsCatalog.ValidateSlots(slots);
            if(instance<1||instance>ExpansionResourceReceipt.OrderSequenceMaximum||ready&&(slots.Any(x=>x<0)||reward!=ParcelsCatalog.Reward(ParcelsCatalog.SatisfiedRequests(boardId,slots)))||!ready&&reward!=-1)
                throw new ArgumentException("Invalid Parcels run");
            Instance=instance;BoardId=boardId;Slots=Array.AsReadOnly(slots.ToArray());Ready=ready;ReadyReward=reward;
        }
        internal ParcelsRun Place(int token,int slot)
        {
            if(Ready||token < -1||token>5||slot<0||slot>3)throw new InvalidOperationException("Invalid Parcels placement");
            var a=Slots.ToArray();if(token>=0)for(int i=0;i<4;i++)if(a[i]==token)a[i]=-1;a[slot]=token;
            return new ParcelsRun(Instance,BoardId,a,false,-1);
        }
        internal ParcelsRun Finish()
        {if(Ready||Slots.Any(x=>x<0))throw new InvalidOperationException("Finish requires four filled slots");return new ParcelsRun(Instance,BoardId,Slots,true,ParcelsCatalog.Reward(ParcelsCatalog.SatisfiedRequests(BoardId,Slots)));}
    }
    public sealed class ParcelsQuote
    {
        public const string Revision="parcels.command.v1";
        public string RuleRevision {get{return Revision;}}
        public string Id {get{return ParcelsCatalog.Id;}}
        public string Action {get;} public long ExpectedSequence {get;} public long Sequence {get{return checked(ExpectedSequence+1);}}
        public long ExpectedInstance {get;} public int TokenId {get;} public int TargetSlot {get;} public string BoardId {get;}
        public int ResultReward {get{return ParcelsCatalog.Reward(ParcelsCatalog.SatisfiedRequests(BoardId,ParcelsCatalog.ReadSlots(ResultSlots)));}}
        public int DiamondsCredit {get;} public int TicketsCost {get{return Action=="start"?1:0;}} public string Key {get;}
        internal string Fingerprint {get;} internal string ResultSlots {get;}
        internal ParcelsQuote(string action,long sequence,long instance,int token,int slot,string board,string resultSlots,int credit,string fingerprint)
        {
            ParcelsCatalog.Index(board);ParcelsCatalog.ReadSlots(resultSlots);
            if(!ParcelsCatalog.Action(action)||sequence<0||sequence>=ExpansionResourceReceipt.OrderSequenceMaximum||instance<0||instance>sequence||
                (action=="start"?instance!=0:instance==0)||(action=="place"?(token < -1||token>5||slot<0||slot>3):(token!=-1||slot!=-1))||
                credit<0||credit>3||credit==2||action!="claim"&&credit!=0||!ExpansionCodec.IsKey(fingerprint))throw new ArgumentException("Invalid Parcels quote");
            if(action=="start"&&resultSlots!="----"||(action=="finish"||action=="claim")&&resultSlots.Contains("-")||
                action=="claim"&&credit!=ParcelsCatalog.Reward(ParcelsCatalog.SatisfiedRequests(board,ParcelsCatalog.ReadSlots(resultSlots))))throw new ArgumentException("Invalid Parcels result quote");
            Action=action;ExpectedSequence=sequence;ExpectedInstance=instance;TokenId=token;TargetSlot=slot;BoardId=board;ResultSlots=resultSlots;DiamondsCredit=credit;Fingerprint=fingerprint;
            Key=ExpansionCodec.Hash(ParcelsCodec.QuoteBody(this));
        }
    }
    internal sealed class ParcelsState
    {
        internal int NextBoardIndex {get;} internal ParcelsRun Run {get;} internal ParcelsQuote LastCommand {get;} internal ParcelsQuote LastClaim {get;}
        internal ParcelsState(int next,ParcelsRun run,ParcelsQuote command,ParcelsQuote claim)
        {
            if(next<0||next>3||command==null||claim!=null&&(claim.Action!="claim"||claim.Sequence>command.Sequence)||
                run!=null&&(next!=(ParcelsCatalog.Index(run.BoardId)+1)%4||run.Instance>command.Sequence||run.Instance<= (claim?.Sequence??0)))throw new ArgumentException("Invalid Parcels checkpoint");
            if(command.Action=="claim"&&(claim==null||command.Key!=claim.Key)||
                (command.Action=="claim"||command.Action=="abandon")&&run!=null||
                (command.Action=="start"||command.Action=="place"||command.Action=="finish")&&(run==null||run.BoardId!=command.BoardId||
                    run.Instance!=(command.Action=="start"?command.Sequence:command.ExpectedInstance)||ParcelsCatalog.SlotsToken(run.Slots)!=command.ResultSlots||run.Ready!=(command.Action=="finish")))
                throw new ArgumentException("Parcels run disagrees with retained command");
            if(next!=(ParcelsCatalog.Index(command.BoardId)+1)%4)throw new ArgumentException("Parcels board cycle mismatch");
            NextBoardIndex=next;Run=run;LastCommand=command;LastClaim=claim;
        }
    }
    public sealed class ParcelsRunView
    {
        public string Id {get{return ParcelsCatalog.Id;}} public long InstanceSequence {get;} public string BoardId {get;}
        public IReadOnlyList<int> TokenSymbols {get;} public IReadOnlyList<int> Slots {get;}
        public bool Ready {get;} public bool Complete {get;} public int SatisfiedRequests {get;} public int PredictedReward {get;} public int ReadyReward {get;}
        public string RequestA {get;} public string RequestB {get;} public bool RequestASatisfied {get;} public bool RequestBSatisfied {get;}
        internal ParcelsRunView(ParcelsRun run)
        {
            InstanceSequence=run.Instance;BoardId=run.BoardId;TokenSymbols=ParcelsCatalog.TokenSymbols(BoardId);Slots=run.Slots;Ready=run.Ready;Complete=Slots.All(x=>x>=0);
            SatisfiedRequests=ParcelsCatalog.SatisfiedRequests(BoardId,Slots);PredictedReward=ParcelsCatalog.Reward(SatisfiedRequests);ReadyReward=run.ReadyReward;
            RequestA=ParcelsCatalog.RequestText(BoardId,0);RequestB=ParcelsCatalog.RequestText(BoardId,1);RequestASatisfied=ParcelsCatalog.RequestSatisfied(BoardId,0,Slots);RequestBSatisfied=ParcelsCatalog.RequestSatisfied(BoardId,1,Slots);
        }
    }
    public sealed class ParcelsView
    {
        public long Sequence {get;} public bool Unlocked {get;} public int NextBoardIndex {get;} public ParcelsRunView ActiveRun {get;}
        public ParcelsQuote LastCommand {get;} public ParcelsQuote LastClaim {get;}
        internal ParcelsView(ModelState state)
        {
            Sequence=state.sequence;Unlocked=ParcelsEconomy.Unlocked(state);var p=ArcadeEconomy.Data(ExpansionEconomy.Project(state)).Parcels;
            NextBoardIndex=p?.NextBoardIndex??0;ActiveRun=p?.Run==null?null:new ParcelsRunView(p.Run);LastCommand=p?.LastCommand;LastClaim=p?.LastClaim;
        }
    }
}
