#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JunkToNeon.Core;
using JunkToNeon.Diagnostics;
using JunkToNeon.Contracts;
using JunkToNeon.Guidance;
using JunkToNeon.Orders;
using JunkToNeon.Effects;
using JunkToNeon.Upgrades;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    // The runtime remains in Assembly-CSharp. Reflection avoids a production asmdef/test hook.
    public sealed class ReelUiLifecycleTests
    {
        const string ScenePath = "Assets/Scenes/JunkToNeon.unity";
        const string SaveOverride = "JTN_EDITOR_SAVE_DIRECTORY";
        readonly JsonStateCodec codec = new JsonStateCodec();
        readonly Dictionary<string, string> normalSaveBytes = new Dictionary<string, string>();
        string directory, previousOverride, previousClock;
        Scene loadedScene, previousScene;
        Component game;
        Type gameType;
        GameSession Session => (GameSession)gameType.GetProperty("Session").GetValue(game);
        IReadOnlyList<DiagnosticRecord> Diagnostics => (IReadOnlyList<DiagnosticRecord>)gameType.GetProperty("LocalDiagnostics").GetValue(game);
        bool Busy => (bool)gameType.GetProperty("Busy").GetValue(game);
        bool Auto => (bool)gameType.GetProperty("AutoRunning").GetValue(game);
        int Frames => (int)gameType.GetProperty("PresentedFrames").GetValue(game);
        T Field<T>(string name) => (T)gameType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);

        [SetUp]
        public void IsolateSave()
        {
            normalSaveBytes.Clear();
            loadedScene = default;
            directory = null;
            previousOverride = Environment.GetEnvironmentVariable(SaveOverride);
            previousClock = Environment.GetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS");
            Environment.SetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS", "1000");
            previousScene = SceneManager.GetActiveScene();
            // No runtime scene may start before this override is installed.
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False,
                "Run from a neutral test scene; do not reuse a user's running game.");
            directory = Path.Combine(Path.GetTempPath(), "jtn-ui-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            foreach (var entry in ObserveNormal()) normalSaveBytes.Add(entry.Key, entry.Value);
            Environment.SetEnvironmentVariable(SaveOverride, directory);
            gameType = Type.GetType("JunkToNeon.JunkGame, Assembly-CSharp", true);
        }

        IEnumerable<string> NormalSavePaths() => Directory.Exists(Application.persistentDataPath)
            ? Directory.GetFiles(Application.persistentDataPath, "jtn.*.json") : Array.Empty<string>();

        Dictionary<string, string> ObserveNormal()
        {
            var observation = new Dictionary<string, string>();
            foreach (var path in NormalSavePaths())
            {
                try { observation[path] = Convert.ToBase64String(File.ReadAllBytes(path)); }
                catch (IOException) { observation[path] = "Changing or inaccessible during read"; }
            }
            return observation;
        }

        IEnumerator Prepare(ModelState state)
        {
            state.lastSettledAt=1000;
            GameLogic.Validate(state);
            File.WriteAllText(Path.Combine(directory, "jtn.save.v1.json"), codec.Encode(state));
            yield return LoadGame();
        }

        IEnumerator LoadGame(bool expectBlocked=false)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            SceneManager.SetActiveScene(loadedScene);
            game = loadedScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(gameType, true)).Single();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(Session.RecoveryRequired, Is.EqualTo(expectBlocked), Session.Error);
            AssertOwnedSession(Session);
        }

        void AssertOwnedSession(GameSession session)
        {
            // Assert the actual storage paths, rather than assuming the environment override worked.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var store = typeof(GameSession).GetField("store", flags).GetValue(session);
            var storage = (FileSnapshotStorage)typeof(GameStore).GetField("storage", flags).GetValue(store);
            foreach (var field in typeof(FileSnapshotStorage).GetFields(flags).Where(field => field.FieldType == typeof(string)))
                Assert.That(Path.GetDirectoryName(Path.GetFullPath((string)field.GetValue(storage))),
                    Is.EqualTo(Path.GetFullPath(directory)), "Session storage escaped its owned fixture: " + field.Name);
            TestContext.WriteLine("Verified owned session storage: " + Path.GetFullPath(directory));
        }

        IEnumerator ReenterScene()
        {
            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(loadedScene);
            game = null;
            yield return LoadGame();
        }

        IEnumerator PointerClick(string field, int index = -1)
        {
            // Newly activated panel graphics enter raycasting on the next rendered UI frame.
            yield return null;
            var button = index<0?Field<Button>(field):Field<Button[]>(field)[index];
            Assert.That(button.gameObject.activeInHierarchy, Is.True, field + " must be visible");
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>();
            var rect = (RectTransform)button.transform;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var data = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), field + " must be reachable by a UI raycast");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject),
                field + " must be the topmost pointer target");
            // Dispatch through uGUI's pointer handlers, including its interactable check.
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        IEnumerator FinishPresentation()
        {
            float deadline = Time.realtimeSinceStartup + 7;
            while (Busy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Busy, Is.False, "Presentation must finish within a bounded real-time wait");
            yield return new WaitForSecondsRealtime(.5f);
        }

        IEnumerator ReturnFromNavigationToGame()
        {
            // Only dismiss our known read-only lists/hub, never an acknowledgement.
            // Product routes pass through a hub (70-76) on the way back to the game.
            for(int i=0;i<4&&Field<GameObject>("panel").activeSelf;i++)
            {
                int mode=Field<int>("wavePanel");
                Assert.That(new[]{20,30,31,32,70,71,72,73,74,75,76},Does.Contain(mode),"Unexpected covering panel");
                yield return PointerClick("closeButton");
            }
            Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
        }
        IEnumerator OpenFamily(int index)
        {
            yield return ReturnFromNavigationToGame();
            yield return PointerClick("productTabs",0);yield return PointerClick("productCards",0);
            yield return PointerClick("navigationCards",index);
            AssertPortraitLayout();
        }
        IEnumerator OpenSkill(int branch,int tier)
        {
            yield return ReturnFromNavigationToGame();
            yield return PointerClick("productTabs",4);yield return PointerClick("productCards",3);
            yield return PointerClick("navigationCards",branch*2+tier-1);
            AssertPortraitLayout();
        }

        sealed class FixtureStorage : ISnapshotStorage
        {
            public string value;
            public string ReadPrimary()=>value;
            public string ReadBackup()=>null;
            public void Write(string raw){value=raw;}
        }

        ModelState BeforeFinalProject()
        {
            var state=ModelState.Fresh(1);state.coins=10000;
            var session=GameSession.Open(new GameStore(new FixtureStorage{value=codec.Encode(state)},codec),1);
            foreach(var family in FamilyIds.All)for(int level=1;level<=2;level++)
            {
                session.SelectChapterProject(ChapterIds.Project(EraIds.Junkyard,family,level),session.State.sequence+1);
                if(family==FamilyIds.House&&level==1)session.AcknowledgeAutoHint(session.State.sequence+1);
            }
            session.EnterWorkshopYard(session.State.sequence+1);
            session.AcknowledgeEraTransition(session.State.sequence+1);
            foreach(var family in FamilyIds.All)for(int level=1;level<=2;level++)
                if(family!=FamilyIds.PowerSource||level==1)
                    session.SelectChapterProject(ChapterIds.Project(EraIds.WorkshopYard,family,level),session.State.sequence+1);
            state=session.State;state.coins=699;
            session=GameSession.Open(new GameStore(new FixtureStorage{value=codec.Encode(state)},codec),1);
            return session.SelectChapterProject(ChapterIds.Project(EraIds.WorkshopYard,FamilyIds.PowerSource,2),session.State.sequence+1);
        }

        GameSession CompletedFixture()
        {
            var state=ModelState.Fresh(1);state.coins=10000;
            var session=GameSession.Open(new GameStore(new FixtureStorage{value=codec.Encode(state)},codec),1);
            foreach(var era in new[]{EraIds.Junkyard,EraIds.WorkshopYard})
            {
                if(era==EraIds.WorkshopYard){session.EnterWorkshopYard(session.State.sequence+1);session.AcknowledgeEraTransition(session.State.sequence+1);}
                foreach(var family in FamilyIds.All)for(int level=1;level<=2;level++)
                {
                    session.SelectChapterProject(ChapterIds.Project(era,family,level),session.State.sequence+1);
                    if(era==EraIds.Junkyard&&family==FamilyIds.House&&level==1)session.AcknowledgeAutoHint(session.State.sequence+1);
                }
            }
            Assert.That(session.State.coins,Is.EqualTo(690));return session;
        }
        void OrderResultSnapshot(string name,string provenance)
        {
            string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/postchapter-delivery-20261101/root","produced-"+name+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target,"jtn.save.v1.json"),codec.Encode(Session.State),new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"navigation-context.txt"),Field<TMPro.TMP_Text>("navigationContext").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"panel-body.txt"),Field<TMPro.TMP_Text>("panelBody").text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"provenance.txt"),provenance+"; sequence="+Session.State.sequence+"; panel mode="+Field<int>("wavePanel")+"; panelBody visible="+Field<TMPro.TMP_Text>("panelBody").isActiveAndEnabled+"; navigationContext visible="+Field<TMPro.TMP_Text>("navigationContext").isActiveAndEnabled+". UI text was captured at this checkpoint and is not replayed by reload.",new System.Text.UTF8Encoding(false));
            TestContext.WriteLine("Private produced postchapter snapshot: "+target);
        }
        IEnumerator OrderResultBackToGame()
        {
            yield return PointerClick("closeButton");Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));
            yield return PointerClick("closeButton");Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
        }
        IEnumerator OpenOrder(int index=0)
        {
            yield return ReturnFromNavigationToGame();
            yield return PointerClick("productTabs",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));
            yield return PointerClick("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
            yield return PointerClick("navigationCards",index);yield return null;AssertCurrentProductLayout();
        }

        void AssertCurrentProductLayout()
        {
            Canvas.ForceUpdateCanvases();var safe=Field<RectTransform>("safe");
            Assert.That(safe.rect.width,Is.GreaterThan(0));Assert.That(safe.rect.height,Is.GreaterThan(0));
            foreach(var button in game.GetComponentsInChildren<Button>(false))
            {
                var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                foreach(var corner in corners)
                {
                    var p=safe.InverseTransformPoint(corner);
                    Assert.That(p.x,Is.InRange(safe.rect.xMin-.1f,safe.rect.xMax+.1f),button.name+" outside safe width");
                    Assert.That(p.y,Is.InRange(safe.rect.yMin-.1f,safe.rect.yMax+.1f),button.name+" outside safe height");
                }
            }
            foreach(var text in game.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if(!text.enabled)continue;text.ForceMeshUpdate();
                Assert.That(text.isTextOverflowing,Is.False,"Visible text overflow: "+text.name+": "+text.text);
                Assert.That(text.textBounds.size.x,Is.LessThanOrEqualTo(text.rectTransform.rect.width+2),text.name);
                Assert.That(text.textBounds.size.y,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2),text.name);
            }
            var panel=Field<GameObject>("panel");if(!panel.activeInHierarchy)return;
            var panelRect=(RectTransform)panel.transform;var panelBounds=ScreenBounds(panelRect);var safeBounds=ScreenBounds(safe);
            Assert.That(panelBounds.xMin,Is.GreaterThanOrEqualTo(safeBounds.xMin-1));Assert.That(panelBounds.xMax,Is.LessThanOrEqualTo(safeBounds.xMax+1));
            Assert.That(panelBounds.yMin,Is.GreaterThanOrEqualTo(safeBounds.yMin-1));Assert.That(panelBounds.yMax,Is.LessThanOrEqualTo(safeBounds.yMax+1));
            Assert.That(Field<TMPro.TMP_Text>("panelTitle").isActiveAndEnabled,Is.True);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").isActiveAndEnabled,Is.True);
            var actions=new[]{Field<Button>("closeButton"),Field<Button>("selectButton"),Field<Button>("chapterPrevious")}.Where(b=>b.isActiveAndEnabled&&b.IsInteractable()).ToArray();
            Assert.That(actions,Does.Contain(Field<Button>("closeButton")));
            foreach(var button in actions)
            {
                var rect=(RectTransform)button.transform;var bounds=ScreenBounds(rect);var canvas=button.GetComponentInParent<Canvas>();
                Assert.That(bounds.xMin,Is.GreaterThanOrEqualTo(panelBounds.xMin-1));Assert.That(bounds.xMax,Is.LessThanOrEqualTo(panelBounds.xMax+1));
                Assert.That(bounds.yMin,Is.GreaterThanOrEqualTo(panelBounds.yMin-1));Assert.That(bounds.yMax,Is.LessThanOrEqualTo(panelBounds.yMax+1));
                var r=rect.rect;Assert.That(r.width,Is.GreaterThan(8));Assert.That(r.height,Is.GreaterThan(8));
                foreach(var point in new[]{r.center,r.min+Vector2.one*4,r.max-Vector2.one*4,new Vector2(r.xMin+4,r.yMax-4),new Vector2(r.xMax-4,r.yMin+4)})
                {
                    var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(point))};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Assert.That(hits.Count,Is.GreaterThan(0),button.name);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(button.gameObject),button.name+" must be topmost");
                }
            }
            for(int i=0;i<actions.Length;i++)for(int j=i+1;j<actions.Length;j++)
                Assert.That(ScreenBounds((RectTransform)actions[i].transform).Overlaps(ScreenBounds((RectTransform)actions[j].transform)),Is.False,"Order actions overlap");
        }

        void Expect(long coins, long sequence, int stage, int hint)
        {
            var state = Session.State;
            Assert.That(state.coins, Is.EqualTo(coins));
            Assert.That(state.sequence, Is.EqualTo(sequence));
            Assert.That(state.revision, Is.EqualTo(sequence * 2));
            Assert.That(state.houseStage, Is.EqualTo(stage));
            Assert.That(state.selectedStage, Is.Zero);
            Assert.That(state.autoHintSeen, Is.EqualTo(hint));
            Assert.That(state.pending, Is.Null);
            Assert.That(codec.Decode(File.ReadAllText(Path.Combine(directory, "jtn.save.v1.json"))).sequence, Is.EqualTo(sequence));
            Assert.That(File.ReadAllText(Path.Combine(directory, "jtn.save.v1.json")), Is.EqualTo(codec.Encode(state)),
                "Every committed state must be written to the verified private primary");
            AssertPortraitLayout();
        }

        void AssertPortraitLayout()
        {
            Canvas.ForceUpdateCanvases();
            var safe = Field<RectTransform>("safe");
            // Product layout: the yard takes .533-.894 and the slot block .11-.488 of the safe area.
            Assert.That(Field<RectTransform>("world").rect.height / safe.rect.height, Is.EqualTo(.361f).Within(.001f));
            Assert.That(((RectTransform)safe.Find("Controls 40%")).rect.height / safe.rect.height, Is.EqualTo(.378f).Within(.001f));
            foreach (var button in game.GetComponentsInChildren<Button>(false))
            {
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = safe.InverseTransformPoint(corner);
                    Assert.That(point.x, Is.InRange(safe.rect.xMin - .1f, safe.rect.xMax + .1f), button.name + " outside safe width");
                    Assert.That(point.y, Is.InRange(safe.rect.yMin - .1f, safe.rect.yMax + .1f), button.name + " outside safe height");
                }
            }
            foreach (var text in game.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (!text.enabled) continue;
                text.ForceMeshUpdate();
                Assert.That(text.isTextOverflowing, Is.False, "Visible text overflow: " + text.name + " = " + text.text + " @" + text.fontSize + " in " + text.rectTransform.rect.size);
            }
            // The product layout hides these legacy labels; only a visible one can obscure anything.
            if(Field<TMPro.TMP_Text>("waveStatus").gameObject.activeInHierarchy)
                Assert.That(ScreenBounds(Field<TMPro.TMP_Text>("waveStatus").rectTransform).Overlaps(ScreenBounds(Field<TMPro.TMP_Text>("heading").rectTransform)),Is.False,"Skill/carry status must not obscure the heading");
            if(Session.Chapter.Progress.EraId==EraIds.WorkshopYard&&Field<TMPro.TMP_Text>("stageLabel").gameObject.activeInHierarchy)
                Assert.That(ScreenBounds(Field<Image>("house").rectTransform).Overlaps(ScreenBounds(Field<TMPro.TMP_Text>("stageLabel").rectTransform)),Is.False,"Opaque yard placeholder must not obscure the selected goal");
            if(Field<int>("wavePanel")==11&&Field<Button>("chapterPrevious").gameObject.activeInHierarchy&&Field<Button>("selectButton").gameObject.activeInHierarchy)
                Assert.That(ScreenBounds(Field<Button>("chapterPrevious").GetComponent<RectTransform>()).Overlaps(ScreenBounds(Field<Button>("selectButton").GetComponent<RectTransform>())),Is.False,"Cancel and claim must be separate unobscured actions");
        }

        static Rect ScreenBounds(RectTransform rect)
        {
            var points=new Vector3[4];rect.GetWorldCorners(points);
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }

        [UnityTest]
        public IEnumerator RepeatedSpinPointersCommitOneOutcomeAndDoNotQueue()
        {
            yield return Prepare(ModelState.Fresh(1));
            int firstFrame = Frames;
            yield return PointerClick("spinButton");
            Assert.That(Busy, Is.True);
            Assert.That(Field<Button>("spinButton").interactable, Is.False);
            for (int i = 0; i < 6; i++) { yield return PointerClick("spinButton"); yield return null; }
            // Seed 1's independently fixed result is potato/potato/trash, exact pair = 8.
            Expect(45, 1, 0, 0);
            Assert.That(Session.State.lastSpin.symbols, Is.EqualTo(new[] { 0, 0, 2 }));
            Assert.That(Session.State.lastSpin.reward, Is.EqualTo(8));
            yield return FinishPresentation();
            Expect(45, 1, 0, 0);
            Assert.That(Frames, Is.GreaterThan(firstFrame));
            Assert.That(Field<Button>("spinButton").interactable, Is.True);
            TestContext.WriteLine("Seven SPIN pointer clicks: exactly sequence 1, 45 coins, stage 0; advancing presentation; no queued spin.");
        }

        [UnityTest]
        public IEnumerator AffordablePreviewNeedsSelectionAndHintAcknowledgementSurvivesReentry()
        {
            // Deliberately affordable fixture: preview must still leave all 640 coins untouched.
            var state = ModelState.Fresh(1); state.coins = 640;
            yield return Prepare(state);
            yield return PointerClick("yardButtons",0);
            Expect(640, 0, 0, 0);
            yield return PointerClick("closeButton");
            Expect(640, 0, 0, 0);
            yield return PointerClick("yardButtons",0);
            yield return PointerClick("selectButton");
            Expect(600, 1, 1, 0);
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True, "First repair opens AUTO hint");
            Assert.That(Field<Button>("selectButton").gameObject.activeSelf, Is.False);
            yield return ReenterScene();
            Expect(600, 1, 1, 0);
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True, "Unacknowledged hint survives scene exit");
            yield return PointerClick("closeButton");
            Expect(600, 2, 1, 1);
            yield return ReenterScene();
            Expect(600, 2, 1, 1);
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            Assert.That(Auto, Is.False);
            TestContext.WriteLine("Preview/close: 640 coins, sequence 0. Explicit repair: 600 coins, sequence 1. Hint acknowledgement: sequence 2; retained across scene re-entry, no automatic successor.");
        }

        [UnityTest]
        public IEnumerator TwoProjectChoicePurchasesOnlyInspectedTargetAndRetainsBonus()
        {
            // 480 for the house improvement plus 120 for the Workshop at current prices.
            var state=ModelState.Fresh(1);state.houseStage=1;state.autoHintSeen=1;state.coins=600;
            yield return Prepare(state);
            yield return PointerClick("yardButtons",1);
            Assert.That(Session.State.coins,Is.EqualTo(600),"Inspection must not purchase");
            yield return PointerClick("closeButton");
            yield return PointerClick("yardButtons",0);yield return PointerClick("selectButton");
            Expect(120,1,2,1);
            Assert.That(Session.State.workshopBuilt,Is.Zero);
            Assert.That(HouseGoal.Complete(Session.State),Is.False,"House alone no longer ends the milestone");
            Assert.That(Field<Button>("autoButton").interactable,Is.True);
            yield return PointerClick("yardButtons",1);yield return PointerClick("selectButton");
            Expect(0,2,2,1);Assert.That(Session.State.workshopBuilt,Is.EqualTo(1));
            Assert.That(HouseGoal.Complete(Session.State),Is.False,"Recycler/loft still required; shop optional");
            yield return PointerClick("spinButton");Expect(9,3,2,1);
            Assert.That(Session.State.lastSpin.reward,Is.EqualTo(8));Assert.That(Session.State.lastSpin.bonus,Is.EqualTo(1));
            yield return FinishPresentation();yield return ReenterScene();Expect(9,3,2,1);
            Assert.That(Session.State.workshopBuilt,Is.EqualTo(1));Assert.That(Auto,Is.False);
            TestContext.WriteLine("House first: 720 -> 120 -> workshop 0; independent pair 8 + workshop 1 -> 9, retained on reload.");
        }

        [UnityTest]
        public IEnumerator FinalAutoSpinStopsAndRestoresExactCommittedResultWithoutFuturePurchase()
        {
            // A valid private checkpoint just before the last purchase; no long spin campaign.
            var state = BeforeFinalProject();long finalSequence=state.sequence+1;
            yield return Prepare(state);
            yield return PointerClick("autoButton");
            Assert.That(Busy, Is.True);
            Expect(22, finalSequence, 2, 1); // 699 + pair8 + workshop1 + recycler1 + potatoes10 + trash3 - 700.
            Assert.That(HouseGoal.Complete(Session.State),Is.True);
            Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            yield return FinishPresentation();
            Assert.That(Auto, Is.False);
            Assert.That(Field<Button>("autoButton").interactable, Is.False);
            yield return PointerClick("autoButton");
            yield return PointerClick("yardButtons",0);
            Assert.That(Field<Button>("selectButton").gameObject.activeSelf, Is.False);
            yield return PointerClick("closeButton");
            Expect(22, finalSequence, 2, 1);
            yield return ReenterScene();
            Expect(22, finalSequence, 2, 1);
            Assert.That(Session.State.lastSpin.symbols, Is.EqualTo(new[] { 0, 0, 2 }));
            Assert.That(Session.State.lastSpin.reward, Is.EqualTo(8));
            Assert.That(Auto, Is.False);
            Assert.That(Field<Button>("autoButton").interactable, Is.False);
            Assert.That(Field<Button>("spinButton").interactable, Is.True, "Optional free manual spins remain available");
            var icons = Field<Image[]>("reelImages");
            var symbols = Field<Sprite[]>("reelSymbols");
            Assert.That(icons.Select(icon => icon.sprite), Is.EqualTo(new[] { symbols[0], symbols[0], symbols[2] }),
                "Restored reel images must present the committed result");
            Assert.That(icons.All(icon => icon.enabled), Is.True);
            TestContext.WriteLine("Final chapter AUTO:699+23-700=22; new potato effect excluded from acquisition spin. All24 projects complete, no skills required; AUTO stopped and exact receipt survived reload.");
        }

        [UnityTest]
        public IEnumerator WaveShopAndProjectsCommitThroughVisiblePointerControlsAndReload()
        {
            var state=ModelState.Fresh(1);state.houseStage=1;state.autoHintSeen=1;state.coins=540;state.selectedStage=2;
            yield return Prepare(state);
            yield return OpenSkill(0,1);
            Assert.That(Session.State.coins,Is.EqualTo(540));Assert.That(Session.State.sequence,Is.Zero);
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("420"));AssertPortraitLayout();
            Session.Checkpoint(1001); // A changed aggregate invalidates the displayed quote.
            yield return PointerClick("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(540));Assert.That(WaveProgress.Of(Session.State).Slot.Level,Is.Zero);
            yield return OpenSkill(0,1);
            yield return PointerClick("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(420));Assert.That(Session.State.selectedStage,Is.EqualTo(2));
            Assert.That(WaveProgress.Of(Session.State).Slot.Level,Is.EqualTo(1));
            yield return OpenFamily(2);
            Assert.That(Session.State.coins,Is.EqualTo(420));AssertPortraitLayout();
            yield return PointerClick("selectButton");
            Expect(240,3,1,1);Assert.That(WaveProgress.Of(Session.State).RecyclerBuilt,Is.EqualTo(1));
            yield return OpenFamily(3);
            AssertPortraitLayout();yield return PointerClick("selectButton");Expect(0,4,1,1);
            Assert.That(WaveProgress.Of(Session.State).PigeonLoftBuilt,Is.EqualTo(1));
            yield return PointerClick("spinButton");Expect(10,5,1,1);
            Assert.That(Session.State.lastSpin.reward,Is.EqualTo(8));Assert.That(Session.State.lastSpin.chapter.Effects.UniversalCoins,Is.EqualTo(1));
            Assert.That(Session.State.lastSpin.chapter.Effects.Baseline.RecyclerCoins,Is.EqualTo(1));
            yield return FinishPresentation();yield return ReenterScene();Expect(10,5,1,1);
            Assert.That(WaveProgress.Of(Session.State).Delivery.PigeonProgress,Is.Zero);
            TestContext.WriteLine("Pointer path: inspect540 unchanged -> slot420 preserving house target -> explicit recycler240 -> loft0 -> pair8+slot1+recycler1=10, saved and reloaded.");
        }

        [UnityTest]
        [Category("LocalDiagnostics")]
        public IEnumerator BothErasAndSixSkillsTraverseVisibleControlsWithResumableTransition()
        {
            var state=ModelState.Fresh(1);state.coins=20000;
            yield return Prepare(state);
            Assert.That(Diagnostics.Count,Is.Zero,"Loading funded historical progress emits no backfill");
            for(int family=0;family<6;family++)for(int level=1;level<=2;level++)
            {
                yield return OpenFamily(family);yield return PointerClick("selectButton");
                if(family==0&&level==1)yield return PointerClick("closeButton");
                Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            }
            Assert.That(ChapterCatalog.Rules.CanUnlockWorkshopYard(Session.Chapter.Progress),Is.True);
            Assert.That(Field<Button>("autoButton").interactable,Is.False);
            for(int branch=0;branch<3;branch++)
            { yield return OpenSkill(branch,1);yield return PointerClick("selectButton"); }
            long wallet=Session.State.coins,sequence=Session.State.sequence;uint seed=Session.State.seed;
            yield return PointerClick("productTabs",0);yield return PointerClick("productCards",0);yield return PointerClick("navigationEraButton");
            yield return PointerClick("closeButton");
            Assert.That(Session.State.sequence,Is.EqualTo(sequence),"Cancelling era offer writes nothing");
            yield return PointerClick("productTabs",0);yield return PointerClick("productCards",0);yield return PointerClick("navigationEraButton");yield return PointerClick("selectButton");
            Assert.That(Session.Chapter.TransitionPending,Is.True);Assert.That(Session.State.coins,Is.EqualTo(wallet));
            Assert.That(Diagnostics.Count(r=>r.Event.Kind==LocalEventKind.ProjectSelected),Is.EqualTo(12));
            Assert.That(Diagnostics.Count(r=>r.Event.Kind==LocalEventKind.ProjectCompleted),Is.EqualTo(12));
            Assert.That(Diagnostics.Count(r=>r.Event.Kind==LocalEventKind.EraEntered),Is.EqualTo(1));
            Assert.That(Diagnostics.Where(r=>r.Event.Kind==LocalEventKind.ProjectSelected||r.Event.Kind==LocalEventKind.ProjectCompleted).GroupBy(r=>r.Event.OperationSequence).All(group=>group.Count()==2),Is.True,"Immediate selection/completion share a command identity, with distinct meanings");
            Assert.That(Session.State.seed,Is.EqualTo(seed));AssertPortraitLayout();
            yield return ReenterScene();
            Assert.That(Session.Chapter.TransitionPending,Is.True);Assert.That(Field<GameObject>("panel").activeSelf,Is.True);
            Assert.That(Diagnostics.Count,Is.Zero,"Reload does not replay old success observations");
            Assert.That(Field<TMPro.TMP_Text>("panelTitle").text,Does.Contain("Workshop Yard"));
            yield return PointerClick("closeButton"); // Skip acknowledges presentation, never rolls back entry.
            Assert.That(Session.Chapter.TransitionPending,Is.False);Assert.That(Session.Chapter.Progress.EraId,Is.EqualTo(EraIds.WorkshopYard));
            Assert.That(Session.State.coins,Is.EqualTo(wallet));
            for(int family=0;family<6;family++)for(int level=1;level<=2;level++)
            { yield return OpenFamily(family);yield return PointerClick("selectButton"); }
            for(int branch=0;branch<3;branch++)
            { yield return OpenSkill(branch,2);yield return PointerClick("selectButton"); }
            Assert.That(Session.State.coins,Is.EqualTo(10080));Assert.That(Session.Chapter.Skills.Receipts.Count,Is.EqualTo(6));
            Assert.That(HouseGoal.Complete(Session.State),Is.True);Assert.That(Session.Chapter.SelectedProjectId,Is.Null);
            Assert.That(Diagnostics.Count,Is.EqualTo(24));Assert.That(Diagnostics.Any(r=>r.Event.Kind==LocalEventKind.TutorialCompleted),Is.False);
            string committed=codec.Encode(Session.State);yield return ReenterScene();
            Assert.That(codec.Encode(Session.State),Is.EqualTo(committed));Assert.That(Auto,Is.False);AssertPortraitLayout();
            TestContext.WriteLine("Pointer traversal:24 project purchases +6 skill purchases cost9920 exactly;20000→10080, free era entry survives reload and skip ACK, final state and all six receipts retained.");
        }

        [UnityTest]
        public IEnumerator OrdersUseNewForegroundEventsAndCancelOnlyAfterExplicitConfirmation()
        {
            yield return Prepare(CompletedFixture().State);
            yield return OpenOrder();long sequence=Session.State.sequence;
            Assert.That(Session.State.coins,Is.EqualTo(690));yield return PointerClick("selectButton");
            long instance=Session.Journey.Orders.InstanceSequence;
            Assert.That(instance,Is.EqualTo(sequence+1));Assert.That(Session.Journey.Orders.Progress,Is.Zero);Assert.That(Session.State.coins,Is.EqualTo(690));
            Assert.That(Field<TMPro.TMP_Text>("message").text,Does.EndWith("Order accepted: 0/12. Only new qualifying results count."));
            yield return PointerClick("spinButton");
            Assert.That(Session.State.coins,Is.EqualTo(715));Assert.That(Session.Journey.Orders.Progress,Is.EqualTo(1));
            Assert.That(Session.State.lastSpin.journey.After.Orders.Progress,Is.EqualTo(1));yield return FinishPresentation();
            string before=codec.Encode(Session.State);
            yield return OpenOrder();yield return PointerClick("chapterPrevious");yield return PointerClick("closeButton");
            Assert.That(codec.Encode(Session.State),Is.EqualTo(before),"Closing forfeiture confirmation preserves all progress");
            yield return OpenOrder();yield return PointerClick("chapterPrevious");yield return PointerClick("selectButton");
            Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);Assert.That(Session.State.coins,Is.EqualTo(715));
            Assert.That(Field<TMPro.TMP_Text>("message").text,Does.EndWith("Order canceled without reward."));
            yield return ReenterScene();yield return OpenOrder();yield return PointerClick("selectButton");
            Assert.That(Session.Journey.Orders.InstanceSequence,Is.GreaterThan(instance));Assert.That(Session.Journey.Orders.Progress,Is.Zero);
            Assert.That(Session.State.coins,Is.EqualTo(715));Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(sequence+2));
            TestContext.WriteLine("Order pointer: accept690 unchanged, one seed1spin+25/progress1, cancelled confirmation noIO, explicitforfeit preserves715, repeatnewinstanceprogress0/no retrocredit.");
        }

        [UnityTest]
        [Category("LocalDiagnostics")]
        public IEnumerator ReadyOrderClaimPaysOnceAndNewInstanceKeepsOldReceiptWithoutRetroCredit()
        {
            var fixture=CompletedFixture();fixture.AcceptOrder("order.sort-salvage",fixture.State.sequence+1);
            for(int spin=0;spin<16;spin++)fixture.ExecuteChapterSpin(fixture.State.sequence+1);
            Assert.That(fixture.State.coins,Is.EqualTo(1062));Assert.That(fixture.Journey.Orders.Ready,Is.True);
            yield return Prepare(fixture.State);
            Assert.That(Diagnostics.Count,Is.Zero);
            long instance=Session.Journey.Orders.InstanceSequence,spinSequence=Session.State.lastSpin.sequence;uint seed=Session.State.seed;
            yield return OpenOrder();Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("Previous order"));
            Assert.That(Session.Journey.Orders.Ready,Is.True);Assert.That(Session.Journey.Orders.LatestClaim,Is.Null);
            OrderResultSnapshot("ready120-before-claim","Private public-command completed chapter, explicitly accepted sorting order and 16 deterministic setup spins; Ready12/12, 1062 Coins, no prior claim; UI detail opened without paying. Load this Ready state and perform an actual Claim for transient result capture");
            var staleClaim=Field<Button>("selectButton").onClick;yield return PointerClick("selectButton");long claimSequence=Session.State.sequence;
            var recorded=Diagnostics.Single();Assert.That(recorded.Event.Kind,Is.EqualTo(LocalEventKind.OrderClaimed));Assert.That(recorded.Event.OperationSequence,Is.EqualTo(claimSequence));Assert.That(recorded.Event.InstanceSequence,Is.EqualTo(instance));
            Assert.That(Session.State.coins,Is.EqualTo(1182));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(spinSequence));
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
            Assert.That(Field<TMPro.TMP_Text>("navigationContext").isActiveAndEnabled,Is.True);
            Assert.That(Field<TMPro.TMP_Text>("navigationContext").text,Does.Contain("+120 Coins").And.Contain(ChapterNarrative.OrderClaimed("order.sort-salvage")));
            Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);OrderResultSnapshot("claim120","Public chapter and order commands plus 16 deterministic fixture spins; actual UI Claim credits 120 once and opens list without next acceptance");
            string paid=codec.Encode(Session.State);staleClaim.Invoke();Assert.That(codec.Encode(Session.State),Is.EqualTo(paid));Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
            yield return OrderResultBackToGame();Assert.That(codec.Encode(Session.State),Is.EqualTo(paid));yield return ReenterScene();
            Assert.That(Diagnostics.Count,Is.Zero);
            string claimed=codec.Encode(Session.State);Session.ClaimOrder("order.sort-salvage",instance,claimSequence);
            Assert.That(codec.Encode(Session.State),Is.EqualTo(claimed));
            yield return OpenOrder();Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Previous order already paid 120 Coins."));
            yield return PointerClick("selectButton");
            Assert.That(Session.Journey.Orders.Progress,Is.Zero);Assert.That(Session.Journey.Orders.InstanceSequence,Is.GreaterThan(instance));
            string repeated=codec.Encode(Session.State);Session.ClaimOrder("order.sort-salvage",instance,claimSequence);
            Assert.That(codec.Encode(Session.State),Is.EqualTo(repeated));Assert.That(Session.State.coins,Is.EqualTo(1182));AssertCurrentProductLayout();
            yield return OpenOrder();
            Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Progress: 0/12.").And.Contain("Previous order already paid 120 Coins."));
            yield return PointerClick("closeButton");Assert.That(codec.Encode(Session.State),Is.EqualTo(repeated));
            Assert.That(Diagnostics.Count,Is.Zero,"Accept, old claim replay and mere progress are not success events");
            TestContext.WriteLine("Ready pointer claim:1062→1182 once, no RNG/spin/carry/construction. Reload and old exact claim replay preserve newer active instance and full live state.");
        }

        [UnityTest]
        public IEnumerator DeliveryOrderClaimReportsItsCommitted180Reward()
        {
            var fixture=CompletedFixture();fixture.AcceptOrder("order.sort-salvage",fixture.State.sequence+1);
            for(int spin=0;spin<80&&!fixture.Journey.Orders.Ready;spin++)fixture.ExecuteChapterSpin(fixture.State.sequence+1);
            Assert.That(fixture.Journey.Orders.Ready,Is.True);
            fixture.ClaimOrder("order.sort-salvage",fixture.Journey.Orders.InstanceSequence,fixture.State.sequence+1);
            fixture.AcceptOrder("order.neighborhood-supply",fixture.State.sequence+1);
            for(int spin=0;spin<80&&!fixture.Journey.Orders.Ready;spin++)fixture.ExecuteChapterSpin(fixture.State.sequence+1);
            Assert.That(fixture.Journey.Orders.Ready,Is.True);
            yield return Prepare(fixture.State);
            long coins=Session.State.coins,spinSequence=Session.State.lastSpin.sequence;uint seed=Session.State.seed;
            yield return OpenOrder(1);Assert.That(Session.Journey.Orders.Ready,Is.True);Assert.That(Session.Journey.Orders.ActiveOrderId,Is.EqualTo("order.neighborhood-supply"));
            OrderResultSnapshot("ready180-before-claim","Private public-command completed chapter, previously claimed sorting order and separately accepted delivery order with bounded setup spins; Ready delivery pays180 on the next explicit Claim and retains the earlier sorting receipt. Load this Ready state and perform an actual Claim for transient result capture");
            var staleClaim=Field<Button>("selectButton").onClick;yield return PointerClick("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(coins+180));Assert.That(Session.State.seed,Is.EqualTo(seed));
            Assert.That(Session.State.lastSpin.sequence,Is.EqualTo(spinSequence));Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);
            Assert.That(Session.Journey.Orders.LatestClaim.RewardCoins,Is.EqualTo(180));
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
            Assert.That(Field<TMPro.TMP_Text>("navigationContext").isActiveAndEnabled,Is.True);
            Assert.That(Field<TMPro.TMP_Text>("navigationContext").text,Does.Contain("+180 Coins").And.Contain(ChapterNarrative.OrderClaimed("order.neighborhood-supply")));
            OrderResultSnapshot("claim180","Public chapter, prior sorting claim and actual ready delivery fixture; actual UI Claim credits 180 once and opens list without next acceptance");
            string paid=codec.Encode(Session.State);staleClaim.Invoke();Assert.That(codec.Encode(Session.State),Is.EqualTo(paid));Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
            yield return OrderResultBackToGame();Assert.That(codec.Encode(Session.State),Is.EqualTo(paid));yield return ReenterScene();string claimed=codec.Encode(Session.State);
            yield return OpenOrder(1);Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Previous order already paid 180 Coins."));
            yield return PointerClick("closeButton");Assert.That(codec.Encode(Session.State),Is.EqualTo(claimed));
            yield return OpenOrder();Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("Previous order"),"The global retained delivery claim cannot certify the older sorting claim");
            yield return PointerClick("closeButton");Assert.That(codec.Encode(Session.State),Is.EqualTo(claimed));
        }

        [UnityTest]
        public IEnumerator PreviousOrderReceiptDoesNotPayOrDescribeTheNewReadyInstance()
        {
            var fixture=CompletedFixture();string older=codec.Encode(fixture.State);
            fixture.AcceptOrder("order.sort-salvage",fixture.State.sequence+1);
            for(int spin=0;spin<80&&!fixture.Journey.Orders.Ready;spin++)fixture.ExecuteChapterSpin(fixture.State.sequence+1);
            Assert.That(fixture.Journey.Orders.Ready,Is.True);
            fixture.ClaimOrder("order.sort-salvage",fixture.Journey.Orders.InstanceSequence,fixture.State.sequence+1);
            long previousClaim=fixture.Journey.Orders.LatestClaim.ClaimSequence;
            fixture.AcceptOrder("order.sort-salvage",fixture.State.sequence+1);
            for(int spin=0;spin<80&&!fixture.Journey.Orders.Ready;spin++)fixture.ExecuteChapterSpin(fixture.State.sequence+1);
            Assert.That(fixture.Journey.Orders.Ready,Is.True);
            yield return Prepare(fixture.State);
            Func<string[]> files=()=>Directory.GetFiles(directory).OrderBy(path=>path).Select(path=>Path.GetFileName(path)+"|"+Convert.ToBase64String(File.ReadAllBytes(path))+"|"+File.GetLastWriteTimeUtc(path).Ticks).ToArray();
            string before=codec.Encode(Session.State);var disk=files();
            for(int open=0;open<2;open++)
            {
                yield return OpenOrder();string body=Field<TMPro.TMP_Text>("panelBody").text;
                Assert.That(body,Does.Contain("Progress: 12/12.").And.Contain("Collect your reward").And.Contain("Previous order already paid 120 Coins."));
                Assert.That(Field<Button>("selectButton").gameObject.activeInHierarchy,Is.True);
                yield return PointerClick("closeButton");
                yield return OpenOrder(1);Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("Previous order"));
                yield return PointerClick("closeButton");yield return ReenterScene();
                Assert.That(codec.Encode(Session.State),Is.EqualTo(before));Assert.That(files(),Is.EqualTo(disk));
                Assert.That(Session.Journey.Orders.LatestClaim.ClaimSequence,Is.EqualTo(previousClaim));
            }
            yield return OpenOrder();yield return PointerClick("chapterPrevious");yield return PointerClick("selectButton");
            long coins=Session.State.coins;string cancelled=codec.Encode(Session.State);disk=files();
            yield return OpenOrder();Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("Previous order already paid 120 Coins."));
            yield return PointerClick("closeButton");Assert.That(codec.Encode(Session.State),Is.EqualTo(cancelled));Assert.That(files(),Is.EqualTo(disk));
            Assert.That(Session.State.coins,Is.EqualTo(coins));
            // Replace only this private fixture with its actual older valid snapshot; no claim history is fabricated.
            SceneManager.SetActiveScene(previousScene);yield return SceneManager.UnloadSceneAsync(loadedScene);game=null;
            var restored=codec.Decode(older);restored.lastSettledAt=1000;
            File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),codec.Encode(restored));yield return LoadGame();
            before=codec.Encode(Session.State);disk=files();
            yield return OpenOrder();Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Not.Contain("Previous order"));
            yield return PointerClick("closeButton");Assert.That(codec.Encode(Session.State),Is.EqualTo(before));Assert.That(files(),Is.EqualTo(disk));
        }

        [UnityTest]
        public IEnumerator OrderMenuBackRestoresFullActionGeometryWithoutClaimOrCancel()
        {
            var fixture=CompletedFixture();fixture.AcceptOrder("order.sort-salvage",fixture.State.sequence+1);
            for(int spin=0;spin<16;spin++)fixture.ExecuteChapterSpin(fixture.State.sequence+1);
            yield return Prepare(fixture.State);
            var select=Field<Button>("selectButton").GetComponent<RectTransform>();var previous=Field<Button>("chapterPrevious").GetComponent<RectTransform>();
            var label=Field<Button>("selectButton").GetComponentInChildren<TMPro.TMP_Text>();
            // Simulate distinct authored geometry before the first panel mutation.
            select.anchorMin=new Vector2(.13f,.18f);select.anchorMax=new Vector2(.87f,.33f);
            previous.anchorMin=new Vector2(.11f,.185f);previous.anchorMax=new Vector2(.42f,.29f);
            label.enableAutoSizing=false;label.fontSize=13.25f;label.fontSizeMin=8.5f;label.fontSizeMax=19.75f;
            Vector2 selectMin=select.anchorMin,selectMax=select.anchorMax,previousMin=previous.anchorMin,previousMax=previous.anchorMax;
            float font=label.fontSize,min=label.fontSizeMin,max=label.fontSizeMax;bool sizing=label.enableAutoSizing;
            yield return OpenOrder();Assert.That(select.anchorMin.x,Is.GreaterThan(selectMin.x));
            string before=codec.Encode(Session.State);
            var type=Type.GetType("JunkToNeon.Presentation.JtnBackNavigation, JunkToNeon.Presentation",true);
            var back=game.GetComponent(type);Assert.That(back,Is.Not.Null);
            Assert.That((bool)type.GetMethod("RequestBack").Invoke(back,null),Is.True);yield return null;
            // Back from an order returns to the order list of the product route.
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));Assert.That(codec.Encode(Session.State),Is.EqualTo(before));
            Assert.That(Session.Journey.Orders.Ready,Is.True);Assert.That(Session.State.coins,Is.EqualTo(codec.Decode(before).coins),"Back must not claim the 120-Coin reward");
            Assert.That(select.anchorMin,Is.EqualTo(selectMin));Assert.That(select.anchorMax,Is.EqualTo(selectMax));
            Assert.That(previous.anchorMin,Is.EqualTo(previousMin));Assert.That(previous.anchorMax,Is.EqualTo(previousMax));
            Assert.That(label.fontSize,Is.EqualTo(font));Assert.That(label.fontSizeMin,Is.EqualTo(min));Assert.That(label.fontSizeMax,Is.EqualTo(max));Assert.That(label.enableAutoSizing,Is.EqualTo(sizing));
            yield return OpenSkill(0,1);
            Assert.That(Field<Button>("selectButton").gameObject.activeInHierarchy,Is.True);Assert.That(select.anchorMin,Is.EqualTo(selectMin));Assert.That(select.anchorMax,Is.EqualTo(selectMax));
            AssertPortraitLayout();Assert.That((bool)type.GetMethod("RequestBack").Invoke(back,null),Is.True);yield return null;
            Assert.That(codec.Encode(Session.State),Is.EqualTo(before));
            TestContext.WriteLine("Production Back closes a ready order without claim/cancel/IO; NONDEFAULT authored CTA/pagination anchors and font restore exactly, next skill offer keeps them and preserves912.");
        }

        [UnityTest]
        [Category("LocalDiagnostics")]
        public IEnumerator FreshGuidanceStaysNonblockingAndRecordsVisibilityOnlyAfterActualPresentation()
        {
            // Empty owned storage exercises actual fresh creation, rather than inferring origin from37coins.
            yield return LoadGame();
            Assert.That(Diagnostics.Count,Is.Zero);
            Assert.That(Session.Journey.Guidance.Origin,Is.EqualTo(GuideOrigin.Fresh));Assert.That(Session.State.coins,Is.EqualTo(37));
            Assert.That(Session.Guidance.Suggestion.HintId,Is.EqualTo(GuideIds.Welcome));
            string fresh=codec.Encode(Session.State);
            yield return PointerClick("guideButton");yield return PointerClick("closeButton");
            Assert.That(codec.Encode(Session.State),Is.EqualTo(fresh));yield return ReenterScene();
            Assert.That(Session.Guidance.Suggestion.HintId,Is.EqualTo(GuideIds.Welcome));
            yield return PointerClick("productGoalButton");yield return null;Canvas.ForceUpdateCanvases();yield return PointerClick("productTabs",1);
            yield return PointerClick("spinButton");yield return FinishPresentation();
            Assert.That(Session.State.coins==40||Session.State.coins==45||Session.State.coins==60,Is.True,"Real fresh creation uses its actual random seed; all three exclusive outcomes fund the40coin repair");
            long afterFirstSpin=Session.State.coins;Assert.That(Session.Journey.Guidance.TutorialBits,Is.EqualTo(5));
            yield return PointerClick("productGoalButton");yield return PointerClick("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(afterFirstSpin-40));Assert.That(Session.Journey.Guidance.TutorialBits,Is.EqualTo(7));
            long completion=Session.Guidance.LastCompletion.Sequence;
            Assert.That(Diagnostics.Select(r=>r.Event.Kind),Is.EqualTo(new[]{LocalEventKind.ProjectSelected,LocalEventKind.ProjectCompleted}));
            yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(Field<bool>("guideImprovementObserved"),Is.False,"The opaque AUTO hint currently covers completion feedback");
            Assert.That(Field<TMPro.TMP_Text>("heading").gameObject.activeInHierarchy,Is.False,"Covered narration must not count as presented");
            yield return PointerClick("closeButton");yield return null;Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();
            var reaction=Field<TMPro.TMP_Text>("heading");
            Assert.That(reaction.isActiveAndEnabled&&reaction.gameObject.activeInHierarchy,Is.True);
            Assert.That(ChapterNarrative.TryCompletion(GameIds.HouseRepair,out var repairBeat),Is.True);
            Assert.That(reaction.text,Does.Contain(repairBeat.Title));
            Assert.That(reaction.transform.parent,Is.EqualTo(Field<RectTransform>("productChrome")));
            Assert.That(reaction.raycastTarget,Is.False);reaction.ForceMeshUpdate();
            Assert.That(reaction.isTextOverflowing,Is.False,reaction.text);
            Assert.That(reaction.textBounds.size.x,Is.LessThanOrEqualTo(reaction.rectTransform.rect.width+2));
            Assert.That(reaction.textBounds.size.y,Is.LessThanOrEqualTo(reaction.rectTransform.rect.height+2));
            Assert.That(Field<bool>("guideImprovementObserved"),Is.True);
            Assert.That(Session.Journey.Guidance.TutorialBits,Is.EqualTo(7),"A rendered frame alone does not create a side save/event command");
            string captureDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/first-session-delivery-20261101/root"));
            Directory.CreateDirectory(captureDirectory);
            string capturePath=Path.Combine(captureDirectory,"repair-fresh-"+Guid.NewGuid().ToString("N")+".state.json");
            File.WriteAllText(capturePath,codec.Encode(Session.State),new System.Text.UTF8Encoding(false));
            TestContext.WriteLine("Private produced Fresh snapshot: "+capturePath);
            Assert.That(Diagnostics.Any(r=>r.Event.Kind==LocalEventKind.TutorialCompleted),Is.False);
            yield return PointerClick("spinButton");yield return FinishPresentation();
            Assert.That(Session.Journey.Guidance.TutorialBits,Is.EqualTo(31));Assert.That(Session.Guidance.LastCompletion.Sequence,Is.EqualTo(completion));
            Assert.That(Diagnostics.Count(r=>r.Event.Kind==LocalEventKind.TutorialCompleted),Is.EqualTo(1));Assert.That(Diagnostics.Count,Is.EqualTo(3),"No per-spin event/history");
            yield return ReenterScene();Assert.That(Session.Journey.Guidance.TutorialBits,Is.EqualTo(31));Assert.That(Session.Guidance.LastCompletion.Sequence,Is.EqualTo(completion));
            Assert.That(Diagnostics.Count,Is.Zero,"Loaded already-complete tutorial is silent");
            TestContext.WriteLine("Fresh origin fromactualcreation; welcome close/reload noACK, freepreview/nonblockingmanualspin, repairbits7, coveredfeedbacknotvisible, realframefactcarriednextlegitimateop→31, completionretained.");
        }

        [UnityTest]
        [Category("LocalDiagnostics")]
        public IEnumerator LocalDiagnosticsCategorizeBlockedSaveAndDiscardUnityLogContent()
        {
            const string malformed="{private-fixture-invalid-save";
            File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),malformed);
            yield return LoadGame(expectBlocked:true);
            Assert.That(Session.RecoveryRequired,Is.True);Assert.That(Diagnostics.Count,Is.EqualTo(1));
            var blocked=Diagnostics.Single();Assert.That(blocked.Event.Kind,Is.EqualTo(LocalEventKind.PersistenceError));Assert.That(blocked.Event.PersistenceCode,Is.EqualTo(PersistenceErrorCode.RecoveryBlocked));Assert.That(blocked.Event.OperationSequence,Is.Zero);
            Assert.That(File.ReadAllText(Path.Combine(directory,"jtn.save.v1.json")),Is.EqualTo(malformed));
            const string fakeSensitive="diagnostics fixture: X:/not-user-data/private token=fake-test-only";
            LogAssert.Expect(LogType.Error,fakeSensitive);Debug.LogError(fakeSensitive);yield return null;
            var error=Diagnostics.Last();Assert.That(error.Event.Kind,Is.EqualTo(LocalEventKind.RuntimeError));Assert.That(error.Event.RuntimeCode,Is.EqualTo(RuntimeErrorCode.UnityError));Assert.That(error.Event.SubjectId,Is.Null);
            Assert.That(error.Build.ReleaseLabel,Is.EqualTo("unity-r10-local-diagnostics"));Assert.That(Diagnostics.Count,Is.EqualTo(2));
            TestContext.WriteLine("Actual blocked private save remains unchanged; fixed persistence category and actual Unity callback retain no raw log content or success event.");
        }

        [UnityTest]
        public IEnumerator ExpandedLegacyCompletionNoticeAcknowledgesOnceWithoutGrantOrConstruction()
        {
            var old=ModelState.Fresh(1);old.coins=1000;old.houseStage=2;old.workshopBuilt=1;old.autoHintSeen=1;
            old.wave=new WaveProgress(1,1,DeliveryState.Fresh(EffectRules.Provisional),UpgradeOwnership.Empty);
            yield return Prepare(old);
            Assert.That(Session.Guidance.Suggestion.HintId,Is.EqualTo(GuideIds.ExpandedR7));Assert.That(Session.Journey.Guidance.Origin,Is.EqualTo(GuideOrigin.LegacyExpanded));
            yield return PointerClick("guideButton");Assert.That(Field<TMPro.TMP_Text>("panelBody").text,Does.Contain("old four-building version"));
            Assert.That(Session.State.coins,Is.EqualTo(1000));yield return PointerClick("selectButton");
            Assert.That(Session.State.coins,Is.EqualTo(1000));Assert.That(Session.State.houseStage,Is.EqualTo(2));Assert.That(Session.State.seed,Is.EqualTo(1u));Assert.That(Session.State.lastSpin,Is.Null);
            Assert.That(Session.Journey.Guidance.IsAcknowledged(GuideIds.ExpandedR7),Is.True);yield return ReenterScene();
            Assert.That(Session.Guidance.Suggestion?.HintId,Is.Not.EqualTo(GuideIds.ExpandedR7));Assert.That(Session.Journey.Guidance.Origin,Is.EqualTo(GuideOrigin.LegacyExpanded));AssertPortraitLayout();
            TestContext.WriteLine("Actualprechapterr7completion notice:explicitonce-onlyACK/reload,1000coins/seed/buildingspreserved,no grant/autobuy; expandedchapterremainsunfinished.");
        }

        [UnityTest]
        public IEnumerator AuthoredGoalPlacementSurvivesAwake()
        {
            yield return Prepare(ModelState.Fresh(1));
            // Clone the serialized hierarchy inactive so authored coordinates precede its first Awake.
            game.gameObject.SetActive(false);
            var clone = UnityEngine.Object.Instantiate(game.gameObject);
            var shell = clone.GetComponent(gameType);
            Assert.That(gameType.GetProperty("Session").GetValue(shell), Is.Null, "Clone must not have awakened yet");
            var label = (Component)gameType.GetField("stageLabel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell);
            var rect = (RectTransform)label.transform;
            var min = new Vector2(.17f, .43f); var max = new Vector2(.82f, .49f);
            var paddingMin = new Vector2(3, 5); var paddingMax = new Vector2(-7, -9);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = paddingMin; rect.offsetMax = paddingMax;
            clone.SetActive(true);
            yield return null;
            AssertOwnedSession((GameSession)gameType.GetProperty("Session").GetValue(shell));
            Assert.That(rect.anchorMin, Is.EqualTo(min), "Awake must preserve authored goal anchors");
            Assert.That(rect.anchorMax, Is.EqualTo(max));
            Assert.That(rect.offsetMin, Is.EqualTo(paddingMin));
            Assert.That(rect.offsetMax, Is.EqualTo(paddingMax));
            TestContext.WriteLine("Non-default goal anchors and padding survive first Awake on the serialized Canvas clone.");
        }

        [UnityTest]
        public IEnumerator OfflineReturnShowsOnceAndWaitsForForegroundPurchase()
        {
            var state=ModelState.Fresh(1);state.houseStage=1;state.workshopBuilt=1;state.autoHintSeen=1;
            state.coins=595;state.selectedStage=2;
            Environment.SetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS","1600");
            yield return Prepare(state);
            Assert.That(Session.State.coins,Is.EqualTo(605));Assert.That(Session.State.houseStage,Is.EqualTo(1));
            Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);Assert.That(Auto,Is.False);
            Assert.That(Session.State.lastSettlement.coins,Is.EqualTo(10));
            int frame=Frames;yield return null;Assert.That(Frames,Is.GreaterThan(frame));
            yield return ReenterScene();
            Assert.That(Session.State.coins,Is.EqualTo(605));Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);
            yield return PointerClick("closeButton");
            Assert.That(Session.State.lastSettlementSeen,Is.EqualTo(1));Assert.That(Session.State.houseStage,Is.EqualTo(1));
            yield return PointerClick("spinButton");
            yield return FinishPresentation();
            Assert.That(Session.State.coins,Is.EqualTo(14));Assert.That(Session.State.houseStage,Is.EqualTo(2));
            Assert.That(Session.State.lastSettlement.coins,Is.EqualTo(10));
            yield return ReenterScene();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            Assert.That(Session.State.coins,Is.EqualTo(14));
            TestContext.WriteLine("Offline return 595+10=605; summary survives reentry without replay; pointer acknowledgement defers purchase; foreground spin 605+8+1-600=14; reload stable, advancing frames.");
        }

        [UnityTest]
        public IEnumerator PauseQuitAndBackupRecoveryPreserveOneReturnReceipt()
        {
            var state=ModelState.Fresh(1);state.houseStage=1;state.workshopBuilt=1;state.autoHintSeen=1;state.coins=595;state.selectedStage=2;
            yield return Prepare(state);
            gameType.GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{true});
            Environment.SetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS","1600");
            gameType.GetMethod("OnApplicationQuit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
            Assert.That(codec.Decode(File.ReadAllText(Path.Combine(directory,"jtn.save.v1.json"))).lastSettledAt,Is.EqualTo(1000),"Background quit must preserve departure");
            gameType.GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{false});
            Assert.That(Session.State.coins,Is.EqualTo(605));Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);
            yield return PointerClick("closeButton");
            // Restore a frozen settlement from a deliberately corrupted private primary.
            state.lastSettledAt=1000;
            File.WriteAllText(Path.Combine(directory,"jtn.backup.v1.json"),codec.Encode(GameLogic.Reserve(state,"settle",0,1,1600)));
            File.WriteAllText(Path.Combine(directory,"jtn.save.v1.json"),"{private corrupt snapshot");
            LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("Junk to Neon storage stopped:"));
            gameType.GetMethod("Reload",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
            Assert.That(Session.RecoveryRequired,Is.True);
            gameType.GetMethod("RestoreBackup",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
            gameType.GetMethod("RestoreBackup",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
            Assert.That(Session.RecoveryRequired,Is.False,Session.Error);Assert.That(Session.State.coins,Is.EqualTo(605));
            Assert.That(Field<bool>("offlineSummaryPanel"),Is.True);Assert.That(Session.State.houseStage,Is.EqualTo(1));
            yield return PointerClick("closeButton");yield return ReenterScene();
            Assert.That(Session.State.coins,Is.EqualTo(605));Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            // Recovery archives intentionally remain as private test evidence; fixture cleanup removes them below.
            foreach(var path in Directory.GetFiles(directory,"jtn.recovery-*.json"))File.Delete(path);
            TestContext.WriteLine("Pause at1000 / background quit1600 preserves departure; resume grants10 once. Explicit private backup recovery applies frozen receipt once and displays it before normal play.");
        }

        [UnityTest,Category("Expansion5h")]
        public IEnumerator ExplicitFastPresentationFinishesSoonerWithOneUnchangedThreeSymbolOutcome()
        {
            yield return Prepare(ModelState.Fresh(1));
            Assert.That(Session.Expansion.SpinDurationSeconds,Is.EqualTo(3.5f));
            Assert.That(Field<Button>("spinButton").GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("SPIN"));
            Session.SelectExpansionSpeed("speed.fast",Session.State.sequence,1000);
            gameType.GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{null,null});
            Assert.That(Field<Button>("spinButton").GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("SPIN · 1.5 s"));
            long sequence=Session.State.sequence;int frames=Frames;float started=Time.realtimeSinceStartup;
            yield return PointerClick("spinButton");Assert.That(Busy,Is.True);
            float deadline=Time.realtimeSinceStartup+3;
            while(Busy&&Time.realtimeSinceStartup<deadline)yield return null;
            float elapsed=Time.realtimeSinceStartup-started;
            Assert.That(Busy,Is.False,"The chosen fast presentation must complete within a bounded wait");
            Assert.That(elapsed,Is.GreaterThanOrEqualTo(1.45f).And.LessThan(2.5f));
            Assert.That(Frames,Is.GreaterThan(frames));Assert.That(Auto,Is.False);
            var after=Session.State;Assert.That(after.sequence,Is.EqualTo(sequence+1));
            Assert.That(after.coins,Is.EqualTo(45));Assert.That(after.lastSpin.reward,Is.EqualTo(8));
            Assert.That(after.lastSpin.symbols,Is.EqualTo(new[]{0,0,2}));
            Assert.That(Session.Expansion.Parts,Is.EqualTo(1));Assert.That(Session.Expansion.Potatoes,Is.EqualTo(2));
            Assert.That(Session.Expansion.Energy,Is.EqualTo(1));Assert.That(after.pending,Is.Null);
            Assert.That(codec.Decode(File.ReadAllText(Path.Combine(directory,"jtn.save.v1.json"))).sequence,Is.EqualTo(after.sequence));
            TestContext.WriteLine("Observed fast foreground presentation "+elapsed+"s, advancing frames, one free seed1 base pair8/45wallet and1Part/2Potatoes/1Energy. No extra base reel or RNG draw.");
        }

        [UnityTearDown]
        public IEnumerator RestoreAndRemoveOnlyPrivateFixtures()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }
            Environment.SetEnvironmentVariable(SaveOverride, previousOverride);
            Environment.SetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS", previousClock);
            // Other Editors can legitimately advance this shared application directory concurrently.
            // Report that observation; fail on escaped storage paths/private snapshot mismatches above.
            var after = ObserveNormal();
            bool changed = !after.OrderBy(entry => entry.Key).SequenceEqual(normalSaveBytes.OrderBy(entry => entry.Key));
            TestContext.WriteLine("Read-only ordinary-save observation changed during parallel run: " + changed + ". No restoration attempted.");
            if (directory != null && TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                TestContext.WriteLine("Preserved failed Reel private fixture: " + directory);
                directory = null;
            }
            if (directory != null)
            {
                foreach (var name in new[] { "jtn.save.v1.json", "jtn.backup.v1.json", "jtn.pending-write.v1.json", "jtn.interrupted-write.v1.json" })
                {
                    var path = Path.Combine(directory, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                Directory.Delete(directory, false);
            }
        }
    }
}
#endif
