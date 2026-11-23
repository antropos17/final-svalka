#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JunkToNeon.Core;
using JunkToNeon.Contracts;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    public sealed class YardNavigationUiTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui=new ReelUiLifecycleTests();
        readonly JsonStateCodec codec=new JsonStateCodec();int? originalProfile;
        Component Game=>(Component)typeof(ReelUiLifecycleTests).GetField("game",Hidden).GetValue(ui);
        GameSession Session=>(GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        string Folder=>(string)typeof(ReelUiLifecycleTests).GetField("directory",Hidden).GetValue(ui);
        T Field<T>(string n)=>(T)Game.GetType().GetField(n,Hidden).GetValue(Game);
        void Call(string n,params object[] a)=>Game.GetType().GetMethod(n,Hidden|BindingFlags.Public).Invoke(Game,a);
        IEnumerator Harness(string n,params object[] a)=>(IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(n,Hidden).Invoke(ui,a);
        string State=>codec.Encode(Session.State);
        Dictionary<string,string> Files()=>Directory.GetFiles(Folder).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
        void Same(string state,Dictionary<string,string> files){Assert.That(State,Is.EqualTo(state));Assert.That(Files(),Is.EquivalentTo(files));}
        ModelState Fixture(bool loft=true,int spins=24)
        {
            var s=(ModelState)typeof(CombinedLifecycleTests).GetMethod("MilestoneEraFixture",Hidden).Invoke(new CombinedLifecycleTests(),new object[]{EraIds.Junkyard,new[]{1,1,1,loft?1:0,1,1}});
            for(int i=0;i<spins;i++)s=GameLogic.ApplyPending(GameLogic.Reserve(s,"spin",0,s.sequence+1,1000));return s;
        }
        PointerEventData Down(Button b)
        {
            Assert.That(b.isActiveAndEnabled&&b.IsInteractable(),Is.True,b.name);Canvas.ForceUpdateCanvases();var rect=(RectTransform)b.transform;
            var p=new PointerEventData(EventSystem.current){pointerId=921,button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerClick=b.gameObject,pointerPress=b.gameObject,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Assert.That(hits.Count,Is.GreaterThan(0),b.name);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(b.gameObject),b.name+" topmost");
            p.pointerCurrentRaycast=hits[0];ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler);return p;
        }
        void Up(Button b,PointerEventData p){ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);}
        IEnumerator Click(string n,int i=-1){yield return null;var b=i<0?Field<Button>(n):Field<Button[]>(n)[i];Up(b,Down(b));yield return null;}
        static Rect Bounds(Button b){var c=new Vector3[4];((RectTransform)b.transform).GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
        void Fit(params Button[] buttons)
        {
            Canvas.ForceUpdateCanvases();var rects=new List<Rect>();
            foreach(var b in buttons.Where(b=>b!=null&&b.gameObject.activeInHierarchy))
            {
                var r=Bounds(b);Assert.That(r.width,Is.GreaterThanOrEqualTo(48),b.name);Assert.That(r.height,Is.GreaterThanOrEqualTo(48),b.name);
                foreach(var other in rects)Assert.That(r.Overlaps(other),Is.False,b.name+" overlap");rects.Add(r);
                foreach(var t in b.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Assert.That(t.isTextOverflowing,Is.False,t.text);}
            }
            if(Field<GameObject>("panel").activeSelf){var body=Field<TMP_Text>("panelBody");body.ForceMeshUpdate();Assert.That(body.isTextOverflowing,Is.False,body.text);}
        }
        void Profile(int index)
        {
            var flags=Hidden|BindingFlags.Public;var window=Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().Single(w=>w.GetType().Name=="SimulatorWindow");
            var main=window.GetType().GetField("m_Main",flags).GetValue(window);var property=main.GetType().GetProperty("deviceIndex",flags);
            if(!originalProfile.HasValue)originalProfile=(int)property.GetValue(main);property.SetValue(main,index);
        }
        IEnumerator Picture(string name)
        {
            string path=Environment.GetEnvironmentVariable("JTN_YARD_NAV_CAPTURE_DIRECTORY");if(path==null)yield break;
            Directory.CreateDirectory(path);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(path,name+".png"));yield return null;
        }
        [SetUp]public void Isolate(){ui.IsolateSave();Assert.That(Path.GetPathRoot(Folder).Replace('\\','/'),Is.EqualTo("X:/").IgnoreCase);}
        IEnumerator Portrait(int height)
        {
            Profile(height==2340?2:3);yield return Harness("Prepare",Fixture());yield return null;
            Assert.That(Screen.width,Is.EqualTo(1080));Assert.That(Screen.height,Is.EqualTo(height));int frame=Time.frameCount;
            string state=State;var files=Files();Fit(Field<Button[]>("yardButtons"));yield return Picture(height+"-yard");
            string[] titles={"House","Workshop","Recycler","Pigeon Loft","Cart","Power Source"};
            for(int i=0;i<6;i++)
            {
                yield return Click("yardButtons",i);Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo(titles[i]));
                Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("stage 1 → 2"));
                Fit(Field<Button>("selectButton"),Field<Button>("closeButton"),Field<Button>("yardActivityButton"));
                if(i==2||i==3)yield return Picture(height+"-"+(i==2?"recycler":"loft"));
                yield return Click("closeButton");Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Same(state,files);
            }
            // All six unbuilt sites remain inspectable, with no purchase or selection.
            File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(ModelState.Fresh(1)));yield return Harness("ReenterScene");state=State;files=Files();
            for(int i=0;i<6;i++){yield return Click("yardButtons",i);yield return Click("closeButton");Same(state,files);}
            Assert.That(Time.frameCount,Is.GreaterThan(frame));
        }
        [UnityTest]public IEnumerator RegularPortraitSixBuildingsInspectWithoutWrites(){yield return Portrait(2340);}
        [UnityTest]public IEnumerator ShortPortraitSixBuildingsInspectWithoutWrites(){yield return Portrait(2280);}
        [UnityTest]public IEnumerator ActivityRoutesResumeReloadAndStaleContact()
        {
            yield return Harness("Prepare",Fixture(false,0));string state=State;var files=Files();
            yield return Click("yardButtons",3);Assert.That(Field<Button>("yardActivityButton").GetComponentInChildren<TMP_Text>().text,Does.Contain("Requires Loft I"));
            yield return Click("yardActivityButton");Assert.That(Field<Button>("arcadePrimaryButton").interactable,Is.False);Same(state,files);
            yield return Click("closeButton");Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Pigeon Loft"));yield return Click("closeButton");Same(state,files);
            var stocked=(ModelState)typeof(BatchExchangeUiTests).GetMethod("Stock",Hidden).Invoke(new BatchExchangeUiTests(),new object[]{95,83,Fixture()});
            File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(stocked));yield return Harness("ReenterScene");
            yield return Click("yardButtons",2);var old=Field<Button>("yardActivityButton").onClick;var held=Down(Field<Button>("yardActivityButton"));
            yield return Click("closeButton");yield return Click("yardButtons",3);state=State;files=Files();old.Invoke();
            Up(Field<Button>("yardActivityButton"),held);Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Pigeon Loft"));Same(state,files);
            yield return Click("yardActivityButton");int tickets=Session.Arcade.Tickets;Same(state,files);
            yield return Click("arcadePrimaryButton");Assert.That(Session.Arcade.Tickets,Is.EqualTo(tickets-1));
            yield return Click("parcelsTokenButtons",0);yield return Click("parcelsSlotButtons",0);long instance=Session.Parcels.ActiveRun.InstanceSequence;
            yield return Click("closeButton");Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Pigeon Loft"));yield return Click("closeButton");
            yield return Harness("ReenterScene");state=State;files=Files();yield return Click("yardButtons",3);yield return Click("yardActivityButton");
            Assert.That(Session.Parcels.ActiveRun.InstanceSequence,Is.EqualTo(instance));Assert.That(Session.Parcels.ActiveRun.Slots,Is.EqualTo(new[]{0,-1,-1,-1}));Same(state,files);
            yield return Click("closeButton");yield return Click("closeButton");yield return Click("yardButtons",2);yield return Click("yardActivityButton");
            Assert.That(Field<TMP_Text>("expansionTitle").text,Does.Contain("exchange"));Same(state,files);
            int index=Array.IndexOf(Field<string[]>("expansionCardIds"),"trade.parts");Assert.That(index,Is.GreaterThanOrEqualTo(0));
            yield return Click("expansionCards",index);Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Batches: 1"));Same(state,files);
            yield return Click("expansionBatchMaxButton");Same(state,files);yield return Click("closeButton");yield return Click("closeButton");
            Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Recycler"));yield return Click("closeButton");Same(state,files);
        }
        [UnityTest]public IEnumerator LoftParcelsConfirmationCancelReturnsToSameEditableRound()
        {
            yield return Harness("Prepare",Fixture(true,8));
            yield return Click("yardButtons",3);yield return Click("yardActivityButton");int tickets=Session.Arcade.Tickets;
            yield return Click("arcadePrimaryButton");Assert.That(Session.Arcade.Tickets,Is.EqualTo(tickets-1));
            for(int slot=0;slot<4;slot++){yield return Click("parcelsTokenButtons",slot);yield return Click("parcelsSlotButtons",slot);}
            Assert.That(Session.Parcels.ActiveRun.SatisfiedRequests,Is.EqualTo(1));
            long instance=Session.Parcels.ActiveRun.InstanceSequence;string state=State;var files=Files();
            yield return Click("arcadePrimaryButton");Assert.That(Field<int>("wavePanel"),Is.EqualTo(62));
            Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Finish this layout?"));
            Assert.That(Field<Button>("closeButton").GetComponentInChildren<TMP_Text>().text,Is.EqualTo("Cancel · to round"));Same(state,files);
            yield return Click("closeButton");Assert.That(Field<int>("wavePanel"),Is.EqualTo(63));
            Assert.That(Session.Parcels.ActiveRun.InstanceSequence,Is.EqualTo(instance));Assert.That(Session.Parcels.ActiveRun.Ready,Is.False);
            Assert.That(Session.Parcels.ActiveRun.Slots,Is.EqualTo(new[]{0,1,2,3}));Same(state,files);
            yield return Click("arcadeSecondaryButton");Assert.That(Field<int>("wavePanel"),Is.EqualTo(62));
            Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Abandon Parcels?"));
            Assert.That(Field<Button>("closeButton").GetComponentInChildren<TMP_Text>().text,Is.EqualTo("Cancel · to round"));Same(state,files);
            var back=Game.GetComponents<Component>().Single(c=>c.GetType().FullName=="JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)back.GetType().GetMethod("RequestBack").Invoke(back,null),Is.True);yield return null;
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(63));Assert.That(Session.Parcels.ActiveRun.InstanceSequence,Is.EqualTo(instance));
            Assert.That(Session.Parcels.ActiveRun.Ready,Is.False);Assert.That(Session.Parcels.ActiveRun.Slots,Is.EqualTo(new[]{0,1,2,3}));Same(state,files);
            yield return Click("closeButton");Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Pigeon Loft"));Same(state,files);
        }
        [UnityTest]public IEnumerator ZeroTicketResumeAndReadyInspectDoNotClaim()
        {
            var complete=(ModelState)typeof(CombinedLifecycleTests).GetMethod("MilestoneEraFixture",Hidden).Invoke(new CombinedLifecycleTests(),new object[]{EraIds.WorkshopYard,new[]{5,5,5,5,5,5}});
            for(int spin=0;spin<8;spin++)complete=GameLogic.ApplyPending(GameLogic.Reserve(complete,"spin",0,complete.sequence+1,1000));
            yield return Harness("Prepare",complete);yield return Click("yardButtons",3);Assert.That(Field<Button>("selectButton").gameObject.activeSelf,Is.False);
            yield return Click("yardActivityButton");yield return Click("arcadePrimaryButton");
            Assert.That(Session.Arcade.Tickets,Is.Zero);yield return Click("closeButton");yield return Click("closeButton");yield return Harness("ReenterScene");
            string state=State;var files=Files();yield return Click("yardButtons",3);Assert.That(Field<Button>("yardActivityButton").GetComponentInChildren<TMP_Text>().text,Does.Contain("Resume free"));
            yield return Click("yardActivityButton");Same(state,files);Assert.That(Session.Arcade.Tickets,Is.Zero);
            int[] solution={0,3,1,2};for(int slot=0;slot<4;slot++){yield return Click("parcelsTokenButtons",solution[slot]);yield return Click("parcelsSlotButtons",slot);}
            yield return Click("arcadePrimaryButton");Assert.That(Session.Parcels.ActiveRun.Ready,Is.True);Assert.That(Session.Parcels.ActiveRun.ReadyReward,Is.EqualTo(3));Assert.That(Session.Arcade.Diamonds,Is.Zero);
            yield return Click("closeButton");yield return Click("closeButton");yield return Harness("ReenterScene");state=State;files=Files();
            yield return Click("yardButtons",3);Assert.That(Field<Button>("yardActivityButton").GetComponentInChildren<TMP_Text>().text,Does.Contain("Saved result"));
            yield return Click("yardActivityButton");Same(state,files);Assert.That(Session.Arcade.Diamonds,Is.Zero);
            yield return Click("arcadePrimaryButton");Assert.That(Session.Arcade.Diamonds,Is.EqualTo(3));Assert.That(Session.Parcels.ActiveRun,Is.Null);
            yield return Click("closeButton");yield return Click("closeButton");state=State;files=Files();yield return Click("yardButtons",3);yield return Click("yardActivityButton");Same(state,files);
            Assert.That(Field<Button>("arcadePrimaryButton").interactable,Is.False);Assert.That(Session.Arcade.Diamonds,Is.EqualTo(3));
        }
        [UnityTearDown]public IEnumerator Restore(){yield return ui.RestoreAndRemoveOnlyPrivateFixtures();if(originalProfile.HasValue){Profile(originalProfile.Value);originalProfile=null;}}
    }
}
#endif
