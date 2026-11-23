using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon.Core;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;

internal static class Program
{
    sealed class Storage:ISnapshotStorage
    {
        public string raw,backup;public int writes,failAt;public bool failAfterCommit;
        public string ReadPrimary()=>raw;public string ReadBackup()=>backup;
        public void Write(string value){writes++;if(writes==failAt&&!failAfterCommit)throw new IOException("r8 before write");backup=raw;raw=value;if(writes==failAt)throw new IOException("r8 after committed write");}
    }
    sealed class Expected
    {
        public string name;public long coins,seq;public uint seed;public int skills,carry;public bool canonical,finishedSelection;
        public Expected(string name,long coins,long seq,int skills,uint seed=1,int carry=0,bool canonical=false,bool finishedSelection=false)
        {this.name=name;this.coins=coins;this.seq=seq;this.skills=skills;this.seed=seed;this.carry=carry;this.canonical=canonical;this.finishedSelection=finishedSelection;}
    }
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static int assertions,groups;
    static void Check(bool ok,string why){assertions++;if(!ok)throw new Exception(why);}
    static void Reject(Action act,string why){assertions++;try{act();}catch{return;}throw new Exception(why);}
    static void Pass(string why){groups++;Console.WriteLine("PASS r8 compatibility: "+why);}
    static string Raw(string name)=>File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",name+".json")).TrimEnd('\r','\n');
    static GameSession Open(Storage storage)=>GameSession.Open(new GameStore(storage,Codec),999);
    static long OldPrice(string id)
    {switch(id){case "slot.coin-boost":return 120;case "skill.universal.2":return 360;case "skill.recycling.1":return 160;case "skill.recycling.2":return 480;case "skill.delivery.1":return 180;case "skill.delivery.2":return 540;default:throw new Exception("Unknown historical skill");}}
    static Expected[] Cases()=>new[]{
        new Expected("ready-no-skills",10000,19,0),new Expected("pending-universal-1",9880,20,1),new Expected("owned-universal-1",9880,20,1),
        new Expected("pending-universal-2",9520,21,2),new Expected("owned-universal-2",9520,21,2),
        new Expected("pending-recycling-1",9840,20,1),new Expected("owned-recycling-1",9840,20,1),
        new Expected("pending-recycling-2",9360,21,2),new Expected("owned-recycling-2",9360,21,2),
        new Expected("pending-delivery-1",9820,20,1),new Expected("owned-delivery-1",9820,20,1),
        new Expected("pending-delivery-2",9280,21,2),new Expected("owned-delivery-2",9280,21,2),
        new Expected("all-skills-owned",8160,25,6),new Expected("pending-all-skills-spin",7484,26,6,2647435461,finishedSelection:true),new Expected("committed-all-skills-spin",7484,26,6,2647435461,finishedSelection:true),
        new Expected("canonical-boost-owned",10000,23,1,2451568673,2,true),new Expected("pending-canonical-universal-2",9640,24,2,2451568673,2,true),
        new Expected("canonical-all-skills-owned",8280,28,6,2451568673,2,true),new Expected("pending-canonical-spin",7602,29,6,2793601271,0,true,true),new Expected("committed-canonical-spin",7602,29,6,2793601271,0,true,true)};
    static void State(ModelState state,Expected e)
    {
        var c=ChapterEconomy.Project(state);var stages=ChapterCatalog.Rules.Stages(c.Progress);var w=WaveProgress.Of(state);
        Check(state.coins==e.coins&&state.sequence==e.seq&&state.seed==e.seed&&state.pending==null&&state.revision==e.seq*2,e.name+": debit/RNG/sequence/reset changed");
        Check(c.Skills.Receipts.Count==e.skills&&w.Delivery.PigeonProgress==e.carry&&state.lastSettledAt==2000,e.name+": ownership/carry/clock changed");
        Check(c.SelectedProjectId==(e.finishedSelection?null:"power-source.workshop-yard.upgrade-2")&&stages.EraId=="workshop-yard"&&c.EntrySequence==(e.canonical?17:13)&&c.TransitionSeen==c.EntrySequence,e.name+": era/selection marker changed");
        int[] levels={4,3,4,4,3,e.finishedSelection?5:4};for(int i=0;i<6;i++)Check(stages.AbsoluteLevel(FamilyIds.All[i])==levels[i],e.name+": stage reset/reuse");
        foreach(var receipt in c.Skills.Receipts)Check(receipt.FrozenPrice==OldPrice(receipt.Request.NodeId),e.name+": owned historical price repriced/refunded");
        if(e.canonical)
        {
            var r=c.Skills.Receipts.Single(x=>x.Request.NodeId==SkillIds.Universal1);
            Check(r.Legacy&&r.Request.Sequence==4&&r.Request.ExpectedWallet==460&&r.Request.Context==null&&r.LegacyHouseStage==1,e.name+": old canonical boost lost/invented history");
            Check(state.lastSettlement?.coins==10&&state.lastSettlement.sequence==7&&state.lastSettlement.from==1000&&state.lastSettlement.to==1600&&state.lastSettlementSeen==7,e.name+": offline original receipt changed");
        }
        else Check(state.lastSettlement==null&&state.lastSettlementSeen==0,e.name+": invented offline receipt");
    }
    static void Goldens()
    {
        using var provenance=JsonDocument.Parse(Raw("provenance"));using var facts=JsonDocument.Parse(Raw("codec-facts"));
        Check(provenance.RootElement.GetProperty("revision").GetString()=="042fb52"||provenance.RootElement.GetProperty("revision").GetString().StartsWith("042fb52"),"Wrong r8 provenance");
        foreach(var entry in provenance.RootElement.GetProperty("fixtureSha256").EnumerateObject())
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures",entry.Name))))==entry.Value.GetString(),"Historical byte hash changed");
        Check(Cases().Length==21,"Wrong golden count");
        foreach(var expected in Cases())
        {
            var raw=Raw(expected.name);using var original=JsonDocument.Parse(raw);bool pending=original.RootElement.GetProperty("pending").ValueKind!=JsonValueKind.Null;
            var storage=new Storage{raw=raw};var game=Open(storage);Check(!game.RecoveryRequired,expected.name+": "+game.Error);State(game.State,expected);
            Check(storage.writes==(pending?1:0),expected.name+": eager rewrite/recharge");if(!pending)Check(storage.raw==raw,"Committed r8 bytes rewritten");
            var expectedSkills=pending?original.RootElement.GetProperty("pending").GetProperty("chapter").GetProperty("after").GetProperty("skills").GetString():original.RootElement.GetProperty("chapter").GetProperty("skills").GetString();
            Check(SkillOwnershipCodec.Encode(game.Chapter.Skills)==expectedSkills,expected.name+": old v2 skill payload shape changed");
            Check(game.Chapter.Skills.Token==facts.RootElement.GetProperty(expected.name+(pending?"/after":"")).GetString(),expected.name+": old ownership token changed");
            string committed=Codec.Encode(game.State);int writes=storage.writes;game=Open(storage);State(game.State,expected);Check(storage.writes==writes&&Codec.Encode(game.State)==committed,expected.name+": repeated recovery paid again");
        }
        Pass("21 literal pinned-r8 snapshots, independent old-price debits, exact old v2 payload/tokens, canonical boost and chapter/carry/clock/selection preservation");
    }
    static void PendingFailures()
    {
        foreach(var e in Cases().Where(x=>x.name.StartsWith("pending-")))foreach(bool afterCommit in new[]{false,true})
        {
            var storage=new Storage{raw=Raw(e.name),failAt=1,failAfterCommit=afterCommit};var game=Open(storage);Check(game.RecoveryRequired,e.name+": uncertain recovery did not block");
            Reject(()=>game.ExecuteChapterSpin(game.State.sequence+1,2000),"Blocked recovery permitted mutation");storage.failAt=0;game=Open(storage);Check(!game.RecoveryRequired,game.Error);State(game.State,e);
            int writes=storage.writes;game=Open(storage);State(game.State,e);Check(storage.writes==writes,e.name+": ambiguous pending recovery replayed");
        }
        Pass("all9 old r8 pending skill/spin operations recover once across before/after durable-write failure");
    }
    static int Main()
    {
        try{Goldens();PendingFailures();Console.WriteLine("PASS "+groups+" r8 historical groups / "+assertions+" counted assertions");
#if JOURNEY_CHECKS
            JourneyCompatibilityChecks.Run();
#else
            Console.WriteLine("PENDING new pricing/Journey API: current quote versions, stale/refusal/replay, order rewards and guidance acknowledgements are not yet qualified.");
#endif
            return 0;}
        catch(Exception e){Console.Error.WriteLine("FAIL "+e);return 1;}
    }
}
