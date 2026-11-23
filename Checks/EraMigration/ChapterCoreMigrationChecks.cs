#if CHAPTER_CORE
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using JunkToNeon.Core;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;

internal static class ChapterCoreMigrationChecks
{
    sealed class Storage : ISnapshotStorage
    {
        public string raw,backup;public int writes;
        public string ReadPrimary()=>raw;
        public string ReadBackup()=>backup;
        public void Write(string next){writes++;backup=raw;raw=next;}
    }
    static readonly JsonStateCodec Codec=new JsonStateCodec();
    static int assertions,groups;
    static void Check(bool ok,string why){assertions++;if(!ok)throw new Exception(why);}
    static void Reject(Action action,string why){assertions++;try{action();}catch{return;}throw new Exception(why);}
    static void Pass(string name){groups++;Console.WriteLine("PASS modern compatibility: "+name);}
    static string Raw(string name)=>File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",name+".json")).TrimEnd('\r','\n');
    static GameSession Open(Storage storage){var game=GameSession.Open(new GameStore(storage,Codec),998877);Check(!game.RecoveryRequired,"Open failed: "+game.Error);return game;}
    static string Fund(string raw,long coins){var json=JsonNode.Parse(raw);json["coins"]=coins;return json.ToJsonString();}
    static void Receipt(ModelState s)
    {
        var r=s.lastSettlement;var slot=WaveProgress.Of(s).Slot;
        Check(r!=null&&r.sequence==7&&r.from==1000&&r.to==1600&&r.seconds==600&&r.coins==10&&s.lastSettlementSeen==7&&s.lastSettledAt==1600,"Offline interval/seen receipt changed");
        Check(slot.Level==1&&slot.PurchaseSequence==4&&slot.PurchaseWalletBefore==460&&slot.PurchaseHouseStage==1,"Legacy slot checkpoint changed");
        var skill=ChapterEconomy.Project(s).Skills.Receipts.Single(x=>x.Request.NodeId==SkillIds.Universal1);
        Check(skill.Legacy&&skill.Request.Sequence==4&&skill.Request.ExpectedWallet==460&&skill.Request.Context==null&&skill.LegacyHouseStage==1&&skill.FrozenPrice==120,"Migrated skill invented context or changed old price/identity");
    }
    static void GoldenQueries()
    {
        foreach(string era in new[]{"r6","r7"})foreach(string path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"Fixtures",era),"*.json").Where(p=>Path.GetFileName(p)!="provenance.json"))
        {
            var storage=new Storage{raw=File.ReadAllText(path).TrimEnd('\r','\n')};var game=Open(storage);var s=game.State;
            string saved=storage.raw;int writes=storage.writes;var chapter=game.Chapter;var stages=ChapterCatalog.Rules.Stages(chapter.Progress);var w=WaveProgress.Of(s);
            Check(stages.EraId==EraIds.Junkyard&&stages.AbsoluteLevel(FamilyIds.House)==s.houseStage&&stages.AbsoluteLevel(FamilyIds.Workshop)==s.workshopBuilt&&stages.AbsoluteLevel(FamilyIds.Recycler)==w.RecyclerBuilt&&stages.AbsoluteLevel(FamilyIds.PigeonLoft)==w.PigeonLoftBuilt&&stages.AbsoluteLevel(FamilyIds.Cart)==0&&stages.AbsoluteLevel(FamilyIds.PowerSource)==0,"Golden stage projection reset/invented family: "+path);
            string[] oldTargets={null,"house.repair","house.improve","workshop.build","recycler.build","pigeon-loft.build"};
            Check(chapter.SelectedProjectId==oldTargets[s.selectedStage]&&chapter.Skills.Owns(SkillIds.Universal1)==(w.Slot.Level==1),"Golden target/boost mapping differs: "+path);
            Check(game.State.chapter==null&&storage.raw==saved&&storage.writes==writes,"Query eagerly migrated/rewrote old bytes: "+path);
            Check(chapter.Skills.Receipts.Count==w.Slot.Level&&chapter.EntrySequence==0&&!chapter.TransitionPending,"Query invented history/entry");
        }
        using var expected=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"ProjectionExpectations.json")));
        foreach(var item in expected.RootElement.GetProperty("cases").EnumerateArray())
        {string fixture=item.GetProperty("fixture").GetString();var game=Open(new Storage{raw=Raw(fixture)});ProjectionContractChecks.AssertProjection(fixture,ChapterCatalog.Rules.Stages(game.Chapter.Progress));}
        var store=new Storage{raw=Raw("r7/owned-offline-seen")};var owned=Open(store);Receipt(owned.State);
        owned.SelectChapterProject("house.improve",9,1600); // Persist migration; still insufficient, no purchase.
        Check(owned.State.coins==361&&owned.State.seed==2451568673&&owned.State.sequence==9&&owned.Chapter.SelectedProjectId=="house.improve"&&WaveProgress.Of(owned.State).Delivery.PigeonProgress==2,"First modern persistence changed wallet/RNG/carry/target");Receipt(owned.State);
        owned=Open(store);owned.ExecuteChapterSpin(10,1600);
        Check(owned.State.coins==370&&owned.State.seed==2793601271&&WaveProgress.Of(owned.State).Delivery.PigeonProgress==0,"Migrated universal tier double-counted or carry/spin replay changed");
        Check(owned.State.lastSpin.chapter.Effects.UniversalCoins==1&&owned.State.lastSpin.wave==null,"Modern spin did not replace separate legacy slot credit once");Receipt(owned.State);
        var oldEndStore=new Storage{raw=Fund(Raw("r7/owned-offline-seen"),600)};var oldEnd=Open(oldEndStore);
        oldEnd.SelectProject(2,9,1600); // Literal r7 command still uses its original finite completion rule.
        Check(oldEnd.State.chapter==null&&oldEnd.State.coins==0&&HouseGoal.Complete(oldEnd.State),"Old r7 completion changed before migration");
        oldEnd=Open(oldEndStore);Check(HouseGoal.Complete(oldEnd.State),"Decode lost literal old r7 completion");
        oldEnd.SelectChapterProject("cart.build",10,1600);
        Check(oldEnd.State.chapter!=null&&!HouseGoal.Complete(oldEnd.State)&&!ChapterCatalog.Rules.CanUnlockWorkshopYard(oldEnd.Chapter.Progress)&&oldEnd.State.coins==0&&oldEnd.State.seed==2451568673&&WaveProgress.Of(oldEnd.State).Delivery.PigeonProgress==2,"Modern four-family state prematurely ended/unlocked/reset or granted coins");Receipt(oldEnd.State);
        Pass("all33 actual Core projections preserve old identities without eager writes; persisted migration/legacy skill receipt and next spin361+9=370");
    }
    static void NewCommandFaults()
    {
        ReplayFaultMatrix.Verify("modern-workshop-acquisition",Raw("r6/selected-workshop"),(g,q)=>g.ExecuteChapterSpin(q,1000),
            s=>Check(s.coins==119&&s.workshopBuilt==0,"Workshop before"),s=>Check(s.coins==7&&s.workshopBuilt==1&&s.lastSpin.bonus==0&&s.lastSpin.chapter.Effects.UniversalCoins==0,"Modern acquisition received new effect"));
        ReplayFaultMatrix.Verify("modern-offline-funded-pigeon",Raw("r7/offline-funded-pigeon"),(g,q)=>g.SelectChapterProject("pigeon-loft.build",q,1600),
            s=>Check(s.coins==249&&s.selectedStage==5,"Pigeon before"),s=>Check(s.coins==9&&s.seed==1&&WaveProgress.Of(s).PigeonLoftBuilt==1&&WaveProgress.Of(s).Delivery.PigeonProgress==0&&s.lastSettlement.coins==10,"Visible purchase changed frozen old receipt/RNG"));
        var skillRaw=Raw("r7/owned-selected-house");var request=ChapterEconomy.QuoteSkill(Codec.Decode(skillRaw),SkillIds.Recycling1).Request;
        Check(request!=null,"Published skill quote missing");
        ReplayFaultMatrix.Verify("modern-skill-purchase",skillRaw,(g,q)=>g.BuySkill(request,1000),
            s=>Check(s.coins==340&&s.sequence==5,"Skill before"),s=>Check(s.coins==180&&s.sequence==6&&s.seed==12305&&ChapterEconomy.Project(s).Skills.Owns(SkillIds.Recycling1)&&ChapterEconomy.Project(s).Skills.Receipts.Count==2&&s.selectedStage==2&&WaveProgress.Of(s).Delivery.LastOperationSequence==0&&WaveProgress.Of(s).Delivery.PigeonProgress==0&&WaveProgress.Of(s).Slot.PurchaseSequence==4&&WaveProgress.Of(s).Slot.PurchaseWalletBefore==460,"Skill purchase changed target/price/ownership/RNG/carry/legacy receipt"));
        var storage=new Storage{raw=skillRaw};var game=Open(storage);game.SelectChapterProject("house.improve",6,1000);int writes=storage.writes;string bytes=storage.raw;
        Reject(()=>game.BuySkill(request,1000),"Stale old-context skill request accepted");Check(storage.writes==writes&&storage.raw==bytes&&!game.RecoveryRequired,"Stale skill request wrote or blocked valid store");
        Pass("real modern spin/selected purchase/skill APIs pass all four write-fault positions, frozen recovery and duplicate command suppression");
    }
    static void BuyEra(GameSession game,string era)
    {foreach(string family in FamilyIds.All)for(int local=1;local<=2;local++){string id=ChapterIds.Project(era,family,local);if(ChapterCatalog.Rules.AvailableUpgrades(game.Chapter.Progress).Contains(id))game.SelectChapterProject(id,game.State.sequence+1,1600);}}
    static string LegacyHistoryAtBoundary()
    {
        var storage=new Storage{raw=Fund(Raw("r7/owned-offline-seen"),20000)};var game=Open(storage);BuyEra(game,EraIds.Junkyard);
        Check(game.State.coins==17460&&game.State.sequence==16&&game.State.seed==2451568673&&WaveProgress.Of(game.State).Delivery.PigeonProgress==2,"Independent first-era carry fixture costs changed");Receipt(game.State);
        Check(ChapterCatalog.Rules.CanUnlockWorkshopYard(game.Chapter.Progress)&&FamilyIds.All.All(id=>ChapterCatalog.Rules.Stages(game.Chapter.Progress).AbsoluteLevel(id)==2),"Fixture did not reach all six2");return storage.raw;
    }
    static void Entered(ModelState s,long seen)
    {
        var c=ChapterEconomy.Project(s);var stages=ChapterCatalog.Rules.Stages(c.Progress);
        Check(c.EntrySequence==17&&c.TransitionSeen==seen&&stages.EraId==EraIds.WorkshopYard&&FamilyIds.All.All(id=>stages.AbsoluteLevel(id)==3),"Era marker/reset differs");
        Check(s.coins==17460&&s.seed==2451568673&&WaveProgress.Of(s).Delivery.PigeonProgress==2&&s.selectedStage==0&&c.SelectedProjectId==null,"Entry charged/granted/redrew/reset carry or selected target");Receipt(s);
        Check(s.lastSpin.sequence==6&&s.lastSpin.chapter==null&&s.lastSpin.wave!=null&&s.lastSpin.symbols.SequenceEqual(new[]{3,3,2})&&s.lastSpin.reward==8&&s.lastSpin.bonus==1&&s.lastSpin.wave.SlotCoins==1,"Transition replaced frozen r7 receipt/history");
    }
    static void EraReplayAndPresentation()
    {
        string before=LegacyHistoryAtBoundary();var storage=new Storage{raw=before};var game=Open(storage);int writes=storage.writes;
        var quote=ChapterCatalog.Rules.ProposeUnlockWorkshopYard(game.Chapter.Progress,game.State.coins);
        // Cancelling an inspected offer issues no economic command.
        Check(storage.raw==before&&storage.writes==writes&&quote.Price==0,"Inspect/cancel before confirmation wrote");
        ReplayFaultMatrix.Verify("actual-era-entry",before,(g,q)=>g.EnterWorkshopYard(q,1600),s=>Check(s.coins==17460&&s.sequence==16,"Entry before"),s=>Entered(s,0));
        game.EnterWorkshopYard(17,1600);string entered=storage.raw;Entered(game.State,0);
        game=Open(storage);Check(game.Chapter.TransitionPending,"Reload lost committed transition presentation");
        string sourcesBefore=string.Join("|",ChapterCatalog.Rules.SymbolModifiers(Codec.Decode(before).chapter.Progress).Sources.Select(x=>x.SourceId));
        string sourcesAfter=string.Join("|",ChapterCatalog.Rules.SymbolModifiers(game.Chapter.Progress).Sources.Select(x=>x.SourceId));Check(sourcesBefore==sourcesAfter,"Era entry added/removed owned benefits");
        writes=storage.writes;
        Reject(()=>game.ExecuteChapterSpin(18,1600),"Spin allowed before transition acknowledgement");
        Reject(()=>game.SelectChapterProject("house.workshop-yard.upgrade-1",18,1600),"Project allowed before acknowledgement");
        Reject(()=>game.BuySkill(ChapterEconomy.QuoteSkill(game.State,SkillIds.Recycling1).Request,1600),"Skill allowed before acknowledgement");
        Reject(()=>game.EnterWorkshopYard(18,1600),"Era entered twice");Check(storage.raw==entered&&storage.writes==writes,"Blocked transition action wrote");
        ReplayFaultMatrix.Verify("actual-era-ack",entered,(g,q)=>g.AcknowledgeEraTransition(q,1600),s=>Entered(s,0),s=>Entered(s,17));
        game.AcknowledgeEraTransition(18,1600);game=Open(storage);Entered(game.State,17);Check(!game.Chapter.TransitionPending,"Skip/ack rollback or presentation replay");
        writes=storage.writes;game.AcknowledgeEraTransition(19,1600);Check(storage.writes==writes&&game.State.sequence==18,"Acknowledged presentation created repeated economic command");
        string acknowledged=storage.raw;
        ReplayFaultMatrix.Verify("actual-era2-stage",acknowledged,(g,q)=>g.SelectChapterProject("house.workshop-yard.upgrade-1",q,1600),s=>Entered(s,17),
            s=>Check(s.coins==17040&&s.seed==2451568673&&ChapterCatalog.Rules.Stages(s.chapter.Progress).AbsoluteLevel(FamilyIds.House)==4&&WaveProgress.Of(s).Delivery.PigeonProgress==2&&s.chapter.Skills.Receipts.Count==1,"Era2 stage debit/history changed"));
        var offlineStore=new Storage{raw=entered};var paused=Open(offlineStore);paused.SettleOffline(1660);
        Check(paused.State.coins==17461&&paused.Chapter.TransitionPending&&WaveProgress.Of(paused.State).Delivery.PigeonProgress==2,"Transition offline protocol bought/redrew/lost marker");
        paused=Open(offlineStore);paused.AcknowledgeEraTransition(paused.State.sequence+1,1660);Check(paused.State.coins==17461&&!paused.Chapter.TransitionPending,"Offline then skip replayed entry/reward");
        Pass("legacy carry/boost/offline history survives actual era entry, four-boundary unlock/ack/era2 purchase, cancel-before-command and reload/skip semantics");
    }
    static void StrictModernAndOptionalEnd()
    {
        var storage=new Storage{raw=Fund(Raw("r6/fresh"),10000)};var game=Open(storage);BuyEra(game,EraIds.Junkyard);
        Check(game.State.coins==6880&&game.State.sequence==12&&game.Chapter.Skills.Receipts.Count==0,"Skill-free first era costs changed");
        game.EnterWorkshopYard(13,1600);game.AcknowledgeEraTransition(14,1600);BuyEra(game,EraIds.WorkshopYard);game=Open(storage);
        Check(game.State.coins==540&&game.State.sequence==26&&game.State.seed==1&&game.Chapter.Skills.Receipts.Count==0&&ChapterCatalog.Rules.IsChapterComplete(game.Chapter.Progress)&&HouseGoal.Complete(game.State),"Actual saved chapter end requires skills or total9460 changed");
        var pending=ChapterEconomy.Reserve(game.State,"spin",null,27,1600);string raw=Codec.Encode(pending);Check(raw.Length<65536,"Valid full descriptor exceeds save cap");
        var json=JsonNode.Parse(raw);Check(json["pending"]["chapter"]["effects"]["sources"].AsArray().Count==19,"Full chapter sources not frozen");Codec.Decode(raw);
        foreach(string field in new[]{"chapter"}){var broken=JsonNode.Parse(raw);broken["pending"].AsObject().Remove(field);Reject(()=>Codec.Decode(broken.ToJsonString()),"Missing modern pending descriptor fell back to legacy");}
        var bad=JsonNode.Parse(raw);bad["pending"]["chapter"]=null;Reject(()=>Codec.Decode(bad.ToJsonString()),"Null modern descriptor accepted");
        bad=JsonNode.Parse(raw);bad["pending"]["chapter"]["format"]=1;Reject(()=>Codec.Decode(bad.ToJsonString()),"Unknown operation format accepted");
        bad=JsonNode.Parse(raw);bad["pending"]["chapter"]["effects"]["sources"][0]["id"]="fake.new.symbol-coins";Reject(()=>Codec.Decode(bad.ToJsonString()),"Unowned/unknown positive source accepted");
        bad=JsonNode.Parse(raw);bad["pending"]["chapter"]["symbols"]=new JsonArray(0,0,2,3);Reject(()=>Codec.Decode(bad.ToJsonString()),"Extended source array limit weakened exact-three reels");
        game.ExecuteChapterSpin(27,1600);Check(game.State.coins==565&&game.State.seed==2647435461&&game.State.lastSpin.chapter.Effects.Modifiers.Sources.Count==19,"No-skill end spin540+8+workshop1+trash1+chapter15 differs");
        game=Open(storage);Check(game.State.coins==565&&ChapterCatalog.Rules.IsChapterComplete(game.Chapter.Progress)&&game.Chapter.Skills.Receipts.Count==0,"End reload reset/granted skill/replayed");
        Pass("actual optional-skill chapter end, full19-source journal below65k, named-source parsing and malformed modern descriptor rejection");
    }
    public static void Run()
    {
        GoldenQueries();NewCommandFaults();EraReplayAndPresentation();StrictModernAndOptionalEnd();
        Console.WriteLine("PASS "+groups+" modern Core compatibility groups / "+assertions+" counted assertions, plus portable four-position replay checks");
    }
}
#endif
