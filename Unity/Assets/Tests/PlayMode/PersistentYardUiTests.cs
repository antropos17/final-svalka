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
    public sealed partial class FirstSessionPresentationUiTests
    {
        Graphic Yard(int i)=>(Graphic)Field<Array>("yardFamilies").GetValue(i);
        int YardStage(int i)=>(int)Yard(i).GetType().GetProperty("PaidStage").GetValue(Yard(i));
        string YardSignature(int i)
        {
            Canvas.ForceUpdateCanvases();var graphic=Yard(i);var mesh=graphic.canvasRenderer.GetMesh();
            {
                // Compare actual rendered vertices/colors, not a stage number or a label.
                // Quantize to 1/10000: the selection ring has exact .5 per-mille values (0.7225) that round either way after a resize.
                var rect=graphic.rectTransform.rect;var vertices=mesh.vertices;var colors=mesh.colors32;
                string data=string.Join(";",vertices.Select((v,n)=>$"{Mathf.Round((v.x-rect.x)/rect.width*10000)},{Mathf.Round((v.y-rect.y)/rect.height*10000)},{ColorUtility.ToHtmlStringRGBA(colors[n])}"));
                if(i==0)
                {
                    var image=Field<Image>("house");data+="|"+image.rectTransform.pivot;
                    if(image.sprite!=null)data+="|"+image.sprite.name;
                    else
                    {
                        // Removed temporary art uses the supported procedural house.
                        var placeholder=Field<Graphic>("yardHousePlaceholder");
                        Assert.That(placeholder.isActiveAndEnabled,Is.True);
                        var fallback=placeholder.canvasRenderer.GetMesh();
                        Assert.That(fallback.vertexCount,Is.GreaterThan(0));
                        var bounds=placeholder.rectTransform.rect;var points=fallback.vertices;var tint=fallback.colors32;
                        data+="|"+string.Join(";",points.Select((v,n)=>$"{Mathf.Round((v.x-bounds.x)/bounds.width*1000)},{Mathf.Round((v.y-bounds.y)/bounds.height*1000)},{ColorUtility.ToHtmlStringRGBA(tint[n])}"));
                    }
                }
                using(var hash=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data)));
            }
        }
        string[] YardSignatures()=>Enumerable.Range(0,6).Select(YardSignature).ToArray();
        ModelState YardFixture(string era,params int[] absolute)
        {
            return (ModelState)typeof(CombinedLifecycleTests).GetMethod("MilestoneEraFixture",Hidden).Invoke(new CombinedLifecycleTests(),new object[]{era,absolute});
        }
        [Serializable]sealed class YardCaptureView
        {public string name,era,state;public int width,height,frame;public int[] paid;public string[] geometry;public Vector2[] sizes;}
        IEnumerator YardCapture(string name)
        {
            string folder=Environment.GetEnvironmentVariable("JTN_YARD_CAPTURE_DIRECTORY");if(string.IsNullOrEmpty(folder))yield break;
            string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/persistent-yard-20261103/captures"));folder=Path.GetFullPath(folder);
            Assert.That(folder.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(folder);
            Assert.That(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length,Is.LessThan(16));string path=Path.Combine(folder,name+".png");Assert.That(File.Exists(path),Is.False);
            Canvas.ForceUpdateCanvases();var view=new YardCaptureView{name=name,era=Session.Chapter.Progress.EraId,state=State,width=Screen.width,height=Screen.height,frame=Time.frameCount,paid=Enumerable.Range(0,6).Select(YardStage).ToArray(),geometry=YardSignatures(),sizes=Enumerable.Range(0,6).Select(i=>Yard(i).rectTransform.rect.size).ToArray()};
            var files=Files();File.WriteAllText(Path.ChangeExtension(path,".json"),JsonUtility.ToJson(view,true));ScreenCapture.CaptureScreenshot(path);for(int f=0;f<30&&!File.Exists(path);f++)yield return null;Assert.That(File.Exists(path),Is.True);Unchanged(view.state,files);
        }
        Rect ScreenRect(RectTransform rect)
        {var corners=new Vector3[4];rect.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);}
        void YardLayout()
        {
            foreach(var control in new[]{"productSettings","productAvatar","productEnergy","productGoalButton","productMapShortcut","productEventShortcut","guideButton","spinButton","autoButton"})
            {
                var b=Field<Button>(control);if(!b.gameObject.activeInHierarchy){Assert.That(control,Is.EqualTo("guideButton"));continue;}var r=ScreenRect((RectTransform)b.transform);Assert.That(r.width,Is.GreaterThanOrEqualTo(48),control);Assert.That(r.height,Is.GreaterThanOrEqualTo(48),control);
                var data=new PointerEventData(EventSystem.current){position=r.center};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Assert.That(hits.Count,Is.GreaterThan(0),control);Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(b.gameObject),control);
                for(int i=0;i<6;i++)Assert.That(ScreenRect(Yard(i).rectTransform).Overlaps(r),Is.False,control+" overlaps family"+i);
            }
            foreach(var b in Field<Button[]>("productTabs").Concat(new[]{Field<Button>("expansionQuickBoostButton")}))
            {
                var r=ScreenRect((RectTransform)b.transform);Assert.That(r.width,Is.GreaterThanOrEqualTo(48));Assert.That(r.height,Is.GreaterThanOrEqualTo(48));
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=r.center},hits);Assert.That(hits.Count,Is.GreaterThan(0),b.name);Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(b.gameObject),b.name);
                for(int i=0;i<6;i++)Assert.That(ScreenRect(Yard(i).rectTransform).Overlaps(r),Is.False,b.name+" overlaps family"+i);
            }
            foreach(string name in new[]{"productPowerCue","productEventBadge","productNarrativeSurface"})
            {var field=Game.GetType().GetField(name,Hidden).GetValue(Game);var component=(Component)field;if(!component.gameObject.activeInHierarchy)continue;for(int i=0;i<6;i++)Assert.That(ScreenRect(Yard(i).rectTransform).Overlaps(ScreenRect((RectTransform)component.transform)),Is.False,name+" obscures family"+i);}
            foreach(var g in Enumerable.Range(0,6).Select(Yard))
            {Assert.That(g.raycastTarget,Is.False);Assert.That(g.GetComponent<Button>(),Is.Null);Assert.That(g.rectTransform.rect.width,Is.GreaterThan(40));Assert.That(g.rectTransform.rect.height,Is.GreaterThan(40));}
            foreach(string name in new[]{"productIdentity","productCoins","productDiamonds","productEnergyText","heading","productGoalText","spinLabel"})Fits(Field<TMP_Text>(name));
            Assert.That(Field<Image>("workshopDraft").gameObject.activeSelf,Is.False);
        }
        IEnumerator YardLayouts(int height)
        {
            Assert.That(Screen.width,Is.EqualTo(1080));Assert.That(Screen.height,Is.EqualTo(height));yield return Harness("Prepare",ModelState.Fresh(1));Call("SetRewardFeedbackReduced",true);
            YardLayout();yield return YardCapture("early");
            // House remains the only existing world inspection target. No new family buttons.
            string before=State;var files=Files();yield return Click("chapterWorldButtons",0);Assert.That(Field<GameObject>("panel").activeSelf,Is.True);yield return Back();Unchanged(before,files);
            if(height==2340){yield return ReplacePrivate(YardFixture(EraIds.Junkyard,1,1,1,1,1,1));YardLayout();yield return YardCapture("paid-I");yield return ReplacePrivate(YardFixture(EraIds.WorkshopYard,4,4,4,4,4,4));YardLayout();yield return YardCapture("paid-III");}
            yield return ReplacePrivate(YardFixture(EraIds.Junkyard,2,2,2,2,2,2));YardLayout();string[] dense=YardSignatures();yield return YardCapture("junkyard12");
            long coins=Session.State.coins;uint seed=Session.State.seed;string[] ids=Session.Chapter.Progress.CompletedUpgradeIds.ToArray();
            yield return Click("productGoalButton");yield return Click("selectButton");Assert.That(Session.Chapter.TransitionPending,Is.True);Assert.That(Session.Chapter.Progress.CompletedUpgradeIds,Is.EquivalentTo(ids));Assert.That(Session.State.coins,Is.EqualTo(coins));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(YardSignatures(),Is.EqualTo(dense));
            string entered=State;Call("Reload");yield return null;Assert.That(State,Is.EqualTo(entered));Assert.That(YardSignatures(),Is.EqualTo(dense));yield return Click("closeButton");Assert.That(Session.Chapter.TransitionPending,Is.False);Assert.That(YardSignatures(),Is.EqualTo(dense));YardLayout();yield return YardCapture("free-entry12");
            yield return ReplacePrivate(YardFixture(EraIds.WorkshopYard,5,5,5,5,5,5));Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(24));YardLayout();yield return YardCapture("complete24");
            before=State;files=Files();foreach(string control in new[]{"productMapShortcut","productEventShortcut","productEnergy","guideButton","productSettings"}){if(!Field<Button>(control).gameObject.activeInHierarchy)continue;yield return Click(control);yield return Back();Unchanged(before,files);}
        }
        [UnityTest]public IEnumerator PersistentYardFitsRegularAndSurvivesFreeEntry(){yield return YardLayouts(2340);}
        [UnityTest]public IEnumerator PersistentYardFitsShortAndSurvivesFreeEntry(){yield return YardLayouts(2280);}

        [UnityTest]public IEnumerator EveryPaidProjectChangesOnlyItsFamilyGeometry()
        {
            yield return Harness("Prepare",ModelState.Fresh(1));string original=State;var files=Files();var observed=new HashSet<string>();
            // Independent stable IDs, including the free-era absolute3 baseline (not paid stage3).
            string[][] ids={new[]{"house.repair","house.improve","house.workshop-yard.upgrade-1","house.workshop-yard.upgrade-2"},new[]{"workshop.build","workshop.improve","workshop.workshop-yard.upgrade-1","workshop.workshop-yard.upgrade-2"},new[]{"recycler.build","recycler.improve","recycler.workshop-yard.upgrade-1","recycler.workshop-yard.upgrade-2"},new[]{"pigeon-loft.build","pigeon-loft.improve","pigeon-loft.workshop-yard.upgrade-1","pigeon-loft.workshop-yard.upgrade-2"},new[]{"cart.build","cart.improve","cart.workshop-yard.upgrade-1","cart.workshop-yard.upgrade-2"},new[]{"power-source.build","power-source.improve","power-source.workshop-yard.upgrade-1","power-source.workshop-yard.upgrade-2"}};
            for(int family=0;family<6;family++)for(int paid=1;paid<=4;paid++)
            {
                string era=paid<=2?EraIds.Junkyard:EraIds.WorkshopYard;int[] levels=Enumerable.Repeat(paid<=2?0:3,6).ToArray();if(paid<=2&&family!=0)levels[0]=1;
                levels[family]=paid<=2?paid-1:paid;var a=YardFixture(era,levels);Call("RenderYard",a);Canvas.ForceUpdateCanvases();var shapes=YardSignatures();
                levels[family]++;var b=YardFixture(era,levels);Assert.That(b.chapter.Progress.CompletedUpgradeIds.Except(a.chapter.Progress.CompletedUpgradeIds),Is.EqualTo(new[]{ids[family][paid-1]}));Call("RenderYard",b);Canvas.ForceUpdateCanvases();Assert.That(YardStage(family),Is.EqualTo(paid));var changed=YardSignatures();Assert.That(changed[family],Is.Not.EqualTo(shapes[family]),ids[family][paid-1]);
                for(int other=0;other<6;other++)if(other!=family)Assert.That(changed[other],Is.EqualTo(shapes[other]),"Unrelated family changed");observed.Add(ids[family][paid-1]);Unchanged(original,files);
            }
            Assert.That(observed.Count,Is.EqualTo(24));Call("Render",null,null);
            // Mixed legal ownership is reconstructed from saved IDs, not total count or goal.
            yield return ReplacePrivate(YardFixture(EraIds.Junkyard,1,2,1,0,2,1));Assert.That(Enumerable.Range(0,6).Select(YardStage),Is.EqualTo(new[]{1,2,1,0,2,1}));
            yield return ReplacePrivate(YardFixture(EraIds.WorkshopYard,4,3,5,4,5,3));Assert.That(Enumerable.Range(0,6).Select(YardStage),Is.EqualTo(new[]{3,2,4,3,4,2}));
            TestContext.WriteLine("All24 literal paid IDs change actual family mesh/sprite, preserve five unrelated composites and perform0IO; two mixed validated saved layouts and all initial0 states are covered. Synthetic ownership fixtures are not played progression campaigns.");
        }
        IEnumerator SelectYardFamily(int family)
        {yield return Click("productTabs",0);yield return Click("productCards",0);yield return Click("navigationCards",family);yield return Click("selectButton");}
        [UnityTest]public IEnumerator NonHousePaidWorldWaitsAcrossResizePauseAndRecovery()
        {
            Assert.That(HouseProfile(),Is.EqualTo(2));var initial=YardFixture(EraIds.Junkyard,1,0,0,0,0,0);initial.coins=119;yield return Harness("Prepare",initial);Call("SetRewardFeedbackReduced",true);yield return SelectYardFamily(1);
            Assert.That(Session.Chapter.SelectedProjectId,Is.EqualTo("workshop.build"));var before=Session.State;string[] old=YardSignatures();yield return Click("spinButton");
            var after=Session.State;Assert.That(after.coins,Is.EqualTo(7));Assert.That(after.lastSpin.reward,Is.EqualTo(8));Assert.That(after.lastSpin.bonus,Is.Zero);Assert.That(after.seed,Is.EqualTo(2647435461u));Assert.That(after.chapter.Progress.CompletedUpgradeIds,Does.Contain("workshop.build"));Assert.That(after.pending,Is.Null);Assert.That(YardSignatures(),Is.EqualTo(old));
            Assert.That(codec.Decode(File.ReadAllText(Path.Combine(Folder,"jtn.save.v1.json"))).coins,Is.EqualTo(7));string committed=State;var files=Files();Vector2 size=Yard(1).rectTransform.rect.size;
            HouseProfile(3);float until=Time.realtimeSinceStartup+1.2f;while((Screen.height!=2280||Field<Vector2>("lastScreenSize")!=new Vector2(Screen.width,Screen.height))&&Time.realtimeSinceStartup<until)yield return null;yield return null;Assert.That(HouseFacts("pending").busy,Is.True);Assert.That(Yard(1).rectTransform.rect.size,Is.Not.EqualTo(size));Assert.That(YardSignatures(),Is.EqualTo(old));Unchanged(committed,files);
            Call("OnApplicationPause",true);Call("Render",null,null);Assert.That(YardSignatures(),Is.EqualTo(old));yield return null;Call("OnApplicationPause",false);Assert.That(YardSignatures(),Is.EqualTo(old));Assert.That(Session.State.coins,Is.EqualTo(7));Assert.That(Session.State.seed,Is.EqualTo(after.seed));yield return YardCapture("workshop-busy");
            yield return HouseFinish();Assert.That(YardStage(1),Is.EqualTo(1));Assert.That(YardSignature(1),Is.Not.EqualTo(old[1]));for(int i=0;i<6;i++)if(i!=1)Assert.That(YardSignature(i),Is.EqualTo(old[i]));yield return YardCapture("workshop-revealed");
            // Second genuine non-house build uses the saved fast setting, with pending reload cancellation.
            var fast=YardFixture(EraIds.Junkyard,1,1,1,0,0,0);string target="recycler.improve";fast.coins=ChapterEconomy.ProjectPrice(target)-1;var setup=GameSession.Open(new GameStore(new HouseStorage{Raw=codec.Encode(fast)},codec),1,initializeJourney:true);setup.SelectExpansionSpeed("speed.fast",setup.State.sequence,1000);setup.SelectChapterProject(target,setup.State.sequence+1,1000);yield return ReplacePrivate(setup.State);old=YardSignatures();
            Assert.That(Session.Expansion.SpinDurationSeconds,Is.EqualTo(1.5));yield return Click("spinButton");Assert.That(HouseFacts("fast").busy,Is.True);Assert.That(Session.Chapter.Progress.CompletedUpgradeIds,Does.Contain(target));Assert.That(YardSignatures(),Is.EqualTo(old));committed=State;Call("Reload");yield return null;Assert.That(HouseFacts("loaded").busy,Is.False);Assert.That(YardStage(2),Is.EqualTo(2));Assert.That(State,Is.EqualTo(committed));string[] loaded=YardSignatures();yield return new WaitForSecondsRealtime(1.7f);Assert.That(State,Is.EqualTo(committed));Assert.That(YardSignatures(),Is.EqualTo(loaded));
            // Genuine pending journal from owning reservation/apply code, then actual private file recovery.
            var reserved=JourneyEconomy.ReserveChapter(setup.State,"spin",null,setup.State.sequence+1,1000,manualSpin:true);var expected=GameLogic.ApplyPending(reserved);File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(reserved));Call("Reload");yield return null;Assert.That(State,Is.EqualTo(codec.Encode(expected)));Assert.That(Session.State.pending,Is.Null);Assert.That(YardStage(2),Is.EqualTo(2));Assert.That(HouseFacts("recovered").busy,Is.False);committed=State;loaded=YardSignatures();Call("Reload");yield return null;Assert.That(State,Is.EqualTo(committed));Assert.That(YardSignatures(),Is.EqualTo(loaded));
            // Affordable explicit selection paints immediately without another spin/RNG draw.
            var affordable=YardFixture(EraIds.Junkyard,1,0,0,0,0,0);affordable.coins=120;yield return ReplacePrivate(affordable);uint seed=Session.State.seed;yield return SelectYardFamily(1);Assert.That(Session.State.coins,Is.Zero);Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(YardStage(1),Is.EqualTo(1));Assert.That(HouseFacts("immediate").busy,Is.False);
        }
    }
}
#endif
