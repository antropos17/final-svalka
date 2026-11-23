using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JunkToNeon.Core;

internal static class Program
{
    sealed class Storage : ISnapshotStorage
    {
        public string primary,backup;public int writes,failAt;public bool failAfterCommit;
        public string ReadPrimary()=>primary;
        public string ReadBackup()=>backup;
        public void Write(string raw)
        {
            writes++;
            if(writes==failAt&&!failAfterCommit)throw new IOException("Private era fixture: before write");
            backup=primary;primary=raw;
            if(writes==failAt)throw new IOException("Private era fixture: after committed write");
        }
    }
    // Hand-calculated expected values. Never populate this table from production output.
    sealed class Expected
    {
        public readonly string name;public readonly long coins,seq;public readonly uint seed;
        public readonly int house,workshop,target,recycler,pigeon,carry,slot;public readonly long offline,seen;
        public Expected(string name,long coins,uint seed,long seq,int house,int workshop,int target=0,int recycler=0,int pigeon=0,int carry=0,int slot=0,long offline=0,long seen=0)
        {this.name=name;this.coins=coins;this.seed=seed;this.seq=seq;this.house=house;this.workshop=workshop;this.target=target;this.recycler=recycler;this.pigeon=pigeon;this.carry=carry;this.slot=slot;this.offline=offline;this.seen=seen;}
    }
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static int assertions,groups;
    static void Check(bool valid,string why) { assertions++;if(!valid)throw new Exception(why); }
    static void Fails(Action action,string why) { assertions++;try{action();}catch{return;}throw new Exception(why); }
    static void Group(string name) { groups++;Console.WriteLine("PASS "+name); }
    static void State(ModelState s,Expected e)
    {
        var w=WaveProgress.Of(s);
        Check(s.coins==e.coins&&s.seed==e.seed&&s.sequence==e.seq&&s.revision==e.seq*2&&s.pending==null,e.name+": wallet/RNG/sequence changed");
        Check(s.houseStage==e.house&&s.workshopBuilt==e.workshop&&s.selectedStage==e.target,e.name+": stages/selection changed");
        Check(w.RecyclerBuilt==e.recycler&&w.PigeonLoftBuilt==e.pigeon&&w.Delivery.PigeonProgress==e.carry&&w.Slot.Level==e.slot,e.name+": carry/ownership changed");
        Check((s.lastSettlement?.coins??0)==e.offline&&s.lastSettlementSeen==e.seen,e.name+": offline receipt/presentation changed");
    }
    static GameSession Open(Storage storage)=>GameSession.Open(new GameStore(storage,Codec),987654321);
    static string Raw(string era,string name)=>File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",era,name+".json")).TrimEnd('\r','\n');
    static string Shape(ModelState s)=>Codec.Encode(s);
    static string BeforePending(string historicalRaw)
    {
        var raw=System.Text.Json.Nodes.JsonNode.Parse(historicalRaw);
        raw["pending"]=null;raw["revision"]=raw["sequence"].GetValue<long>()*2;
        return raw.ToJsonString(); // Test-only pre-command projection; never rewrites the historical file.
    }
    static void HistoricalCorpus()
    {
        foreach(string era in new[]{"r6","r7"})
        {
            var folder=Path.Combine(AppContext.BaseDirectory,"Fixtures",era);
            using var provenance=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"provenance.json")));
            var hashes=provenance.RootElement.GetProperty("fixtureSha256");
            Check(hashes.EnumerateObject().Count()==(era=="r6"?8:25),"Unexpected historical fixture count");
            foreach(var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"Fixtures",era),"*.json").Where(f=>Path.GetFileName(f)!="provenance.json"))
            {
                var digest=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
                Check(digest==hashes.GetProperty(Path.GetFileName(file)).GetString(),"Historical fixture changed without regenerating against its pinned codec");
                var raw=File.ReadAllText(file).TrimEnd('\r','\n');var original=Codec.Decode(raw);
                var storage=new Storage{primary=raw};var session=Open(storage);
                Check(!session.RecoveryRequired,Path.GetFileName(file)+": "+session.Error);
                Check(storage.writes==(original.pending==null?0:1),"Unexpected fresh grant/eager rewrite: "+file);
                if(original.pending==null)Check(storage.primary==raw,"Read changed historical bytes");
                var committed=Shape(session.State);var seed=session.State.seed;var seq=session.State.sequence;var coins=session.State.coins;
                session=Open(storage);
                Check(Shape(session.State)==committed&&session.State.seed==seed&&session.State.sequence==seq&&session.State.coins==coins,"Reopen replay: "+file);
                Check(Shape(Codec.Decode(Codec.Encode(session.State)))==committed,"Round-trip changed preserved state");
            }
        }
        Group("Literal real-r6/r7 corpus loads without grant/reset, pending resolves once, reload and strict round-trip preserve committed state");
    }
    static void IndependentExpectations()
    {
        foreach(var era in new[]{"r6","r7"})foreach(var e in new[]{
            new Expected("fresh",37,1,0,0,0),new Expected("selected-workshop",119,1,1,1,0,3),
            new Expected("pending-acquire-workshop-spin",7,2647435461,2,1,1),
            new Expected("committed-acquire-workshop-spin",7,2647435461,2,1,1),
            new Expected("pending-finish-house-spin",4,2647435461,2,2,1),
            new Expected("committed-finish-house-spin",4,2647435461,2,2,1),
            new Expected("pending-select-workshop",0,1,1,1,1),new Expected("old-complete",42,1,42,2,1)})
            State(Open(new Storage{primary=Raw(era,e.name)}).State,e);
        foreach(var e in new[]{
            new Expected("pending-slot-purchase",340,12305,4,1,1,0,1,1,0,1),
            new Expected("owned-selected-house",340,12305,5,1,1,2,1,1,0,1),
            new Expected("pending-effects-spin",351,2451568673,6,1,1,2,1,1,2,1),
            new Expected("owned-carry-two",351,2451568673,6,1,1,2,1,1,2,1),
            new Expected("pending-offline",361,2451568673,7,1,1,2,1,1,2,1,10),
            new Expected("owned-offline-unseen",361,2451568673,7,1,1,2,1,1,2,1,10),
            new Expected("owned-offline-seen",361,2451568673,8,1,1,2,1,1,2,1,10,7),
            new Expected("pending-carry-spin",370,2793601271,9,1,1,2,1,1,0,1,10,7),
            new Expected("committed-carry-spin",370,2793601271,9,1,1,2,1,1,0,1,10,7),
            new Expected("selected-pigeon",239,1,1,1,1,5),new Expected("offline-funded-pigeon",249,1,2,1,1,5,offline:10),
            new Expected("pending-acquire-pigeon-spin",18,2647435461,3,1,1,0,0,1,0,0,10),
            new Expected("committed-acquire-pigeon-spin",18,2647435461,3,1,1,0,0,1,0,0,10),
            new Expected("pending-select-house-repair",37,1,1,0,0,1),new Expected("pending-select-house-improve",0,1,1,2,0),
            new Expected("pending-select-recycler",0,1,1,1,0,0,1),new Expected("pending-select-pigeon",0,1,1,1,0,0,0,1)})
            State(Open(new Storage{primary=Raw("r7",e.name)}).State,e);
        Group("Independent numeric expectations: 1000-120-180-240-120=340; [3,3,2] yields11/carry2; next [3,0,2] yields9/carry0; offline+10; acquisition excludes new effect");
    }
    static void InterruptedReplay()
    {
        foreach(var era in new[]{"r6","r7"})foreach(var name in new[]{"pending-acquire-workshop-spin","pending-finish-house-spin","pending-select-workshop"})
            PendingFailure(era,name);
        foreach(var name in new[]{"pending-slot-purchase","pending-effects-spin","pending-offline","pending-carry-spin","pending-acquire-pigeon-spin","pending-select-recycler","pending-select-pigeon"})
            PendingFailure("r7",name);
        Group("Historical pending spin/construction/slot/offline recovery interrupted before or after durable write blocks, then resolves once on reload");
    }
    static void PendingFailure(string era,string name)
    {
        string raw=Raw(era,name);var expected=Shape(Open(new Storage{primary=raw}).State);
        foreach(bool committed in new[]{false,true})
        {
            var storage=new Storage{primary=raw,failAt=1,failAfterCommit=committed};var game=Open(storage);
            Check(game.RecoveryRequired,name+": failed save did not block");
            Fails(()=>game.ExecuteSpin(game.State.sequence+1),name+": blocked mutation accepted");
            storage.failAt=0;game=Open(storage);
            Check(!game.RecoveryRequired&&Shape(game.State)==expected,name+": changed frozen result after interrupted recovery");
            int writes=storage.writes;game=Open(storage);
            Check(storage.writes==writes&&Shape(game.State)==expected,name+": repeated recovery credited again");
        }
    }
    static void StrictDescriptors()
    {
        string raw=Raw("r7","pending-effects-spin");
        using(var doc=JsonDocument.Parse(raw))Check(doc.RootElement.GetProperty("pending").GetProperty("wave").GetProperty("format").GetInt32()==1,"Fixture is not old format1");
        var document=System.Text.Json.Nodes.JsonNode.Parse(raw);
        document["pending"]["wave"].AsObject().Remove("format");Fails(()=>Codec.Decode(document.ToJsonString()),"Missing descriptor version accepted");
        document=System.Text.Json.Nodes.JsonNode.Parse(raw);document["pending"].AsObject().Remove("wave");Fails(()=>Codec.Decode(document.ToJsonString()),"Missing new descriptor accepted as legacy");
        document=System.Text.Json.Nodes.JsonNode.Parse(raw);document["wave"].AsObject().Remove("slot");Fails(()=>Codec.Decode(document.ToJsonString()),"Missing owned level accepted");
        var legacy=Open(new Storage{primary=Raw("r6","pending-acquire-workshop-spin")}).State;
        Check(legacy.lastSpin.reward==8&&legacy.lastSpin.bonus==0&&legacy.lastSpin.wave==null&&WaveProgress.Of(legacy).Delivery.PigeonProgress==0,"Old frozen result gained new effect");
        var owned=Open(new Storage{primary=Raw("r7","owned-offline-seen")}).State;
        Check(owned.lastSettledAt==1600&&owned.lastSettlement.from==1000&&owned.lastSettlement.to==1600&&owned.lastSettlement.seconds==600&&owned.lastSettlement.sequence==7,"Receipt interval/high-water changed");
        Check(WaveProgress.Of(owned).Slot.PurchaseSequence==4&&WaveProgress.Of(owned).Slot.Revision==1&&WaveProgress.Of(owned).Slot.PurchaseWalletBefore==460&&WaveProgress.Of(owned).Slot.PurchaseHouseStage==1,"Legacy boost receipt changed");
        Group("Strict present-descriptor completeness, legacy frozen-result semantics and exact boost/offline receipt checkpoints");
    }
    static int Main()
    {
        try
        {
            HistoricalCorpus();IndependentExpectations();InterruptedReplay();StrictDescriptors();
            ReplayFaultMatrix.Verify("real-r7-slot-purchase",BeforePending(Raw("r7","pending-slot-purchase")),
                (game,seq)=>game.BuySlotUpgrade(seq),
                state=>Check(state.coins==460&&state.sequence==3&&WaveProgress.Of(state).Slot.Level==0,"Before slot purchase"),
                state=>Check(state.coins==340&&state.sequence==4&&WaveProgress.Of(state).Slot.Level==1,"After slot purchase"));
            Group("Portable four-boundary replay driver exercised against real r7 slot command; independent wallet/ownership expectations");
#if CHAPTER_CONTRACT
            ProjectionContractChecks.Run();
#endif
#if CHAPTER_CONTENT
            ChapterContentMigrationChecks.Run();
#endif
#if CHAPTER_CORE
            ChapterCoreMigrationChecks.Run();
#endif
            Console.WriteLine("PASS "+groups+" historical groups / "+assertions+" independent assertions");
#if !CHAPTER_CORE
            Console.WriteLine("PENDING integration02: two-era/six-family projection, legacy boost->skill mapping, new-format missing descriptor, interrupted era unlock/stage/skill purchase; no future API was simulated.");
#endif
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine("FAIL "+error);return 1;}
    }
}
