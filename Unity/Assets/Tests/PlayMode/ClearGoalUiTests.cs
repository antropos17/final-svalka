#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JunkToNeon.Core;
using JunkToNeon.Guidance;
using JunkToNeon.Orders;
using JunkToNeon.Upgrades;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace JunkToNeon.Tests.PlayMode
{
    // One bounded private Editor case, two prepared old-price scenarios. No native
    // input/durability or uninterrupted human-history claim; existing fixture owns cleanup.
    public sealed class ClearGoalUiTests
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
        string Caption(string field)=>Field<UnityEngine.UI.Button>(field).GetComponentInChildren<TMP_Text>(true).text;
        void Fits(TMP_Text text)
        {Canvas.ForceUpdateCanvases();text.ForceMeshUpdate();Assert.That(text.isTextOverflowing,Is.False,text.text);Assert.That(text.textBounds.size.x,Is.LessThanOrEqualTo(text.rectTransform.rect.width+2));Assert.That(text.textBounds.size.y,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2));}
        IEnumerator Click(string field,int index=-1)
        {
            var button=index<0?Field<UnityEngine.UI.Button>(field):Field<UnityEngine.UI.Button[]>(field)[index];
            Assert.That(button.isActiveAndEnabled&&button.IsInteractable(),Is.True);yield return Harness("PointerClick",field,index);
        }
        IEnumerator Back()
        {yield return null;var back=Game.GetComponents<Component>().Single(c=>c.GetType().FullName=="JunkToNeon.Presentation.JtnBackNavigation");Assert.That((bool)back.GetType().GetMethod("RequestBack").Invoke(back,null),Is.True);}
        IEnumerator Progress()
        {
            yield return Click("productAvatar");yield return Click("productCards",2);yield return Click("sectionButtons",5);
            Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Progress"));Fits(Field<TMP_Text>("panelBody"));
        }
        void ClearSlot()
        {
            Assert.That(Caption("chapterPrevious"),Is.EqualTo("Clear goal"));var button=Field<UnityEngine.UI.Button>("chapterPrevious");Fits(button.GetComponentInChildren<TMP_Text>(true));
            var panel=(RectTransform)Field<GameObject>("panel").transform;Assert.That(RectTransformUtility.CalculateRelativeRectTransformBounds(panel,button.transform).Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(panel,Field<TMP_Text>("panelBody").transform)),Is.False);
        }
        void Confirmation(long price)
        {
            string text=Field<TMP_Text>("panelBody").text;
            Assert.That(text,Does.Contain("Cost: 0").And.Contain("Coins remain 535").And.Contain("no refund").And.Contain("Saved price "+price+" expires").And.Contain("current price (480)"));
            Assert.That(Caption("selectButton"),Is.EqualTo("Clear goal"));Assert.That(Caption("closeButton"),Is.EqualTo("Back"));Fits(Field<TMP_Text>("panelBody"));
        }
        [SetUp]public void Isolate()
        {ui.IsolateSave();Assert.That(Path.GetPathRoot(Path.GetFullPath(DirectoryPath)).Replace('\\','/'),Is.EqualTo("X:/").IgnoreCase);}
        [UnityTest,Timeout(180000)]
        public IEnumerator ExplicitClearKeeps535AndOldHistoryUntilFreshConfirmationThenNew480SelectionLeaves55()
        {
            // Synthetic literal pre-Chapter600 target, not an invented paid/spin history.
            var old=ModelState.Fresh(1);old.coins=535;old.houseStage=1;old.autoHintSeen=1;old.selectedStage=2;
            yield return Harness("Prepare",old);
            foreach(long price in new[]{600L,540L})
            {
                if(price==540)
                {
                    var v2=codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Checks/Milestones/fixtures/v2-selected540.json"))));v2.lastSettledAt=1000;
                    GameLogic.Validate(v2);File.WriteAllText(Path.Combine(DirectoryPath,"jtn.save.v1.json"),codec.Encode(v2));yield return Harness("ReenterScene");
                }
                var before=Session.State;string raw=codec.Encode(before);var files=Files();uint seed=before.seed;
                string revision=Session.Chapter.ConstructionPriceRevision,guide=GuideStateCodec.Encode(Session.Journey.Guidance),orders=OrderStateCodec.Encode(Session.Journey.Orders);
                string previous=Caption("chapterPrevious"),next=Caption("chapterNext");var left=(RectTransform)Field<UnityEngine.UI.Button>("chapterPrevious").transform;var min=left.anchorMin;var max=left.anchorMax;
                Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(price));
                yield return Progress();ClearSlot();yield return Click("chapterPrevious");Confirmation(price);Unchanged(raw,files);
                yield return Back();Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Progress"));Unchanged(raw,files);
                yield return Click("chapterPrevious");Confirmation(price);yield return Click("closeButton");Unchanged(raw,files);
                Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Progress"));
                // Pausing invalidates the transient immutable confirmation; hidden/queued actions cannot retry it.
                yield return Click("chapterPrevious");var stale=Field<UnityEngine.UI.Button>("selectButton").onClick;
                Runtime("OnApplicationPause",true);stale.Invoke();Unchanged(raw,files);Runtime("OnApplicationPause",false);
                Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);stale.Invoke();Unchanged(raw,files);
                yield return Click("closeButton");Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
                yield return Progress();ClearSlot();yield return Click("chapterPrevious");Confirmation(price);
                var quote=Field<ClearGoalRequest>("goalClearRequest");Assert.That(quote.ProjectId,Is.EqualTo("house.improve"));Assert.That(quote.SavedPrice,Is.EqualTo(price));Assert.That(quote.ExpectedSequence,Is.EqualTo(before.sequence));
                yield return Click("selectButton");Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
                Assert.That(Session.State.sequence,Is.EqualTo(before.sequence+1));Assert.That(Session.State.coins,Is.EqualTo(535));Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.State.houseStage,Is.EqualTo(1));
                Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Session.State.selectedStage,Is.Zero);Assert.That(Session.Chapter.ConstructionPriceRevision,Is.EqualTo(revision));
                Assert.That(Session.Chapter.Progress.CompletedUpgradeIds,Is.EquivalentTo(new[]{"house.repair"}));Assert.That(Session.Chapter.Skills.Receipts.Count,Is.Zero);
                Assert.That(GuideStateCodec.Encode(Session.Journey.Guidance),Is.EqualTo(guide));Assert.That(OrderStateCodec.Encode(Session.Journey.Orders),Is.EqualTo(orders));
                Assert.That(File.ReadAllText(Path.Combine(DirectoryPath,"jtn.save.v1.json")),Is.EqualTo(codec.Encode(Session.State)));
                var backup=codec.Decode(File.ReadAllText(Path.Combine(DirectoryPath,"jtn.backup.v1.json")));Assert.That(backup.pending.kind,Is.EqualTo("clear-goal"));Assert.That(backup.pending.chapter.Price,Is.Zero);
                Assert.That(backup.pending.chapter.Before.SelectedProjectId,Is.EqualTo("house.improve"));Assert.That(backup.pending.chapter.After.SelectedProjectId,Is.Null);
                Assert.That(Caption("chapterPrevious"),Is.EqualTo(previous));Assert.That(Caption("chapterNext"),Is.EqualTo(next));Assert.That(left.anchorMin,Is.EqualTo(min));Assert.That(left.anchorMax,Is.EqualTo(max));Fits(Field<TMP_Text>("message"));
                raw=codec.Encode(Session.State);files=Files();stale.Invoke();Field<UnityEngine.UI.Button>("chapterPrevious").onClick.Invoke();Unchanged(raw,files);
                yield return Progress();Assert.That(Field<UnityEngine.UI.Button>("chapterPrevious").gameObject.activeSelf,Is.False);
                Assert.That(Caption("chapterNext"),Is.EqualTo("Choose goal"));Fits(Field<UnityEngine.UI.Button>("chapterNext").GetComponentInChildren<TMP_Text>(true));
                var choose=Field<UnityEngine.UI.Button>("chapterNext").onClick;
                Field<UnityEngine.UI.Button>("chapterPrevious").onClick.Invoke();Unchanged(raw,files);
                Runtime("OnApplicationPause",true);choose.Invoke();Unchanged(raw,files);Runtime("OnApplicationPause",false);choose.Invoke();Unchanged(raw,files);
                Assert.That(Field<UnityEngine.UI.Button>("chapterNext").gameObject.activeSelf,Is.False);yield return Click("closeButton");Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
                yield return Progress();yield return Click("chapterNext");Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("Buildings · all six"));Assert.That(Field<int>("chapterPage"),Is.Zero);
                Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeSelf,Is.False);Unchanged(raw,files);for(int back=0;back<4&&Field<GameObject>("panel").activeSelf;back++)yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False,"Back must leave the nested Buildings route");choose.Invoke();Unchanged(raw,files);
                yield return Progress();yield return Click("chapterNext");yield return Click("navigationCards",0);
                Assert.That(Field<TMP_Text>("panelBody").text,Does.Contain("Price: 480 Coins."));Unchanged(raw,files);yield return Click("selectButton");
                Assert.That(Session.State.coins,Is.EqualTo(55));Assert.That(Session.State.houseStage,Is.EqualTo(2));Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Session.State.seed,Is.EqualTo(seed));Assert.That(Session.Chapter.ConstructionPriceRevision,Is.EqualTo("construction.prices.v3"));
                raw=codec.Encode(Session.State);files=Files();stale.Invoke();Unchanged(raw,files);Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            }
        }
        [UnityTearDown]public IEnumerator RestoreFixture()
        {
            if(TestContext.CurrentContext.Result.Outcome.Status==NUnit.Framework.Interfaces.TestStatus.Failed)
            {TestContext.WriteLine("Preserved failed clear-goal fixture: "+DirectoryPath);typeof(ReelUiLifecycleTests).GetField("directory",Hidden).SetValue(ui,null);}
            yield return ui.RestoreAndRemoveOnlyPrivateFixtures();
        }
    }
}
#endif
