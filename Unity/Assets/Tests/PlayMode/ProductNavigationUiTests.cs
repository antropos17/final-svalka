#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JunkToNeon.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    public sealed class ProductNavigationUiTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui=new ReelUiLifecycleTests();
        readonly JsonStateCodec codec=new JsonStateCodec();
        Component Game=>(Component)typeof(ReelUiLifecycleTests).GetField("game",Hidden).GetValue(ui);
        GameSession Session=>(GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        string Folder=>(string)typeof(ReelUiLifecycleTests).GetField("directory",Hidden).GetValue(ui);
        T Field<T>(string n)=>(T)Game.GetType().GetField(n,Hidden).GetValue(Game);
        IEnumerator Harness(string n,params object[] args)=>(IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(n,Hidden).Invoke(ui,args);
        void Call(string n,params object[] args)=>Game.GetType().GetMethod(n,Hidden|BindingFlags.Public).Invoke(Game,args);
        string Text(string n)=>Field<TMP_Text>(n).text;
        Dictionary<string,string> Files()=>Directory.GetFiles(Folder).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
        void Unchanged(string state,Dictionary<string,string> files){Assert.That(codec.Encode(Session.State),Is.EqualTo(state));Assert.That(Files(),Is.EquivalentTo(files));}
        IEnumerator Click(string n,int index=-1){yield return Harness("PointerClick",n,index);yield return null;}
        PointerEventData Hold(Button b,int id)
        {
            var p=new PointerEventData(EventSystem.current){pointerId=id,button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerClick=b.gameObject,pointerCurrentRaycast=new RaycastResult{gameObject=b.gameObject}};
            ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler);return p;
        }
        void Release(Button b,PointerEventData p)
        {ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);}
        IEnumerator Back()
        {
            var b=Game.GetComponents<Component>().Single(c=>c.GetType().FullName=="JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)b.GetType().GetMethod("RequestBack").Invoke(b,null),Is.True);yield return null;
        }
        void Fits(TMP_Text t){t.ForceMeshUpdate();Assert.That(t.isTextOverflowing,Is.False,t.name+": "+t.text);Assert.That(t.textBounds.size.y,Is.LessThanOrEqualTo(t.rectTransform.rect.height+2),t.text);}
        void Target(Button b)
        {
            Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;Assert.That(r.rect.width,Is.GreaterThanOrEqualTo(48),b.name);Assert.That(r.rect.height,Is.GreaterThanOrEqualTo(48),b.name);
            foreach(var t in b.GetComponentsInChildren<TMP_Text>())Fits(t);
            foreach(var point in new[]{r.rect.center,r.rect.min+Vector2.one*5,r.rect.max-Vector2.one*5,new Vector2(r.rect.xMin+5,r.rect.yMax-5),new Vector2(r.rect.xMax-5,r.rect.yMin+5)})
            {
                var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(point))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
                Assert.That(hits.Count,Is.GreaterThan(0),b.name);Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(b.gameObject),b.name);
            }
        }
        void Hub(string title)
        {
            Assert.That(Text("panelTitle"),Is.EqualTo(title));Assert.That(Field<RectTransform>("productHubRoot").gameObject.activeInHierarchy,Is.True);
            Fits(Field<TMP_Text>("productContext"));foreach(var b in Field<Button[]>("productCards").Where(b=>b.gameObject.activeInHierarchy))Target(b);
        }
        [SetUp]public void Isolate(){ui.IsolateSave();Assert.That(Path.GetPathRoot(Folder).Replace('\\','/'),Is.EqualTo("X:/").IgnoreCase);}
        [UnityTest]public IEnumerator ProductFooterOrganizesRoutesAndStaleContactsCannotNavigateOrSpend()
        {
            yield return Harness("Prepare",ModelState.Fresh(1));string state=codec.Encode(Session.State);var files=Files();
            Assert.That(Field<RectTransform>("world").Find("Goal Readout").GetComponent<Image>().enabled,Is.False);
            foreach(var b in Field<Button[]>("productTabs"))Target(b);
            foreach(var n in new[]{"productAvatar","productSettings","productEnergy","productEventShortcut","productMapShortcut","productGoalButton"})Target(Field<Button>(n));
            if(Field<Button>("guideButton").gameObject.activeInHierarchy)Target(Field<Button>("guideButton"));
            foreach(var g in Game.GetComponentsInChildren<Graphic>(true).Where(g=>g.GetType().Name.StartsWith("JtnProduct")))Assert.That(g.GetComponent<CanvasRenderer>(),Is.Not.Null,g.name);
            var avatar=Field<Button>("productAvatar");var headerHeld=Hold(avatar,92);
            yield return Click("productTabs",0);Hub("My District");Release(avatar,headerHeld);Hub("My District");Unchanged(state,files);
            yield return Click("productCards",0);Assert.That(Field<int>("wavePanel"),Is.EqualTo(30));yield return Back();Hub("My District");
            yield return Click("productTabs",2);Hub("Yard Events");Assert.That(Text("productContext"),Does.Contain("Tickets: 0/3"));var old=Field<Button[]>("productCards")[0].onClick;
            headerHeld=Hold(avatar,93);Call("OnApplicationPause",true);Call("OnApplicationPause",false);yield return null;
            Release(avatar,headerHeld);Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));Assert.That(Field<RectTransform>("productHubRoot").gameObject.activeSelf,Is.False);Unchanged(state,files);
            yield return Click("productTabs",2);Hub("Yard Events");
            yield return Click("productCards",0);Assert.That(Field<int>("wavePanel"),Is.EqualTo(62));Unchanged(state,files);yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(60));yield return Back();Hub("Yard Events");
            yield return Click("productTabs",3);Hub("Shop");old.Invoke();Hub("Shop");Unchanged(state,files);
            var tabs=Field<Button[]>("productTabs");var held=new PointerEventData(EventSystem.current){pointerId=91,button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerClick=tabs[2].gameObject,pointerCurrentRaycast=new RaycastResult{gameObject=tabs[2].gameObject}};
            ExecuteEvents.Execute(tabs[2].gameObject,held,ExecuteEvents.pointerDownHandler);yield return Click("productTabs",4);Hub("Upgrades");
            ExecuteEvents.Execute(tabs[2].gameObject,held,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(tabs[2].gameObject,held,ExecuteEvents.pointerClickHandler);Hub("Upgrades");Unchanged(state,files);
            yield return Click("productEnergy");Hub("Energy & Bonus");Assert.That(Text("productContext"),Is.EqualTo("Bonus reel locked: 20 parts + 10 potatoes.\nMissing: 20 parts, 10 potatoes"));yield return Click("productCards",0);Assert.That(Field<int>("wavePanel"),Is.EqualTo(52));Unchanged(state,files);
            yield return Click("productTabs",1);Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Target(Field<Button>("spinButton"));Unchanged(state,files);
            var busyHeld=Hold(tabs[2],94);long beforeSpin=Session.State.sequence;yield return Click("spinButton");Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.True);Assert.That(Session.State.sequence,Is.EqualTo(beforeSpin+1));
            state=codec.Encode(Session.State);files=Files();foreach(var b in tabs){Assert.That(b.interactable,Is.False);b.onClick.Invoke();}Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Unchanged(state,files);yield return Harness("FinishPresentation");
            state=codec.Encode(Session.State);files=Files();Release(tabs[2],busyHeld);Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Unchanged(state,files);
            yield return Click("productTabs",2);Hub("Yard Events");Unchanged(state,files);
        }
        [UnityTest]public IEnumerator SavedScenarioProfileAndSettingsPreserveStateAndRequireFreshActions()
        {
            var saved=codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/ArcadeCore/Evidence/memory-partial.json"))));yield return Harness("Prepare",saved);
            string state=codec.Encode(Session.State);var files=Files();yield return Click("productEventShortcut");Hub("Yard Events");
            Assert.That(Field<Button[]>("productCards")[3].GetComponentInChildren<TMP_Text>().text,Does.Contain("Resume round"));yield return Click("productCards",3);Assert.That(Field<int>("wavePanel"),Is.EqualTo(63));Unchanged(state,files);
            yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(60));yield return Back();Hub("Yard Events");Unchanged(state,files);
            yield return Click("productAvatar");Hub("My Character");yield return Click("productCards",0);Assert.That(Field<int>("wavePanel"),Is.EqualTo(23));yield return Back();Hub("My Character");Unchanged(state,files);
            yield return Click("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(20));yield return Click("sectionButtons",6);Assert.That(Field<int>("wavePanel"),Is.EqualTo(25));yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(20));yield return Back();Hub("My Character");Unchanged(state,files);
            yield return Click("productSettings");Hub("Settings");var old=Field<Button[]>("productCards")[0].onClick;yield return Click("productCards",0);Hub("Settings");
            Assert.That((bool)Game.GetType().GetProperty("RewardFeedbackReduced").GetValue(Game),Is.True);old.Invoke();Assert.That((bool)Game.GetType().GetProperty("RewardFeedbackReduced").GetValue(Game),Is.True);Unchanged(state,files);
            Assert.That(Field<Button[]>("productCards")[0].GetComponentInChildren<TMP_Text>().text,Does.Contain("Off"));
            yield return Click("productCards",1);Assert.That(Field<int>("wavePanel"),Is.EqualTo(25));yield return Back();Hub("Settings");Unchanged(state,files);
            Call("OnApplicationPause",true);old.Invoke();Call("OnApplicationPause",false);yield return null;
            Assert.That(Field<RectTransform>("productHubRoot").gameObject.activeInHierarchy,Is.False);Assert.That(Text("panelBody"),Does.Contain("Reopen"));Unchanged(state,files);
            yield return Click("productTabs",2);Hub("Yard Events");Assert.That(Session.Arcade.ActiveRun.InstanceSequence,Is.EqualTo(ArcadeEconomy.Project(saved).ActiveRun.InstanceSequence));Unchanged(state,files);
        }
        [UnityTest]public IEnumerator FallbackSectionsHaveOpaqueSeparatedLayoutAndHonestCapacityAndCompletion()
        {
            var fixture=(GameSession)typeof(ReelUiLifecycleTests).GetMethod("CompletedFixture",Hidden).Invoke(ui,null);
            // Bounded legal fixture preparation in memory, not a UI/pacing run.
            for(int i=0;i<24;i++)fixture.ExecuteChapterSpin(fixture.State.sequence+1,1000,true);
            Assert.That(fixture.Arcade.Tickets,Is.EqualTo(3));yield return Harness("Prepare",fixture.State);
            var type=Game.GetType();UnityEngine.Object.Destroy(Game.gameObject);yield return null;
            var root=new GameObject("Fallback product verification");root.SetActive(false);var replacement=root.AddComponent(type);
            typeof(ReelUiLifecycleTests).GetField("game",Hidden).SetValue(ui,replacement);root.SetActive(true);yield return null;
            Assert.That(Field<bool>("sceneUiAuthored"),Is.False);string state=codec.Encode(Session.State);var files=Files();
            int count=Game.GetComponentsInChildren<RectTransform>(true).Length;Call("CreateProductUi");Assert.That(Game.GetComponentsInChildren<RectTransform>(true).Length,Is.EqualTo(count));
            yield return Click("productTabs",2);Hub("Yard Events");Assert.That(Text("productContext"),Does.Contain("Tickets: 3/3").And.Contain("No new Tickets can be added now").And.Not.Contain("until a Ticket:"));
            var background=Field<GameObject>("panel").transform.Find("Product Surface").GetComponent<Graphic>();Assert.That(background.enabled&&background.color.a==1&&background.depth>=0,Is.True);
            Assert.That(Field<TMP_Text>("panelTitle").rectTransform.anchorMin.y,Is.GreaterThanOrEqualTo(Field<RectTransform>("productHubRoot").anchorMax.y));
            yield return Click("productTabs",0);Hub("My District");Assert.That(Field<Button[]>("productCards")[2].GetComponentInChildren<TMP_Text>().text,Does.Contain("District complete · 24/24").And.Not.Contain("Choose"));Unchanged(state,files);
            yield return Click("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(23));yield return Back();Hub("My District");Unchanged(state,files);
        }
        [UnityTest]public IEnumerator HintRejectsHeldContactAcrossPauseAndFreshInputOpensWithoutAcknowledgement()
        {
            // An empty owned folder lets the actual loader initialize a Fresh
            // Journey. The seed is chosen by the game; no spin or OS input is used.
            Assert.That(Directory.GetFileSystemEntries(Folder),Is.Empty,"Fresh guidance requires a genuinely empty private store");
            yield return Harness("LoadGame",false);yield return null;
            Assert.That(Session.Journey.Guidance.Origin,Is.EqualTo(JunkToNeon.Guidance.GuideOrigin.Fresh));
            var guide=Field<Button>("guideButton");var panel=Field<GameObject>("panel");
            var es=EventSystem.current;Assert.That(es,Is.Not.Null);
            var module=es.currentInputModule;Assert.That(module,Is.Not.Null,"The same active input module must remain in place");
            bool focused=Application.isFocused;
            Assert.That(Session.State.workshopBuilt,Is.Zero);Assert.That(Session.State.coins,Is.EqualTo(37));
            Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.False);
            Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.False);
            Assert.That(Field<bool>("offlineSummaryPanel")||Field<bool>("autoHintPanel")||Field<bool>("exitPanel"),Is.False);
            Assert.That(panel.activeSelf,Is.False);Assert.That(Field<string>("shownGuide"),Is.EqualTo(JunkToNeon.Guidance.GuideIds.Welcome));
            Assert.That(Session.Journey.Guidance.IsAcknowledged(JunkToNeon.Guidance.GuideIds.Welcome),Is.False);
            Assert.That(guide.isActiveAndEnabled&&guide.IsInteractable(),Is.True);Target(guide);
            var held=Hold(guide,801);
            Assert.That(held.eligibleForClick&&held.pointerClick==guide.gameObject,Is.True);
            Call("OnApplicationPause",true);Assert.That((bool)Game.GetType().GetProperty("InBackground").GetValue(Game),Is.True);
            Call("OnApplicationPause",false);yield return null;
            Assert.That((bool)Game.GetType().GetProperty("InBackground").GetValue(Game),Is.False);
            Assert.That(EventSystem.current,Is.SameAs(es));Assert.That(es.currentInputModule,Is.SameAs(module));Assert.That(Application.isFocused,Is.EqualTo(focused));
            Assert.That(panel.activeSelf,Is.False);Assert.That(Field<bool>("offlineSummaryPanel"),Is.False);
            Assert.That(guide.isActiveAndEnabled&&guide.IsInteractable(),Is.True);Target(guide);
            Assert.That(held.pointerId,Is.EqualTo(801));Assert.That(held.eligibleForClick&&held.pointerClick==guide.gameObject,Is.True);
            // Pause/checkpoint may write legitimately; this is the post-resume release baseline.
            string resumed=codec.Encode(Session.State);var resumedFiles=Files();
            Release(guide,held);
            Assert.That(panel.activeSelf,Is.False,"A pre-pause held Hint contact must not open guidance after resume");
            Unchanged(resumed,resumedFiles);
            Assert.That(Session.Journey.Guidance.IsAcknowledged(JunkToNeon.Guidance.GuideIds.Welcome),Is.False);

            yield return null;Target(guide);var fresh=Hold(guide,802);Release(guide,fresh);
            Assert.That(panel.activeSelf,Is.True);Assert.That(Text("panelTitle"),Is.EqualTo("Progress hint"));
            Assert.That(Field<string>("shownGuide"),Is.EqualTo(JunkToNeon.Guidance.GuideIds.Welcome));Unchanged(resumed,resumedFiles);
            yield return Click("closeButton");Assert.That(panel.activeSelf,Is.False);Unchanged(resumed,resumedFiles);
            Assert.That(Session.Journey.Guidance.IsAcknowledged(JunkToNeon.Guidance.GuideIds.Welcome),Is.False);

            yield return null;Target(guide);ExecuteEvents.Execute(guide.gameObject,new BaseEventData(es),ExecuteEvents.submitHandler);
            Assert.That(panel.activeSelf,Is.True);Assert.That(Text("panelTitle"),Is.EqualTo("Progress hint"));Unchanged(resumed,resumedFiles);
            yield return Click("closeButton");Assert.That(panel.activeSelf,Is.False);Unchanged(resumed,resumedFiles);
            Assert.That(Session.Journey.Guidance.IsAcknowledged(JunkToNeon.Guidance.GuideIds.Welcome),Is.False);
            TestContext.WriteLine("Simulated uGUI handlers: one retained Hint contact crosses pause/resume without opening or writing after the checkpoint; fresh pointer and Submit open the same Welcome hint and Close dismisses without ACK. The same input module/focus is checked, but no installed InputSystem contact or OS/device touch is claimed.");
        }
        [UnityTearDown]public IEnumerator Restore()
        {
            if(TestContext.CurrentContext.Result.Outcome.Status==NUnit.Framework.Interfaces.TestStatus.Failed){TestContext.WriteLine("Preserved product UI private fixture: "+Folder);typeof(ReelUiLifecycleTests).GetField("directory",Hidden).SetValue(ui,null);}
            yield return ui.RestoreAndRemoveOnlyPrivateFixtures();
        }
    }
}
#endif
