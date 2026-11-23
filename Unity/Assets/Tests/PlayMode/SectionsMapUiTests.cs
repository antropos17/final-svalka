#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace JunkToNeon.Tests.PlayMode
{
    // Short private Editor smoke checks. Existing qualified additive fixture and
    // raycast uGUI dispatch are reused; no physical input, chapter grind or APK claim.
    public sealed class SectionsMapUiTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui=new ReelUiLifecycleTests();
        readonly JsonStateCodec codec=new JsonStateCodec();
        object FixtureField(string name)=>typeof(ReelUiLifecycleTests).GetField(name,Hidden).GetValue(ui);
        Component Game=>(Component)FixtureField("game");
        GameSession Session=>(GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        T Field<T>(string name)=>(T)Game.GetType().GetField(name,Hidden).GetValue(Game);
        string DirectoryPath=>(string)FixtureField("directory");
        IEnumerator Harness(string name,params object[] args)=>(IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(name,Hidden).Invoke(ui,args);
        void Runtime(string name,params object[] args)=>Game.GetType().GetMethod(name,Hidden|BindingFlags.Public).Invoke(Game,args);
        Dictionary<string,string> Files()=>Directory.GetFiles(DirectoryPath).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
        void Unchanged(string state,Dictionary<string,string> files)
        {Assert.That(codec.Encode(Session.State),Is.EqualTo(state));Assert.That(Files(),Is.EquivalentTo(files));Assert.That(Session.RecoveryRequired,Is.False,Session.Error);}
        string Text(string field)=>Field<TMP_Text>(field).text;
        string Node(int i)=>Field<UnityEngine.UI.Button[]>("sectionMapNodes")[i].GetComponentInChildren<TMP_Text>().text;
        IEnumerator Click(string name,int index=-1)
        {
            var button=index<0?Field<UnityEngine.UI.Button>(name):Field<UnityEngine.UI.Button[]>(name)[index];
            Assert.That(button.isActiveAndEnabled&&button.IsInteractable(),Is.True,name+" is not an actionable visible control; panel="+Text("panelTitle")+"; mode="+Field<int>("wavePanel")+"; active="+button.gameObject.activeInHierarchy+"; interactable="+button.interactable);
            yield return Harness("PointerClick",name,index);
        }
        IEnumerator Back()
        {
            yield return null;
            var adapter=Game.GetComponents<Component>().Single(c=>c.GetType().FullName=="JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)adapter.GetType().GetMethod("RequestBack").Invoke(adapter,null),Is.True);
        }
        IEnumerator ReturnToGame()
        {
            // Dismiss only the known read-only six-card lists/Hub. A purchase,
            // transition, offline acknowledgement or other modal is an error.
            for(int i=0;i<3&&Field<GameObject>("panel").activeSelf;i++)
            {
                Assert.That(new[]{20,30,31,32,70,75},Does.Contain(Field<int>("wavePanel")),"Unexpected covering panel");
                yield return Click("closeButton");
            }
            Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
        }
        IEnumerator EnterSections()
        {
            yield return Click("productAvatar");Assert.That(Field<int>("wavePanel"),Is.EqualTo(75));
            yield return Click("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(20));
        }
        IEnumerator OpenMap(){yield return EnterSections();yield return Click("sectionButtons",1);yield return null;CheckMapLayout();}
        string CloseLabel(){var label=Field<UnityEngine.UI.Button>("closeButton").GetComponentInChildren<TMP_Text>();Fits(label);return label.text;}
        IEnumerator Progress(params string[] expected)
        {
            string before=codec.Encode(Session.State);var files=Files();
            var next=Field<UnityEngine.UI.Button>("chapterNext");var nextRect=(RectTransform)next.transform;
            string oldCaption=next.GetComponentInChildren<TMP_Text>(true).text;var oldMin=nextRect.anchorMin;var oldMax=nextRect.anchorMax;
            yield return EnterSections();
            for(int pass=0;pass<2;pass++)
            {
                CheckHub();yield return Click("sectionButtons",5);Assert.That(Text("panelTitle"),Is.EqualTo("Progress"));
                foreach(string value in expected)Assert.That(Text("panelBody"),Does.Contain(value));
                Assert.That(Text("panelBody"),Does.Not.Match(@"(?<!\d)0/2(?!\d)"));Fits(Field<TMP_Text>("panelBody"));
                if(!expected.Any(value=>value.StartsWith("In this save:",StringComparison.Ordinal)))Assert.That(Text("panelBody"),Does.Not.Contain("In this save:"));
                else Assert.That(Text("panelBody"),Does.Not.Contain("order reward"));
                Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);Assert.That(Field<string>("shownGuide"),Is.Null);
                Assert.That(CloseLabel(),Is.EqualTo("Back"));
                string selected=Session.Chapter.SelectedProjectId;
                // Literal expected scenarios determine this oracle, not the production NextAction helper.
                bool choose=selected==null&&expected.Any(value=>value.Contains("choose one building"));
                bool house=selected==null&&expected.Any(value=>value.Contains("inspect the house"));
                bool entry=selected==null&&expected.Any(value=>value.Contains("enter Workshop Yard for free"));
                Assert.That(next.gameObject.activeSelf,Is.EqualTo(selected!=null||choose||house||entry));
                Assert.That(Field<UnityEngine.UI.Button>("chapterPrevious").gameObject.activeSelf,Is.EqualTo(selected!=null));
                if(choose)
                {
                    Runtime("Render",null,null);Assert.That(next.isActiveAndEnabled&&next.IsInteractable(),Is.True);Unchanged(before,files);
                    Assert.That(next.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo("Choose goal"));Fits(next.GetComponentInChildren<TMP_Text>(true));
                    Assert.That(nextRect.anchorMin,Is.EqualTo(oldMin));Assert.That(nextRect.anchorMax,Is.EqualTo(oldMax));
                    var panelRect=(RectTransform)Field<GameObject>("panel").transform;Assert.That(RectTransformUtility.CalculateRelativeRectTransformBounds(panelRect,next.transform).Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(panelRect,Field<TMP_Text>("panelBody").transform)),Is.False);
                    yield return Click("chapterNext");Assert.That(CloseLabel(),Is.EqualTo("Back"));Assert.That(Text("panelTitle"),Is.EqualTo("Buildings · all six"));
                    Assert.That(Field<int>("wavePanel"),Is.EqualTo(30));Assert.That(Field<UnityEngine.UI.Button[]>("navigationCards").Count(b=>b.gameObject.activeInHierarchy),Is.EqualTo(6));
                    Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);Unchanged(before,files);
                    if(pass==0)yield return Back();else yield return Click("closeButton");
                    CheckHub();next.onClick.Invoke();Assert.That(Text("panelTitle"),Is.EqualTo("All Sections"));Unchanged(before,files);
                    yield return Click("sectionButtons",5);Assert.That(CloseLabel(),Is.EqualTo("Back"));
                    foreach(string value in expected)Assert.That(Text("panelBody"),Does.Contain(value));Fits(Field<TMP_Text>("panelBody"));
                }
                else if(selected==null&&!house&&!entry){next.onClick.Invoke();Assert.That(Text("panelTitle"),Is.EqualTo("Progress"));}
                else
                {
                    string caption=house?"Inspect house":entry?"Inspect era entry":"Inspect goal";
                    if((house||entry)&&pass==0)
                    {
                        Runtime("OnApplicationPause",true);next.onClick.Invoke();Unchanged(before,files);Runtime("OnApplicationPause",false);next.onClick.Invoke();Unchanged(before,files);
                        Assert.That(next.gameObject.activeSelf,Is.False);yield return Click("closeButton");Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
                        yield return EnterSections();yield return Click("sectionButtons",5);
                    }
                    Runtime("Render",null,null);Assert.That(next.isActiveAndEnabled&&next.IsInteractable(),Is.True);Unchanged(before,files);
                    Assert.That(next.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo(caption));Fits(next.GetComponentInChildren<TMP_Text>(true));
                    var panelRect=(RectTransform)Field<GameObject>("panel").transform;
                    Assert.That(RectTransformUtility.CalculateRelativeRectTransformBounds(panelRect,next.transform).Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(panelRect,Field<TMP_Text>("panelBody").transform)),Is.False,"Existing goal action slot must not overlap Progress body");
                    Assert.That(nextRect.anchorMin,Is.EqualTo(oldMin));Assert.That(nextRect.anchorMax,Is.EqualTo(oldMax));
                    yield return Click("chapterNext");Assert.That(CloseLabel(),Is.EqualTo("Back"));
                    if(house){Assert.That(Field<string>("chapterProject"),Is.EqualTo("house.repair"));Assert.That(Text("panelBody"),Does.Contain("Price: 40 Coins."));Assert.That(Session.State.coins,Is.EqualTo(37));Assert.That(Session.State.seed,Is.EqualTo(1));}
                    else if(entry){Assert.That(Text("panelBody"),Does.Contain("Enter for free?"));Assert.That(Session.State.coins,Is.EqualTo(100));Assert.That(Session.Chapter.Progress.EraId,Is.EqualTo(EraIds.Junkyard));}
                    else Assert.That(Field<string>("chapterProject"),Is.EqualTo(selected));
                    Fits(Field<TMP_Text>("panelBody"));Unchanged(before,files);
                    if(pass==0)yield return Back();else yield return Click("closeButton");
                    Assert.That(Text("panelTitle"),Is.EqualTo("Progress"));foreach(string value in expected)Assert.That(Text("panelBody"),Does.Contain(value));
                    Assert.That(CloseLabel(),Is.EqualTo("Back"));Fits(Field<TMP_Text>("panelBody"));Unchanged(before,files);
                }
                Field<UnityEngine.UI.Button>("selectButton").onClick.Invoke();Unchanged(before,files);
                if(pass==0)yield return Back();else yield return Click("closeButton");CheckHub();Unchanged(before,files);
            }
            yield return Click("sectionButtons",0);Assert.That(Field<int>("wavePanel"),Is.EqualTo(75));Unchanged(before,files);
            yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Unchanged(before,files);
            Assert.That(next.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo(oldCaption));Assert.That(nextRect.anchorMin,Is.EqualTo(oldMin));Assert.That(nextRect.anchorMax,Is.EqualTo(oldMax));
        }
        void Fits(TMP_Text text)
        {
            Canvas.ForceUpdateCanvases();text.ForceMeshUpdate();var rect=text.rectTransform.rect;
            Assert.That(rect.width,Is.GreaterThan(0));Assert.That(rect.height,Is.GreaterThan(0));
            Assert.That(text.isTextOverflowing,Is.False,text.name+" overflow: "+text.text);
            Assert.That(text.textBounds.size.x,Is.LessThanOrEqualTo(rect.width+2),text.name);
            Assert.That(text.textBounds.size.y,Is.LessThanOrEqualTo(rect.height+2),text.name);
        }
        void CheckMapLayout()
        {
            var map=Field<RectTransform>("sectionMapRoot");var safe=Field<RectTransform>("safe");Assert.That(map.gameObject.activeInHierarchy,Is.True);
            var corners=new Vector3[4];map.GetWorldCorners(corners);
            foreach(var corner in corners){var local=safe.InverseTransformPoint(corner);Assert.That(local.x,Is.InRange(safe.rect.xMin-2,safe.rect.xMax+2));Assert.That(local.y,Is.InRange(safe.rect.yMin-2,safe.rect.yMax+2));}
            foreach(var text in map.GetComponentsInChildren<TMP_Text>())Fits(text);
            Assert.That(Field<UnityEngine.UI.Button[]>("sectionMapNodes").Length,Is.EqualTo(24));
            Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Length,Is.EqualTo(1));
        }
        void CheckHub()
        {
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(20));Assert.That(Text("panelTitle"),Is.EqualTo("All Sections"));
            var root=Field<RectTransform>("sectionsHubRoot");Assert.That(root.gameObject.activeInHierarchy,Is.True);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>())Fits(text);
            Assert.That(Field<UnityEngine.UI.Button[]>("sectionButtons").Select(b=>b.GetComponentInChildren<TMP_Text>().text),
                Is.EqualTo(new[]{"Game","Progress Map","Buildings","Skills","Orders","Progress","Rules"}));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeInHierarchy||Field<UnityEngine.UI.Button>("chapterPrevious").gameObject.activeInHierarchy||Field<UnityEngine.UI.Button>("chapterNext").gameObject.activeInHierarchy,Is.False,"Hub must not expose overlapping Select/paging controls");
            var buttons=Field<UnityEngine.UI.Button[]>("sectionButtons");var bounds=new List<Rect>();
            foreach(var button in buttons)
            {
                var rect=(RectTransform)button.transform;var min=root.InverseTransformPoint(rect.TransformPoint(rect.rect.min));var max=root.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
                Assert.That(min.x,Is.GreaterThanOrEqualTo(root.rect.xMin-2));Assert.That(min.y,Is.GreaterThanOrEqualTo(root.rect.yMin-2));
                Assert.That(max.x,Is.LessThanOrEqualTo(root.rect.xMax+2));Assert.That(max.y,Is.LessThanOrEqualTo(root.rect.yMax+2));
                Assert.That(rect.rect.width,Is.GreaterThan(0));Assert.That(rect.rect.height,Is.GreaterThanOrEqualTo(44),"Qualified portrait hub requires44 logical-unit tap height");
                var panel=(RectTransform)Field<GameObject>("panel").transform;var b=RectTransformUtility.CalculateRelativeRectTransformBounds(panel,button.transform);
                Assert.That(b.Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(panel,Field<UnityEngine.UI.Button>("closeButton").transform)),Is.False);
                Assert.That(b.Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(panel,Field<TMP_Text>("panelTitle").transform)),Is.False);
                var cell=Rect.MinMaxRect(min.x,min.y,max.x,max.y);foreach(var previous in bounds)Assert.That(cell.Overlaps(previous),Is.False,"Section tap rectangles overlap");bounds.Add(cell);
            }
        }
        void HubRaycasts()
        {
            foreach(var button in Field<UnityEngine.UI.Button[]>("sectionButtons").Concat(new[]{Field<UnityEngine.UI.Button>("closeButton")}))
            {
                var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();
                var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Assert.That(hits.Count,Is.GreaterThan(0));
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(button.gameObject),"Topmost hub/Close raycast must reach the intended button");
            }
        }
        IEnumerator Rules()
        {
            string before=codec.Encode(Session.State),guide=JunkToNeon.Guidance.GuideStateCodec.Encode(Session.Journey.Guidance);var files=Files();
            var previous=Field<UnityEngine.UI.Button>("chapterPrevious");var next=Field<UnityEngine.UI.Button>("chapterNext");
            string previousCaption=previous.GetComponentInChildren<TMP_Text>(true).text,nextCaption=next.GetComponentInChildren<TMP_Text>(true).text;
            bool previousInteractable=previous.interactable,nextInteractable=next.interactable;
            var previousMin=((RectTransform)previous.transform).anchorMin;var nextMin=((RectTransform)next.transform).anchorMin;
            string[][] facts={new[]{"Free spin: 3 symbols.","all different: 3; pair: 8; triple: 23.","do not stack"},
                new[]{"Enough Coins: buy immediately.","Clear goal: free, no refund.","New selections use current prices."},
                new[]{"No spins in background.","1 Coin per full minute.","Up to 480 per absence (8h).","automatically"},
                new[]{"24 paid building stages.","free: 12/24.","optional.","backup may"},
                new[]{"Main spins are free.","Parts","Potatoes","Energy","never retroactively or on a timer","Equipment","bonus reel","explicit Diamond purchases of Parts or Potatoes"}};
            yield return EnterSections();yield return null;CheckHub();HubRaycasts();
            for(int pass=0;pass<2;pass++)
            {
                yield return Click("sectionButtons",6);
                for(int page=0;page<5;page++)
                {
                    Assert.That(Text("panelTitle"),Is.EqualTo("Rules · "+(page+1)+"/5"));foreach(string fact in facts[page])Assert.That(Text("panelBody"),Does.Contain(fact));
                    Assert.That(previous.gameObject.activeSelf,Is.EqualTo(page>0));Assert.That(next.gameObject.activeSelf,Is.EqualTo(page<4));
                    Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);Assert.That(Field<string>("shownGuide"),Is.Null);
                    Assert.That(CloseLabel(),Is.EqualTo("Back"));Fits(Field<TMP_Text>("panelBody"));foreach(var b in new[]{previous,next}.Where(b=>b.gameObject.activeSelf))Fits(b.GetComponentInChildren<TMP_Text>(true));Unchanged(before,files);
                    if(page==0){previous.onClick.Invoke();Assert.That(Text("panelTitle"),Is.EqualTo("Rules · 1/5"));Unchanged(before,files);}
                    if(page<4)yield return Click("chapterNext");
                }
                next.onClick.Invoke();Assert.That(Text("panelTitle"),Is.EqualTo("Rules · 5/5"));Unchanged(before,files);
                yield return Click("chapterPrevious");Assert.That(Text("panelTitle"),Is.EqualTo("Rules · 4/5"));yield return Click("chapterNext");
                if(pass==0)yield return Back();else yield return Click("closeButton");yield return null;CheckHub();HubRaycasts();Unchanged(before,files);
            }
            yield return Click("sectionButtons",6);Assert.That(Text("panelTitle"),Is.EqualTo("Rules · 1/5"));
            Runtime("OnApplicationPause",true);next.onClick.Invoke();previous.onClick.Invoke();Unchanged(before,files);Runtime("OnApplicationPause",false);next.onClick.Invoke();
            Assert.That(previous.gameObject.activeSelf||next.gameObject.activeSelf,Is.False);Assert.That(Text("panelTitle"),Is.EqualTo("Rules · 1/5"));Unchanged(before,files);
            yield return Click("closeButton");Assert.That(Field<GameObject>("panel").activeSelf,Is.False);next.onClick.Invoke();Field<UnityEngine.UI.Button[]>("sectionButtons")[6].onClick.Invoke();Unchanged(before,files);
            Assert.That(previous.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo(previousCaption));Assert.That(next.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo(nextCaption));
            Assert.That(previous.interactable,Is.EqualTo(previousInteractable));Assert.That(next.interactable,Is.EqualTo(nextInteractable));Assert.That(((RectTransform)previous.transform).anchorMin,Is.EqualTo(previousMin));Assert.That(((RectTransform)next.transform).anchorMin,Is.EqualTo(nextMin));
            Assert.That(JunkToNeon.Guidance.GuideStateCodec.Encode(Session.Journey.Guidance),Is.EqualTo(guide));
        }
        [SetUp]public void Isolate()
        {
            ui.IsolateSave();Assert.That(Path.GetPathRoot(Path.GetFullPath(DirectoryPath)).Replace('\\','/'),Is.EqualTo("X:/").IgnoreCase);
        }

        [UnityTest,Category("Expansion5h")]
        public IEnumerator ResourceRulesRemainReadOnlyAndKeepPagingConsent()
        {
            yield return Harness("Prepare",ModelState.Fresh(1));
            yield return Rules();
        }

        const string ExpectedResourceSources="Main spins are free. Parts buy modules and equipment; Potatoes buy upgrades or trade for Coins; Energy powers boosts. Resources come from new spins or explicit Diamond purchases of Parts or Potatoes, never retroactively or on a timer.";
        const string ExistingRulesTail="\n\nInstall modules in sockets. Equipment upgrades its own building. Unlock the bonus reel in the shop.";
        [Serializable]sealed class ResourceRulesMeasure
        {public string name,text;public float font,min,width,height,boundsWidth,boundsHeight;}
        [Serializable]sealed class ResourceRulesView
        {public int width,height,frame;public string state;public ResourceRulesMeasure[] texts;}
        IEnumerator ResourceRulesPicture()
        {
            string folder=Environment.GetEnvironmentVariable("JTN_RULES_CAPTURE_DIRECTORY");if(string.IsNullOrEmpty(folder))yield break;
            folder=Path.GetFullPath(folder);string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/resource-source-rules-20261103/captures"));
            Assert.That(folder.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(folder);Assert.That(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length,Is.LessThan(2));
            var targets=new[]{Field<TMP_Text>("panelTitle"),Field<TMP_Text>("panelBody"),Field<UnityEngine.UI.Button>("chapterPrevious").GetComponentInChildren<TMP_Text>(),Field<UnityEngine.UI.Button>("closeButton").GetComponentInChildren<TMP_Text>()};
            var measures=new List<ResourceRulesMeasure>();foreach(var text in targets){Fits(text);Assert.That(text.fontSize,Is.GreaterThanOrEqualTo(text.fontSizeMin));measures.Add(new ResourceRulesMeasure{name=text.name,text=text.text,font=text.fontSize,min=text.fontSizeMin,width=text.rectTransform.rect.width,height=text.rectTransform.rect.height,boundsWidth=text.textBounds.size.x,boundsHeight=text.textBounds.size.y});}
            Assert.That(Field<TMP_Text>("panelBody").fontSize,Is.GreaterThanOrEqualTo(14));
            foreach(var button in new[]{Field<UnityEngine.UI.Button>("chapterPrevious"),Field<UnityEngine.UI.Button>("closeButton")})
            {var rect=(RectTransform)button.transform;var corners=new Vector3[4];rect.GetWorldCorners(corners);var canvas=button.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);Assert.That(max.x-min.x,Is.GreaterThanOrEqualTo(48));Assert.That(max.y-min.y,Is.GreaterThanOrEqualTo(48));}
            string state=codec.Encode(Session.State);var files=Files();string path=Path.Combine(folder,"rules5.png");Assert.That(File.Exists(path),Is.False);var view=new ResourceRulesView{width=Screen.width,height=Screen.height,frame=Time.frameCount,state=state,texts=measures.ToArray()};
            ScreenCapture.CaptureScreenshot(path);for(int frame=0;frame<30&&!File.Exists(path);frame++)yield return null;Assert.That(File.Exists(path),Is.True);File.WriteAllText(Path.ChangeExtension(path,".json"),JsonUtility.ToJson(view,true));Unchanged(state,files);
        }
        IEnumerator SettingsResourceRules(int height)
        {
            Assert.That(Screen.width,Is.EqualTo(1080));Assert.That(Screen.height,Is.EqualTo(height));yield return Harness("Prepare",ModelState.Fresh(1));
            string before=codec.Encode(Session.State);var files=Files();int frame=Time.frameCount;
            yield return Click("productSettings");Assert.That(Text("panelTitle"),Is.EqualTo("Settings"));yield return Click("productCards",1);Assert.That(Text("panelTitle"),Is.EqualTo("Rules · 1/5"));
            var previous=Field<UnityEngine.UI.Button>("chapterPrevious");var next=Field<UnityEngine.UI.Button>("chapterNext");Assert.That(previous.gameObject.activeSelf,Is.False);previous.onClick.Invoke();Unchanged(before,files);
            for(int page=1;page<5;page++){yield return Click("chapterNext");Assert.That(Text("panelTitle"),Is.EqualTo("Rules · "+(page+1)+"/5"));Unchanged(before,files);}
            Assert.That(Time.frameCount,Is.GreaterThan(frame));Assert.That(ExpectedResourceSources.Length,Is.EqualTo(234));Assert.That(ExpansionCopy.FirstUse,Is.EqualTo(ExpectedResourceSources));Assert.That(Text("panelBody"),Is.EqualTo(ExpectedResourceSources+ExistingRulesTail));
            Assert.That(next.gameObject.activeSelf,Is.False);next.onClick.Invoke();Assert.That(Text("panelTitle"),Is.EqualTo("Rules · 5/5"));Unchanged(before,files);
            foreach(var name in new[]{"selectButton","expansionPrimaryButton","arcadePrimaryButton"}){var button=Field<UnityEngine.UI.Button>(name);Assert.That(button.gameObject.activeInHierarchy,Is.False);button.onClick.Invoke();Unchanged(before,files);}
            yield return Click("chapterPrevious");Assert.That(Text("panelTitle"),Is.EqualTo("Rules · 4/5"));yield return Click("chapterNext");Assert.That(Text("panelBody"),Is.EqualTo(ExpectedResourceSources+ExistingRulesTail));yield return ResourceRulesPicture();
            yield return Back();Assert.That(Text("panelTitle"),Is.EqualTo("Settings"));Unchanged(before,files);
            yield return Click("productCards",1);Assert.That(Text("panelTitle"),Is.EqualTo("Rules · 1/5"));yield return Click("closeButton");Assert.That(Text("panelTitle"),Is.EqualTo("Settings"));yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Unchanged(before,files);
            TestContext.WriteLine("Visible Settings -> How to Play -> Next x4: exact234-character FirstUse plus existing tail; full body/targets fit, paging boundaries and Back/Close via Settings preserve encoded state and private bytes/timestamps. Hidden Select/purchase events inert; no Spin/purchase/claim/ACK.");
        }
        [UnityTest]public IEnumerator SettingsRulesExplainDiamondResourcePacksReadOnly(){yield return SettingsResourceRules(2340);}
        [UnityTest]public IEnumerator ResourceRulesFullBodyFitsShortPortraitReadOnly(){yield return SettingsResourceRules(2280);}

        [UnityTest]
        public IEnumerator FreshSectionsMapAndExistingRoutesStayReadOnlyAndBusyCannotRevealMap()
        {
            yield return Harness("Prepare",ModelState.Fresh(1));
            string before=codec.Encode(Session.State);var files=Files();
            yield return Click("productAvatar");yield return Click("productCards",2);CheckHub();Unchanged(before,files);
            var controls=Field<UnityEngine.UI.Button[]>("sectionButtons").ToArray();int children=Field<RectTransform>("sectionsHubRoot").childCount;
            Runtime("CreateSectionsUi");Runtime("CreateSectionsUi");Assert.That(Field<UnityEngine.UI.Button[]>("sectionButtons"),Is.EqualTo(controls));Assert.That(Field<RectTransform>("sectionsHubRoot").childCount,Is.EqualTo(children));
            yield return Click("sectionButtons",0);Unchanged(before,files);
            // Back leaves All Sections for the Profile hub, then the hub for the game root.
            yield return Click("productAvatar");yield return Click("productCards",2);yield return Back();yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Unchanged(before,files);
            yield return Progress("Buildings: 0/24","house repaired no","Workshop no","Junkyard no","chapter no","inspect the house");
            yield return Rules();
            yield return OpenMap();Assert.That(Text("sectionMapCount"),Does.Contain("0/24"));Assert.That(Text("sectionMapGoal"),Does.Contain("No goal selected"));
            Assert.That(Node(0),Does.Contain("Available").And.Contain("40 · saving"));Assert.That(Node(1),Does.Contain("Locked").And.Contain("Requires I"));Assert.That(Node(12),Does.Contain("Locked").And.Contain("Era entry"));
            Assert.That(Node(2),Does.Contain("Locked").And.Contain("House repair"));Assert.That(Node(3),Does.Contain("Locked").And.Contain("Requires I"));
            Assert.That(Node(13),Does.Contain("Era entry").And.Not.Contain("Requires I"));
            Assert.That(Field<UnityEngine.UI.Button[]>("sectionMapNodes")[0].interactable,Is.True);
            Assert.That(Field<UnityEngine.UI.Button[]>("sectionMapNodes")[1].interactable,Is.False);
            Assert.That(Field<UnityEngine.UI.Button[]>("sectionMapNodes")[2].interactable,Is.False);
            var staleMap=Field<UnityEngine.UI.Button[]>("sectionMapNodes")[0].onClick;
            Unchanged(before,files);yield return Back();Unchanged(before,files);
            Assert.That(Field<RectTransform>("sectionMapRoot").gameObject.activeSelf,Is.False);
            CheckHub();yield return Click("sectionButtons",1);CheckMapLayout();yield return Click("sectionMapNodes",0);
            Assert.That(Text("panelBody"),Does.Contain("Price: 40 Coins."));Unchanged(before,files);
            Assert.That(Field<RectTransform>("sectionMapRoot").gameObject.activeSelf,Is.False);Assert.That(CloseLabel(),Is.EqualTo("Back"));
            yield return Back();CheckMapLayout();Unchanged(before,files);
            yield return Click("sectionMapNodes",0);Assert.That(CloseLabel(),Is.EqualTo("Back"));yield return Click("closeButton");CheckMapLayout();Unchanged(before,files);
            yield return Click("sectionMapClose");CheckHub();Unchanged(before,files);
            yield return Click("sectionButtons",2);Assert.That(Text("panelTitle"),Is.EqualTo("Buildings · all six"));yield return Back();CheckHub();Unchanged(before,files);
            yield return Click("sectionButtons",3);Assert.That(Text("panelTitle"),Is.EqualTo("Skills · optional"));yield return Back();CheckHub();Unchanged(before,files);
            yield return Click("sectionButtons",4);Assert.That(Text("panelTitle"),Is.EqualTo("Orders · optional"));
            Assert.That(Text("navigationContext"),Does.Contain("Unlocks at 24/24").And.Contain("Optional to finish."));
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);yield return Back();CheckHub();Unchanged(before,files);
            // Construction remains reachable through District alongside the All Sections entry; Back returns to the District hub.
            yield return ReturnToGame();Unchanged(before,files);
            yield return Click("productTabs",0);yield return Click("productCards",0);Assert.That(Text("panelTitle"),Is.EqualTo("Buildings · all six"));yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(70));Unchanged(before,files);
            yield return ReturnToGame();Unchanged(before,files);
            yield return Click("spinButton");Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.True);
            string committed=codec.Encode(Session.State);files=Files();Assert.That(Session.State.coins,Is.EqualTo(45));Assert.That(Session.Milestones.ConstructionCount,Is.Zero);
            Field<UnityEngine.UI.Button>("sectionsButton").onClick.Invoke();staleMap.Invoke();Field<UnityEngine.UI.Button[]>("sectionButtons")[5].onClick.Invoke();Field<UnityEngine.UI.Button[]>("sectionButtons")[6].onClick.Invoke();Field<UnityEngine.UI.Button>("chapterNext").onClick.Invoke();Unchanged(committed,files);
            Assert.That(Field<RectTransform>("sectionMapRoot").gameObject.activeSelf||Field<RectTransform>("sectionsHubRoot").gameObject.activeSelf,Is.False);
            yield return Harness("FinishPresentation");Unchanged(committed,files);yield return OpenMap();Assert.That(Text("sectionMapCount"),Does.Contain("0/24"));
            yield return Click("sectionMapClose");CheckHub();Unchanged(committed,files);
        }
        ModelState StageFixture(string era,int level)
        {
            // Explicit synthetic boundary facts, not a paid chapter traversal.
            var state=ModelState.Fresh(1);state.coins=100;state.lastSettledAt=1000;state.houseStage=2;state.workshopBuilt=1;state.autoHintSeen=1;
            var stages=new FamilyStages(era,FamilyIds.All.Select(f=>new FamilyStage(f,level)));
            var progress=ChapterCatalog.Rules.FromStages(stages);state.sequence=Math.Max(progress.CompletedUpgradeIds.Count,13);state.revision=state.sequence*2;
            state.wave=new WaveProgress(1,1,WaveProgress.Empty.Delivery,JunkToNeon.Upgrades.UpgradeOwnership.Empty);
            long entry=era==EraIds.WorkshopYard?13:0;
            state.chapter=new ChapterState(2,JunkToNeon.Content.ChapterContentIds.RuleRevision,progress,JunkToNeon.Upgrades.SkillOwnership.Empty,null,entry,entry);
            GameLogic.Validate(state);return state;
        }
        IEnumerator NextFixture(ModelState state)
        {
            state.lastSettledAt=1000;GameLogic.Validate(state);File.WriteAllText(Path.Combine(DirectoryPath,"jtn.save.v1.json"),codec.Encode(state));
            yield return Harness("ReenterScene");
        }
        [UnityTest]
        public IEnumerator SavedGoalPricesFreeEntryAndFiniteOptionalOrdersRemainTruthful()
        {
            var old=ModelState.Fresh(1);old.coins=535;old.houseStage=1;old.selectedStage=2;old.autoHintSeen=1;
            yield return Harness("Prepare",old);
            var v2=codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/Milestones/fixtures/v2-selected540.json"))));
            foreach(long saved in new[]{600L,540L})
            {
                if(saved==540)yield return NextFixture(v2);
                string before=codec.Encode(Session.State);var files=Files();
                yield return Progress("Buildings: 1/24","house repaired yes","Workshop no","535/"+saved+" at its saved price","reselect only explicitly");
                if(saved==600)yield return Rules();
                yield return OpenMap();
                Assert.That(Text("sectionMapGoal"),Does.Contain("535/"+saved+" Coins at its saved price").And.Contain("New selection price: 480 Coins"));
                Assert.That(Node(0),Does.Contain("Done"));Assert.That(Node(1),Does.Contain("New 480").And.Contain("Goal "+saved));
                Assert.That(Node(2),Does.Contain("Available").And.Not.Contain("House repair"));Assert.That(Node(3),Does.Contain("Requires I"));
                Unchanged(before,files);yield return Click("sectionMapNodes",1);
                Assert.That(Text("panelBody"),Does.Contain("Saved goal: "+saved+" Coins. Price: 480 Coins."));Assert.That(CloseLabel(),Is.EqualTo("Back"));Unchanged(before,files);
                yield return Back();CheckMapLayout();Unchanged(before,files);Assert.That(Text("sectionMapGoal"),Does.Contain("535/"+saved+" Coins at its saved price"));
                yield return Click("sectionMapNodes",1);Assert.That(CloseLabel(),Is.EqualTo("Back"));yield return Click("closeButton");CheckMapLayout();Unchanged(before,files);
                yield return Click("sectionMapClose");CheckHub();yield return Click("sectionButtons",5);
                if(saved==540){yield return Click("chapterNext");Assert.That(Text("panelBody"),Does.Contain("Saved goal: 540 Coins. Price: 480 Coins."));Assert.That(CloseLabel(),Is.EqualTo("Back"));}
                Runtime("OnApplicationPause",true);
                Assert.That(Field<UnityEngine.UI.Button>("chapterNext").gameObject.activeSelf,Is.False);
                Field<UnityEngine.UI.Button[]>("sectionMapNodes")[1].onClick.Invoke();Field<UnityEngine.UI.Button>("sectionMapEra").onClick.Invoke();Field<UnityEngine.UI.Button[]>("sectionButtons")[5].onClick.Invoke();Field<UnityEngine.UI.Button>("chapterNext").onClick.Invoke();Field<UnityEngine.UI.Button>("closeButton").onClick.Invoke();Unchanged(before,files);
                Assert.That(CloseLabel(),Is.EqualTo("Close"));Assert.That(Field<UnityEngine.UI.Button[]>("sectionMapNodes")[1].interactable,Is.False);
                Runtime("OnApplicationPause",false);
                if(saved==600){Assert.That(Field<UnityEngine.UI.Button>("chapterNext").gameObject.activeSelf,Is.True);Assert.That(Field<UnityEngine.UI.Button>("chapterNext").GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo("Inspect goal"));}
                yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Unchanged(before,files);
                if(saved==540)
                {
                    // Existing V2 fixture: Progress target inspection itself is free; explicit480 pays once without reopening Progress.
                    yield return EnterSections();yield return Click("sectionButtons",5);yield return Click("chapterNext");Assert.That(CloseLabel(),Is.EqualTo("Back"));
                    Assert.That(Text("panelBody"),Does.Contain("Saved goal: 540 Coins. Price: 480 Coins."));Unchanged(before,files);
                    long seq=Session.State.sequence;uint seed=Session.State.seed;
                    var queued=Field<UnityEngine.UI.Button>("selectButton").onClick;yield return Click("selectButton");
                    Assert.That(Session.State.coins,Is.EqualTo(55));Assert.That(Session.State.sequence,Is.EqualTo(seq+1));Assert.That(Session.State.seed,Is.EqualTo(seed));
                    Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Session.Chapter.Progress.CompletedUpgradeIds,Is.EquivalentTo(new[]{"house.repair","house.improve"}));
                    Assert.That(Session.Chapter.ConstructionPriceRevision,Is.EqualTo("construction.prices.v3"));Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
                    string paid=codec.Encode(Session.State);var paidFiles=Files();queued.Invoke();Field<UnityEngine.UI.Button>("closeButton").onClick.Invoke();Unchanged(paid,paidFiles);
                    Assert.That(Field<RectTransform>("sectionMapRoot").gameObject.activeSelf||Field<RectTransform>("sectionsHubRoot").gameObject.activeSelf,Is.False);
                    yield return Progress("Buildings: 2/24","Next: choose one building");
                }
            }
            yield return NextFixture(StageFixture(EraIds.Junkyard,2));
            string checkpoint=codec.Encode(Session.State);var checkpointFiles=Files();yield return OpenMap();
            Assert.That(Text("sectionMapCount"),Does.Contain("12/24"));Assert.That(Node(11),Does.Contain("Done"));Assert.That(Node(12),Does.Contain("Locked").And.Contain("Era entry"));
            yield return Click("sectionMapEra");Assert.That(Text("panelBody"),Does.Contain("Enter for free?"));Assert.That(CloseLabel(),Is.EqualTo("Back"));Unchanged(checkpoint,checkpointFiles);
            yield return Back();CheckMapLayout();Unchanged(checkpoint,checkpointFiles);
            yield return Click("sectionMapEra");yield return Click("closeButton");CheckMapLayout();Unchanged(checkpoint,checkpointFiles);yield return Click("sectionMapClose");
            Assert.That(Session.Chapter.Progress.EraId,Is.EqualTo(EraIds.Junkyard));
            CheckHub();yield return ReturnToGame();Unchanged(checkpoint,checkpointFiles);
            yield return Progress("Buildings: 12/24","Junkyard yes","chapter no","enter Workshop Yard for free");
            yield return EnterSections();yield return Click("sectionButtons",5);yield return Click("chapterNext");Assert.That(CloseLabel(),Is.EqualTo("Back"));long entry=Session.State.sequence;uint entrySeed=Session.State.seed;
            yield return Click("selectButton");Assert.That(Session.State.sequence,Is.EqualTo(entry+1));Assert.That(Session.Chapter.TransitionPending,Is.True);
            Assert.That(Session.Chapter.TransitionSeen,Is.Zero);Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(12));Assert.That(Session.State.coins,Is.EqualTo(100));Assert.That(Session.State.seed,Is.EqualTo(entrySeed));
            Assert.That(CloseLabel(),Is.EqualTo("Skip introduction"));Assert.That(Field<RectTransform>("sectionMapRoot").gameObject.activeSelf,Is.False);
            Assert.That(Field<UnityEngine.UI.Button>("chapterNext").gameObject.activeSelf||Field<UnityEngine.UI.Button>("chapterPrevious").gameObject.activeSelf,Is.False);
            string pending=codec.Encode(Session.State);var pendingFiles=Files();Field<UnityEngine.UI.Button>("chapterNext").onClick.Invoke();Unchanged(pending,pendingFiles);
            yield return Click("closeButton");Assert.That(Session.State.sequence,Is.EqualTo(entry+2));Assert.That(Session.Chapter.TransitionPending,Is.False);
            Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Assert.That(Session.Milestones.ConstructionCount,Is.EqualTo(12));
            checkpoint=codec.Encode(Session.State);checkpointFiles=Files();yield return OpenMap();
            Assert.That(Text("sectionMapCount"),Does.Contain("12/24"));Assert.That(Node(11),Does.Contain("Done"));Assert.That(Node(12),Does.Contain("Available").And.Not.Contain("Era entry"));Assert.That(Node(13),Does.Contain("Locked").And.Contain("Requires I").And.Not.Contain("Era entry"));
            Assert.That(Text("sectionMapGate"),Does.Contain("free entry added no purchases"));yield return Back();Unchanged(checkpoint,checkpointFiles);
            CheckHub();yield return ReturnToGame();Unchanged(checkpoint,checkpointFiles);
            yield return Progress("Buildings: 12/24","Junkyard yes","chapter no","choose one building");
            yield return NextFixture(StageFixture(EraIds.WorkshopYard,5));checkpoint=codec.Encode(Session.State);checkpointFiles=Files();yield return OpenMap();
            Assert.That(Text("sectionMapCount"),Does.Contain("24/24"));Assert.That(Text("sectionMapGate"),Does.Contain("you may finish").And.Contain("optional"));
            Assert.That(Session.Milestones.ReachedCount,Is.EqualTo(4));Assert.That(Session.Chapter.Skills.Receipts.Count,Is.Zero);
            yield return Click("sectionMapEra");Assert.That(Text("panelBody"),Does.Contain("Skills: 0/6").And.Contain("You may finish here"));Assert.That(CloseLabel(),Is.EqualTo("Back"));
            yield return Back();CheckMapLayout();Unchanged(checkpoint,checkpointFiles);
            yield return Click("sectionMapEra");yield return Click("closeButton");CheckMapLayout();Unchanged(checkpoint,checkpointFiles);yield return Click("sectionMapClose");Unchanged(checkpoint,checkpointFiles);
            CheckHub();yield return Click("sectionButtons",4);Assert.That(Text("panelTitle"),Is.EqualTo("Orders · optional"));
            Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);Assert.That(Session.OptionalAchievements.ReachedCount,Is.Zero);yield return Back();CheckHub();Unchanged(checkpoint,checkpointFiles);
            yield return ReturnToGame();Unchanged(checkpoint,checkpointFiles);
            yield return Progress("Buildings: 24/24","house repaired yes","Workshop yes","Junkyard yes","chapter yes","You may finish here","optional");
            // Existing accepted valid pre-final fixture, not a new paid/human-play traversal.
            var six=codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/Milestones/fixtures/late23-skills6.json"))));
            yield return NextFixture(six);yield return Progress("Buildings: 23/24","chapter no","In this save: all skills owned.","choose one building");
            yield return EnterSections();yield return Click("sectionButtons",5);Assert.That(CloseLabel(),Is.EqualTo("Back"));
            yield return NextFixture(v2);Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Assert.That(Field<RectTransform>("sectionsHubRoot").gameObject.activeSelf,Is.False);
            Field<UnityEngine.UI.Button>("chapterNext").onClick.Invoke();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            yield return Progress("Buildings: 1/24","Junkyard no","chapter no","535/540 at its saved price");
        }
        [UnityTearDown]public IEnumerator RestoreFixture()
        {
            if(TestContext.CurrentContext.Result.Outcome.Status==NUnit.Framework.Interfaces.TestStatus.Failed)
            {TestContext.WriteLine("Preserved failed Sections/Map private fixture: "+DirectoryPath);typeof(ReelUiLifecycleTests).GetField("directory",Hidden).SetValue(ui,null);}
            yield return ui.RestoreAndRemoveOnlyPrivateFixtures();
        }
    }
}
#endif
