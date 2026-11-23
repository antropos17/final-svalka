using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JunkToNeon.Content;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;

namespace JunkToNeon.Tests
{
    public static class ConstructionPricingRegression
    {
        sealed class Storage : ISnapshotStorage
        {
            public string raw;
            public int writes, failAt;
            public bool committedFailure;
            public string ReadPrimary() { return raw; }
            public string ReadBackup() { return null; }
            public void Write(string next)
            {
                writes++;
                if(writes==failAt&&!committedFailure)throw new IOException("Private price write failed before commit");
                raw=next;
                if(writes==failAt)throw new IOException("Private price write failed after commit");
            }
        }
        static readonly JsonStateCodec Codec=new JsonStateCodec();
        static int assertions;
        static void Need(bool condition,string why){assertions++;if(!condition)throw new Exception(why);}
        static void Reject(Action action,string why)
        {
            try{action();}catch(FormatException){assertions++;return;}catch(InvalidOperationException){assertions++;return;}catch(ArgumentException){assertions++;return;}
            throw new Exception(why);
        }
        static GameSession Open(Storage storage,bool fresh=false)
        {
            var game=GameSession.Open(new GameStore(storage,Codec),1,initializeJourney:fresh);
            Need(!game.RecoveryRequired,"Private pricing fixture failed to open: "+game.Error);return game;
        }
        // Independent hand-authored legacy format2 states, not new-format output
        // with a field removed. The V1 rule, selection and empty ownership are literal.
        static string Legacy(long coins,bool selected=true,bool workshop=false)
        {
            return "{\"schemaVersion\":1,\"contentVersion\":1,\"revision\":0,\"sequence\":0,\"coins\":"+coins+
                ",\"houseStage\":1,\"selectedStage\":"+(selected?2:0)+",\"seed\":1,\"autoHintSeen\":1,\"workshopBuilt\":"+(workshop?1:0)+
                ",\"lastSettledAt\":0,\"lastSettlementSeen\":0,\"lastSettlement\":null,\"lastSpin\":null,\"pending\":null,"+
                "\"wave\":{\"format\":1,\"recycler\":0,\"pigeonLoft\":0,\"delivery\":{\"sequence\":0,\"carry\":0,\"cadence\":3,\"key\":null},\"slot\":{\"version\":1,\"level\":0,\"revision\":0,\"sequence\":0,\"walletBefore\":0,\"houseStage\":0}},"+
                "\"chapter\":{\"format\":2,\"rule\":\"chapter.rules.v1\",\"era\":\"junkyard\",\"stages\":[1,"+(workshop?1:0)+",0,0,0,0],\"skills\":\"{\\\"version\\\":2,\\\"receipts\\\":[]}\",\"selected\":"+(selected?"\"house.improve\"":"null")+",\"entry\":0,\"seen\":0}}";
        }
        static ChapterState WithPrice(ChapterState c,string price)
        {return new ChapterState(2,c.RuleRevision,c.Progress,c.Skills,c.SelectedProjectId,c.EntrySequence,c.TransitionSeen,price);}
        static ChapterOperation WithAfter(ChapterOperation op,ChapterState after)
        {return new ChapterOperation(2,op.Kind,op.Sequence,op.At,op.ActionId,op.Price,op.CoinsBefore,op.CoinsAfter,op.SeedBefore,op.SeedAfter,op.Symbols,op.BaseCoins,op.WorkshopCoins,op.Before,after,op.Effects,op.SkillRequest,op.Settlement);}
        // An independently specified pre-update immediate select:600-600=0.
        // This frozen shape is still legal; NEW selects must not generate it.
        static ModelState OldInstantSelect()
        {
            var state=Codec.Decode(Legacy(600,false));var before=state.chapter;
            var after=new ChapterState(2,before.RuleRevision,new ChapterProgress(EraIds.Junkyard,new[]{GameIds.HouseRepair,GameIds.HouseImprove}),before.Skills,null,0,0);
            var op=new ChapterOperation(2,"select",1,0,GameIds.HouseImprove,600,600,0,1,1,null,0,0,before,after,null,null,null);
            state.pending=new PendingOperation{kind="select",sequence=1,chapter=op};state.revision=1;
            GameLogic.Validate(state);return state;
        }
        static void FileBackup(string root,ModelState pending)
        {
            var folder=Path.Combine(root,"old600-backup");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"jtn.save.v1.json"),"{broken primary");
            File.WriteAllText(Path.Combine(folder,"jtn.backup.v1.json"),Codec.Encode(pending));
            var game=GameSession.Open(new GameStore(new FileSnapshotStorage(folder),Codec),99);
            Need(game.RecoveryRequired&&game.BackupAvailable,"Old600 backup was not offered fail-closed");
            game.RestoreBackup();Need(game.State.coins==3&&game.State.houseStage==2&&game.State.sequence==1&&game.State.lastSpin.chapter.Price==600,"Backup reinterpreted frozen600");
            string restored=File.ReadAllText(Path.Combine(folder,"jtn.save.v1.json"));
            game.ExecuteChapterSpin(1);Need(File.ReadAllText(Path.Combine(folder,"jtn.save.v1.json"))==restored,"Recovered old spin replay rewrote wallet");
            game=GameSession.Open(new GameStore(new FileSnapshotStorage(folder),Codec),99);
            Need(!game.RecoveryRequired&&game.State.coins==3&&File.ReadAllText(Path.Combine(folder,"jtn.save.v1.json"))==restored,"Recovered backup changed on reopen");
        }
        public static IReadOnlyList<string> Run(string root,string repository)
        {
            assertions=0;var passed=new List<string>();long oldTotal=0,v2Total=0,newTotal=0;int count=0;
            foreach(var era in ChapterCatalog.Rules.Definition.Eras)foreach(var project in era.Catalog.Upgrades)
            {
                long old=ConstructionPricing.Price(project.Id,ConstructionPricing.LegacyRevision),offer=ChapterEconomy.ProjectPrice(project.Id);
                long v2=ConstructionPricing.Price(project.Id,ConstructionPricing.RevisionV2);
                long expected=project.Id==GameIds.HouseImprove?480:project.Id=="cart.build"?140:project.Id=="power-source.build"?190:old;
                Need(old==project.Price.Value&&v2==(project.Id==GameIds.HouseImprove?540:old)&&offer==expected,"Independent V1/V2/V3 construction table changed");oldTotal+=old;v2Total+=v2;newTotal+=offer;count++;
            }
            Need(count==24&&oldTotal==9460&&v2Total==9400&&newTotal==9310&&ChapterEconomy.ProjectPrice(GameIds.HouseRepair)==40,"Independent24-project totals/repair changed");
            Reject(()=>ConstructionPricing.Price(GameIds.HouseImprove,"unknown"),"Unknown revision quoted");
            passed.Add("24 explicit prices: V1 total9460, V2 total9400, V3 total9310; only house/cart/power current offers change");

            var freshStore=new Storage();var fresh=Open(freshStore,true);Need(fresh.State.coins==37&&freshStore.writes==1,"Fresh37 was not granted once");
            fresh=Open(freshStore,true);Need(fresh.State.coins==37&&freshStore.writes==1,"Fresh reload granted again");
            fresh.SelectChapterProject(GameIds.HouseRepair,1);fresh.ExecuteChapterSpin(2);
            Need(fresh.State.coins==5&&fresh.State.houseStage==1&&fresh.State.seed==2647435461&&fresh.State.lastSpin.reward==8&&fresh.Chapter.ConstructionPriceRevision==ConstructionPricing.RevisionV3,"Fresh37+8-40 or RNG changed");
            passed.Add("Fresh grant once; new repair adopts priceV3, same seed1 outcome37+8-40=5");

            var storage=new Storage{raw=Legacy(550)};var oldGame=Open(storage);string literal=storage.raw;
            Need(oldGame.Chapter.ConstructionPriceRevision==ConstructionPricing.LegacyRevision&&ChapterEconomy.SelectedProjectPrice(oldGame.Chapter)==600&&ChapterEconomy.ProjectPrice(GameIds.HouseImprove)==480&&storage.writes==0,"Absent revision eagerly migrated or misquoted");
            oldGame.ExecuteChapterSpin(1);Need(oldGame.State.coins==558&&oldGame.State.houseStage==1&&oldGame.Chapter.SelectedProjectId==GameIds.HouseImprove&&ChapterEconomy.SelectedProjectPrice(oldGame.Chapter)==600,"Old550+8 adopted a new offer instead of its pinned600 target");
            int writes=storage.writes;oldGame=Open(storage);Need(storage.writes==writes&&oldGame.State.coins==558,"Legacy selected reopen changed state");
            oldGame.SelectChapterProject(GameIds.HouseImprove,2);Need(oldGame.State.coins==78&&oldGame.State.houseStage==2&&oldGame.Chapter.SelectedProjectId==null&&oldGame.Chapter.ConstructionPriceRevision==ConstructionPricing.RevisionV3,"Explicit reselection did not adopt480");
            var direct=Open(new Storage{raw=Legacy(550,false)});direct.SelectChapterProject(GameIds.HouseImprove,1);
            Need(direct.State.coins==70&&direct.State.houseStage==2&&direct.State.seed==1&&direct.Chapter.ConstructionPriceRevision==ConstructionPricing.RevisionV3,"New instant selection fromV1 did not charge480");
            passed.Add("Old selected550+8 remains unfunded at600; explicit reselection charges480 and retains78; direct550 retains70 without RNG/grant");

            var keep=Open(new Storage{raw=Legacy(590,true,true)});keep.BuySkill(ChapterEconomy.QuoteSkill(keep.State,SkillIds.Universal1).Request);
            keep.Checkpoint(1700000000);keep.SettleOffline(1700007200);
            Need(keep.State.coins==590&&keep.Chapter.ConstructionPriceRevision==ConstructionPricing.LegacyRevision&&ChapterEconomy.SelectedProjectPrice(keep.Chapter)==600&&keep.State.houseStage==1&&keep.State.seed==1,"Skills/clock/offline repriced or autobought old goal");
            passed.Add("V1 selected term survives skill120, clock and offline120; no auto purchase, RNG or carry change");

            foreach(bool committed in new[]{false,true})foreach(int failure in new[]{1,2})
            {
                var fault=new Storage{raw=Legacy(595),failAt=failure,committedFailure=committed};var game=Open(fault);
                try{game.ExecuteChapterSpin(1);throw new Exception("Expected private write failure");}catch(IOException){}
                Need(game.RecoveryRequired,"Uncertain old600 purchase did not block");fault.failAt=0;game=Open(fault);game.ExecuteChapterSpin(1);
                Need(game.State.coins==3&&game.State.houseStage==2&&game.State.sequence==1&&game.State.lastSpin.chapter.Price==600&&game.State.seed==2647435461,"Old frozen purchase recovery charged/rerolled twice");
                string committedRaw=fault.raw;int n=fault.writes;game=Open(fault);game.ExecuteChapterSpin(1);
                Need(fault.raw==committedRaw&&fault.writes==n,"Old applied receipt replay wrote again");
            }
            var pending=ChapterEconomy.Reserve(Codec.Decode(Legacy(595)),"spin",null,1);
            Need(pending.pending.chapter.Price==600&&pending.pending.chapter.After.ConstructionPriceRevision==ConstructionPricing.LegacyRevision,"Frozen legacy spin changed price history");
            FileBackup(root,pending);
            var retainedStore=new Storage{raw=Codec.Encode(GameLogic.ApplyPending(pending))};var retained=Open(retainedStore);
            retained.SelectChapterProject(GameIds.WorkshopBuild,2);int retainedWrites=retainedStore.writes;retained=Open(retainedStore);
            Need(retained.State.coins==3&&retained.Chapter.ConstructionPriceRevision==ConstructionPricing.RevisionV3&&retained.State.lastSpin.chapter.Price==600&&retained.State.lastSpin.chapter.After.ConstructionPriceRevision==ConstructionPricing.LegacyRevision&&retainedStore.writes==retainedWrites,"New offer reinterpreted retained old600 receipt");
            var instantStorage=new Storage{raw=Codec.Encode(OldInstantSelect())};var instant=Open(instantStorage);
            Need(instant.State.coins==0&&instant.State.houseStage==2&&instant.Chapter.ConstructionPriceRevision==ConstructionPricing.LegacyRevision&&instantStorage.writes==1,"Old instant600 pending reinterpreted as a current offer");
            instant.SelectChapterProject(GameIds.HouseImprove,1);Need(instantStorage.writes==1&&instant.State.coins==0,"Old instantselect replay debited twice");
            passed.Add("Old600 spin recovers once at four write-fault positions; applied replay, old instant600 and real-file backup recovery preserve original debit");

            var selectedNew=ChapterEconomy.Reserve(Codec.Decode(Legacy(475,false)),"select",GameIds.HouseImprove,1);
            Need(selectedNew.pending.chapter.Price==480&&selectedNew.pending.chapter.Before.ConstructionPriceRevision==ConstructionPricing.LegacyRevision&&selectedNew.pending.chapter.After.ConstructionPriceRevision==ConstructionPricing.RevisionV3,"Frozen newselect lost explicit price revision");
            var current=GameLogic.ApplyPending(selectedNew);var currentStore=new Storage{raw=Codec.Encode(current)};var currentGame=Open(currentStore);
            var reserveStore=new Storage{raw=Codec.Encode(selectedNew)};var recovered=Open(reserveStore);
            Need(reserveStore.writes==1&&recovered.State.coins==475&&ChapterEconomy.SelectedProjectPrice(recovered.Chapter)==480,"New unfunded selection recovery lost V3 term");
            recovered.SelectChapterProject(GameIds.HouseImprove,1);Need(reserveStore.writes==1,"Recovered V3 selection replay wrote");
            var instantNew=ChapterEconomy.Reserve(Codec.Decode(Legacy(550,false)),"select",GameIds.HouseImprove,1);
            var instantNewStore=new Storage{raw=Codec.Encode(instantNew)};recovered=Open(instantNewStore);
            Need(instantNew.pending.chapter.Before.ConstructionPriceRevision==ConstructionPricing.LegacyRevision&&instantNew.pending.chapter.After.ConstructionPriceRevision==ConstructionPricing.RevisionV3&&instantNewStore.writes==1&&recovered.State.coins==70,"New immediate480 pending recovery used old before-price");
            recovered.SelectChapterProject(GameIds.HouseImprove,1);Need(instantNewStore.writes==1,"Recovered immediate480 replay wrote");
            Need(currentStore.writes==0&&ChapterEconomy.SelectedProjectPrice(currentGame.Chapter)==480,"V3 selected term failed no-write reopen");
            currentGame.ExecuteChapterSpin(2);Need(currentGame.State.coins==3&&currentGame.State.houseStage==2&&currentGame.State.lastSpin.chapter.Price==480,"V3 selected475+8-480 changed");
            Reject(()=>ChapterEconomy.ValidateFrozen(WithAfter(currentGame.State.lastSpin.chapter,WithPrice(currentGame.State.lastSpin.chapter.After,ConstructionPricing.LegacyRevision))),"Nonselect price downgrade accepted");
            var tampered=currentGame.State;tampered.chapter=WithPrice(tampered.chapter,ConstructionPricing.LegacyRevision);
            Reject(()=>GameLogic.Validate(tampered),"Price history regressed after retained V3 receipt");
            Reject(()=>ChapterEconomy.ValidateFrozen(WithAfter(selectedNew.pending.chapter,WithPrice(selectedNew.pending.chapter.After,ConstructionPricing.LegacyRevision))),"480 descriptor accepted with V1 after-state");
            var currentBefore=Codec.Decode(Codec.Encode(current));var currentSelect=ChapterEconomy.Reserve(currentBefore,"select",GameIds.HouseImprove,2).pending.chapter;
            Reject(()=>ChapterEconomy.ValidateFrozen(WithAfter(currentSelect,WithPrice(currentSelect.After,ConstructionPricing.LegacyRevision))),"Known stale V3->V1 selection revision accepted");
            string encoded=Codec.Encode(current);Need(encoded.Contains("\"pricing\":\"construction.prices.v3\"")&&Codec.Encode(Codec.Decode(encoded))==encoded,"V3 codec not explicit/stable");
            foreach(string bad in new[]{"null","7","\"unknown\""})Reject(()=>Codec.Decode(encoded.Replace("\"construction.prices.v3\"",bad)),"Malformed/unknown present construction pricing accepted");
            Reject(()=>Codec.Decode(encoded.Replace("\"pricing\":\"construction.prices.v3\"","\"pricing\":\"construction.prices.v3\",\"pricing\":\"construction.prices.v3\"")),"Duplicate pricing accepted");
            Need(Codec.Encode(Codec.Decode(literal))==literal,"Literal V1 bytes changed through canonical codec");
            passed.Add("V3 pending/selected475+8-480=3 and strict revision round-trip; unknown/null/type/duplicate and stale price descriptors reject");

            string capturedV2ConstructionRaw=File.ReadAllText(Path.Combine(repository,"Checks/ConstructionPricing/evidence/cadence-r12/v2/v2-selected540.json"));
            var v2Store=new Storage{raw=capturedV2ConstructionRaw};var v2Session=Open(v2Store);
            Need(v2Store.writes==0&&v2Session.Chapter.ConstructionPriceRevision==ConstructionPricing.RevisionV2&&ChapterEconomy.SelectedProjectPrice(v2Session.Chapter)==540&&v2Session.State.coins==535,"Genuine pre-change V2 target was repriced/eagerly written");
            long v2Spin=v2Session.State.sequence+1;v2Session.ExecuteChapterSpin(v2Spin);
            Need(v2Session.State.coins==3&&v2Session.State.lastSpin.chapter.Price==540&&v2Session.State.houseStage==2,"Genuine V2 selected535+8-540 changed");
            int v2Writes=v2Store.writes;string v2Committed=v2Store.raw;v2Session=Open(v2Store);v2Session.ExecuteChapterSpin(v2Spin);
            Need(v2Store.writes==v2Writes&&v2Store.raw==v2Committed&&v2Session.State.coins==3,"Genuine V2 paid540 replay changed state/storage");
            passed.Add("Genuine pre-change V2 selected535/540 opens without writes, spends540 once and reopens/replays at3 without a new debit");

            int corpus=0;
            foreach(string relative in new[]{"Checks/EraMigration/Fixtures/r6","Checks/EraMigration/Fixtures/r7","Checks/EraMigration/R8/Fixtures"})
            foreach(string file in Directory.GetFiles(Path.Combine(repository,relative),"*.json"))
            {
                string name=Path.GetFileName(file);if(name=="provenance.json"||name=="codec-facts.json"||name.EndsWith("-proposal.json",StringComparison.Ordinal))continue;
                string raw=File.ReadAllText(file);var model=Codec.Decode(raw);string canonical=Codec.Encode(model);
                var journal=new Storage{raw=raw};var game=Open(journal);
                Need(journal.writes==(model.pending==null?0:1),"Literal old corpus caused eager migration/repeated recovery");
                if(model.pending==null)Need(Codec.Encode(game.State)==canonical,"Old committed literal changed on read");
                Need(game.Chapter.ConstructionPriceRevision==ConstructionPricing.LegacyRevision,"Old literal was repriced");corpus++;
            }
            passed.Add(corpus+" immutable existing r6/r7/r8 literal saves load/recover without repricing or eager IO");
            string v2Literal=File.ReadAllText(Path.Combine(repository,"Checks/EraMigration/R8/V2Pricing/delivery2-price5-owned.json")).TrimEnd('\r','\n');
            var v2Ownership=SkillOwnershipCodec.DecodeOrDefault(v2Literal);
            Need(SkillOwnershipCodec.Encode(v2Ownership)==v2Literal&&v2Ownership.Receipts.Any(r=>r.Request.NodeId==SkillIds.Delivery2&&r.Request.PricingRevision==SkillPricing.RevisionV2),"Literal V2 skill payload changed");
            string[] skillIds={SkillIds.Universal1,SkillIds.Universal2,SkillIds.Recycling1,SkillIds.Recycling2,SkillIds.Delivery1,SkillIds.Delivery2};
            long[][] skillPrices={new long[]{120,360,160,480,180,540},new long[]{120,160,160,90,60,5},new long[]{120,160,160,90,60,20}};
            string[] revisions={SkillPricing.RevisionV1,SkillPricing.RevisionV2,SkillPricing.RevisionV3};
            for(int revision=0;revision<3;revision++)for(int node=0;node<6;node++)Need(SkillPricing.Price(skillIds[node],revisions[revision])==skillPrices[revision][node],"Skill price revision changed");
            passed.Add("Literal skillV2 module payload and all18 V1/V2/V3 skill prices unchanged");
            passed.Add(assertions+" assertions; no production save or Editor access");return passed;
        }
    }
}
