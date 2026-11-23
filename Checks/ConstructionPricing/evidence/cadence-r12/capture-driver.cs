using System;
using System.IO;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Core;

internal static class RevisionBoundaryChecks
{
    sealed class Memory : ISnapshotStorage
    {
        public string raw; public int writes, failAt;
        public string ReadPrimary()=>raw;
        public string ReadBackup()=>null;
        public void Write(string next){raw=next;if(++writes==failAt)throw new IOException("Private committed reserve interruption");}
    }
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static void Need(bool ok,string why){if(!ok)throw new Exception(why);}
    static GameSession Open(Memory m)
    {var g=GameSession.Open(new GameStore(m,Codec),1);Need(!g.RecoveryRequired,g.Error);return g;}
    static Memory Legacy(long coins)
    {
        var s=ModelState.Fresh(1);s.coins=coins;s.houseStage=1;s.selectedStage=2;s.autoHintSeen=1;
        return new Memory{raw=Codec.Encode(s)};
    }
    static string Interrupted(Memory m,Action<GameSession> command)
    {
        var g=Open(m);m.failAt=1;
        try{command(g);throw new Exception("Missing intended reserve interruption");}catch(IOException){}
        Need(g.RecoveryRequired&&Codec.Decode(m.raw).pending!=null,"Did not capture durable pending");return m.raw;
    }
    public static void Capture(string root)
    {
        Need(ConstructionPricing.CurrentRevision==ConstructionPricing.RevisionV2,"Capture authentic V2 before changing current revision");
        Need(!Directory.Exists(root),"Preserve frozen literals: use a new directory");Directory.CreateDirectory(root);
        Action<string,string> save=(name,raw)=>File.WriteAllText(Path.Combine(root,name+".json"),raw+"\n");
        var m=Legacy(535);var g=Open(m);g.SelectChapterProject(GameIds.HouseImprove,1);save("v2-selected540",m.raw);
        save("v2-pending-select-from-v1",Interrupted(Legacy(535),x=>x.SelectChapterProject(GameIds.HouseImprove,1)));
        save("v2-pending-instant540",Interrupted(Legacy(550),x=>x.SelectChapterProject(GameIds.HouseImprove,1)));
        string pending=Interrupted(new Memory{raw=m.raw},x=>x.ExecuteChapterSpin(2));save("v2-pending-spin540",pending);
        var paid=new Memory{raw=pending};g=Open(paid);Need(g.State.coins==3&&g.State.lastSpin.chapter.Price==540,"Independent535+8-540 capture");save("v2-paid540",paid.raw);
        var old=Legacy(595);g=Open(old);g.ExecuteChapterSpin(1);Need(g.State.coins==3&&g.State.lastSpin.chapter.Price==600,"Independent595+8-600 capture");
        g.SelectChapterProject(GameIds.WorkshopBuild,2);save("v2-retained-v1-spin600",old.raw);
        Console.WriteLine("PASS froze6 actual V2 literals before Current changes; explicitly funded private legacy fixtures, no player data");
    }
#if R12_BOUNDARY
    public static void Verify(string root)
    {
        string[] rev={ConstructionPricing.LegacyRevision,ConstructionPricing.RevisionV2,ConstructionPricing.RevisionV3};
        for(int a=0;a<3;a++)for(int b=0;b<3;b++)Need(ConstructionPricing.CanAdvance(rev[a],rev[b])==(a<=b),"Historical9-pair revision matrix");
        foreach(string bad in new[]{null,"unknown","construction.prices.v4"})foreach(string known in rev)
            Need(!ConstructionPricing.CanAdvance(bad,known)&&!ConstructionPricing.CanAdvance(known,bad),"Unknown history revision accepted");
        foreach(string file in Directory.GetFiles(root,"*.json"))
        {
            string name=Path.GetFileNameWithoutExtension(file),raw=File.ReadAllText(file);var original=Codec.Decode(raw);var m=new Memory{raw=raw};var g=Open(m);
            long expected=name=="v2-pending-instant540"?10:name=="v2-pending-spin540"||name=="v2-paid540"||name=="v2-retained-v1-spin600"?3:535;
            Need(g.State.coins==expected,"Independent old540/600 wallet changed: "+name);
            Need(m.writes==(original.pending==null?0:1),"Unexpected eager/repeated writes: "+name);
            Need(g.Chapter.ConstructionPriceRevision==ConstructionPricing.RevisionV2,"Old V2 repriced: "+name);
            if(g.Chapter.SelectedProjectId==GameIds.HouseImprove)Need(ChapterEconomy.SelectedProjectPrice(g.Chapter)==540,"Old selected540 promise changed");
            if(name=="v2-pending-select-from-v1"||name=="v2-pending-instant540")g.SelectChapterProject(GameIds.HouseImprove,1);
            if(name=="v2-pending-spin540"||name=="v2-paid540")g.ExecuteChapterSpin(2);
            int count=m.writes;var again=Open(m);Need(count==m.writes&&again.State.coins==expected,"Old exact replay/reopen changed wallet");
            Console.WriteLine("PASS preserved "+name+" wallet="+expected+" revision="+g.Chapter.ConstructionPriceRevision);
        }
        var selected=new Memory{raw=File.ReadAllText(Path.Combine(root,"v2-selected540.json"))};var game=Open(selected);
        game.ExecuteChapterSpin(2);Need(game.State.coins==3&&game.State.lastSpin.chapter.Price==540,"V2 selected535+8 must still pay540");
        selected=new Memory{raw=File.ReadAllText(Path.Combine(root,"v2-selected540.json"))};game=Open(selected);game.SelectChapterProject(GameIds.HouseImprove,2);
        Need(game.State.coins==535-ChapterEconomy.ProjectPrice(GameIds.HouseImprove)&&game.Chapter.ConstructionPriceRevision==ConstructionPricing.RevisionV3,"Explicit new selection did not adopt current V3");
        Console.WriteLine("PASS monotonic history only; old selected540 retained until explicit current reselection; no refund/grant");
    }
#endif
}
