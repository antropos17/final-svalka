using System;
using System.IO;
using System.Linq;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;

internal static class SelectedRevisionChecks
{
    sealed class Memory : ISnapshotStorage
    {
        public string raw;public int writes;
        public string ReadPrimary()=>raw;
        public string ReadBackup()=>null;
        public void Write(string next){raw=next;writes++;}
    }
    static readonly JsonStateCodec Codec=new JsonStateCodec();static int count;
    static void Need(bool ok,string why){count++;if(!ok)throw new Exception(why);}
    static void Reject(Action action,string why)
    {
        try{action();}catch(InvalidOperationException){count++;return;}catch(FormatException){count++;return;}catch(ArgumentException){count++;return;}
        throw new Exception(why);
    }
    static GameSession Open(Memory m)
    {var g=GameSession.Open(new GameStore(m,Codec),1);Need(!g.RecoveryRequired,g.Error);return g;}
    static ChapterState Revision(ChapterState c,string revision)
    {return new ChapterState(2,c.RuleRevision,c.Progress,c.Skills,c.SelectedProjectId,c.EntrySequence,c.TransitionSeen,revision);}
    static ChapterOperation After(ChapterOperation p,ChapterState after)
    {return new ChapterOperation(2,p.Kind,p.Sequence,p.At,p.ActionId,p.Price,p.CoinsBefore,p.CoinsAfter,p.SeedBefore,p.SeedAfter,p.Symbols,p.BaseCoins,p.WorkshopCoins,p.Before,after,p.Effects,p.SkillRequest,p.Settlement);}
    static void Backup(string root,string name,string pending,long coins,long sequence,long price)
    {
        string folder=Path.Combine(root,name);Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"jtn.save.v1.json"),"{private broken primary");
        File.WriteAllText(Path.Combine(folder,"jtn.backup.v1.json"),pending);
        var g=GameSession.Open(new GameStore(new FileSnapshotStorage(folder),Codec),1);
        Need(g.RecoveryRequired&&g.BackupAvailable,"Historical backup not offered");g.RestoreBackup();
        Need(g.State.coins==coins&&g.State.sequence==sequence&&g.State.houseStage==2,"Backup changed old debit");
        long paid=g.State.lastSpin.chapter!=null?g.State.lastSpin.chapter.Price:g.State.lastSpin.wave.Price;
        Need(paid==price,"Backup reinterpreted540/600");
        string before=File.ReadAllText(Path.Combine(folder,"jtn.save.v1.json"));g.ExecuteChapterSpin(sequence);
        Need(File.ReadAllText(Path.Combine(folder,"jtn.save.v1.json"))==before,"Recovered backup replay wrote again");
        g=GameSession.Open(new GameStore(new FileSnapshotStorage(folder),Codec),1);
        Need(!g.RecoveryRequired&&g.State.coins==coins&&File.ReadAllText(Path.Combine(folder,"jtn.save.v1.json"))==before,"Backup reopen changed state");
    }
    public static void Verify(string fixtures,string repository,string privateRoot)
    {
        count=0;Need(!Directory.Exists(privateRoot),"Use a new exact private directory");Directory.CreateDirectory(privateRoot);
        Need(ConstructionPricing.CurrentRevision=="construction.prices.v3"&&ConstructionPricing.LegacyRevision=="construction.prices.v1"&&ConstructionPricing.RevisionV2=="construction.prices.v2","Price identities changed");
        string[] rev={ConstructionPricing.LegacyRevision,ConstructionPricing.RevisionV2,ConstructionPricing.RevisionV3};
        bool[,] expected={{true,true,true},{false,true,true},{false,false,true}};
        for(int a=0;a<3;a++)for(int b=0;b<3;b++)Need(ConstructionPricing.CanAdvance(rev[a],rev[b])==expected[a,b],"Literal9-pair history matrix");
        foreach(string bad in new[]{null,"unknown","construction.prices.v4"})
        {Need(!ConstructionPricing.CanAdvance(bad,bad),"Unknown equal revisions accepted");foreach(string known in rev)Need(!ConstructionPricing.CanAdvance(bad,known)&&!ConstructionPricing.CanAdvance(known,bad),"Unknown history revision accepted");}
        Console.WriteLine("PASS literal9-pair historical matrix and unknown/backward refusals");

        long[,] prices={{40,600,420,680},{120,260,380,620},{180,320,400,640},{240,360,440,660},{160,300,360,580},{200,340,460,700}};
        long total1=0,total2=0,total3=0;
        for(int era=0;era<2;era++)for(int family=0;family<6;family++)for(int local=1;local<=2;local++)
        {
            string id=ChapterIds.Project(era==0?EraIds.Junkyard:EraIds.WorkshopYard,FamilyIds.All[family],local);
            long v1=prices[family,era*2+local-1],v2=id==GameIds.HouseImprove?540:v1;
            long v3=id==GameIds.HouseImprove?480:id=="cart.build"?140:id=="power-source.build"?190:v2;
            Need(ConstructionPricing.Price(id,rev[0])==v1&&ConstructionPricing.Price(id,rev[1])==v2&&ConstructionPricing.Price(id,rev[2])==v3,"Independent historical/current price vector: "+id);
            Need(ChapterEconomy.ProjectPrice(id)==v3,"New quote did not use actual Current");total1+=v1;total2+=v2;total3+=v3;
        }
        Need(total1==9460&&total2==9400&&total3==9310,"Independent24-price totals");
        Need(SkillTree.Nodes.Sum(n=>n.Price)==610,"Current skill spend changed");
        Console.WriteLine("PASS all24 V1/V2/V3 independent prices9460/9400/9310; unchanged610 skills");

        Need(Directory.GetFiles(fixtures,"*.json").Length==6,"Expected six authentic prechange V2 snapshots");
        foreach(string file in Directory.GetFiles(fixtures,"*.json"))
        {
            string name=Path.GetFileNameWithoutExtension(file),raw=File.ReadAllText(file);var original=Codec.Decode(raw);var m=new Memory{raw=raw};var g=Open(m);
            long wallet=name=="v2-pending-instant540"?10:name=="v2-pending-spin540"||name=="v2-paid540"||name=="v2-retained-v1-spin600"?3:535;
            Need(g.State.coins==wallet&&g.Chapter.ConstructionPriceRevision==ConstructionPricing.RevisionV2,"Old540/600 term changed: "+name);
            Need(m.writes==(original.pending==null?0:1),"Unexpected eager/repeated write: "+name);
            if(original.pending==null)Need(m.raw==raw,"Nonpending V2 bytes eagerly rewritten");
            if(g.Chapter.SelectedProjectId==GameIds.HouseImprove)Need(ChapterEconomy.SelectedProjectPrice(g.Chapter)==540,"Old selected540 repriced");
            int writes=m.writes;
            if(name=="v2-pending-select-from-v1"||name=="v2-pending-instant540")g.SelectChapterProject(GameIds.HouseImprove,1);
            if(name=="v2-pending-spin540"||name=="v2-paid540")g.ExecuteChapterSpin(2);
            Need(m.writes==writes&&g.State.coins==wallet,"Old exact replay changed wallet/storage");g=Open(m);
            Need(m.writes==writes&&g.State.coins==wallet,"Old reopen changed wallet/storage");
            if(name=="v2-retained-v1-spin600")Need(g.State.lastSpin.chapter.Price==600&&g.State.lastSpin.chapter.After.ConstructionPriceRevision==ConstructionPricing.LegacyRevision,"Retained600 spin reinterpreted");
            Console.WriteLine("PASS preserved literal "+name+" wallet="+wallet);
        }
        var selected=new Memory{raw=File.ReadAllText(Path.Combine(fixtures,"v2-selected540.json"))};var game=Open(selected);game.ExecuteChapterSpin(2);
        Need(game.State.coins==3&&game.State.lastSpin.chapter.Price==540,"Old selected535+8 must still pay540");
        var frozen=game.State.lastSpin.chapter;
        Reject(()=>ChapterEconomy.ValidateFrozen(After(frozen,Revision(frozen.After,ConstructionPricing.RevisionV3))),"Nonselect V2->V3 revision change accepted");
        selected=new Memory{raw=File.ReadAllText(Path.Combine(fixtures,"v2-selected540.json"))};game=Open(selected);game.SelectChapterProject(GameIds.HouseImprove,2);
        Need(game.State.coins==55&&game.Chapter.ConstructionPriceRevision==ConstructionPricing.RevisionV3&&game.Chapter.SelectedProjectId==null,"Explicit535-480 selection did not adopt onlyCurrent");
        var old=ModelState.Fresh(1);old.coins=550;old.houseStage=1;old.selectedStage=2;old.autoHintSeen=1;
        var legacy=new Memory{raw=Codec.Encode(old)};var g1=Open(legacy);g1.ExecuteChapterSpin(1);
        Need(g1.State.coins==558&&g1.State.houseStage==1&&ChapterEconomy.SelectedProjectPrice(g1.Chapter)==600,"V1 selected550+8 repriced");
        g1.SelectChapterProject(GameIds.HouseImprove,2);Need(g1.State.coins==78&&g1.Chapter.ConstructionPriceRevision==ConstructionPricing.RevisionV3,"Explicit558-480 current selection changed");
        var fresh=new Memory();var gf=Open(fresh);Need(gf.State.coins==37&&fresh.writes==1,"Fresh37 changed");gf=Open(fresh);Need(fresh.writes==1,"Fresh grant repeated");
        gf.SelectChapterProject(GameIds.HouseRepair,1);gf.ExecuteChapterSpin(2);Need(gf.State.coins==5&&gf.State.houseStage==1&&gf.State.lastSpin.reward==8,"Fresh37+8-40 changed");
        var q=ChapterEconomy.QuoteSkill(Codec.Decode(File.ReadAllText(Path.Combine(fixtures,"v2-selected540.json"))),SkillIds.Universal1).Request;
        Need(q.PricingRevision==SkillPricing.CurrentRevision,"Fresh skill quote was downgraded");
        var stale=new SkillPurchaseRequest(q.NodeId,q.OperationId,q.Sequence,q.ExpectedWallet,q.ExpectedOwnership,q.Context,SkillPricing.RevisionV2);
        selected=new Memory{raw=File.ReadAllText(Path.Combine(fixtures,"v2-selected540.json"))};game=Open(selected);Reject(()=>game.BuySkill(stale),"Fresh historical skill quote accepted");Need(selected.writes==0&&!game.RecoveryRequired,"Stale quote wrote or poisoned session");
        Console.WriteLine("PASS old600/540 promise until explicit480 reselection; fresh37/repair40 and current-only skill quotes");

        int corpus=0;foreach(string relative in new[]{"Checks/EraMigration/Fixtures/r6","Checks/EraMigration/Fixtures/r7","Checks/EraMigration/R8/Fixtures"})foreach(string file in Directory.GetFiles(Path.Combine(repository,relative),"*.json"))
        {
            string name=Path.GetFileName(file);if(name=="provenance.json"||name=="codec-facts.json")continue;
            string raw=File.ReadAllText(file);var s=Codec.Decode(raw);var m=new Memory{raw=raw};var g=Open(m);
            Need(m.writes==(s.pending==null?0:1)&&g.Chapter.ConstructionPriceRevision==ConstructionPricing.LegacyRevision,"Old literal migration/debit changed");
            if(s.pending==null)Need(m.raw==raw,"Old literal eagerly rewritten");int writes=m.writes;g=Open(m);Need(m.writes==writes,"Old literal recovered twice");corpus++;
        }
        Need(corpus==54,"Literal corpus count changed");Console.WriteLine("PASS54 immutable V1 literal saves, pending recovery and no eager IO");
        Backup(privateRoot,"v2-pending540",File.ReadAllText(Path.Combine(fixtures,"v2-pending-spin540.json")),3,2,540);
        Backup(privateRoot,"v1-pending600",File.ReadAllText(Path.Combine(repository,"Checks/EraMigration/Fixtures/r7/pending-finish-house-spin.json")),4,2,600);
        Console.WriteLine("PASS two private actual-file V2/V1 backup restores540/600, exact replay and reopen");
        Console.WriteLine("PASS "+count+" selected-r12 assertions; no progression rerun or ordinary saves");
    }
}
