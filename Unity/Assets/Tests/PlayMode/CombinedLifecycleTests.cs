#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Diagnostics;
using JunkToNeon.Guidance;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    // Two combined transitions absent from the existing idle-return/basic-Back cases.
    // Uses the same private-save, additive scene and uGUI pointer approach; no runtime hooks.
    public sealed class CombinedLifecycleTests
    {
        const string ScenePath = "Assets/Scenes/JunkToNeon.unity";
        const string SaveOverride = "JTN_EDITOR_SAVE_DIRECTORY", ClockOverride = "JTN_EDITOR_UTC_SECONDS";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly JsonStateCodec codec = new JsonStateCodec();
        string directory, previousSave, previousClock;
        bool retainTouchProof;
        Scene scene, previousScene;
        Component game; Type gameType;
        GameSession Session => (GameSession)gameType.GetProperty("Session").GetValue(game);
        bool Flag(string property) => (bool)gameType.GetProperty(property).GetValue(game);
        T Field<T>(string name) => (T)gameType.GetField(name, Private).GetValue(game);
        void Call(string name, params object[] args) => gameType.GetMethod(name, Private | BindingFlags.Public).Invoke(game, args);
        void Clock(long seconds) => Environment.SetEnvironmentVariable(ClockOverride, seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));

        [SetUp]
        public void Isolate()
        {
            retainTouchProof=false;
            previousSave = Environment.GetEnvironmentVariable(SaveOverride); previousClock = Environment.GetEnvironmentVariable(ClockOverride);
            previousScene = SceneManager.GetActiveScene(); scene = default; directory = null;
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False, "Use the existing neutral test scene, not the user's running game");
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.work/combined-lifecycle-tests", Guid.NewGuid().ToString("N")));
            Assert.That(Path.GetPathRoot(directory), Is.EqualTo("X:\\").IgnoreCase, "Keep private fixture output on X:");
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable(SaveOverride, directory); Clock(1000);
            gameType = Type.GetType("JunkToNeon.JunkGame, Assembly-CSharp", true);
        }

        IEnumerator Load(ModelState state)
        {
            state.lastSettledAt = 1000; GameLogic.Validate(state);
            File.WriteAllText(Path.Combine(directory, "jtn.save.v1.json"), codec.Encode(state));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(ScenePath); SceneManager.SetActiveScene(scene);
            game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(gameType, true)).Single();
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
            var store = typeof(GameSession).GetField("store", Private).GetValue(Session);
            var storage = (FileSnapshotStorage)typeof(GameStore).GetField("storage", Private).GetValue(store);
            foreach (var field in typeof(FileSnapshotStorage).GetFields(Private).Where(f => f.FieldType == typeof(string)))
                Assert.That(Path.GetDirectoryName(Path.GetFullPath((string)field.GetValue(storage))), Is.EqualTo(directory), "Fixture storage escaped its exact private folder");
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            TestContext.WriteLine("Verified all actual storage paths: " + directory);
        }

        IEnumerator Click(string field, int index = -1)
        {
            yield return null; Canvas.ForceUpdateCanvases();
            var button = index < 0 ? Field<Button>(field) : Field<Button[]>(field)[index];
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, field + " unavailable");
            var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), "Combined-case action is obscured");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        IEnumerator Back()
        {
            yield return null;
            var adapter = game.GetComponents<Component>().Single(c => c.GetType().FullName == "JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)adapter.GetType().GetMethod("RequestBack").Invoke(adapter, null), Is.True);
        }

        // Product routes are nested (detail > list > hub), so leaving a panel can take several Backs.
        IEnumerator Root()
        {
            for (int i = 0; i < 4 && Field<GameObject>("panel").activeSelf; i++) yield return Back();
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False, "Back did not return to the root screen");
        }

        IEnumerator Finish()
        {
            float deadline = Time.realtimeSinceStartup + 7;
            while (Flag("Busy") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Flag("Busy"), Is.False, "The single committed presentation did not finish");
            Assert.That(Flag("AutoRunning"), Is.False);
        }
        void Saved()
        {
            Assert.That(Session.State.pending, Is.Null);
            Assert.That(File.ReadAllText(Path.Combine(directory, "jtn.save.v1.json")), Is.EqualTo(codec.Encode(Session.State)), "Live state differs from its committed private primary");
        }
        sealed class FixtureStorage : ISnapshotStorage
        {
            public string Raw;
            public string ReadPrimary() => Raw; public string ReadBackup() => null;
            public void Write(string raw) { Raw = raw; }
        }
        GameSession Fixture(ModelState state) => GameSession.Open(new GameStore(new FixtureStorage { Raw = codec.Encode(state) }, codec), 1);
        static long Price(string id)
        {
            Assert.That(ChapterCatalog.Rules.TryGetUpgrade(id, out var project) && project.Price.HasValue, Is.True);
            return ChapterEconomy.ProjectPrice(id);
        }
        ModelState DeferredHouse(out long price)
        {
            var state = ModelState.Fresh(1); state.coins = 1000;
            var setup = Fixture(state);
            setup.SelectChapterProject(GameIds.HouseRepair, setup.State.sequence + 1);
            setup.AcknowledgeAutoHint(setup.State.sequence + 1);
            setup.SelectChapterProject(GameIds.WorkshopBuild, setup.State.sequence + 1);
            price = Price(GameIds.HouseImprove);
            state = setup.State; state.coins = price - 10;
            setup = Fixture(state);
            return setup.SelectChapterProject(GameIds.HouseImprove, setup.State.sequence + 1);
        }
        ModelState AlmostReadyOrder()
        {
            var state = ModelState.Fresh(1); state.coins = 10000;
            var setup = Fixture(state); long cost = 0;
            foreach (string era in new[] { EraIds.Junkyard, EraIds.WorkshopYard })
            {
                if (era == EraIds.WorkshopYard) { setup.EnterWorkshopYard(setup.State.sequence + 1); setup.AcknowledgeEraTransition(setup.State.sequence + 1); }
                foreach (var family in FamilyIds.All) for (int level = 1; level <= 2; level++)
                {
                    string project = ChapterIds.Project(era, family, level); cost += Price(project);
                    setup.SelectChapterProject(project, setup.State.sequence + 1);
                    if (project == GameIds.HouseRepair) setup.AcknowledgeAutoHint(setup.State.sequence + 1);
                }
            }
            setup.AcceptOrder("order.sort-salvage", setup.State.sequence + 1);
            for (int i = 0; i < 15; i++) setup.ExecuteChapterSpin(setup.State.sequence + 1);
            // Independent fixed xorshift trace: first15 spins contain11 trash, carry1,
            // total352 coins with chapter rates[6,7,3,3]; next [trash,pigeon,trash] gives20.
            Assert.That(setup.State.coins, Is.EqualTo(10000 - cost + 352));
            Assert.That(setup.State.seed, Is.EqualTo(82514103u));
            Assert.That(setup.Journey.Orders.Progress, Is.EqualTo(11)); Assert.That(setup.Journey.Orders.Ready, Is.False);
            Assert.That(setup.State.wave.Delivery.PigeonProgress, Is.EqualTo(1));
            return setup.State;
        }

        [UnityTest]
        public IEnumerator AutoPresentationReturnDefersAffordableConstructionAndCannotRestartItself()
        {
            var fixture = DeferredHouse(out long price); long start = fixture.sequence;
            yield return Load(fixture);
            yield return Click("autoButton");
            Assert.That(Flag("Busy") && Flag("AutoRunning"), Is.True);
            Assert.That(Session.State.coins, Is.EqualTo(price - 1));
            Assert.That(Session.State.lastSpin.symbols, Is.EqualTo(new[] { 0, 0, 2 }));
            Assert.That(Session.State.lastSpin.reward, Is.EqualTo(8)); Assert.That(Session.State.lastSpin.bonus, Is.EqualTo(1));
            Call("OnApplicationPause", true); int frame = (int)gameType.GetProperty("PresentedFrames").GetValue(game);
            yield return new WaitForSecondsRealtime(EconomyRules.SpinDurationSeconds + .1f);
            Assert.That(Flag("Busy") && Flag("InBackground"), Is.True, "Presentation elapsed while backgrounded");
            Assert.That(Flag("AutoRunning"), Is.False);
            Assert.That((int)gameType.GetProperty("PresentedFrames").GetValue(game), Is.GreaterThan(frame));
            Clock(1600); Call("OnApplicationPause", false);
            Assert.That(Flag("Busy"), Is.True); Assert.That(Field<bool>("offlineSummaryPanel"), Is.False);
            Assert.That(Session.State.coins, Is.EqualTo(price + 9)); Assert.That(Session.State.sequence, Is.EqualTo(start + 2));
            string returning = codec.Encode(Session.State); Call("Spin"); Call("ToggleAuto"); Call("OnApplicationPause", false);
            Assert.That(codec.Encode(Session.State), Is.EqualTo(returning), "Resume/reentrant controls committed another spin or settlement");
            yield return Finish();
            Assert.That(Field<GameObject>("panel").activeSelf && Field<bool>("offlineSummaryPanel"), Is.True);
            Assert.That(Field<Button>("selectButton").gameObject.activeSelf, Is.False);
            Assert.That(Session.State.houseStage, Is.EqualTo(1)); Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo(GameIds.HouseImprove));
            Assert.That(Session.State.lastSpin.sequence, Is.EqualTo(start + 1)); Assert.That(Session.State.lastSettlement.coins, Is.EqualTo(10)); Saved();
            yield return Back();
            Assert.That(Field<GameObject>("panel").activeSelf || Field<bool>("exitPanel"), Is.False);
            Assert.That(Session.State.sequence, Is.EqualTo(start + 3)); Assert.That(Session.State.lastSettlementSeen, Is.EqualTo(start + 2));
            yield return new WaitForSecondsRealtime(EconomyRules.AutoGapSeconds + .35f);
            Assert.That(Session.State.coins, Is.EqualTo(price + 9)); Assert.That(Session.State.houseStage, Is.EqualTo(1));
            Assert.That(Session.State.sequence, Is.EqualTo(start + 3)); Assert.That(Flag("Busy") || Flag("AutoRunning"), Is.False); Saved();
            TestContext.WriteLine("AUTO committed pair8+workshop1 once; paused presentation survived full duration; return+10 deferred selected house at current price=" + price + "; Back ACK only, no hidden purchase or resumed AUTO.");
        }

        [UnityTest]
        public IEnumerator ReadyOrderDuringPresentationReturnAndBackNeverClaimsItsReward()
        {
            var fixture = AlmostReadyOrder(); long start = fixture.sequence, beforeCoins = fixture.coins;
            yield return Load(fixture);
            Assert.That(Field<Button>("autoButton").interactable, Is.False, "Completed chapter disallows AUTO; do not synthesize an impossible auto-order state");
            yield return Click("spinButton");
            Assert.That(Flag("Busy"), Is.True); Assert.That(Session.State.coins, Is.EqualTo(beforeCoins + 20));
            Assert.That(Session.State.lastSpin.symbols, Is.EqualTo(new[] { 2, 3, 2 })); Assert.That(Session.State.seed, Is.EqualTo(2371448660u));
            Assert.That(Session.Journey.Orders.Ready, Is.True); Assert.That(Session.Journey.Orders.Progress, Is.EqualTo(12));
            Call("OnApplicationPause", true); yield return null; Clock(1600); Call("OnApplicationPause", false);
            Assert.That(Flag("Busy"), Is.True); Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            string settled = codec.Encode(Session.State); Call("Spin"); Call("ToggleAuto");
            Assert.That(codec.Encode(Session.State), Is.EqualTo(settled));
            yield return Finish();
            Assert.That(Field<bool>("offlineSummaryPanel"), Is.True); Assert.That(Session.State.coins, Is.EqualTo(beforeCoins + 30));
            Assert.That(Session.Journey.Orders.Progress, Is.EqualTo(12)); Assert.That(Session.State.wave.Delivery.PigeonProgress, Is.EqualTo(2));
            yield return Back();
            Assert.That(Session.State.lastSettlementSeen, Is.EqualTo(start + 2));
            yield return ProductOrderDetail();
            Assert.That(Field<Button>("selectButton").gameObject.activeInHierarchy, Is.True, "Ready order claim should be visible after the return summary");
            string beforeBack = codec.Encode(Session.State); yield return Back();
            yield return new WaitForSecondsRealtime(EconomyRules.AutoGapSeconds + .35f);
            Assert.That(codec.Encode(Session.State), Is.EqualTo(beforeBack), "Back confirmed claim/cancel or a presentation queued another spin");
            Assert.That(Session.State.coins, Is.EqualTo(beforeCoins + 30), "The120-coin reward must remain unclaimed");
            Assert.That(Session.Journey.Orders.ActiveOrderId, Is.EqualTo("order.sort-salvage")); Assert.That(Session.Journey.Orders.Ready, Is.True);
            Assert.That(Session.State.lastSpin.sequence, Is.EqualTo(start + 1)); Assert.That(Session.State.sequence, Is.EqualTo(start + 3));
            yield return Root(); Assert.That(codec.Encode(Session.State), Is.EqualTo(beforeBack), "Leaving the order route changed state");
            Assert.That(Field<GameObject>("panel").activeSelf || Flag("Busy") || Flag("AutoRunning"), Is.False); Saved();
            TestContext.WriteLine("Spin16 [trash,pigeon,trash] paid20 and capped order11->12 once; return+10 kept carry2; summary Back ACK then ready-order Back left reward120 unclaimed and AUTO stopped.");
        }

        [UnityTest]
        public IEnumerator CurrentOfferAndPinnedLegacySelectionRemainDistinctThroughActualUi()
        {
            var current=ModelState.Fresh(1);current.houseStage=1;current.autoHintSeen=1;current.coins=200;
            var setup=Fixture(current);current=setup.SelectChapterProject(GameIds.HouseImprove,1);
            yield return Load(current);
            Assert.That(Field<TMPro.TMP_Text>("stageLabel").text,Does.Contain("200/480"));
            Call("ShowChapterSkill",SkillIds.Universal1);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("200/480"));yield return Back();
            SceneManager.SetActiveScene(previousScene);yield return SceneManager.UnloadSceneAsync(scene);
            var legacy=ModelState.Fresh(1);legacy.houseStage=1;legacy.selectedStage=2;legacy.autoHintSeen=1;legacy.coins=550;
            yield return Load(legacy);uint seed=Session.State.seed;long sequence=Session.State.sequence;
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(600));
            Assert.That(Field<TMPro.TMP_Text>("stageLabel").text,Does.Contain("550/600"));
            Call("ShowChapterSkill",SkillIds.Universal1);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("550/600"));yield return Back();
            Call("ShowChapterFamily",FamilyIds.House,false);yield return null;
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Price: 480"));
            Assert.That(Field<Button>("selectButton").GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("Build for 480 Coins"));
            Assert.That(Session.State.sequence,Is.EqualTo(sequence));
            yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(70));Assert.That(Session.State.houseStage,Is.EqualTo(2));
            Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Session.State.seed,Is.EqualTo(seed));
            Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Saved();
            TestContext.WriteLine("Actual current goal/skill200/480; old goal/skill550/600 unchanged until visible480 reselection pointer, one480 debit, no RNG or hidden successor.");
        }

        [UnityTest]
        public IEnumerator CurrentOfferAndPinnedV2SelectionRemainDistinctThroughActualUi()
        {
            string fixture=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","Checks","ConstructionPricing","evidence","cadence-r12","v2","v2-selected540.json"));
            yield return Load(codec.Decode(File.ReadAllText(fixture)));
            Assert.That(Session.State.coins,Is.EqualTo(535));
            Assert.That(Session.Chapter.ConstructionPriceRevision,Is.EqualTo(JunkToNeon.Content.ConstructionPricing.RevisionV2));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
            Assert.That(Field<TMPro.TMP_Text>("stageLabel").text,Does.Contain("535/540"));
            Call("ShowChapterSkill",SkillIds.Universal1);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("535/540"));yield return Back();
            long sequence=Session.State.sequence;uint seed=Session.State.seed;
            string before=codec.Encode(Session.State);var beforeFiles=SelectionFiles();
            yield return Click("productTabs",1);yield return Click("productGoalButton");
            ReselectionCopy("Builds now. Pay 480: 535 → 55 Coins.","Build for 480 Coins");
            Assert.That(Session.State.coins,Is.EqualTo(535));Assert.That(Session.State.sequence,Is.EqualTo(sequence));
            yield return Back();SelectionUnchanged(before,beforeFiles);
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
            yield return Click("productTabs",1);yield return Click("productGoalButton");ReselectionCopy("Builds now. Pay 480: 535 → 55 Coins.","Build for 480 Coins");
            var queued=Field<Button>("selectButton").onClick;
            yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(55));Assert.That(Session.State.houseStage,Is.EqualTo(2));
            Assert.That(Session.Chapter.ConstructionPriceRevision,Is.EqualTo(JunkToNeon.Content.ConstructionPricing.RevisionV3));
            Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Session.State.seed,Is.EqualTo(seed));
            Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Saved();
            string committed=codec.Encode(Session.State);var committedFiles=SelectionFiles();
            queued.Invoke();SelectionUnchanged(committed,committedFiles);
            foreach(var offer in new[]{new{card=4,text="Price: 140"},new{card=5,text="Price: 190"}})
            {yield return Click("productTabs",0);yield return Click("productCards",0);yield return Click("navigationCards",offer.card);Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain(offer.text));yield return Back();}
            Assert.That(codec.Encode(Session.State),Is.EqualTo(committed),"Inspecting the other current offers must preserve complete state");
            TestContext.WriteLine("Genuine V2 preview visibly explains selected540/current480 and immediate535->55 before the pointer; Back preserves exact state/files, one click debits480, stale callback cannot debit again. Other current offers remain read-only.");
        }

        void ReselectionCopy(string consequence,string caption)
        {
            var body=Field<TMPro.TMP_Text>("panelBody");
            Assert.That(body.text,Does.Contain("Saved goal: 540 Coins. Price: 480 Coins.").And.Contain(consequence));
            Assert.That(Field<Button>("selectButton").GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo(caption));
            NarrativeBounds(Field<Button>("selectButton").GetComponentInChildren<TMPro.TMP_Text>());
            NarrativeBounds(body);Assert.That(body.isTextOverflowing,Is.False,"Reselection explanation must fit the existing portrait body");
        }

        [UnityTest]
        public IEnumerator CurrentOfferAndPinnedV2InsufficientSelectionExplainsNoDebit()
        {
            string fixture=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","Checks","ConstructionPricing","evidence","cadence-r12","v2","v2-selected540.json"));
            var synthetic=codec.Decode(File.ReadAllText(fixture));
            // Explicit private wallet variant, not a genuine historical470 snapshot.
            // The genuine535 file above is never edited; no frozen spin/pending receipt is rewritten.
            Assert.That(synthetic.pending,Is.Null);Assert.That(synthetic.lastSpin,Is.Null);
            synthetic.coins=470;GameLogic.Validate(synthetic);yield return Load(synthetic);
            string before=codec.Encode(Session.State);var files=SelectionFiles();long sequence=Session.State.sequence;uint seed=Session.State.seed;
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
            yield return Click("productGoalButton");ReselectionCopy("Select without spending: 470 → 470 Coins. Need 10 more.","Update goal · no spend");
            yield return Back();SelectionUnchanged(before,files);
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
            yield return Click("productTabs",1);yield return Click("productGoalButton");ReselectionCopy("Select without spending: 470 → 470 Coins. Need 10 more.","Update goal · no spend");
            var queued=Field<Button>("selectButton").onClick;yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(470));Assert.That(Session.State.houseStage,Is.EqualTo(1));
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(GameIds.HouseImprove));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(480));
            Assert.That(Session.Chapter.ConstructionPriceRevision,Is.EqualTo(JunkToNeon.Content.ConstructionPricing.RevisionV3));
            Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Assert.That(Session.State.seed,Is.EqualTo(seed));Saved();
            string selected=codec.Encode(Session.State);files=SelectionFiles();queued.Invoke();SelectionUnchanged(selected,files);
            TestContext.WriteLine("Explicit synthetic470 V2 target: visible no-debit/missing10 explanation, Back preserves540, one pointer selects current480 without spending, stale callback is read-only. Not natural pacing or a genuine historical470 receipt.");
        }

        [UnityTest]
        public IEnumerator CompletionHeadingNamesTheQualifyingBoxSymbol()
        {
            var state=ModelState.Fresh(1);state.houseStage=1;state.workshopBuilt=1;state.autoHintSeen=1;state.coins=1000;
            yield return Load(state);Call("ShowChapterFamily",FamilyIds.Workshop,false);yield return null;
            yield return Click("selectButton");yield return null;
            Assert.That(Session.State.coins,Is.EqualTo(740));
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Contain("+1 Coin per Box symbol"));Saved();
            TestContext.WriteLine("Actual workshop improvement pointer paid260 once; visible completion heading names box condition instead of generic symbol.");
        }

        GameSession NarrativeFixture()
        {
            var state=ModelState.Fresh(1);state.coins=20000;
            return Fixture(state); // Explicit funded canonical test fixture, never ordinary progress.
        }
        void NarrativeEra(GameSession session,string era,bool omitLast=false)
        {
            foreach(string family in FamilyIds.All)for(int level=1;level<=2;level++)
            {
                if(omitLast&&family==FamilyIds.PowerSource&&level==2)continue;
                string id=ChapterIds.Project(era,family,level);session.SelectChapterProject(id,session.State.sequence+1);
                if(id==GameIds.HouseRepair)session.AcknowledgeAutoHint(session.State.sequence+1);
            }
        }
        IEnumerator NarrativeUnload()
        {
            SceneManager.SetActiveScene(previousScene);yield return SceneManager.UnloadSceneAsync(scene);scene=default;
        }
        void NarrativeBounds(TMPro.TMP_Text label)
        {
            Canvas.ForceUpdateCanvases();label.ForceMeshUpdate();
            Assert.That(label.textBounds.size.y,Is.LessThanOrEqualTo(label.rectTransform.rect.height+2),label.name+" story exceeds existing height: "+label.text);
            Assert.That(label.textBounds.size.x,Is.LessThanOrEqualTo(label.rectTransform.rect.width+2),label.name+" story exceeds existing width: "+label.text);
        }
        void VisibleProductNarration()
        {
            var label=Field<TMPro.TMP_Text>("heading");
            Assert.That(label.isActiveAndEnabled&&label.gameObject.activeInHierarchy,Is.True,"Narration must be on the visible canvas");
            Assert.That(label.transform.parent,Is.EqualTo(Field<RectTransform>("productChrome")));
            Assert.That(label.raycastTarget,Is.False); NarrativeBounds(label);
            Assert.That(label.isTextOverflowing,Is.False,label.text);
        }
        void MidLateSnapshot(string name,string provenance)
        {
            string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/mid-late-delivery-20261101/root","produced-"+name+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(target);
            string path=Path.Combine(target,"jtn.save.v1.json");
            File.WriteAllText(path,codec.Encode(Session.State),new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"visible-message.txt"),Field<TMPro.TMP_Text>("message").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"visible-heading.txt"),Field<TMPro.TMP_Text>("heading").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"product-context.txt"),Field<TMPro.TMP_Text>("productContext").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"provenance.txt"),provenance+" Checkpoint: "+name+"; sequence: "+Session.State.sequence+". Visible text is captured at this checkpoint and is not recreated by reload.",new System.Text.UTF8Encoding(false));
            TestContext.WriteLine("Private produced snapshot: "+path);
        }
        void ReturnSnapshot(string name,string provenance)
        {
            string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/return-session-delivery-20261101/root","produced-"+name+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target,"jtn.save.v1.json"),codec.Encode(Session.State),new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"goal.txt"),Field<TMPro.TMP_Text>("productGoalText").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"message.txt"),Field<TMPro.TMP_Text>("message").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"panel.txt"),Field<TMPro.TMP_Text>("panelBody").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"product-card-2.txt"),Field<Button[]>("productCards")[2].GetComponentInChildren<TMPro.TMP_Text>(true).text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"provenance.txt"),provenance+" Clock="+Environment.GetEnvironmentVariable(ClockOverride)+"; sequence="+Session.State.sequence+". UI text records the checkpoint, not replayed events.",new System.Text.UTF8Encoding(false));
            TestContext.WriteLine("Private produced return snapshot: "+target);
        }
        void PostchapterSnapshot(string name,string provenance)
        {
            string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/postchapter-delivery-20261101/root","produced-"+name+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target,"jtn.save.v1.json"),codec.Encode(Session.State),new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"panel.txt"),Field<TMPro.TMP_Text>("panelBody").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"navigation-context.txt"),Field<TMPro.TMP_Text>("navigationContext").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"provenance.txt"),provenance+"; sequence="+Session.State.sequence+"; panel mode="+Field<int>("wavePanel")+"; panelBody visible="+Field<TMPro.TMP_Text>("panelBody").isActiveAndEnabled+"; navigationContext visible="+Field<TMPro.TMP_Text>("navigationContext").isActiveAndEnabled+". UI text is captured at this checkpoint, not replayed by reload.",new System.Text.UTF8Encoding(false));
            TestContext.WriteLine("Private produced postchapter snapshot: "+target);
        }
        IReadOnlyList<DiagnosticRecord> MidLateDiagnostics => (IReadOnlyList<DiagnosticRecord>)gameType.GetProperty("LocalDiagnostics").GetValue(game);

        [UnityTest]
        public IEnumerator NarrativeFreshOpeningAndImmediateRepairKeepExistingSaveBoundary()
        {
            var fresh=GameSession.Open(new GameStore(new FixtureStorage(),codec),1,initializeJourney:true).State;
            yield return Load(fresh);string original=codec.Encode(Session.State);
            yield return Click("guideButton");Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain(ChapterNarrative.Opening));
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("do not select or buy a project"));NarrativeBounds(Field<TMPro.TMP_Text>("panelBody"));
            yield return Back();Assert.That(codec.Encode(Session.State),Is.EqualTo(original),"Reading opening must not write or grant");
            yield return Click("spinButton");yield return Finish();Assert.That(Session.State.coins,Is.EqualTo(45));
            yield return Click("yardButtons",0);yield return Click("selectButton");Assert.That(Session.State.coins,Is.EqualTo(5));
            Assert.That(Session.Guidance.LastCompletion.ProjectId,Is.EqualTo(GameIds.HouseRepair));long completion=Session.Guidance.LastCompletion.Sequence;
            if(Field<GameObject>("panel").activeSelf)yield return Click("closeButton");
            yield return Click("guideButton");ChapterNarrative.TryCompletion(GameIds.HouseRepair,out var beat);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain(beat.Body).And.Contain("AUTO"));NarrativeBounds(Field<TMPro.TMP_Text>("panelBody"));
            string read=codec.Encode(Session.State);yield return Back();Call("Reload");yield return null;
            Assert.That(codec.Encode(Session.State),Is.EqualTo(read));Assert.That(Session.Guidance.LastCompletion.Sequence,Is.EqualTo(completion));Saved();
            TestContext.WriteLine("Fresh opening is read-only; real seed1 spin45 then visible immediate40 repair leaves5. Existing exact completion survives actual Reload, no story save or intro grant.");
        }

        [UnityTest,Category("Expansion07"),Category("Expansion5h")]
        public IEnumerator NarrativeAutoRetainsLatestReactionThroughExplicitRestartAndNextReel()
        {
            var state=ModelState.Fresh(1);state.houseStage=1;state.autoHintSeen=1;state.coins=119;
            var setup=Fixture(state);setup.SelectChapterProject(GameIds.WorkshopBuild,setup.State.sequence+1);
            yield return Load(setup.State);long initial=Session.State.sequence;float start=Time.realtimeSinceStartup;
            yield return Click("autoButton");Assert.That(Flag("Busy")&&Flag("AutoRunning"),Is.True);
            long completion=Session.Guidance.LastCompletion.Sequence;Assert.That(Session.Guidance.LastCompletion.ProjectId,Is.EqualTo(GameIds.WorkshopBuild));
            ChapterNarrative.TryCompletion(GameIds.WorkshopBuild,out var beat);
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Not.Contain(beat.Title),"Acquisition spin must not reveal its newly committed story before presentation");
            Call("Render",null,null);Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Not.Contain(beat.Title),"Intervening after-state Render must not reveal it early");
            float deadline=Time.realtimeSinceStartup+7;while(Flag("Busy")&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(Flag("Busy"),Is.False);Assert.That(Flag("AutoRunning"),Is.False);Assert.That(Time.realtimeSinceStartup-start,Is.GreaterThanOrEqualTo(3.45f));
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Contain(beat.Title).And.Contain("+1 Coin per future spin"));
            VisibleProductNarration();
            yield return Click("autoButton");
            deadline=Time.realtimeSinceStartup+2;while(!Flag("Busy")&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(Flag("Busy")&&Flag("AutoRunning"),Is.True,"After explicit restart, story must not interrupt no-goal AUTO");
            Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Contain(beat.Title).And.Contain("+1 Coin per future spin"));
            VisibleProductNarration();
            Assert.That(Session.Guidance.LastCompletion.Sequence,Is.EqualTo(completion));Call("Render",null,null);
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Contain(beat.Title));yield return Click("autoButton");
            Call("OnApplicationPause",true);yield return null;Call("OnApplicationPause",false);Call("Render",null,null);
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Contain(beat.Title),"Already presented reaction survives a Busy pause/resize render");yield return Finish();
            Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(initial+2));string before=codec.Encode(Session.State);Call("Reload");yield return null;
            Assert.That(codec.Encode(Session.State),Is.EqualTo(before));Assert.That(Session.Guidance.LastCompletion.Sequence,Is.EqualTo(completion));
            yield return Click("guideButton");Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain(beat.Body).And.Contain("+1 Coin per future spin"));
            yield return Click("selectButton");Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.EqualTo(completion));
            yield return Click("spinButton");Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Not.Contain(beat.Title),"ACK must not replay an old narrative on the next reel");yield return Finish();Saved();
            yield return NarrativeUnload();const string cartId="cart.build";
            var a=ModelState.Fresh(1);a.houseStage=1;a.autoHintSeen=1;a.coins=1000;
            var actorA=Fixture(a);actorA.SelectChapterProject(cartId,actorA.State.sequence+1);yield return Load(actorA.State);
            yield return null;Canvas.ForceUpdateCanvases();ChapterNarrative.TryCompletion(cartId,out var beatA);
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Contain(beatA.Title));
            var b=ModelState.Fresh(1);b.houseStage=1;b.autoHintSeen=1;b.coins=1000;
            var actorB=Fixture(b);actorB.SelectChapterProject(GameIds.WorkshopBuild,actorB.State.sequence+1);b=actorB.State;b.lastSettledAt=400;
            Assert.That(actorA.Guidance.LastCompletion.Sequence,Is.EqualTo(actorB.Guidance.LastCompletion.Sequence),"Equal sequence/different project exercises both identity fields");
            File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),codec.Encode(b));Call("Reload");yield return null;
            Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);Assert.That(Session.State.coins,Is.EqualTo(890));
            Assert.That(Session.Chapter.Progress.CompletedUpgradeIds,Does.Not.Contain(cartId));
            // Close the covering summary and start in the same frame before an uncovered Canvas pass.
            Call("ClosePanel");Call("Spin");Assert.That(Flag("Busy"),Is.True);
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Not.Contain(beatA.Title).And.Not.Contain(beat.Title),"Reload must invalidate superseded A without revealing covered B early");
            Assert.That(Session.State.coins,Is.EqualTo(899));yield return Finish();yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Contain(beat.Title));Saved();
            VisibleProductNarration();
            TestContext.WriteLine("AUTO retains presented reaction without early acquisition reveal or post-ACK replay. Actual Reload into distinct completion with equal sequence/covered Canvas invalidates superseded story; same-frame close+spin gives only existing offline10+foreground9, then reveals B normally.");
        }

        [UnityTest]
        public IEnumerator NarrativeEraEntryFinalBoundaryOrdersAndAllTextBoundsStayFactual()
        {
            // Validated synthetic ownership boundaries: these do not claim a played economic history.
            yield return Load(MilestoneEraFixture(EraIds.Junkyard,new[]{0,0,0,0,0,0}));
            Assert.That(ChapterCatalog.Rules.RemainingEraRequirements(Session.Chapter.Progress).Count,Is.EqualTo(6));
            yield return Click("productTabs",0);Assert.That(Field<TMPro.TMP_Text>("productContext").text,Does.Contain("12 building stages until Workshop Yard"));
            string initial=codec.Encode(Session.State);var initialFiles=SelectionFiles();yield return Back();SelectionUnchanged(initial,initialFiles);
            yield return NarrativeUnload();
            foreach(var item in new[]{new{levels=new[]{1,0,0,0,0,0},paid=11,families=6},new{levels=new[]{2,0,0,0,0,0},paid=10,families=5}})
            {
                yield return Load(MilestoneEraFixture(EraIds.Junkyard,item.levels));
                Assert.That(ChapterCatalog.Rules.RemainingEraRequirements(Session.Chapter.Progress).Count,Is.EqualTo(item.families));
                yield return Click("productTabs",0);Assert.That(Field<TMPro.TMP_Text>("productContext").text,Does.Contain(item.paid+" building stages until Workshop Yard"));
                yield return NarrativeUnload();
            }
            yield return Load(MilestoneEraFixture(EraIds.Junkyard,new[]{2,2,2,2,2,1}));
            Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(11));
            yield return Click("productTabs",0);Assert.That(Field<TMPro.TMP_Text>("productContext").text,Does.Contain("1 building stage until Workshop Yard"));MidLateSnapshot("late-first-era-11","Synthetic validated 11-stage ownership, not a played campaign.");
            yield return NarrativeUnload();
            var ready=MilestoneEraFixture(EraIds.Junkyard,new[]{2,2,2,2,2,2});ready.coins=1000;
            yield return Load(ready);long readyCoins=Session.State.coins;uint readySeed=Session.State.seed;
            Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(12));
            Assert.That(Field<TMPro.TMP_Text>("productGoalText").text,Does.Contain("free entry"));
            string[] readyIds=Session.Chapter.Progress.CompletedUpgradeIds.ToArray();string preEntry=codec.Encode(Session.State);var preEntryFiles=SelectionFiles();
            yield return Click("productGoalButton");
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain(ChapterNarrative.EraReady).And.Contain("free"));NarrativeBounds(Field<TMPro.TMP_Text>("panelBody"));
            SelectionUnchanged(preEntry,preEntryFiles);yield return Back();SelectionUnchanged(preEntry,preEntryFiles);
            yield return Click("productTabs",1);yield return Click("productGoalButton");
            yield return Click("selectButton");Assert.That(Session.Chapter.TransitionPending,Is.True);
            Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(12));Assert.That(Session.State.coins,Is.EqualTo(readyCoins));Assert.That(Session.State.seed,Is.EqualTo(readySeed));MidLateSnapshot("free-entry-12-of-24","Synthetic validated 12-stage ownership; free entry committed by current UI pointer.");
            Assert.That(Session.Chapter.Progress.CompletedUpgradeIds,Is.EquivalentTo(readyIds));
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain(ChapterNarrative.EraEntry).And.Contain("Era entry saved"));
            string entered=codec.Encode(Session.State);Call("Reload");yield return null;Assert.That(codec.Encode(Session.State),Is.EqualTo(entered));
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain(ChapterNarrative.EraEntry));yield return Click("closeButton");
            Assert.That(Session.Chapter.TransitionPending,Is.False);yield return Click("productTabs",0);
            Assert.That(Field<TMPro.TMP_Text>("productContext").text,Does.Contain("Chapter needs 12 building stages").And.Contain("12/24").And.Not.Contain("until Workshop Yard"));yield return NarrativeUnload();
            var late=MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,4});late.coins=1000;
            yield return Load(late);Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(23));
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Not.Contain("Chapter complete"));
            yield return Click("productTabs",0);yield return Click("productCards",0);yield return Click("navigationCards",5);
            long immediateBefore=Session.State.coins;uint immediateSeed=Session.State.seed;string last=ChapterIds.Project(EraIds.WorkshopYard,FamilyIds.PowerSource,2);long lastPrice=Price(last);
            yield return Click("selectButton");Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(24));
            Assert.That(Session.State.coins,Is.EqualTo(immediateBefore-lastPrice));Assert.That(Session.State.seed,Is.EqualTo(immediateSeed));
            Assert.That(Field<TMPro.TMP_Text>("message").text,Does.Contain("paid "+lastPrice).And.Contain("yard is complete").And.Contain("Neon"));
            Assert.That(Field<TMPro.TMP_Text>("message").isActiveAndEnabled,Is.True);NarrativeBounds(Field<TMPro.TMP_Text>("message"));
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Contain("yard is complete").And.Contain("Neon"));VisibleProductNarration();
            Assert.That(Session.Guidance.LastCompletion.ProjectId,Is.EqualTo(last));long completion=Session.Guidance.LastCompletion.Sequence;
            Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.ChapterEnd),Is.False);MidLateSnapshot("final-immediate-power-skills0","Synthetic validated 23-stage ownership and funded wallet; actual UI selection paid the PowerSource final stage.");
            string beforeReload=codec.Encode(Session.State);var filesBeforeReload=SelectionFiles();int eventsBeforeReload=MidLateDiagnostics.Count;
            Call("Reload");yield return null;SelectionUnchanged(beforeReload,filesBeforeReload);
            Assert.That(MidLateDiagnostics.Count,Is.EqualTo(eventsBeforeReload));Assert.That((int)gameType.GetProperty("RewardFeedbackVisibleCount").GetValue(game),Is.Zero);
            yield return Click("guideButton");string body=Field<TMPro.TMP_Text>("panelBody").text;
            Assert.That(body,Does.Contain("yard is complete").And.Contain("Neon"));NarrativeBounds(Field<TMPro.TMP_Text>("panelBody"));
            yield return Back();SelectionUnchanged(beforeReload,filesBeforeReload);
            Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.Not.EqualTo(completion));Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.ChapterEnd),Is.False);
            yield return Click("guideButton");var oldCompletionAction=Field<Button>("selectButton").onClick;
            yield return Click("selectButton");Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.EqualTo(completion));
            Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.ChapterEnd),Is.False);
            yield return Click("guideButton");Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain(ChapterNarrative.ChapterEnd));
            string beforeStale=codec.Encode(Session.State);var beforeStaleFiles=SelectionFiles();oldCompletionAction.Invoke();SelectionUnchanged(beforeStale,beforeStaleFiles);
            Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.ChapterEnd),Is.False);
            yield return Click("selectButton");Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.ChapterEnd),Is.True);
            Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.EqualTo(completion));
            Assert.That(Session.State.coins,Is.EqualTo(immediateBefore-lastPrice));Assert.That(Session.State.seed,Is.EqualTo(immediateSeed));
            string acked=codec.Encode(Session.State);var ackFiles=SelectionFiles();Call("Reload");yield return null;SelectionUnchanged(acked,ackFiles);
            yield return Click("productTabs",2);yield return Click("productCards",2);
            foreach(string order in new[]{"order.sort-salvage","order.neighborhood-supply"})
            {
                yield return Click("navigationCards",order=="order.sort-salvage"?0:1);body=Field<TMPro.TMP_Text>("panelBody").text;
                Assert.That(body,Does.Contain(ChapterNarrative.OrderIntroduction(order)).And.Contain(order=="order.sort-salvage"?"Reward: 120":"Reward: 180"));
                Assert.That(body,Does.Contain(order=="order.sort-salvage"?"12 Trash appearances":"3 completed deliveries of 3 Pigeons"));NarrativeBounds(Field<TMPro.TMP_Text>("panelBody"));yield return Back();
            }
            string committed=codec.Encode(Session.State);var heading=Field<TMPro.TMP_Text>("heading");var panelText=Field<TMPro.TMP_Text>("panelBody");string originalHeading=heading.text,originalBody=panelText.text;
            var inline=gameType.GetMethod("InlineCompletion",BindingFlags.Static|BindingFlags.NonPublic);var benefit=gameType.GetMethod("CompletionBenefit",BindingFlags.Static|BindingFlags.NonPublic);var title=gameType.GetMethod("ProjectName",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(string era in new[]{EraIds.Junkyard,EraIds.WorkshopYard})foreach(string family in FamilyIds.All)for(int level=1;level<=2;level++)
            {
                string id=ChapterIds.Project(era,family,level);Assert.That(ChapterNarrative.TryCompletion(id,out var beat),Is.True);
                heading.text=(string)inline.Invoke(null,new object[]{id});NarrativeBounds(heading);
                panelText.text=beat.Body+"\nComplete: "+title.Invoke(null,new object[]{id})+".\n"+benefit.Invoke(null,new object[]{id})+"\nAll six Junkyard buildings are complete. Free entry is available in the era menu.";NarrativeBounds(panelText);
            }
            heading.text=originalHeading;panelText.text=originalBody;Assert.That(codec.Encode(Session.State),Is.EqualTo(committed));Saved();
            TestContext.WriteLine("Actual free era confirmation/unseen Reload/skip preserve state; final24 only, no future target. Both existing order prefices retain literal12/120 and3/180. All24 inline/panel strings fit unchanged labels without save writes.");
        }

        [UnityTest]
        public IEnumerator FinalSelectedCartFundedBySpinShowsEndingAndSeparateAcknowledgements()
        {
            var boundary=MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,4,5});
            string last=ChapterIds.Project(EraIds.WorkshopYard,FamilyIds.Cart,2);long price=Price(last);
            boundary.coins=price-1;var setup=Fixture(boundary);
            setup.SelectChapterProject(last,setup.State.sequence+1,1000);
            Assert.That(setup.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(23));
            Assert.That(ChapterEconomy.SelectedProjectPrice(setup.Chapter),Is.EqualTo(price));
            yield return Load(setup.State);long before=Session.State.coins;uint seed=Session.State.seed;
            yield return Click("productTabs",0);Assert.That(Field<TMPro.TMP_Text>("productContext").text,Does.Contain("Chapter needs 1 building stage").And.Contain("23/24").And.Not.Contain("until Workshop Yard"));
            yield return Click("productTabs",1);yield return Click("spinButton");
            Assert.That(Flag("Busy"),Is.True);
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Not.Contain("yard is complete").And.Not.Contain("Neon"),"Committed final state must not reveal its ending during the acquisition reel");
            yield return Finish();
            Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(24));
            Assert.That(Session.Guidance.LastCompletion.ProjectId,Is.EqualTo(last));long completion=Session.Guidance.LastCompletion.Sequence;
            Assert.That(Session.State.seed,Is.Not.EqualTo(seed));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            Assert.That(Session.State.lastSpin.chapter.Price,Is.EqualTo(price));
            Assert.That(Session.State.coins,Is.EqualTo(before+Session.Arcade.LastResourceReceipt.CoinIncome-price));
            Assert.That(Session.Arcade.LastResourceReceipt.ConstructionDebit,Is.EqualTo(price));
            var message=Field<TMPro.TMP_Text>("message");Assert.That(message.isActiveAndEnabled,Is.True);
            Assert.That(message.text,Does.Contain("+"+Session.Arcade.LastResourceReceipt.CoinIncome).And.Contain("Paid "+price));NarrativeBounds(message);
            Assert.That(Field<TMPro.TMP_Text>("heading").text,Does.Contain("yard is complete").And.Contain("Neon").And.Contain("+1").And.Contain("Box"));VisibleProductNarration();
            Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.ChapterEnd),Is.False);MidLateSnapshot("final-spin-cart-skills0","Synthetic validated 23-stage ownership and price-minus-one wallet; a public selection and actual UI spin paid the Cart final stage.");
            long wallet=Session.State.coins;uint afterSeed=Session.State.seed;string state=codec.Encode(Session.State);var files=SelectionFiles();int events=MidLateDiagnostics.Count;
            Call("Reload");yield return null;SelectionUnchanged(state,files);Assert.That(MidLateDiagnostics.Count,Is.EqualTo(events));
            Assert.That((int)gameType.GetProperty("RewardFeedbackVisibleCount").GetValue(game),Is.Zero);
            yield return Click("guideButton");Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("yard is complete").And.Contain("Neon"));
            yield return Click("selectButton");Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.EqualTo(completion));
            Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.ChapterEnd),Is.False);
            yield return Click("guideButton");yield return Click("selectButton");
            Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.ChapterEnd),Is.True);
            Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.EqualTo(completion));
            Assert.That(Session.State.coins,Is.EqualTo(wallet));Assert.That(Session.State.seed,Is.EqualTo(afterSeed));
            state=codec.Encode(Session.State);files=SelectionFiles();events=MidLateDiagnostics.Count;Call("Reload");yield return null;
            SelectionUnchanged(state,files);Assert.That(MidLateDiagnostics.Count,Is.EqualTo(events));
        }

        [UnityTest]
        public IEnumerator NarrativeCompletedExpandedLegacyNoticeNeverInventsRemainingBuildings()
        {
            var old=ModelState.Fresh(1);old.coins=20000;old.houseStage=2;old.workshopBuilt=1;old.autoHintSeen=1;
            old.wave=new WaveProgress(1,1,JunkToNeon.Effects.DeliveryState.Fresh(JunkToNeon.Effects.EffectRules.Provisional),JunkToNeon.Upgrades.UpgradeOwnership.Empty);
            var setup=Fixture(old);Assert.That(setup.Journey.Guidance.Origin,Is.EqualTo(GuideOrigin.LegacyExpanded));
            while(!ChapterCatalog.Rules.CanUnlockWorkshopYard(setup.Chapter.Progress))
                setup.SelectChapterProject(ChapterCatalog.Rules.AvailableUpgrades(setup.Chapter.Progress).First(),setup.State.sequence+1);
            setup.EnterWorkshopYard(setup.State.sequence+1);setup.AcknowledgeEraTransition(setup.State.sequence+1);
            while(!ChapterCatalog.Rules.IsChapterComplete(setup.Chapter.Progress))
                setup.SelectChapterProject(ChapterCatalog.Rules.AvailableUpgrades(setup.Chapter.Progress).First(),setup.State.sequence+1);
            yield return Load(setup.State);string before=codec.Encode(Session.State);
            yield return Click("guideButton");string body=Field<TMPro.TMP_Text>("panelBody").text;
            Assert.That(body,Does.Contain("Both eras of the expanded chapter are complete").And.Contain("no repeat starting reward").And.Not.Contain("Complete them separately").And.Not.Contain(ChapterNarrative.Opening));
            NarrativeBounds(Field<TMPro.TMP_Text>("panelBody"));Assert.That(codec.Encode(Session.State),Is.EqualTo(before));long coins=Session.State.coins;
            yield return Click("selectButton");Assert.That(Session.State.coins,Is.EqualTo(coins));Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.ExpandedR7),Is.True);Saved();
            TestContext.WriteLine("Actual prior r7 origin carried through canonical funded remaining stages; expanded notice remains factual after all24 and has no intro replay/grant/read write. Existing explicit ACK only.");
        }

        Dictionary<string,string> SelectionFiles()
        {
            return Directory.GetFiles(directory).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
        }
        void SelectionHealthy()
        {
            Assert.That(Session.RecoveryRequired,Is.False,Session.Error);Assert.That(Session.Error,Is.Null);
            Assert.That(Field<bool>("failed"),Is.False,"Inactive callbacks must not poison a healthy UI/session");
        }
        void SelectionUnchanged(string state,Dictionary<string,string> files)
        {
            Assert.That(codec.Encode(Session.State),Is.EqualTo(state));Assert.That(SelectionFiles(),Is.EquivalentTo(files),"No file bytes, inventory or last-write timestamps may change");SelectionHealthy();
        }
        IEnumerator SelectionLoadRaw(string raw,bool blocked=false)
        {
            File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),raw);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Additive));
            scene=SceneManager.GetSceneByPath(ScenePath);SceneManager.SetActiveScene(scene);
            game=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren(gameType,true)).Single();
            yield return null;Canvas.ForceUpdateCanvases();Assert.That(Session.RecoveryRequired,Is.EqualTo(blocked));
            var store=typeof(GameSession).GetField("store",Private).GetValue(Session);
            var storage=(FileSnapshotStorage)typeof(GameStore).GetField("storage",Private).GetValue(store);
            foreach(var field in typeof(FileSnapshotStorage).GetFields(Private).Where(f=>f.FieldType==typeof(string)))
                Assert.That(Path.GetDirectoryName(Path.GetFullPath((string)field.GetValue(storage))),Is.EqualTo(directory));
        }
        ModelState SelectionCurrentFixture()
        {
            var s=ModelState.Fresh(1);s.coins=640;s.houseStage=1;s.autoHintSeen=1;
            var fixture=Fixture(s);fixture.SelectChapterProject("cart.build",fixture.State.sequence+1);fixture.Checkpoint(1000);
            Assert.That(fixture.State.coins,Is.EqualTo(500));return fixture.State;
        }
        string SelectionFixtureFile(params string[] parts)
        {return File.ReadAllText(Path.GetFullPath(Path.Combine(new[]{Application.dataPath,"..","..","Checks"}.Concat(parts).ToArray())));}

        [UnityTest]
        public IEnumerator SelectionGatePausedCallbacksPreserveCurrentAndHistoricalPromises()
        {
            var old=ModelState.Fresh(1);old.coins=550;old.houseStage=1;old.selectedStage=2;old.autoHintSeen=1;old.lastSettledAt=1000;
            var cases=new[]{
                new{label="current480",raw=codec.Encode(SelectionCurrentFixture()),yard=0,pinned=0L,afterCoins=20L,afterHouse=2,afterSelected=(string)null},
                new{label="constructed selected600",raw=codec.Encode(old),yard=0,pinned=600L,afterCoins=70L,afterHouse=2,afterSelected=(string)null},
                new{label="genuine selected540",raw=SelectionFixtureFile("ConstructionPricing","evidence","cadence-r12","v2","v2-selected540.json"),yard=0,pinned=540L,afterCoins=55L,afterHouse=2,afterSelected=(string)null},
                new{label="genuine frozen600 recovery",raw=SelectionFixtureFile("EraMigration","Fixtures","r6","pending-finish-house-spin.json"),yard=1,pinned=0L,afterCoins=4L,afterHouse=2,afterSelected="workshop.improve"}
            };
            foreach(var item in cases)
            {
                Clock(1000);yield return SelectionLoadRaw(item.raw);
                if(Field<GameObject>("panel").activeSelf){Assert.That(Field<bool>("autoHintPanel"),Is.True);yield return Click("closeButton");}
                if(item.pinned>0)Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(item.pinned));
                yield return Click("yardButtons",item.yard);var queued=Field<Button>("selectButton").onClick;
                Assert.That(Field<Button>("selectButton").gameObject.activeInHierarchy,Is.True);
                Clock(1001);Call("OnApplicationPause",true);Assert.That(Flag("InBackground"),Is.True);
                string checkpoint=codec.Encode(Session.State);var files=SelectionFiles();uint seed=Session.State.seed;long coins=Session.State.coins;
                queued.Invoke();SelectionUnchanged(checkpoint,files);
                Clock(1002);Call("OnApplicationPause",false);Assert.That(Flag("InBackground"),Is.False);SelectionHealthy();Assert.That(Session.State.coins,Is.EqualTo(coins));
                yield return Click("selectButton");Assert.That(Session.State.coins,Is.EqualTo(item.afterCoins),item.label);
                Assert.That(Session.State.houseStage,Is.EqualTo(item.afterHouse));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(item.afterSelected));Assert.That(Session.State.seed,Is.EqualTo(seed));Saved();
                yield return NarrativeUnload();
            }
            TestContext.WriteLine("Four actual visible previews, injected queued callbacks after programmatic pause: full checkpoint state/files/timestamps exact; real resume+pointer still selects/pays once. Current480, constructed600, genuine540 and frozen600 recovery remain literal. Not OS input.");
        }

        [UnityTest]
        public IEnumerator SelectionGateClosedHiddenAndPausedCloseCallbacksAreReadOnly()
        {
            yield return SelectionLoadRaw(codec.Encode(SelectionCurrentFixture()));yield return Click("yardButtons",0);var oldSelect=Field<Button>("selectButton").onClick;
            yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            string before=codec.Encode(Session.State);var files=SelectionFiles();oldSelect.Invoke();Call("ClosePanel");SelectionUnchanged(before,files);yield return NarrativeUnload();
            var s=ModelState.Fresh(1);s.coins=200;s.houseStage=1;s.workshopBuilt=1;s.autoHintSeen=1;s.lastSettledAt=1000;
            yield return SelectionLoadRaw(codec.Encode(s));yield return Click("yardButtons",0);oldSelect=Field<Button>("selectButton").onClick;
            Clock(1001);Call("OnApplicationPause",true);Clock(1601);Call("OnApplicationPause",false);yield return null;
            Assert.That(Session.State.coins,Is.EqualTo(210));Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);Assert.That(Field<Button>("selectButton").gameObject.activeInHierarchy,Is.False);
            before=codec.Encode(Session.State);files=SelectionFiles();oldSelect.Invoke();SelectionUnchanged(before,files);
            var queuedClose=Field<Button>("closeButton").onClick;Clock(1602);Call("OnApplicationPause",true);
            before=codec.Encode(Session.State);files=SelectionFiles();long seen=Session.State.lastSettlementSeen;queuedClose.Invoke();oldSelect.Invoke();SelectionUnchanged(before,files);
            Assert.That(Session.State.lastSettlementSeen,Is.EqualTo(seen));Assert.That(Field<GameObject>("panel").activeSelf,Is.True);
            Clock(1603);Call("OnApplicationPause",false);SelectionHealthy();yield return Back();
            Assert.That(Session.State.coins,Is.EqualTo(210));Assert.That(Session.State.lastSettlementSeen,Is.EqualTo(Session.State.lastSettlement.sequence));
            Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Saved();
            TestContext.WriteLine("Actual project pointer preview followed by Back-adapter close discards old Select callback; hidden select during return summary and paused Close cannot mutate/ACK/write. Foreground Back still acknowledges the one10coin summary, without hidden construction.");
        }

        [UnityTest]
        public IEnumerator SelectionGateRecoveryKeepsNullStateWarningCancelConfirmAndHiddenReturnSafe()
        {
            var good=ModelState.Fresh(1);good.coins=100;good.houseStage=1;good.workshopBuilt=1;good.autoHintSeen=1;good.lastSettledAt=400;
            File.WriteAllText(Path.Combine(directory,"jtn.backup.v1.json"),codec.Encode(good));yield return SelectionLoadRaw("{broken",true);
            Assert.That(Session.State,Is.Null);Assert.That(Session.BackupAvailable,Is.True);var queued=Field<Button>("selectButton").onClick;var files=SelectionFiles();
            Clock(1001);Call("OnApplicationPause",true);queued.Invoke();Assert.That(Field<bool>("restoreConfirmation"),Is.False);Assert.That(SelectionFiles(),Is.EquivalentTo(files));
            Clock(1002);Call("OnApplicationPause",false);yield return Click("selectButton");Assert.That(Field<bool>("restoreConfirmation"),Is.True);Assert.That(Session.State,Is.Null);Assert.That(SelectionFiles(),Is.EquivalentTo(files));
            var warning=Field<TMPro.TMP_Text>("panelBody");
            Assert.That(warning.text,Does.Contain("The current file and backup will be kept separately.").And.Contain("Recent progress may be lost.").And.Not.Contain("Corrupted file"));
            NarrativeBounds(warning);Assert.That(warning.isTextOverflowing,Is.False);Assert.That(Session.State,Is.Null);Assert.That(SelectionFiles(),Is.EquivalentTo(files));
            yield return Click("reloadButton");Assert.That(Field<bool>("restoreConfirmation"),Is.False);Assert.That(Session.RecoveryRequired,Is.True);Assert.That(SelectionFiles(),Is.EquivalentTo(files));
            yield return Click("selectButton");yield return Click("selectButton");SelectionHealthy();Assert.That(Session.State.coins,Is.EqualTo(110));
            Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);Assert.That(Field<Button>("selectButton").gameObject.activeInHierarchy,Is.False);
            string restored=codec.Encode(Session.State);files=SelectionFiles();queued.Invoke();SelectionUnchanged(restored,files);
            yield return Click("closeButton");yield return Click("yardButtons",0);yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(110));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(GameIds.HouseImprove));SelectionHealthy();Saved();
            TestContext.WriteLine("Actual corrupt primary/valid backup has StateNULL. Paused recovery callback refuses; foreground warning/cancel/confirm restore still works. Hidden post-restore Select cannot mutate summary; later foreground project selection remains usable.");
            TestContext.WriteLine("The generic current-file warning retains rollback/separate-preservation wording and existing portrait bounds. This connected fixture is corrupt-primary consent coverage; the valid blocked-primary write-failure cause is qualified separately by physical storage checks.");
        }

        [UnityTest]
        public IEnumerator SelectionGateForegroundGuideAndShopRemainUsable()
        {
            yield return SelectionLoadRaw(codec.Encode(SelectionCurrentFixture()));long completion=Session.Guidance.LastCompletion.Sequence;
            yield return Click("guideButton");yield return Click("selectButton");Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.EqualTo(completion));Assert.That(Session.State.coins,Is.EqualTo(500));
            yield return Click("productTabs",4);yield return Click("productCards",3);yield return Click("navigationCards",0);
            var queued=Field<Button>("selectButton").onClick;yield return Click("selectButton");Assert.That(Session.State.coins,Is.EqualTo(380));Assert.That(Session.Chapter.Skills.Owns(GameIds.SlotCoinBoost),Is.True);
            string committed=codec.Encode(Session.State);var files=SelectionFiles();queued.Invoke();SelectionUnchanged(committed,files);Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            yield return Back();Assert.That(Field<bool>("exitPanel"),Is.True);Assert.That(Field<Button>("selectButton").isActiveAndEnabled&&Field<Button>("selectButton").IsInteractable(),Is.True);
            yield return Back();Assert.That(Field<bool>("exitPanel"),Is.False);SelectionUnchanged(committed,files);Saved();
            TestContext.WriteLine("Real foreground guide ACK and V3 universal120 shop purchase remain usable. Repeated old action callback has no second charge/write/error. Root Back still exposes actionable exit confirmation and foreground Back cancels without writes; actual application quit is not invoked.");
        }

        [UnityTest]
        public IEnumerator SelectionGateHiddenReloadDefersIncomeButVisibleRetryAndTrustedLoaderStillWork()
        {
            var good=ModelState.Fresh(1);good.coins=100;good.houseStage=1;good.workshopBuilt=1;good.autoHintSeen=1;good.lastSettledAt=1000;
            Clock(1000);yield return SelectionLoadRaw(codec.Encode(good));var queued=Field<Button>("reloadButton").onClick;
            Assert.That(Field<Button>("reloadButton").gameObject.activeInHierarchy,Is.False);
            Clock(1001);Call("OnApplicationPause",true);Clock(1601);string checkpoint=codec.Encode(Session.State);var files=SelectionFiles();queued.Invoke();SelectionUnchanged(checkpoint,files);Assert.That(Session.State.coins,Is.EqualTo(100));
            Call("OnApplicationPause",false);yield return null;Assert.That(Session.State.coins,Is.EqualTo(110));Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);
            string summary=codec.Encode(Session.State);files=SelectionFiles();queued.Invoke();SelectionUnchanged(summary,files);
            yield return Click("closeButton");string acknowledged=codec.Encode(Session.State);files=SelectionFiles();Call("Reload");yield return null;SelectionUnchanged(acknowledged,files);
            yield return NarrativeUnload();Clock(1000);File.WriteAllText(Path.Combine(directory,"jtn.backup.v1.json"),codec.Encode(good));yield return SelectionLoadRaw("{broken",true);
            Assert.That(Session.State,Is.Null);Assert.That(Field<Button>("reloadButton").isActiveAndEnabled&&Field<Button>("reloadButton").IsInteractable(),Is.True);
            File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),codec.Encode(good));yield return Click("reloadButton");SelectionHealthy();Assert.That(Session.State.coins,Is.EqualTo(100));Saved();
            TestContext.WriteLine("Hidden UI Retry after pause cannot reload/credit income; foreground resume gives exactly10 once. Hidden Retry on summary stays read-only. Direct trusted Reload still works with panel closed; actual visible foreground Retry heals corrected primary while prior StateNULL, without a fresh grant.");
        }

        ModelState MilestoneEraFixture(string era,int[] levels)
        {
            // Explicit synthetic validated ownership, not a simulated full chapter run.
            var state=ModelState.Fresh(1);state.coins=100;state.lastSettledAt=1000;
            var stages=new FamilyStages(era,FamilyIds.All.Select((id,i)=>new FamilyStage(id,levels[i])));
            var progress=ChapterCatalog.Rules.FromStages(stages);long entry=era==EraIds.WorkshopYard?13:0;
            state.sequence=Math.Max(progress.CompletedUpgradeIds.Count,entry);state.revision=state.sequence*2;
            state.houseStage=Math.Min(levels[0],2);state.autoHintSeen=state.houseStage>0?1:0;state.workshopBuilt=levels[1]>0?1:0;
            state.wave=new WaveProgress(levels[2]>0?1:0,levels[3]>0?1:0,WaveProgress.Empty.Delivery,JunkToNeon.Upgrades.UpgradeOwnership.Empty);
            state.chapter=new ChapterState(2,JunkToNeon.Content.ChapterContentIds.RuleRevision,progress,JunkToNeon.Upgrades.SkillOwnership.Empty,null,entry,entry);
            GameLogic.Validate(state);return state;
        }
        string MilestoneInput(string name)=>File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/Milestones/fixtures",name)));
        void MilestoneBody(params string[] expected)
        {
            var body=Field<TMPro.TMP_Text>("panelBody");
            foreach(string text in expected)Assert.That(body.text,Does.Contain(text));
            NarrativeBounds(body);Assert.That(body.isTextOverflowing,Is.False,"Milestone explanation must fit the existing portrait body");
        }
        IEnumerator MilestoneOpenEraReadOnly(params string[] expected)
        {
            string before=codec.Encode(Session.State);var files=SelectionFiles();
            // The era explanation is reached through District > Buildings, so three Backs return to the root.
            yield return Click("productTabs",0);yield return Click("productCards",0);yield return Click("navigationEraButton");MilestoneBody(expected);
            SelectionUnchanged(before,files);yield return Back();yield return Back();yield return Back();SelectionUnchanged(before,files);
            Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
        }

        [UnityTest]
        public IEnumerator MilestoneExistingExplanationReadsFreshPartialAndSavedPricesWithoutWrites()
        {
            yield return Load(MilestoneEraFixture(EraIds.Junkyard,new[]{0,0,0,0,0,0}));
            yield return MilestoneOpenEraReadOnly("Buildings: 0/24","house repaired no","chapter no","inspect the house");yield return NarrativeUnload();
            yield return Load(MilestoneEraFixture(EraIds.Junkyard,new[]{2,0,0,0,0,0}));
            yield return MilestoneOpenEraReadOnly("Buildings: 2/24","house repaired yes","Workshop no","Junkyard no","chapter no");yield return NarrativeUnload();
            var old=ModelState.Fresh(1);old.coins=535;old.houseStage=1;old.selectedStage=2;old.autoHintSeen=1;
            foreach(var item in new[]{new{raw=codec.Encode(old),price="535/600"},new{raw=MilestoneInput("v2-selected540.json"),price="535/540"}})
            {
                var state=codec.Decode(item.raw);state.lastSettledAt=1000;yield return Load(state);
                yield return MilestoneOpenEraReadOnly("Buildings: 1/24",item.price,"at its saved price","reselect only explicitly");
                Assert.That(Session.State.coins,Is.EqualTo(535));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(GameIds.HouseImprove));yield return NarrativeUnload();
            }
            TestContext.WriteLine("Actual existing menu pointer opens plus Back preserve full encoded state and file bytes/inventory/timestamps. Zero, house2-only, literal600 and genuine540 explanations fit; no selection, repricing or reward. Simulated pointer, not OS/device input.");
        }

        [UnityTest]
        public IEnumerator MilestoneFreeEraEntryStaysTwelveAndPendingExplanationWinsUntilAcknowledged()
        {
            yield return Load(MilestoneEraFixture(EraIds.Junkyard,new[]{2,2,2,2,2,2}));
            yield return MilestoneOpenEraReadOnly("Buildings: 12/24","Junkyard yes","chapter no","free");
            yield return Click("productTabs",0);yield return Click("productCards",0);yield return Click("navigationEraButton");long coins=Session.State.coins;uint seed=Session.State.seed;
            yield return Click("selectButton");Assert.That(Session.Chapter.TransitionPending,Is.True);
            MilestoneBody("Buildings: 12/24","Era entry saved","Continue it first","chapter no");
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("choose one building"));
            string pending=codec.Encode(Session.State);var files=SelectionFiles();Call("Reload");yield return null;
            SelectionUnchanged(pending,files);MilestoneBody("Buildings: 12/24","Continue it first");
            yield return Click("closeButton");Assert.That(Session.Chapter.TransitionPending,Is.False);
            Assert.That(Session.State.coins,Is.EqualTo(coins));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(12));
            yield return MilestoneOpenEraReadOnly("Buildings: 12/24","Junkyard yes","chapter no","choose one building");Saved();
            TestContext.WriteLine("Actual free era confirmation and skip/ack preserve12 owned purchases, wallet and RNG. Pending Reload shows continue-entry priority; subsequent read-only era open offers a construction without auto-selection.");
        }

        [UnityTest]
        public IEnumerator MilestoneFinalPurchaseAndEarlierReloadDeriveCurrentFactsWithoutCache()
        {
            var state=codec.Decode(MilestoneInput("late23-skills6.json"));state.lastSettledAt=1000;yield return Load(state);
            yield return MilestoneOpenEraReadOnly("Buildings: 23/24","chapter no");long coins=Session.State.coins;uint seed=Session.State.seed;
            Call("ShowChapterFamily",FamilyIds.PowerSource,false);yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(coins-700));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(24));
            string complete=codec.Encode(Session.State);var files=SelectionFiles();
            yield return Click("productGoalButton");
            MilestoneBody("Buildings: 24/24","chapter yes","You may finish here","optional","all skills owned");
            yield return Back();SelectionUnchanged(complete,files);
            var earlier=codec.Decode(MilestoneInput("legacy-complete.json"));earlier.lastSettledAt=1000;
            File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),codec.Encode(earlier));Call("Reload");yield return null;
            yield return MilestoneOpenEraReadOnly("Buildings: 3/24","Workshop yes","Junkyard no","chapter no");yield return NarrativeUnload();
            yield return Load(MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,5}));
            complete=codec.Encode(Session.State);files=SelectionFiles();yield return Click("productGoalButton");
            MilestoneBody("Buildings: 24/24","chapter yes","optional");Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("all skills owned"));yield return Back();SelectionUnchanged(complete,files);
            TestContext.WriteLine("One real final700 construction only; no full chapter replay. Final24 is independent of0/6 or6/6 skills. Existing postchapter explanation remains read-only; actual Reload into earlier genuine history lowers facts to3/24 with no cached achievement history.");
        }

        [UnityTest]
        public IEnumerator ConstructionFundedChoiceExplainsOneImmediateDebitBeforePointer()
        {
            // Explicit funded fresh-state wallet variant; no spin/history is fabricated.
            var fresh=MilestoneEraFixture(EraIds.Junkyard,new[]{0,0,0,0,0,0});fresh.coins=45;
            foreach(var item in new[]{
                new{state=fresh,card=0,text="Builds now. Pay 40: 45 → 5 Coins.",caption="Build for 40 Coins",coins=5L,workshop=0,replacement=false},
                new{state=codec.Decode(MilestoneInput("v2-selected540.json")),card=1,text="Builds now. Pay 120: 535 → 415 Coins.",caption="Build for 120 Coins",coins=415L,workshop=1,replacement=true}})
            {
                yield return Load(item.state);string before=codec.Encode(Session.State);var files=SelectionFiles();
                long sequence=Session.State.sequence;uint seed=Session.State.seed;
                yield return Click("productTabs",0);yield return Click("productCards",0);yield return Click("navigationCards",item.card);MilestoneBody(item.text);
                var actionLabel=Field<Button>("selectButton").GetComponentInChildren<TMPro.TMP_Text>();
                Assert.That(actionLabel.text,Is.EqualTo(item.caption));NarrativeBounds(actionLabel);
                if(item.replacement)Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Replaces the current goal."));
                SelectionUnchanged(before,files);yield return Back();SelectionUnchanged(before,files);
                Assert.That(Field<TMPro.TMP_Text>("panelTitle").text,Is.EqualTo("Buildings · all six"));
                yield return Click("navigationCards",item.card);MilestoneBody(item.text);var queued=Field<Button>("selectButton").onClick;
                yield return Click("selectButton");
                Assert.That(Session.State.coins,Is.EqualTo(item.coins));Assert.That(Session.State.houseStage,Is.EqualTo(1));
                Assert.That(Session.State.workshopBuilt,Is.EqualTo(item.workshop));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
                Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Saved();
                string committed=codec.Encode(Session.State);files=SelectionFiles();queued.Invoke();SelectionUnchanged(committed,files);
                yield return NarrativeUnload();
            }
            TestContext.WriteLine("Existing pointers: funded45/40 explains5 before one debit; replacing genuine saved540 with workshop120 explains415, keeps house1 and leaves no successor. Preview/Back and stale callbacks preserve full state/files; no price/effect change.");
        }

        [UnityTest]
        public IEnumerator ConstructionInsufficientReplacementExplainsNoDebitAndSelectedStatusHasNoAction()
        {
            var synthetic=codec.Decode(MilestoneInput("v2-selected540.json"));
            // Explicit private100-wallet variant; retain the original file and no frozen receipt rewrite.
            Assert.That(synthetic.pending,Is.Null);Assert.That(synthetic.lastSpin,Is.Null);synthetic.coins=100;GameLogic.Validate(synthetic);
            yield return Load(synthetic);string before=codec.Encode(Session.State);var files=SelectionFiles();
            long sequence=Session.State.sequence;uint seed=Session.State.seed;
            yield return Click("yardButtons",1);MilestoneBody("Replaces the current goal.","Select without spending: 100 → 100 Coins. Need 20 more.","Pays once on a spin or selection when you have enough Coins.");
            SelectionUnchanged(before,files);yield return Back();SelectionUnchanged(before,files);
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
            yield return Click("yardButtons",1);var queued=Field<Button>("selectButton").onClick;yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(100));Assert.That(Session.State.houseStage,Is.EqualTo(1));Assert.That(Session.State.workshopBuilt,Is.EqualTo(0));
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(GameIds.WorkshopBuild));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(120));
            Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Assert.That(Session.State.seed,Is.EqualTo(seed));Saved();
            string selected=codec.Encode(Session.State);files=SelectionFiles();queued.Invoke();SelectionUnchanged(selected,files);
            yield return Click("yardButtons",1);MilestoneBody("Goal selected. Need 20 Coins more. No spending now.","Pays once on a spin or selection when you have enough Coins.");
            Assert.That(Field<Button>("selectButton").gameObject.activeSelf,Is.False);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("Select without spending").And.Not.Contain("Replaces the current goal"));
            queued.Invoke();SelectionUnchanged(selected,files);yield return Back();SelectionUnchanged(selected,files);
            TestContext.WriteLine("Synthetic100 replaces old540 with120 once without debit; later same selected-unfunded preview shows remaining20 and no actionable selection. Actual Back and stale hidden callback preserve state/files/RNG; no foreground-spin rule changed.");
        }

        [UnityTest]
        public IEnumerator ConstructionLockedPrerequisiteRemainsNonactionableAndReadOnly()
        {
            var state=MilestoneEraFixture(EraIds.Junkyard,new[]{0,0,0,0,0,0});yield return Load(state);
            string before=codec.Encode(Session.State);var files=SelectionFiles();
            yield return Click("yardButtons",1);MilestoneBody("Repair the house first.");
            Assert.That(Field<Button>("selectButton").gameObject.activeSelf,Is.False);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("Builds now").And.Not.Contain("Select without spending"));
            Field<Button>("selectButton").onClick.Invoke();SelectionUnchanged(before,files);
            yield return Back();SelectionUnchanged(before,files);Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            TestContext.WriteLine("Existing locked workshop preview retains actual repair prerequisite, hidden action and unchanged full state/files despite a queued callback. Text fits existing bounds.");
        }

        [UnityTest,Category("Expansion07"),Category("Expansion5h")]
        public IEnumerator ReopenedConfirmationHeldActualTouchCannotAuthorizeAnotherProject()
        {
            retainTouchProof=true;GameSession setup=null;
            try
            {
                var state=ModelState.Fresh(1);state.coins=1000;state.lastSettledAt=1000;
                setup=Fixture(state);setup.SelectChapterProject(GameIds.HouseRepair,setup.State.sequence+1,1000);
                setup.AcknowledgeAutoHint(setup.State.sequence+1,1000);GameLogic.Validate(setup.State);
            }
            catch(Exception error){Assert.Inconclusive("Private Core fixture setup: "+error.GetType().Name);}
            if(setup==null||setup.State.coins!=960||setup.State.houseStage!=1||setup.State.workshopBuilt!=0||setup.Chapter.SelectedProjectId!=null)
                Assert.Inconclusive("Private actual repair40 fixture did not reach960/house1/workshop0/no target");
            yield return ReopenedActionTouchProof.LoadGuarded(Load(setup.State));
            yield return ReopenedActionTouchProof.Run(game,directory);
        }

        [UnityTest]
        public IEnumerator SharedActionPointerConsentSeparatesTouchIdsAndDoesNotPoisonSubmit()
        {
            yield return Load(ModelState.Fresh(1));
            Call("ShowChapterFamily",FamilyIds.House,false);
            var button=Field<Button>("selectButton");
            var consent=button.GetComponents<Component>().Single(c=>c.GetType().FullName=="JunkToNeon.JtnPanelPointerConsent");
            var down=(IPointerDownHandler)consent;var up=(IPointerUpHandler)consent;
            Action invalidate=()=>consent.GetType().GetMethod("Invalidate").Invoke(consent,null);
            Func<bool> allow=()=>(bool)consent.GetType().GetMethod("AllowInvocation").Invoke(consent,null);
            Func<int,PointerEventData> pointer=id=>new PointerEventData(EventSystem.current){pointerId=id,
                button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerClick=button.gameObject,
                pointerCurrentRaycast=new RaycastResult{gameObject=button.gameObject}};
            string before=codec.Encode(Session.State);var files=SelectionFiles();
            var stale=pointer(101);down.OnPointerDown(stale);invalidate();
            var fresh=pointer(102);down.OnPointerDown(fresh);
            up.OnPointerUp(stale);Assert.That(allow(),Is.False,"Old contact must not consume another contact's current consent");
            Assert.That(allow(),Is.True,"Consumed old release must not poison a fresh Submit in the same frame");
            up.OnPointerUp(fresh);Assert.That(allow(),Is.True,"Fresh independent contact must retain its own consent");
            foreach(var kind in new[]{PointerEventData.InputButton.Right,PointerEventData.InputButton.Middle})
            {
                var other=pointer(103);other.button=kind;down.OnPointerDown(other);up.OnPointerUp(other);
                Assert.That(allow(),Is.True,"Non-left releases must not veto Submit");
            }
            var outside=pointer(104);down.OnPointerDown(outside);invalidate();
            outside.pointerCurrentRaycast=new RaycastResult();up.OnPointerUp(outside);
            Assert.That(allow(),Is.True,"Outside release is not an onClick candidate and must not leave a rejection");
            var canceled=pointer(105);down.OnPointerDown(canceled);invalidate();canceled.eligibleForClick=false;up.OnPointerUp(canceled);
            Assert.That(allow(),Is.True,"Canceled/drag release must not leave a rejection for Submit");
            var disabled=pointer(106);down.OnPointerDown(disabled);button.gameObject.SetActive(false);button.gameObject.SetActive(true);
            up.OnPointerUp(disabled);Assert.That(allow(),Is.False,"Disable/re-enable must invalidate a held contact");
            var same=pointer(107);down.OnPointerDown(same);up.OnPointerUp(same);Assert.That(allow(),Is.True,"Unchanged-context left click must remain valid");
            var mouse=pointer(-1);down.OnPointerDown(mouse);up.OnPointerUp(mouse);Assert.That(allow(),Is.True,"Fresh mouse left release must remain valid");
            SelectionUnchanged(before,files);Saved();
            TestContext.WriteLine("Observer identity checks only: separate stale/current contacts, consumed rejection versus Submit, outside/canceled/right/middle releases, disable invalidation and fresh mouse. Actual InputSystem behavior is the separate held-touch case.");
        }
        [UnityTest]
        public IEnumerator SharedActionSwipeCancelsTapWhileJitterStillSelectsTheGoal()
        {
            yield return Load(ModelState.Fresh(1));
            Call("ShowChapterFamily",FamilyIds.House,false);
            var button=Field<Button>("selectButton");var rect=(RectTransform)button.transform;
            Canvas.ForceUpdateCanvases();var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var moved=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center+new Vector2(rect.rect.width*.3f,0)));
            Assert.That(Vector2.Distance(center,moved),Is.GreaterThan(EventSystem.current.pixelDragThreshold*2));
            Func<int,PointerEventData> pointer=id=>new PointerEventData(EventSystem.current){pointerId=id,
                button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerClick=button.gameObject,pointerPress=button.gameObject,
                pointerCurrentRaycast=new RaycastResult{gameObject=button.gameObject},position=center};
            Action<PointerEventData> down=p=>ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerDownHandler);
            Action<PointerEventData> move=p=>ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerMoveHandler);
            Action<PointerEventData> release=p=>{ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerClickHandler);};
            string before=codec.Encode(Session.State);var files=SelectionFiles();
            var swipe=pointer(1101);down(swipe);swipe.position=moved;move(swipe);release(swipe);
            SelectionUnchanged(before,files);
            var returned=pointer(1102);down(returned);returned.position=moved;move(returned);returned.position=center;move(returned);release(returned);
            SelectionUnchanged(before,files);
            var sparse=pointer(1103);down(sparse);sparse.position=moved;release(sparse);
            SelectionUnchanged(before,files);
            var jitter=pointer(1104);down(jitter);jitter.position=center+Vector2.right*Mathf.Min(1,EventSystem.current.pixelDragThreshold*.25f);move(jitter);release(jitter);
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(GameIds.HouseRepair));
            Assert.That(Session.State.sequence,Is.EqualTo(codec.Decode(before).sequence+1));
            Assert.That(Session.State.coins,Is.EqualTo(codec.Decode(before).coins));Saved();
            TestContext.WriteLine("Simulated pointer events: long in-button swipe, out-and-back and sparse release preserve state/files; jitter permits one fresh explicit goal selection without spending.");
        }
        [UnityTest]
        public IEnumerator RestoreConfirmationActualTouchRequiresNewConsentWhileStateIsNull()
        {
            retainTouchProof=true;
            // Explicit synthetic valid backup: no historical price/receipt is edited.
            var good=ModelState.Fresh(123);good.coins=73;good.houseStage=1;good.autoHintSeen=1;good.lastSettledAt=1000;
            string backup=codec.Encode(good);File.WriteAllText(Path.Combine(directory,"jtn.backup.v1.json"),backup);
            yield return ReopenedActionTouchProof.LoadGuarded(SelectionLoadRaw("{private restore proof",true));
            Assert.That(Session.State,Is.Null);Assert.That(Session.BackupAvailable,Is.True);
            yield return ReopenedActionTouchProof.RunRecovery(game,directory,backup);
        }
        [UnityTest]
        public IEnumerator FreshKeyboardSubmitUsesInstalledActionAndPaysWorkshopOnce()
        {
            retainTouchProof=true;
            var state=ModelState.Fresh(1);state.coins=1000;state.lastSettledAt=1000;
            var setup=Fixture(state);setup.SelectChapterProject(GameIds.HouseRepair,setup.State.sequence+1,1000);
            setup.AcknowledgeAutoHint(setup.State.sequence+1,1000);
            yield return ReopenedActionTouchProof.LoadGuarded(Load(setup.State));
            // Legal existing panel setup; only confirmation is claimed actual input.
            Call("ShowChapterFamily",FamilyIds.Workshop,false);
            yield return ReopenedActionTouchProof.RunSubmit(game,directory);
        }
        IEnumerator ProductOrderDetail()
        {
            yield return Click("productTabs",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));
            yield return Click("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
            yield return Click("navigationCards",0);
        }
        string postchapterObservedBody;
        IEnumerator ProductEraReadOnly(params string[] expected)
        {
            string before=codec.Encode(Session.State);var files=SelectionFiles();
            yield return Click("productTabs",0);yield return Click("productCards",0);Assert.That(Field<int>("wavePanel"),Is.EqualTo(30));
            yield return Click("navigationEraButton");MilestoneBody(expected);postchapterObservedBody=Field<TMPro.TMP_Text>("panelBody").text;SelectionUnchanged(before,files);
            yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(30));SelectionUnchanged(before,files);
            yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(70));SelectionUnchanged(before,files);
            yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);SelectionUnchanged(before,files);
        }
        [UnityTest]
        public IEnumerator OrdersGuidanceChooseContinueClaimReadsActualProgressAndAckStaysFinite()
        {
            // Explicit validated final ownership with0/6 skills, not a UI chapter traversal.
            var setup=Fixture(MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,5}));
            Assert.That(setup.Chapter.Skills.Receipts.Count,Is.Zero);Assert.That(setup.Guidance.LastCompletion,Is.Null);
            Assert.That(setup.Guidance.Suggestion.HintId,Is.EqualTo(GuideIds.ChapterEnd));
            setup.AcknowledgeGuidance(GuideIds.ChapterEnd,setup.State.sequence+1,0,1000);
            Assert.That(setup.Guidance.Suggestion.HintId,Is.EqualTo(GuideIds.Orders));var choose=setup.State;
            setup.AcceptOrder("order.sort-salvage",setup.State.sequence+1,1000);
            // Existing independent seed1 trace:15 spins =>11 trash/+352, next =>12 capped/+20.
            for(int spin=0;spin<15;spin++)setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
            Assert.That(setup.Journey.Orders.Progress,Is.EqualTo(11));Assert.That(setup.Journey.Orders.Ready,Is.False);
            Assert.That(setup.State.coins,Is.EqualTo(452));Assert.That(setup.State.seed,Is.EqualTo(82514103u));var active=setup.State;
            setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
            Assert.That(setup.Journey.Orders.Progress,Is.EqualTo(12));Assert.That(setup.Journey.Orders.Ready,Is.True);
            Assert.That(setup.State.coins,Is.EqualTo(472));var ready=setup.State;

            IEnumerator ReadHint(string title,params string[] fragments)
            {
                Assert.That(Field<string>("shownGuide"),Is.EqualTo(GuideIds.Orders));
                var heading=Field<TMPro.TMP_Text>("heading");Assert.That(heading.text,Is.EqualTo(title));
                NarrativeBounds(heading);Assert.That(heading.isTextOverflowing,Is.False);
                string before=codec.Encode(Session.State);var files=SelectionFiles();
                yield return Click("guideButton");MilestoneBody(fragments);SelectionUnchanged(before,files);
                yield return Back();SelectionUnchanged(before,files);Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            }
            yield return Load(choose);
            yield return ReadHint("Orders are optional. Choose one.","Choose one order.","Only new ordinary spins");
            Assert.That(Session.State.coins,Is.EqualTo(100));Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);yield return NarrativeUnload();
            yield return Load(active);
            yield return ReadHint("Order in progress. Keep making ordinary spins.","Progress: 11/12.","Make ordinary spins for this order.","Finish or explicitly cancel it before taking another");
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("Choose one order").And.Not.Contain("Collect reward"));yield return NarrativeUnload();
            yield return Load(ready);
            yield return ReadHint("Order ready. Collect the reward.","Progress: 12/12.","press Collect reward","Rewards are not automatic");
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("Make ordinary spins").And.Not.Contain("Choose one order"));
            long wallet=Session.State.coins,lastSpin=Session.State.lastSpin.sequence;uint seed=Session.State.seed;
            yield return Click("guideButton");yield return Click("selectButton");
            Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.Orders),Is.True);
            Assert.That(Session.State.coins,Is.EqualTo(wallet));Assert.That(Session.Journey.Orders.Ready,Is.True);
            Assert.That(Field<string>("shownGuide"),Is.Not.EqualTo(GuideIds.Orders));
            // Ready needs no new spin: use only the existing current-order Claim action.
            yield return ProductOrderDetail();
            Assert.That(Field<Button>("selectButton").GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("Collect reward"));
            MilestoneBody("Progress: 12/12.","Reward: 120");yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(592));Assert.That(Session.State.coins,Is.EqualTo(wallet+120));
            Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(lastSpin));
            Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);Assert.That(Session.Journey.Orders.LatestClaim.RewardCoins,Is.EqualTo(120));Saved();
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
            Assert.That(Field<TMPro.TMP_Text>("navigationContext").text,Does.Contain("+120 Coins").And.Contain(ChapterNarrative.OrderClaimed("order.sort-salvage")));
            Assert.That(Field<TMPro.TMP_Text>("navigationContext").isActiveAndEnabled,Is.True);
            string claimed=codec.Encode(Session.State);var claimedFiles=SelectionFiles();Call("Reload");yield return null;SelectionUnchanged(claimed,claimedFiles);
            Assert.That(Field<string>("shownGuide"),Is.Not.EqualTo(GuideIds.Orders));
            yield return ProductOrderDetail();yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(592));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(lastSpin));
            Assert.That(Session.Journey.Orders.Progress,Is.Zero);Assert.That(Session.Journey.Orders.ActiveOrderId,Is.EqualTo("order.sort-salvage"));
            Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.Orders),Is.True);Assert.That(Field<string>("shownGuide"),Is.Not.EqualTo(GuideIds.Orders));Saved();
            TestContext.WriteLine("Final0/6-skills synthetic ownership,16 bounded memory spins only. Existing pointer/Back guide opens show Choose/11of12 Continue/12of12 Claim and preserve full state/files/mtime; all existing portrait text fits. ACK stays finite after claim/Reload/new acceptance; ready claims120 once without a UI spin, RNG or last-spin change. Not OS/device input.");
        }

        [UnityTest]
        public IEnumerator OptionalSkillAchievementAppearsBeforeFinalAndEarlierReloadClearsIt()
        {
            var earlier=MilestoneEraFixture(EraIds.Junkyard,new[]{0,0,0,0,0,0});
            yield return Load(earlier);yield return ProductEraReadOnly("Buildings: 0/24");
            Assert.That(postchapterObservedBody,Does.Not.Contain("In this save:").And.Not.Contain("all skills").And.Not.Contain("order reward").And.Not.Match(@"(?<!\d)0/2(?!\d)"));
            yield return NarrativeUnload();
            var six=codec.Decode(MilestoneInput("late23-skills6.json"));
            Assert.That(six.chapter.Skills.Receipts.Count,Is.EqualTo(6));Assert.That(six.chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(23));
            yield return Load(six);
            yield return ProductEraReadOnly("Buildings: 23/24","chapter no","In this save: all skills owned.");
            Assert.That(postchapterObservedBody,Does.Not.Contain("order reward").And.Not.Match(@"(?<!\d)0/2(?!\d)"));
            string oldRaw=codec.Encode(earlier);File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),oldRaw);var files=SelectionFiles();
            Call("Reload");yield return null;SelectionUnchanged(oldRaw,files);
            yield return ProductEraReadOnly("Buildings: 0/24");
            Assert.That(postchapterObservedBody,Does.Not.Contain("In this save:").And.Not.Contain("all skills").And.Not.Contain("order reward"));
            Saved();
            TestContext.WriteLine("Existing era-info pointer opens/Back are read-only with real TMP bounds. No optional header when fresh; genuine six skills show an earned-only label at23/24; actual Reload to earlier valid zero snapshot removes it without a cross-save cache. No chapter traversal or UI redesign.");
        }

        [UnityTest]
        public IEnumerator OptionalClaimAchievementRequiresPaymentRetainsNewInstanceAndBothEarnedTextFits()
        {
            IEnumerator ReadChapterInfo(params string[] expected)
            {
                string before=codec.Encode(Session.State);var files=SelectionFiles();
                yield return Click("productTabs",2);yield return Click("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
                yield return Click("navigationCards",4);MilestoneBody(expected);postchapterObservedBody=Field<TMPro.TMP_Text>("panelBody").text;SelectionUnchanged(before,files);
                yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));SelectionUnchanged(before,files);
                yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));SelectionUnchanged(before,files);
                yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);SelectionUnchanged(before,files);
            }
            // Explicit final ownership with zero skills; fixed seed1 memory setup only.
            var setup=Fixture(MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,5}));
            setup.AcceptOrder("order.sort-salvage",setup.State.sequence+1,1000);
            for(int spin=0;spin<16;spin++)setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
            Assert.That(setup.State.coins,Is.EqualTo(472));Assert.That(setup.Journey.Orders.Ready,Is.True);
            Assert.That(setup.Journey.Orders.LatestClaim,Is.Null);var unclaimed=setup.State;
            yield return Load(unclaimed);
            yield return ReadChapterInfo("Buildings: 24/24","Skills: 0/6","You may finish here");
            Assert.That(postchapterObservedBody,Does.Not.Contain("In this save:").And.Not.Contain("order reward"));
            long lastSpin=Session.State.lastSpin.sequence;uint seed=Session.State.seed;
            yield return ProductOrderDetail();yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(592));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(lastSpin));
            Assert.That(Session.Journey.Orders.LatestClaim.RewardCoins,Is.EqualTo(120));
            var paidContext=Field<TMPro.TMP_Text>("navigationContext");Assert.That(paidContext.isActiveAndEnabled,Is.True);
            Assert.That(paidContext.text,Does.Contain("+120 Coins").And.Contain("Coins can buy remaining optional skills."));NarrativeBounds(paidContext);
            PostchapterSnapshot("claim-skills-remaining","Synthetic completed chapter and 16 public fixture spins; actual UI claim paid120; zero skills owned and no next acceptance");
            yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            yield return ReadChapterInfo("Buildings: 24/24","In this save: order reward collected.");
            Assert.That(postchapterObservedBody,Does.Not.Contain("all skills").And.Not.Match(@"(?<!\d)0/2(?!\d)"));
            yield return ProductOrderDetail();yield return Click("selectButton");
            Assert.That(Session.Journey.Orders.Progress,Is.Zero);Assert.That(Session.State.coins,Is.EqualTo(592));
            yield return ReadChapterInfo("In this save: order reward collected.");
            long accepted=Session.Journey.Orders.InstanceSequence;long retained=Session.Journey.Orders.LatestClaim.ClaimSequence;
            yield return ProductOrderDetail();yield return Click("chapterPrevious");yield return Click("selectButton");
            Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);Assert.That(Session.Journey.Orders.LatestClaim.ClaimSequence,Is.EqualTo(retained));
            Assert.That(Session.State.coins,Is.EqualTo(592));Assert.That(accepted,Is.GreaterThan(retained));
            yield return ReadChapterInfo("In this save: order reward collected.");
            string oldRaw=codec.Encode(unclaimed);File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),oldRaw);var files=SelectionFiles();
            Call("Reload");yield return null;SelectionUnchanged(oldRaw,files);
            yield return ReadChapterInfo("Buildings: 24/24","Skills: 0/6");
            Assert.That(postchapterObservedBody,Does.Not.Contain("In this save:").And.Not.Contain("order reward"));
            yield return NarrativeUnload();
            // Genuine six-skill/pre-final receipt; only one existing final700 command.
            var both=Fixture(codec.Decode(MilestoneInput("late23-skills6.json")));
            both.SelectChapterProject("power-source.workshop-yard.upgrade-2",both.State.sequence+1,1000);
            Assert.That(both.State.coins,Is.EqualTo(9957));
            both.AcceptOrder("order.sort-salvage",both.State.sequence+1,1000);
            for(int spin=0;spin<64&&!both.Journey.Orders.Ready;spin++)both.ExecuteChapterSpin(both.State.sequence+1,1000);
            Assert.That(both.Journey.Orders.Ready,Is.True);Assert.That(both.Journey.Orders.LatestClaim,Is.Null);
            yield return Load(both.State);
            yield return ReadChapterInfo("Buildings: 24/24","In this save: all skills owned.");
            Assert.That(postchapterObservedBody,Does.Not.Contain("order reward"));
            long wallet=Session.State.coins;seed=Session.State.seed;lastSpin=Session.State.lastSpin.sequence;
            yield return ProductOrderDetail();
            Assert.That(Session.Journey.Orders.Ready,Is.True);Assert.That(Session.Journey.Orders.LatestClaim,Is.Null);Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(6));
            MilestoneBody("Spins yield materials based on results and caps.","Orders do not increase yield; upgrades do not change order credit.");
            PostchapterSnapshot("ready120-all-skills-before-claim","Tracked genuine six-skill23 fixture; final project command and public order acceptance with bounded setup spins; Ready sorting unpaid and no previous claim. Load this Ready state and perform an actual Claim for transient result capture");
            yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(wallet+120));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(lastSpin));
            paidContext=Field<TMPro.TMP_Text>("navigationContext");Assert.That(paidContext.isActiveAndEnabled,Is.True);
            Assert.That(paidContext.text,Does.Contain("+120 Coins").And.Contain("6/6 skills").And.Contain("free play for Coins"));
            NarrativeBounds(paidContext);
            PostchapterSnapshot("claim-all-skills","Tracked genuine six-skill23 fixture, final project command and public ready order; actual UI claim paid120; no successor accepted");
            yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            yield return ReadChapterInfo("Buildings: 24/24","Skills: 6/6","You may finish here","In this save: all skills owned; order reward collected.");
            Assert.That(postchapterObservedBody,Does.Not.Match(@"(?<!\d)0/2(?!\d)"));Saved();
            TestContext.WriteLine("Existing Claim pointers pay120 once; ready-unclaimed has no earned claim label, zero-skill claim and new instance retain only the claim label, older actual Reload removes it. Genuine six-skill23 plus one700 command and bounded memory order setup reaches both-earned longest existing chapter text, checked read-only against real TMP bounds. No UI spin grind or new reward.");
        }

        void WholeLoopSnapshot(string name,string provenance)
        {
            string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/whole-loop-delivery-20261101/root","produced-"+name+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target,"jtn.save.v1.json"),codec.Encode(Session.State),new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"panel.txt"),Field<TMPro.TMP_Text>("panelBody").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"message.txt"),Field<TMPro.TMP_Text>("message").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"provenance.txt"),provenance+"; clock="+Environment.GetEnvironmentVariable(ClockOverride)+"; sequence="+Session.State.sequence+"; panel="+Field<int>("wavePanel")+". Text is captured live, not replayed on load.",new System.Text.UTF8Encoding(false));
            TestContext.WriteLine("Private whole-loop snapshot: "+target);
        }
        [UnityTest]
        public IEnumerator WholeLoopAutoKeepsOnlyLastRevealedReceiptThroughBusyPauseAndReload()
        {
            var state=ModelState.Fresh(1);state.houseStage=1;state.autoHintSeen=1;state.coins=119;
            var setup=Fixture(state);setup.SelectChapterProject(GameIds.WorkshopBuild,setup.State.sequence+1,1000);
            yield return Load(setup.State);Call("SetRewardFeedbackReduced",true);
            Assert.That((bool)gameType.GetProperty("RewardFeedbackReduced").GetValue(game),Is.True);
            long start=Session.State.sequence;var message=Field<TMPro.TMP_Text>("message");
            var goal=Field<TMPro.TMP_Text>("productGoalText");var identity=Field<TMPro.TMP_Text>("productIdentity");
            string beforeGoal=goal.text,beforeIdentity=identity.text;
            Assert.That(beforeGoal,Does.Contain("Workshop").And.Contain("120"));Assert.That(beforeIdentity,Does.Contain("1/24"));
            yield return Click("autoButton");Assert.That(Flag("Busy")&&Flag("AutoRunning"),Is.True);
            Assert.That(Session.Arcade.LastResourceReceipt.CoinIncome,Is.EqualTo(8));Assert.That(Session.Arcade.LastResourceReceipt.ConstructionDebit,Is.EqualTo(120));
            Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(2));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            Assert.That(message.text,Does.Not.Contain("+8").And.Not.Contain("Paid 120"));
            Assert.That(goal.text,Is.EqualTo(beforeGoal));Assert.That(identity.text,Is.EqualTo(beforeIdentity));
            Call("Render",null,null);Assert.That(message.text,Does.Not.Contain("+8").And.Not.Contain("Paid 120"));
            Assert.That(goal.text,Is.EqualTo(beforeGoal),"Incidental after-state Render must not reveal the paid Workshop goal");
            Assert.That(identity.text,Is.EqualTo(beforeIdentity),"Incidental after-state Render must not reveal the new construction count");
            float deadline=Time.realtimeSinceStartup+7;while(Flag("Busy")&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(Flag("Busy"),Is.False);Assert.That(Flag("AutoRunning"),Is.False);
            Assert.That(message.text,Does.Contain("+8").And.Contain("120"));NarrativeBounds(message);
            Assert.That(goal.text,Does.Contain("Next goal").And.Not.Contain("Goal: Workshop"));
            Assert.That(identity.text,Does.Contain("2/24"));
            string revealedGoal=goal.text,revealedIdentity=identity.text;
            yield return Click("autoButton");
            WholeLoopSnapshot("auto-first-reveal","Selected Workshop120, synthetic wallet119 and seed1; first real AUTO spin credits8 and pays120, reduced effects enabled");
            deadline=Time.realtimeSinceStartup+2;while(!Flag("Busy")&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(Flag("Busy")&&Flag("AutoRunning"),Is.True);Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(start+2));
            long secondIncome=Session.State.lastSpin.reward+1;Assert.That(Session.Arcade.LastResourceReceipt.CoinIncome,Is.EqualTo(secondIncome));
            Assert.That(Session.Arcade.LastResourceReceipt.ConstructionDebit,Is.Zero);Assert.That(secondIncome,Is.Not.EqualTo(8));
            Assert.That(message.text.ToLowerInvariant(),Does.Contain("previous"));Assert.That(message.text,Does.Contain("+8").And.Contain("120"));
            Assert.That(message.text,Does.Not.Contain("+"+secondIncome+" coins"));
            var lastSize=gameType.GetField("lastScreenSize",Private);lastSize.SetValue(game,new Vector2(-1,-1));yield return null;
            Assert.That(Flag("Busy"),Is.True);Assert.That(message.text,Does.Contain("+8").And.Not.Contain("+"+secondIncome+" coins"));
            Assert.That(goal.text,Is.EqualTo(revealedGoal));Assert.That(identity.text,Is.EqualTo(revealedIdentity));
            yield return Click("autoButton");Assert.That(Flag("AutoRunning"),Is.False);
            Call("OnApplicationPause",true);Assert.That(Flag("InBackground"),Is.True);Assert.That(message.text,Does.Not.Contain("+"+secondIncome+" coins"));
            yield return null;Call("OnApplicationPause",false);yield return null;Call("Render",null,null);
            Assert.That(Flag("Busy"),Is.True);Assert.That(message.text,Does.Not.Contain("+"+secondIncome+" coins"));
            Assert.That(goal.text,Is.EqualTo(revealedGoal));Assert.That(identity.text,Is.EqualTo(revealedIdentity));
            yield return Finish();Assert.That(message.text,Does.Contain("+"+secondIncome).And.Not.Contain("Paid 120"));NarrativeBounds(message);
            Assert.That((int)gameType.GetProperty("RewardFeedbackVisibleCount").GetValue(game),Is.Zero);
            WholeLoopSnapshot("auto-second-reveal","Two real AUTO spins; second replaces first only at reveal; reduced effects and synthetic safe-area change-detection plus actual pause/resume");
            string before=codec.Encode(Session.State);var files=SelectionFiles();Call("Reload");yield return null;SelectionUnchanged(before,files);
            Assert.That(Flag("AutoRunning")||Flag("Busy"),Is.False);Assert.That((int)gameType.GetProperty("RewardFeedbackVisibleCount").GetValue(game),Is.Zero);
            Assert.That(message.text,Does.Contain("+"+secondIncome));
            long completion=Session.Journey.Guidance.LastCompletion.Sequence;yield return Click("guideButton");
            Assert.That(Field<long>("shownCompletion"),Is.EqualTo(completion));yield return Click("selectButton");
            Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.EqualTo(completion));Call("Render",null,null);
            Assert.That(message.text,Does.Contain("+"+secondIncome));Assert.That((int)gameType.GetProperty("RewardFeedbackVisibleCount").GetValue(game),Is.Zero);
            long preSettlement=Session.State.coins;Call("OnApplicationPause",true);Clock(1600);Call("OnApplicationPause",false);yield return null;
            Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);Assert.That(Session.State.coins,Is.EqualTo(preSettlement+10));
            long settlement=Session.State.lastSettlement.sequence;yield return Click("closeButton");Assert.That(Session.State.lastSettlementSeen,Is.EqualTo(settlement));
            Call("Render",null,null);Assert.That(message.text,Does.Contain("+"+secondIncome));
            Assert.That((int)gameType.GetProperty("RewardFeedbackVisibleCount").GetValue(game),Is.Zero);
            before=codec.Encode(Session.State);files=SelectionFiles();Call("Reload");yield return null;SelectionUnchanged(before,files);
            Assert.That(message.text,Does.Contain("+"+secondIncome));Assert.That((int)gameType.GetProperty("RewardFeedbackVisibleCount").GetValue(game),Is.Zero);
            // A different, valid private backup with the same sequence and no
            // spin cannot inherit the prior session's revealed receipt cache.
            long sameSequence=Session.State.sequence;Assert.That(sameSequence,Is.InRange(1,16));
            var empty=ModelState.Fresh(1);empty.lastSettledAt=1000;var alternate=Fixture(empty);
            for(long i=1;i<=sameSequence;i++)alternate.Checkpoint(1000+i);
            Assert.That(alternate.State.sequence,Is.EqualTo(sameSequence));Assert.That(alternate.State.lastSpin,Is.Null);
            Clock(1000+sameSequence);File.WriteAllText(Path.Combine(directory,"jtn.backup.v1.json"),codec.Encode(alternate.State));
            File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),"{private invalid primary");Call("Reload");yield return null;
            Assert.That(Session.RecoveryRequired,Is.True);Assert.That(Session.BackupAvailable,Is.True);
            yield return Click("selectButton");Assert.That(Field<bool>("restoreConfirmation"),Is.True);yield return Click("selectButton");
            Assert.That(Session.RecoveryRequired,Is.False);Assert.That(Session.State.sequence,Is.EqualTo(sameSequence));Assert.That(Session.State.lastSpin,Is.Null);
            Call("Render",null,null);Assert.That(message.text,Does.Not.Contain("+"+secondIncome));
            Assert.That((int)gameType.GetProperty("RewardFeedbackVisibleCount").GetValue(game),Is.Zero);
            TestContext.WriteLine("Two AUTO commits, first Workshop120 receipt and second Workshop-owned income remain reveal-gated with reduced FX; previous receipt survives next Busy/forced resize-detection, pause cannot leak next result. Cold Reload and later exact completion ACK/+10 offline settlement retain the historic spin without replay; actual private backup restore at the same sequence cannot inherit the previous session's receipt. Actual display resizing is separate.");
        }
        [UnityTest]
        public IEnumerator WholeLoopPositiveOfflineSummaryNamesCurrentUnselectedPhase()
        {
            var cases=new[]{
                new{name="ordinary",state=MilestoneEraFixture(EraIds.Junkyard,new[]{1,1,0,0,0,0}),phase="choose one",count=2},
                new{name="era-ready",state=MilestoneEraFixture(EraIds.Junkyard,new[]{2,2,2,2,2,2}),phase="entry",count=12},
                new{name="complete",state=MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,5}),phase="24/24",count=24}};
            foreach(var item in cases)
            {
                Assert.That(item.state.coins,Is.EqualTo(100));Assert.That(item.state.workshopBuilt,Is.EqualTo(1));
                Clock(1600);yield return SelectionLoadRaw(codec.Encode(item.state));
                Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);Assert.That(Session.State.coins,Is.EqualTo(110));
                Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(item.count));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
                var receipt=Session.State.lastSettlement;Assert.That(receipt.coins,Is.EqualTo(10));long receiptSequence=receipt.sequence;
                var body=Field<TMPro.TMP_Text>("panelBody");Assert.That(body.isActiveAndEnabled,Is.True);
                Assert.That(body.text,Does.Contain("Already credited: 10 coins.").And.Contain("Now: 110 coins"));
                Assert.That(body.text.ToLowerInvariant(),Does.Contain(item.phase));
                if(item.name=="complete")Assert.That(body.text,Does.Contain("Inspect Progress."));
                Assert.That(body.text,Does.Contain("Close"));NarrativeBounds(body);Assert.That(body.isTextOverflowing,Is.False);
                string before=codec.Encode(Session.State);var files=SelectionFiles();Call("Render",null,null);SelectionUnchanged(before,files);
                WholeLoopSnapshot("offline-"+item.name,"Synthetic validated phase and wallet100, Workshop owned; actual ten-minute private settlement credited10 once; summary still unacknowledged");
                yield return Click("closeButton");Assert.That(Session.State.coins,Is.EqualTo(110));Assert.That(Session.State.lastSettlementSeen,Is.EqualTo(receiptSequence));
                Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(item.count));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
                Assert.That(Flag("AutoRunning"),Is.False);yield return NarrativeUnload();
            }
            TestContext.WriteLine("Three positive10-Coin settlements distinguish ordinary no-goal, unentered12/24 free era and sufficient24/24; opening/render never selects, enters, builds or repays, and Close ACKs exact prior receipt once.");
        }
        [UnityTest]
        public IEnumerator WholeLoopCompletedChapterKeepsOrderRoundAndExactCompletionAckIndependent()
        {
            var boundary=MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,4});boundary.coins=1000;
            var setup=Fixture(boundary);setup.SelectChapterProject("power-source.workshop-yard.upgrade-2",setup.State.sequence+1,1000);
            Assert.That(setup.Milestones.ConstructionCount,Is.EqualTo(24));var completion=setup.Journey.Guidance.LastCompletion;
            Assert.That(completion,Is.Not.Null);long completionSequence=completion.Sequence;
            for(int i=0;i<8;i++)setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
            Assert.That(setup.Arcade.Tickets,Is.EqualTo(1));setup.AcceptOrder("order.sort-salvage",setup.State.sequence+1,1000);
            long orderInstance=setup.Journey.Orders.InstanceSequence;
            setup.ExecuteArcade(setup.GetArcadeQuote("start","arcade.memory",setup.Arcade.Sequence),1000);
            long runInstance=setup.Arcade.ActiveRun.InstanceSequence;
            Assert.That(setup.Journey.Guidance.CompletionAcknowledgedSequence,Is.Not.EqualTo(completionSequence));
            yield return Load(setup.State);string before=codec.Encode(Session.State);var files=SelectionFiles();
            Assert.That(Field<TMPro.TMP_Text>("message").text,Does.Not.Contain("Last spin"),"Newer accepted activities must not be displaced by an older spin receipt");
            Assert.That(Field<TMPro.TMP_Text>("productGoalText").text,Does.Contain("24/24"));
            yield return Click("productGoalButton");Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("You may finish here").And.Contain("Accepted optional order"));
            yield return Click("chapterNext");Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));SelectionUnchanged(before,files);
            WholeLoopSnapshot("completed-order-list","Synthetic23 ownership and funded wallet; public final project, eight actual setup spins, accepted order and started Memory; completion remains unacknowledged");
            yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));
            Assert.That(Field<Button[]>("productCards")[3].GetComponentInChildren<TMPro.TMP_Text>(true).text,Does.Contain("Resume round"));
            yield return Click("productCards",3);Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.EqualTo(runInstance));SelectionUnchanged(before,files);
            yield return Click("productTabs",1);SelectionUnchanged(before,files);
            Assert.That(Session.Journey.Orders.InstanceSequence,Is.EqualTo(orderInstance));Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.Not.EqualTo(completionSequence));
            Assert.That(Field<string>("shownGuide"),Is.EqualTo(GuideIds.Completion));yield return Click("guideButton");
            Assert.That(Field<long>("shownCompletion"),Is.EqualTo(completionSequence));SelectionUnchanged(before,files);
            var stale=Field<Button>("selectButton").onClick;yield return Click("selectButton");
            Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.EqualTo(completionSequence));Assert.That(Session.State.sequence,Is.EqualTo(setup.State.sequence+1));
            Assert.That(Session.Journey.Orders.InstanceSequence,Is.EqualTo(orderInstance));Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.EqualTo(runInstance));
            string acknowledged=codec.Encode(Session.State);files=SelectionFiles();stale.Invoke();SelectionUnchanged(acknowledged,files);
            Call("Reload");yield return null;SelectionUnchanged(acknowledged,files);
            Assert.That(Session.Journey.Guidance.CompletionAcknowledgedSequence,Is.EqualTo(completionSequence));
            TestContext.WriteLine("24/24 remains sufficient; accepted order and saved Memory inspect without writes. Explicit completion ACK binds exactly the actual final project sequence once and leaves both instance identities untouched across Reload.");
        }

        [UnityTest]
        public IEnumerator ReturnSummarySeparatesSavedIntervalFromPaidMinutesWithoutRepayingPinnedGoals()
        {
            // Valid synthetic states, not rewritten historical receipts. Both goals become affordable.
            var v1=ModelState.Fresh(123);v1.coins=598;v1.houseStage=1;v1.workshopBuilt=1;v1.selectedStage=2;v1.autoHintSeen=1;v1.lastSettledAt=1000;
            var v2=MilestoneEraFixture(EraIds.Junkyard,new[]{1,1,0,0,0,0});v2.coins=535;v2.selectedStage=2;
            v2.chapter=new ChapterState(2,v2.chapter.RuleRevision,v2.chapter.Progress,v2.chapter.Skills,GameIds.HouseImprove,0,0,JunkToNeon.Content.ConstructionPricing.RevisionV2);
            GameLogic.Validate(v1);GameLogic.Validate(v2);
            foreach(var item in new[]{
                new{state=v1,to=1185L,seconds=185L,reward=3L,wallet=601L,price=600L,interval="3m 5s",paid="Income for 3 full minutes",back=true},
                new{state=v2,to=37030L,seconds=28800L,reward=480L,wallet=1015L,price=540L,interval="10h 0m 30s",paid="Income for 480 full minutes",back=false}})
            {
                Clock(1000);yield return Load(item.state);long sequence=Session.State.sequence;uint seed=Session.State.seed;
                Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(item.price));
                ReturnSnapshot("pinned-"+item.price+"-before-return","Synthetic selected legacy goal and owned Workshop; before settlement. Saved goal terms were not changed.");
                Clock(item.to);Call("Reload");yield return null;
                Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);var receipt=Session.State.lastSettlement;
                Assert.That(receipt.from,Is.EqualTo(1000));Assert.That(receipt.to,Is.EqualTo(item.to));Assert.That(receipt.seconds,Is.EqualTo(item.seconds));
                Assert.That(receipt.coins,Is.EqualTo(item.reward));Assert.That(Session.State.coins,Is.EqualTo(item.wallet));
                Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Assert.That(Session.State.seed,Is.EqualTo(seed));
                Assert.That(Session.State.houseStage,Is.EqualTo(1));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(GameIds.HouseImprove));
                Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(item.price));
                MilestoneBody($"Already credited: {item.reward} coins.",$"Saved interval: {item.interval}.",item.paid,"cap: 8h","This credit does not build or change your goal or its price.");
                Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Now: "+item.wallet+" coins").And.Contain("price "+item.price+" coins").And.Contain("not yet paid").And.Contain("press Close first"));
                ReturnSnapshot("pinned-"+item.price+"-summary","Actual private return settlement; receipt is already credited. Summary Close will only acknowledge it.");
                Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("minutes away"));
                Assert.That(Field<Button>("selectButton").gameObject.activeSelf,Is.False);
                string credited=codec.Encode(Session.State);var files=SelectionFiles();string text=Field<TMPro.TMP_Text>("panelBody").text;
                Call("Reload");yield return null;SelectionUnchanged(credited,files);
                Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Is.EqualTo(text));
                var oldClose=Field<Button>("closeButton").onClick;
                if(item.back)yield return Back();else yield return Click("closeButton");
                Assert.That(Session.State.coins,Is.EqualTo(item.wallet));Assert.That(Session.State.sequence,Is.EqualTo(sequence+2));
                Assert.That(Session.State.lastSettlementSeen,Is.EqualTo(receipt.sequence));Assert.That(Session.State.seed,Is.EqualTo(seed));
                Assert.That(Session.State.houseStage,Is.EqualTo(1));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(GameIds.HouseImprove));
                Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(item.price));Saved();
                var goal=Field<TMPro.TMP_Text>("productGoalText");Assert.That(goal.isActiveAndEnabled,Is.True);
                Assert.That(goal.text,Does.Contain("now "+item.wallet).And.Contain(item.price+" / "+item.price).And.Contain("unpaid"));
                Assert.That(Field<RectTransform>("productGoalFill").anchorMax.x,Is.EqualTo(1f));
                ReturnSnapshot("pinned-"+item.price+"-post-ack","Actual UI Back/Close acknowledged the previously credited receipt; no second payment or construction.");
                string acknowledged=codec.Encode(Session.State);files=SelectionFiles();oldClose.Invoke();SelectionUnchanged(acknowledged,files);
                Call("Render",null,null);SelectionUnchanged(acknowledged,files);
                Assert.That(goal.text,Does.Contain("now "+item.wallet).And.Contain(item.price+" / "+item.price).And.Contain("unpaid"));
                Call("Reload");yield return null;SelectionUnchanged(acknowledged,files);
                Assert.That(Field<bool>("offlineSummaryPanel"),Is.False);Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
                yield return NarrativeUnload();
            }
            TestContext.WriteLine("Synthetic valid pinned600/540 targets:185 saved seconds pays3 full minutes once (598->601);10h30s saved interval pays capped480 minutes once (535->1015). Existing summary/TMP bounds distinguish saved interval and already-credited amount. Unseen Reload is read-only; actual Back/Close only acknowledges, stale Close/reload do not repay/build/reprice. No wall-clock absence certainty or economic change.");
        }

        ModelState ReturnPinnedGoal(long price,long wallet,bool workshop=true)
        {
            if(price==600)
            {
                var old=ModelState.Fresh(123);old.coins=wallet;old.houseStage=1;old.workshopBuilt=workshop?1:0;old.selectedStage=2;old.autoHintSeen=1;old.lastSettledAt=1000;
                GameLogic.Validate(old);return old;
            }
            Assert.That(price,Is.EqualTo(540));
            var v2=MilestoneEraFixture(EraIds.Junkyard,new[]{1,workshop?1:0,0,0,0,0});v2.coins=wallet;v2.selectedStage=2;
            v2.chapter=new ChapterState(2,v2.chapter.RuleRevision,v2.chapter.Progress,v2.chapter.Skills,GameIds.HouseImprove,0,0,JunkToNeon.Content.ConstructionPricing.RevisionV2);
            GameLogic.Validate(v2);return v2;
        }
        [UnityTest]
        public IEnumerator ReturnPinnedGoalExplainsActualWalletAcrossRenderAndColdReload()
        {
            foreach(var item in new[]{new{price=540L,wallet=539L,progress="539 / 540"},new{price=540L,wallet=540L,progress="540 / 540"},new{price=540L,wallet=564L,progress="540 / 540"},
                new{price=600L,wallet=599L,progress="599 / 600"},new{price=600L,wallet=600L,progress="600 / 600"},new{price=600L,wallet=624L,progress="600 / 600"}})
            {
                Clock(1000);yield return Load(ReturnPinnedGoal(item.price,item.wallet,false));
                Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(item.price));
                var goal=Field<TMPro.TMP_Text>("productGoalText");Assert.That(goal.isActiveAndEnabled,Is.True);
                Assert.That(goal.text,Does.Contain("now "+item.wallet).And.Contain(item.progress));
                NarrativeBounds(goal);Assert.That(goal.isTextOverflowing,Is.False);
                if(item.wallet<item.price)Assert.That(goal.text,Does.Contain("Need 1 more"));
                else Assert.That(goal.text,Does.Contain("Enough").And.Contain("unpaid"));
                if(item.wallet<item.price)Assert.That(Field<RectTransform>("productGoalFill").anchorMax.x,Is.LessThan(1f));
                else Assert.That(Field<RectTransform>("productGoalFill").anchorMax.x,Is.EqualTo(1f));
                Assert.That(Session.State.houseStage,Is.EqualTo(1));Assert.That(Session.State.workshopBuilt,Is.Zero);
                string original=codec.Encode(Session.State);var files=SelectionFiles();
                Call("Render",null,null);SelectionUnchanged(original,files);Assert.That(goal.text,Does.Contain("now "+item.wallet).And.Contain(item.price.ToString()));
                Call("Reload");yield return null;SelectionUnchanged(original,files);
                Assert.That(Field<TMPro.TMP_Text>("productGoalText").text,Does.Contain("now "+item.wallet).And.Contain(item.price.ToString()));
                Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(item.price));Assert.That(Flag("AutoRunning"),Is.False);
                if(item.price==540&&item.wallet==564)ReturnSnapshot("goal-540-overfunded","Synthetic existing saved540 goal without a Workshop, overfunded wallet564; no construction or return was committed.");
                if(item.price==600&&item.wallet==599)
                {
                    Call("OnApplicationPause",true);Clock(1060);Call("OnApplicationPause",false);yield return null;
                    Assert.That(Session.State.coins,Is.EqualTo(599));Assert.That(Session.State.lastSettlement,Is.Null);Assert.That(Field<bool>("offlineSummaryPanel"),Is.False);
                    Assert.That(Field<TMPro.TMP_Text>("message").text,Does.Contain("Welcome back"));Assert.That(goal.text,Does.Contain("now 599").And.Contain("Need 1 more"));
                    original=codec.Encode(Session.State);files=SelectionFiles();Call("Render",null,null);SelectionUnchanged(original,files);
                    Assert.That(goal.text,Does.Contain("now 599").And.Contain("Need 1 more"));
                }
                yield return NarrativeUnload();
            }
        }
        [UnityTest]
        public IEnumerator ReturnReceiptKeepsActualWalletThroughOlderZeroAndReplacement()
        {
            var initial=MilestoneEraFixture(EraIds.Junkyard,new[]{2,2,1,0,0,0});initial.coins=316;
            var setup=Fixture(initial);setup.SelectChapterProject("recycler.improve",setup.State.sequence+1,1000);
            Assert.That(setup.Chapter.SelectedProjectId,Is.EqualTo("recycler.improve"));Assert.That(ChapterEconomy.SelectedProjectPrice(setup.Chapter),Is.EqualTo(320));
            Clock(1000);yield return Load(setup.State);uint seed=Session.State.seed;ReturnSnapshot("goal320-before-return","Synthetic wallet316 with publicly selected saved320 target and owned Workshop; before a private return settlement.");
            Clock(15880);Call("Reload");yield return null;
            Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);Assert.That(Session.State.coins,Is.EqualTo(564));
            var older=Session.State.lastSettlement;Assert.That(older.coins,Is.EqualTo(248));Assert.That(older.from,Is.EqualTo(1000));Assert.That(older.to,Is.EqualTo(15880));
            Assert.That(Session.State.lastSettlementSeen,Is.LessThan(older.sequence));
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Already credited: 248 coins.").And.Contain("Now: 564 coins").And.Contain("price 320 coins"));
            Assert.That(Field<TMPro.TMP_Text>("productGoalText").text,Does.Contain("320 / 320").And.Contain("now 564").And.Contain("unpaid"));
            Assert.That(Field<RectTransform>("productGoalFill").anchorMax.x,Is.EqualTo(1f));ReturnSnapshot("goal320-wallet564-summary","Actual private settlement +248: wallet316->564 while saved320 goal remains unpaid; summary still unacknowledged.");
            Clock(15895);Call("Reload");yield return null;
            Assert.That(Session.State.coins,Is.EqualTo(564));Assert.That(Session.State.lastSettlement.sequence,Is.EqualTo(older.sequence));
            Assert.That(Session.State.lastSettlementSeen,Is.LessThan(older.sequence));Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Already credited: 248 coins.").And.Contain("Now: 564 coins"));
            Clock(16015);Call("Reload");yield return null;
            var newer=Session.State.lastSettlement;Assert.That(newer.sequence,Is.GreaterThan(older.sequence));Assert.That(newer.coins,Is.EqualTo(2));
            Assert.That(Session.State.coins,Is.EqualTo(566));Assert.That(Session.State.lastSettlementSeen,Is.LessThan(newer.sequence));
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Already credited: 2 coins.").And.Contain("Now: 566 coins").And.Not.Contain("Already credited: 248"));
            ReturnSnapshot("goal320-newer-summary","Actual later private +2 settlement replaced an older unseen +248 receipt; wallet retains both credits, saved320 remains unpaid.");
            long beforeAck=Session.State.sequence;var staleClose=Field<Button>("closeButton").onClick;yield return Click("closeButton");
            Assert.That(Session.State.sequence,Is.EqualTo(beforeAck+1));Assert.That(Session.State.lastSettlementSeen,Is.EqualTo(newer.sequence));
            Assert.That(Session.State.coins,Is.EqualTo(566));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("recycler.improve"));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(320));Assert.That(Flag("AutoRunning"),Is.False);
            string acknowledged=codec.Encode(Session.State);var files=SelectionFiles();staleClose.Invoke();SelectionUnchanged(acknowledged,files);
            Call("Render",null,null);SelectionUnchanged(acknowledged,files);
            Assert.That(Field<TMPro.TMP_Text>("productGoalText").text,Does.Contain("320 / 320").And.Contain("now 566").And.Contain("unpaid"));
            ReturnSnapshot("goal320-post-ack","Actual UI Close acknowledged the latest +2 receipt only; wallet566 and selected320 target remain intact.");
            Call("OnApplicationPause",true);Clock(16025);Call("OnApplicationPause",false);yield return null;
            Assert.That(Field<bool>("offlineSummaryPanel"),Is.False);Assert.That(Session.State.coins,Is.EqualTo(566));
            Assert.That(Field<TMPro.TMP_Text>("productGoalText").text,Does.Contain("320 / 320").And.Contain("now 566").And.Contain("unpaid"));
            acknowledged=codec.Encode(Session.State);files=SelectionFiles();Call("Render",null,null);SelectionUnchanged(acknowledged,files);
            Call("Reload");yield return null;SelectionUnchanged(acknowledged,files);Assert.That(Flag("AutoRunning"),Is.False);
        }

        [UnityTest]
        public IEnumerator ReturnNoGoalAndOrdersKeepTheirOwnExistingRoutes()
        {
            var branches=new[]{
                new{state=MilestoneEraFixture(EraIds.Junkyard,new[]{0,0,0,0,0,0}),copy="Repair House"},
                new{state=MilestoneEraFixture(EraIds.Junkyard,new[]{1,0,0,0,0,0}),copy="Inspect Workshop"},
                new{state=MilestoneEraFixture(EraIds.Junkyard,new[]{2,2,2,2,2,2}),copy="Workshop Yard unlocked"},
                new{state=MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,5}),copy="District complete · 24/24"}};
            foreach(var item in branches)
            {
                Clock(1000);yield return Load(item.state);string before=codec.Encode(Session.State);var files=SelectionFiles();
                var goal=Field<TMPro.TMP_Text>("productGoalText");Assert.That(goal.isActiveAndEnabled,Is.True);Assert.That(goal.text,Does.Contain(item.copy));
                NarrativeBounds(goal);Assert.That(goal.isTextOverflowing,Is.False);Call("Render",null,null);SelectionUnchanged(before,files);
                Assert.That(goal.text,Does.Contain(item.copy));
                if(item.copy=="Workshop Yard unlocked")
                {
                    ReturnSnapshot("no-goal-era-eligible","Synthetic validated Junkyard12 ownership; free entry is eligible but has not been chosen.");
                    yield return Click("productGoalButton");Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("free"));
                    SelectionUnchanged(before,files);yield return Back();SelectionUnchanged(before,files);
                }
                else if(item.copy=="Inspect Workshop")
                {
                    ReturnSnapshot("no-goal-workshop","Synthetic repaired-house ownership; Workshop is not owned and its inspection remains optional.");
                    yield return Click("productGoalButton");
                    Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Partial minutes do not carry between returns.").And.Contain("Later upgrades do not raise this income."));
                    SelectionUnchanged(before,files);yield return Back();SelectionUnchanged(before,files);
                }
                else if(item.copy=="District complete · 24/24")ReturnSnapshot("no-goal-chapter-complete","Synthetic validated24-stage ownership; no selected successor or new reward.");
                yield return NarrativeUnload();
            }
            Clock(1000);yield return Load(MilestoneEraFixture(EraIds.Junkyard,new[]{1,2,0,0,0,0}));
            string workshopState=codec.Encode(Session.State);var workshopFiles=SelectionFiles();
            yield return Click("productTabs",0);yield return Click("productCards",0);yield return Click("navigationCards",1);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Partial minutes do not carry between returns.").And.Contain("Later upgrades do not raise this income."));
            SelectionUnchanged(workshopState,workshopFiles);yield return NarrativeUnload();
            // A committed entry takes priority over a newer positive offline receipt.
            var transition=Fixture(MilestoneEraFixture(EraIds.Junkyard,new[]{2,2,2,2,2,2}));
            transition.EnterWorkshopYard(transition.State.sequence+1,1000);Assert.That(transition.Chapter.TransitionPending,Is.True);
            Clock(1600);yield return SelectionLoadRaw(codec.Encode(transition.State));
            Assert.That(Session.Chapter.TransitionPending,Is.True);Assert.That(Field<bool>("offlineSummaryPanel"),Is.False);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain(ChapterNarrative.EraEntry));Assert.That(Session.State.coins,Is.EqualTo(110));
            ReturnSnapshot("committed-transition-first","Synthetic12-stage fixture, public free era entry, then actual +10 private settlement; transition presentation precedes the unseen receipt.");
            yield return Click("closeButton");Assert.That(Session.Chapter.TransitionPending,Is.False);Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Already credited: 10 coins."));Assert.That(Session.State.coins,Is.EqualTo(110));
            yield return Click("closeButton");Assert.That(Session.State.coins,Is.EqualTo(110));Assert.That(Flag("AutoRunning"),Is.False);yield return NarrativeUnload();

            var orderSetup=Fixture(MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,5}));var none=orderSetup.State;
            orderSetup.AcceptOrder("order.sort-salvage",orderSetup.State.sequence+1,1000);
            for(int spin=0;spin<15;spin++)orderSetup.ExecuteChapterSpin(orderSetup.State.sequence+1,1000);
            Assert.That(orderSetup.Journey.Orders.Progress,Is.EqualTo(11));var active=orderSetup.State;
            orderSetup.ExecuteChapterSpin(orderSetup.State.sequence+1,1000);Assert.That(orderSetup.Journey.Orders.Ready,Is.True);var ready=orderSetup.State;
            long instance=orderSetup.Journey.Orders.InstanceSequence;
            orderSetup.ClaimOrder("order.sort-salvage",instance,orderSetup.State.sequence+1,1000);
            orderSetup.AcceptOrder("order.sort-salvage",orderSetup.State.sequence+1,1000);var restarted=orderSetup.State;
            Assert.That(orderSetup.Journey.Orders.LatestClaim.RewardCoins,Is.EqualTo(120));Assert.That(orderSetup.Journey.Orders.Progress,Is.Zero);
            foreach(var item in new[]{
                new{state=MilestoneEraFixture(EraIds.Junkyard,new[]{1,0,0,0,0,0}),status="Unlock after 24/24",name="locked"},
                new{state=none,status="Accept · manual spins · claim",name="none"},
                new{state=active,status="11/12",name="active"},new{state=ready,status="12/12",name="ready"},
                new{state=restarted,status="0/12",name="new-after-claim"}})
            {
                Clock(1000);yield return Load(item.state);string before=codec.Encode(Session.State);var files=SelectionFiles();
                yield return Click("productTabs",2);var card=Field<Button[]>("productCards")[2];var text=card.GetComponentInChildren<TMPro.TMP_Text>(true);
                Assert.That(text.text,Does.Contain(item.status));NarrativeBounds(text);Assert.That(text.isTextOverflowing,Is.False);
                if(item.name=="active"||item.name=="new-after-claim")Assert.That(text.text,Does.Contain("In progress").And.Not.Contain("Ready"));
                if(item.name=="ready")Assert.That(text.text,Does.Contain("Ready").And.Contain("120 coins").And.Contain("claim"));
                if(item.name=="active"||item.name=="ready")ReturnSnapshot("orders-"+item.name,"Synthetic completed chapter and publicly accepted order; fifteen/sixteen public seed1 spins produce the actual11/12 or Ready12/12 instance. No claim via inspection.");
                yield return Click("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));SelectionUnchanged(before,files);
                yield return Back();SelectionUnchanged(before,files);Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));
                yield return NarrativeUnload();
            }
        }

        [UnityTest]
        public IEnumerator OverdueRepairGuidanceKeepsItsAckAtEraAndFinalStopsWithoutPromisingAuto()
        {
            foreach(int phase in new[]{0,1,2})
            {
                // Explicit valid Fresh-origin snapshots; no UI chapter traversal or invented completion event.
                var state=phase==0?MilestoneEraFixture(EraIds.Junkyard,new[]{1,0,0,0,0,0}):phase==1?
                    MilestoneEraFixture(EraIds.Junkyard,new[]{2,2,2,2,2,2}):
                    MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,5});
                state.journey=JourneyState.Fresh();var setup=Fixture(state);
                if(phase!=0)setup.AcknowledgeGuidance(phase==1?GuideIds.EraReady:GuideIds.ChapterEnd,setup.State.sequence+1,0,1000);
                Assert.That(setup.Guidance.Suggestion.HintId,Is.EqualTo(GuideIds.FirstRepair));yield return Load(setup.State);
                Assert.That(Field<string>("shownGuide"),Is.EqualTo(GuideIds.FirstRepair));
                Assert.That(Field<Button>("autoButton").interactable,Is.EqualTo(phase==0));
                string title=phase==0?"AUTO unlocked. Choose the next goal yourself.":phase==1?"Junkyard complete. Enter Workshop Yard.":"Chapter complete. Continue if you wish.";
                Assert.That(Field<TMPro.TMP_Text>("heading").text,Is.EqualTo(title));NarrativeBounds(Field<TMPro.TMP_Text>("heading"));
                string before=codec.Encode(Session.State);var files=SelectionFiles();long sequence=Session.State.sequence;uint seed=Session.State.seed;long wallet=Session.State.coins;
                yield return Click("guideButton");
                if(phase==0)MilestoneBody("Repair unlocks AUTO.");
                else
                {
                    MilestoneBody(phase==1?"Enter Workshop Yard for free":"Stop here or spin manually.");
                    Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("AUTO"));
                    if(phase==2)Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("optional").And.Contain("no further era"));
                }
                SelectionUnchanged(before,files);yield return Back();SelectionUnchanged(before,files);
                yield return Click("guideButton");yield return Click("selectButton");
                Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.FirstRepair),Is.True);
                Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Assert.That(Session.State.coins,Is.EqualTo(wallet));Assert.That(Session.State.seed,Is.EqualTo(seed));
                string acknowledged=codec.Encode(Session.State);files=SelectionFiles();Call("Reload");yield return null;SelectionUnchanged(acknowledged,files);
                Assert.That(Session.Guidance.Suggestion?.HintId,Is.Not.EqualTo(GuideIds.FirstRepair));Saved();yield return NarrativeUnload();
                if(phase==0)continue;
                // Immutable synthetic pending/committed bytes captured using accepted pre-fix23f115c rules.
                string prefix=phase==1?"late-repair-era":"late-repair-final";
                string fixtureRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/Guidance/fixtures"));
                var pending=codec.Decode(File.ReadAllText(Path.Combine(fixtureRoot,prefix+"-pending.json")));
                string committed=File.ReadAllText(Path.Combine(fixtureRoot,prefix+"-committed.json"));
                Assert.That(pending.pending.journey.ActionId,Is.EqualTo(GuideIds.FirstRepair));yield return Load(pending);
                Assert.That(codec.Encode(Session.State),Is.EqualTo(committed));Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.FirstRepair),Is.True);
                files=SelectionFiles();Session.AcknowledgeGuidance(GuideIds.FirstRepair,Session.State.sequence,0,1000);SelectionUnchanged(committed,files);
                Call("Reload");yield return null;SelectionUnchanged(committed,files);Saved();yield return NarrativeUnload();
            }
            TestContext.WriteLine("Three bounded Fresh snapshots: ordinary repaired house retains AUTO, overdue repair after era-ready ACK directs free entry, and after chapter-end ACK keeps finite/manual optional play. Existing hint identity/priority/ACK remains; read/Back is byte/mtime read-only, ACK once and Reload stable. Two pre-fix synthetic pending/committed late ACK pairs recover/replay exactly; no UI chapter traversal or economic change.");
        }

        [UnityTest]
        public IEnumerator SkillResultUsesCommittedPriceAndPreservesGenuine540GoalWithoutReplayFeedback()
        {
            yield return Load(codec.Decode(MilestoneInput("v2-selected540.json")));
            IEnumerator OpenFirstSkill()
            {
                yield return Click("productTabs",4);yield return Click("productCards",3);yield return Click("navigationCards",0);
            }
            string before=codec.Encode(Session.State);var files=SelectionFiles();long sequence=Session.State.sequence;uint seed=Session.State.seed;
            yield return OpenFirstSkill();MilestoneBody("Price: 120 Coins.","535/540");SelectionUnchanged(before,files);
            yield return Back();SelectionUnchanged(before,files);yield return OpenFirstSkill();
            var queued=Field<Button>("selectButton").onClick;yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(415));Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Assert.That(Session.State.seed,Is.EqualTo(seed));
            Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(1));var receipt=Session.Chapter.Skills.Receipts[0];
            Assert.That(receipt.FrozenPrice,Is.EqualTo(120));Assert.That(receipt.Request.NodeId,Is.EqualTo(SkillIds.Universal1));Assert.That(receipt.Request.Sequence,Is.EqualTo(Session.State.sequence));
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(GameIds.HouseImprove));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
            Assert.That(Session.Chapter.ConstructionPriceRevision,Is.EqualTo(JunkToNeon.Content.ConstructionPricing.RevisionV2));Assert.That(Session.State.houseStage,Is.EqualTo(1));
            var text=Field<TMPro.TMP_Text>("message");
            Assert.That(text.text,Does.Contain("Coins I −120; balance 415. Same goal: House 540."));
            Assert.That(text.text,Does.EndWith("Skill: +0 → +1 Coins per spin."));
            Assert.That(text.text,Does.Not.Contain("Choose the next goal").And.Not.Contain("All skills owned (optional)"));
            NarrativeBounds(text);Assert.That(text.isTextOverflowing,Is.False);Saved();
            yield return PurchaseChoicePortrait.Capture(game,"skill-I-result","message");
            string committed=codec.Encode(Session.State);files=SelectionFiles();queued.Invoke();SelectionUnchanged(committed,files);
            Call("Reload");yield return null;SelectionUnchanged(committed,files);
            Assert.That(Field<TMPro.TMP_Text>("message").text,Does.Not.Contain("Coins I"));queued.Invoke();SelectionUnchanged(committed,files);
            Session.BuySkill(receipt.Request,1000);SelectionUnchanged(committed,files);
            Assert.That(Field<TMPro.TMP_Text>("message").text,Does.Not.Contain("Coins I"));
            TestContext.WriteLine("Genuine saved535/540 goal: existing first-skill pointer pays120 once ->415, receipt-bound result names Coins I/debit/remainder and retained540. Preview/Back, stale hidden callbacks, exact Core request replay and actual Reload preserve full state/files/mtime; Reload does not replay purchase feedback. Existing message bounds checked; no reward or repricing.");
        }

        [UnityTest]
        public IEnumerator SixthSkillResultIsEarnedOnlyAndEarlierFiveSkillReloadRemainsIncomplete()
        {
            var recorded=codec.Decode(MilestoneInput("late23-skills6.json"));
            var ownership=JunkToNeon.Upgrades.SkillOwnership.Empty;
            // Reconstruct the exact immutable five-receipt prefix through the public recovery API.
            // This new valid synthetic snapshot has no fabricated old spin/pending receipt.
            foreach(var paid in recorded.chapter.Skills.Receipts.Take(5))
                ownership=JunkToNeon.Upgrades.SkillTree.RestorePurchase(paid.Request.Sequence-1,paid.Request.ExpectedWallet,paid.Request.Context,ownership,paid.Request,paid.FrozenPrice).OwnershipAfter;
            var five=MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,4});five.coins=40;five.sequence=34;five.revision=68;
            five.chapter=new ChapterState(2,five.chapter.RuleRevision,five.chapter.Progress,ownership,"power-source.workshop-yard.upgrade-2",five.chapter.EntrySequence,five.chapter.TransitionSeen);
            five.wave=new WaveProgress(1,1,five.wave.Delivery,ChapterEconomy.LegacySlot(ownership));GameLogic.Validate(five);
            var setup=Fixture(five);setup.ExecuteChapterSpin(35,1000);
            // Independent seed1 [potato,potato,trash]:8base+1workshop+2universal+10potato+6trash=27.
            Assert.That(setup.State.coins,Is.EqualTo(67));Assert.That(setup.State.lastSpin.symbols,Is.EqualTo(new[]{0,0,2}));
            Assert.That(setup.Chapter.Skills.Receipts.Count,Is.EqualTo(5));Assert.That(setup.Milestones.ConstructionCount,Is.EqualTo(23));
            yield return Load(setup.State);string earlier=codec.Encode(Session.State);var files=SelectionFiles();uint seed=Session.State.seed;long sequence=Session.State.sequence;
            IEnumerator OpenSixthSkill()
            {
                yield return Click("productTabs",4);yield return Click("productCards",3);yield return Click("navigationCards",5);
            }
            yield return OpenSixthSkill();MilestoneBody("Price: 20 Coins.","67/700");SelectionUnchanged(earlier,files);
            yield return Back();SelectionUnchanged(earlier,files);yield return OpenSixthSkill();var queued=Field<Button>("selectButton").onClick;
            yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(47));Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(35));
            Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(6));Assert.That(Session.Chapter.Skills.Receipts.Last().FrozenPrice,Is.EqualTo(20));
            Assert.That(Session.Chapter.Skills.Receipts.Last().Request.NodeId,Is.EqualTo(SkillIds.Delivery2));
            Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(23));Assert.That(ChapterCatalog.Rules.IsChapterComplete(Session.Chapter.Progress),Is.False);
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("power-source.workshop-yard.upgrade-2"));Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(700));
            var text=Field<TMPro.TMP_Text>("message");
            Assert.That(text.text,Does.Contain("Deliveries II −20; balance 47. Same goal: Power Source 700."));
            Assert.That(text.text,Does.Contain("Skill: +1 → +2 Coins per 3 Pigeons.").And.EndWith("All skills owned (optional)."));
            Assert.That(text.text,Does.Not.Contain("Chapter complete").And.Not.Contain("Choose the next goal"));NarrativeBounds(text);Assert.That(text.isTextOverflowing,Is.False);Saved();
            yield return PurchaseChoicePortrait.Capture(game,"skill-sixth-result","message");
            string committed=codec.Encode(Session.State);files=SelectionFiles();queued.Invoke();SelectionUnchanged(committed,files);
            yield return OpenSixthSkill();MilestoneBody("This node is already owned");Assert.That(Field<Button>("selectButton").gameObject.activeSelf,Is.False);
            queued.Invoke();SelectionUnchanged(committed,files);Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("Skill:"));
            yield return Back();SelectionUnchanged(committed,files);Assert.That(Field<int>("wavePanel"),Is.EqualTo(31));
            yield return Click("productTabs",1);SelectionUnchanged(committed,files);Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            Assert.That(Field<TMPro.TMP_Text>("message").text,Does.Not.Contain("All skills owned (optional)"));
            File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),earlier);files=SelectionFiles();Call("Reload");yield return null;SelectionUnchanged(earlier,files);
            Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(5));Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(23));
            Assert.That(Field<TMPro.TMP_Text>("message").text,Does.Not.Contain("All skills owned (optional)"));
            yield return Click("productTabs",0);yield return Click("productCards",3);MilestoneBody("Buildings: 23/24");SelectionUnchanged(earlier,files);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("all skills owned"));Saved();
            TestContext.WriteLine("Public reconstruction of five accepted receipts +one bounded memory spin gives67; actual current deliveryII pointer spends20 ->47. Sixth-skill earned-only result preserves23/24 and700 target, checked with the populated spin receipt in real existing message bounds. Owned-node reopen/hidden callback cannot spend again; actual older-five Reload removes absent earned fact. No internal constructor, chapter grind or persisted badge.");
        }

        [UnityTest]
        public IEnumerator FreshOptionalSkillAt24Of24KeepsNoGoalAndDoesNotReplayItsEffect()
        {
            var state=MilestoneEraFixture(EraIds.WorkshopYard,new[]{5,5,5,5,5,5});state.coins=1000;
            yield return Load(state);string before=codec.Encode(Session.State);var files=SelectionFiles();
            yield return Click("productTabs",4);yield return Click("productCards",3);yield return Click("navigationCards",0);
            MilestoneBody("No building selected.","Price: 120 Coins.");SelectionUnchanged(before,files);
            var stale=Field<Button>("selectButton").onClick;yield return Click("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(880));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(24));Assert.That(ChapterCatalog.Rules.IsChapterComplete(Session.Chapter.Progress),Is.True);
            var text=Field<TMPro.TMP_Text>("message");Assert.That(text.text,Does.Contain("No building selected.").And.Contain("Skill: +0 → +1 Coins per spin.").And.Not.Contain("Choose the next goal"));
            NarrativeBounds(text);Saved();yield return PurchaseChoicePortrait.Capture(game,"skill-24-optional","message");
            before=codec.Encode(Session.State);files=SelectionFiles();stale.Invoke();SelectionUnchanged(before,files);
            Call("Reload");yield return null;SelectionUnchanged(before,files);Assert.That(Field<TMPro.TMP_Text>("message").text,Does.Not.Contain("Skill:"));
        }


        // Cold action evidence is closed/flushed immediately; Unity's nested runner may
        // abandon the parent iterator without executing its finally on child failure.
        void AppendJourneyEvidence(string name,string entry)
        {
            Assert.That(Path.GetFileName(name),Is.EqualTo(name));
            long added=System.Text.Encoding.UTF8.GetByteCount(entry+Environment.NewLine);
            Assert.That(added,Is.LessThan(128*1024));
            string evidence=Path.GetFullPath(Path.Combine(directory,"journey-evidence"));
            Assert.That(Path.GetDirectoryName(evidence),Is.EqualTo(directory));
            if(Directory.Exists(evidence))Assert.That((File.GetAttributes(evidence)&FileAttributes.ReparsePoint)==0,Is.True);
            Directory.CreateDirectory(evidence);
            Assert.That(Directory.GetDirectories(evidence),Is.Empty);
            string path=Path.Combine(evidence,name);
            if(File.Exists(path))Assert.That((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,Is.True);
            long bytes=Directory.GetFiles(directory).Concat(Directory.GetFiles(evidence)).Sum(f=>new FileInfo(f).Length);
            Assert.That(bytes+added,Is.LessThan(32L*1024*1024));
            File.AppendAllText(path,entry+Environment.NewLine,new System.Text.UTF8Encoding(false));
        }

        [Test]
        public void JourneyEvidenceSurvivesNestedFailureBeforeParentDisposal()
        {
            bool parentDisposed=false;var snapshots=SelectionFiles();
            IEnumerator Child(){AppendJourneyEvidence("nested-evidence.log","ACTION committed before nested failure");yield return null;throw new InvalidOperationException("Expected nested harness probe");}
            IEnumerator Parent(){try{yield return Child();}finally{parentDisposed=true;}}
            var parent=Parent();Assert.That(parent.MoveNext(),Is.True);var child=(IEnumerator)parent.Current;
            try
            {
                Assert.That(child.MoveNext(),Is.True);
                Assert.Throws<InvalidOperationException>(()=>child.MoveNext());
                Assert.That(parentDisposed,Is.False,"Probe must reproduce an undisposed parent, not rely on its finally");
                Assert.That(File.ReadAllLines(Path.Combine(directory,"journey-evidence","nested-evidence.log")),Is.EqualTo(new[]{"ACTION committed before nested failure"}));
                Assert.That(SelectionFiles(),Is.EquivalentTo(snapshots),"Diagnostic append must preserve top-level snapshot inventory/bytes/mtime");
            }
            finally{(child as IDisposable)?.Dispose();(parent as IDisposable)?.Dispose();}
            Assert.That(parentDisposed,Is.True);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator RetainedSixHundredSpinHistoryContinuesToFiniteEndingAndOneOptionalClaim()
        {
            // Two-session genuine history continuation, NOT a fresh37 route pass.
            const string primaryHash="EF1ABA9A14F5E70256F4B92D09F5F851CBEC8FB6E23E33F99FEC9777F1DC5C8A";
            const string backupHash="6B0A897928C0A045823E4BF53A2BA7A70E848334C2F23C0DC981E3F131DB609E";
            string inputs=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/SeededJourney/fixtures"));
            string primary=Path.Combine(inputs,"600-spin-primary.json"),backup=Path.Combine(inputs,"600-spin-backup.json");
            string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
            string InputIdentity()=>Hash(primary)+"|"+File.GetLastWriteTimeUtc(primary).Ticks+"|"+Hash(backup)+"|"+File.GetLastWriteTimeUtc(backup).Ticks;
            Assert.That(Hash(primary),Is.EqualTo(primaryHash));Assert.That(Hash(backup),Is.EqualTo(backupHash));string inputIdentity=InputIdentity();
            Assert.That(Directory.GetFiles(directory),Is.Empty);
            File.Copy(primary,Path.Combine(directory,"jtn.save.v1.json"));File.Copy(backup,Path.Combine(directory,"jtn.backup.v1.json"));
            Assert.That(Hash(Path.Combine(directory,"jtn.save.v1.json")),Is.EqualTo(primaryHash));
            Assert.That(Hash(Path.Combine(directory,"jtn.backup.v1.json")),Is.EqualTo(backupHash));
            double started=Time.realtimeSinceStartupAsDouble;int spins=0,entries=0;long wallet=497;
            void Bound(){Assert.That(Time.realtimeSinceStartupAsDouble-started,Is.LessThan(600));Assert.That(spins,Is.LessThanOrEqualTo(100));Assert.That(InputIdentity(),Is.EqualTo(inputIdentity),"Immutable genuine inputs changed");}
            void Record(string entry){Bound();Assert.That(++entries,Is.LessThan(500));AppendJourneyEvidence("seeded-continuation.log",entry);}
            void Facts(int count,string selected=null)
            {
                Bound();SelectionHealthy();Saved();Assert.That(Session.State.coins,Is.EqualTo(wallet));Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(count));
                Assert.That(Session.Chapter.Skills.Receipts.Count,Is.Zero);Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo(selected));Assert.That(Flag("AutoRunning"),Is.False);
                Assert.That(Session.Chapter.EntrySequence,Is.EqualTo(308));Assert.That(Session.Chapter.TransitionSeen,Is.EqualTo(308));
            }
            IEnumerator Tap(string field,int index=-1){Bound();Record("ATTEMPT "+field+"["+index+"] seq="+Session.State.sequence+" coins="+Session.State.coins);yield return Click(field,index);Record("UI "+field+"["+index+"] seq="+Session.State.sequence+" coins="+Session.State.coins);}
            IEnumerator Orders(){yield return Tap("productTabs",2);yield return Tap("productCards",2);yield return Tap("navigationCards",0);}
            IEnumerator Spin(long income,int count,string selected=null)
            {
                Assert.That(spins,Is.LessThan(100));Assert.That(Field<GameObject>("panel").activeSelf,Is.False);long seq=Session.State.sequence;
                wallet+=income;if(count==24&&selected==null&&spins==8)wallet-=700;
                yield return Tap("spinButton");spins++;Assert.That(Session.State.sequence,Is.EqualTo(seq+1));Facts(count,selected);
                Record("SPIN "+spins+" income="+income+" coins="+wallet+" seed="+Session.State.seed);yield return Finish();Bound();
                Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            }
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Additive));
            scene=SceneManager.GetSceneByPath(ScenePath);SceneManager.SetActiveScene(scene);game=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren(gameType,true)).Single();
            yield return null;Canvas.ForceUpdateCanvases();
            var store=typeof(GameSession).GetField("store",Private).GetValue(Session);
            var storage=(FileSnapshotStorage)typeof(GameStore).GetField("storage",Private).GetValue(store);
            foreach(var field in typeof(FileSnapshotStorage).GetFields(Private).Where(f=>f.FieldType==typeof(string)))
                Assert.That(Path.GetDirectoryName(Path.GetFullPath((string)field.GetValue(storage))),Is.EqualTo(directory));
            Assert.That(Hash(Path.Combine(directory,"jtn.save.v1.json")),Is.EqualTo(primaryHash));Assert.That(Hash(Path.Combine(directory,"jtn.backup.v1.json")),Is.EqualTo(backupHash));
            Assert.That(Session.State.sequence,Is.EqualTo(656));Assert.That(Session.State.seed,Is.EqualTo(3531443369u));Assert.That(Session.State.wave.Delivery.PigeonProgress,Is.Zero);
            Assert.That(Session.State.pending,Is.Null);Assert.That(Session.Journey.Guidance.Origin,Is.EqualTo(GuideOrigin.Fresh));
            Assert.That(Session.Chapter.ConstructionPriceRevision,Is.EqualTo("construction.prices.v3"));
            Assert.That(ChapterCatalog.Rules.Stages(Session.Chapter.Progress).Entries.Select(e=>e.AbsoluteLevel),Is.EqualTo(new[]{5,5,5,5,5,4}));
            Facts(23,"power-source.workshop-yard.upgrade-2");Record("CHECKPOINT resumed23 "+codec.Encode(Session.State));
            string before=codec.Encode(Session.State);var files=SelectionFiles();
            yield return Tap("productTabs",0);yield return Tap("productCards",0);yield return Tap("navigationCards",5);
            MilestoneBody("Goal selected","Need 203");Assert.That(Field<Button>("selectButton").gameObject.activeSelf,Is.False);
            SelectionUnchanged(before,files);yield return Root();SelectionUnchanged(before,files);
            long[] finalIncomes={26,20,22,28,23,16,27,26,19};
            for(int i=0;i<9;i++)yield return Spin(finalIncomes[i],i==8?24:23,i==8?null:"power-source.workshop-yard.upgrade-2");
            Assert.That(wallet,Is.EqualTo(4));Assert.That(Session.State.seed,Is.EqualTo(4222460643u));Assert.That(Session.State.wave.Delivery.PigeonProgress,Is.Zero);
            Assert.That(Session.State.lastSpin.chapter.Price,Is.EqualTo(700));Assert.That(Session.State.lastSpin.chapter.ActionId,Is.EqualTo("power-source.workshop-yard.upgrade-2"));
            Assert.That(Session.Milestones.ReachedCount,Is.EqualTo(4));Assert.That(Session.Milestones.Milestones.All(m=>m.Reached),Is.True);Assert.That(Session.OptionalAchievements.ReachedCount,Is.Zero);
            Assert.That(Field<Button>("autoButton").interactable,Is.False);Record("CHECKPOINT finite24 "+codec.Encode(Session.State));
            before=codec.Encode(Session.State);files=SelectionFiles();
            yield return Tap("productGoalButton");
            MilestoneBody("Buildings: 24/24","chapter yes","You may finish here","optional");Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("all skills owned"));
            SelectionUnchanged(before,files);yield return Root();SelectionUnchanged(before,files);
            yield return Orders();MilestoneBody("12 Trash appearances","Reward: 120","Only new results after explicit acceptance");
            long accept=Session.State.sequence;yield return Tap("selectButton");Assert.That(Session.State.sequence,Is.EqualTo(accept+1));Facts(24);yield return Root();
            Assert.That(Session.Journey.Orders.InstanceSequence,Is.EqualTo(accept+1));Assert.That(Session.Journey.Orders.ActiveOrderId,Is.EqualTo("order.sort-salvage"));
            long[] sortingIncomes={25,25,20,21,25,28,42,28,26,21,29,25,28,27,21,17,20,23};
            int[] trash={1,2,4,5,6,6,6,6,6,7,7,8,8,8,9,10,10,12};
            for(int i=0;i<18;i++)
            {
                yield return Spin(sortingIncomes[i],24);Assert.That(Session.Journey.Orders.Progress,Is.EqualTo(trash[i]));
                Assert.That(Session.Journey.Orders.LatestClaim,Is.Null);Assert.That(Session.OptionalAchievements.ReachedCount,Is.Zero);
            }
            Assert.That(spins,Is.EqualTo(27));Assert.That(wallet,Is.EqualTo(455));Assert.That(Session.State.seed,Is.EqualTo(2875318843u));Assert.That(Session.State.wave.Delivery.PigeonProgress,Is.Zero);
            Assert.That(Session.Journey.Orders.Ready,Is.True);Record("CHECKPOINT ready12 "+codec.Encode(Session.State));
            yield return Orders();MilestoneBody("Progress: 12/12","Collect your reward");Assert.That(Field<Button>("selectButton").GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("Collect reward"));
            long claim=Session.State.sequence;wallet+=120;yield return Tap("selectButton");Assert.That(Session.State.sequence,Is.EqualTo(claim+1));Facts(24);
            Assert.That(wallet,Is.EqualTo(575));Assert.That(Session.State.seed,Is.EqualTo(2875318843u));Assert.That(Session.State.wave.Delivery.PigeonProgress,Is.Zero);
            Assert.That(Session.Journey.Orders.LatestClaim.RewardCoins,Is.EqualTo(120));Assert.That(Session.Journey.Orders.LatestClaim.InstanceSequence,Is.EqualTo(accept+1));Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);
            Assert.That(Session.OptionalAchievements.Achievements.Single(a=>a.Definition.Id=="optional.retained-order-claim").Reached,Is.True);
            Assert.That(Session.OptionalAchievements.Achievements.Single(a=>a.Definition.Id=="optional.all-skills-owned").Reached,Is.False);Record("CHECKPOINT claimed120 "+codec.Encode(Session.State));
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
            yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            before=codec.Encode(Session.State);files=SelectionFiles();yield return Orders();MilestoneBody("Previous order already paid 120");
            SelectionUnchanged(before,files);yield return Root();SelectionUnchanged(before,files);
            yield return Tap("productGoalButton");
            MilestoneBody("Buildings: 24/24","In this save: order reward collected.");Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("all skills"));
            SelectionUnchanged(before,files);yield return Root();SelectionUnchanged(before,files);Facts(24);
            Record("PASS genuine-history continuation27spins only; original fresh journey remains FAILED600");
        }

        [UnityTearDown]
        public IEnumerator RestorePrivateContext()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Environment.SetEnvironmentVariable(SaveOverride, previousSave); Environment.SetEnvironmentVariable(ClockOverride, previousClock);
            if (directory == null) yield break;
            if(retainTouchProof){TestContext.WriteLine("Preserved private actual-touch proof fixture: "+directory);yield break;}
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            { TestContext.WriteLine("Preserved failed private fixture: " + directory); yield break; }
            foreach (string name in new[] { "jtn.save.v1.json", "jtn.backup.v1.json", "jtn.pending-write.v1.json", "jtn.interrupted-write.v1.json" })
            {
                string path = Path.Combine(directory, name);
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) File.Delete(path);
            }
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory, false);
        }
    }
}
#endif
