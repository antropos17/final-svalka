using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Content;
using JunkToNeon.Guidance;
using JunkToNeon.Orders;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Tests
{
    public static class ClearGoalRegression
    {
        sealed class Storage:ISnapshotStorage
        {
            public string Primary,Backup;public int Reads,Writes,FailAt=-1;public bool LandThenThrow;
            public string ReadPrimary(){Reads++;return Primary;}
            public string ReadBackup(){Reads++;return Backup;}
            public void Write(string raw)
            {
                Writes++;if(Writes==FailAt&&!LandThenThrow)throw new IOException("Expected private clear write fault before storage");
                Backup=Primary;Primary=raw;if(Writes==FailAt)throw new IOException("Expected private clear write fault after storage");
            }
        }
        static readonly JsonStateCodec Codec=new JsonStateCodec();
        static void Need(bool value,string message){if(!value)throw new Exception(message);}
        static void Fails<T>(Action action) where T:Exception
        {try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
        static GameSession Open(Storage storage)=>GameSession.Open(new GameStore(storage,Codec),1);
        static ModelState Legacy600()
        {var s=ModelState.Fresh(1);s.coins=535;s.houseStage=1;s.autoHintSeen=1;s.selectedStage=2;s.lastSettledAt=1000;GameLogic.Validate(s);return s;}
        static string Input(string repository,string relative)=>File.ReadAllText(Path.Combine(repository,relative));
        static void NoIo(Storage storage,GameSession game,Action refusal)
        {
            int reads=storage.Reads,writes=storage.Writes;string raw=game.State==null?null:Codec.Encode(game.State),primary=storage.Primary,backup=storage.Backup,error=game.Error;bool blocked=game.RecoveryRequired;
            Fails<InvalidOperationException>(refusal);
            Need(storage.Reads==reads&&storage.Writes==writes&&storage.Primary==primary&&storage.Backup==backup,"Refusal performed storage IO");
            Need((game.State==null?null:Codec.Encode(game.State))==raw&&game.Error==error&&game.RecoveryRequired==blocked,"Refusal changed state/error/health");
        }
        static void Preserved(ModelState before,ModelState after)
        {
            var a=ChapterEconomy.Project(before);var b=ChapterEconomy.Project(after);var wa=WaveProgress.Of(before);var wb=WaveProgress.Of(after);
            Need(after.coins==before.coins&&after.seed==before.seed&&after.autoHintSeen==before.autoHintSeen,"Clear changed wallet/RNG/onboarding");
            Need(b.SelectedProjectId==null&&after.selectedStage==0,"Clear retained a target or created a successor");
            Need(a.ConstructionPriceRevision==b.ConstructionPriceRevision&&a.EntrySequence==b.EntrySequence&&a.TransitionSeen==b.TransitionSeen,"Clear changed saved aggregate terms/entry");
            Need(a.Progress.EraId==b.Progress.EraId&&a.Progress.CompletedUpgradeIds.SequenceEqual(b.Progress.CompletedUpgradeIds)&&SkillOwnershipCodec.Encode(a.Skills)==SkillOwnershipCodec.Encode(b.Skills),"Clear changed construction or skill ownership");
            Need(wa.RecyclerBuilt==wb.RecyclerBuilt&&wa.PigeonLoftBuilt==wb.PigeonLoftBuilt&&UpgradeOwnershipCodec.Encode(wa.Slot)==UpgradeOwnershipCodec.Encode(wb.Slot),"Clear changed old ownership projection");
            Need(wa.Delivery.PigeonProgress==wb.Delivery.PigeonProgress&&wa.Delivery.LastOperationSequence==wb.Delivery.LastOperationSequence&&wa.Delivery.Cadence==wb.Delivery.Cadence&&wa.Delivery.LastProposalKey==wb.Delivery.LastProposalKey,"Clear advanced delivery carry/history");
            var ja=JourneyEconomy.Project(before);var jb=JourneyEconomy.Project(after);
            Need(OrderStateCodec.Encode(ja.Orders)==OrderStateCodec.Encode(jb.Orders)&&GuideStateCodec.Encode(ja.Guidance)==GuideStateCodec.Encode(jb.Guidance),"Clear changed orders/guidance/last completion");
            Need(ja.AdoptedSequence==jb.AdoptedSequence&&ja.CommandSequence==jb.CommandSequence&&ja.CommandKind==jb.CommandKind&&ja.CommandId==jb.CommandId&&ja.CommandInstance==jb.CommandInstance&&ja.CommandCompletion==jb.CommandCompletion,"Clear invented retained command history");
            var receiptCheck=before.Copy();receiptCheck.lastSpin=after.lastSpin;receiptCheck.lastSettlement=after.lastSettlement;receiptCheck.lastSettlementSeen=after.lastSettlementSeen;
            Need(Codec.Encode(receiptCheck)==Codec.Encode(before),"Clear rewrote retained spin/settlement receipts");
        }
        static ModelState PendingTransition()
        {
            var s=ModelState.Fresh(1);s.coins=535;s.houseStage=2;s.workshopBuilt=1;s.autoHintSeen=1;s.sequence=13;s.revision=26;s.lastSettledAt=1000;
            var stages=new FamilyStages(EraIds.WorkshopYard,FamilyIds.All.Select(f=>new FamilyStage(f,3)));
            s.wave=new WaveProgress(1,1,WaveProgress.Empty.Delivery,UpgradeOwnership.Empty);
            s.chapter=new ChapterState(2,ChapterContentIds.RuleRevision,ChapterCatalog.Rules.FromStages(stages),SkillOwnership.Empty,"house.workshop-yard.upgrade-1",13,0,ConstructionPricing.CurrentRevision);
            GameLogic.Validate(s);return s;
        }
        // One independent group, two explicitly prepared/synthetic old-quote rows.
        // No retained spin is rewritten and the accepted source fixture stays byte-exact.
        public static IReadOnlyList<string> RunAfterClearSpin(string repository)
        {
            var v1=Legacy600();v1.coins=595;GameLogic.Validate(v1);
            string v2=Input(repository,"Checks/Milestones/fixtures/v2-selected540.json");
            var rows=new[]{(raw:Codec.Encode(v1),wallet:595L,price:600L,sequence:0L,pricing:ConstructionPricing.LegacyRevision),
                (raw:v2,wallet:535L,price:540L,sequence:1L,pricing:"construction.prices.v2")};
            foreach(var row in rows)
            {
                var storage=new Storage{Primary=row.raw,FailAt=2,LandThenThrow=false};var game=Open(storage);var initial=game.State;
                Need(initial.coins==row.wallet&&initial.sequence==row.sequence&&initial.seed==1&&initial.lastSpin==null,"Prepared old-quote row differs from literal wallet/sequence/seed/no-spin facts");
                Need(initial.houseStage==1&&game.Milestones.ConstructionCount==1&&game.Chapter.Skills.Receipts.Count==0&&ChapterEconomy.SelectedProjectPrice(game.Chapter)==row.price,"Prepared row differs from house1/one paid upgrade/no skills/old quote");
                var request=game.QuoteClearGoal();Fails<IOException>(()=>game.ClearGoal(request,1000));
                Need(game.RecoveryRequired&&storage.Writes==2,"Expected second-write failure after successful clear reservation");
                var pending=Codec.Decode(storage.Primary);var clear=pending.pending.chapter;
                Need(pending.sequence==row.sequence&&pending.pending.kind=="clear-goal"&&clear.ActionId=="house.improve"&&clear.Price==0&&clear.Before.SelectedProjectId=="house.improve"&&clear.After.SelectedProjectId==null,"Durable clear descriptor does not preserve exact old target/zero price/null after");
                NoIo(storage,game,()=>game.ExecuteChapterSpin(row.sequence+2,1000));storage.FailAt=-1;
                game=Open(storage);Preserved(initial,game.State);
                Need(storage.Writes==3&&game.State.sequence==row.sequence+1&&game.State.coins==row.wallet,"Recovery did not apply exactly one zero-cost clear");
                NoIo(storage,game,()=>game.ClearGoal(request,1000));
                string confirmed=Codec.Encode(game.State),primary=storage.Primary,backup=storage.Backup;int writes=storage.Writes,reads=storage.Reads;
                game=Open(storage);
                Need(storage.Reads==reads+2&&storage.Writes==writes&&storage.Primary==primary&&storage.Backup==backup&&Codec.Encode(game.State)==confirmed,"Healthy cleared reopen must only read the two snapshots, without writes/state changes");
                // Exactly one ordinary foreground spin: independent seed1 draw is [0,0,2], base8, no extras.
                game.ExecuteChapterSpin(row.sequence+2,1000);
                var after=game.State;var spin=after.lastSpin;var op=spin.chapter;
                Need(after.sequence==row.sequence+2&&storage.Writes==writes+2,"One normal spin did not make exactly two durable writes");
                Need(spin.symbols.SequenceEqual(new[]{0,0,2})&&spin.reward==8&&spin.bonus==0&&op.BaseCoins==8&&op.WorkshopCoins==0&&op.Effects.TotalCoins==0,"Literal first spin symbols/base/extras differ");
                Need(after.coins==(row.price==600?603:543)&&op.CoinsBefore==row.wallet&&op.CoinsAfter==after.coins&&after.seed==2647435461U&&WaveProgress.Of(after).Delivery.PigeonProgress==0,"Cleared-goal spin differs from literal603/543, seed2647435461, carry0");
                Need(op.Kind=="spin"&&op.ActionId==null&&op.Price==0&&op.Before.SelectedProjectId==null&&op.After.SelectedProjectId==null,"Spin resurrected the old goal/debit or assigned a successor");
                Need(after.houseStage==1&&after.selectedStage==0&&game.Chapter.SelectedProjectId==null&&game.Chapter.Progress.CompletedUpgradeIds.SequenceEqual(new[]{"house.repair"})&&game.Milestones.ConstructionCount==1&&game.Chapter.Skills.Receipts.Count==0,"Crossing old price bought a project/skill or created a target");
                Need(game.Chapter.ConstructionPriceRevision==row.pricing&&op.Before.ConstructionPriceRevision==row.pricing&&op.After.ConstructionPriceRevision==row.pricing,"Ordinary unselected spin adopted a new construction offer");
                confirmed=Codec.Encode(after);primary=storage.Primary;backup=storage.Backup;writes=storage.Writes;reads=storage.Reads;game=Open(storage);
                Need(storage.Reads==reads+2&&storage.Writes==writes&&storage.Primary==primary&&storage.Backup==backup&&Codec.Encode(game.State)==confirmed,"Final healthy reopen repaid/spun/wrote or changed confirmed bytes");
            }
            return Array.AsReadOnly(new[]{"recovered cleared600/540 goals stay absent when one seed1 foreground spin crosses old price:603/543, no purchase or successor"});
        }
        public static IReadOnlyList<string> Run(string repository)
        {
            string v2=Input(repository,"Checks/Milestones/fixtures/v2-selected540.json"),late=Input(repository,"Checks/Milestones/fixtures/late23-skills6.json"),r7=Input(repository,"Checks/EraMigration/Fixtures/r7/owned-carry-two.json");
            var results=new List<string>();Action<string,Action> run=(name,check)=>{check();results.Add(name);};
            run("literal600/540 clear535 through two writes; later explicit480 leaves55",()=>{
                foreach(string raw in new[]{Codec.Encode(Legacy600()),v2})
                {
                    var storage=new Storage{Primary=raw};var game=Open(storage);var before=game.State;var request=game.QuoteClearGoal();int writes=storage.Writes;
                    Need(request.SavedPrice==(before.chapter==null?600:540)&&request.ProjectId=="house.improve","Wrong historical clear confirmation");
                    int reads=storage.Reads;game.QuoteClearGoal();Need(reads==storage.Reads&&writes==storage.Writes,"Quote wrote/read storage");
                    var after=game.ClearGoal(request,1000);Preserved(before,after);Need(after.coins==535&&after.sequence==before.sequence+1&&storage.Writes==writes+2,"Clear did not commit literal zero debit/two writes");
                    NoIo(storage,game,()=>game.ClearGoal(request,1000));
                    game.SelectChapterProject("house.improve",game.State.sequence+1,1000);
                    Need(game.State.coins==55&&game.State.houseStage==2&&game.Chapter.SelectedProjectId==null&&game.Chapter.ConstructionPriceRevision==ConstructionPricing.CurrentRevision,"New explicit selection did not adopt480 once");
                    NoIo(storage,game,()=>game.ClearGoal(request,1000));
                }
            });
            run("retained r7 carry/spin/legacy skill and affordable700 target remain untouched",()=>{
                // Explicit valid synthetic affordable-goal variant; original late23 fixture has no goal.
                var prepared=Codec.Decode(late);var chapter=prepared.chapter;
                prepared.chapter=new ChapterState(2,chapter.RuleRevision,chapter.Progress,chapter.Skills,"power-source.workshop-yard.upgrade-2",chapter.EntrySequence,chapter.TransitionSeen,chapter.ConstructionPriceRevision);
                GameLogic.Validate(prepared);string affordable=Codec.Encode(prepared);
                foreach(string raw in new[]{r7,affordable})
                {
                    var storage=new Storage{Primary=raw};var game=Open(storage);var before=game.State;var request=game.QuoteClearGoal();
                    game.ClearGoal(request,1000);Preserved(before,game.State);
                    if(raw==affordable)Need(game.State.coins==10657&&game.Milestones.ConstructionCount==23&&game.Chapter.Skills.Receipts.Count==6,"Affordable unpaid target was purchased/refunded or optional ownership lost");
                    else Need(WaveProgress.Of(game.State).Delivery.PigeonProgress==2,"Retained historical carry changed");
                }
            });
            run("atomic request fields, null and bypass reject without IO; oldselect0/null still refuse",()=>{
                var storage=new Storage{Primary=Codec.Encode(Legacy600())};var store=new GameStore(storage,Codec);var game=GameSession.Open(store,1);var q=game.QuoteClearGoal();
                foreach(var bad in new[]{new ClearGoalRequest(q.ExpectedSequence+1,q.ProjectId,600,q.PricingRevision),new ClearGoalRequest(q.ExpectedSequence,"workshop.build",120,q.PricingRevision),new ClearGoalRequest(q.ExpectedSequence,q.ProjectId,599,q.PricingRevision),new ClearGoalRequest(q.ExpectedSequence,q.ProjectId,480,ConstructionPricing.CurrentRevision)})NoIo(storage,game,()=>game.ClearGoal(bad,1000));
                NoIo(storage,game,()=>game.ClearGoal(null,1000));NoIo(storage,game,()=>store.ExecuteChapter(ClearGoalRequest.Kind,q.ProjectId,q.Sequence,1000));
                NoIo(storage,game,()=>game.SelectProject(0,game.State.sequence+1,1000));NoIo(storage,game,()=>game.SelectChapterProject(null,game.State.sequence+1,1000));
                Fails<ArgumentException>(()=>new ClearGoalRequest(0,null,600,ConstructionPricing.LegacyRevision));Fails<ArgumentException>(()=>new ClearGoalRequest(0,"house.improve",0,ConstructionPricing.LegacyRevision));
            });
            run("no goal, pending transition and corrupt blocked/null state refuse before IO",()=>{
                foreach(string raw in new[]{Codec.Encode(ModelState.Fresh(1)),Codec.Encode(PendingTransition()),"{broken"})
                {
                    var storage=new Storage{Primary=raw};var game=Open(storage);var q=new ClearGoalRequest(game.State?.sequence??0,"house.improve",600,ConstructionPricing.LegacyRevision);
                    NoIo(storage,game,()=>game.QuoteClearGoal());NoIo(storage,game,()=>game.ClearGoal(q,1000));
                    if(game.State?.chapter?.TransitionPending==true)
                    {
                        var pending=game.State;Fails<InvalidOperationException>(()=>JourneyEconomy.ReserveChapter(pending,ClearGoalRequest.Kind,pending.chapter.SelectedProjectId,pending.sequence+1,1000));
                    }
                }
            });
            run("stale after newer selection, same goal reselected, and clock advancement never clear it",()=>{
                var storage=new Storage{Primary=v2};var game=Open(storage);var q=game.QuoteClearGoal();game.Checkpoint(1000);NoIo(storage,game,()=>game.ClearGoal(q,1000));
                q=game.QuoteClearGoal();game.SelectChapterProject("house.improve",game.State.sequence+1,1000);NoIo(storage,game,()=>game.ClearGoal(q,1000));
                foreach(string project in new[]{"house.improve","workshop.build"})
                {
                    var legacy=Legacy600();legacy.coins=100;storage=new Storage{Primary=Codec.Encode(legacy)};game=Open(storage);q=game.QuoteClearGoal();game.SelectChapterProject(project,game.State.sequence+1,1000);NoIo(storage,game,()=>game.ClearGoal(q,1000));
                    Need(game.Chapter.SelectedProjectId==project&&game.State.coins==100,"Old cancel removed newer/reselected unpaid goal");
                }
            });
            run("strict frozen clear roundtrip and malformed descriptor rejection",()=>{
                var before=Codec.Decode(v2);var pending=JourneyEconomy.ReserveChapter(before,ClearGoalRequest.Kind,"house.improve",before.sequence+1,1000);string raw=Codec.Encode(pending);
                var replay=GameLogic.ApplyPending(Codec.Decode(raw));Preserved(before,replay);Need(Codec.Encode(GameLogic.ApplyPending(replay))==Codec.Encode(replay),"Committed pending reapply changed state");
                Fails<InvalidOperationException>(()=>JourneyEconomy.ReserveChapter(pending,ClearGoalRequest.Kind,"house.improve",pending.sequence+1,1000));
                var op=pending.pending.chapter;
                ChapterOperation Bad(string action=null,long? price=null,long? coins=null,uint? seed=null,ChapterState after=null,int[] symbols=null,int baseCoins=0)=>new ChapterOperation(2,ClearGoalRequest.Kind,op.Sequence,op.At,action??op.ActionId,price??0,op.CoinsBefore,coins??op.CoinsAfter,op.SeedBefore,seed??op.SeedAfter,symbols,baseCoins,0,op.Before,after??op.After,null,null,null);
                var repriced=new ChapterState(2,op.After.RuleRevision,op.After.Progress,op.After.Skills,null,op.After.EntrySequence,op.After.TransitionSeen,ConstructionPricing.CurrentRevision);
                foreach(var malformed in new[]{Bad(action:"workshop.build"),Bad(price:1),Bad(coins:534),Bad(seed:2),Bad(after:op.Before),Bad(after:repriced),Bad(symbols:new[]{0,0,0}),Bad(baseCoins:1)})Fails<InvalidOperationException>(()=>ChapterEconomy.ValidateFrozen(malformed));
                Fails<InvalidOperationException>(()=>new ChapterOperation(2,"clear-unknown",op.Sequence,op.At,op.ActionId,0,535,535,1,1,null,0,0,op.Before,op.After,null,null,null));
                var transition=PendingTransition().chapter;var transitionAfter=new ChapterState(2,transition.RuleRevision,transition.Progress,transition.Skills,null,13,0,transition.ConstructionPriceRevision);
                var during=new ChapterOperation(2,ClearGoalRequest.Kind,14,1000,transition.SelectedProjectId,0,535,535,1,1,null,0,0,transition,transitionAfter,null,null,null);
                Fails<InvalidOperationException>(()=>ChapterEconomy.ValidateFrozen(during));
            });
            run("reservation failure does not auto retry; new explicit confirmation after reopen can clear",()=>{
                var storage=new Storage{Primary=v2,FailAt=1};var game=Open(storage);var before=game.State;var q=game.QuoteClearGoal();Fails<IOException>(()=>game.ClearGoal(q,1000));
                Need(game.RecoveryRequired&&storage.Primary==v2,"Reservation-before-write simulation changed durable bytes");NoIo(storage,game,()=>game.QuoteClearGoal());NoIo(storage,game,()=>game.ClearGoal(q,1000));
                storage.FailAt=-1;game=Open(storage);Need(Codec.Encode(game.State)==Codec.Encode(before),"Reopen automatically canceled an unreserved goal");
                var fresh=game.QuoteClearGoal();game.ClearGoal(fresh,1000);Preserved(before,game.State);Need(storage.Writes==3,"New explicit cancellation did not perform exactly two successful writes");
            });
            run("landed reservation/final commit uncertainty recover once; old request strictly refuses",()=>{
                foreach(var fault in new[]{(write:1,land:true),(write:2,land:false),(write:2,land:true)})
                {
                    var storage=new Storage{Primary=v2,FailAt=fault.write,LandThenThrow=fault.land};var game=Open(storage);var before=game.State;var q=game.QuoteClearGoal();Fails<IOException>(()=>game.ClearGoal(q,1000));
                    NoIo(storage,game,()=>game.QuoteClearGoal());NoIo(storage,game,()=>game.ClearGoal(q,1000));storage.FailAt=-1;
                    game=Open(storage);Preserved(before,game.State);Need(game.State.sequence==q.Sequence,"Recovered cancellation sequence changed");NoIo(storage,game,()=>game.ClearGoal(q,1000));
                    string committed=storage.Primary;int writes=storage.Writes;game=Open(storage);Need(storage.Primary==committed&&storage.Writes==writes,"Reopen committed cancellation wrote/replayed again");NoIo(storage,game,()=>game.QuoteClearGoal());
                }
            });
            return results.AsReadOnly();
        }
    }
#if UNITY_INCLUDE_TESTS
    public sealed class ClearGoalRegressionUnity
    {
        [NUnit.Framework.Test]public void JunkToNeonClearGoalPreservesConfirmedFactsAndRefusesStaleRequests()
        {NUnit.Framework.Assert.That(ClearGoalRegression.Run(Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../.."))).Count,NUnit.Framework.Is.EqualTo(8));}
        [NUnit.Framework.Test]public void JunkToNeonRecoveredClearStaysUnselectedWhenNextSpinCrossesOldPrice()
        {NUnit.Framework.Assert.That(ClearGoalRegression.RunAfterClearSpin(Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../.."))).Count,NUnit.Framework.Is.EqualTo(1));}
    }
#endif
}
