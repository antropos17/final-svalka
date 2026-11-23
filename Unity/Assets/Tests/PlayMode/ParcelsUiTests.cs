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
    public sealed class ParcelsUiTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui=new ReelUiLifecycleTests();readonly JsonStateCodec codec=new JsonStateCodec();int? originalProfile;
        Component Game=>(Component)typeof(ReelUiLifecycleTests).GetField("game",Hidden).GetValue(ui);
        GameSession Session=>(GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        string Folder=>(string)typeof(ReelUiLifecycleTests).GetField("directory",Hidden).GetValue(ui);
        T Field<T>(string n)=>(T)Game.GetType().GetField(n,Hidden).GetValue(Game);
        IEnumerator Harness(string n,params object[] a)=>(IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(n,Hidden).Invoke(ui,a);
        void Call(string n,params object[] a)=>Game.GetType().GetMethod(n,Hidden|BindingFlags.Public).Invoke(Game,a);
        string State=>codec.Encode(Session.State);
        Dictionary<string,string> Files()=>Directory.GetFiles(Folder).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
        void Same(string state,Dictionary<string,string> files){Assert.That(State,Is.EqualTo(state));Assert.That(Files(),Is.EquivalentTo(files));}
        PointerEventData Down(Button b,int id)
        {
            Assert.That(b.isActiveAndEnabled&&b.IsInteractable(),Is.True,b.name);Canvas.ForceUpdateCanvases();var rect=(RectTransform)b.transform;
            var data=new PointerEventData(EventSystem.current){pointerId=id,button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerClick=b.gameObject,pointerPress=b.gameObject,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Assert.That(hits.Count,Is.GreaterThan(0),b.name);Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(b.gameObject),b.name);data.pointerCurrentRaycast=hits[0];ExecuteEvents.Execute(b.gameObject,data,ExecuteEvents.pointerDownHandler);return data;
        }
        void Up(Button b,PointerEventData p){ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);}
        IEnumerator Click(string n,int i=-1){yield return null;var b=i<0?Field<Button>(n):Field<Button[]>(n)[i];Up(b,Down(b,810));yield return null;}
        ModelState Fixture(bool loft=true,int spins=24)
        {
            var s=(ModelState)typeof(CombinedLifecycleTests).GetMethod("MilestoneEraFixture",Hidden).Invoke(new CombinedLifecycleTests(),new object[]{EraIds.Junkyard,new[]{1,1,1,loft?1:0,1,1}});
            for(int i=0;i<spins;i++)s=GameLogic.ApplyPending(GameLogic.Reserve(s,"spin",0,s.sequence+1,1000));return s;
        }
        IEnumerator Open(){yield return Click("productTabs",2);yield return Click("productCards",5);}
        IEnumerator Start(){yield return Open();yield return Click("arcadePrimaryButton");Assert.That(Session.Parcels.ActiveRun,Is.Not.Null);}
        IEnumerator Place(int token,int slot){yield return Click("parcelsTokenButtons",token);yield return Click("parcelsSlotButtons",slot);}
        IEnumerator Layout(int[] slots){for(int i=0;i<4;i++)yield return Place(slots[i],i);}
        void Slots(params int[] expected)=>Assert.That(Session.Parcels.ActiveRun.Slots,Is.EqualTo(expected));
        void NoFlights(){Assert.That((int)Game.GetType().GetProperty("RewardFeedbackVisibleCount").GetValue(Game),Is.Zero);}
        [SetUp]public void Isolate(){ui.IsolateSave();Assert.That(Path.GetPathRoot(Folder).Replace('\\','/'),Is.EqualTo("X:/").IgnoreCase);}
        IEnumerator InspectSavedHud(string expectedMessage,string expectedBadge)
        {
            string state=State;var files=Files();yield return Click("productTabs",1);
            Assert.That(Field<TMP_Text>("message").text,Does.Contain(expectedMessage));
            Assert.That(Field<TMP_Text>("productEventBadge").text,Does.Contain(expectedBadge));Same(state,files);
            yield return Open();Same(state,files);
        }
        [UnityTest]public IEnumerator ParcelsVisibleStartTrapRepairFinishClaimAndOldEvents()
        {
            yield return Harness("Prepare",Fixture());Call("SetRewardFeedbackReduced",false);int tickets=Session.Arcade.Tickets;long diamonds=Session.Arcade.Diamonds;string state=State;var files=Files();yield return Open();Same(state,files);
            var start=Field<Button>("arcadePrimaryButton").onClick;yield return Click("arcadePrimaryButton");Assert.That(Session.Arcade.Tickets,Is.EqualTo(tickets-1));Assert.That(Session.Parcels.ActiveRun.BoardId,Is.EqualTo("parcels.box-priority"));
            yield return InspectSavedHud("Round saved · Events","ROUND SAVED");
            state=State;files=Files();start.Invoke();Same(state,files);
            yield return Place(0,0);state=State;files=Files();yield return Place(0,0);Same(state,files);
            yield return Click("parcelsSlotButtons",0);Slots(-1,-1,-1,-1);yield return Place(0,0);
            yield return Place(1,1);Slots(0,1,-1,-1);Assert.That(Session.Parcels.ActiveRun.RequestASatisfied,Is.True);
            yield return Place(2,2);yield return Place(3,3);Assert.That(Session.Parcels.ActiveRun.SatisfiedRequests,Is.EqualTo(1));
            yield return Place(3,1);Slots(0,3,2,-1);yield return Place(1,3);Assert.That(Session.Parcels.ActiveRun.SatisfiedRequests,Is.EqualTo(2));Assert.That(Session.Parcels.ActiveRun.PredictedReward,Is.EqualTo(3));
            var finish=Field<Button>("arcadePrimaryButton").onClick;ExecuteEvents.Execute(Field<Button>("arcadePrimaryButton").gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);yield return null;
            Assert.That(Session.Parcels.ActiveRun.Ready,Is.True);Assert.That(Session.Arcade.Diamonds,Is.EqualTo(diamonds));NoFlights();
            yield return InspectSavedHud("Ready to claim: 3 · Events","CLAIM 3 DIAM.");
            state=State;files=Files();finish.Invoke();Same(state,files);var claim=Field<Button>("arcadePrimaryButton").onClick;yield return Click("arcadePrimaryButton");Assert.That(Session.Arcade.Diamonds,Is.EqualTo(diamonds+3));Assert.That(Session.Parcels.ActiveRun,Is.Null);
            var flyers=Field<TMP_Text[]>("rewardFeedbackLabels").Where(t=>t.gameObject.activeInHierarchy).ToArray();Assert.That(flyers.Length,Is.EqualTo(1));Assert.That(flyers[0].text,Is.EqualTo("Diamonds +3"));int nextFlyer=Field<int>("rewardFeedbackNext");
            state=State;files=Files();claim.Invoke();finish.Invoke();start.Invoke();Same(state,files);Assert.That(Field<int>("rewardFeedbackNext"),Is.EqualTo(nextFlyer));
            yield return Harness("ReenterScene");NoFlights();
            yield return Open();yield return Click("arcadePrimaryButton");state=State;files=Files();claim.Invoke();Same(state,files);Assert.That(Session.Parcels.ActiveRun.BoardId,Is.EqualTo("parcels.matching-pair"));
        }
        [UnityTest]public IEnumerator ParcelsPartialCancelColdConfirmationAndZeroRewardAreExplicit()
        {
            yield return Harness("Prepare",Fixture());yield return Start();yield return Layout(new[]{0,1,2,3});long diamonds=Session.Arcade.Diamonds;
            string state=State;var files=Files();yield return Click("arcadePrimaryButton");Assert.That(Session.Parcels.ActiveRun.Ready,Is.False);Same(state,files);
            yield return Click("arcadeSecondaryButton");Same(state,files);Slots(0,1,2,3);
            yield return Click("arcadePrimaryButton");var oldConfirm=Field<Button>("arcadePrimaryButton").onClick;
            yield return Harness("ReenterScene");yield return Open();Assert.That(Session.Parcels.ActiveRun.Ready,Is.False);Slots(0,1,2,3);state=State;files=Files();oldConfirm.Invoke();Same(state,files);
            yield return Click("arcadePrimaryButton");yield return Click("arcadePrimaryButton");Assert.That(Session.Parcels.ActiveRun.ReadyReward,Is.EqualTo(1));Assert.That(Session.Arcade.Diamonds,Is.EqualTo(diamonds));
            yield return Click("arcadePrimaryButton");Assert.That(Session.Arcade.Diamonds,Is.EqualTo(diamonds+1));
            // A fresh private first-board instance independently exercises the0whole-round path.
            File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(Fixture()));yield return Harness("ReenterScene");yield return Start();yield return Layout(new[]{1,2,3,4});
            Assert.That(Session.Parcels.ActiveRun.SatisfiedRequests,Is.Zero);state=State;files=Files();yield return Click("arcadePrimaryButton");Same(state,files);yield return Click("arcadePrimaryButton");
            Assert.That(Session.Parcels.ActiveRun.ReadyReward,Is.Zero);Assert.That(Session.Arcade.Diamonds,Is.Zero);NoFlights();
            yield return InspectSavedHud("Ready: 0 · finish · Events","FINISH · 0");
            yield return Click("arcadePrimaryButton");Assert.That(Session.Parcels.ActiveRun,Is.Null);Assert.That(Session.Arcade.Diamonds,Is.Zero);NoFlights();
        }
        [UnityTest]public IEnumerator ParcelsHeldSelectionPauseAndSessionReplacementCannotPlaceOrFinish()
        {
            yield return Harness("Prepare",Fixture());yield return Start();yield return Place(0,0);yield return Place(1,1);yield return Place(2,2);yield return Place(3,3);
            var slot=Field<Button[]>("parcelsSlotButtons")[0];var held=Down(slot,811);var old=slot.onClick;yield return Click("parcelsTokenButtons",4);
            string state=State;var files=Files();Up(slot,held);old.Invoke();Same(state,files);Assert.That(Field<int>("parcelsSelectedToken"),Is.EqualTo(4));
            yield return Click("arcadePrimaryButton");var confirm=Field<Button>("arcadePrimaryButton");held=Down(confirm,812);var oldConfirm=confirm.onClick;
            Call("OnApplicationPause",true);Call("OnApplicationPause",false);yield return null;state=State;files=Files();Up(confirm,held);oldConfirm.Invoke();Same(state,files);Assert.That(Session.Parcels.ActiveRun.Ready,Is.False);
            yield return Harness("ReenterScene");yield return Open();Assert.That(Field<int>("parcelsSelectedToken"),Is.EqualTo(-1));Slots(0,1,2,3);
            yield return Click("arcadePrimaryButton");oldConfirm=Field<Button>("arcadePrimaryButton").onClick;
            var replacement=GameSession.Open(new GameStore(new FileSnapshotStorage(Folder),codec),1);Assert.That(replacement.RecoveryRequired,Is.False);Game.GetType().GetProperty("Session").GetSetMethod(true).Invoke(Game,new object[]{replacement});
            state=State;files=Files();oldConfirm.Invoke();Same(state,files);Assert.That(Session.Parcels.ActiveRun.Ready,Is.False);
        }
        [UnityTest]public IEnumerator ParcelsLockedClassicConflictAndZeroTicketResumeRemainHonest()
        {
            yield return Harness("Prepare",Fixture(false));string state=State;var files=Files();yield return Open();Same(state,files);Assert.That(Field<Button>("arcadePrimaryButton").IsInteractable(),Is.False);
            var s=Fixture();var memory=new Memory{Raw=codec.Encode(s)};var setup=GameSession.Open(new GameStore(memory,codec),1);setup.ExecuteArcade(setup.GetArcadeQuote("start",ArcadeCatalog.Memory,setup.State.sequence),1000);
            File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(setup.State));yield return Harness("ReenterScene");yield return Open();state=State;files=Files();yield return Click("arcadePrimaryButton");Same(state,files);Assert.That(Session.Arcade.ActiveRun.Id,Is.EqualTo(ArcadeCatalog.Memory));
            memory=new Memory{Raw=codec.Encode(Fixture(true,8))};setup=GameSession.Open(new GameStore(memory,codec),1);setup.ExecuteParcels(setup.GetParcelsQuote("start",setup.State.sequence),1000);Assert.That(setup.Arcade.Tickets,Is.Zero);
            File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(setup.State));yield return Harness("ReenterScene");state=State;files=Files();yield return Open();Same(state,files);Assert.That(Session.Parcels.ActiveRun,Is.Not.Null);Assert.That(Field<Button[]>("parcelsTokenButtons").All(b=>b.gameObject.activeInHierarchy),Is.True);
            yield return Click("closeButton");Same(state,files);yield return Click("productAvatar");yield return Click("productCards",2);yield return Click("arcadeEntryButton");
            Assert.That(Field<Button>("arcadeSecondaryButton").GetComponentInChildren<TMP_Text>().text,Does.Contain("Tickets & effects"));yield return Click("arcadeSecondaryButton");Assert.That(Field<int>("wavePanel"),Is.EqualTo(65));yield return Click("closeButton");
            yield return Click("arcadeCards",5);Same(state,files);Assert.That(Field<RectTransform>("parcelsRoot").gameObject.activeInHierarchy,Is.True);
        }
        sealed class Memory:ISnapshotStorage {public string Raw;public string ReadPrimary()=>Raw;public string ReadBackup()=>null;public void Write(string s){Raw=s;}}
        void Profile(int index)
        {
            var flags=Hidden|BindingFlags.Public;var window=Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().Single(w=>w.GetType().Name=="SimulatorWindow");var main=window.GetType().GetField("m_Main",flags).GetValue(window);var p=main.GetType().GetProperty("deviceIndex",flags);if(!originalProfile.HasValue)originalProfile=(int)p.GetValue(main);p.SetValue(main,index);
        }
        void Fits()
        {
            Canvas.ForceUpdateCanvases();var buttons=Field<Button[]>("parcelsTokenButtons").Concat(Field<Button[]>("parcelsSlotButtons")).Concat(new[]{Field<Button>("arcadePrimaryButton"),Field<Button>("arcadeSecondaryButton"),Field<Button>("closeButton")}).Where(b=>b!=null&&b.gameObject.activeInHierarchy).ToArray();
            var rects=new List<Rect>();foreach(var b in buttons)
            {
                var corners=new Vector3[4];((RectTransform)b.transform).GetWorldCorners(corners);var rect=Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);Assert.That(rect.width,Is.GreaterThanOrEqualTo(48),b.name);Assert.That(rect.height,Is.GreaterThanOrEqualTo(48),b.name);foreach(var prior in rects)Assert.That(rect.Overlaps(prior),Is.False,b.name+" overlaps another target");rects.Add(rect);
                foreach(var t in b.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Assert.That(t.isTextOverflowing,Is.False,t.name+": "+t.text);}
            }
            foreach(var t in Field<RectTransform>("arcadeRoot").GetComponentsInChildren<TMP_Text>().Where(t=>t.gameObject.activeInHierarchy)){t.ForceMeshUpdate();Assert.That(t.isTextOverflowing,Is.False,t.name+": "+t.text);}
        }
        IEnumerator Picture(string name)
        {
            Fits();string folder=Environment.GetEnvironmentVariable("JTN_PARCELS_CAPTURE_DIRECTORY");if(string.IsNullOrEmpty(folder))yield break;
            string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/parcels-20261103/tests/captures"));folder=Path.GetFullPath(folder);Assert.That(folder.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(folder);
            Assert.That(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length,Is.LessThan(12));string path=Path.Combine(folder,name+".png");Assert.That(File.Exists(path),Is.False);string state=State;var files=Files();File.WriteAllText(Path.ChangeExtension(path,".json"),state);ScreenCapture.CaptureScreenshot(path);for(int f=0;f<30&&!File.Exists(path);f++)yield return null;Assert.That(File.Exists(path),Is.True);Same(state,files);
        }
        IEnumerator Portrait(int height)
        {
            Profile(height==2340?2:3);yield return Harness("Prepare",Fixture());float until=Time.realtimeSinceStartup+2;while(Screen.height!=height&&Time.realtimeSinceStartup<until)yield return null;Assert.That(Screen.width,Is.EqualTo(1080));Assert.That(Screen.height,Is.EqualTo(height));int frame=Time.frameCount;
            yield return Open();yield return Picture(height+"-preview");yield return Click("arcadePrimaryButton");yield return Picture(height+"-empty");yield return Layout(new[]{0,1,2,3});yield return Picture(height+"-partial1");yield return Click("arcadePrimaryButton");yield return Picture(height+"-confirm1");yield return Click("arcadeSecondaryButton");yield return Place(3,1);yield return Place(1,3);yield return Picture(height+"-solved2");yield return Click("arcadePrimaryButton");yield return Picture(height+"-ready3");yield return Click("arcadePrimaryButton");Assert.That(Session.Arcade.Diamonds,Is.EqualTo(3));Assert.That(Time.frameCount,Is.GreaterThan(frame));
            int[][] solutions={new[]{0,3,1,2},new[]{3,4,0,5},new[]{0,3,4,5},new[]{0,1,3,4}};
            for(int board=1;board<4;board++)
            {
                var memory=new Memory{Raw=codec.Encode(Fixture())};var setup=GameSession.Open(new GameStore(memory,codec),1);
                for(int earlier=0;earlier<board;earlier++)
                {setup.ExecuteParcels(setup.GetParcelsQuote("start",setup.State.sequence),1000);setup.ExecuteParcels(setup.GetParcelsQuote("abandon",setup.State.sequence,setup.Parcels.ActiveRun.InstanceSequence),1000);}
                if(setup.Arcade.Tickets==0)for(int spin=0;spin<8;spin++)setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
                File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(setup.State));yield return Harness("ReenterScene");yield return Start();Fits();yield return Layout(solutions[board]);Fits();
                Assert.That(Session.Parcels.ActiveRun.SatisfiedRequests,Is.EqualTo(2));Assert.That(Session.Parcels.ActiveRun.BoardId,Is.EqualTo(ParcelsCatalog.BoardIds[board]));
            }
        }
        [UnityTest]public IEnumerator ParcelsRegularPortraitRoundConfirmationAndReadyFit(){yield return Portrait(2340);}
        [UnityTest]public IEnumerator ParcelsShortPortraitRoundConfirmationAndReadyFit(){yield return Portrait(2280);}
        [UnityTearDown]public IEnumerator Restore(){yield return ui.RestoreAndRemoveOnlyPrivateFixtures();if(originalProfile.HasValue){Profile(originalProfile.Value);originalProfile=null;}}
    }
}
#endif
