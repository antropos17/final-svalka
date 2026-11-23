#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Upgrades;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace JunkToNeon.Tests.PlayMode
{
    // Existing valid prepared/historical fixtures, copied to private storage. These
    // short uGUI checks do not establish natural play, real absence or OS touch.
    public sealed class ChapterInformationUiTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui=new ReelUiLifecycleTests();
        readonly JsonStateCodec codec=new JsonStateCodec();
        bool loaded;
        object FixtureField(string name)=>typeof(ReelUiLifecycleTests).GetField(name,Hidden).GetValue(ui);
        Component Game=>(Component)FixtureField("game");
        GameSession Session=>(GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        T Field<T>(string name)=>(T)Game.GetType().GetField(name,Hidden).GetValue(Game);
        string DirectoryPath=>(string)FixtureField("directory");
        IEnumerator Harness(string name,params object[] args)=>(IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(name,Hidden).Invoke(ui,args);
        Dictionary<string,string> Files()=>Directory.GetFiles(DirectoryPath).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
        void Unchanged(string state,Dictionary<string,string> files)
        {Assert.That(codec.Encode(Session.State),Is.EqualTo(state));Assert.That(Files(),Is.EquivalentTo(files));Assert.That(Session.RecoveryRequired,Is.False,Session.Error);}
        void FitsBody()
        {
            var text=Field<TMP_Text>("panelBody");Canvas.ForceUpdateCanvases();text.ForceMeshUpdate();
            Assert.That(text.isTextOverflowing,Is.False,text.text);Assert.That(text.textBounds.size.x,Is.LessThanOrEqualTo(text.rectTransform.rect.width+2));Assert.That(text.textBounds.size.y,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2));
        }
        IEnumerator Back()
        {
            yield return null;var adapter=Game.GetComponents<Component>().Single(c=>c.GetType().FullName=="JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)adapter.GetType().GetMethod("RequestBack").Invoke(adapter,null),Is.True);
        }
        IEnumerator Input(string relative)
        {
            string source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks",relative));
            byte[] bytes=File.ReadAllBytes(source);long modified=File.GetLastWriteTimeUtc(source).Ticks;
            var original=codec.Decode(File.ReadAllText(source));GameLogic.Validate(original);
            // Preserve retained receipt/time fields; do not use Prepare's fixed1000 rewrite.
            Environment.SetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS",Math.Max(1000,original.lastSettledAt).ToString(System.Globalization.CultureInfo.InvariantCulture));
            File.Copy(source,Path.Combine(DirectoryPath,"jtn.save.v1.json"),true);
            if(loaded)yield return Harness("ReenterScene");else {yield return Harness("LoadGame",false);loaded=true;}
            Assert.That(Session.State.coins,Is.EqualTo(original.coins),"Fixture opening must not add income or mutate the wallet");
            Assert.That(File.ReadAllBytes(source),Is.EqualTo(bytes));Assert.That(File.GetLastWriteTimeUtc(source).Ticks,Is.EqualTo(modified));
            Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
        }
        [SetUp]public void Isolate()
        {
            loaded=false;ui.IsolateSave();Assert.That(Path.GetPathRoot(Path.GetFullPath(DirectoryPath)).Replace('\\','/'),Is.EqualTo("X:/").IgnoreCase);
        }
        void CheckHud(int count,float? fraction)
        {
            Assert.That(Field<TMP_Text>("chapterBanner").text,Does.EndWith($" · {count}/24"));
            var track=Field<RectTransform>("goalProgressTrack");var fill=Field<RectTransform>("goalProgressFill");
            Assert.That(track.gameObject.activeSelf,Is.EqualTo(fraction.HasValue));
            Assert.That(track.GetComponent<UnityEngine.UI.Image>().raycastTarget,Is.False);
            Assert.That(fill.GetComponent<UnityEngine.UI.Image>().raycastTarget,Is.False);
            if(fraction.HasValue)
            {
                Canvas.ForceUpdateCanvases();
                Assert.That(fill.anchorMax.x,Is.EqualTo(fraction.Value).Within(.00001f));
                Assert.That(fill.rect.width,Is.EqualTo(track.rect.width*fraction.Value).Within(.01f));
                Assert.That(track.rect.height,Is.GreaterThan(0));
            }
            foreach(var name in new[]{"chapterBanner","stageLabel"})
            {
                var text=Field<TMP_Text>(name);text.ForceMeshUpdate();
                Assert.That(text.isTextOverflowing,Is.False,text.text);
                Assert.That(text.textBounds.size.x,Is.LessThanOrEqualTo(text.rectTransform.rect.width+2));
            }
        }
        void RefreshHudWithoutWrites(int count,float? fraction)
        {
            string before=codec.Encode(Session.State);var files=Files();
            Game.GetType().GetMethod("Render",Hidden).Invoke(Game,new object[]{null,null});
            CheckHud(count,fraction);Unchanged(before,files);
        }
        IEnumerator PreparedHud(ModelState state)
        {
            state.lastSettledAt=1000;GameLogic.Validate(state);
            File.WriteAllText(Path.Combine(DirectoryPath,"jtn.save.v1.json"),codec.Encode(state));
            if(loaded)yield return Harness("ReenterScene");else {yield return Harness("LoadGame",false);loaded=true;}
        }
        [UnityTest]
        public IEnumerator HudProgressFollowsExplicitGoalAndHidesAfterThePaidSpin()
        {
            yield return PreparedHud(ModelState.Fresh(1));CheckHud(0,null);
            yield return Harness("OpenFamily",0);yield return Harness("PointerClick","selectButton",-1);
            Assert.That(Session.State.coins,Is.EqualTo(37));RefreshHudWithoutWrites(0,37f/40);
            yield return Harness("PointerClick","spinButton",-1);yield return Harness("FinishPresentation");
            Assert.That(Session.State.coins,Is.EqualTo(5));Assert.That(Session.State.houseStage,Is.EqualTo(1));
            Assert.That(Session.Chapter.SelectedProjectId,Is.Null);RefreshHudWithoutWrites(1,null);
            // The real seed1 pair credits8 and pays exactly40; no successor is selected.
            Assert.That(Session.State.lastSpin.symbols,Is.EqualTo(new[]{0,0,2}));
        }
        [UnityTest]
        public IEnumerator HudProgressKeepsSavedPricesAndCountsFreeEntryAsTwelve()
        {
            yield return Input("EraMigration/Fixtures/r7/owned-selected-house.json");
            Assert.That(Session.State.coins,Is.EqualTo(340));RefreshHudWithoutWrites(4,340f/600);
            yield return Input("Milestones/fixtures/v2-selected540.json");
            Assert.That(Session.State.coins,Is.EqualTo(535));RefreshHudWithoutWrites(1,535f/540);
            // An affordable saved target remains unpaid until an explicit economic action.
            var affordable=Session.State;affordable.coins=600;
            yield return PreparedHud(affordable);RefreshHudWithoutWrites(1,1);
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("house.improve"));
            var empty=Session.State;empty.coins=0;
            yield return PreparedHud(empty);RefreshHudWithoutWrites(1,0);
            var current=Session.State;current.coins=470;
            yield return PreparedHud(current);yield return Harness("OpenFamily",0);
            yield return Harness("PointerClick","selectButton",-1);
            Assert.That(Session.State.coins,Is.EqualTo(470));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(480));RefreshHudWithoutWrites(1,470f/480);
            var mapFixtures=new SectionsMapUiTests();
            var stageFixture=typeof(SectionsMapUiTests).GetMethod("StageFixture",Hidden);
            yield return PreparedHud((ModelState)stageFixture.Invoke(mapFixtures,new object[]{EraIds.Junkyard,2}));
            RefreshHudWithoutWrites(12,null);
            yield return PreparedHud((ModelState)stageFixture.Invoke(mapFixtures,new object[]{EraIds.WorkshopYard,3}));
            RefreshHudWithoutWrites(12,null);
            yield return PreparedHud((ModelState)stageFixture.Invoke(mapFixtures,new object[]{EraIds.WorkshopYard,5}));
            RefreshHudWithoutWrites(24,null);
            // Loading an earlier confirmed file lowers the count and restores its own quote.
            yield return Input("Milestones/fixtures/v2-selected540.json");RefreshHudWithoutWrites(1,535f/540);
        }
        [UnityTest]
        public IEnumerator CompletedConstructionInspectionReportsEarnedBonusesAcrossBothErasWithoutWrites()
        {
            string[][] expected={
                new[]{"AUTO unlocked","+1 Coin per spin; +1 Coin per Box symbol","+2 Coins per Trash symbol","+1 Coin per Pigeon symbol; +3 Coins per delivery (3 Pigeons accumulated; carry 0–2)","+2 Coins per Box symbol","+2 Coins per Potato symbol"},
                new[]{"AUTO unlocked; +2 Coins per Potato symbol","+1 Coin per spin; +3 Coins per Box symbol","+4 Coins per Trash symbol","+3 Coins per Pigeon symbol; +3 Coins per delivery (3 Pigeons accumulated; carry 0–2)","+4 Coins per Box symbol","+4 Coins per Potato symbol"}
            };
            string[][] stories={
                new[]{"More room at home.","Work runs smoother.","Less junk, more use.","Pigeon expands.","The Cart can do more.","More reliable power."},
                new[]{"Ready for everyday life.","Workshop knows its work.","Order becomes a habit.","The main address is ready.","A route for every find.","Power has its place."}
            };
            string[] advice={"Junkyard complete. Enter Workshop Yard for free before choosing your next project.","Chapter complete. Skills and orders are optional."};
            var fixtures=new SectionsMapUiTests();var stageFixture=typeof(SectionsMapUiTests).GetMethod("StageFixture",Hidden);
            for(int era=0;era<2;era++)
            {
                yield return PreparedHud((ModelState)stageFixture.Invoke(fixtures,new object[]{era==0?EraIds.Junkyard:EraIds.WorkshopYard,era==0?2:5}));
                Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(era==0?12:24));
                Assert.That(ChapterCatalog.Rules.CanUnlockWorkshopYard(Session.Chapter.Progress),Is.EqualTo(era==0));
                Assert.That(ChapterCatalog.Rules.IsChapterComplete(Session.Chapter.Progress),Is.EqualTo(era==1));
                Assert.That(Session.Chapter.TransitionPending,Is.False);Assert.That(ChapterCatalog.Rules.AvailableUpgrades(Session.Chapter.Progress),Is.Empty);
                for(int family=0;family<6;family++)
                    yield return InspectCompletedConstruction(family,expected[era][family],stories[era][family],era==1,advice[era]);
            }
            // Earlier confirmed ownership removes the later-era contribution again.
            yield return PreparedHud((ModelState)stageFixture.Invoke(fixtures,new object[]{EraIds.Junkyard,2}));
            yield return InspectCompletedConstruction(2,"+2 Coins per Trash symbol",stories[0][2],false,"Junkyard complete. Enter Workshop Yard for free before choosing your next project.");
            // Small public-Core setup only: a paid House II while the other Junkyard families remain unbuilt.
            var start=ModelState.Fresh(1);start.coins=1000;start.lastSettledAt=1000;
            var setup=GameSession.Open(new GameStore(new CadenceMemory{Raw=codec.Encode(start)},codec),1,initializeJourney:true);
            setup.SelectChapterProject("house.repair",setup.State.sequence+1,1000);setup.AcknowledgeAutoHint(setup.State.sequence+1,1000);setup.SelectChapterProject("house.improve",setup.State.sequence+1,1000);
            yield return PreparedHud(setup.State);Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(2));Assert.That(ChapterCatalog.Rules.CanUnlockWorkshopYard(Session.Chapter.Progress),Is.False);
            yield return InspectCompletedConstruction(0,"AUTO unlocked","More room at home.",false,"Choose the next goal yourself.");
        }
        [UnityTest]public IEnumerator EraBoundaryLongestOwnedDetailFitsShortPortraitReadOnly()
        {
            Assert.That(Screen.width,Is.EqualTo(1080));Assert.That(Screen.height,Is.EqualTo(2280));
            Assert.That(int.TryParse(Environment.GetEnvironmentVariable("JTN_ERA_LONGEST_FAMILY"),out int family)&& (family==1||family==3),Is.True,"Choose the longer measured regular Workshop/Pigeon detail");
            var fixture=typeof(SectionsMapUiTests).GetMethod("StageFixture",Hidden);yield return PreparedHud((ModelState)fixture.Invoke(new SectionsMapUiTests(),new object[]{EraIds.Junkyard,2}));
            Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(12));Assert.That(ChapterCatalog.Rules.CanUnlockWorkshopYard(Session.Chapter.Progress),Is.True);Assert.That(Session.Chapter.TransitionPending,Is.False);
            string benefit=family==1?"+1 Coin per spin; +1 Coin per Box symbol":"+1 Coin per Pigeon symbol; +3 Coins per delivery (3 Pigeons accumulated; carry 0–2)";
            string story=family==1?"Work runs smoother.":"Pigeon expands.";
            yield return InspectCompletedConstruction(family,benefit,story,false,"Junkyard complete. Enter Workshop Yard for free before choosing your next project.");
        }
        IEnumerator InspectCompletedConstruction(int family,string expected,string story,bool complete,string advice)
        {
            string before=codec.Encode(Session.State);var files=Files();
            yield return VocabularyFamily(family);
            string body=Field<TMP_Text>("panelBody").text;
            Assert.That(body,Does.Contain("stage 2/2").And.Contain(story).And.Contain("Building benefits: "+expected+". Skills excluded.").And.Not.Contain("Price:"));
            Assert.That(body.Contains("Chapter complete."),Is.EqualTo(complete));
            Assert.That(body,Does.EndWith(advice));
            Assert.That(body.Contains("Offline:"),Is.EqualTo(family==1));
            if(family==1)Assert.That(body,Does.Contain("1 Coin/full min., up to 480 per absence"));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);
            Field<UnityEngine.UI.Button>("selectButton").onClick.Invoke();FitsBody();Unchanged(before,files);
            if(family==1||family==3)yield return VocabularyPicture("owned-"+family+"-"+Session.Chapter.Progress.EraId,"panelBody");
            var measured=Field<TMP_Text>("panelBody");TestContext.WriteLine($"Owned detail measurement: era={Session.Chapter.Progress.EraId}; family={family}; font={measured.fontSize}; height={measured.textBounds.size.y}; available={measured.rectTransform.rect.height}; advice={advice}");
            if(Environment.GetEnvironmentVariable("JTN_ERA_CAPTURE_DIRECTORY")!=null&&advice=="Junkyard complete. Enter Workshop Yard for free before choosing your next project."&&(family==1||family==3))yield return LockedSkillPicture("era-family-"+family);
            yield return Back();Unchanged(before,files);
        }
        [UnityTest]
        public IEnumerator CompletedConstructionInspectionExcludesOwnedSkillsAndPreservesTheCurrentGoal()
        {
            var state=codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/Milestones/fixtures/late23-skills6.json"))));
            // A prepared wallet, not a claimed paid-play traversal. The next real
            // UI selection must retain the final700 goal without purchasing it.
            state.coins=699;
            yield return PreparedHud(state);Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(6));
            Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(23));Assert.That(ChapterCatalog.Rules.CanUnlockWorkshopYard(Session.Chapter.Progress),Is.False);Assert.That(ChapterCatalog.Rules.IsChapterComplete(Session.Chapter.Progress),Is.False);
            yield return VocabularyFamily(5);
            yield return Harness("PointerClick","selectButton",-1);
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("power-source.workshop-yard.upgrade-2"));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(700));Assert.That(Session.State.coins,Is.EqualTo(699));
            // Three independently literal totals remain construction-only despite six owned skills.
            yield return InspectCompletedConstruction(1,"+1 Coin per spin; +3 Coins per Box symbol","Workshop knows its work.",false,"Choose the next goal yourself.");
            yield return InspectCompletedConstruction(2,"+4 Coins per Trash symbol","Order becomes a habit.",false,"Choose the next goal yourself.");
            yield return InspectCompletedConstruction(3,"+3 Coins per Pigeon symbol; +3 Coins per delivery (3 Pigeons accumulated; carry 0–2)","The main address is ready.",false,"Choose the next goal yourself.");
        }
        [UnityTest]
        public IEnumerator SelectedRepairGuidanceMovesToSpinningAndReturnsToChoiceAfterClear()
        {
            // An empty owned directory creates genuine Fresh guidance; encoding a
            // bare ModelState first would correctly migrate it as Existing instead.
            yield return Harness("LoadGame",false);loaded=true;
            yield return Harness("OpenFamily",0);yield return Harness("PointerClick","selectButton",-1);
            Assert.That(Session.State.coins,Is.EqualTo(37));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("house.repair"));
            Assert.That(Session.Guidance.Suggestion.HintId,Is.EqualTo(JunkToNeon.Guidance.GuideIds.ChooseProject));
            Assert.That(Field<TMP_Text>("heading").text,Is.EqualTo("Goal selected. Press SPIN."));
            string before=codec.Encode(Session.State);var files=Files();
            yield return Harness("PointerClick","guideButton",-1);
            Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("Goal selected: House.").And.Contain("Saved price: 40 Coins.").And.Contain("Wallet: 37; still needed: 3.").And.Contain("Press SPIN").And.Not.Contain("Choose a project explicitly"));
            FitsBody();Unchanged(before,files);yield return Back();Unchanged(before,files);
            // A real explicit clear restores the earlier next action without acknowledging the guide.
            // All Sections is reached through Profile; its legacy root button is hidden by the product navigation.
            yield return Harness("PointerClick","productAvatar",-1);yield return Harness("PointerClick","productCards",2);yield return Harness("PointerClick","sectionButtons",5);
            yield return Harness("PointerClick","chapterPrevious",-1);yield return Harness("PointerClick","selectButton",-1);
            Assert.That(Session.State.coins,Is.EqualTo(37));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            Assert.That(Field<TMP_Text>("heading").text,Is.EqualTo("Choose one goal for your Coins."));
            before=codec.Encode(Session.State);files=Files();yield return Harness("PointerClick","guideButton",-1);
            Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("Choose a project explicitly.").And.Not.Contain("Goal selected"));
            FitsBody();Unchanged(before,files);yield return Back();Unchanged(before,files);
            yield return Harness("ReenterScene");
            Assert.That(Session.State.coins,Is.EqualTo(37));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            Assert.That(Field<TMP_Text>("heading").text,Is.EqualTo("Choose one goal for your Coins."));
        }
        [UnityTest]
        public IEnumerator InitialWorkshopExplainsExistingCappedOfflineIncomeWithoutPromisingTierMultipliers()
        {
            yield return Input("Milestones/fixtures/v2-selected540.json");
            Assert.That(Session.State.coins,Is.EqualTo(535));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
            string before=codec.Encode(Session.State);var files=Files();
            yield return VocabularyFamily(1);string body=Field<TMP_Text>("panelBody").text;
            Assert.That(body,Does.Contain("Price: 120 Coins.").And.Contain("+1 Coin per future spin.").And.Contain("Offline: +1 Coin/full min.").And.Contain("up to 480 per absence (8h)").And.Contain("Credited on return."));
            FitsBody();Unchanged(before,files);yield return Back();Unchanged(before,files);
            yield return VocabularyFamily(2);body=Field<TMP_Text>("panelBody").text;
            Assert.That(body,Does.Contain("Price: 180 Coins.").And.Contain("per Trash").And.Not.Contain("Offline:"));FitsBody();yield return Back();Unchanged(before,files);
            // Existing r7 file already owns WorkshopI and retains an unpurchased600 house goal.
            yield return Input("EraMigration/Fixtures/r7/owned-selected-house.json");
            Assert.That(Session.State.coins,Is.EqualTo(340));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(600));
            before=codec.Encode(Session.State);files=Files();yield return VocabularyFamily(1);body=Field<TMP_Text>("panelBody").text;
            Assert.That(body,Does.Contain("Price: 260 Coins.").And.Contain("per Box").And.Not.Contain("Offline:"));
            FitsBody();Unchanged(before,files);yield return Back();Unchanged(before,files);
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("house.improve"));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(600));
        }
        PointerEventData Hold(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();Assert.That(button.isActiveAndEnabled&&button.IsInteractable(),Is.True);
            var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();
            var data=new PointerEventData(EventSystem.current){pointerId=71,button=PointerEventData.InputButton.Left,
                position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center)),
                eligibleForClick=true,pointerClick=button.gameObject};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Assert.That(hits.Count,Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(button.gameObject));data.pointerCurrentRaycast=hits[0];
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);return data;
        }
        [UnityTest]
        public IEnumerator OwnedSkillsUseRetainedPaymentWhileUnownedOffersKeepCurrentPriceWithoutRepayment()
        {
            yield return Input("EraMigration/R8/Fixtures/canonical-all-skills-owned.json");
            Assert.That(Session.State.coins,Is.EqualTo(8280));
            Assert.That(Session.Chapter.Skills.Receipts.Single(r=>r.Request.NodeId==SkillIds.Delivery2).FrozenPrice,Is.EqualTo(540));
            string before=codec.Encode(Session.State);var files=Files();
            // A legally actionable house preview is held, then dismissed without purchase.
            yield return Harness("OpenFamily",0);yield return null;var held=Field<UnityEngine.UI.Button>("selectButton");var pointer=Hold(held);var queued=held.onClick;
            Unchanged(before,files);yield return Back();Unchanged(before,files);
            yield return Harness("OpenSkill",2,2);string body=Field<TMP_Text>("panelBody").text;
            Assert.That(body,Does.Contain("Previously paid: 540 Coins.").And.Contain("This node is already owned").And.Not.Contain("Price: 20."));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton"),Is.SameAs(held));Assert.That(held.gameObject.activeSelf,Is.False);FitsBody();
            ExecuteEvents.Execute(held.gameObject,pointer,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(held.gameObject,pointer,ExecuteEvents.pointerClickHandler);queued.Invoke();
            Unchanged(before,files);yield return Back();Unchanged(before,files);
            // Actual valid r7 purchase provenance establishes known120 even though migration is Legacy=true.
            yield return Input("EraMigration/Fixtures/r7/owned-selected-house.json");
            var migrated=Session.Chapter.Skills.Receipts.Single(r=>r.Request.NodeId==SkillIds.Universal1);Assert.That(migrated.Legacy,Is.True);Assert.That(migrated.FrozenPrice,Is.EqualTo(120));
            before=codec.Encode(Session.State);files=Files();yield return Harness("OpenSkill",0,1);body=Field<TMP_Text>("panelBody").text;
            Assert.That(body,Does.Contain("Previously paid: 120 Coins.").And.Contain("This node is already owned").And.Not.Contain("unknown"));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);Field<UnityEngine.UI.Button>("selectButton").onClick.Invoke();FitsBody();Unchanged(before,files);yield return Back();Unchanged(before,files);
            // Accepted prepared ready-no-skills checkpoint: current deliveryII20 is unowned/locked, not paid history.
            yield return Input("EraMigration/R8/Fixtures/ready-no-skills.json");
            Assert.That(Session.State.coins,Is.EqualTo(10000));Assert.That(Session.Chapter.Skills.Owns(SkillIds.Delivery2),Is.False);
            before=codec.Encode(Session.State);files=Files();yield return Harness("OpenSkill",2,2);body=Field<TMP_Text>("panelBody").text;
            Assert.That(body,Does.Contain("Price: 20 Coins.").And.Not.Contain("Previously paid:").And.Contain("Requires tier I"));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);Field<UnityEngine.UI.Button>("selectButton").onClick.Invoke();FitsBody();Unchanged(before,files);yield return Back();Unchanged(before,files);
            TestContext.WriteLine("Held uGUI down/dismiss/release and queued callbacks only; actual installed InputSystem consent is qualified separately. No purchase, wallet change, new history or impossible unknown-legacy fixture.");
        }
        IEnumerator VisibleSkill(int branch,int tier)
        {
            int frame=Time.frameCount;
            yield return Harness("PointerClick","productTabs",4);
            yield return Harness("PointerClick","productCards",3);
            yield return Harness("PointerClick","navigationCards",branch*2+tier-1);
            Assert.That(Time.frameCount,Is.GreaterThan(frame));
            Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo($"Skill · {new[]{"Coins","recycling","delivery"}[branch]} {tier}"));
        }
        [Serializable]sealed class LockedSkillMeasure
        {public string name,text;public float font,min,width,height,boundsWidth,boundsHeight;}
        [Serializable]sealed class LockedSkillPortrait
        {public string name;public int width,height,frame;public LockedSkillMeasure[] texts;}
        IEnumerator LockedSkillPicture(string name)
        {
            FitsBody();
            var targets=new[]{Field<TMP_Text>("panelTitle"),Field<TMP_Text>("panelBody"),Field<UnityEngine.UI.Button>("closeButton").GetComponentInChildren<TMP_Text>()};
            var facts=new List<LockedSkillMeasure>();
            foreach(var text in targets)
            {
                text.ForceMeshUpdate();Assert.That(text.isTextOverflowing,Is.False,text.text);
                Assert.That(text.textBounds.size.x,Is.LessThanOrEqualTo(text.rectTransform.rect.width+2));
                Assert.That(text.textBounds.size.y,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2));
                Assert.That(text.fontSize,Is.GreaterThanOrEqualTo(text.fontSizeMin));
                facts.Add(new LockedSkillMeasure{name=text.name,text=text.text,font=text.fontSize,min=text.fontSizeMin,width=text.rectTransform.rect.width,height=text.rectTransform.rect.height,boundsWidth=text.textBounds.size.x,boundsHeight=text.textBounds.size.y});
            }
            string era=Environment.GetEnvironmentVariable("JTN_ERA_CAPTURE_DIRECTORY");string upgrade=Environment.GetEnvironmentVariable("JTN_UPGRADE_CAPTURE_DIRECTORY");string folder=era??upgrade??Environment.GetEnvironmentVariable("JTN_LOCKED_SKILL_CAPTURE_DIRECTORY");if(string.IsNullOrEmpty(folder))yield break;
            string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,era!=null?"../../.work/era-completion-guidance-20261103/captures":upgrade==null?"../../.work/locked-skill-preview-20261103/captures":"../../.work/upgrade-preview-clarity-20261103/captures"));folder=Path.GetFullPath(folder);
            Assert.That(folder.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Directory.CreateDirectory(folder);Assert.That(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length,Is.LessThan(era!=null?3:upgrade==null?7:16));
            string state=codec.Encode(Session.State);var files=Files();string path=Path.Combine(folder,name+".png");Assert.That(File.Exists(path),Is.False);
            ScreenCapture.CaptureScreenshot(path);for(int i=0;i<30&&!File.Exists(path);i++)yield return null;Assert.That(File.Exists(path),Is.True);
            Unchanged(state,files);File.WriteAllText(Path.ChangeExtension(path,".json"),JsonUtility.ToJson(new LockedSkillPortrait{name=name,width=Screen.width,height=Screen.height,frame=Time.frameCount,texts=facts.ToArray()},true));
            File.WriteAllText(Path.ChangeExtension(path,".state.json"),state);
        }
        IEnumerator MissingTierOne(int branch,string capture)
        {
            string before=codec.Encode(Session.State);var files=Files();
            var node=JunkToNeon.Upgrades.SkillTree.Nodes[branch*2+1];
            Assert.That(Session.Chapter.Skills.Owns(node.RequiredSkillId),Is.False);Assert.That(Session.Chapter.Skills.Owns(node.Id),Is.False);
            var q=ChapterEconomy.QuoteSkill(Session.State,node.Id);Assert.That(q.Status,Is.EqualTo(OfferStatus.Locked));
            Assert.That(q.Node.Price,Is.EqualTo(new long[]{160,90,20}[branch]));
            yield return VisibleSkill(branch,2);string body=Field<TMP_Text>("panelBody").text;
            Assert.That(body,Does.StartWith($"This skill adds +1 Coin {new[]{"per spin","per Trash","per delivery (3 Pigeons)"}[branch]} after purchase. Odds unchanged."));
            Assert.That(body,Does.Contain($"Price: {q.Node.Price} Coins.").And.Contain("Requires tier I, Workshop Yard").And.Contain("Optional").And.Not.Contain("→"));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);
            Field<UnityEngine.UI.Button>("selectButton").onClick.Invoke();Unchanged(before,files);
            yield return LockedSkillPicture(capture);yield return Back();Unchanged(before,files);
            yield return Harness("PointerClick","productTabs",1);Unchanged(before,files);
        }
        [UnityTest]public IEnumerator LockedTierTwoExplainsOnePurchasedNodeWhileControlsRetainTheirFacts()
        {
            yield return Input("EraMigration/R8/Fixtures/ready-no-skills.json");
            Assert.That(Session.State.coins,Is.EqualTo(10000));Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(0));
            var context=ChapterEconomy.Context(Session.Chapter);Assert.That(context.Era,Is.EqualTo(2));
            Assert.That(new[]{context.HouseStage,context.RecyclerLevel,context.PigeonLoftLevel},Is.EqualTo(new[]{4,4,4}));
            for(int branch=0;branch<3;branch++)yield return MissingTierOne(branch,"locked-"+branch);
            yield return Input("EraMigration/R8/Fixtures/owned-universal-1.json");
            Assert.That(Session.State.coins,Is.EqualTo(9880));Assert.That(Session.Chapter.Skills.Owns(SkillIds.Universal1),Is.True);
            Assert.That(Session.Chapter.Skills.Owns(SkillIds.Universal2),Is.False);
            Assert.That(ChapterEconomy.QuoteSkill(Session.State,SkillIds.Universal2).Status,Is.EqualTo(OfferStatus.Available));
            string before=codec.Encode(Session.State);var files=Files();yield return VisibleSkill(0,2);
            Assert.That(Field<TMP_Text>("panelBody").text,Does.StartWith("Branch bonus: +1 → +2 Coins per spin. Odds unchanged.").And.Contain("Price: 160 Coins."));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.True);
            yield return LockedSkillPicture("prior-owned");yield return Back();Unchanged(before,files);
            yield return Input("EraMigration/R8/Fixtures/owned-universal-2.json");
            Assert.That(Session.State.coins,Is.EqualTo(9520));Assert.That(Session.Chapter.Skills.Receipts.Single(x=>x.Request.NodeId==SkillIds.Universal2).FrozenPrice,Is.EqualTo(360));
            before=codec.Encode(Session.State);files=Files();yield return VisibleSkill(0,2);
            Assert.That(Field<TMP_Text>("panelBody").text,Does.StartWith("Branch bonus: +2 → +2 Coins per spin. Odds unchanged.").And.Contain("Previously paid: 360 Coins.").And.Contain("already owned").And.Not.Contain("after purchase"));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);
            yield return LockedSkillPicture("owned-historical");yield return Back();Unchanged(before,files);
            yield return Input("Milestones/fixtures/v2-selected540.json");
            Assert.That(Session.State.coins,Is.EqualTo(535));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("house.improve"));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
            yield return MissingTierOne(0,"selected540");Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
            before=codec.Encode(Session.State);files=Files();yield return VisibleSkill(0,1);Assert.That(Field<TMP_Text>("panelBody").text,Does.StartWith("Branch bonus: +0 → +1 Coins per spin.").And.Not.Contain("This skill adds"));yield return Back();Unchanged(before,files);
            yield return Input("EraMigration/Fixtures/r7/owned-selected-house.json");
            Assert.That(Session.Chapter.Skills.Owns(SkillIds.Universal1),Is.True);Assert.That(Session.Chapter.Skills.Owns(SkillIds.Universal2),Is.False);
            Assert.That(ChapterEconomy.QuoteSkill(Session.State,SkillIds.Universal2).Status,Is.EqualTo(OfferStatus.Locked));
            before=codec.Encode(Session.State);files=Files();yield return VisibleSkill(0,2);
            Assert.That(Field<TMP_Text>("panelBody").text,Does.StartWith("Branch bonus: +1 → +2 Coins per spin.").And.Not.Contain("This skill adds"));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);FitsBody();yield return Back();Unchanged(before,files);
            TestContext.WriteLine("Six private checkpoint inspections, visible Upgrades/Skills/node pointers, no purchase/Spin. Whole encoded state and private bytes/timestamps exact across navigation; fixture load/clock setup outside that interval.");
        }
        [UnityTest]public IEnumerator LockedDeliveryTierTwoFitsShortPortraitReadOnly()
        {yield return Input("EraMigration/R8/Fixtures/ready-no-skills.json");yield return MissingTierOne(2,"delivery-short");}
        sealed class CadenceMemory:ISnapshotStorage
        {public string Raw;public string ReadPrimary()=>Raw;public string ReadBackup()=>null;public void Write(string raw){Raw=raw;}}
        GameSession CadencePrepared(int spin)
        {
            // Earned public-command checkpoint from the qualified seed1 study;
            // subsequent commands prepare boundaries, not a new pacing screen.
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/unity/decision-cadence-20261102/screen-first.interval-start.json"));
            var game=GameSession.Open(new GameStore(new CadenceMemory{Raw=File.ReadAllText(path)},codec),1,initializeJourney:true);
            const long at=1700000268;
            game.AcknowledgeAutoHint(game.State.sequence+1,at);
            game.SelectChapterProject("power-source.build",game.State.sequence+1,at);
            for(int i=68;i<=spin;i++)
            {
                game.ExecuteChapterSpin(game.State.sequence+1,at,manualSpin:true);
                if(i==84&&spin>84)game.BuySkill(ChapterEconomy.QuoteSkill(game.State,JunkToNeon.Upgrades.SkillTree.RecyclingTier1).Request,at);
            }
            Assert.That(game.RecoveryRequired,Is.False);Assert.That(game.Expansion.SpeedId,Is.EqualTo("speed.normal"));return game;
        }
        IEnumerator CadenceLoad(ModelState state)
        {
            Environment.SetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS",state.lastSettledAt.ToString(System.Globalization.CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(DirectoryPath,"jtn.save.v1.json"),codec.Encode(state));
            if(loaded)Game.GetType().GetMethod("Reload",Hidden).Invoke(Game,null);
            else {yield return Harness("LoadGame",false);loaded=true;}
            yield return null;Assert.That(codec.Encode(Session.State),Is.EqualTo(codec.Encode(state)));
            Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
        }
        void CueFacts(bool skill,bool power)
        {
            Assert.That(Field<RectTransform>("productRecyclingCue").gameObject.activeInHierarchy,Is.EqualTo(skill));
            Assert.That(Field<RectTransform>("productPowerCue").gameObject.activeInHierarchy,Is.EqualTo(power));
            foreach(string name in new[]{"productRecyclingCue","productPowerCue"})
                foreach(var graphic in Field<RectTransform>(name).GetComponentsInChildren<UnityEngine.UI.Graphic>(true))Assert.That(graphic.raycastTarget,Is.False);
        }
        [Serializable]sealed class CadenceMeasure
        {public string name,text;public float font,width,height,boundsWidth,boundsHeight;}
        [Serializable]sealed class CadencePortrait
        {public string name;public int frame,width,height;public bool stateExact;public CadenceMeasure[] texts;}
        IEnumerator CadenceCapture(string name)
        {
            string root=Environment.GetEnvironmentVariable("JTN_CADENCE_CAPTURE_DIRECTORY");if(string.IsNullOrEmpty(root))yield break;
            string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/cadence-cues-20261102/portraits"));root=Path.GetFullPath(root);
            Assert.That(root.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(root);
            if(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length>=12)
            {TestContext.WriteLine("Cadence capture cap reached; preserve prior matching portrait evidence.");yield break;}
            string state=codec.Encode(Session.State);var files=Files();Canvas.ForceUpdateCanvases();
            var texts=new List<CadenceMeasure>();
            var targets=new[]{Field<RectTransform>("productRecyclingCue"),Field<RectTransform>("productPowerCue")}.SelectMany(t=>t.GetComponentsInChildren<TMP_Text>()).ToList();
            var card=Field<UnityEngine.UI.Button[]>("productCards")[3];if(card.isActiveAndEnabled)targets.Add(card.GetComponentInChildren<TMP_Text>());
            foreach(var text in targets)
            {text.ForceMeshUpdate();Assert.That(text.isTextOverflowing,Is.False,text.text);Assert.That(text.textBounds.size.x,Is.LessThanOrEqualTo(text.rectTransform.rect.width+2));Assert.That(text.textBounds.size.y,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2));texts.Add(new CadenceMeasure{name=text.name,text=text.text,font=text.fontSize,width=text.rectTransform.rect.width,height=text.rectTransform.rect.height,boundsWidth=text.textBounds.size.x,boundsHeight=text.textBounds.size.y});}
            string path=Path.Combine(root,name+".png");Assert.That(File.Exists(path),Is.False,"Preserve first capture");ScreenCapture.CaptureScreenshot(path);
            for(int i=0;i<30&&!File.Exists(path);i++)yield return null;
            Assert.That(File.Exists(path),Is.True);Unchanged(state,files);
            File.WriteAllText(Path.ChangeExtension(path,".json"),JsonUtility.ToJson(new CadencePortrait{name=name,frame=Time.frameCount,width=Screen.width,height=Screen.height,stateExact=true,texts=texts.ToArray()},true));
        }
        IEnumerator CadenceInspectSkill()
        {
            string before=codec.Encode(Session.State);var files=Files();
            // Touch the visible badge itself, not just the old button center.
            yield return null;Canvas.ForceUpdateCanvases();var cue=Field<RectTransform>("productRecyclingCue");
            var button=Field<UnityEngine.UI.Button[]>("productTabs")[4];var target=(RectTransform)button.transform;
            Assert.That(target.rect.width,Is.GreaterThanOrEqualTo(48));Assert.That(target.rect.height,Is.GreaterThanOrEqualTo(48));
            var corners=new Vector3[4];cue.GetWorldCorners(corners);foreach(var corner in corners){var p=target.InverseTransformPoint(corner);Assert.That(target.rect.Contains(new Vector2(p.x,p.y)),Is.True,"Badge must stay inside the unchanged Upgrades target");}
            var canvas=button.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var data=new PointerEventData(EventSystem.current){pointerId=441,button=PointerEventData.InputButton.Left,eligibleForClick=true,position=RectTransformUtility.WorldToScreenPoint(camera,cue.TransformPoint(cue.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Assert.That(hits.Count,Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(button.gameObject));data.pointerCurrentRaycast=hits[0];
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);data.pointerPress=data.pointerClick=button.gameObject;
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);yield return null;
            Assert.That(Field<UnityEngine.UI.Button[]>("productCards")[3].GetComponentInChildren<TMP_Text>().text,Does.Contain("Recycling I ready · 160 Coins"));
            yield return CadenceCapture("skill-section");
            yield return Harness("PointerClick","productCards",3);yield return Harness("PointerClick","navigationCards",2);
            FitsBody();Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("After purchase: 1 Coins").And.Contain("Goal still needs 189"));
            Unchanged(before,files);Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(190));
        }
        [UnityTest]public IEnumerator CadenceCuesFollowEarnedAffordabilityAndPowerReveal()
        {
            var early=CadencePrepared(83);Assert.That(early.State.coins,Is.EqualTo(155));yield return CadenceLoad(early.State);CueFacts(false,false);
            yield return Harness("PointerClick","spinButton",-1);Assert.That(Session.State.coins,Is.EqualTo(161));CueFacts(false,false);
            Game.GetType().GetMethod("Render",Hidden).Invoke(Game,new object[]{null,null});CueFacts(false,false);
            yield return Harness("FinishPresentation");CueFacts(true,false);yield return CadenceCapture("skill-ready");
            yield return CadenceInspectSkill();
            yield return Harness("PointerClick","selectButton",-1);yield return null;
            Assert.That(Session.State.coins,Is.EqualTo(1));Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(2));
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("power-source.build"));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(190));CueFacts(false,false);
            var paid=CadencePrepared(99);Assert.That(paid.State.coins,Is.EqualTo(172));yield return CadenceLoad(paid.State);CueFacts(false,false);
            yield return Harness("PointerClick","spinButton",-1);Assert.That(Session.State.coins,Is.EqualTo(10));Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(5));CueFacts(false,false);
            Game.GetType().GetMethod("Render",Hidden).Invoke(Game,new object[]{null,null});CueFacts(false,false);
            yield return Harness("FinishPresentation");CueFacts(false,true);yield return CadenceCapture("power-built");
            string committed=codec.Encode(Session.State);var committedFiles=Files();
            Game.GetType().GetMethod("Render",Hidden).Invoke(Game,new object[]{null,null});CueFacts(false,true);Unchanged(committed,committedFiles);
            yield return Harness("PointerClick","productMapShortcut",-1);Assert.That(Field<int>("wavePanel"),Is.EqualTo(21));CueFacts(false,false);Unchanged(committed,committedFiles);
            yield return Harness("PointerClick","productTabs",1);CueFacts(false,true);Unchanged(committed,committedFiles);
            Game.GetType().GetMethod("Reload",Hidden).Invoke(Game,null);yield return null;CueFacts(false,true);Unchanged(committed,committedFiles);
            Game.GetType().GetMethod("OnApplicationPause",Hidden).Invoke(Game,new object[]{true});CueFacts(false,false);
            Game.GetType().GetMethod("OnApplicationPause",Hidden).Invoke(Game,new object[]{false});yield return null;CueFacts(false,true);
            // Earlier confirmed ownership must remove the persistent built fact.
            yield return CadenceLoad(early.State);CueFacts(false,false);
        }
        [UnityTest]public IEnumerator CadenceCuePortraitsKeepExistingNavigationTargets()
        {
            // Geometry-only companion on the short official portrait; no UI Spin.
            yield return CadenceLoad(CadencePrepared(84).State);Assert.That(Session.State.coins,Is.EqualTo(161));CueFacts(true,false);
            yield return CadenceCapture("skill-ready");yield return CadenceInspectSkill();yield return Harness("PointerClick","productTabs",1);
            yield return CadenceLoad(CadencePrepared(100).State);CueFacts(false,true);yield return CadenceCapture("power-built");
            string before=codec.Encode(Session.State);var files=Files();yield return Harness("PointerClick","productMapShortcut",-1);Unchanged(before,files);
        }
        IEnumerator VocabularyFamily(int family)
        {
            yield return Harness("PointerClick","productTabs",0);yield return Harness("PointerClick","productCards",0);
            yield return Harness("PointerClick","navigationCards",family);
        }
        IEnumerator VocabularyPicture(string name,string field)
        {
            string state=codec.Encode(Session.State);var files=Files();var text=Field<TMP_Text>(field);
            float floor=field=="navigationContext"?12:field=="heading"?11:field=="panelTitle"?18:13;
            Canvas.ForceUpdateCanvases();text.ForceMeshUpdate();Assert.That(text.fontSize,Is.GreaterThanOrEqualTo(floor));
            Assert.That(text.isTextOverflowing,Is.False,text.text);
            Assert.That(text.textBounds.size.y,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2));
            yield return PurchaseChoicePortrait.Capture(Game,name,field);Unchanged(state,files);
        }
        IEnumerator VocabularyPower(bool payment)
        {
            // Public-command earned172/190 boundary; no new cadence screen.
            yield return CadenceLoad(CadencePrepared(99).State);CueFacts(false,false);
            Assert.That(Session.State.coins,Is.EqualTo(172));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(190));
            string state=codec.Encode(Session.State);var files=Files();
            yield return Harness("PointerClick","productTabs",0);yield return Harness("PointerClick","productCards",0);
            Assert.That(Field<UnityEngine.UI.Button[]>("navigationCards")[5].GetComponentInChildren<TMP_Text>().text,Does.Contain("Power Source · 0/2"));
            yield return VocabularyPicture("power-list","navigationContext");
            yield return Harness("PointerClick","navigationCards",5);
            Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Power Source"));
            Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("Price: 190 Coins").And.Contain("+1 Coin per Potato symbol on future spins."));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);Unchanged(state,files);
            yield return VocabularyPicture("power-preview","panelBody");yield return Harness("PointerClick","productTabs",1);
            if(payment)
            {
                long sequence=Session.State.sequence;yield return Harness("PointerClick","spinButton",-1);
                Assert.That(Session.State.coins,Is.EqualTo(10));Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));
                Assert.That(Session.Arcade.LastResourceReceipt.ConstructionDebit,Is.EqualTo(190));CueFacts(false,false);
                Game.GetType().GetMethod("Render",Hidden).Invoke(Game,new object[]{null,null});CueFacts(false,false);
                yield return Harness("FinishPresentation");
            }
            else yield return CadenceLoad(CadencePrepared(100).State);
            CueFacts(false,true);Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(5));
            Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Session.State.coins,Is.EqualTo(10));
            if(payment)
            {
                Assert.That(Field<TMP_Text>("message").text,Does.Contain("Paid 190 → Power Source 1/2"));
                Assert.That(Field<TMP_Text>("heading").text,Does.Contain("Power Source:").And.Contain("+1 Coin per Potato symbol"));
            }
            yield return VocabularyPicture("power-result","heading");
            state=codec.Encode(Session.State);files=Files();yield return VocabularyFamily(5);
            Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Power Source"));
            Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("stage 1 → 2").And.Contain("Coin per Potato symbol"));
            yield return VocabularyPicture("power-current-stage","panelBody");Unchanged(state,files);
            yield return Harness("PointerClick","productTabs",1);yield return Harness("PointerClick","productMapShortcut",-1);
            Assert.That(Field<RectTransform>("sectionMapRoot").GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Power"),Is.True,"Existing authored compact map label; fallback not rebuilt");
            yield return VocabularyPicture("authored-map","panelTitle");Unchanged(state,files);
            yield return Harness("PointerClick","productTabs",1);yield return Harness("PointerClick","productEnergy",-1);
            Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Energy & Bonus"));
            yield return VocabularyPicture("energy-resource","productContext");Unchanged(state,files);
        }
        [UnityTest]public IEnumerator PowerConstructionCopyFollowsVisiblePayment(){yield return VocabularyPower(true);}
        [UnityTest]public IEnumerator PowerConstructionCopyShortPortrait(){yield return VocabularyPower(false);}
        [UnityTest]public IEnumerator OwnedConstructionCopyShortPortrait()
        {
            var fixtures=new SectionsMapUiTests();var method=typeof(SectionsMapUiTests).GetMethod("StageFixture",Hidden);
            yield return PreparedHud((ModelState)method.Invoke(fixtures,new object[]{EraIds.WorkshopYard,5}));
            yield return InspectCompletedConstruction(1,"+1 Coin per spin; +3 Coins per Box symbol","Workshop knows its work.",true,"Chapter complete. Skills and orders are optional.");
            yield return InspectCompletedConstruction(3,"+3 Coins per Pigeon symbol; +3 Coins per delivery (3 Pigeons accumulated; carry 0–2)","The main address is ready.",true,"Chapter complete. Skills and orders are optional.");
        }
        [UnityTearDown]public IEnumerator RestoreFixture()
        {
            if(TestContext.CurrentContext.Result.Outcome.Status==NUnit.Framework.Interfaces.TestStatus.Failed)
            {TestContext.WriteLine("Preserved failed Chapter information fixture: "+DirectoryPath);typeof(ReelUiLifecycleTests).GetField("directory",Hidden).SetValue(ui,null);}
            yield return ui.RestoreAndRemoveOnlyPrivateFixtures();
        }
    }
}
#endif
