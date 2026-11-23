using System;
using System.Collections.Generic;
using System.IO;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Tests
{
    public static class MilestoneRegression
    {
        sealed class Storage:ISnapshotStorage
        {
            public string Primary,Backup;public int Writes;
            public string ReadPrimary(){return Primary;} public string ReadBackup(){return Backup;}
            public void Write(string raw){Backup=Primary;Primary=raw;Writes++;}
        }
        static readonly JsonStateCodec codec=new JsonStateCodec();
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static void Fails<T>(Action action) where T:Exception
        {try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
        static ChapterState Chapter(string era,int[] levels,bool pending=false,string selected=null)
        {
            var stages=new List<FamilyStage>();
            for(int i=0;i<6;i++)stages.Add(new FamilyStage(FamilyIds.All[i],levels[i]));
            var progress=ChapterCatalog.Rules.FromStages(new FamilyStages(era,stages));
            return new ChapterState(2,ChapterContentIds.RuleRevision,progress,SkillOwnership.Empty,selected,era==EraIds.WorkshopYard?12:0,era==EraIds.WorkshopYard&&!pending?12:0);
        }
        static void Facts(ChapterMilestoneSnapshot snapshot,int count,int reached,MilestoneNextAction action,params bool[] facts)
        {
            Check(snapshot.ConstructionCount==count&&snapshot.TotalConstructions==24,"Wrong construction count");
            Check(snapshot.Milestones.Count==4&&snapshot.ReachedCount==reached&&snapshot.NextAction==action,"Wrong current milestone/goal");
            for(int i=0;i<4;i++)Check(snapshot.Milestones[i].Reached==facts[i],"Wrong independent milestone flag at"+i);
        }
        static GameSession Open(Storage storage){return GameSession.Open(new GameStore(storage,codec),1);}
        static void ReadOnly(GameSession session,Storage storage,int expectedCount,int expectedReached)
        {
            string state=codec.Encode(session.State),primary=storage.Primary,backup=storage.Backup;int writes=storage.Writes;string error=session.Error;
            for(int i=0;i<16;i++)
            {
                var result=session.Milestones;
                Check(result.ConstructionCount==expectedCount&&result.ReachedCount==expectedReached,"Historical facts changed");
                Check(codec.Encode(session.State)==state&&storage.Primary==primary&&storage.Backup==backup&&storage.Writes==writes,"Passive read mutated state/encoded snapshots/writes");
                Check(session.Error==error&&!session.RecoveryRequired,"Passive read poisoned session");
            }
        }
        public static IReadOnlyList<string> Run(string fixtures)
        {
            var passed=new List<string>();Action<string,Action> run=(name,action)=>{action();passed.Add(name);};
            run("current-state ladder and free era entry preserve literal0..24 purchase count",()=>{
                Facts(ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{0,0,0,0,0,0})),0,0,MilestoneNextAction.InspectHouse,false,false,false,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{1,0,0,0,0,0})),1,1,MilestoneNextAction.ChooseConstruction,true,false,false,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{2,0,0,0,0,0})),2,1,MilestoneNextAction.ChooseConstruction,true,false,false,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{1,1,0,0,0,0})),2,2,MilestoneNextAction.ChooseConstruction,true,true,false,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{2,1,2,0,1,0})),6,2,MilestoneNextAction.ChooseConstruction,true,true,false,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{1,0,2,1,2,1})),7,1,MilestoneNextAction.ChooseConstruction,true,false,false,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{2,2,2,2,2,1})),11,2,MilestoneNextAction.ChooseConstruction,true,true,false,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{2,2,2,2,2,2})),12,3,MilestoneNextAction.EnterWorkshopYard,true,true,true,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.WorkshopYard,new[]{3,3,3,3,3,3},true)),12,3,MilestoneNextAction.ContinueEraEntry,true,true,true,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.WorkshopYard,new[]{3,3,3,3,3,3})),12,3,MilestoneNextAction.ChooseConstruction,true,true,true,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.WorkshopYard,new[]{3,4,5,3,4,5})),18,3,MilestoneNextAction.ChooseConstruction,true,true,true,false);
                Facts(ChapterMilestones.Project(Chapter(EraIds.WorkshopYard,new[]{5,5,5,5,5,4})),23,3,MilestoneNextAction.ChooseConstruction,true,true,true,false);
                var final=ChapterMilestones.Project(Chapter(EraIds.WorkshopYard,new[]{5,5,5,5,5,5}));
                Facts(final,24,4,MilestoneNextAction.FinishOrContinueOptionally,true,true,true,true);Check(final.OwnedSkillCount==0,"Skills became mandatory");
            });
            run("selected project and pending entry take precedence without auto-select or fabricated history",()=>{
                var selected=ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{0,0,0,0,0,0},selected:GameIds.HouseRepair));
                Facts(selected,0,0,MilestoneNextAction.InspectSelectedProject,false,false,false,false);Check(selected.SelectedProjectId==GameIds.HouseRepair,"Selected identity lost");
                var pending=ChapterMilestones.Project(Chapter(EraIds.WorkshopYard,new[]{3,3,3,3,3,3},true,"house.workshop-yard.upgrade-1"));
                Check(pending.NextAction==MilestoneNextAction.ContinueEraEntry,"A construction suggestion bypassed unacknowledged entry");
                Fails<ArgumentNullException>(()=>ChapterMilestones.Project(null));
                Fails<ArgumentException>(()=>ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{0,1,0,0,0,0})));
            });
            run("catalog and snapshots are immutable and independent between queries",()=>{
                var empty=ChapterMilestones.Project(Chapter(EraIds.Junkyard,new[]{0,0,0,0,0,0}));
                var final=ChapterMilestones.Project(Chapter(EraIds.WorkshopYard,new[]{5,5,5,5,5,5}));
                Check(empty.ReachedCount==0&&empty.ConstructionCount==0&&final.ReachedCount==4,"A later query changed an earlier snapshot");
                Fails<NotSupportedException>(()=>((IList<MilestoneDefinition>)ChapterMilestones.Catalog)[0]=ChapterMilestones.Catalog[1]);
                Fails<NotSupportedException>(()=>((IList<MilestoneProgress>)empty.Milestones)[0]=final.Milestones[0]);
                Check(ChapterMilestones.Catalog[0].Id==ChapterMilestones.HouseRepaired&&ChapterMilestones.Catalog[3].Id==ChapterMilestones.ChapterRestored,"Unstable catalog identities");
            });
            run("literal old and V2 selected snapshots derive present ownership without a write or grant",()=>{
                foreach(var item in new[]{new{file="legacy-complete.json",count=3,reached=2},new{file="v2-selected540.json",count=1,reached=1}})
                {
                    string raw=File.ReadAllText(Path.Combine(fixtures,item.file));var storage=new Storage{Primary=raw};var session=Open(storage);
                    Check(!session.RecoveryRequired&&storage.Primary==raw&&storage.Writes==0,"Opening an intact history rewrote it");
                    ReadOnly(session,storage,item.count,item.reached);
                    if(item.file=="v2-selected540.json")Check(session.State.coins==535&&ChapterEconomy.SelectedProjectPrice(session.Chapter)==540&&session.Milestones.NextAction==MilestoneNextAction.InspectSelectedProject,"Read repriced/spent or misclassified the old promise");
                }
                var old=ModelState.Fresh(1);old.coins=535;old.houseStage=1;old.selectedStage=2;old.autoHintSeen=1;
                var oldStorage=new Storage{Primary=codec.Encode(old)};var oldSession=Open(oldStorage);
                ReadOnly(oldSession,oldStorage,1,1);
                Check(oldSession.Milestones.NextAction==MilestoneNextAction.InspectSelectedProject&&ChapterEconomy.SelectedProjectPrice(oldSession.Chapter)==600&&oldSession.State.coins==535,"V1 target was treated as an affordable current480 offer");
            });
            run("one actual final construction updates milestones with no achievement reward or extra writes",()=>{
                var storage=new Storage{Primary=File.ReadAllText(Path.Combine(fixtures,"late23-skills6.json"))};var session=Open(storage);
                Check(session.Milestones.ConstructionCount==23&&session.Milestones.OwnedSkillCount==6&&session.Milestones.ReachedCount==3,"Six skills falsely completed construction");
                ReadOnly(session,storage,23,3);long coins=session.State.coins;uint seed=session.State.seed;int writes=storage.Writes;
                session.SelectChapterProject("power-source.workshop-yard.upgrade-2",session.State.sequence+1,1000);
                Check(session.State.coins==coins-700&&session.State.seed==seed&&storage.Writes==writes+2,"Construction debit/RNG/two-write boundary changed");
                ReadOnly(session,storage,24,4);Check(session.Milestones.NextAction==MilestoneNextAction.FinishOrContinueOptionally,"Completion forced further activity");
                var earlierStorage=new Storage{Primary=File.ReadAllText(Path.Combine(fixtures,"legacy-complete.json"))};var earlier=Open(earlierStorage);
                ReadOnly(earlier,earlierStorage,3,2);Check(session.Milestones.ReachedCount==4,"Loading an earlier snapshot leaked/revoked another session's facts");
            });
            run("recovery-required state refuses milestones without storage or error mutation",()=>{
                var storage=new Storage{Primary="{broken",Backup=File.ReadAllText(Path.Combine(fixtures,"v2-selected540.json"))};var session=Open(storage);
                Check(session.RecoveryRequired,"Broken primary was trusted");string primary=storage.Primary,backup=storage.Backup,error=session.Error;int writes=storage.Writes;
                Fails<InvalidOperationException>(()=>{var ignored=session.Milestones;});
                Check(storage.Primary==primary&&storage.Backup==backup&&storage.Writes==writes&&session.Error==error&&session.RecoveryRequired,"Milestone read altered blocked recovery");
            });
            return passed.AsReadOnly();
        }
    }
}
