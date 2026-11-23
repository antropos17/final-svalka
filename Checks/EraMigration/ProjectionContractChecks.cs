#if CHAPTER_CONTRACT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JunkToNeon.Contracts;

// Tests only the published DTO/identity contract and independent expected data.
// Passing this file does NOT mean the production save migrator exists or works.
internal static class ProjectionContractChecks
{
    static void Require(bool value,string message) { if(!value)throw new Exception(message); }
    internal static void AssertProjection(string fixture,FamilyStages actual)
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"ProjectionExpectations.json")));
        var expected=data.RootElement.GetProperty("cases").EnumerateArray().Single(c=>c.GetProperty("fixture").GetString()==fixture);
        var ids=data.RootElement.GetProperty("familyOrder").EnumerateArray().Select(x=>x.GetString()).ToArray();
        Require(actual.EraId==expected.GetProperty("era").GetString(),fixture+": era changed");
        int index=0;foreach(var level in expected.GetProperty("absoluteLevels").EnumerateArray())
            Require(actual.AbsoluteLevel(ids[index++])==level.GetInt32(),fixture+": building identity/stage changed");
    }
    public static void Run()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"ProjectionExpectations.json")));
        var ids=data.RootElement.GetProperty("familyOrder").EnumerateArray().Select(x=>x.GetString()).ToArray();
        Require(ids.SequenceEqual(FamilyIds.All),"Expected family order does not match published contract");
        foreach(var item in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            var era=item.GetProperty("era").GetString();int index=0;
            var entries=item.GetProperty("absoluteLevels").EnumerateArray().Select(x=>new FamilyStage(ids[index++],x.GetInt32())).ToArray();
            var stages=new FamilyStages(era,entries);
            var selected=item.GetProperty("selectedProject");
            Require(selected.ValueKind==JsonValueKind.Null||ChapterIds.IsProject(selected.GetString(),era),"Unknown expected old target mapping");
            Require(stages.AbsoluteLevel(FamilyIds.Cart)==0&&stages.AbsoluteLevel(FamilyIds.PowerSource)==0,"Legacy projection invented new ownership");
        }
        string[] oldIds={"house.repair","house.improve","workshop.build","recycler.build","pigeon-loft.build"};
        string[] mapped={ChapterIds.Project(EraIds.Junkyard,FamilyIds.House,1),ChapterIds.Project(EraIds.Junkyard,FamilyIds.House,2),
            ChapterIds.Project(EraIds.Junkyard,FamilyIds.Workshop,1),ChapterIds.Project(EraIds.Junkyard,FamilyIds.Recycler,1),ChapterIds.Project(EraIds.Junkyard,FamilyIds.PigeonLoft,1)};
        Require(mapped.SequenceEqual(oldIds),"Legacy project identity reused/renamed");
        Require(SkillIds.Universal1=="slot.coin-boost","Legacy boost node identity renamed");
        Require(!ChapterIds.IsProject(ChapterIds.UnlockWorkshopYard,EraIds.Junkyard),"Era unlock accidentally selectable as construction");
        var first=new FamilyStages(EraIds.Junkyard,ids.Select(id=>new FamilyStage(id,2)));
        var second=new FamilyStages(EraIds.WorkshopYard,ids.Select(id=>new FamilyStage(id,3)));
        var end=new FamilyStages(EraIds.WorkshopYard,ids.Select(id=>new FamilyStage(id,5)));
        Require(ids.All(id=>first.LocalLevel(id)==2&&second.LocalLevel(id)==0&&end.LocalLevel(id)==2),"Era absolute/local boundary differs");
        Console.WriteLine("PASS published ChapterContracts identities/DTO bounds and independent projection dataset; this group checks shared contracts only");
    }
}
#endif
