using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Tests
{
    public static class OptionalAchievementRegression
    {
        sealed class Storage:ISnapshotStorage
        {
            public string Primary,Backup;public int Reads,Writes,FailAt=-1;
            public string ReadPrimary(){Reads++;return Primary;}
            public string ReadBackup(){Reads++;return Backup;}
            public void Write(string raw){Writes++;if(Writes==FailAt)throw new IOException("Private uncertain write");Backup=Primary;Primary=raw;}
        }
        static readonly JsonStateCodec Codec=new JsonStateCodec();
        static void Need(bool value,string message){if(!value)throw new Exception(message);}
        static void Fails<T>(Action action) where T:Exception
        {try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
        static GameSession Open(Storage storage){return GameSession.Open(new GameStore(storage,Codec),1);}
        static void Facts(OptionalAchievementSnapshot facts,bool skills,bool claim)
        {
            Need(facts.Achievements.Count==2,"Wrong optional fact cardinality");
            Need(facts.Achievements[0].Definition.Id=="optional.all-skills-owned"&&facts.Achievements[0].Reached==skills,"Wrong full-skill fact");
            Need(facts.Achievements[1].Definition.Id=="optional.retained-order-claim"&&facts.Achievements[1].Reached==claim,"Wrong retained-claim fact");
            Need(facts.ReachedCount==(skills?1:0)+(claim?1:0),"Wrong reached count");
        }
        static void ReadOnly(GameSession game,Storage storage,bool skills,bool claim)
        {
            string before=Codec.Encode(game.State),primary=storage.Primary,backup=storage.Backup,error=game.Error;int reads=storage.Reads,writes=storage.Writes;
            for(int i=0;i<3;i++)Facts(game.OptionalAchievements,skills,claim);
            Need(Codec.Encode(game.State)==before&&game.Error==error&&!game.RecoveryRequired,"Query changed healthy state/error");
            Need(storage.Reads==reads&&storage.Writes==writes&&storage.Primary==primary&&storage.Backup==backup,"Query performed storage IO or changed evidence");
        }
        static void Ready(GameSession game)
        {
            for(int i=0;i<64&&!game.Journey.Orders.Ready;i++)game.ExecuteChapterSpin(game.State.sequence+1,1000);
            Need(game.Journey.Orders.Ready,"Bounded deterministic order did not become ready");
        }
        public static IReadOnlyList<string> Run(string repository)
        {
            string latePath=Path.Combine(repository,"Checks/Milestones/fixtures/late23-skills6.json");
            string slotPath=Path.Combine(repository,"Checks/EraMigration/Fixtures/r7/pending-slot-purchase.json");
            byte[] lateBytes=File.ReadAllBytes(latePath),slotBytes=File.ReadAllBytes(slotPath);
            string late=File.ReadAllText(latePath);var parsed=Codec.Decode(late);
            var passed=new List<string>();Action<string,Action> run=(name,check)=>{check();passed.Add(name);};
            run("fresh and literal legacy slot ownership stay optional and read-only",()=>{
                var freshStorage=new Storage();var fresh=Open(freshStorage);ReadOnly(fresh,freshStorage,false,false);
                Need(fresh.Milestones.ConstructionCount==0&&fresh.Milestones.Milestones.Count==4,"Fresh chapter contract changed");
                var slot=GameLogic.ApplyPending(Codec.Decode(File.ReadAllText(slotPath)));
                var storage=new Storage{Primary=Codec.Encode(slot)};var game=Open(storage);
                Need(game.Chapter.Skills.Receipts.Count==1,"Legacy paid slot was not one skill");ReadOnly(game,storage,false,false);
            });
            run("zero five and six skills derive before final construction",()=>{
                var chapter=parsed.chapter;Need(chapter.Skills.Receipts.Count==6&&chapter.Progress.CompletedUpgradeIds.Count==23,"Retained six-skill fixture changed");
                foreach(int count in new[]{0,5,6})
                {
                    var skills=SkillOwnership.Empty;
                    foreach(var receipt in chapter.Skills.Receipts.Take(count))
                        skills=SkillTree.RestorePurchase(receipt.Request.Sequence-1,receipt.Request.ExpectedWallet,
                            receipt.Request.Context,skills,receipt.Request,receipt.FrozenPrice).OwnershipAfter;
                    var partial=new ChapterState(2,chapter.RuleRevision,chapter.Progress,skills,null,chapter.EntrySequence,chapter.TransitionSeen,chapter.ConstructionPriceRevision);
                    Facts(OptionalAchievements.Project(partial,parsed.journey),count==6,false);
                    Need(!ChapterCatalog.Rules.IsChapterComplete(partial.Progress),"Optional skills completed the chapter");
                }
                var storage=new Storage{Primary=late};var game=Open(storage);ReadOnly(game,storage,true,false);
                Need(game.Milestones.ConstructionCount==23&&game.Milestones.ReachedCount==3,"Optional facts changed mandatory milestones");
            });
            run("ready and canceled orders are not claims but actual rewards are retained",()=>{
                var storage=new Storage{Primary=late};var game=Open(storage);
                game.SelectChapterProject("power-source.workshop-yard.upgrade-2",game.State.sequence+1,1000);
                Need(game.State.coins==9957&&game.Milestones.ConstructionCount==24,"Final literal700 construction changed");
                game.AcceptOrder("order.sort-salvage",game.State.sequence+1,1000);Ready(game);ReadOnly(game,storage,true,false);
                long wallet=game.State.coins;game.CancelOrder("order.sort-salvage",game.Journey.Orders.InstanceSequence,game.State.sequence+1,1000);
                Need(game.State.coins==wallet,"Cancel granted coins");ReadOnly(game,storage,true,false);
                game.AcceptOrder("order.sort-salvage",game.State.sequence+1,1000);Ready(game);
                wallet=game.State.coins;long instance=game.Journey.Orders.InstanceSequence,claimSequence=game.State.sequence+1;uint seed=game.State.seed;
                game.ClaimOrder("order.sort-salvage",instance,claimSequence,1000);
                Need(game.State.coins==wallet+120&&game.State.seed==seed,"Claim did not pay exactly120 without drawing");ReadOnly(game,storage,true,true);
                string claimed=Codec.Encode(game.State);game.ClaimOrder("order.sort-salvage",instance,claimSequence,1000);
                Need(Codec.Encode(game.State)==claimed,"Exact retained claim replay changed state");
                game.AcceptOrder("order.neighborhood-supply",game.State.sequence+1,1000);ReadOnly(game,storage,true,true);
                game.CancelOrder("order.neighborhood-supply",game.Journey.Orders.InstanceSequence,game.State.sequence+1,1000);ReadOnly(game,storage,true,true);
                game.AcceptOrder("order.neighborhood-supply",game.State.sequence+1,1000);Ready(game);wallet=game.State.coins;
                game.ClaimOrder("order.neighborhood-supply",game.Journey.Orders.InstanceSequence,game.State.sequence+1,1000);
                Need(game.State.coins==wallet+180,"Later different claim did not pay exactly180");ReadOnly(game,storage,true,true);
                var oldStorage=new Storage{Primary=late};var old=Open(oldStorage);ReadOnly(old,oldStorage,true,false);ReadOnly(game,storage,true,true);
            });
            run("snapshots and catalog are immutable with no earned-forever cache",()=>{
                var fresh=ModelState.Fresh(1);
                var earlier=OptionalAchievements.Project(ChapterEconomy.Project(fresh),JourneyEconomy.Project(fresh));
                var later=OptionalAchievements.Project(parsed.chapter,parsed.journey);Facts(earlier,false,false);Facts(later,true,false);
                Fails<NotSupportedException>(()=>((IList<OptionalAchievementDefinition>)OptionalAchievements.Catalog)[0]=OptionalAchievements.Catalog[1]);
                Fails<NotSupportedException>(()=>((IList<OptionalAchievementProgress>)later.Achievements)[0]=earlier.Achievements[0]);
                Fails<ArgumentNullException>(()=>OptionalAchievements.Project(null,parsed.journey));
                Fails<ArgumentNullException>(()=>OptionalAchievements.Project(parsed.chapter,null));
            });
            run("blocked null and reserved sessions reject without storage IO or state mutation",()=>{
                foreach(int failAt in new[]{1,2})
                {
                    var storage=new Storage{Primary=late,FailAt=failAt};var game=Open(storage);
                    Fails<IOException>(()=>game.SelectChapterProject("power-source.workshop-yard.upgrade-2",game.State.sequence+1,1000));
                    Need(game.RecoveryRequired&&(failAt!=2||game.State.pending!=null),"Uncertain state did not block");
                    string before=Codec.Encode(game.State),error=game.Error,primary=storage.Primary,backup=storage.Backup;int reads=storage.Reads,writes=storage.Writes;
                    for(int i=0;i<3;i++)Fails<InvalidOperationException>(()=>{var ignored=game.OptionalAchievements;});
                    Need(Codec.Encode(game.State)==before&&game.Error==error&&game.RecoveryRequired,"Blocked getter changed state/error");
                    Need(storage.Reads==reads&&storage.Writes==writes&&storage.Primary==primary&&storage.Backup==backup,"Blocked getter performed IO");
                }
                var corruptStorage=new Storage{Primary="{broken"};var corrupt=Open(corruptStorage);Need(corrupt.State==null,"Corrupt setup not null");
                int originalReads=corruptStorage.Reads,originalWrites=corruptStorage.Writes;string originalError=corrupt.Error;
                Fails<InvalidOperationException>(()=>{var ignored=corrupt.OptionalAchievements;});
                Need(corrupt.State==null&&corrupt.Error==originalError&&corruptStorage.Reads==originalReads&&corruptStorage.Writes==originalWrites,"Null-state getter performed IO or changed error");
            });
            Need(File.ReadAllBytes(latePath).SequenceEqual(lateBytes)&&File.ReadAllBytes(slotPath).SequenceEqual(slotBytes),"Accepted input fixture was changed");
            return passed.AsReadOnly();
        }
    }
}
