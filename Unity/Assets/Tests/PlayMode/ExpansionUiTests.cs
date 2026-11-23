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

namespace JunkToNeon.Tests.PlayMode
{
    [Category("Expansion5h")]
    public sealed class ExpansionUiTests
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui=new ReelUiLifecycleTests();
        readonly JsonStateCodec codec=new JsonStateCodec();
        Component Game=>(Component)typeof(ReelUiLifecycleTests).GetField("game",Hidden).GetValue(ui);
        GameSession Session=>(GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        string Folder=>(string)typeof(ReelUiLifecycleTests).GetField("directory",Hidden).GetValue(ui);
        T Field<T>(string name)=>(T)Game.GetType().GetField(name,Hidden).GetValue(Game);
        string Text(string name)=>Field<TMP_Text>(name).text;
        IEnumerator Harness(string name,params object[] args)=>(IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(name,Hidden).Invoke(ui,args);
        void Call(string name,params object[] args)=>Game.GetType().GetMethod(name,Hidden|BindingFlags.Public).Invoke(Game,args);
        Dictionary<string,string> Files()=>Directory.GetFiles(Folder).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks);
        void Unchanged(string before,Dictionary<string,string> files)
        {Assert.That(codec.Encode(Session.State),Is.EqualTo(before));Assert.That(Files(),Is.EquivalentTo(files));Assert.That(Session.RecoveryRequired,Is.False,Session.Error);}
        IEnumerator Click(string name,int index=-1){yield return Harness("PointerClick",name,index);yield return null;}
        IEnumerator Back()
        {
            yield return null;var back=Game.GetComponents<Component>().Single(c=>c.GetType().FullName=="JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)back.GetType().GetMethod("RequestBack").Invoke(back,null),Is.True);yield return null;
        }
        IEnumerator Card(string id)
        {
            int index=Array.IndexOf(Field<string[]>("expansionCardIds"),id);Assert.That(index,Is.GreaterThanOrEqualTo(0),id+" not shown");
            yield return Click("expansionCards",index);
        }
        void Fits(TMP_Text text)
        {
            Canvas.ForceUpdateCanvases();text.ForceMeshUpdate();
            Assert.That(text.isTextOverflowing,Is.False,text.name+": "+text.text);
            Assert.That(text.textBounds.size.x,Is.LessThanOrEqualTo(text.rectTransform.rect.width+2));
            Assert.That(text.textBounds.size.y,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2));
        }
        void Layout()
        {
            var root=Field<RectTransform>("expansionRoot");Assert.That(root.gameObject.activeInHierarchy,Is.True);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>())Fits(text);
            var buttons=root.GetComponentsInChildren<UnityEngine.UI.Button>().Concat(new[]{Field<UnityEngine.UI.Button>("closeButton")}).ToArray();
            var panel=(RectTransform)Field<GameObject>("panel").transform;var bounds=new List<Bounds>();
            foreach(var button in buttons)
            {
                var rect=(RectTransform)button.transform;Assert.That(rect.rect.width,Is.GreaterThanOrEqualTo(48));Assert.That(rect.rect.height,Is.GreaterThanOrEqualTo(48));
                var box=RectTransformUtility.CalculateRelativeRectTransformBounds(panel,rect);
                foreach(var previous in bounds)Assert.That(box.Intersects(previous),Is.False,"Visible expansion controls overlap");bounds.Add(box);
                if(!button.IsInteractable())continue;
                var canvas=button.GetComponentInParent<Canvas>();
                foreach(var point in new[]{rect.rect.center,rect.rect.min+Vector2.one*6,rect.rect.max-Vector2.one*6})
                {
                    var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(point))};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Assert.That(hits.Count,Is.GreaterThan(0));
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(button.gameObject));
                }
            }
            Assert.That(Field<UnityEngine.UI.Button>("selectButton").gameObject.activeInHierarchy,Is.False);
        }
        [SetUp]public void Isolate()
        {ui.IsolateSave();Assert.That(Path.GetPathRoot(Path.GetFullPath(Folder)).Replace('\\','/'),Is.EqualTo("X:/").IgnoreCase);}

        [UnityTest,Category("ExpansionQol5h"),Category("ExpansionUtility5h")]public IEnumerator OldSaveShopInspectionIsReadOnlyAndSevenOldHubObjectsRemain()
        {
            var old=ModelState.Fresh(1);old.coins=535;old.houseStage=1;old.selectedStage=2;old.autoHintSeen=1;
            yield return Harness("Prepare",old);Assert.That(Session.State.expansion,Is.Null);
            string before=codec.Encode(Session.State);var files=Files();
            var oldHub=Field<UnityEngine.UI.Button[]>("sectionButtons").Take(7).ToArray();
            yield return Click("productAvatar");yield return Click("productCards",2);yield return Click("expansionEntryButton");Layout();Assert.That(Text("expansionTitle"),Is.EqualTo("Slot & Shop"));
            Assert.That(Text("expansionResources"),Does.Contain("Coins: 535").And.Contain("Parts: 0/").And.Contain("Energy: 0/"));
            int[] counts={5,6,6,6,4,6};
            for(int category=0;category<6;category++)
            {
                yield return Click("expansionCards",category);Layout();
                Assert.That(Field<UnityEngine.UI.Button[]>("expansionCards").Count(b=>b.gameObject.activeSelf),Is.EqualTo(counts[category]));
                if(category==0)
                {
                    Assert.That(Field<string[]>("expansionCardIds").Take(5),Is.EqualTo(new[]{"module.salvage","module.packing","module.harvest","module.combo","module.dynamo"}));
                    yield return Card("module.salvage");Layout();Assert.That(Text("expansionBody"),Does.Contain("Missing:").And.Contain("parts"));
                    Assert.That(Field<UnityEngine.UI.Button>("expansionPrimaryButton").gameObject.activeSelf,Is.False);
                    Field<UnityEngine.UI.Button>("expansionPrimaryButton").onClick.Invoke();Field<UnityEngine.UI.Button>("selectButton").onClick.Invoke();Unchanged(before,files);
                    yield return Back();
                }
                else if(category==3)
                {
                    foreach(var button in Field<UnityEngine.UI.Button[]>("expansionCards"))Assert.That(button.GetComponentInChildren<TMP_Text>(true).text,Does.Contain("0/2"));
                    yield return Card("equipment.workshop.1");Assert.That(Text("expansionBody"),Does.Contain("First build:").And.Contain("24 buildings"));yield return Back();
                }
                yield return Back();Assert.That(Text("expansionTitle"),Is.EqualTo("Slot & Shop"));Unchanged(before,files);
            }
            yield return Click("expansionSecondaryButton");Layout();Assert.That(Text("expansionBody"),Does.Contain("free"));yield return Back();
            int childCount=Field<RectTransform>("expansionRoot").childCount;Call("CreateExpansionUi");Call("CreateExpansionUi");Assert.That(Field<RectTransform>("expansionRoot").childCount,Is.EqualTo(childCount));
            yield return Back();Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("All Sections"));
            Assert.That(Field<UnityEngine.UI.Button[]>("sectionButtons").Take(7),Is.EqualTo(oldHub));
            yield return Click("expansionEntryButton");yield return Back();yield return Click("sectionButtons",0);
            Assert.That(Field<TMP_Text>("panelTitle").text,Is.EqualTo("My Character"));Unchanged(before,files);yield return Click("productTabs",1);
            Assert.That(Field<GameObject>("panel").activeSelf,Is.False);Assert.That(Session.State.expansion,Is.Null);Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(600));Unchanged(before,files);
        }
        sealed class Memory : ISnapshotStorage
        {public string value;public string ReadPrimary()=>value;public string ReadBackup()=>null;public void Write(string raw){value=raw;}}
        GameSession PreparedResources()
        {
            // Bounded pure foreground setup only, not a UI chapter playthrough.
            var state=ModelState.Fresh(1);state.houseStage=2;state.autoHintSeen=1;state.lastSettledAt=1000;
            var setup=GameSession.Open(new GameStore(new Memory{value=codec.Encode(state)},codec),1,initializeJourney:true);
            for(int spin=0;spin<80;spin++)setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
            Assert.That(setup.Expansion.Parts,Is.GreaterThanOrEqualTo(60));Assert.That(setup.Expansion.Potatoes,Is.GreaterThanOrEqualTo(30));
            Assert.That(setup.Expansion.Energy,Is.EqualTo(30));return setup;
        }
        [UnityTest,Category("ExpansionQol5h")]public IEnumerator ExplicitBuyEquipUnequipPaletteAndFreeSpeedPersistWithoutAutomaticInstallation()
        {
            yield return Harness("Prepare",PreparedResources().State);
            yield return Click("productAvatar");yield return Click("productCards",2);yield return Click("expansionEntryButton");
            yield return Click("expansionCards",0);yield return Card("module.salvage");Layout();
            var before=Session.State;int parts=Session.Expansion.Parts;
            var oldBuy=Field<UnityEngine.UI.Button>("expansionPrimaryButton").onClick;
            yield return Click("expansionPrimaryButton");
            Assert.That(Session.Expansion.OwnsModule("module.salvage"),Is.True);Assert.That(Session.Expansion.ModuleAt(1),Is.Null);
            Assert.That(Session.Expansion.Parts,Is.EqualTo(parts-12));Assert.That(Session.State.coins,Is.EqualTo(before.coins));Assert.That(Session.State.sequence,Is.EqualTo(before.sequence+1));
            string committed=codec.Encode(Session.State);var files=Files();oldBuy.Invoke();Unchanged(committed,files);
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(54));Layout();
            Assert.That(Field<UnityEngine.UI.Button>("expansionPrimaryButton").GetComponentInChildren<TMP_Text>().text,Does.Contain("slot"));
            yield return Click("expansionPrimaryButton");Unchanged(committed,files);
            Assert.That(Field<int>("wavePanel"),Is.EqualTo(53));
            yield return Click("expansionCards",0);Layout();
            yield return Back();Unchanged(committed,files);yield return Click("expansionCards",0);yield return Click("expansionPrimaryButton");
            Assert.That(Session.Expansion.ModuleAt(1),Is.EqualTo("module.salvage"));Assert.That(Session.Expansion.Parts,Is.EqualTo(parts-12));
            yield return Back();yield return Click("expansionSecondaryButton");Layout();yield return Click("expansionPrimaryButton");
            Assert.That(Session.Expansion.ModuleAt(1),Is.Null);Assert.That(Session.Expansion.OwnsModule("module.salvage"),Is.True);
            yield return Back();yield return Back();yield return Back();yield return Click("expansionCards",2);yield return Card("theme.amber");
            int potatoes=Session.Expansion.Potatoes;parts=Session.Expansion.Parts;
            var oldPaletteBuy=Field<UnityEngine.UI.Button>("expansionPrimaryButton").onClick;
            yield return Click("expansionPrimaryButton");
            Assert.That(Session.Expansion.OwnsTheme("theme.amber"),Is.True);Assert.That(Session.Expansion.ThemeId,Is.EqualTo("theme.neon"));
            Assert.That(Session.Expansion.Parts,Is.EqualTo(parts-15));Assert.That(Session.Expansion.Potatoes,Is.EqualTo(potatoes-5));
            committed=codec.Encode(Session.State);files=Files();oldPaletteBuy.Invoke();Unchanged(committed,files);
            Assert.That(Session.Expansion.ThemeId,Is.EqualTo("theme.neon"));Layout();
            Assert.That(Field<UnityEngine.UI.Button>("expansionPrimaryButton").GetComponentInChildren<TMP_Text>().text,Does.Contain("Apply").And.Contain("Free"));
            yield return Click("expansionPrimaryButton");Assert.That(Session.Expansion.ThemeId,Is.EqualTo("theme.amber"));
            yield return Back();yield return Back();uint seed=Session.State.seed;parts=Session.Expansion.Parts;potatoes=Session.Expansion.Potatoes;
            yield return Click("expansionPrimaryButton");Assert.That(Text("expansionTitle"),Is.EqualTo("Slot & Shop"));
            Assert.That(Session.Expansion.SpeedId,Is.EqualTo("speed.fast"));Assert.That(Session.Expansion.SpinDurationSeconds,Is.EqualTo(1.5f));Assert.That(Session.State.seed,Is.EqualTo(seed));
            Assert.That(Session.Expansion.Parts,Is.EqualTo(parts));Assert.That(Session.Expansion.Potatoes,Is.EqualTo(potatoes));
            yield return Harness("ReenterScene");Assert.That(Session.Expansion.ThemeId,Is.EqualTo("theme.amber"));Assert.That(Session.Expansion.SpeedId,Is.EqualTo("speed.fast"));
            Assert.That(Session.Expansion.OwnsModule("module.salvage"),Is.True);Assert.That(Session.Expansion.ModuleAt(1),Is.Null);
        }
        [UnityTest,Category("ExpansionQol5h")]public IEnumerator EquipmentIsSeparateAndBoostedBonusAppearsOnlyAfterNormalPresentation()
        {
            var setup=PreparedResources();setup.BuyExpansion(setup.GetExpansionOffer("bonus.unlock"),1000);
            var boost=setup.GetExpansionAction("boost","boost.next-bonus");Assert.That(boost.EnergyCost,Is.EqualTo(15));
            setup.ExecuteExpansion(boost,1000);Assert.That(setup.Expansion.Energy,Is.EqualTo(15));
            setup.ExecuteExpansion(setup.GetExpansionAction("speed","speed.fast"),1000);
            for(int i=0;i<4;i++)setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
            Assert.That(setup.Expansion.BonusProgress,Is.EqualTo(4));Assert.That(setup.Expansion.BoostArmed,Is.True);
            yield return Harness("Prepare",setup.State);int construction=Session.Chapter.Progress.CompletedUpgradeIds.Count;
            yield return Click("productSettings");yield return Click("productCards",4);yield return Click("expansionCards",3);yield return Card("equipment.house.1");Layout();yield return Click("expansionPrimaryButton");
            Assert.That(Session.Expansion.EquipmentLevel("house"),Is.EqualTo(1));Assert.That(Session.Expansion.PartsCap,Is.EqualTo(250));Assert.That(Session.Expansion.PotatoesCap,Is.EqualTo(250));
            Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(construction));Assert.That(Session.Expansion.BonusProgress,Is.EqualTo(4));
            // The product route is nested; leave it by Back until the game root is visible.
            for(int i=0;i<6&&Field<GameObject>("panel").activeSelf;i++)yield return Back();Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
            string visibleResources=Text("expansionHudResources");long lastRolls=Session.Expansion.BonusRolls;
            yield return Click("spinButton");Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.True);
            Assert.That(Text("expansionHudResources"),Is.EqualTo(visibleResources));Assert.That(Text("expansionBonusResult"),Is.EqualTo("…"));
            Call("Render",null,Session.State);Assert.That(Text("expansionHudResources"),Is.EqualTo(visibleResources));Assert.That(Text("expansionBonusResult"),Does.Not.Contain("Parts +"));
            yield return Harness("FinishPresentation");var view=Session.Expansion;var receipt=view.LastResourceReceipt;
            Assert.That(view.BonusRolls,Is.EqualTo(lastRolls+1));Assert.That(view.BoostArmed,Is.False);Assert.That(receipt.Boosted,Is.True);Assert.That(receipt.BonusSymbol,Is.InRange(0,3));
            Assert.That(Text("expansionHudResources"),Does.Contain("Parts "+view.Parts+"/").And.Contain("potatoes "+view.Potatoes+"/"));
            Assert.That(Text("expansionBonusResult"),Does.Contain("Parts +"+receipt.BonusParts).And.Contain("Potatoes +"+receipt.BonusPotatoes).And.Contain("×2"));
            Assert.That(Text("expansionBonusResult"),Does.Contain("×2 included"));
            Assert.That(Text("expansionBonusProgress"),Is.EqualTo("5 spins left"));
            Fits(Field<TMP_Text>("expansionHudResources"));Fits(Field<TMP_Text>("expansionBonusResult"));Fits(Field<TMP_Text>("expansionBonusProgress"));
            Assert.That(Field<RectTransform>("expansionBonusFrame").gameObject.activeInHierarchy,Is.True);
            var images=Field<UnityEngine.UI.Image[]>("reelImages");Assert.That(images.Length,Is.EqualTo(3));
            for(int i=0;i<3;i++)Assert.That(((RectTransform)images[i].transform.parent).anchorMin.x,Is.EqualTo(.05f+i*.24f).Within(.0001f));
            // Older loaded save truthfully hides the optional reel and restores all
            // three authored frame anchors; it does not keep the newer ownership.
            var old=ModelState.Fresh(1);old.lastSettledAt=1000;File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(old));
            yield return Harness("ReenterScene");Assert.That(Field<RectTransform>("expansionBonusFrame").gameObject.activeSelf,Is.False);
            images=Field<UnityEngine.UI.Image[]>("reelImages");for(int i=0;i<3;i++)Assert.That(((RectTransform)images[i].transform.parent).anchorMin.x,Is.EqualTo(.035f+i*.317f).Within(.0001f));
        }
        [UnityTearDown]public IEnumerator Restore()
        {
            if(TestContext.CurrentContext.Result.Outcome.Status==NUnit.Framework.Interfaces.TestStatus.Failed)
            {TestContext.WriteLine("Preserved Expansion UI fixture: "+Folder);typeof(ReelUiLifecycleTests).GetField("directory",Hidden).SetValue(ui,null);}
            yield return ui.RestoreAndRemoveOnlyPrivateFixtures();
        }
    }
}
#endif
