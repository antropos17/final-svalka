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
    public sealed class BatchExchangeUiTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui=new ReelUiLifecycleTests();
        readonly JsonStateCodec codec=new JsonStateCodec();
        Component Game=>(Component)typeof(ReelUiLifecycleTests).GetField("game",Hidden).GetValue(ui);
        GameSession Session=>(GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        string Folder=>(string)typeof(ReelUiLifecycleTests).GetField("directory",Hidden).GetValue(ui);
        T Field<T>(string n)=>(T)Game.GetType().GetField(n,Hidden).GetValue(Game);
        IEnumerator Harness(string n,params object[] a)=>(IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(n,Hidden).Invoke(ui,a);
        void Call(string n,params object[] a)=>Game.GetType().GetMethod(n,Hidden|BindingFlags.Public).Invoke(Game,a);
        IEnumerator Click(string n,int i=-1){yield return Harness("PointerClick",n,i);yield return null;}
        string Body=>Field<TMP_Text>("expansionBody").text;
        Dictionary<string,string> Files()=>Directory.GetFiles(Folder).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
        void Same(string state,Dictionary<string,string> files){Assert.That(codec.Encode(Session.State),Is.EqualTo(state));Assert.That(Files(),Is.EquivalentTo(files));}
        int? originalProfile;
        ModelState Stock(int parts=95,int potatoes=83,ModelState source=null)
        {
            var s=source?.Copy()??GameLogic.ApplyPending(GameLogic.Reserve(ModelState.Fresh(1),"spin",0,1));
            if(s.expansion==null)s=GameLogic.ApplyPending(ExpansionEconomy.Reserve(s,ExpansionEconomy.Quote(s,"speed","speed.fast")));
            var type=typeof(ExpansionState).Assembly.GetType("JunkToNeon.Core.ExpansionDraft",true);
            var draft=Activator.CreateInstance(type,Hidden,null,new object[]{s.expansion},null);
            type.GetField("Parts",Hidden).SetValue(draft,parts);type.GetField("Potatoes",Hidden).SetValue(draft,potatoes);
            s.expansion=(ExpansionState)type.GetMethod("Freeze",Hidden).Invoke(draft,null);s.coins=7;GameLogic.Validate(s);return s;
        }
        IEnumerator OpenParts(){yield return OpenTrade("trade.parts");}
        IEnumerator OpenTrade(string id)
        {
            yield return Click("productTabs",3);yield return Click("productCards",4);
            // The public category list owns the resource-exchange offer cards.
            int index=Array.IndexOf(Field<string[]>("expansionCardIds"),id);
            Assert.That(index,Is.GreaterThanOrEqualTo(0),id+" exchange visible in Shop category");yield return Click("expansionCards",index);
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(52));
        }
        PointerEventData Hold(Button b,int id)
        {
            var p=new PointerEventData(EventSystem.current){pointerId=id,button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerClick=b.gameObject,pointerCurrentRaycast=new RaycastResult{gameObject=b.gameObject}};
            ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler);return p;
        }
        void Release(Button b,PointerEventData p){ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);}
        void Layout()
        {
            Canvas.ForceUpdateCanvases();
            foreach(string name in new[]{"expansionBatchMinusButton","expansionBatchPlusButton","expansionBatchMaxButton","expansionPrimaryButton","closeButton"})
            {
                var b=Field<Button>(name);if(!b.gameObject.activeInHierarchy)continue;var rect=(RectTransform)b.transform;
                var corners=new Vector3[4];rect.GetWorldCorners(corners);Assert.That(corners[2].y-corners[0].y,Is.GreaterThanOrEqualTo(48),name);Assert.That(corners[2].x-corners[0].x,Is.GreaterThanOrEqualTo(48),name);
                foreach(var t in b.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Assert.That(t.isTextOverflowing,Is.False,name+": "+t.text);}
                if(b.IsInteractable())
                {
                    var p=new PointerEventData(EventSystem.current){position=(corners[0]+corners[2])*.5f};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
                    Assert.That(hits.Count,Is.GreaterThan(0),name);Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(b.gameObject),name);
                }
            }
            var body=Field<TMP_Text>("expansionBody");float size=body.fontSize;bool auto=body.enableAutoSizing;body.enableAutoSizing=false;body.fontSize=14;
            Vector2 preferred=body.GetPreferredValues(body.text,body.rectTransform.rect.width,float.PositiveInfinity);
            body.fontSize=size;body.enableAutoSizing=auto;
            Assert.That(preferred.y,Is.LessThanOrEqualTo(body.rectTransform.rect.height+1),"Whole body must fit at unchanged14pt: "+Body);
            Assert.That(preferred.x,Is.LessThanOrEqualTo(body.rectTransform.rect.width+1),"Whole body width14pt: "+Body);
            body.ForceMeshUpdate();Assert.That(body.isTextOverflowing,Is.False,Body);
            Assert.That(Field<Button>("expansionPreviousButton").gameObject.activeInHierarchy,Is.False);
            Assert.That(Field<Button>("expansionNextButton").gameObject.activeInHierarchy,Is.False);
        }
        [SetUp]public void Isolate(){ui.IsolateSave();Assert.That(Path.GetPathRoot(Folder).Replace('\\','/'),Is.EqualTo("X:/").IgnoreCase);}
        [UnityTest]public IEnumerator BatchPreviewBackExactThreeAndFreshSingleRepeat()
        {
            yield return Harness("Prepare",Stock());yield return OpenParts();
            string state=codec.Encode(Session.State);var files=Files();Assert.That(Field<int>("expansionBatchCount"),Is.EqualTo(1));
            yield return Click("expansionBatchPlusButton");yield return Click("expansionBatchPlusButton");
            Assert.That(Field<int>("expansionBatchCount"),Is.EqualTo(3));Assert.That(Body,Does.Contain("60 parts").And.Contain("35").And.Contain("67"));Layout();Same(state,files);
            yield return Click("closeButton");Same(state,files);yield return Click("closeButton");yield return OpenParts();Assert.That(Field<int>("expansionBatchCount"),Is.EqualTo(1));
            yield return Click("expansionBatchPlusButton");yield return Click("expansionBatchPlusButton");var old=Field<Button>("expansionPrimaryButton").onClick;
            ExecuteEvents.Execute(Field<Button>("expansionPrimaryButton").gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);yield return null;
            Assert.That(Session.Expansion.Parts,Is.EqualTo(35));Assert.That(Session.Expansion.Potatoes,Is.EqualTo(83));Assert.That(Session.State.coins,Is.EqualTo(67));
            Assert.That(Body,Does.Contain("3 batches").And.Contain("60 parts").And.Contain("60 coins"));
            state=codec.Encode(Session.State);files=Files();old.Invoke();Same(state,files);
            Assert.That(Field<int>("expansionBatchCount"),Is.EqualTo(1));Assert.That(Field<ExpansionQuote>("expansionQuote").PartsCost,Is.EqualTo(20));
            yield return Click("expansionSecondaryButton");Assert.That(Session.Expansion.Parts,Is.EqualTo(15));Assert.That(Session.State.coins,Is.EqualTo(87));
        }
        [UnityTest]public IEnumerator BatchMaxOldHeldQuantityEventsAndPauseRequireFreshConsent()
        {
            yield return Harness("Prepare",Stock());yield return OpenParts();var confirm=Field<Button>("expansionPrimaryButton");var held=Hold(confirm,940);var old=confirm.onClick;
            yield return Click("expansionBatchMaxButton");Assert.That(Field<int>("expansionBatchCount"),Is.EqualTo(4));
            string state=codec.Encode(Session.State);var files=Files();Release(confirm,held);old.Invoke();Same(state,files);
            var plus=Field<Button>("expansionBatchMinusButton").onClick;yield return Click("expansionBatchMinusButton");plus.Invoke();Assert.That(Field<int>("expansionBatchCount"),Is.EqualTo(3));Same(state,files);
            held=Hold(confirm,941);Call("OnApplicationPause",true);Call("OnApplicationPause",false);yield return null;
            state=codec.Encode(Session.State);files=Files();Release(confirm,held);old.Invoke();Same(state,files);
            yield return Click("closeButton");yield return OpenParts();yield return Click("expansionBatchMaxButton");Layout();yield return Click("expansionPrimaryButton");
            Assert.That(Session.Expansion.Parts,Is.EqualTo(15));Assert.That(Session.State.coins,Is.EqualTo(87));
        }
        [UnityTest]public IEnumerator BatchInsufficientAndStaleSessionCannotSpendReopenedQuote()
        {
            yield return Harness("Prepare",Stock(19));yield return OpenParts();string state=codec.Encode(Session.State);var files=Files();
            Assert.That(Field<Button>("expansionPrimaryButton").gameObject.activeInHierarchy,Is.False);Assert.That(Body,Does.Contain("Unavailable"));Same(state,files);
            yield return Harness("ReenterScene");
            // Replace only the private loaded session using the fixture's normal reload path.
            File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(Stock()));yield return Harness("ReenterScene");yield return OpenParts();yield return Click("expansionBatchMaxButton");
            var old=Field<Button>("expansionPrimaryButton").onClick;var held=Hold(Field<Button>("expansionPrimaryButton"),942);
            var replacement=GameSession.Open(new GameStore(new FileSnapshotStorage(Folder),codec),1);
            Assert.That(replacement.RecoveryRequired,Is.False);
            Game.GetType().GetProperty("Session").GetSetMethod(true).Invoke(Game,new object[]{replacement});
            state=codec.Encode(Session.State);files=Files();old.Invoke();Release(Field<Button>("expansionPrimaryButton"),held);Same(state,files);
            yield return Harness("ReenterScene");yield return OpenParts();
            Assert.That(Field<int>("expansionBatchCount"),Is.EqualTo(1));
            var retained=Field<Button>("expansionPrimaryButton").onClick;Session.SelectExpansionSpeed("speed.fast",Session.State.sequence,1000);state=codec.Encode(Session.State);files=Files();retained.Invoke();Same(state,files);
            Assert.That(Field<TMP_Text>("expansionTitle").text,Is.EqualTo("Offer changed"));Assert.That(Body,Does.Contain("Reopen"));Assert.That(Field<ExpansionQuote>("expansionQuote"),Is.Null);
        }
        void Profile(int index)
        {
            var flags=Hidden|BindingFlags.Public;var window=Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().Single(w=>w.GetType().Name=="SimulatorWindow");
            var main=window.GetType().GetField("m_Main",flags).GetValue(window);var property=main.GetType().GetProperty("deviceIndex",flags);
            if(!originalProfile.HasValue)originalProfile=(int)property.GetValue(main);property.SetValue(main,index);
        }
        [Serializable]sealed class CaptureView {public int width,height,frame;public string state,body;public Rect[] targets;}
        IEnumerator Picture(string name)
        {
            string folder=Environment.GetEnvironmentVariable("JTN_BATCH_CAPTURE_DIRECTORY");if(string.IsNullOrEmpty(folder))yield break;
            var allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/batch-exchange-20261103/tests/captures"));folder=Path.GetFullPath(folder);
            Assert.That(folder.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(folder);
            Assert.That(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length,Is.LessThan(8));string path=Path.Combine(folder,name+".png");Assert.That(File.Exists(path),Is.False);
            var buttons=new[]{"expansionBatchMinusButton","expansionBatchPlusButton","expansionBatchMaxButton","expansionPrimaryButton","expansionSecondaryButton","closeButton"}.Select(Field<Button>).Where(b=>b!=null&&b.gameObject.activeInHierarchy).ToArray();
            Rect RectOf(Button b){var c=new Vector3[4];((RectTransform)b.transform).GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
            var view=new CaptureView{width=Screen.width,height=Screen.height,frame=Time.frameCount,state=codec.Encode(Session.State),body=Body,targets=buttons.Select(RectOf).ToArray()};
            var files=Files();File.WriteAllText(Path.ChangeExtension(path,".json"),JsonUtility.ToJson(view,true));ScreenCapture.CaptureScreenshot(path);
            for(int f=0;f<30&&!File.Exists(path);f++)yield return null;Assert.That(File.Exists(path),Is.True);Same(view.state,files);
        }
        IEnumerator Portrait(int height)
        {
            Profile(height==2340?2:3);yield return null;
            foreach(int price in new[]{540,600})
            {
                ModelState s;if(price==540)s=codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/Milestones/fixtures/v2-selected540.json"))));
                else{s=ModelState.Fresh(1);s.houseStage=1;s.selectedStage=2;s.autoHintSeen=1;}
                s=Stock(95,83,s);
                if(Game==null)yield return Harness("Prepare",s);else{File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(s));yield return Harness("ReenterScene");}
                float until=Time.realtimeSinceStartup+2;while(Screen.height!=height&&Time.realtimeSinceStartup<until)yield return null;Assert.That(Screen.width,Is.EqualTo(1080));Assert.That(Screen.height,Is.EqualTo(height));
                string resource=price==540?"trade.parts":"trade.potatoes";
                yield return OpenTrade(resource);yield return Click("expansionBatchMaxButton");Layout();Assert.That(Body,Does.Contain("saved "+price));
                Assert.That(Field<int>("expansionBatchCount"),Is.EqualTo(4));Assert.That(Body,Does.Contain(price==540?"80 parts":"80 potatoes"));
                yield return Picture(height+"-saved"+price+"-max-offer");yield return Click("expansionPrimaryButton");Layout();
                Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(price));Assert.That(Session.State.coins,Is.EqualTo(87));
                Assert.That(Session.Expansion.Parts,Is.EqualTo(price==540?15:95));Assert.That(Session.Expansion.Potatoes,Is.EqualTo(price==540?83:3));
                yield return Picture(height+"-saved"+price+"-max-result");
            }
        }
        [UnityTest]public IEnumerator BatchOfferAndResultFitRegularSaved540And600(){yield return Portrait(2340);}
        [UnityTest]public IEnumerator BatchOfferAndResultFitShortSaved540And600(){yield return Portrait(2280);}
        [UnityTest]public IEnumerator BatchRetainsAvailableFilterThroughCommitRepeatAndReinspection()
        {
            yield return Harness("Prepare",Stock());yield return Click("productTabs",3);yield return Click("productCards",4);
            Assert.That(Field<int>("expansionSecondaryAction"),Is.EqualTo(9));yield return Click("expansionSecondaryButton");
            Assert.That(Field<int>("expansionFilteredCategory"),Is.EqualTo(4));
            int index=Array.IndexOf(Field<string[]>("expansionCardIds"),"trade.parts");Assert.That(index,Is.GreaterThanOrEqualTo(0));yield return Click("expansionCards",index);
            yield return Click("expansionBatchPlusButton");Assert.That(Field<int>("expansionBatchCount"),Is.EqualTo(2));yield return Click("expansionPrimaryButton");
            Assert.That(Session.Expansion.Parts,Is.EqualTo(55));Assert.That(Session.State.coins,Is.EqualTo(47));Assert.That(Session.Expansion.Potatoes,Is.EqualTo(83));
            Assert.That(Field<int>("expansionFilteredCategory"),Is.EqualTo(4),"Batch result must retain the explicit Available category context");
            Assert.That(Field<ExpansionQuote>("expansionQuote").PartsCost,Is.EqualTo(20));yield return Click("expansionSecondaryButton");
            Assert.That(Session.Expansion.Parts,Is.EqualTo(35));Assert.That(Session.State.coins,Is.EqualTo(67));Assert.That(Field<int>("expansionFilteredCategory"),Is.EqualTo(4));
            string state=codec.Encode(Session.State);var files=Files();yield return Click("closeButton");
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(51));Assert.That(Field<int>("expansionFilteredCategory"),Is.EqualTo(4));Assert.That(Field<TMP_Text>("expansionTitle").text,Does.Contain("available to buy"));Same(state,files);
            index=Array.IndexOf(Field<string[]>("expansionCardIds"),"trade.parts");Assert.That(index,Is.GreaterThanOrEqualTo(0));yield return Click("expansionCards",index);
            Assert.That(Field<int>("expansionBatchCount"),Is.EqualTo(1));Same(state,files);
        }
        [UnityTearDown]public IEnumerator Restore(){yield return ui.RestoreAndRemoveOnlyPrivateFixtures();if(originalProfile.HasValue){Profile(originalProfile.Value);originalProfile=null;}}
    }
}
#endif
