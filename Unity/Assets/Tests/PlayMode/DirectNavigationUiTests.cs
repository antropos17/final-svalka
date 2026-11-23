#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Guidance;
using JunkToNeon.Orders;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace JunkToNeon.Tests.PlayMode
{
    // Parent executes only after its four Chapter/Journey hooks and scene authoring.
    // Prepared boundary fixtures and simulated uGUI input; no phone/human-play claim.
    [Category("Expansion07")]
    public sealed class DirectNavigationUiTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui=new ReelUiLifecycleTests();
        readonly JsonStateCodec codec=new JsonStateCodec();
        Component Game=>(Component)typeof(ReelUiLifecycleTests).GetField("game",Hidden).GetValue(ui);
        GameSession Session=>(GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        string Folder=>(string)typeof(ReelUiLifecycleTests).GetField("directory",Hidden).GetValue(ui);
        T Field<T>(string name)=>(T)Game.GetType().GetField(name,Hidden).GetValue(Game);
        IEnumerator Harness(string name,params object[] args)=>(IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(name,Hidden).Invoke(ui,args);
        void Call(string name,params object[] args)=>Game.GetType().GetMethod(name,Hidden|BindingFlags.Public).Invoke(Game,args);
        string Text(string field)=>Field<TMP_Text>(field).text;
        // Card captions carry rich-text styling; compare what the player reads.
        string Card(int index){var label=Field<UnityEngine.UI.Button[]>("navigationCards")[index].GetComponentInChildren<TMP_Text>(true);label.ForceMeshUpdate();return label.GetParsedText();}
        string CloseCaption=>Field<UnityEngine.UI.Button>("closeButton").GetComponentInChildren<TMP_Text>(true).text;
        Dictionary<string,string> Files()=>Directory.GetFiles(Folder).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
        void Unchanged(string state,Dictionary<string,string> files)
        {Assert.That(codec.Encode(Session.State),Is.EqualTo(state));Assert.That(Files(),Is.EquivalentTo(files));Assert.That(Session.RecoveryRequired,Is.False,Session.Error);}
        IEnumerator Click(string field,int index=-1){yield return Harness("PointerClick",field,index);yield return null;}
        IEnumerator Back()
        {
            yield return null;
            var back=Game.GetComponents<Component>().Single(c=>c.GetType().FullName=="JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)back.GetType().GetMethod("RequestBack").Invoke(back,null),Is.True);
            yield return null; // Newly shown Graphics join raycasting on the next rendered frame.
        }
        // All Sections is reached through Profile; its legacy root button is hidden by the product navigation.
        IEnumerator Open(int section){yield return Click("productAvatar");yield return Click("productCards",2);yield return Click("sectionButtons",section);yield return null;}
        IEnumerator OpenOrders()
        {
            yield return Click("productTabs",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));
            yield return Click("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
        }
        void Fits(TMP_Text text)
        {
            Canvas.ForceUpdateCanvases();text.ForceMeshUpdate();
            Assert.That(text.isTextOverflowing,Is.False,text.name+": "+text.text);
            Assert.That(text.textBounds.size.x,Is.LessThanOrEqualTo(text.rectTransform.rect.width+2),text.text);
            Assert.That(text.textBounds.size.y,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2),text.text);
        }
        void Layout(int visibleCards)
        {
            var root=Field<RectTransform>("navigationRoot");Assert.That(root.gameObject.activeInHierarchy,Is.True);
            var buttons=Field<UnityEngine.UI.Button[]>("navigationCards").Where(b=>b.gameObject.activeSelf).ToArray();
            Assert.That(buttons.Length,Is.EqualTo(visibleCards));Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>())Fits(text);
            var panel=(RectTransform)Field<GameObject>("panel").transform;
            var close=Field<UnityEngine.UI.Button>("closeButton");
            var occupied=new List<Bounds>{RectTransformUtility.CalculateRelativeRectTransformBounds(panel,close.transform),
                RectTransformUtility.CalculateRelativeRectTransformBounds(panel,Field<TMP_Text>("panelTitle").transform),
                RectTransformUtility.CalculateRelativeRectTransformBounds(panel,Field<TMP_Text>("navigationContext").transform)};
            var era=Field<UnityEngine.UI.Button>("navigationEraButton");
            if(era.gameObject.activeInHierarchy)
            {
                var rect=(RectTransform)era.transform;Assert.That(rect.rect.height,Is.GreaterThanOrEqualTo(48));Fits(era.GetComponentInChildren<TMP_Text>(true));
                occupied.Add(RectTransformUtility.CalculateRelativeRectTransformBounds(panel,rect));
            }
            foreach(var button in buttons)
            {
                var rect=(RectTransform)button.transform;Assert.That(rect.rect.width,Is.GreaterThanOrEqualTo(48));Assert.That(rect.rect.height,Is.GreaterThanOrEqualTo(48));
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(panel,button.transform);
                foreach(var prior in occupied)Assert.That(bounds.Intersects(prior),Is.False,"Overlapping navigation controls");occupied.Add(bounds);
                var corners=new Vector3[4];rect.GetWorldCorners(corners);
                foreach(var corner in corners){var p=panel.InverseTransformPoint(corner);Assert.That(panel.rect.Contains(new Vector2(p.x,p.y)),Is.True);}
                if(!button.IsInteractable())continue;
                var canvas=button.GetComponentInParent<Canvas>();
                foreach(var position in new[]{rect.rect.center,rect.rect.min+Vector2.one*6,rect.rect.max-Vector2.one*6})
                {
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(position))};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Assert.That(hits.Count,Is.GreaterThan(0));
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(button.gameObject),button.name+" must be topmost");
                }
            }
        }
        [SetUp]public void Isolate()
        {ui.IsolateSave();Assert.That(Path.GetPathRoot(Path.GetFullPath(Folder)).Replace('\\','/'),Is.EqualTo("X:/").IgnoreCase);}

        [UnityTest]public IEnumerator ExpiredListsNameVisibleReopenRoutesWithoutChangingProgress()
        {
            yield return Harness("Prepare",ModelState.Fresh(1));
            string state=codec.Encode(Session.State);var files=Files();
            foreach(var mode in new[]{
                new{tab=0,card=0,title="Buildings",route="District → Construction"},
                new{tab=4,card=3,title="Skills",route="Upgrades → Skills"},
                new{tab=2,card=2,title="Orders",route="Events → Neighbor Orders"}})
            {
                yield return Click("productTabs",mode.tab);yield return Click("productCards",mode.card);
                Assert.That(Field<RectTransform>("navigationRoot").gameObject.activeInHierarchy,Is.True);
                Assert.That(Text("panelTitle"),Does.StartWith(mode.title));
                var oldClick=Field<UnityEngine.UI.Button[]>("navigationCards")[0].onClick;
                Call("OnApplicationPause",true);Call("OnApplicationPause",false);yield return null;
                Assert.That(Field<bool>("navigationContextValid"),Is.False);
                Assert.That(Text("panelBody"),Is.EqualTo("Reopen this list. Press Close, then "+mode.route+"."));
                Fits(Field<TMP_Text>("panelBody"));oldClick.Invoke();Unchanged(state,files);
                yield return Click("closeButton");Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
                yield return Click("productTabs",mode.tab);yield return Click("productCards",mode.card);
                Assert.That(Field<RectTransform>("navigationRoot").gameObject.activeInHierarchy,Is.True);
                Assert.That(Field<bool>("navigationContextValid"),Is.True);
                Assert.That(Text("panelTitle"),Does.StartWith(mode.title));Unchanged(state,files);
                yield return Click("productTabs",1);Unchanged(state,files);
            }
        }
        [UnityTest]public IEnumerator AllSixFamiliesAndSkillsUseOneReadOnlyViewAndStaleCallbacksRefuse()
        {
            yield return Harness("Prepare",ModelState.Fresh(1));string state=codec.Encode(Session.State);var files=Files();
            yield return Open(2);Layout(6);Assert.That(CloseCaption,Is.EqualTo("Back"));
            Assert.That(Text("navigationContext"),Does.Contain("Wallet: 37").And.Contain("0/24").And.Contain("No goal selected"));
            string[] familyTitles={"House","Workshop","Recycler","Pigeon Loft","Cart","Power Source"};
            for(int i=0;i<6;i++)
            {
                Assert.That(Card(i),Does.StartWith(familyTitles[i]+" · 0/2"));
                Assert.That(Card(i),i==0?Does.Contain("Available · 40").And.Contain("Need 3"):Does.Contain("Locked · repair house"));
                yield return Click("navigationCards",i);Assert.That(Text("panelTitle"),Is.EqualTo(familyTitles[i]));Assert.That(CloseCaption,Is.EqualTo("Back"));
                Fits(Field<TMP_Text>("panelBody"));Unchanged(state,files);
                if(i%2==0)yield return Back();else yield return Click("closeButton");Layout(6);Unchanged(state,files);
            }
            var cards=Field<UnityEngine.UI.Button[]>("navigationCards");int children=Field<RectTransform>("navigationRoot").childCount;
            Call("CreateSectionsUi");Call("CreateSectionsUi");Assert.That(Field<RectTransform>("navigationRoot").childCount,Is.EqualTo(children));
            var held=new PointerEventData(EventSystem.current){pointerId=77,button=PointerEventData.InputButton.Left,eligibleForClick=true,
                pointerClick=cards[0].gameObject,pointerCurrentRaycast=new RaycastResult{gameObject=cards[0].gameObject}};
            ExecuteEvents.Execute(cards[0].gameObject,held,ExecuteEvents.pointerDownHandler);
            yield return Back();yield return Click("sectionButtons",3);
            ExecuteEvents.Execute(cards[0].gameObject,held,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(cards[0].gameObject,held,ExecuteEvents.pointerClickHandler);
            Assert.That(Text("panelTitle"),Is.EqualTo("Skills · optional"));Unchanged(state,files);
            yield return Click("navigationCards",0);Assert.That(Text("panelTitle"),Is.EqualTo("Skill · Coins 1"));
            yield return Back();yield return Back();yield return Click("sectionButtons",2);
            yield return Click("navigationEraButton");Assert.That(CloseCaption,Is.EqualTo("Back"));yield return Back();Layout(6);Unchanged(state,files);
            yield return Back();Assert.That(Text("panelTitle"),Is.EqualTo("All Sections"));
            cards[0].onClick.Invoke();Assert.That(Text("panelTitle"),Is.EqualTo("All Sections"));Unchanged(state,files);
            yield return Click("sectionButtons",3);Layout(6);
            string[] skillTitles={"Coins I","Coins II","Recycling I","Recycling II","Delivery I","Delivery II"};
            long[] prices={120,160,160,90,60,20};
            for(int i=0;i<6;i++)
            {
                Assert.That(Card(i),Does.StartWith(skillTitles[i]).And.Contain("Locked").And.Contain("Price: "+prices[i]));
                yield return Click("navigationCards",i);Assert.That(CloseCaption,Is.EqualTo("Back"));
                Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);Fits(Field<TMP_Text>("panelBody"));
                yield return Back();Layout(6);Unchanged(state,files);
            }
            Call("SelectJourneyPanel");Assert.That(Text("panelTitle"),Is.EqualTo("Skills · optional"));Unchanged(state,files);
            Call("OnApplicationPause",true);cards[0].onClick.Invoke();Assert.That(Field<RectTransform>("navigationRoot").gameObject.activeSelf,Is.False);Unchanged(state,files);
            Call("OnApplicationPause",false);cards[0].onClick.Invoke();Unchanged(state,files);
            foreach(var mode in new[]{new{section=3,title="Skills"},new{section=2,title="Buildings"},new{section=4,title="Orders"}})
            {
                if(mode.section!=3)
                {
                    yield return Open(mode.section);Layout(mode.section==4?4:6);
                    Assert.That(Field<bool>("navigationContextValid"),Is.True);
                    Assert.That(Field<RectTransform>("navigationRoot").gameObject.activeInHierarchy,Is.True);
                    if(mode.section==4)
                    {
                        Assert.That(Field<bool[]>("navigationCardAvailable").All(available=>!available),Is.True);
                        Assert.That(cards.All(button=>!button.interactable),Is.True);
                        for(int i=0;i<4;i++)Assert.That(Card(i),Does.Contain("Locked · requires 24/24"));
                        Unchanged(state,files); // Legitimate all-locked Orders must still have a visible valid list.
                    }
                    Call("OnApplicationPause",true);cards[0].onClick.Invoke();Unchanged(state,files);
                    Call("OnApplicationPause",false);cards[0].onClick.Invoke();Unchanged(state,files);
                }
                yield return null;Canvas.ForceUpdateCanvases();
                Assert.That(Field<GameObject>("panel").activeSelf,Is.True);
                Assert.That(Field<bool>("navigationContextValid"),Is.False);
                Assert.That(Field<RectTransform>("navigationRoot").gameObject.activeInHierarchy,Is.False);
                Assert.That(Field<bool[]>("navigationCardAvailable").All(available=>!available),Is.True);
                Assert.That(cards.All(button=>!button.interactable),Is.True);
                Assert.That(Field<UnityEngine.UI.Button>("navigationEraButton").interactable,Is.False);
                Assert.That(Text("panelTitle"),Is.EqualTo(mode.title));
                string route=mode.section==2?"District → Construction":mode.section==3?"Upgrades → Skills":"Events → Neighbor Orders";
                Assert.That(Text("panelBody"),Is.EqualTo("Reopen this list. Press Close, then "+route+"."));
                Assert.That(Field<TMP_Text>("panelTitle").isActiveAndEnabled&&Field<TMP_Text>("panelBody").isActiveAndEnabled,Is.True);
                Assert.That(CloseCaption,Is.EqualTo("Close"));Fits(Field<TMP_Text>("panelTitle"));Fits(Field<TMP_Text>("panelBody"));
                var close=Field<UnityEngine.UI.Button>("closeButton");Fits(close.GetComponentInChildren<TMP_Text>());
                Assert.That(close.isActiveAndEnabled&&close.IsInteractable(),Is.True);
                var closeRect=(RectTransform)close.transform;var r=closeRect.rect;var canvas=close.GetComponentInParent<Canvas>();
                Assert.That(r.width,Is.GreaterThanOrEqualTo(48));Assert.That(r.height,Is.GreaterThanOrEqualTo(48));
                foreach(var point in new[]{r.center,r.min+Vector2.one*4,r.max-Vector2.one*4,new Vector2(r.xMin+4,r.yMax-4),new Vector2(r.xMax-4,r.yMin+4)})
                {
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,closeRect.TransformPoint(point))};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Assert.That(hits.Count,Is.GreaterThan(0));
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(close.gameObject));
                }
                cards[0].onClick.Invoke();Unchanged(state,files);yield return Back();
                Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Unchanged(state,files);
            }
            yield return Click("spinButton");Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.True);
            state=codec.Encode(Session.State);files=Files();Call("ShowDirectSkills");Call("ShowDirectConstruction");cards[0].onClick.Invoke();
            Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Unchanged(state,files);yield return Harness("FinishPresentation");
        }

        [UnityTest]public IEnumerator Old600And540GoalsAreDistinctFrom480OfferAndReturnToTheirSixCardList()
        {
            var old=ModelState.Fresh(1);old.coins=535;old.houseStage=1;old.selectedStage=2;old.autoHintSeen=1;
            yield return Harness("Prepare",old);
            foreach(long saved in new[]{600L,540L})
            {
                if(saved==540)
                {
                    var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/Milestones/fixtures/v2-selected540.json"));
                    var fixture=codec.Decode(File.ReadAllText(path));fixture.lastSettledAt=1000;
                    File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(fixture));yield return Harness("ReenterScene");
                }
                string state=codec.Encode(Session.State);var files=Files();yield return Click("productTabs",0);yield return Click("productCards",0);Layout(6);
                Assert.That(Text("navigationContext"),Does.Contain("535/"+saved));
                Assert.That(Card(0),Does.Contain("Saved "+saved).And.Contain("Reselect: 480").And.Contain("need "+(saved-535)));
                yield return Click("navigationCards",0);Assert.That(Text("panelBody"),Does.Contain("Saved goal: "+saved+" Coins. Price: 480 Coins."));
                Assert.That(CloseCaption,Is.EqualTo("Back"));yield return Back();Layout(6);Unchanged(state,files);
                yield return Back();Assert.That(Text("panelTitle"),Is.EqualTo("My District"));yield return Back();Unchanged(state,files);
            }
        }

        ModelState CompleteBoundary()
        {return (ModelState)typeof(SectionsMapUiTests).GetMethod("StageFixture",Hidden).Invoke(new SectionsMapUiTests(),new object[]{EraIds.WorkshopYard,5});}
        void PostchapterSnapshot(string name,string provenance)
        {
            string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/postchapter-delivery-20261101/root","produced-"+name+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target,"jtn.save.v1.json"),codec.Encode(Session.State),new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"panel.txt"),Text("panelBody"),new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"navigation-context.txt"),Text("navigationContext"),new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"provenance.txt"),provenance+"; sequence="+Session.State.sequence+". Visible UI text was captured at this checkpoint, not recreated on reload.",new System.Text.UTF8Encoding(false));
            TestContext.WriteLine("Private produced postchapter snapshot: "+target);
        }
        [UnityTest]public IEnumerator PostchapterProgressShowsOnlyCurrentAcceptedOrderAndOpensReadOnlyList()
        {
            var fixture=(GameSession)typeof(CombinedLifecycleTests).GetMethod("Fixture",Hidden).Invoke(new CombinedLifecycleTests(),new object[]{CompleteBoundary()});
            var none=fixture.State;fixture.AcceptOrder(OrderIds.TrashSort,fixture.State.sequence+1,1000);
            for(int i=0;i<15;i++)fixture.ExecuteChapterSpin(fixture.State.sequence+1,1000);
            Assert.That(fixture.Journey.Orders.Progress,Is.EqualTo(11));var active=fixture.State;
            fixture.ExecuteChapterSpin(fixture.State.sequence+1,1000);Assert.That(fixture.Journey.Orders.Ready,Is.True);var ready=fixture.State;
            long instance=fixture.Journey.Orders.InstanceSequence;
            fixture.ClaimOrder(OrderIds.TrashSort,instance,fixture.State.sequence+1,1000);var claimed=fixture.State;
            Assert.That(fixture.Journey.Orders.ActiveOrderId,Is.Null);Assert.That(fixture.Journey.Orders.LatestClaim.RewardCoins,Is.EqualTo(120));
            foreach(var item in new[]{new{state=none,name="none",progress=""},new{state=active,name="active",progress="11/12"},
                new{state=ready,name="ready",progress="12/12"},new{state=claimed,name="claimed-no-active",progress=""}})
            {
                if(Game==null)yield return Harness("Prepare",item.state);
                else {var copy=item.state.Copy();copy.lastSettledAt=1000;File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(copy));Call("Reload");yield return null;}
                string before=codec.Encode(Session.State);var files=Files();
                Assert.That(Field<TMP_Text>("productGoalText").text,Does.Contain("24/24"));yield return Click("productGoalButton");
                Assert.That(Text("panelTitle"),Is.EqualTo("Progress"));Assert.That(Text("panelBody"),Does.Contain("24/24").And.Contain("You may finish here"));
                var next=Field<UnityEngine.UI.Button>("chapterNext");Assert.That(Field<UnityEngine.UI.Button>("chapterPrevious").gameObject.activeSelf,Is.False);
                Fits(Field<TMP_Text>("panelBody"));
                if(item.progress=="")
                {
                    Assert.That(next.gameObject.activeSelf,Is.False);Assert.That(Text("panelBody"),Does.Not.Contain("Accepted optional order"));
                    if(item.name=="claimed-no-active")Assert.That(Text("panelBody"),Does.Contain("order reward collected"));
                    next.onClick.Invoke();Unchanged(before,files);
                }
                else
                {
                    Assert.That(Text("panelBody"),Does.Contain("Accepted optional order").And.Contain("Sort Trash").And.Contain(item.progress));
                    if(item.name=="ready")Assert.That(Text("panelBody"),Does.Contain("Ready").And.Contain("120 Coins"));
                    Assert.That(next.isActiveAndEnabled&&next.IsInteractable(),Is.True);Assert.That(next.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo("Open orders"));
                    Fits(next.GetComponentInChildren<TMP_Text>(true));PostchapterSnapshot("progress-"+item.name,"Synthetic completed chapter; publicly accepted seed1 order and actual 15/16 setup spins; no claim via inspection");
                    yield return Click("chapterNext");Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));Layout(5);Unchanged(before,files);
                    Assert.That(Session.Journey.Orders.InstanceSequence,Is.EqualTo(instance));
                    yield return Click("navigationCards",0);Assert.That(Text("panelTitle"),Is.EqualTo("Sort Trash"));Unchanged(before,files);
                    yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));Unchanged(before,files);
                    yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));Unchanged(before,files);
                }
                Unchanged(before,files);
                if(Field<GameObject>("panel").activeSelf)yield return Back();Unchanged(before,files);
            }
            TestContext.WriteLine("Progress keeps 24/24 and ending sufficient; no-active/retained-claim do not invent an order; active11 and ready12 route through read-only list/detail then Events with unchanged instance, wallet, files and timestamps.");
        }
        [UnityTest]public IEnumerator ProgressOrderActionRefusesHiddenHeldStaleReplacementAndSessionChange()
        {
            var fixture=(GameSession)typeof(CombinedLifecycleTests).GetMethod("Fixture",Hidden).Invoke(new CombinedLifecycleTests(),new object[]{CompleteBoundary()});
            fixture.AcceptOrder(OrderIds.TrashSort,fixture.State.sequence+1,1000);yield return Harness("Prepare",fixture.State);
            yield return Click("productGoalButton");var next=Field<UnityEngine.UI.Button>("chapterNext");var old=next.onClick;
            var held=new PointerEventData(EventSystem.current){pointerId=1901,button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerClick=next.gameObject,pointerCurrentRaycast=new RaycastResult{gameObject=next.gameObject}};
            ExecuteEvents.Execute(next.gameObject,held,ExecuteEvents.pointerDownHandler);
            long first=Session.Journey.Orders.InstanceSequence;
            Session.CancelOrder(OrderIds.TrashSort,first,Session.State.sequence+1,1000);
            Session.AcceptOrder(OrderIds.GatherPotatoes,Session.State.sequence+1,1000);
            Assert.That(Session.Journey.Orders.InstanceSequence,Is.GreaterThan(first));string replacement=codec.Encode(Session.State);var files=Files();
            ExecuteEvents.Execute(next.gameObject,held,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(next.gameObject,held,ExecuteEvents.pointerClickHandler);
            old.Invoke();Assert.That(Field<int>("wavePanel"),Is.Not.EqualTo(32));Unchanged(replacement,files);
            Call("SetProgressGoalAction",false,MilestoneNextAction.FinishOrContinueOptionally);
            Assert.That(next.gameObject.activeSelf,Is.False);old.Invoke();Assert.That(Field<int>("wavePanel"),Is.Not.EqualTo(32));Unchanged(replacement,files);
            Call("Reload");yield return null;Unchanged(replacement,files);yield return Click("productGoalButton");
            Assert.That(next.isActiveAndEnabled,Is.True);Assert.That(Text("panelBody"),Does.Contain("Gather Potatoes"));
            old.Invoke();Assert.That(Field<int>("wavePanel"),Is.Not.EqualTo(32));Unchanged(replacement,files);
            var current=next.onClick;Call("OnApplicationPause",true);Assert.That(next.gameObject.activeSelf,Is.False);current.Invoke();Unchanged(replacement,files);
            Call("OnApplicationPause",false);current.Invoke();Assert.That(Field<int>("wavePanel"),Is.Not.EqualTo(32));Unchanged(replacement,files);
            TestContext.WriteLine("Old held contact and captured event refuse a separately cancelled/replaced instance; explicit default hide, Reload/new session and pause invalidate the action without navigation or writes.");
        }
        [UnityTest]public IEnumerator RecoveredPendingClaimDoesNotOpenOrReplayFreshPaidResult()
        {
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/OptionalOrderStorage/fixtures/ready-sort-no-claim.json"));
            var ready=codec.Decode(File.ReadAllText(path));ready.lastSettledAt=1000;var orders=JourneyEconomy.Project(ready).Orders;
            Assert.That(ready.coins,Is.EqualTo(1062));Assert.That(orders.Ready,Is.True);Assert.That(orders.LatestClaim,Is.Null);
            long instance=orders.InstanceSequence,sequence=ready.sequence+1;
            var pending=JourneyEconomy.Reserve(ready,"order-claim",OrderIds.TrashSort,instance,sequence,0,1000);
            Assert.That(pending.pending,Is.Not.Null);yield return Harness("Prepare",pending);
            Assert.That(Session.State.pending,Is.Null);Assert.That(Session.State.coins,Is.EqualTo(1182));Assert.That(Session.State.sequence,Is.EqualTo(sequence));
            Assert.That(Session.Journey.Orders.LatestClaim.InstanceSequence,Is.EqualTo(instance));Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);
            Assert.That(Field<GameObject>("panel").activeSelf,Is.False,"Recovery must not present a fresh-claim continuation");
            string recovered=codec.Encode(Session.State);var files=Files();Session.ClaimOrder(OrderIds.TrashSort,instance,sequence);Unchanged(recovered,files);
            yield return Click("productTabs",2);yield return Click("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
            Assert.That(Text("navigationContext"),Does.Not.Contain(ChapterNarrative.OrderClaimed(OrderIds.TrashSort)));
            Unchanged(recovered,files);yield return Back();Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));Unchanged(recovered,files);
            TestContext.WriteLine("Tracked ready fixture -> exact pending claim reservation; session recovery applies 120 once but does not show fresh-claim list or reaction; old claim replay and normal Events inspection write nothing.");
        }
        [UnityTest]public IEnumerator FourOrderCardsKeepExplicitAcceptCancelAndChapterEndReadOnly()
        {
            void CancellationView(string body,long instance)
            {
                Assert.That(Field<int>("wavePanel"),Is.EqualTo(12));Assert.That(Text("panelTitle"),Is.EqualTo("Cancel order?"));
                Assert.That(Field<string>("shownOrder"),Is.EqualTo(OrderIds.GatherPotatoes));
                Assert.That(Field<long>("shownOrderInstance"),Is.EqualTo(instance));
                Assert.That(Field<long>("journeyQuoteSequence"),Is.EqualTo(Session.State.sequence+1));
                Assert.That(Text("panelBody"),Is.EqualTo(body));Fits(Field<TMP_Text>("panelTitle"));Fits(Field<TMP_Text>("panelBody"));
                foreach(var button in new[]{Field<UnityEngine.UI.Button>("selectButton"),Field<UnityEngine.UI.Button>("closeButton")})
                {
                    Assert.That(button.isActiveAndEnabled&&button.IsInteractable(),Is.True,button.name);
                    Fits(button.GetComponentInChildren<TMP_Text>());var rect=(RectTransform)button.transform;var r=rect.rect;
                    Assert.That(r.width,Is.GreaterThanOrEqualTo(48));Assert.That(r.height,Is.GreaterThanOrEqualTo(48));
                    var canvas=button.GetComponentInParent<Canvas>();
                    foreach(var p in new[]{r.center,r.min+Vector2.one*4,r.max-Vector2.one*4,new Vector2(r.xMin+4,r.yMax-4),new Vector2(r.xMax-4,r.yMin+4)})
                    {
                        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(p))};
                        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Assert.That(hits.Count,Is.GreaterThan(0));
                        Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(button.gameObject),button.name);
                    }
                }
            }
            Assert.That(OrderCatalog.Choices.Select(o=>o.Id),Is.EquivalentTo(new[]{"order.sort-salvage","order.neighborhood-supply","order.gather-potatoes","order.pack-boxes"}),"Integrate03 Core definitions before this UI check.");
            yield return Harness("Prepare",CompleteBoundary());string state=codec.Encode(Session.State);var files=Files();
            yield return OpenOrders();Layout(5);
            string[] names={"Sort Trash","Neighborhood Delivery","Gather Potatoes","Pack Boxes"};
            for(int i=0;i<4;i++)
            {
                var cardLabel=Field<UnityEngine.UI.Button[]>("navigationCards")[i].GetComponentInChildren<TMP_Text>(true);
                cardLabel.ForceMeshUpdate();Assert.That(cardLabel.GetParsedText(),Does.StartWith(names[i]).And.Contain("Available · accept explicitly"));
                yield return Click("navigationCards",i);Assert.That(Text("panelTitle"),Is.EqualTo(names[i]));Assert.That(CloseCaption,Is.EqualTo("Back"));
                Assert.That(Text("panelBody"),Does.Contain(i==1?"3 completed deliveries of 3 Pigeons":i==2?"12 Potatoes appearances":i==3?"12 Boxes appearances":"12 Trash appearances"));
                Fits(Field<TMP_Text>("panelBody"));yield return Back();Unchanged(state,files);
            }
            yield return Click("navigationCards",4);Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);
            Assert.That(Text("panelBody"),Does.Contain("You may finish here"));yield return Back();Unchanged(state,files);
            yield return Click("navigationCards",2);long wallet=Session.State.coins;yield return Click("selectButton");
            Assert.That(Session.Journey.Orders.ActiveOrderId,Is.EqualTo("order.gather-potatoes"));Assert.That(Session.State.coins,Is.EqualTo(wallet));
            yield return OpenOrders();Assert.That(Card(2),Does.Contain("In progress · 0/12"));Assert.That(Card(3),Does.Contain("Busy · inspect"));
            state=codec.Encode(Session.State);files=Files();yield return Click("navigationCards",3);
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);yield return Back();Unchanged(state,files);
            long activeInstance=Session.Journey.Orders.InstanceSequence;uint seed=Session.State.seed;
            yield return Click("navigationCards",3);Assert.That(Text("panelTitle"),Is.EqualTo(names[3]));
            Assert.That(Field<string>("shownOrder"),Is.EqualTo(OrderIds.PackBoxes));
            Assert.That(Field<UnityEngine.UI.Button>("chapterPrevious").GetComponentInChildren<TMP_Text>().text,Is.EqualTo("Cancel current"));
            yield return Click("chapterPrevious");
            CancellationView("Current order: Gather Potatoes.\nProgress: 0/12 Potatoes.\nProgress will be lost. Canceling gives no reward.\nYou may start a new instance from zero later.",activeInstance);
            Assert.That(CloseCaption,Is.EqualTo("Back"));Unchanged(state,files);
            yield return Back();Layout(5);Unchanged(state,files);
            yield return Click("navigationCards",2);yield return Click("chapterPrevious");Assert.That(Text("panelTitle"),Is.EqualTo("Cancel order?"));
            Assert.That(CloseCaption,Is.EqualTo("Back"));yield return Back();Layout(5);Unchanged(state,files);
            long cancelSequence=Session.State.sequence+1;var beforeCancel=Session.State.Copy();string guide=GuideStateCodec.Encode(Session.Journey.Guidance);
            yield return Click("navigationCards",2);yield return Click("chapterPrevious");yield return Click("selectButton");
            Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);Assert.That(Session.State.coins,Is.EqualTo(wallet));Assert.That(Session.Journey.Orders.LatestClaim,Is.Null);
            Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.sequence,Is.EqualTo(cancelSequence));
            Assert.That(Session.Journey.CommandKind,Is.EqualTo("order-cancel"));Assert.That(Session.Journey.CommandId,Is.EqualTo(OrderIds.GatherPotatoes));
            Assert.That(Session.Journey.CommandInstance,Is.EqualTo(activeInstance));Assert.That(Session.Journey.CommandSequence,Is.EqualTo(cancelSequence));
            var afterCancel=Session.State.Copy();beforeCancel.journey=null;afterCancel.journey=null;
            afterCancel.sequence=beforeCancel.sequence;afterCancel.revision=beforeCancel.revision;
            Assert.That(codec.Encode(afterCancel),Is.EqualTo(codec.Encode(beforeCancel)));Assert.That(GuideStateCodec.Encode(Session.Journey.Guidance),Is.EqualTo(guide));

            // Reuse the exact tracked public-command checkpoint; no new ready
            // progress, ownership, wallet or receipt is fabricated for this read.
            var ready=codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/OrderVariety/fixtures/potatoes-ready12.json"))));
            ready.lastSettledAt=1000;File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(ready));Call("Reload");yield return null;
            Assert.That(Session.Journey.Orders.Ready,Is.True);Assert.That(Session.Journey.Orders.Progress,Is.EqualTo(12));
            Assert.That(Session.Journey.Orders.LatestClaim,Is.Null);Assert.That(Session.State.coins,Is.EqualTo(690));
            state=codec.Encode(Session.State);files=Files();activeInstance=Session.Journey.Orders.InstanceSequence;
            yield return OpenOrders();yield return Click("navigationCards",3);Assert.That(Text("panelTitle"),Is.EqualTo(names[3]));
            yield return Click("chapterPrevious");
            CancellationView("Current order: Gather Potatoes.\nProgress: 12/12 Potatoes.\nUnclaimed reward: 120 Coins. Canceling forfeits it. Wallet Coins are not spent.\nYou may start a new instance from zero later.",activeInstance);
            Unchanged(state,files);yield return Back();Layout(5);Assert.That(Card(2),Does.Contain("Ready · collect explicitly"));Unchanged(state,files);
        }
        [UnityTest]public IEnumerator NewSymbolOrdersClaim120OnceAndNeverReplayTheirStoryOnInspection()
        {
            foreach(var pair in new[]{new{file="potatoes-ready12.json",id=OrderIds.GatherPotatoes,index=2},new{file="boxes-ready12.json",id=OrderIds.PackBoxes,index=3}})
            {
                var fixture=codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/OrderVariety/fixtures/"+pair.file))));
                if(Game==null)yield return Harness("Prepare",fixture);
                else
                {
                    fixture.lastSettledAt=1000;
                    File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(fixture));Call("Reload");yield return null;
                }
                Assert.That(Session.State.coins,Is.EqualTo(690));Assert.That(Session.Journey.Orders.Ready,Is.True);
                string before=codec.Encode(Session.State);var files=Files();
                yield return Click("productTabs",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(71));Unchanged(before,files);
                yield return Click("productCards",2);Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));Unchanged(before,files);
                Assert.That(Card(pair.index),Does.Contain("120"));
                yield return Click("navigationCards",pair.index);Assert.That(Text("panelBody"),Does.Contain(ChapterNarrative.OrderReady(pair.id)));
                yield return Back();Unchanged(before,files);yield return Click("navigationCards",pair.index);
                long instance=Session.Journey.Orders.InstanceSequence;var oldClaim=Field<UnityEngine.UI.Button>("selectButton").onClick;yield return Click("selectButton");
                Assert.That(Session.State.coins,Is.EqualTo(810));Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
                Assert.That(Field<TMP_Text>("navigationContext").isActiveAndEnabled,Is.True);
                Assert.That(Text("navigationContext"),Does.Contain("+120 Coins").And.Contain(ChapterNarrative.OrderClaimed(pair.id)));Layout(5);
                long sequence=Session.State.sequence;string claimed=codec.Encode(Session.State);files=Files();
                Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);Assert.That(Session.Journey.Orders.LatestClaim.InstanceSequence,Is.EqualTo(instance));
                PostchapterSnapshot("paid-"+pair.id,"Actual UI Claim from tracked ready fixture; paid120 once, result is visible on Orders list; no next acceptance");
                oldClaim.Invoke();Field<UnityEngine.UI.Button>("selectButton").onClick.Invoke();Unchanged(claimed,files);Assert.That(Field<int>("wavePanel"),Is.EqualTo(32));
                yield return Click("navigationCards",pair.index);
                Assert.That(Text("panelBody"),Does.Contain("Previous order already paid 120 Coins.").And.Not.Contain(ChapterNarrative.OrderClaimed(pair.id)));
                Unchanged(claimed,files);yield return Click("selectButton");
                Assert.That(Session.Journey.Orders.Progress,Is.Zero);Assert.That(Session.Journey.Orders.InstanceSequence,Is.GreaterThan(instance));
                string active=codec.Encode(Session.State);files=Files();Session.ClaimOrder(pair.id,instance,sequence);Unchanged(active,files);
                Assert.That(Session.State.coins,Is.EqualTo(810));oldClaim.Invoke();Unchanged(active,files);
            }
        }
        [UnityTest]public IEnumerator CapturedAcceptEventCannotConfirmCurrentCancellationOrCancelANewerInstance()
        {
            RaycastResult Hit(UnityEngine.UI.Button button,Vector2 point)
            {
                Canvas.ForceUpdateCanvases();var canvas=button.GetComponentInParent<Canvas>();
                var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,button.transform.TransformPoint(point))};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                Assert.That(hits.Count,Is.GreaterThan(0),button.name);
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(button.gameObject),button.name);
                return hits[0];
            }
            void Target(UnityEngine.UI.Button button)
            {
                Assert.That(button.isActiveAndEnabled&&button.IsInteractable(),Is.True,button.name);
                var r=((RectTransform)button.transform).rect;
                Assert.That(r.width,Is.GreaterThanOrEqualTo(48));Assert.That(r.height,Is.GreaterThanOrEqualTo(48));
                foreach(var point in new[]{r.center,r.min+Vector2.one*4,r.max-Vector2.one*4,
                    new Vector2(r.xMin+4,r.yMax-4),new Vector2(r.xMax-4,r.yMin+4)})Hit(button,point);
                Fits(button.GetComponentInChildren<TMP_Text>(true));
            }
            PointerEventData Press(UnityEngine.UI.Button button,int id)
            {
                Target(button);var hit=Hit(button,((RectTransform)button.transform).rect.center);
                var data=new PointerEventData(EventSystem.current){pointerId=id,button=PointerEventData.InputButton.Left,
                    eligibleForClick=true,pointerCurrentRaycast=hit,position=hit.screenPosition};
                ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);
                data.pointerPress=button.gameObject;data.pointerClick=button.gameObject;return data;
            }
            void Release(PointerEventData data,UnityEngine.UI.Button current)
            {
                Target(current);data.pointerCurrentRaycast=Hit(current,((RectTransform)current.transform).rect.center);
                data.position=data.pointerCurrentRaycast.screenPosition;
                bool candidate=data.eligibleForClick&&data.pointerClick==ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject);
                ExecuteEvents.Execute(data.pointerPress,data,ExecuteEvents.pointerUpHandler);
                if(candidate)ExecuteEvents.Execute(data.pointerClick,data,ExecuteEvents.pointerClickHandler);
            }
            IEnumerator Fresh(UnityEngine.UI.Button button,int id)
            {
                yield return null;int frame=Time.frameCount;Release(Press(button,id),button);yield return null;
                Assert.That(Time.frameCount,Is.GreaterThan(frame));
            }
            var completed=(GameSession)typeof(ReelUiLifecycleTests).GetMethod("CompletedFixture",Hidden).Invoke(ui,null);
            yield return Harness("Prepare",completed.State);
            Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);Assert.That(Session.Journey.Orders.LatestClaim,Is.Null);
            var economic=Session.State.Copy();economic.journey=null;string guide=GuideStateCodec.Encode(Session.Journey.Guidance);
            long start=Session.State.sequence;const string order=OrderIds.GatherPotatoes;
            void EconomyUnchanged()
            {
                var current=Session.State.Copy();current.journey=null;current.sequence=economic.sequence;current.revision=economic.revision;
                Assert.That(codec.Encode(current),Is.EqualTo(codec.Encode(economic)),"Only exact Journey command metadata may change");
                Assert.That(GuideStateCodec.Encode(Session.Journey.Guidance),Is.EqualTo(guide));
                Assert.That(Session.State.coins,Is.EqualTo(690));Assert.That(Session.State.seed,Is.EqualTo(1u));
                Assert.That(Session.State.pending,Is.Null);Assert.That(Session.State.lastSpin,Is.Null);
                Assert.That(Session.Journey.Orders.LatestClaim,Is.Null);Assert.That(Session.RecoveryRequired,Is.False,Session.Error);
            }
            void Cancellation(long instance)
            {
                Assert.That(Field<int>("wavePanel"),Is.EqualTo(12));Assert.That(Text("panelTitle"),Is.EqualTo("Cancel order?"));
                Assert.That(Field<string>("shownOrder"),Is.EqualTo(order));Assert.That(Field<long>("shownOrderInstance"),Is.EqualTo(instance));
                Assert.That(Field<long>("journeyQuoteSequence"),Is.EqualTo(Session.State.sequence+1));
                Assert.That(Session.Journey.Orders.ActiveOrderId,Is.EqualTo(order));Assert.That(Session.Journey.Orders.InstanceSequence,Is.EqualTo(instance));
                Assert.That(Field<UnityEngine.UI.Button>("selectButton").GetComponentInChildren<TMP_Text>().text,Is.EqualTo("Cancel without reward"));
                Fits(Field<TMP_Text>("panelBody"));Target(Field<UnityEngine.UI.Button>("selectButton"));EconomyUnchanged();
            }
            void Cancelled(long instance,long sequence)
            {
                Assert.That(Session.State.sequence,Is.EqualTo(sequence));Assert.That(Session.State.revision,Is.EqualTo(sequence*2));
                Assert.That(Session.Journey.CommandKind,Is.EqualTo("order-cancel"));Assert.That(Session.Journey.CommandId,Is.EqualTo(order));
                Assert.That(Session.Journey.CommandInstance,Is.EqualTo(instance));Assert.That(Session.Journey.CommandSequence,Is.EqualTo(sequence));
                Assert.That(Session.Journey.Orders.ActiveOrderId,Is.Null);Assert.That(Session.Journey.Orders.Progress,Is.Zero);
                Assert.That(Session.Journey.Orders.InstanceSequence,Is.EqualTo(instance));Assert.That(Session.Journey.Orders.Cursor,Is.EqualTo(sequence));
                Assert.That(Session.Journey.Orders.LastCommandSequence,Is.EqualTo(sequence));Assert.That(Session.Journey.Orders.LastOperationKey,Has.Length.EqualTo(64));
                Assert.That(Field<GameObject>("panel").activeSelf,Is.False);EconomyUnchanged();
            }
            yield return OpenOrders();yield return Click("navigationCards",2);
            var select=Field<UnityEngine.UI.Button>("selectButton");Target(select);
            Assert.That(select.GetComponentInChildren<TMP_Text>().text,Is.EqualTo("Accept order"));
            var oldAcceptEvent=select.onClick;var heldAccept=Press(select,1201);
            yield return Fresh(select,1202);long first=Session.Journey.Orders.InstanceSequence;
            Assert.That(first,Is.EqualTo(start+1));Assert.That(Session.Journey.Orders.ActiveOrderId,Is.EqualTo(order));EconomyUnchanged();
            yield return OpenOrders();yield return Click("navigationCards",2);yield return Click("chapterPrevious");Cancellation(first);
            string before=codec.Encode(Session.State);var files=Files();
            Release(heldAccept,select);Unchanged(before,files); // The invalid held release must be vetoed independently.
            TestContext.WriteLine("Old held Accept release refused0IO; separately invoking its captured Accept event on actionable Cancel now.");
            oldAcceptEvent.Invoke();Unchanged(before,files);Cancellation(first); // A separate invocation cannot borrow current Cancel authority.
            var oldCancelEvent=select.onClick;var heldCancel=Press(select,1203);
            yield return Fresh(select,1204);Cancelled(first,start+2);
            before=codec.Encode(Session.State);files=Files();oldAcceptEvent.Invoke();oldCancelEvent.Invoke();Unchanged(before,files);

            yield return OpenOrders();yield return Click("navigationCards",2);yield return Fresh(select,1205);
            long second=Session.Journey.Orders.InstanceSequence;Assert.That(second,Is.EqualTo(start+3));Assert.That(second,Is.GreaterThan(first));
            Assert.That(Session.Journey.Orders.Progress,Is.Zero);EconomyUnchanged();
            yield return OpenOrders();yield return Click("navigationCards",2);yield return Click("chapterPrevious");Cancellation(second);
            before=codec.Encode(Session.State);files=Files();
            Release(heldCancel,select);Unchanged(before,files);
            oldAcceptEvent.Invoke();Unchanged(before,files);oldCancelEvent.Invoke();Unchanged(before,files);Cancellation(second);
            Target(select);int submitFrame=Time.frameCount;
            ExecuteEvents.Execute(select.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            yield return null;Assert.That(Time.frameCount,Is.GreaterThan(submitFrame));Cancelled(second,start+4);
            before=codec.Encode(Session.State);files=Files();oldAcceptEvent.Invoke();oldCancelEvent.Invoke();Unchanged(before,files);
            TestContext.WriteLine("Existing CompletedFixture: declared initial10000Coins then public24construction/era/hint commands ->690Coins, no new ownership/economy setter or Spin. Old held release and old captured Accept are separate0IO checks on live Cancel; fresh pointer cancels first instance exactly once, old Accept/Cancel/held cannot cancel new instance, fresh Submit cancels second once; wallet690/seed1/construction/pricing/guidance unchanged, no reward/claim.");
        }
        [UnityTearDown]public IEnumerator Restore()
        {
            if(TestContext.CurrentContext.Result.Outcome.Status==NUnit.Framework.Interfaces.TestStatus.Failed)
            {TestContext.WriteLine("Preserved DirectNavigation private fixture: "+Folder);typeof(ReelUiLifecycleTests).GetField("directory",Hidden).SetValue(ui,null);}
            yield return ui.RestoreAndRemoveOnlyPrivateFixtures();
        }
    }
}
#endif
