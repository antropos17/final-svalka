#if CHAPTER_CONTENT
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using JunkToNeon.Core;
using JunkToNeon.Content;
using JunkToNeon.Contracts;

// Exercises04's actual published projection API; it does not replace02's save migrator.
internal static class ChapterContentMigrationChecks
{
    sealed class Storage : ISnapshotStorage
    {
        public string raw;public int writes;
        public string ReadPrimary()=>raw;
        public string ReadBackup()=>null;
        public void Write(string value){writes++;raw=value;}
    }
    static void Require(bool value,string why){if(!value)throw new Exception(why);}
    static void Fails(Action act,string why){try{act();}catch{return;}throw new Exception(why);}
    public static void Run()
    {
        var rules=new ChapterRules(ChapterData.TwoEras(),new[]{"Editable/HouseStage0","Editable/HouseStage1","Editable/HouseStage2"});
        var codec=new JsonStateCodec();
        using var expected=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"ProjectionExpectations.json")));
        int cases=0;
        foreach(var item in expected.RootElement.GetProperty("cases").EnumerateArray())
        {
            string fixture=item.GetProperty("fixture").GetString();
            var storage=new Storage{raw=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",fixture+".json"))};
            var game=GameSession.Open(new GameStore(storage,codec),222);
            Require(!game.RecoveryRequired,fixture+": old replay failed");
            var state=game.State;string before=codec.Encode(state);int writes=storage.writes;
            var wave=WaveProgress.Of(state);
            var projected=rules.ProjectR7(state.houseStage,state.workshopBuilt,wave.RecyclerBuilt,wave.PigeonLoftBuilt,state.selectedStage,state.contentVersion);
            ProjectionContractChecks.AssertProjection(fixture,rules.Stages(projected.Progress));
            string selected=item.GetProperty("selectedProject").ValueKind==JsonValueKind.Null?null:item.GetProperty("selectedProject").GetString();
            Require(projected.SelectedUpgradeId==selected,fixture+": old target reused/changed");
            Require(state.coins==item.GetProperty("coins").GetInt64()&&state.seed==item.GetProperty("seed").GetUInt32()&&state.sequence==item.GetProperty("sequence").GetInt64(),fixture+": independent wallet/RNG/sequence differs");
            Require(wave.Slot.Level==item.GetProperty("universal1").GetInt32(),fixture+": boost checkpoint changed");
            Require(storage.writes==writes&&codec.Encode(game.State)==before,fixture+": pure projection wrote/reset source state");
            Require(!rules.CanUnlockWorkshopYard(projected.Progress)&&!rules.IsChapterComplete(projected.Progress),fixture+": old completion skipped new families/era");
            var roundTrip=rules.Stages(rules.FromStages(rules.Stages(projected.Progress)));
            ProjectionContractChecks.AssertProjection(fixture,roundTrip);cases++;
        }
        Fails(()=>rules.ProjectR7(0,1,0,0),"Invalid legacy workshop silently reset");
        Fails(()=>rules.ProjectR7(1,0,0,0,6),"Unknown legacy integer reused");
        Fails(()=>rules.ProjectR7(1,0,0,0,0,2),"Unknown legacy content version reset");
        var first=rules.FromStages(new FamilyStages(EraIds.Junkyard,FamilyIds.All.Select(id=>new FamilyStage(id,2))));
        Require(rules.CanUnlockWorkshopYard(first)&&!rules.IsChapterComplete(first),"First era incorrectly final");
        var proposal=rules.ProposeUnlockWorkshopYard(first,543);
        Require(proposal.CoinsBefore==543&&proposal.CoinsAfter==543&&proposal.Price==0&&proposal.OperationId==ChapterIds.UnlockWorkshopYard,"Era proposal charged/granted or changed identity");
        var second=rules.Stages(proposal.After);
        Require(FamilyIds.All.All(id=>second.AbsoluteLevel(id)==3&&second.LocalLevel(id)==0),"Entry reset/reused old stage");
        Require(proposal.After.CompletedUpgradeIds.Count==12&&first.CompletedUpgradeIds.Count==12,"Entry lost first-era history or mutated input");
        Fails(()=>rules.ProposeUnlockWorkshopYard(proposal.After,543),"Era proposal replayed as new unlock");
        var end=rules.FromStages(new FamilyStages(EraIds.WorkshopYard,FamilyIds.All.Select(id=>new FamilyStage(id,5))));
        Require(rules.IsChapterComplete(end),"Chapter end requires absent skill state");
        Console.WriteLine("PASS actual Content.ProjectR7 "+cases+" literal-fixture projections, exact qualified selection, no pure-query writes, era2 proposal and skill-independent chapter boundary; persistence qualification is separate");
    }
}
#endif
