#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JunkToNeon.Core;
using JunkToNeon.Guidance;
using JunkToNeon.Contracts;
using JunkToNeon.Upgrades;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    public sealed partial class FirstSessionPresentationUiTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui = new ReelUiLifecycleTests();
        readonly JsonStateCodec codec = new JsonStateCodec();
        Component Game => (Component)typeof(ReelUiLifecycleTests).GetField("game", Hidden).GetValue(ui);
        GameSession Session => (GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        string Folder => (string)typeof(ReelUiLifecycleTests).GetField("directory", Hidden).GetValue(ui);
        T Field<T>(string name) => (T)Game.GetType().GetField(name, Hidden).GetValue(Game);
        bool[] YardSelection() => Field<Array>("yardFamilies").Cast<Component>().Select(site=>(bool)site.GetType().GetProperty("Selected").GetValue(site)).ToArray();
        void Call(string name, params object[] args) => Game.GetType().GetMethod(name, Hidden | BindingFlags.Public).Invoke(Game, args);
        IEnumerator Harness(string name, params object[] args) => (IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(name, Hidden).Invoke(ui, args);
        string State => codec.Encode(Session.State);
        Dictionary<string,string> Files() => Directory.GetFiles(Folder).ToDictionary(Path.GetFileName,
            path => Convert.ToBase64String(File.ReadAllBytes(path)) + "|" + File.GetLastWriteTimeUtc(path).Ticks);
        void Unchanged(string state, Dictionary<string,string> files)
        {
            Assert.That(State, Is.EqualTo(state)); Assert.That(Files(), Is.EquivalentTo(files));
            Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
        }
        IEnumerator Click(string field, int index = -1) { yield return Harness("PointerClick", field, index); yield return null; }
        IEnumerator Back()
        {
            yield return null;
            var adapter = Game.GetComponents<Component>().Single(c => c.GetType().FullName == "JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)adapter.GetType().GetMethod("RequestBack").Invoke(adapter, null), Is.True); yield return null;
        }
        static void Fits(TMP_Text text)
        {
            Assert.That(text.isActiveAndEnabled, Is.True, text.name); Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
            Assert.That(text.isTextOverflowing, Is.False, text.name + ": " + text.text);
            Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 2), text.name);
            Assert.That(text.textBounds.size.y, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name);
        }
        void NoFlyers()
        {
            Assert.That((int)Game.GetType().GetProperty("RewardFeedbackVisibleCount").GetValue(Game), Is.Zero);
            Assert.That(Field<TMP_Text[]>("rewardFeedbackLabels").Where(t => t.gameObject.activeInHierarchy), Is.Empty);
        }
        void Snapshot(string name)
        {
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/first-session-delivery-20261101/root"));
            Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,name+"-"+Guid.NewGuid().ToString("N")+".state.json");
            File.WriteAllText(path,State,new System.Text.UTF8Encoding(false));
            TestContext.WriteLine("Private produced snapshot: "+path);
        }
        void MidLateSnapshot(string name,string provenance)
        {
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/mid-late-delivery-20261101/root","produced-"+name+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"jtn.save.v1.json");
            File.WriteAllText(path,State,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory,"visible-message.txt"),Field<TMP_Text>("message").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory,"visible-heading.txt"),Field<TMP_Text>("heading").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory,"product-context.txt"),Field<TMP_Text>("productContext").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory,"provenance.txt"),provenance+" Checkpoint: "+name+"; sequence: "+Session.State.sequence+". Visible text is captured at this checkpoint and is not recreated by reload.",new System.Text.UTF8Encoding(false));
            TestContext.WriteLine("Private produced snapshot: "+path);
        }
        IEnumerator Capture(string name)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.work/first-session-integration-20261101/root"));
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path) || File.Exists(Path.ChangeExtension(path, ".state.json")))
                path = Path.Combine(directory, name + "-" + Guid.NewGuid().ToString("N") + ".png");
            string state = State;
            ScreenCapture.CaptureScreenshot(path);
            File.WriteAllText(Path.ChangeExtension(path, ".state.json"), state, new System.Text.UTF8Encoding(false));
            for (int frame = 0; frame < 30 && !File.Exists(path); frame++) yield return null;
            Assert.That(File.Exists(path), Is.True, "Capture did not appear: " + path); TestContext.WriteLine("Private UI capture: " + path);
        }
        IEnumerator ReplacePrivate(ModelState state)
        {
            state.lastSettledAt = 1000; GameLogic.Validate(state);
            File.WriteAllText(Path.Combine(Folder, "jtn.save.v1.json"), codec.Encode(state));
            yield return Harness("ReenterScene");
        }
        sealed class MemoryStorage : ISnapshotStorage
        {
            public string Raw;
            public string ReadPrimary() => Raw;
            public string ReadBackup() => null;
            public void Write(string value) { Raw = value; }
        }
        ModelState WorkshopSelection()
        {
            // Declared private starting funding; the goal is a genuine public
            // selection, and the subsequent acquisition uses the actual UI.
            var initial = ModelState.Fresh(1); initial.coins = 115; initial.houseStage = 1;
            initial.autoHintSeen = 1; initial.lastSettledAt = 1000; GameLogic.Validate(initial);
            var setup = GameSession.Open(new GameStore(new MemoryStorage { Raw = codec.Encode(initial) }, codec), 1, initializeJourney: true);
            setup.SelectChapterProject("workshop.build", setup.State.sequence + 1, 1000);
            Assert.That(ChapterEconomy.SelectedProjectPrice(setup.Chapter), Is.EqualTo(120));
            Assert.That(setup.State.coins, Is.EqualTo(115)); Assert.That(setup.State.workshopBuilt, Is.Zero);
            Assert.That(setup.State.lastSpin, Is.Null); Assert.That(setup.State.seed, Is.EqualTo(1u));
            return setup.State;
        }
        [UnityTest] public IEnumerator AutoPausesAfterPaidGoalAndCanRestartWithoutAGoal()
        {
            yield return Harness("Prepare",WorkshopSelection());Call("SetRewardFeedbackReduced",true);
            Assert.That(Session.State.coins,Is.EqualTo(115));Assert.That(Session.State.workshopBuilt,Is.Zero);
            yield return Click("autoButton");yield return Harness("FinishPresentation");
            Assert.That(Session.State.coins,Is.EqualTo(3));Assert.That(Session.State.workshopBuilt,Is.EqualTo(1));
            Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.False,"A completed paid goal returns control to the player");
            string completed=State;var files=Files();long spin=Session.State.lastSpin.sequence;
            yield return new WaitForSecondsRealtime(EconomyRules.AutoGapSeconds+.25f);Unchanged(completed,files);
            yield return Click("autoButton");
            Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.True,"No-goal free AUTO remains an explicit available choice");
            Assert.That(Session.State.lastSpin.sequence,Is.GreaterThan(spin));
            yield return Click("autoButton");yield return Harness("FinishPresentation");
            Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.False);
        }
        [UnityTest] public IEnumerator FreshOriginFirstHouseKeepsOneExplicitChoiceBeforeTheFirstSpin()
        {
            yield return Harness("LoadGame",false);
            Assert.That(Session.Journey.Guidance.Origin,Is.EqualTo(GuideOrigin.Fresh));
            Assert.That(Session.State.coins,Is.EqualTo(37));string before=State;var files=Files();
            Assert.That(Field<Button>("autoButton").interactable,Is.False);
            Assert.That(Field<TMP_Text>("autoLabel").text,Does.Contain("after House"));Fits(Field<TMP_Text>("autoLabel"));
            Call("StopAuto");Assert.That(Field<TMP_Text>("autoLabel").text,Does.Contain("after House"));Unchanged(before,files);
            Assert.That(YardSelection().Count(selected=>selected),Is.Zero);
            if(Environment.GetEnvironmentVariable("JTN_CAPTURE_FIRST_HOUSE")=="1")yield return Capture("first-house-fallback-ruin");
            int frame=Time.frameCount;yield return Click("productGoalButton");
            Assert.That(Field<string>("chapterProject"),Is.EqualTo("house.repair"));Unchanged(before,files);
            yield return Click("selectButton");Assert.That(Session.State.coins,Is.EqualTo(37));
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("house.repair"));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(40));
            Assert.That(YardSelection(),Is.EqualTo(new[]{true,false,false,false,false,false}));
            Assert.That(Field<TMP_Text>("yardInspectionHint").text,Does.Contain("Selected: House"));Fits(Field<TMP_Text>("yardInspectionHint"));
            long sequence=Session.State.sequence;yield return Click("spinButton");
            Assert.That(YardSelection()[0],Is.True,"Selected marker remains with the pending before-state");
            Assert.That(Field<int[]>("yardPaidStages")[0],Is.Zero);
            Assert.That(Field<TMP_Text>("autoLabel").text,Does.Contain("after House"),"AUTO prerequisite follows the pending before-state too");
            Call("StopAuto");Assert.That(Field<TMP_Text>("autoLabel").text,Does.Contain("after House"));
            yield return Harness("FinishPresentation");
            Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(sequence+1));
            Assert.That(Session.State.lastSpin.chapter.Price,Is.EqualTo(40));
            Assert.That(Session.State.coins,Is.EqualTo(37+Session.State.lastSpin.reward-40));
            Assert.That(Session.State.houseStage,Is.EqualTo(1));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            Assert.That(Field<int[]>("yardPaidStages")[0],Is.EqualTo(1));
            Assert.That(YardSelection().Count(selected=>selected),Is.Zero);
            if(Field<bool>("autoHintPanel"))yield return Click("closeButton");
            Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.False);
            Assert.That(Field<Button>("autoButton").interactable,Is.True);
            Assert.That(Field<TMP_Text>("autoLabel").text,Is.EqualTo("AUTO"));
            Assert.That(Field<TMP_Text>("productGoalText").gameObject.activeInHierarchy,Is.True);
            Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("Workshop").And.Contain("120"));Fits(Field<TMP_Text>("productGoalText"));
            Assert.That(Time.frameCount,Is.GreaterThan(frame));
            if(Environment.GetEnvironmentVariable("JTN_CAPTURE_FIRST_HOUSE")=="1")yield return Capture("first-house-fallback-repaired");
        }
        [UnityTest] public IEnumerator FirstHouseGoalInspectsBeforeExplicitSelectionOrPurchase()
        {
            foreach(long wallet in new[]{37L,45L})
            {
                var fresh=ModelState.Fresh(1);fresh.coins=wallet;
                if(wallet==37)yield return Harness("Prepare",fresh);else yield return ReplacePrivate(fresh);
                string before=State;var files=Files();long sequence=Session.State.sequence;
                Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("Repair House").And.Contain("40 coins").And.Contain("Tap to inspect and choose"));Fits(Field<TMP_Text>("productGoalText"));
                yield return Click("productGoalButton");
                Assert.That(Field<string>("chapterProject"),Is.EqualTo("house.repair"));
                Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("House"));
                Assert.That(Field<Button>("selectButton").GetComponentInChildren<TMP_Text>().text,Is.EqualTo(wallet==37?"Choose project":"Build for 40 Coins"));
                Fits(Field<TMP_Text>("panelBody"));Unchanged(before,files);
                yield return Back();Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("My District"));Unchanged(before,files);
                yield return Click("productCards",0);Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Buildings · all six"));
                Assert.That(Field<Button[]>("navigationCards").Count(b=>b.gameObject.activeSelf),Is.EqualTo(6));Unchanged(before,files);
                yield return Click("productTabs",1);yield return Click("productGoalButton");Unchanged(before,files);
                var queued=Field<Button>("selectButton").onClick;yield return Click("selectButton");
                Assert.That(Session.State.coins,Is.EqualTo(wallet==37?37:5));Assert.That(Session.State.houseStage,Is.EqualTo(wallet==37?0:1));
                Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(wallet==37?"house.repair":null));
                Assert.That(Session.State.seed,Is.EqualTo(1u));Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));
                if(wallet==45)Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("Workshop").And.Contain("120"));
                string committed=State;files=Files();queued.Invoke();Unchanged(committed,files);
            }
        }
        [UnityTest] public IEnumerator FirstRepairAdviceReceiptsAndOptionalEventsKeepExistingGoalTerms()
        {
            yield return Harness("Prepare", ModelState.Fresh(1));
            Assert.That(Session.State.coins, Is.EqualTo(37)); Assert.That(Session.State.seed, Is.EqualTo(1u));
            Assert.That(Session.Journey.Guidance.Origin, Is.EqualTo(GuideOrigin.Existing));
            Call("SetRewardFeedbackReduced", true);
            yield return Click("productGoalButton"); yield return Click("selectButton");
            Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo("house.repair"));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(40)); Assert.That(Session.State.coins, Is.EqualTo(37));
            yield return Click("spinButton"); yield return Harness("FinishPresentation");
            Assert.That(Session.State.coins, Is.EqualTo(5)); Assert.That(Session.State.houseStage, Is.EqualTo(1));
            Assert.That(Session.State.seed, Is.EqualTo(2647435461u)); Assert.That(Session.State.lastSpin.symbols, Is.EqualTo(new[] { 0, 0, 2 }));
            Assert.That(Session.State.lastSpin.chapter.BaseCoins, Is.EqualTo(8)); Assert.That(Session.State.lastSpin.chapter.WorkshopCoins, Is.Zero);
            Assert.That(Session.State.lastSpin.chapter.Price, Is.EqualTo(40));
            Assert.That(Session.Arcade.LastResourceReceipt.CoinIncome, Is.EqualTo(8)); Assert.That(Session.Arcade.LastResourceReceipt.ConstructionDebit, Is.EqualTo(40));
            Assert.That(Session.Chapter.SelectedProjectId, Is.Null); NoFlyers();
            if (Field<GameObject>("panel").activeSelf && Field<bool>("autoHintPanel")) yield return Click("closeButton");
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False); Assert.That(Session.Chapter.SelectedProjectId, Is.Null);
            var reaction=Field<TMP_Text>("heading"); Fits(reaction);
            Assert.That(reaction.transform.parent,Is.EqualTo(Field<RectTransform>("productChrome"))); Assert.That(reaction.raycastTarget,Is.False);
            Assert.That(Session.Guidance.LastCompletion.ProjectId,Is.EqualTo("house.repair")); Snapshot("repair-existing");
            var goal = Field<TMP_Text>("productGoalText"); var message = Field<TMP_Text>("message");
            Assert.That(goal.text.ToLowerInvariant(), Does.Contain("workshop").And.Contain("120").And.Contain("+1"));
            Assert.That(message.text.ToLowerInvariant(), Does.Contain("+8").And.Contain("40").And.Contain("1/2").And.Contain("saved"));
            Fits(goal); Fits(message); yield return Capture("repair-tested");
            string before = State; var files = Files();
            yield return Click("productGoalButton");
            Assert.That(Field<TMP_Text>("panelTitle").text, Is.EqualTo("Workshop"));
            Assert.That(Field<string>("chapterProject"), Is.EqualTo("workshop.build"));
            Assert.That(Field<TMP_Text>("panelBody").text, Does.Contain("120").And.Contain("+1 Coin per future spin"));
            Fits(Field<TMP_Text>("panelTitle")); Fits(Field<TMP_Text>("panelBody")); Unchanged(before, files);
            yield return Back(); Assert.That(Field<TMP_Text>("panelTitle").text, Is.EqualTo("My District")); Unchanged(before, files);
            yield return Back(); Assert.That(Field<GameObject>("panel").activeSelf, Is.False); Unchanged(before, files);

            // House II remains an inspectable alternative with its actual
            // progress purpose; inspection cannot silently choose it.
            yield return Click("productTabs", 0); yield return Click("productCards", 0); yield return Click("navigationCards", 0);
            Assert.That(Field<string>("chapterProject"), Is.EqualTo("house.improve"));
            Assert.That(Field<TMP_Text>("panelBody").text, Does.Contain("Price: 480").And.Contain("Completes this building for the next era").And.Not.Contain("+1 Coin per future spin"));
            Assert.That(Field<Button>("selectButton").gameObject.activeInHierarchy, Is.True); Fits(Field<TMP_Text>("panelBody")); Unchanged(before, files);
            yield return Click("productTabs", 1); Assert.That(Field<GameObject>("panel").activeSelf, Is.False); Unchanged(before, files);

            // The existing bounded selected-goal boundary uses actual UI input
            // for the acquisition; do not reload or render away its result.
            yield return ReplacePrivate(WorkshopSelection()); Call("SetRewardFeedbackReduced", true);
            Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo("workshop.build")); Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(120));
            yield return Click("spinButton"); yield return Harness("FinishPresentation");
            Assert.That(Session.State.coins, Is.EqualTo(3)); Assert.That(Session.State.workshopBuilt, Is.EqualTo(1));
            Assert.That(Session.State.seed, Is.EqualTo(2647435461u)); Assert.That(Session.State.lastSpin.chapter.BaseCoins, Is.EqualTo(8));
            Assert.That(Session.State.lastSpin.chapter.Price, Is.EqualTo(120)); Assert.That(Session.State.lastSpin.chapter.WorkshopCoins, Is.Zero);
            Assert.That(Session.Arcade.LastResourceReceipt.CoinIncome, Is.EqualTo(8)); Assert.That(Session.Arcade.LastResourceReceipt.ConstructionDebit, Is.EqualTo(120));
            message = Field<TMP_Text>("message");
            Assert.That(message.text.ToLowerInvariant(), Does.Contain("+8").And.Contain("120").And.Contain("1/2").And.Contain("saved"));
            Assert.That(message.text.ToLowerInvariant(), Does.Not.Contain("workshop 1 included")); Fits(message); NoFlyers();
            yield return Click("spinButton"); yield return Harness("FinishPresentation");
            Assert.That(Session.State.coins, Is.EqualTo(12)); Assert.That(Session.State.seed, Is.EqualTo(745495504u));
            Assert.That(Session.State.lastSpin.symbols, Is.EqualTo(new[] { 0, 2, 0 }));
            Assert.That(Session.State.lastSpin.chapter.BaseCoins, Is.EqualTo(8)); Assert.That(Session.State.lastSpin.chapter.WorkshopCoins, Is.EqualTo(1));
            Assert.That(Session.Arcade.LastResourceReceipt.CoinIncome, Is.EqualTo(9)); Assert.That(Session.Arcade.LastResourceReceipt.ConstructionDebit, Is.Zero);
            Assert.That(message.text.ToLowerInvariant(), Does.Contain("+9").And.Contain("workshop 1").And.Contain("included"));
            Fits(message); NoFlyers(); yield return Capture("workshop-tested");
            Fits(Field<TMP_Text>("heading")); Assert.That(Session.Guidance.LastCompletion.ProjectId,Is.EqualTo("workshop.build")); Snapshot("workshop-existing");

            // Existing bounded producer setup earns one real ticket through its
            // eight public spins and retains the historical selected 540 goal.
            var fixtureOwner = new ArcadeUiTests();
            var ticketFixture = (ModelState)typeof(ArcadeUiTests).GetMethod("Prepared", Hidden).Invoke(fixtureOwner,
                new object[] { 540L, 0L, 80, 80, 30, false, 8 });
            yield return ReplacePrivate(ticketFixture); Call("SetRewardFeedbackReduced", true);
            Assert.That(Session.Arcade.Tickets, Is.EqualTo(1)); Assert.That(Session.Arcade.TicketProgress, Is.Zero);
            Assert.That(Session.Arcade.LastResourceReceipt.TicketsAdded, Is.EqualTo(1));
            Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo("house.improve")); Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(540));
            goal = Field<TMP_Text>("productGoalText"); Assert.That(goal.text, Does.Contain("540")); Fits(goal);
            var badge = Field<TMP_Text>("productEventBadge"); Assert.That(badge.text.ToLowerInvariant(), Does.Contain("1").And.Contain("ticket").And.Contain("events"));
            message = Field<TMP_Text>("message"); Assert.That(message.text.ToLowerInvariant(), Does.Contain("ticket").And.Contain("events").And.Contain("optional")); Fits(message);
            Fits(badge); NoFlyers(); yield return Capture("ticket-tested"); before = State; files = Files();
            yield return Click("productGoalButton"); Assert.That(Field<string>("chapterProject"), Is.EqualTo("house.improve"));
            Assert.That(Field<TMP_Text>("panelBody").text, Does.Contain("540").And.Contain("480")); Unchanged(before, files);
            yield return Back(); Assert.That(Field<TMP_Text>("panelTitle").text, Is.EqualTo("My District")); Unchanged(before, files);
            yield return Back(); Unchanged(before, files);
            yield return Click("productEventShortcut"); Assert.That(Field<int>("wavePanel"), Is.EqualTo(71));
            Assert.That(Field<TMP_Text>("productContext").text.ToLowerInvariant(), Does.Contain("ticket"));
            Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo("house.improve")); Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(540));
            Unchanged(before, files); yield return Back(); Unchanged(before, files);

            // This is the established synthetic legacy-600 boundary; no paid
            // receipt or prior spin is fabricated.
            var old600 = ModelState.Fresh(1); old600.coins = 535; old600.houseStage = 1; old600.autoHintSeen = 1; old600.selectedStage = 2;
            yield return ReplacePrivate(old600); Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo("house.improve"));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(600));
            goal = Field<TMP_Text>("productGoalText"); Assert.That(goal.text, Does.Contain("600").And.Not.Contain("120")); Fits(goal);
            before = State; files = Files(); yield return Click("productGoalButton");
            Assert.That(Field<string>("chapterProject"), Is.EqualTo("house.improve")); Assert.That(Field<TMP_Text>("panelBody").text, Does.Contain("600").And.Contain("480")); Unchanged(before, files);
            yield return Back(); Assert.That(Field<TMP_Text>("panelTitle").text, Is.EqualTo("My District")); Unchanged(before, files);
            yield return Back(); Assert.That(Field<GameObject>("panel").activeSelf, Is.False); Unchanged(before, files);
        }
        IEnumerator Skills()
        { yield return Click("productTabs",4); yield return Click("productCards",3); }
        [UnityTest] public IEnumerator HistoricalGoalSkillPreviewAndPurchaseKeepExactSavedPrice()
        {
            string fixture=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/ConstructionPricing/evidence/cadence-r12/v2/v2-selected540.json"));
            yield return Harness("Prepare",codec.Decode(File.ReadAllText(fixture)));
            for(int index=0;index<2;index++)
            {
                if(index==1)
                {
                    var old=ModelState.Fresh(1);old.coins=535;old.houseStage=1;old.autoHintSeen=1;old.selectedStage=2;
                    yield return ReplacePrivate(old);
                }
                long saved=index==0?540:600,remaining=index==0?125:185;
                Assert.That(Session.State.coins,Is.EqualTo(535));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(saved));
                string state=State;var files=Files();uint seed=Session.State.seed;
                yield return Skills();yield return Click("navigationCards",0);
                var body=Field<TMP_Text>("panelBody");Fits(body);
                Assert.That(body.text,Does.Contain("535/"+saved).And.Contain("Price: 120"));
                Assert.That(body.text,Does.Contain("After purchase: 415 Coins. Goal still needs "+remaining+" Coins. Goal retained."));
                Unchanged(state,files);if(index==0)Snapshot("skill-540-available");
                yield return Click("selectButton");
                Assert.That(Session.State.coins,Is.EqualTo(415));Assert.That(Session.State.seed,Is.EqualTo(seed));
                Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("house.improve"));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(saved));
                Assert.That(Session.State.houseStage,Is.EqualTo(1));
                state=State;files=Files();yield return Skills();yield return Click("navigationCards",0);
                Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("already owned").IgnoreCase.And.Not.Contain("After purchase:"));
                Assert.That(Field<Button>("selectButton").gameObject.activeSelf,Is.False);Unchanged(state,files);
                yield return Back();yield return Click("navigationCards",2);
                Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("Build first").And.Not.Contain("After purchase:"));
                Assert.That(Field<Button>("selectButton").gameObject.activeSelf,Is.False);Unchanged(state,files);
            }
            var poor=ModelState.Fresh(1);poor.coins=119;poor.houseStage=1;poor.autoHintSeen=1;poor.selectedStage=2;
            yield return ReplacePrivate(poor);string before=State;var savedFiles=Files();
            yield return Skills();yield return Click("navigationCards",0);
            Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("Need 1 more Coins").And.Not.Contain("After purchase:"));
            Assert.That(Field<Button>("selectButton").gameObject.activeSelf,Is.False);Unchanged(before,savedFiles);
        }

        static RaycastResult Top(Button button,Vector2 local)
        {
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();
            var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(local))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Assert.That(hits.Count,Is.GreaterThan(0),button.name);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(button.gameObject),button.name);return hits[0];
        }
        static PointerEventData Hold(Button button)
        {
            Assert.That(button.isActiveAndEnabled&&button.IsInteractable(),Is.True);var rect=((RectTransform)button.transform).rect;
            Assert.That(rect.width,Is.GreaterThanOrEqualTo(48));Assert.That(rect.height,Is.GreaterThanOrEqualTo(48));
            foreach(var point in new[]{rect.center,rect.min+Vector2.one*4,rect.max-Vector2.one*4,new Vector2(rect.xMin+4,rect.yMax-4),new Vector2(rect.xMax-4,rect.yMin+4)})Top(button,point);
            var hit=Top(button,rect.center);var pointer=new PointerEventData(EventSystem.current){pointerId=701,button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerCurrentRaycast=hit,position=hit.screenPosition};
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);pointer.pointerPress=button.gameObject;pointer.pointerClick=button.gameObject;return pointer;
        }
        static void Release(Button button,PointerEventData pointer)
        {
            pointer.pointerCurrentRaycast=Top(button,((RectTransform)button.transform).rect.center);pointer.position=pointer.pointerCurrentRaycast.screenPosition;
            ExecuteEvents.Execute(pointer.pointerPress,pointer,ExecuteEvents.pointerUpHandler);
            if(pointer.eligibleForClick&&pointer.pointerClick==ExecuteEvents.GetEventHandler<IPointerClickHandler>(pointer.pointerCurrentRaycast.gameObject))
                ExecuteEvents.Execute(pointer.pointerClick,pointer,ExecuteEvents.pointerClickHandler);
        }
        [UnityTest] public IEnumerator SettingsSpeedAndEnergyRequireSeparateCurrentActions()
        {
            yield return Harness("Prepare",ModelState.Fresh(1));string before=State;var files=Files();
            Assert.That(Session.Expansion.SpeedId,Is.EqualTo("speed.normal"));uint seed=Session.State.seed;long coins=Session.State.coins;
            yield return Click("productSettings");Assert.That(Field<Button[]>("productCards")[4].GetComponentInChildren<TMP_Text>(true).text,Does.Contain("normal").And.Contain("3.5").And.Contain("1.5"));
            yield return Click("productCards",4);Assert.That(Field<int>("wavePanel"),Is.EqualTo(50));Unchanged(before,files);
            var primary=Field<Button>("expansionPrimaryButton");var held=Hold(primary);var old=primary.onClick;
            yield return Back();yield return Click("productCards",4);Release(primary,held);old.Invoke();Unchanged(before,files);
            yield return Click("expansionPrimaryButton");Assert.That(Session.Expansion.SpeedId,Is.EqualTo("speed.fast"));
            Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.coins,Is.EqualTo(coins));Assert.That(Session.State.lastSpin,Is.Null);
            Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Snapshot("speed-fast");
            yield return Harness("ReenterScene");Assert.That(Session.Expansion.SpeedId,Is.EqualTo("speed.fast"));

            // Explicit synthetic resource funding using the established public codec fixture.
            var fixtureOwner=new ExpansionUtilityUiTests();
            var poor=(GameSession)typeof(ExpansionUtilityUiTests).GetMethod("Funded",Hidden).Invoke(fixtureOwner,new object[]{0,0,15,0L,false,false,null});
            yield return ReplacePrivate(poor.State);before=State;files=Files();yield return Click("productEnergy");
            Assert.That(Field<TMP_Text>("productContext").text,Does.Contain("20 parts").And.Contain("10 potatoes").And.Contain("Missing"));
            Snapshot("energy-locked-shortage");yield return Click("productCards",0);
            Assert.That(Field<string>("expansionInspectedId"),Is.EqualTo("bonus.unlock"));
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("20 parts").And.Contain("10 potatoes").And.Contain("Missing"));
            Assert.That(Field<ExpansionQuote>("expansionQuote"),Is.Null);Assert.That(Field<Button>("expansionPrimaryButton").gameObject.activeSelf,Is.False);Unchanged(before,files);

            var funded=(GameSession)typeof(ExpansionUtilityUiTests).GetMethod("Funded",Hidden).Invoke(fixtureOwner,new object[]{20,10,14,0L,false,false,null});
            yield return ReplacePrivate(funded.State);yield return Click("productEnergy");yield return Click("productCards",0);
            var unlock=Field<ExpansionQuote>("expansionQuote");Assert.That(unlock.Id,Is.EqualTo("bonus.unlock"));
            Assert.That(unlock.PartsCost,Is.EqualTo(20));Assert.That(unlock.PotatoesCost,Is.EqualTo(10));Assert.That(unlock.EnergyCost,Is.Zero);
            yield return Click("expansionPrimaryButton");Assert.That(Session.Expansion.BonusUnlocked,Is.True);
            Assert.That(Session.Expansion.Parts,Is.Zero);Assert.That(Session.Expansion.Potatoes,Is.Zero);Assert.That(Session.Expansion.Energy,Is.EqualTo(14));
            yield return Click("productTabs",1);yield return Click("spinButton");yield return Harness("FinishPresentation");
            Assert.That(Session.Expansion.Energy,Is.EqualTo(15));Assert.That(Session.Expansion.BonusProgress,Is.EqualTo(1));
            yield return Click("productEnergy");
            int countdown=Session.Expansion.BonusCadence-Session.Expansion.BonusProgress;before=State;files=Files();
            Assert.That(countdown,Is.EqualTo(4));
            Assert.That(Field<TMP_Text>("productContext").text,Does.Contain("bonus in "+countdown).And.Contain("does not reset"));
            yield return Click("productCards",0);var boost=Field<ExpansionQuote>("expansionQuote");
            Assert.That(boost.Id,Is.EqualTo("boost.next-bonus"));Assert.That(boost.EnergyCost,Is.EqualTo(15));Unchanged(before,files);
            yield return Click("expansionPrimaryButton");Assert.That(Session.Expansion.BoostArmed,Is.True);Assert.That(Session.Expansion.Energy,Is.Zero);
            Assert.That(Session.Expansion.BonusCadence-Session.Expansion.BonusProgress,Is.EqualTo(countdown));Snapshot("energy-armed");
            yield return Click("productTabs",1);yield return Click("productEnergy");
            Assert.That(Field<Button[]>("productCards")[1].GetComponentInChildren<TMP_Text>(true).text,Does.Contain("Shared boost armed"));
            before=State;files=Files();yield return Click("productCards",1);Unchanged(before,files);
        }
        ModelState MidLateTradeFixture(long price,int shortage,int parts,int potatoes)
        {
            var owner=new ExpansionTradeUiTests();
            var state=(ModelState)typeof(ExpansionTradeUiTests).GetMethod("Prepared",Hidden).Invoke(owner,new object[]{price});
            state.coins=price-shortage;
            var fields=ExpansionCodec.EncodeState(state.expansion).Split('|');
            Assert.That(fields[0],Is.EqualTo("expansion.state.v1"));Assert.That(fields.Length,Is.EqualTo(21));
            fields[1]=parts.ToString(System.Globalization.CultureInfo.InvariantCulture);fields[2]=potatoes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            state.expansion=ExpansionCodec.DecodeState(string.Join("|",fields));GameLogic.Validate(state);return state;
        }
        string ProductCardText(int index)=>Field<Button[]>("productCards")[index].GetComponentInChildren<TMP_Text>(true).text;
        [UnityTest] public IEnumerator ContextualExchangeUsesSavedPriceAndSeparateConsent()
        {
            string before=null;Dictionary<string,string> files=null;
            var cases=new[]{new{price=540L,shortage=20,parts=60,potatoes=0},new{price=600L,shortage=21,parts=60,potatoes=0},
                new{price=540L,shortage=21,parts=0,potatoes=60},new{price=600L,shortage=20,parts=0,potatoes=60}};
            for(int i=0;i<cases.Length;i++)
            {
                var item=cases[i];var fixture=MidLateTradeFixture(item.price,item.shortage,item.parts,item.potatoes);
                if(i==0)yield return Harness("Prepare",fixture);else yield return ReplacePrivate(fixture);
                Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(item.price));
                Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain(item.price.ToString()));
                before=State;files=Files();yield return Click("productTabs",0);
                var card=Field<Button[]>("productCards")[4];Assert.That(card.gameObject.activeInHierarchy,Is.True);
                string caption=ProductCardText(4);Assert.That(caption,Does.Contain("20 coins").And.Contain(item.parts>0?"20 parts":"20 potatoes"));
                if(item.shortage==20)Assert.That(caption,Does.Contain("Trade covers the goal price"));
                else Assert.That(caption,Does.Contain("Still needed after trade: 1 coins").And.Not.Contain("Trade covers the goal price"));
                Fits(card.GetComponentInChildren<TMP_Text>(true));Unchanged(before,files);
                if(i==0)
                {
                    MidLateSnapshot("exchange-540-shortage20","Existing V2 saved 540 fixture; synthetic wallet520 and Parts60, with genuine free speed adoption. No exchange performed at capture.");var held=Hold(card);var old=card.onClick;
                    yield return Click("productTabs",1);yield return Click("productTabs",0);Release(card,held);old.Invoke();Unchanged(before,files);
                    old=card.onClick;Call("OnApplicationPause",true);Call("OnApplicationPause",false);yield return null;old.Invoke();Unchanged(before,files);
                    yield return Click("productTabs",0);
                    yield return Click("productCards",4);var quote=Field<ExpansionQuote>("expansionQuote");
                    Assert.That(quote.Id,Is.EqualTo("trade.parts"));Assert.That(quote.PartsCost,Is.EqualTo(20));Assert.That(quote.CoinsReward,Is.EqualTo(20));Unchanged(before,files);
                    Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("saved 540 coins").And.Contain("Coins: 520 → 540").And.Contain("After exchange: funded, unpaid"));
                    yield return PurchaseChoicePortrait.Capture(Game,"trade-540-preview","expansionBody");
                    for(int depth=0;Field<int>("wavePanel")!=70&&depth<4;depth++)yield return Back();
                    Assert.That(Field<int>("wavePanel"),Is.EqualTo(70));Unchanged(before,files);
                    yield return Click("productCards",4);var oldConfirm=Field<Button>("expansionPrimaryButton").onClick;
                    yield return Click("expansionPrimaryButton");Assert.That(Session.State.coins,Is.EqualTo(item.price));Assert.That(Session.Expansion.Parts,Is.EqualTo(40));
                    Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("saved 540 coins").And.Contain("Funded, unpaid · Slot → SPIN").And.Not.Contain("Need 20 more"));
                    yield return PurchaseChoicePortrait.Capture(Game,"trade-540-funded","expansionBody");
                    Assert.That(Session.State.houseStage,Is.EqualTo(1));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("house.improve"));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
                    before=State;files=Files();oldConfirm.Invoke();Unchanged(before,files);
                    var repeat=Field<Button>("expansionSecondaryButton");Assert.That(repeat.isActiveAndEnabled,Is.True);
                    yield return Click("expansionSecondaryButton");Assert.That(Session.State.coins,Is.EqualTo(560));Assert.That(Session.Expansion.Parts,Is.EqualTo(20));
                    Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Funded, unpaid").And.Contain("saved 540 coins"));
                    Assert.That(Session.State.houseStage,Is.EqualTo(1));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("house.improve"));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
                }
                else
                {
                    yield return Click("productCards",4);var quote=Field<ExpansionQuote>("expansionQuote");
                    Assert.That(quote.Id,Is.EqualTo(item.parts>0?"trade.parts":"trade.potatoes"));Assert.That(quote.CoinsReward,Is.EqualTo(20));
                    Assert.That(item.parts>0?quote.PartsCost:quote.PotatoesCost,Is.EqualTo(20));Unchanged(before,files);
                    Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("saved "+item.price+" coins").And.Contain("Coins: "+(item.price-item.shortage)+" → "+(item.price-item.shortage+20)).And.Contain(item.shortage==20?"After exchange: funded, unpaid":"After exchange: need 1 coin."));
                    yield return PurchaseChoicePortrait.Capture(Game,"trade-"+item.price+"-"+quote.Id+"-preview","expansionBody");
                    yield return Click("expansionPrimaryButton");Assert.That(Session.State.coins,Is.EqualTo(item.price-item.shortage+20));
                    Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(item.price));Assert.That(Session.State.houseStage,Is.EqualTo(1));
                    Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain(item.shortage==20?"Funded, unpaid":"Need 1 more coin."));
                    yield return PurchaseChoicePortrait.Capture(Game,"trade-"+item.price+"-"+quote.Id+"-result","expansionBody");
                    if(i==3)
                    {
                        before=State;files=Files();yield return Click("productTabs",1);Unchanged(before,files);
                        Assert.That(Session.State.houseStage,Is.EqualTo(1));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("house.improve"));
                        yield return Click("spinButton");yield return Harness("FinishPresentation");
                        Assert.That(Session.State.lastSpin.symbols,Is.EqualTo(new[]{0,0,2}));
                        Assert.That(Session.Arcade.LastResourceReceipt.CoinIncome,Is.EqualTo(8));Assert.That(Session.Arcade.LastResourceReceipt.ConstructionDebit,Is.EqualTo(600));
                        Assert.That(Session.State.lastSpin.chapter.Price,Is.EqualTo(600));Assert.That(Session.State.coins,Is.EqualTo(8));
                        Assert.That(Session.State.houseStage,Is.EqualTo(2));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
                        yield return PurchaseChoicePortrait.Capture(Game,"trade-600-paid-on-spin","productGoalText");
                        yield return Click("spinButton");yield return Harness("FinishPresentation");
                        Assert.That(Session.Arcade.LastResourceReceipt.ConstructionDebit,Is.Zero);Assert.That(Session.State.houseStage,Is.EqualTo(2));
                        Assert.That(Session.State.coins,Is.EqualTo(8+Session.Arcade.LastResourceReceipt.CoinIncome));
                    }
                }
            }
            var insufficient=MidLateTradeFixture(540,20,19,19);yield return ReplacePrivate(insufficient);before=State;files=Files();yield return Click("productTabs",0);
            Assert.That(Field<Button[]>("productCards")[4].gameObject.activeSelf,Is.False);Unchanged(before,files);
            yield return Click("productTabs",3);yield return Click("productCards",4);
            int unavailableIndex=Array.IndexOf(Field<string[]>("expansionCardIds"),"trade.parts");yield return Click("expansionCards",unavailableIndex);
            Assert.That(Field<ExpansionQuote>("expansionQuote"),Is.Null);Assert.That(Field<Button>("expansionPrimaryButton").gameObject.activeInHierarchy,Is.False);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Need 20 more coins").And.Not.Contain("After exchange:"));Unchanged(before,files);
            yield return PurchaseChoicePortrait.Capture(Game,"trade-unavailable-current-goal","expansionBody");
            var affordable=MidLateTradeFixture(600,0,60,0);yield return ReplacePrivate(affordable);before=State;files=Files();yield return Click("productTabs",0);
            Assert.That(Field<Button[]>("productCards")[4].gameObject.activeSelf,Is.False);Unchanged(before,files);
            var owner=new ExpansionUtilityUiTests();var noGoal=(GameSession)typeof(ExpansionUtilityUiTests).GetMethod("Funded",Hidden).Invoke(owner,new object[]{60,0,0,0L,false,false,null});
            yield return ReplacePrivate(noGoal.State);before=State;files=Files();yield return Click("productTabs",0);
            Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Field<Button[]>("productCards")[4].gameObject.activeSelf,Is.False);Unchanged(before,files);
            yield return Click("productTabs",3);yield return Click("productCards",4);
            int tradeIndex=Array.IndexOf(Field<string[]>("expansionCardIds"),"trade.parts");Assert.That(tradeIndex,Is.GreaterThanOrEqualTo(0));
            yield return Click("expansionCards",tradeIndex);Assert.That(Field<TMP_Text>("expansionBody").text,Does.Not.Contain("Goal:").And.Not.Contain("After exchange:"));Unchanged(before,files);
            var complete=(GameSession)typeof(ExpansionUtilityUiTests).GetMethod("Funded",Hidden).Invoke(owner,new object[]{60,0,0,0L,false,true,null});
            yield return ReplacePrivate(complete.State);Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(24));
            before=State;files=Files();yield return Click("productTabs",3);yield return Click("productCards",4);
            tradeIndex=Array.IndexOf(Field<string[]>("expansionCardIds"),"trade.parts");yield return Click("expansionCards",tradeIndex);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Not.Contain("Goal:").And.Not.Contain("After exchange:"));Unchanged(before,files);
        }
        [UnityTest] public IEnumerator CompletedRootKeepsAcceptedOrderUnitsAndWaitsForReelReveal()
        {
            var owner=new DirectNavigationUiTests();var initial=(ModelState)typeof(DirectNavigationUiTests).GetMethod("CompleteBoundary",Hidden).Invoke(owner,null);
            initial.seed=1;yield return Harness("Prepare",initial);
            Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("District complete · 24/24").And.Contain("Keep playing if you like"));
            foreach(var item in new[]{new{index=1,id="order.neighborhood-supply",unit="0/3 deliveries"},new{index=0,id="order.sort-salvage",unit="0/12 Trash"}})
            {
                yield return Click("productTabs",2);yield return Click("productCards",2);yield return Click("navigationCards",item.index);yield return Click("selectButton");
                Assert.That(Session.Journey.Orders.ActiveOrderId,Is.EqualTo(item.id));
                yield return Click("productTabs",1);var goal=Field<TMP_Text>("productGoalText");Assert.That(goal.text,Does.Contain(item.unit).And.Contain("Optional:").And.Contain(item.index==1?"Neighborhood Delivery":"Sort Trash"));Fits(goal);
                yield return PurchaseChoicePortrait.Capture(Game,"root-"+item.id,"productGoalText");
                if(item.index==1)
                {
                    var before=State;var files=Files();yield return Click("productGoalButton");Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Progress"));Unchanged(before,files);
                    yield return Click("chapterNext");yield return Click("navigationCards",1);yield return Click("chapterPrevious");yield return Click("selectButton");
                    Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);yield return Click("productTabs",1);
                    Assert.That(goal.text,Does.Contain("Keep playing if you like").And.Not.Contain("deliveries"));
                }
            }
            string old=Field<TMP_Text>("productGoalText").text;yield return Click("spinButton");Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.True);
            Assert.That(Session.State.lastSpin.symbols,Is.EqualTo(new[]{0,0,2}));Assert.That(Session.Journey.Orders.Progress,Is.EqualTo(1));
            Assert.That(Field<TMP_Text>("productGoalText").text,Is.EqualTo(old));Call("Render",null,null);yield return null;
            Assert.That(Field<TMP_Text>("productGoalText").text,Is.EqualTo(old),"Committed count must remain hidden before reel reveal");
            yield return Harness("FinishPresentation");Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("1/12 Trash"));
            Assert.That(Field<RectTransform>("productGoalFill").anchorMax.x,Is.EqualTo(1));
            yield return PurchaseChoicePortrait.Capture(Game,"root-trash-revealed","productGoalText");
            var ready=codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/OrderVariety/fixtures/potatoes-ready12.json"))));
            yield return ReplacePrivate(ready);Assert.That(Session.State.coins,Is.EqualTo(690));
            Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("District complete · 24/24").And.Contain("Optional: Gather Potatoes").And.Contain("120 coins unclaimed"));
            yield return PurchaseChoicePortrait.Capture(Game,"root-order-ready","productGoalText");
            yield return Click("spinButton");yield return Harness("FinishPresentation");Assert.That(Session.Journey.Orders.Progress,Is.EqualTo(12));
            string saved=State;var savedFiles=Files();Call("Reload");yield return null;Unchanged(saved,savedFiles);
            Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("120 coins unclaimed"));
            yield return Click("productGoalButton");yield return Click("chapterNext");yield return Click("navigationCards",2);
            long wallet=Session.State.coins;var oldClaim=Field<Button>("selectButton").onClick;yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(wallet+120));Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);
            saved=State;savedFiles=Files();oldClaim.Invoke();Unchanged(saved,savedFiles);
            yield return Click("productTabs",1);Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("Keep playing if you like").And.Not.Contain("unclaimed"));
            Call("Reload");yield return null;Unchanged(saved,savedFiles);Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("Keep playing if you like"));
            // Synthetic completed ownership, then actual public acceptance/spins;
            // this bounded fixture produces a real Ready180 delivery checkpoint.
            initial.lastSettledAt=1000;
            var delivery=GameSession.Open(new GameStore(new MemoryStorage{Raw=codec.Encode(initial)},codec),1,initializeJourney:true);
            delivery.AcceptOrder("order.neighborhood-supply",delivery.State.sequence+1,1000);
            int setupSpins=0;while(!delivery.Journey.Orders.Ready&&setupSpins<64){delivery.ExecuteChapterSpin(delivery.State.sequence+1,1000);setupSpins++;}
            Assert.That(delivery.Journey.Orders.Progress,Is.EqualTo(3));Assert.That(delivery.Journey.Orders.Ready,Is.True);
            yield return ReplacePrivate(delivery.State);Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("Optional: Neighborhood Delivery").And.Contain("180 coins unclaimed"));
            saved=State;savedFiles=Files();yield return PurchaseChoicePortrait.Capture(Game,"root-delivery-ready180","productGoalText");Unchanged(saved,savedFiles);
            TestContext.WriteLine("Delivery Ready180 fixture used "+setupSpins+" public setup spins; three completed deliveries, not individual Pigeons.");
        }
        ModelState MidLateOwnershipBoundary(int absolutePowerLevel)
        {
            var owner=new CombinedLifecycleTests();
            var state=(ModelState)typeof(CombinedLifecycleTests).GetMethod("MilestoneEraFixture",Hidden).Invoke(owner,new object[]{EraIds.WorkshopYard,new[]{5,5,5,5,5,absolutePowerLevel}});
            state.coins=10000;GameLogic.Validate(state);return state;
        }
        ModelState MidLateAllSkillsAndYield()
        {
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/Milestones/fixtures/late23-skills6.json"));
            var state=codec.Decode(File.ReadAllText(path));state.lastSettledAt=1000;GameLogic.Validate(state);
            var setup=GameSession.Open(new GameStore(new MemoryStorage{Raw=codec.Encode(state)},codec),1,initializeJourney:true);
            Assert.That(setup.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(23));Assert.That(setup.Chapter.Skills.Receipts.Count,Is.EqualTo(6));
            setup.SelectChapterProject(ChapterIds.Project(EraIds.WorkshopYard,FamilyIds.PowerSource,2),setup.State.sequence+1,1000);
            Assert.That(setup.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(24));
            setup.SelectExpansionSpeed("speed.fast",setup.State.sequence,1000);state=setup.State;
            // Synthetic initial resources; the two actual catalog purchases below are separate public commands.
            var fields=ExpansionCodec.EncodeState(state.expansion).Split('|');Assert.That(fields[0],Is.EqualTo("expansion.state.v1"));Assert.That(fields.Length,Is.EqualTo(21));
            fields[1]="100";fields[2]="100";state.expansion=ExpansionCodec.DecodeState(string.Join("|",fields));GameLogic.Validate(state);
            setup=GameSession.Open(new GameStore(new MemoryStorage{Raw=codec.Encode(state)},codec),1,initializeJourney:true);
            setup.BuyExpansion(setup.GetExpansionOffer("potato.yield.1"),1000);setup.BuyExpansion(setup.GetExpansionOffer("potato.yield.2"),1000);
            Assert.That(setup.Expansion.PotatoYieldLevel,Is.EqualTo(2));return setup.State;
        }
        [UnityTest] public IEnumerator CompletedChapterDistinguishesSkillsFromResourceActivities()
        {
            yield return Harness("Prepare",MidLateOwnershipBoundary(4));Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(23));
            string before=State;var files=Files();yield return Skills();yield return Click("navigationCards",0);
            Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("Buildings left: 1"));Unchanged(before,files);
            yield return ReplacePrivate(MidLateOwnershipBoundary(5));Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(24));
            Assert.That(Session.Chapter.Skills.Receipts.Count,Is.Zero);before=State;files=Files();yield return Click("productTabs",0);
            Assert.That(Field<TMP_Text>("productContext").text,Does.Contain("District complete").And.Not.Contain("Coin upgrades complete"));MidLateSnapshot("post24-skills0","Synthetic validated 24-stage ownership with zero skills, not a played campaign.");Unchanged(before,files);
            yield return Skills();yield return Click("navigationCards",0);
            Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("For free play after the chapter"));Unchanged(before,files);
            yield return ReplacePrivate(MidLateAllSkillsAndYield());Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(24));
            Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(6));Assert.That(Session.Expansion.PotatoYieldLevel,Is.EqualTo(2));
            before=State;files=Files();yield return Click("productTabs",0);
            Assert.That(Field<TMP_Text>("productContext").text,Does.Contain("Coin upgrades complete").And.Contain("resources and events remain"));
            MidLateSnapshot("post24-skills6-yield-max","Existing late23-skills6 fixture; public final PowerSource purchase, synthetic Parts/Potatoes funding, and two separate public potato-yield purchases.");Unchanged(before,files);
            yield return Click("productTabs",4);
            Assert.That(ProductCardText(3),Does.Contain("All skills owned"));Assert.That(ProductCardText(2),Does.Contain("All levels owned"));
            Assert.That(ProductCardText(1),Does.Contain("Unlocked 0/"));Unchanged(before,files);
            yield return Click("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(51));Unchanged(before,files);
            yield return Back();Unchanged(before,files);
            yield return Click("productTabs",2);Assert.That(ProductCardText(2),Does.Contain("Accept").And.Contain("manual spins").And.Contain("claim"));
            yield return Click("productCards",2);Assert.That(Field<Button[]>("navigationCards")[0].interactable,Is.True);Unchanged(before,files);
        }
        [SetUp] public void Isolate()
        {
            ui.IsolateSave(); Assert.That(Path.GetPathRoot(Path.GetFullPath(Folder)).Replace('\\', '/'), Is.EqualTo("X:/").IgnoreCase);
        }
        int? houseOriginalProfile;
        int HouseProfile(int? next=null)
        {
            var f=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var window=Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().Single(w=>w.GetType().Name=="SimulatorWindow");
            var main=window.GetType().GetField("m_Main",f).GetValue(window);var property=main.GetType().GetProperty("deviceIndex",f);
            int current=(int)property.GetValue(main);Assert.That(current,Is.EqualTo(2).Or.EqualTo(3));
            if(next.HasValue){if(!houseOriginalProfile.HasValue)houseOriginalProfile=current;property.SetValue(main,next.Value);}
            return current;
        }
        [Serializable]sealed class HouseView
        {
            public string name,sprite,color,era,origin;public int frame,width,height,legacy,local;public bool busy,background;
            public Rect safe;public Vector2 world,size,pivot,lastScreen;public long coins,sequence;public uint seed;
        }
        HouseView HouseFacts(string name)
        {
            var image=Field<Image>("house");var c=Session.Chapter;
            return new HouseView{name=name,sprite=image.sprite==null?null:image.sprite.name,color=ColorUtility.ToHtmlStringRGBA(image.color),
                era=c.Progress.EraId,origin=Session.Journey.Guidance.Origin.ToString(),frame=Time.frameCount,width=Screen.width,height=Screen.height,
                legacy=Session.State.houseStage,local=ChapterCatalog.Rules.Stages(c.Progress).LocalLevel(FamilyIds.House),
                busy=(bool)Game.GetType().GetProperty("Busy").GetValue(Game),background=(bool)Game.GetType().GetProperty("InBackground").GetValue(Game),
                safe=Screen.safeArea,world=Field<RectTransform>("world").rect.size,size=image.rectTransform.sizeDelta,pivot=image.rectTransform.pivot,
                lastScreen=Field<Vector2>("lastScreenSize"),coins=Session.State.coins,sequence=Session.State.sequence,seed=Session.State.seed};
        }
        void SameHouse(HouseView old)
        {
            var current=HouseFacts("assert");Assert.That(current.sprite,Is.EqualTo(old.sprite),"House sprite exposed before ordinary reveal");
            Assert.That(current.color,Is.EqualTo(old.color),"House tint exposed before ordinary reveal");
            Assert.That(current.pivot,Is.EqualTo(old.pivot),"House stage pivot exposed before ordinary reveal");
        }
        IEnumerator HouseCapture(string name)
        {
            string root=Environment.GetEnvironmentVariable("JTN_HOUSE_CAPTURE_DIRECTORY");if(string.IsNullOrEmpty(root))yield break;
            string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/world-reveal-20261102/captures"));root=Path.GetFullPath(root);
            Assert.That(root.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(root);
            Assert.That(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length,Is.LessThan(10));
            string path=Path.Combine(root,name+".png");Assert.That(File.Exists(path),Is.False);
            Canvas.ForceUpdateCanvases();File.WriteAllText(Path.ChangeExtension(path,".json"),JsonUtility.ToJson(HouseFacts(name),true));
            string state=State;var files=Files();File.WriteAllText(Path.ChangeExtension(path,".state.json"),state);ScreenCapture.CaptureScreenshot(path);
            for(int i=0;i<30&&!File.Exists(path);i++)yield return null;Assert.That(File.Exists(path),Is.True);Unchanged(state,files);
        }
        IEnumerator HouseFinish()
        {
            float deadline=Time.realtimeSinceStartup+7;
            while((bool)Game.GetType().GetProperty("Busy").GetValue(Game)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.False);yield return null;
        }
        IEnumerator SelectRepair()
        {yield return Click("productGoalButton");yield return Click("selectButton");}
        [UnityTest]public IEnumerator HouseRepairAppearanceWaitsThroughRealResizeAndSessionBoundaries()
        {
            Assert.That(HouseProfile(),Is.EqualTo(2));yield return Harness("Prepare",ModelState.Fresh(1));Call("SetRewardFeedbackReduced",true);
            Assert.That(Session.Journey.Guidance.Origin,Is.EqualTo(GuideOrigin.Existing));yield return SelectRepair();
            var old=HouseFacts("before");Assert.That(old.sprite,Is.EqualTo(Field<Sprite[]>("houseStages")[0].name));
            yield return Click("spinButton");Assert.That(Session.State.coins,Is.EqualTo(5));Assert.That(Session.State.houseStage,Is.EqualTo(1));
            Assert.That(Session.State.lastSpin.symbols,Is.EqualTo(new[]{0,0,2}));Assert.That(HouseFacts("start").busy,Is.True);SameHouse(old);
            yield return HouseCapture("repair-spin-start");string committed=State;var files=Files();
            HouseProfile(3);float deadline=Time.realtimeSinceStartup+1.2f;
            while((Screen.height!=2280||Field<Vector2>("lastScreenSize")!=new Vector2(Screen.width,Screen.height))&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;Canvas.ForceUpdateCanvases();yield return null;
            var resized=HouseFacts("resized");Assert.That(resized.busy,Is.True,"Use ordinary pending presentation, never extend it");
            Assert.That(new[]{resized.width,resized.height},Is.EqualTo(new[]{1080,2280}));Assert.That(resized.world.y,Is.Not.EqualTo(old.world.y).Within(.1f));
            Assert.That(resized.size.y,Is.EqualTo(resized.world.y*.47f).Within(.4f));Unchanged(committed,files);
            yield return HouseCapture("repair-busy-real-resize");SameHouse(old);
            Call("Render",null,null);Call("Render",null,null);SameHouse(old);Unchanged(committed,files);
            uint seed=Session.State.seed;var retainedSpin=codec.Encode(Session.State);
            Call("OnApplicationPause",true);SameHouse(old);yield return null;Call("OnApplicationPause",false);SameHouse(old);
            Assert.That(Session.State.coins,Is.EqualTo(5));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.houseStage,Is.EqualTo(1));
            TestContext.WriteLine("Pause/checkpoint metadata before: "+retainedSpin+"\nAfter: "+State);
            yield return HouseFinish();Assert.That(Field<Image>("house").sprite,Is.EqualTo(Field<Sprite[]>("houseStages")[1]));
            Assert.That(Field<bool>("autoHintPanel"),Is.True,"First repair retains its existing AUTO explanation");
            yield return Click("closeButton");Assert.That(Field<GameObject>("panel").activeSelf,Is.False);yield return HouseCapture("repair-uncovered-reveal");
            // Bounded reload while another genuine repair presentation is pending.
            yield return ReplacePrivate(ModelState.Fresh(1));yield return SelectRepair();yield return Click("spinButton");
            Assert.That(HouseFacts("interrupted").busy,Is.True);string saved=State;Call("Reload");yield return null;
            Assert.That(HouseFacts("loaded").busy,Is.False);Assert.That(Field<Image>("house").sprite,Is.EqualTo(Field<Sprite[]>("houseStages")[1]));
            Assert.That(State,Is.EqualTo(saved));yield return null;yield return null;Assert.That(State,Is.EqualTo(saved));
            yield return Click("closeButton");
            // Actual idle countercase: unselected37->45, explicit repair40->5.
            yield return ReplacePrivate(ModelState.Fresh(1));yield return Click("spinButton");yield return HouseFinish();
            Assert.That(Session.State.coins,Is.EqualTo(45));Assert.That(Session.State.houseStage,Is.Zero);
            seed=Session.State.seed;yield return SelectRepair();Assert.That(Session.State.coins,Is.EqualTo(5));Assert.That(Session.State.seed,Is.EqualTo(seed));
            Assert.That(HouseFacts("immediate").busy,Is.False);Assert.That(Field<Image>("house").sprite,Is.EqualTo(Field<Sprite[]>("houseStages")[1]));
            Assert.That(Field<bool>("autoHintPanel"),Is.True);yield return Click("closeButton");
        }
        sealed class HouseStorage:ISnapshotStorage
        {public string Raw;public string ReadPrimary()=>Raw;public string ReadBackup()=>null;public void Write(string raw){Raw=raw;}}
        [UnityTest]public IEnumerator YardHouseLocalTintWaitsForItsFastReveal()
        {
            // Explicit synthetic valid phase checkpoint, no full-chapter traversal.
            var s=ModelState.Fresh(1);var stages=new FamilyStages(EraIds.WorkshopYard,FamilyIds.All.Select(id=>new FamilyStage(id,3)));
            s.sequence=14;s.revision=28;s.houseStage=2;s.workshopBuilt=1;s.autoHintSeen=1;s.lastSettledAt=1000;
            s.wave=new WaveProgress(1,1,WaveProgress.Empty.Delivery,UpgradeOwnership.Empty);
            s.chapter=new ChapterState(2,JunkToNeon.Content.ChapterContentIds.RuleRevision,ChapterCatalog.Rules.FromStages(stages),SkillOwnership.Empty,null,13,13);
            string id=ChapterIds.Project(EraIds.WorkshopYard,FamilyIds.House,1);long price=ChapterEconomy.ProjectPrice(id);s.coins=price-1;
            var setup=GameSession.Open(new GameStore(new HouseStorage{Raw=codec.Encode(s)},codec),1,initializeJourney:true);
            setup.SelectExpansionSpeed("speed.fast",setup.State.sequence,1000);setup.SelectChapterProject(id,setup.State.sequence+1,1000);
            yield return Harness("Prepare",setup.State);var old=HouseFacts("yard-before");Assert.That(old.sprite,Is.EqualTo(Field<Sprite[]>("houseStages")[2].name));Assert.That(old.color,Is.EqualTo("FFFFFFFF"));string exterior=YardSignature(0);
            Assert.That(old.legacy,Is.EqualTo(2));Assert.That(old.local,Is.Zero);Assert.That(Session.Expansion.SpinDurationSeconds,Is.EqualTo(1.5f));
            yield return Click("spinButton");Assert.That(HouseFacts("yard-start").busy,Is.True);Assert.That(Session.State.houseStage,Is.EqualTo(2));
            Assert.That(ChapterCatalog.Rules.Stages(Session.Chapter.Progress).LocalLevel(FamilyIds.House),Is.EqualTo(1));string committed=State;var files=Files();
            Call("Render",null,null);Call("Render",null,null);SameHouse(old);Assert.That(YardSignature(0),Is.EqualTo(exterior));Unchanged(committed,files);yield return HouseCapture("yard-busy-tint");
            yield return HouseFinish();var after=HouseFacts("yard-after");Assert.That(after.sprite,Is.EqualTo(Field<Sprite[]>("houseStages")[3].name),"The five-stage house art advances with the paid stage");Assert.That(after.color,Is.EqualTo(old.color));Assert.That(YardStage(0),Is.EqualTo(3));Assert.That(YardSignature(0),Is.Not.EqualTo(exterior));string fitted=YardSignature(0);
            Assert.That(after.legacy,Is.EqualTo(2));Assert.That(after.local,Is.EqualTo(1));Assert.That(after.size.x/after.size.y,Is.EqualTo(1f).Within(.001f));
            yield return HouseCapture("yard-normal-reveal");committed=State;Call("Reload");yield return null;
            Assert.That(HouseFacts("yard-loaded").color,Is.EqualTo(after.color));Assert.That(YardSignature(0),Is.EqualTo(fitted));Assert.That(State,Is.EqualTo(committed));
        }
        [Serializable]sealed class ExitMeasure
        {public string name,text;public float font,width,height,boundsWidth,boundsHeight;}
        [Serializable]sealed class ExitView
        {public string name,caption;public int frame,width,height;public bool busy,auto,exit,background,interactable;public long coins,sequence,spin;public uint seed;public ExitMeasure[] texts;}
        Button ExitSelect=>Field<Button>("selectButton");
        IEnumerator ExitCapture(string name)
        {
            string root=Environment.GetEnvironmentVariable("JTN_EXIT_CAPTURE_DIRECTORY");if(string.IsNullOrEmpty(root))yield break;
            root=Path.GetFullPath(root);string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/exit-readiness-20261103/captures"));
            Assert.That(root.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(root);
            Assert.That(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length,Is.LessThan(10));
            string path=Path.Combine(root,name+".png");Assert.That(File.Exists(path),Is.False);string state=State;var files=Files();
            var targets=new[]{Field<TMP_Text>("panelTitle"),Field<TMP_Text>("panelBody"),ExitSelect.GetComponentInChildren<TMP_Text>(true),Field<Button>("closeButton").GetComponentInChildren<TMP_Text>()};
            var texts=new List<ExitMeasure>();foreach(var text in targets.Where(t=>t.isActiveAndEnabled))
            {Fits(text);texts.Add(new ExitMeasure{name=text.name,text=text.text,font=text.fontSize,width=text.rectTransform.rect.width,height=text.rectTransform.rect.height,boundsWidth=text.textBounds.size.x,boundsHeight=text.textBounds.size.y});}
            var view=new ExitView{name=name,caption=ExitSelect.GetComponentInChildren<TMP_Text>(true).text,frame=Time.frameCount,width=Screen.width,height=Screen.height,busy=HouseFacts("exit").busy,
                auto=(bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),exit=Field<bool>("exitPanel"),background=HouseFacts("exit").background,interactable=ExitSelect.IsInteractable(),
                coins=Session.State.coins,sequence=Session.State.sequence,spin=Session.State.lastSpin?.sequence??0,seed=Session.State.seed,texts=texts.ToArray()};
            File.WriteAllText(Path.ChangeExtension(path,".json"),JsonUtility.ToJson(view,true));File.WriteAllText(Path.ChangeExtension(path,".state.json"),state);ScreenCapture.CaptureScreenshot(path);
            for(int i=0;i<30&&!File.Exists(path);i++)yield return null;Assert.That(File.Exists(path),Is.True);Unchanged(state,files);
        }
        void ExitReadiness(bool waiting)
        {
            Assert.That(Field<bool>("exitPanel"),Is.True);Assert.That(Field<GameObject>("panel").activeInHierarchy,Is.True);
            Assert.That(HouseFacts("exit").busy,Is.EqualTo(waiting));Assert.That(ExitSelect.IsInteractable(),Is.EqualTo(!waiting),"Exit readiness must match its Busy refusal");
            Assert.That(Field<TMP_Text>("panelBody").text,Is.EqualTo(waiting?"Finishing the spin. Exit is temporarily unavailable.":"Your results are already saved."));
            Assert.That(ExitSelect.GetComponentInChildren<TMP_Text>().text,Is.EqualTo(waiting?"Finishing spin…":"Exit"));
            Assert.That(Field<Button>("closeButton").IsInteractable(),Is.True);Fits(Field<TMP_Text>("panelBody"));
        }
        static ModelState OrdinaryExitFixture()
        {
            // Declared valid private legacy checkpoint: repaired/ACKed, no goal,
            // no Workshop/offline income. Seed1 earns8, never builds anything.
            var state=ModelState.Fresh(1);state.houseStage=1;state.autoHintSeen=1;return state;
        }
        PointerEventData ExitPointer(int id)
        {
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)ExitSelect.transform;var canvas=ExitSelect.GetComponentInParent<Canvas>();
            var data=new PointerEventData(EventSystem.current){pointerId=id,button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerClick=ExitSelect.gameObject,
                position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Assert.That(hits,Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(ExitSelect.gameObject));data.pointerCurrentRaycast=hits[0];return data;
        }
        void RetiredExitIsInert(Button.ButtonClickedEvent old,string state,Dictionary<string,string> files)
        {Assert.That(ReferenceEquals(ExitSelect.onClick,old),Is.False,"Never invoke a current ready Exit event");old.Invoke();Unchanged(state,files);}
        IEnumerator ExitWorkshop()
        {
            yield return Click("productTabs",0);yield return Click("productCards",0);yield return Click("navigationCards",1);
            Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Workshop"));Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("Price: 120 Coins."));
            Assert.That(ExitSelect.isActiveAndEnabled&&ExitSelect.IsInteractable(),Is.True,"Exit must release the shared CTA before a normal owner binds");
            yield return ExitCapture("workshop-select-restored");
            long coins=Session.State.coins;uint seed=Session.State.seed;yield return Click("selectButton");
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(GameIds.WorkshopBuild));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(120));
            Assert.That(Session.State.coins,Is.EqualTo(coins));Assert.That(Session.State.seed,Is.EqualTo(seed));
        }
        [UnityTest]public IEnumerator ExitWaitsForOrdinarySpinAndRestoresSelectWithoutQuitting()
        {
            yield return Harness("Prepare",OrdinaryExitFixture());bool prior=ExitSelect.interactable;
            yield return Click("autoButton");Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.True);
            Assert.That(Session.State.coins,Is.EqualTo(45));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);int firstFrame=Time.frameCount;
            string committed=State;var files=Files();yield return Back();Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.False);
            Assert.That(Time.frameCount,Is.GreaterThan(firstFrame));yield return ExitCapture("ordinary-waiting");ExitReadiness(true);Unchanged(committed,files);
            // Disabled Submit is safe; never submit/click any ready Exit.
            Assert.That(!ExitSelect.IsInteractable()&&HouseFacts("submit").busy,Is.True);
            ExecuteEvents.Execute(ExitSelect.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);Unchanged(committed,files);
            var canceled=ExitSelect.onClick;yield return Click("closeButton");Assert.That(ExitSelect.interactable,Is.EqualTo(prior));RetiredExitIsInert(canceled,committed,files);
            yield return Back();ExitReadiness(true);yield return Back();Unchanged(committed,files);yield return Back();ExitReadiness(true);
            Call("OnApplicationPause",true);Assert.That(ExitSelect.IsInteractable(),Is.False);yield return null;Call("OnApplicationPause",false);ExitReadiness(true);
            Assert.That(Session.State.coins,Is.EqualTo(45));Assert.That(Session.State.lastSpin.symbols,Is.EqualTo(new[]{0,0,2}));
            TestContext.WriteLine("Ordinary committed before pause: "+committed+"\nAfter checkpoint/resume: "+State);committed=State;files=Files();
            var pointer=ExitPointer(708);ExecuteEvents.Execute(ExitSelect.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            yield return HouseFinish();ExitReadiness(false);Unchanged(committed,files);
            pointer.pointerCurrentRaycast=ExitPointer(708).pointerCurrentRaycast;ExecuteEvents.Execute(ExitSelect.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            var consent=ExitSelect.GetComponents<Component>().Single(c=>c.GetType().FullName=="JunkToNeon.JtnPanelPointerConsent");
            Assert.That((bool)consent.GetType().GetMethod("AllowInvocation").Invoke(consent,null),Is.False,"Disabled-down must not authorize ready-up");
            var fresh=ExitPointer(709);ExecuteEvents.Execute(ExitSelect.gameObject,fresh,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(ExitSelect.gameObject,fresh,ExecuteEvents.pointerUpHandler);
            Assert.That((bool)consent.GetType().GetMethod("AllowInvocation").Invoke(consent,null),Is.True);consent.GetType().GetMethod("Invalidate").Invoke(consent,null); // Observe only: no final click/Submit.
            yield return ExitCapture("ordinary-ready");var retired=ExitSelect.onClick;yield return Back();Assert.That(Field<bool>("exitPanel"),Is.False);
            Assert.That(ExitSelect.interactable,Is.EqualTo(prior));RetiredExitIsInert(retired,committed,files);
            yield return Back();ExitReadiness(false);yield return Click("closeButton");Unchanged(committed,files);
            yield return ExitWorkshop();Assert.That(Session.State.coins,Is.EqualTo(45));
        }
        [UnityTest]public IEnumerator FirstRepairAutoIntroductionReleasesWaitingExitSelect()
        {
            yield return Harness("Prepare",ModelState.Fresh(1));yield return SelectRepair();yield return Click("spinButton");
            Assert.That(Session.State.coins,Is.EqualTo(5));yield return Back();ExitReadiness(true);yield return ExitCapture("repair-waiting");
            var old=ExitSelect.onClick;string committed=State;var files=Files();yield return HouseFinish();
            Assert.That(Field<bool>("exitPanel"),Is.False);Assert.That(Field<bool>("autoHintPanel"),Is.True);Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("AUTO unlocked"));
            Assert.That(ExitSelect.gameObject.activeInHierarchy,Is.False);Assert.That(ExitSelect.interactable,Is.True);RetiredExitIsInert(old,committed,files);
            yield return ExitCapture("auto-introduction");yield return Click("closeButton");Assert.That(Session.State.autoHintSeen,Is.EqualTo(1));Assert.That(Session.State.coins,Is.EqualTo(5));
            yield return ExitWorkshop();
        }
        [UnityTest]public IEnumerator ExitWaitingAndReadyFitShortPortraitWithoutQuitting()
        {
            yield return Harness("Prepare",OrdinaryExitFixture());yield return Click("spinButton");yield return Back();ExitReadiness(true);yield return ExitCapture("short-waiting");
            string committed=State;var files=Files();yield return HouseFinish();ExitReadiness(false);yield return ExitCapture("short-ready");yield return Back();Unchanged(committed,files);
        }
        [Serializable]sealed class FlightEndpoint
        {public string label;public int slot,frame;public float age,alpha;public Vector2 actual,expected,materials;}
        [Serializable]sealed class FlightView
        {public string phase,state;public int width,height,frame,visible;public bool autoHint;public FlightEndpoint energy;}
        Vector2 FlightDestination(RectTransform target)
        {
            Assert.That(target.gameObject.activeInHierarchy,Is.True);var root=Field<RectTransform>("rewardFeedbackRoot");
            return (Vector2)root.InverseTransformPoint(target.TransformPoint(target.rect.center))-root.rect.center;
        }
        void FlightEndsAt(string label,RectTransform target)
        {
            var labels=Field<TMP_Text[]>("rewardFeedbackLabels");int slot=Array.FindIndex(labels,t=>t.gameObject.activeInHierarchy&&t.text==label);
            Assert.That(slot,Is.GreaterThanOrEqualTo(0),label);Assert.That(Vector2.Distance(Field<Vector2[]>("rewardFeedbackEnds")[slot],FlightDestination(target)),Is.LessThan(.05f),label);
        }
        FlightEndpoint ObserveEnergyFlight()
        {
            var labels=Field<TMP_Text[]>("rewardFeedbackLabels");int slot=Array.FindIndex(labels,t=>t.gameObject.activeInHierarchy&&t.text=="Energy +1");
            Assert.That(slot,Is.GreaterThanOrEqualTo(0));Assert.That(labels[slot].color.a,Is.GreaterThan(0));
            var target=Field<TMP_Text>("productEnergyText");Assert.That(target.text,Does.Contain("2/30"));Fits(target);
            var materials=Field<TMP_Text>("productMaterials");Assert.That(materials.text,Does.Contain("Parts 2/200").And.Contain("Potatoes 4/200"));Fits(materials);
            var actual=Field<Vector2[]>("rewardFeedbackEnds")[slot];var expected=FlightDestination(target.rectTransform);var other=FlightDestination(materials.rectTransform);
            Assert.That(Vector2.Distance(actual,expected),Is.LessThan(.05f));Assert.That(Vector2.Distance(actual,other),Is.GreaterThan(30));
            int coinSlot=Array.FindIndex(labels,t=>t.gameObject.activeInHierarchy&&t.text=="Coins +8");
            Assert.That(coinSlot,Is.GreaterThanOrEqualTo(0));
            var captionLift=Field<Vector2[]>("rewardFeedbackEnds")[coinSlot]-Field<Vector2[]>("rewardFeedbackStarts")[coinSlot];
            Assert.That(captionLift.x,Is.Zero);
            Assert.That(captionLift.y,Is.GreaterThan(0).And.LessThan(40));
            Assert.That(Field<RectTransform>("rewardImpactTarget"),Is.SameAs(CoinWalletIcon()));
            FlightEndsAt("Parts +1",materials.rectTransform);FlightEndsAt("Potatoes +2",materials.rectTransform);
            var group=Field<RectTransform>("rewardFeedbackRoot").GetComponent<CanvasGroup>();Assert.That(group.blocksRaycasts||group.interactable,Is.False);
            Assert.That(labels.All(t=>!t.raycastTarget),Is.True);
            return new FlightEndpoint{label=labels[slot].text,slot=slot,frame=Time.frameCount,age=Field<float[]>("rewardFeedbackAges")[slot],alpha=labels[slot].color.a,actual=actual,expected=expected,materials=other};
        }
        IEnumerator FlightPicture(string phase,FlightEndpoint energy=null)
        {
            string root=Environment.GetEnvironmentVariable("JTN_FLIGHT_CAPTURE_DIRECTORY");if(string.IsNullOrEmpty(root))yield break;
            string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/reward-flight-fix-20261103/captures"));root=Path.GetFullPath(root);
            Assert.That(root.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(root);
            Assert.That(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length,Is.LessThan(4));
            string path=Path.Combine(root,phase+".png");Assert.That(File.Exists(path),Is.False);string before=State;var files=Files();
            var view=new FlightView{phase=phase,state=before,width=Screen.width,height=Screen.height,frame=Time.frameCount,visible=(int)Game.GetType().GetProperty("RewardFeedbackVisibleCount").GetValue(Game),autoHint=Field<bool>("autoHintPanel"),energy=energy};
            ScreenCapture.CaptureScreenshot(path);for(int frame=0;frame<30&&!File.Exists(path);frame++)yield return null;Assert.That(File.Exists(path),Is.True);
            File.WriteAllText(Path.ChangeExtension(path,".json"),JsonUtility.ToJson(view,true));Unchanged(before,files);
        }
        IEnumerator NormalRepairAndEnergyFlight(int height)
        {
            Assert.That(Screen.width,Is.EqualTo(1080));Assert.That(Screen.height,Is.EqualTo(height));
            yield return Harness("Prepare",ModelState.Fresh(1));Assert.That((bool)Game.GetType().GetProperty("RewardFeedbackReduced").GetValue(Game),Is.False);
            var pool=Field<TMP_Text[]>("rewardFeedbackLabels").Select(t=>t.GetInstanceID()).ToArray();Assert.That(pool.Length,Is.EqualTo(8));
            yield return SelectRepair();Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(40));
            int first=Time.frameCount;yield return Click("spinButton");Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.True);NoFlyers();
            Assert.That(Session.State.coins,Is.EqualTo(5));Assert.That(Session.State.lastSpin.symbols,Is.EqualTo(new[]{0,0,2}));
            Assert.That(Session.Arcade.LastEvent.CoinIncome,Is.EqualTo(8));Assert.That(Session.Arcade.LastEvent.ConstructionDebit,Is.EqualTo(40));
            Assert.That(new[]{Session.Expansion.Parts,Session.Expansion.Potatoes,Session.Expansion.Energy},Is.EqualTo(new[]{1,2,1}));
            // Observe the first frame after ordinary3.5s reveal, not the reduced-effects route.
            float deadline=Time.realtimeSinceStartup+7;while((bool)Game.GetType().GetProperty("Busy").GetValue(Game)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.False);Assert.That(Time.frameCount,Is.GreaterThan(first));
            Assert.That(Field<bool>("autoHintPanel"),Is.True);Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("AUTO unlocked"));
            Assert.That(Session.State.autoHintSeen,Is.Zero);Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Contains("house.repair"),Is.True);
            Assert.That(Field<object>("rewardFeedbackPending"),Is.Null);NoFlyers();Fits(Field<TMP_Text>("panelTitle"));Fits(Field<TMP_Text>("panelBody"));Fits(Field<Button>("closeButton").GetComponentInChildren<TMP_Text>());
            string before=State;var files=Files();for(int frame=0;frame<8;frame++){yield return null;NoFlyers();Unchanged(before,files);}
            yield return FlightPicture("auto-introduction");Call("Render",null,null);Call("RevealRewardFeedback",Session.State);NoFlyers();Unchanged(before,files);
            var expected=codec.Decode(before);expected.autoHintSeen=1;expected.sequence++;expected.revision+=2;
            yield return Click("closeButton");Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Assert.That(State,Is.EqualTo(codec.Encode(expected)),"Only the existing AUTO-information acknowledgement may commit");
            NoFlyers();before=State;files=Files();Call("Render",null,null);Call("RevealRewardFeedback",Session.State);yield return null;NoFlyers();Unchanged(before,files);
            Call("Reload");yield return null;Call("RevealRewardFeedback",Session.State);NoFlyers();Unchanged(before,files);
            yield return Click("spinButton");Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.True);NoFlyers();
            deadline=Time.realtimeSinceStartup+7;while((bool)Game.GetType().GetProperty("Busy").GetValue(Game)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.False);Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            Assert.That(Session.State.lastSpin.symbols,Is.EqualTo(new[]{0,2,0}));Assert.That(Session.State.seed,Is.EqualTo(745495504u));Assert.That(Session.State.coins,Is.EqualTo(13));
            Assert.That(Session.Arcade.LastEvent.CoinIncome,Is.EqualTo(8));Assert.That(Session.Arcade.LastEvent.ConstructionDebit,Is.Zero);
            Assert.That(new[]{Session.Expansion.Parts,Session.Expansion.Potatoes,Session.Expansion.Energy},Is.EqualTo(new[]{2,4,2}));
            deadline=Time.realtimeSinceStartup+.4f;while(!Field<TMP_Text[]>("rewardFeedbackLabels").Any(t=>t.gameObject.activeInHierarchy&&t.text=="Energy +1"&&t.color.a>0)&&Time.realtimeSinceStartup<deadline)yield return null;
            var firstEnergy=ObserveEnergyFlight();yield return FlightPicture("energy-flight",firstEnergy);yield return null;var laterEnergy=ObserveEnergyFlight();
            Assert.That(laterEnergy.slot,Is.EqualTo(firstEnergy.slot));Assert.That(laterEnergy.frame,Is.GreaterThan(firstEnergy.frame));Assert.That(laterEnergy.age,Is.GreaterThan(firstEnergy.age));
            Assert.That(Field<TMP_Text[]>("rewardFeedbackLabels").Select(t=>t.GetInstanceID()),Is.EqualTo(pool));
            var method=Game.GetType().GetMethod("RewardFeedbackTarget",Hidden);var currency=method.GetParameters()[0].ParameterType;
            Assert.That(method.Invoke(Game,new[]{Enum.Parse(currency,"Diamonds")}),Is.SameAs(Field<TMP_Text>("productDiamonds").rectTransform));
            Assert.That(method.Invoke(Game,new[]{Enum.Parse(currency,"Tickets")}),Is.SameAs(Field<TMP_Text>("productEventBadge").rectTransform));
            TestContext.WriteLine("Same-flight endpoint samples: "+JsonUtility.ToJson(firstEnergy)+" then "+JsonUtility.ToJson(laterEnergy));
            TestContext.WriteLine("Fresh seed1 real first repair37+8-40=5 and1/2/1; AUTO modal suppresses consumed batch across frames, explicit hint ACK, Render and Reload. Second uncovered normal spin+8/+1/+2/+1,13Coins and2/4/2; eight-label pool and other destinations preserved. Actual private FileSnapshotStorage paths are verified by the shared harness.");
        }
        [UnityTest]public IEnumerator NormalRepairModalConsumesFlightsAndEnergyTargetsVisibleCounter()
        {yield return NormalRepairAndEnergyFlight(2340);}
        [UnityTest]public IEnumerator RewardModalAndEnergyEndpointFitShortPortrait()
        {yield return NormalRepairAndEnergyFlight(2280);}
        [UnityTearDown] public IEnumerator Restore()
        {
            if(houseOriginalProfile.HasValue){HouseProfile(houseOriginalProfile.Value);houseOriginalProfile=null;}
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            { TestContext.WriteLine("Preserved first-session private fixture: " + Folder); typeof(ReelUiLifecycleTests).GetField("directory", Hidden).SetValue(ui, null); }
            yield return ui.RestoreAndRemoveOnlyPrivateFixtures();
        }
    }
}
#endif
