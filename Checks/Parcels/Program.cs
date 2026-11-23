using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JunkToNeon.Core;
using JunkToNeon.Contracts;
using JunkToNeon.Content;
using JunkToNeon.Upgrades;

static class Program
{
    static readonly JsonStateCodec Codec=new JsonStateCodec();static string Root;
    static readonly string[] Ids={"parcels.box-priority","parcels.matching-pair","parcels.keep-the-pair","parcels.no-boxes"};
    static readonly string[] Symbols={"PBBTGG","PPBTTG","PPPGBT","BBBPTT"};
    static readonly int[][] Histograms={new[]{228,120,12},new[]{272,80,8},new[]{60,288,12},new[]{72,240,48}};
    static readonly int[][] Solutions={new[]{0,3,1,2},new[]{3,4,0,5},new[]{0,3,4,5},new[]{0,1,3,4}};
    static readonly int[][] Traps={new[]{0,1,-1,-1},new[]{0,1,-1,-1},new[]{0,4,-1,-1},new[]{0,3,-1,-1}};
    sealed class Storage:ISnapshotStorage
    {
        public string Primary,Backup;public int Reads,Writes,FailAt=-1;public bool Land;public Action Inspect;
        public string ReadPrimary(){Reads++;return Primary;}public string ReadBackup(){Reads++;return Backup;}
        public void Write(string raw){Writes++;Inspect?.Invoke();if(Writes==FailAt&&!Land)throw new IOException("Private before landing fault");Backup=Primary;Primary=raw;if(Writes==FailAt)throw new IOException("Private after landing fault");}
    }
    static void Need(bool b,string why){if(!b)throw new Exception(why);}
    static string Raw(ModelState s)=>Codec.Encode(s);
    static void Reject(Action action){try{action();}catch(ArgumentException){return;}catch(InvalidOperationException){return;}catch(FormatException){return;}catch(OverflowException){return;}throw new Exception("Expected refusal");}
    static Storage Stored(ModelState s)=>new Storage{Primary=Raw(s)};
    static GameSession Open(Storage s){var g=GameSession.Open(new GameStore(s,Codec),1);Need(!g.RecoveryRequired,"Open failed "+g.Error);return g;}
    static ModelState Input(string relative)=>Codec.Decode(File.ReadAllText(Path.Combine(Root,relative)));
    static void NoIo(Storage s,GameSession g,Action action,bool refuse=true)
    {string p=s.Primary,b=s.Backup,r=Raw(g.State);int reads=s.Reads,writes=s.Writes;if(refuse)Reject(action);else action();Need(p==s.Primary&&b==s.Backup&&r==Raw(g.State)&&reads==s.Reads&&writes==s.Writes&&!g.RecoveryRequired,"Read/refusal/noop changed IO/state/health");}
    static ModelState Ownership(bool loft=true,bool later=false)
    {
        var s=ModelState.Fresh(1);s.coins=100;s.lastSettledAt=1000;
        int[] levels=later?new[]{3,3,3,3,3,3}:new[]{1,1,1,loft?1:0,1,1};string era=later?EraIds.WorkshopYard:EraIds.Junkyard;
        var progress=ChapterCatalog.Rules.FromStages(new FamilyStages(era,FamilyIds.All.Select((id,i)=>new FamilyStage(id,levels[i]))));long entry=later?13:0;
        s.sequence=Math.Max(progress.CompletedUpgradeIds.Count,entry);s.revision=s.sequence*2;s.houseStage=Math.Min(levels[0],2);s.autoHintSeen=1;s.workshopBuilt=1;
        s.wave=new WaveProgress(1,loft||later?1:0,WaveProgress.Empty.Delivery,UpgradeOwnership.Empty);
        s.chapter=new ChapterState(2,ChapterContentIds.RuleRevision,progress,SkillOwnership.Empty,null,entry,entry);GameLogic.Validate(s);return s;
    }
    static ModelState Funded(bool loft=true,bool later=false)
    {var s=Ownership(loft,later);for(int i=0;i<24;i++)s=GameLogic.ApplyPending(GameLogic.Reserve(s,"spin",0,s.sequence+1,1000));return s;}
    static ParcelsQuote Quote(GameSession g,string action,int token=-1,int slot=-1)=>g.GetParcelsQuote(action,g.State.sequence,g.Parcels.ActiveRun?.InstanceSequence??0,token,slot);
    static void Do(GameSession g,string action,int token=-1,int slot=-1)=>g.ExecuteParcels(Quote(g,action,token,slot),1000);
    static void Arrange(GameSession g,int[] slots){for(int i=0;i<4;i++)Do(g,"place",slots[i],i);}
    static int Score(int board,int[] slots,int request)
    {
        int i=slots[request*2],j=slots[request*2+1];if(i<0||j<0)return 0;char a=Symbols[board][i],b=Symbols[board][j];
        bool result=board==0?(request==0?a!=b&&(a=='P'||b=='P'):a=='B'&&b=='B'):
            board==1?(request==0?a==b:a!=b&&(a=='P'||b=='P')&&(a=='G'||b=='G')):
            board==2?(request==0?a!=b:a!=b&&(a=='B'||b=='B')&&(a=='T'||b=='T')):
            request==0?a=='B'||b=='B':a!=b&&a!='B'&&b!='B';return result?1:0;
    }
    static IEnumerable<int[]> Assignments()
    {for(int a=0;a<6;a++)for(int b=0;b<6;b++)for(int c=0;c<6;c++)for(int d=0;d<6;d++){int[] x={a,b,c,d};if(x.Distinct().Count()==4)yield return x;}}
    static void Catalog()
    {
        Need(ParcelsCatalog.BoardIds.SequenceEqual(Ids),"Stable board order");
        for(int board=0;board<4;board++)
        {
            var histogram=new int[3];int count=0;foreach(var x in Assignments())
            {int a=Score(board,x,0),b=Score(board,x,1);Need(ParcelsCatalog.RequestSatisfied(Ids[board],0,x)==(a==1)&&ParcelsCatalog.RequestSatisfied(Ids[board],1,x)==(b==1),"Independent request predicate "+Ids[board]);Need(ParcelsCatalog.SatisfiedRequests(Ids[board],x)==a+b,"Independent total score");histogram[a+b]++;count++;}
            Need(count==360&&histogram.SequenceEqual(Histograms[board]),"Literal360/histogram "+Ids[board]);
            Need(ParcelsCatalog.SatisfiedRequests(Ids[board],Solutions[board])==2,"Example success");Need(ParcelsCatalog.RequestSatisfied(Ids[board],0,Traps[board]),"Local trap must satisfy A");
            Need(!Assignments().Any(x=>x[0]==Traps[board][0]&&x[1]==Traps[board][1]&&Score(board,x,0)+Score(board,x,1)==2),"Trap incorrectly has extension");
            Need(ParcelsCatalog.SatisfiedRequests(Ids[board],new[]{-1,-1,-1,-1})==0,"Empty requests false");
        }
        Need(ParcelsCatalog.Reward(0)==0&&ParcelsCatalog.Reward(1)==1&&ParcelsCatalog.Reward(2)==3,"Whole reward table");
        foreach(int[] bad in new[]{new[]{0,0,1,2},new[]{0,1,2,6},new[]{0,1,2,-2},new[]{0,1,2}})Reject(()=>ParcelsCatalog.SatisfiedRequests(Ids[0],bad));
    }
    static void EligibilityCycle()
    {
        var st=Stored(Funded(false));var g=Open(st);Need(!g.Parcels.Unlocked,"Five other constructions cannot unlock Loft");NoIo(st,g,()=>Quote(g,"start"));
        st=Stored(Ownership());g=Open(st);Need(g.Parcels.Unlocked,"Actual LoftI unlock");NoIo(st,g,()=>Quote(g,"start"));
        foreach(bool later in new[]{false,true})
        {
            st=Stored(Funded(true,later));g=Open(st);Need(g.Parcels.Unlocked,"Normalized Loft across eras");uint seed=g.State.seed,challenge=g.State.expansion.ArcadeData.ChallengeSeed;
            for(int i=0;i<5;i++)
            {
                if(g.Arcade.Tickets==0)for(int j=0;j<8;j++)g.ExecuteChapterSpin(g.State.sequence+1,1000);
                int before=g.Arcade.Tickets,next=g.Parcels.NextBoardIndex;var q=Quote(g,"start");NoIo(st,g,()=>{var view=g.Parcels;Quote(g,"start");},false);
                g.ExecuteParcels(q,1000);Need(g.Arcade.Tickets==before-1&&g.Parcels.ActiveRun.BoardId==Ids[i%4]&&g.Parcels.NextBoardIndex==(next+1)%4,"Start one ticket/cycle");
                NoIo(st,g,()=>g.ExecuteParcels(q,1000),false);Need(g.State.expansion.ArcadeData.ChallengeSeed==challenge,"Parcels consumed classic RNG");
                NoIo(st,g,()=>g.GetArcadeQuote("start",ArcadeCatalog.Memory,g.State.sequence));NoIo(st,g,()=>Quote(g,"start"));
                var reopened=Open(st);Need(reopened.Parcels.NextBoardIndex==g.Parcels.NextBoardIndex&&reopened.Parcels.ActiveRun.BoardId==Ids[i%4],"Reload advanced board");Do(g,"abandon");Need(g.Arcade.Tickets==before-1,"Abandon refund");
            }
        }
        st=Stored(Funded());g=Open(st);g.ExecuteArcade(g.GetArcadeQuote("start",ArcadeCatalog.Sorting,g.State.sequence));NoIo(st,g,()=>Quote(g,"start"));
    }
    static void Moves()
    {
        var st=Stored(Funded());var g=Open(st);Do(g,"start");long diamonds=g.Arcade.Diamonds,coins=g.State.coins;int tickets=g.Arcade.Tickets;
        Do(g,"place",0,0);Do(g,"place",1,1);Do(g,"place",0,2);Need(g.Parcels.ActiveRun.Slots.SequenceEqual(new[]{-1,1,0,-1}),"Move clears source");
        Do(g,"place",2,1);Need(g.Parcels.ActiveRun.Slots.SequenceEqual(new[]{-1,2,0,-1}),"Replacement returns displaced token; no swap");Do(g,"place",-1,1);Need(g.Parcels.ActiveRun.Slots.SequenceEqual(new[]{-1,-1,0,-1}),"Remove token");
        NoIo(st,g,()=>Do(g,"place",0,2),false);NoIo(st,g,()=>Do(g,"place",-1,0),false);
        foreach(int token in new[]{-2,6,int.MaxValue})NoIo(st,g,()=>Quote(g,"place",token,0));foreach(int slot in new[]{-1,4,int.MaxValue})NoIo(st,g,()=>Quote(g,"place",0,slot));
        var stale=Quote(g,"place",3,3);Do(g,"place",4,3);NoIo(st,g,()=>g.ExecuteParcels(stale,1000));
        int max=0,min=int.MaxValue;for(int i=0;i<64;i++){Do(g,"place",i%2==0?5:-1,0);string raw=Raw(g.State);max=Math.Max(max,raw.Length);min=Math.Min(min,raw.Length);Need(Raw(Codec.Decode(raw))==raw,"Move reload bytes");}
        Need(max-min<128&&max<64000,"Moves grew transcript payload");Need(g.Arcade.Diamonds==diamonds&&g.State.coins==coins&&g.Arcade.Tickets==tickets,"Free moves spent/granted");
        Need(Open(st).Parcels.ActiveRun.Slots.SequenceEqual(g.Parcels.ActiveRun.Slots),"Cold restore exact assignment");
    }
    static void ScoresAndClaims()
    {
        int[][] layouts={new[]{1,2,3,4},new[]{0,1,2,3},new[]{0,3,1,2}};int[] rewards={0,1,3};
        for(int score=0;score<3;score++)
        {
            var st=Stored(Funded());var g=Open(st);Do(g,"start");NoIo(st,g,()=>Quote(g,"finish"));Arrange(g,layouts[score]);
            Need(g.Parcels.ActiveRun.Complete&&g.Parcels.ActiveRun.SatisfiedRequests==score&&g.Parcels.ActiveRun.PredictedReward==rewards[score],"Exact0/1/2 prediction");
            long before=g.Arcade.Diamonds;var finish=Quote(g,"finish");g.ExecuteParcels(finish,1000);
            Need(g.Parcels.ActiveRun.Ready&&g.Parcels.ActiveRun.ReadyReward==rewards[score]&&g.Arcade.Diamonds==before,"Finish credits or rescores");
            NoIo(st,g,()=>g.ExecuteParcels(finish,1000),false);NoIo(st,g,()=>Quote(g,"place",0,0));
            g=Open(st);Need(g.Parcels.ActiveRun.Ready&&g.Parcels.ActiveRun.ReadyReward==rewards[score],"Ready reload");var claim=Quote(g,"claim");g.ExecuteParcels(claim,1000);
            Need(g.Parcels.ActiveRun==null&&g.Arcade.Diamonds==before+rewards[score],"Separate exact claim");NoIo(st,g,()=>g.ExecuteParcels(claim,1000),false);
            Do(g,"start");string newer=Raw(g.State);NoIo(st,g,()=>g.ExecuteParcels(claim,1000),false);Need(Raw(g.State)==newer,"Old claim cleared new run");
        }
        var baseState=Funded();var e=new ExpansionDraft(baseState.expansion);var a=new ArcadeDraft(e.ArcadeData){Diamonds=long.MaxValue-2};e.ArcadeData=a.Freeze();baseState.expansion=e.Freeze();
        var storage=Stored(baseState);var game=Open(storage);Do(game,"start");Arrange(game,Solutions[0]);Do(game,"finish");NoIo(storage,game,()=>Quote(game,"claim"));
    }
    static void Faults()
    {
        foreach(string action in new[]{"start","place","finish","claim","abandon"})
        {
            var prepare=Open(Stored(Funded()));if(action!="start")Do(prepare,"start");if(action=="finish"||action=="claim")Arrange(prepare,Solutions[0]);if(action=="claim")Do(prepare,"finish");
            var initial=prepare.State;
            foreach(int fail in new[]{1,2})foreach(bool land in new[]{false,true})
            {
                var st=Stored(initial);var g=Open(st);var q=Quote(g,action,action=="place"?0:-1,action=="place"?0:-1);string before=Raw(g.State);var expectedStorage=Stored(initial);var expected=Open(expectedStorage);expected.ExecuteParcels(q,1000);string result=Raw(expected.State);
                st.FailAt=fail;st.Land=land;bool threw=false;try{g.ExecuteParcels(q,1000);}catch(IOException){threw=true;}Need(threw&&g.RecoveryRequired,"Fault not blocked "+action);
                st.FailAt=-1;g=Open(st);Need(Raw(g.State)==(fail==2||land?result:before),"Exact frozen recovery "+action);int writes=st.Writes;if(fail==2||land)NoIo(st,g,()=>g.ExecuteParcels(q,1000),false);Open(st);Need(st.Writes==writes,"Repeated recovery wrote "+action);
            }
            var normalStorage=Stored(initial);var normal=Open(normalStorage);var quote=Quote(normal,action,action=="place"?0:-1,action=="place"?0:-1);
            normalStorage.Inspect=()=>{var publicState=normal.State;Need(publicState.coins==initial.coins&&publicState.expansion.Parts==initial.expansion.Parts&&publicState.expansion.Potatoes==initial.expansion.Potatoes&&publicState.expansion.Energy==initial.expansion.Energy&&publicState.expansion.ArcadeData.Tickets==initial.expansion.ArcadeData.Tickets&&publicState.expansion.ArcadeData.Diamonds==initial.expansion.ArcadeData.Diamonds,"Public intermediate economic adoption "+action);};
            normal.ExecuteParcels(quote,1000);Need(normalStorage.Writes==2,"One action/two writes "+action);
            var after=normal.State;var isolatedExpected=initial.Copy();isolatedExpected.sequence=after.sequence;isolatedExpected.revision=after.revision;isolatedExpected.lastSettledAt=after.lastSettledAt;
            var expansion=new ExpansionDraft(initial.expansion);var arcade=new ArcadeDraft(expansion.ArcadeData){Parcels=after.expansion.ArcadeData.Parcels};
            arcade.Tickets-=action=="start"?1:0;arcade.Diamonds+=action=="claim"?3:0;expansion.ArcadeData=arcade.Freeze();isolatedExpected.expansion=expansion.Freeze();
            Need(Raw(isolatedExpected)==Raw(after),"Unrelated root/resources/RNG/Order/carry/ACK changed "+action);
        }
    }
    static void Tamper()
    {
        var g=Open(Stored(Funded()));Do(g,"start");Arrange(g,Solutions[0]);var q=Quote(g,"finish");var pending=ParcelsEconomy.Reserve(g.State,q,1000);string token=ExpansionCodec.EncodeOperation(pending.pending.expansion);
        Need(token.StartsWith("expansion.op.v5~"),"Parcels must dispatch before state/batch versions");Need(Raw(Codec.Decode(Raw(pending)))==Raw(pending),"V5 canonical roundtrip");
        foreach(int field in new[]{0,1,2,3,6,7,10,11,12})
        {
            var f=token.Split('~');f[field]=field==0?"expansion.op.v4":field==1?ExpansionBatchQuote.Revision:field==2?"expansion.batch":field==3?"0":field==10||field==11?"999":f[field]+"!";
            f[f.Length-1]=ExpansionCodec.Hash(string.Join("~",f.Take(f.Length-1)));string forged=string.Join("~",f);
            Reject(()=>{var x=pending.Copy();x.pending.expansion=ExpansionCodec.DecodeOperation(forged);GameLogic.Validate(x);});
        }
        string run=ParcelsCodec.RunToken(g.State.expansion.ArcadeData.Parcels.Run);
        foreach(int field in Enumerable.Range(0,7))
        {var f=run.Split('/');f[field]=field==0?"parcels.rules.v99":field==1?"parcels.unknown":field==2?"0":field==3?"111111":field==4?"0012":field==5?"ready":"3";Reject(()=>ParcelsCodec.Run(string.Join("/",f)));}
        var classic=Input("Checks/ArcadeCore/Evidence/memory-partial.json").expansion.ArcadeData.Run;
        Reject(()=>new ArcadeRun(classic.Id,classic.Instance,classic.SeedBefore,classic.SeedAfter,classic.BoardToken,new string('0',28)));
    }
    static void HistoryGraft()
    {
        var early=Ownership();for(int i=0;i<8;i++)early=GameLogic.ApplyPending(GameLogic.Reserve(early,"spin",0,early.sequence+1,1000));
        var game=Open(Stored(early));Do(game,"start");var oldParcels=game.State.expansion.ArcadeData.Parcels;
        var later=Funded();Need(later.lastSpin.sequence>oldParcels.LastCommand.Sequence&&later.lastSpin.expansion.After.ArcadeData.Parcels==null,"Independent graft boundary");
        var e=new ExpansionDraft(later.expansion);var a=new ArcadeDraft(e.ArcadeData){Parcels=oldParcels};e.ArcadeData=a.Freeze();later.expansion=e.Freeze();
        // A new current Parcels checkpoint cannot claim existence before a retained
        // spin whose frozen aggregate says that feature was still absent.
        Reject(()=>GameLogic.Validate(later));
    }
    static void CompatibilityAndIsolation()
    {
        foreach(string file in new[]{"memory-partial","sorting-ready-three","memory-zero-ready","claim-pending","claim-committed","selected540-spin"})
        {string raw=File.ReadAllText(Path.Combine(Root,"Checks/ArcadeCore/Evidence",file+".json"));Need(Raw(Codec.Decode(raw))==raw,"Literal classic bytes "+file);}
        var s=Funded();var g=Open(Stored(s));Do(g,"start");Arrange(g,Solutions[0]);Do(g,"finish");Do(g,"claim");
        var e=new ExpansionDraft(g.State.expansion){Parts=95,Potatoes=83};s=g.State;s.expansion=e.Freeze();g=Open(Stored(s));g.BuyExpansionBatch(g.GetExpansionBatchOffer("trade.parts",3),1000);Need(g.Expansion.Parts==35&&g.State.coins==s.coins+60,"Current batch after Parcels");
        g.ExecuteArcade(g.GetArcadeQuote("start",ArcadeCatalog.Sorting,g.State.sequence),1000);Need(g.Arcade.ActiveRun.Id==ArcadeCatalog.Sorting,"Classic after Parcels+batch");
        for(int i=0;i<8;i++)g.ExecuteArcade(g.GetArcadeQuote("sort",ArcadeCatalog.Sorting,g.State.sequence,g.Arcade.ActiveRun.InstanceSequence,g.Arcade.ActiveRun.CurrentSortingSymbol),1000);
        Need(g.Arcade.ActiveRun.Ready&&g.Arcade.ActiveRun.ReadyReward==3,"Classic score unchanged");g.ExecuteArcade(g.GetArcadeQuote("claim",ArcadeCatalog.Sorting,g.State.sequence,g.Arcade.ActiveRun.InstanceSequence),1000);
        Do(g,"start");Need(g.Parcels.ActiveRun.BoardId==Ids[1],"Classic/batch advanced Parcels cursor");
        string beforeRun=g.Parcels.ActiveRun.BoardId;var slots=g.Parcels.ActiveRun.Slots.ToArray();int tickets=g.Arcade.Tickets;for(int i=0;i<8;i++)g.ExecuteChapterSpin(g.State.sequence+1,1000);
        Need(g.Arcade.Tickets==Math.Min(3,tickets+1)&&g.Parcels.ActiveRun.BoardId==beforeRun&&g.Parcels.ActiveRun.Slots.SequenceEqual(slots),"Active ticket cadence mutated puzzle");
        Arrange(g,Solutions[1]);Do(g,"finish");tickets=g.Arcade.Tickets;for(int i=0;i<8;i++)g.ExecuteChapterSpin(g.State.sequence+1,1000);Need(g.Arcade.Tickets==Math.Min(3,tickets+1)&&g.Parcels.ActiveRun.ReadyReward==3,"Ready ticket cadence mutated reward");
    }
    static int Main(string[] args)
    {
        try{Root=Path.GetFullPath(args.Length>0?args[0]:Directory.GetCurrentDirectory());var groups=new Dictionary<string,Action>{{"catalog1440",Catalog},{"eligibility-cycle",EligibilityCycle},{"moves-bounded",Moves},{"scores-claims-overflow",ScoresAndClaims},{"write-faults-recovery",Faults},{"compatibility-isolation",CompatibilityAndIsolation},{"codec-tamper",Tamper},{"history-graft",HistoryGraft}};
            foreach(var group in groups){if(args.Length>1&&args[1]!=group.Key)continue;group.Value();Console.WriteLine("PASS "+group.Key);}return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
