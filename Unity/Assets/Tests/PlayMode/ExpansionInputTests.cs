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
    // Simulated uGUI events against the actual saved scene and private X: store.
    // Existing installed-InputSystem held-touch proofs remain separate evidence.
    [Category("Expansion5h")]
    public sealed class ExpansionInputTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly CombinedLifecycleTests ui = new CombinedLifecycleTests();
        readonly JsonStateCodec codec = new JsonStateCodec();
        Component Game => (Component)typeof(CombinedLifecycleTests).GetField("game", Hidden).GetValue(ui);
        string Folder => (string)typeof(CombinedLifecycleTests).GetField("directory", Hidden).GetValue(ui);
        GameSession Session => (GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        T Field<T>(string name) => (T)Game.GetType().GetField(name, Hidden).GetValue(Game);
        void Call(string name, params object[] args) => Game.GetType().GetMethod(name, Hidden | BindingFlags.Public).Invoke(Game, args);
        IEnumerator Harness(string name, params object[] args) =>
            (IEnumerator)typeof(CombinedLifecycleTests).GetMethod(name, Hidden).Invoke(ui, args);
        string State => codec.Encode(Session.State);
        Dictionary<string, string> Files()
        {
            Assert.That(Directory.GetDirectories(Folder), Is.Empty, "Private save fixture must have no nested output");
            var paths = Directory.GetFiles(Folder); Assert.That(paths.Length, Is.LessThanOrEqualTo(8));
            foreach (string path in paths)
            {
                Assert.That((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, Is.True);
                Assert.That(new FileInfo(path).Length, Is.LessThanOrEqualTo(262148));
            }
            return paths.ToDictionary(Path.GetFileName,
                p => Convert.ToBase64String(File.ReadAllBytes(p)) + "|" + File.GetLastWriteTimeUtc(p).Ticks);
        }
        void Unchanged(string state, Dictionary<string, string> files)
        {
            Assert.That(State, Is.EqualTo(state), "The rejected/read-only action changed the complete state");
            Assert.That(Files(), Is.EquivalentTo(files), "Private file inventory, bytes or timestamps changed");
            Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
        }
        void WholeLoopSnapshot(string name,string provenance)
        {
            string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.work/whole-loop-delivery-20261101/root","produced-"+name+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"jtn.save.v1.json"),State,new System.Text.UTF8Encoding(false));
            var quick=Field<Button>("expansionQuickBoostButton");
            File.WriteAllText(Path.Combine(target,"quick-label.txt"),quick.GetComponentInChildren<TMP_Text>(true).text,new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(target,"provenance.txt"),provenance+"; sequence="+Session.State.sequence+"; active="+quick.gameObject.activeInHierarchy+"; interactable="+quick.IsInteractable()+". UI text is a live checkpoint.",new System.Text.UTF8Encoding(false));
            TestContext.WriteLine("Private whole-loop snapshot: "+target);
        }
        static Vector2 ScreenPoint(Button button, Vector2 local)
        {
            var canvas = button.GetComponentInParent<Canvas>();
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                button.transform.TransformPoint(local));
        }
        static RaycastResult Top(Button button, Vector2 local)
        {
            Canvas.ForceUpdateCanvases();
            var data = new PointerEventData(EventSystem.current) { position = ScreenPoint(button, local) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), button.name + " must be raycastable");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject),
                button.name + " must be the topmost Button");
            return hits[0];
        }
        static void Target(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, button.name);
            var rect = ((RectTransform)button.transform).rect;
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(48), button.name + " width in Canvas units");
            Assert.That(rect.height, Is.GreaterThanOrEqualTo(48), button.name + " height in Canvas units");
            foreach (var point in new[] { rect.center, rect.min + Vector2.one * 4, rect.max - Vector2.one * 4,
                new Vector2(rect.xMin + 4, rect.yMax - 4), new Vector2(rect.xMax - 4, rect.yMin + 4) }) Top(button, point);
        }
        static PointerEventData Down(Button button, int id)
        {
            Target(button);
            var local = ((RectTransform)button.transform).rect.center;
            var data = new PointerEventData(EventSystem.current) { pointerId = id, button = PointerEventData.InputButton.Left,
                position = ScreenPoint(button, local), eligibleForClick = true, pointerCurrentRaycast = Top(button, local) };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            data.pointerPress = button.gameObject;
            data.pointerClick = button.gameObject;
            return data;
        }
        static void Release(PointerEventData data, Button current)
        {
            var local = ((RectTransform)current.transform).rect.center;
            data.position = ScreenPoint(current, local);
            data.pointerCurrentRaycast = Top(current, local);
            bool candidate = data.eligibleForClick && data.pointerClick ==
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject);
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            if (candidate) ExecuteEvents.Execute(data.pointerClick, data, ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Click(Button button, int id = 401)
        { yield return null; Release(Down(button, id), button); }
        IEnumerator Back()
        {
            yield return null;
            var back = Game.GetComponents<Component>().Single(c => c.GetType().FullName == "JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)back.GetType().GetMethod("RequestBack").Invoke(back, null), Is.True);
        }
        static void Submit(Button button)
        {
            Target(button);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
        IEnumerator PrepareResources(bool ownSalvage = false, bool cycleBoost = false)
        {
            // Explicit prepared wallet/house, then 25 actual in-memory foreground
            // operations. No runtime speed change, internal resource setter or
            // fabricated purchase/ownership receipt is used.
            var initial = ModelState.Fresh(1);
            initial.coins = 1000; initial.houseStage = 1; initial.autoHintSeen = 1; initial.lastSettledAt = 1000;
            var setup = (GameSession)typeof(CombinedLifecycleTests).GetMethod("Fixture", Hidden).Invoke(ui, new object[] { initial });
            for (int spin = 0; spin < 25; spin++) setup.ExecuteSpin(setup.State.sequence + 1, 1000);
            Assert.That(setup.State.seed, Is.EqualTo(2338876636u));
            Assert.That(new[] { setup.Expansion.Parts, setup.Expansion.Potatoes, setup.Expansion.Energy },
                Is.EqualTo(new[] { 32, 27, 25 }), "Independent seed1/75-symbol tally; 25 Energy within cap30");
            setup.BuyExpansion(setup.GetExpansionOffer("bonus.unlock"), 1000);
            if (ownSalvage) setup.BuyExpansion(setup.GetExpansionOffer("module.salvage"), 1000);
            Assert.That(new[] { setup.Expansion.Parts, setup.Expansion.Potatoes, setup.Expansion.Energy },
                Is.EqualTo(ownSalvage ? new[] { 0, 17, 25 } : new[] { 12, 17, 25 }));
            if (cycleBoost)
            {
                setup.ArmExpansionBoost(setup.State.sequence, 1000);
                for (int spin = 0; spin < 5; spin++) setup.ExecuteSpin(setup.State.sequence + 1, 1000);
                Assert.That(setup.Expansion.Energy, Is.EqualTo(15));
                Assert.That(setup.Expansion.BoostArmed, Is.False);
                Assert.That(setup.Expansion.BonusRolls, Is.EqualTo(1));
            }
            yield return Harness("Load", setup.State);
            yield return null;
        }
        IEnumerator Home()
        {
            // Expansion Home is reached through Settings > Spin Speed; the legacy shop button is hidden by the product navigation.
            if (!Field<GameObject>("panel").activeSelf) { yield return Click(Field<Button>("productSettings")); yield return Click(Field<Button[]>("productCards")[4]); }
            for (int depth = 0; Field<int>("wavePanel") != 50 && depth < 4; depth++) yield return Back();
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(50), "Expected actual Expansion Home route");
        }
        IEnumerator ReturnToGame()
        {
            for (int depth = 0; Field<GameObject>("panel").activeSelf && depth < 5; depth++) yield return Back();
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False, "Expected normal Game after bounded Back navigation");
        }
        IEnumerator Preview(string id, int category)
        {
            string before = State; var files = Files();
            yield return Home();
            yield return Click(Field<Button[]>("expansionCards")[category]);
            int index = -1;
            for (int page = 0; page < 3; page++)
            {
                index = Array.IndexOf(Field<string[]>("expansionCardIds"), id);
                if (index >= 0) break;
                yield return Click(Field<Button>("expansionNextButton"));
            }
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Published stable item is absent from actual category: " + id);
            yield return Click(Field<Button[]>("expansionCards")[index]);
            yield return null; // Newly activated action Graphics must enter actual raycasting.
            Unchanged(before, files);
        }
        ExpansionQuote Quote(string action, string id, int parts, int potatoes, int energy, int slot = 0, string field = "expansionQuote")
        {
            var quote = Field<ExpansionQuote>(field);
            Assert.That(quote, Is.Not.Null);
            Assert.That(quote.Action, Is.EqualTo(action)); Assert.That(quote.Id, Is.EqualTo(id)); Assert.That(quote.Slot, Is.EqualTo(slot));
            Assert.That(quote.ExpectedSequence, Is.EqualTo(Session.State.sequence));
            Assert.That(quote.Sequence, Is.EqualTo(Session.State.sequence + 1));
            Assert.That(new[] { quote.PartsCost, quote.PotatoesCost, quote.EnergyCost, quote.CoinsCost, quote.CoinsReward },
                Is.EqualTo(new long[] { parts, potatoes, energy, 0, 0 }));
            return quote;
        }
        IEnumerator EquipPreview()
        {
            string before = State; var files = Files();
            yield return Preview("module.salvage", 0);
            Assert.That(Session.Expansion.OwnsModule("module.salvage"), Is.True);
            yield return Click(Field<Button>("expansionPrimaryButton")); // Choose an actual socket; no equip yet.
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(53));
            yield return Click(Field<Button[]>("expansionCards")[0]);
            yield return null;
            Quote("equip", "module.salvage", 0, 0, 0, 1);
            Unchanged(before, files);
        }

        [UnityTest]
        public IEnumerator OldBuyContactCannotArmDifferentBoostAndFreshIndependentContactSpendsFifteenOnce()
        {
            yield return PrepareResources();
            yield return Preview("module.salvage", 0); Quote("buy", "module.salvage", 12, 0, 0);
            var primary = Field<Button>("expansionPrimaryButton");
            var old = Down(primary, 411);
            yield return Preview("boost.next-bonus", 5);
            Assert.That(Field<Button>("expansionPrimaryButton"), Is.SameAs(primary), "The test must exercise a reused economic control");
            var current = Quote("boost", "boost.next-bonus", 0, 0, 15);
            string before = State; var files = Files(); long coins = Session.State.coins; uint seed = Session.State.seed;
            var fresh = Down(primary, 412);
            Release(old, primary); Unchanged(before, files);
            Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.SameAs(current), "Rejected old release must not replace current consent/quote");
            Release(fresh, primary);
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 12, 17, 10 }));
            Assert.That(Session.Expansion.BoostArmed, Is.True);
            Assert.That(Session.Expansion.OwnsModule("module.salvage"), Is.False, "Old Buy must not execute");
            Assert.That(Session.State.sequence, Is.EqualTo(current.Sequence));
            Assert.That(Session.State.coins, Is.EqualTo(coins)); Assert.That(Session.State.seed, Is.EqualTo(seed));
            Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(current.Key));
            Assert.That(Session.Expansion.LastCommand.Action, Is.EqualTo("boost"));
            Assert.That(Session.Expansion.LastCommand.Id, Is.EqualTo("boost.next-bonus"));
            before = State; files = Files(); primary.onClick.Invoke(); primary.onClick.Invoke(); Unchanged(before, files);
            TestContext.WriteLine("Simulated uGUI: old Buy release rejects; distinct fresh Boost contact debits energy25->10 once; Parts12/Potatoes17/Coins/main RNG preserved.");
        }

        [UnityTest]
        public IEnumerator OldEquipReleaseDoesNotPoisonFreshSubmitAndStaleQuoteCannotEquipAfterARealSpin()
        {
            yield return PrepareResources(true);
            yield return EquipPreview();
            var primary = Field<Button>("expansionPrimaryButton"); var old = Down(primary, 421);
            yield return Preview("boost.next-bonus", 5);
            Assert.That(Field<Button>("expansionPrimaryButton"), Is.SameAs(primary));
            var boost = Quote("boost", "boost.next-bonus", 0, 0, 15);
            string before = State; var files = Files();
            Release(old, primary); Unchanged(before, files);
            Submit(primary); // Same-frame simulated Submit after the consumed stale release.
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 0, 17, 10 }));
            Assert.That(Session.Expansion.BoostArmed, Is.True);
            Assert.That(Session.Expansion.ModuleAt(1), Is.Null, "Held old Equip must not install its module");
            Assert.That(Session.State.sequence, Is.EqualTo(boost.Sequence));
            Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(boost.Key));

            yield return EquipPreview();
            var stale = Quote("equip", "module.salvage", 0, 0, 0, 1);
            // One legal public store operation, while the existing modal retains
            // its quote. No Runtime field, source, resource or receipt is edited.
            Session.ExecuteSpin(Session.State.sequence + 1, 1000);
            Assert.That(Session.State.sequence, Is.EqualTo(stale.Sequence));
            before = State; files = Files();
            Submit(primary); Unchanged(before, files);
            Assert.That(Session.Expansion.ModuleAt(1), Is.Null);
            Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(boost.Key));
            Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null, "Refused stale quote must not auto-retry or refresh authorization");

            yield return EquipPreview();
            var current = Quote("equip", "module.salvage", 0, 0, 0, 1);
            int parts = Session.Expansion.Parts, potatoes = Session.Expansion.Potatoes, energy = Session.Expansion.Energy;
            long coins = Session.State.coins; uint seed = Session.State.seed;
            Submit(primary);
            Assert.That(Session.Expansion.ModuleAt(1), Is.EqualTo("module.salvage"));
            Assert.That(Session.State.sequence, Is.EqualTo(current.Sequence));
            Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(current.Key));
            Assert.That(Session.Expansion.LastCommand.Slot, Is.EqualTo(1));
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { parts, potatoes, energy }));
            Assert.That(Session.State.coins, Is.EqualTo(coins)); Assert.That(Session.State.seed, Is.EqualTo(seed));
            before = State; files = Files(); primary.onClick.Invoke(); Unchanged(before, files);
            TestContext.WriteLine("Simulated uGUI/Submit: old Equip cannot become Boost; fresh Submit debits15 once. One legal intervening Core spin makes the retained equip quote stale/no-IO; new explicit socket1 equip succeeds free.");
        }

        [UnityTest]
        public IEnumerator MainQuickBoostRejectsMenuAndPauseContactsAndStaleEnergyThenFreshSubmitSpendsFifteenOnce()
        {
            yield return PrepareResources(cycleBoost: true);
            var quick = Field<Button>("expansionQuickBoostButton");
            Quote("boost", "boost.next-bonus", 0, 0, 15, field: "expansionQuickBoostQuote");
            Assert.That(quick.GetComponentInChildren<TMP_Text>(true).text, Does.Contain("15"), "Main boost must display its actual energy cost");
            var old = Down(quick, 431);
            string before = State; var files = Files();
            yield return Click(Field<Button[]>("productTabs")[3]);Assert.That(Field<int>("wavePanel"),Is.EqualTo(72));
            quick.onClick.Invoke(); Unchanged(before, files);
            yield return ReturnToGame();
            Action<string> observeQuick = label =>
            {
                Canvas.ForceUpdateCanvases();
                var graphic=quick.targetGraphic;var canvas=quick.GetComponentInParent<Canvas>(true);
                var point=RectTransformUtility.WorldToScreenPoint(canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null,
                    quick.transform.TransformPoint(((RectTransform)quick.transform).rect.center));
                var hits=new List<RaycastResult>();
                if(EventSystem.current!=null)EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
                TestContext.WriteLine($"QuickBoost {label}: frame={Time.frameCount} activeSelf={quick.gameObject.activeSelf} hierarchy={quick.gameObject.activeInHierarchy} enabled={quick.isActiveAndEnabled} interactable={quick.IsInteractable()} graphicEnabled={graphic!=null&&graphic.enabled} graphicRaycast={graphic!=null&&graphic.raycastTarget} graphicDepth={(graphic==null?-1:graphic.depth)} center={point} hits={hits.Count} top={(hits.Count==0?"none":hits[0].gameObject.name)}");
            };
            observeQuick("immediate after Back");yield return null;observeQuick("one frame after Back");
            Release(old, quick); Unchanged(before, files);

            var paused = Down(quick, 432);
            Call("OnApplicationPause", true); quick.onClick.Invoke(); Unchanged(before, files);
            Call("OnApplicationPause", false); yield return null; Release(paused, quick); Unchanged(before, files);
            var stale = Quote("boost", "boost.next-bonus", 0, 0, 15, field: "expansionQuickBoostQuote");
            var staleHeld=Down(quick,435);
            Assert.That(Session.Expansion.Energy, Is.EqualTo(15));
            Session.ExecuteSpin(Session.State.sequence + 1, 1000);
            Assert.That(Session.Expansion.Energy, Is.EqualTo(16), "One real future spin changes the displayed Energy quote context");
            Assert.That(Session.State.sequence, Is.EqualTo(stale.Sequence));
            before = State; files = Files(); Release(staleHeld,quick); Unchanged(before, files);
            Assert.That(Session.Expansion.BoostArmed, Is.False);

            // Actual Busy spin consumes no boost; the permanently wired callback
            // cannot become consent while the displayed control is disabled.
            yield return Click(Field<Button>("spinButton"));Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.True);
            Assert.That(quick.IsInteractable(),Is.False);before=State;files=Files();quick.onClick.Invoke();Unchanged(before,files);
            Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.False);
            yield return Harness("Finish");
            var pointerStart=Session.State;int pointerEnergy=Session.Expansion.Energy;
            var pointerQuote=Quote("boost", "boost.next-bonus", 0, 0, 15, field: "expansionQuickBoostQuote");
            WholeLoopSnapshot("quickboost-before-pointer","Public fixture25 spins/unlock/previous consumed boost; real stale-quote spin and actual Busy UI spin, current quote15 is unarmed");
            yield return Click(quick,433);Assert.That(Session.Expansion.Energy,Is.EqualTo(pointerEnergy-15));
            Assert.That(Session.Expansion.BoostArmed,Is.True);Assert.That(Session.State.sequence,Is.EqualTo(pointerQuote.Sequence));
            Assert.That(quick.isActiveAndEnabled&&quick.gameObject.activeInHierarchy,Is.True);Assert.That(quick.IsInteractable(),Is.False);
            Assert.That(quick.GetComponentInChildren<TMP_Text>(true).text,Does.Contain("active"));
            Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.False);
            WholeLoopSnapshot("quickboost-pointer-armed","Fresh pointer explicitly spent15 Energy once; charge armed and AUTO remains off");
            before=State;files=Files();quick.onClick.Invoke();Unchanged(before,files);
            // Reuse the exact private pre-pointer snapshot for an independent Submit.
            File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(pointerStart));Call("Reload");yield return null;
            Assert.That(Session.Expansion.BoostArmed,Is.False);Assert.That(Session.Expansion.Energy,Is.EqualTo(pointerEnergy));
            var current = Quote("boost", "boost.next-bonus", 0, 0, 15, field: "expansionQuickBoostQuote");
            int parts = Session.Expansion.Parts, potatoes = Session.Expansion.Potatoes;
            long coins = Session.State.coins; uint seed = Session.State.seed;
            Submit(quick);
            Assert.That(Session.Expansion.Energy, Is.EqualTo(pointerEnergy-15)); Assert.That(Session.Expansion.BoostArmed, Is.True);
            Assert.That(Session.State.sequence, Is.EqualTo(current.Sequence));
            Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(current.Key));
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes }, Is.EqualTo(new[] { parts, potatoes }));
            Assert.That(Session.State.coins, Is.EqualTo(coins)); Assert.That(Session.State.seed, Is.EqualTo(seed));
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False, "Quick boost must remain on Game without a redundant confirmation menu");
            before = State; files = Files(); quick.onClick.Invoke(); quick.onClick.Invoke(); Unchanged(before, files);
            // The same permanent listener is legal in a later real AUTO gap.
            File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(pointerStart));Call("Reload");yield return null;
            yield return Click(Field<Button>("autoButton"));Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.True);
            float deadline=Time.realtimeSinceStartup+7;
            while((bool)Game.GetType().GetProperty("Busy").GetValue(Game)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.False);
            Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.True);
            int gapEnergy=Session.Expansion.Energy;long gapSequence=Session.State.sequence;Target(quick);
            yield return Click(quick,434);Assert.That(Session.Expansion.Energy,Is.EqualTo(gapEnergy-15));
            Assert.That(Session.Expansion.BoostArmed,Is.True);Assert.That(Session.State.sequence,Is.EqualTo(gapSequence+1));
            Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.True,"Boost must not stop AUTO");
            yield return Click(Field<Button>("autoButton"));Assert.That((bool)Game.GetType().GetProperty("AutoRunning").GetValue(Game),Is.False);
            yield return Harness("Finish");
            var shortSetup=(GameSession)typeof(CombinedLifecycleTests).GetMethod("Fixture",Hidden).Invoke(ui,new object[]{pointerStart});
            shortSetup.ArmExpansionBoost(shortSetup.State.sequence,1000);
            for(int spin=0;spin<5;spin++)shortSetup.ExecuteChapterSpin(shortSetup.State.sequence+1,1000);
            Assert.That(shortSetup.Expansion.BoostArmed,Is.False);Assert.That(shortSetup.Expansion.Energy,Is.LessThan(15));
            File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(shortSetup.State));Call("Reload");yield return null;
            Assert.That(quick.isActiveAndEnabled,Is.True);Assert.That(quick.IsInteractable(),Is.False);
            Assert.That(quick.GetComponentInChildren<TMP_Text>(true).text,Does.Contain("15").And.Contain("energy"));
            WholeLoopSnapshot("quickboost-energy-short","Publicly armed boost was consumed by five bounded Core spins; current Energy is below15 and no charge is armed");
            var locked=ModelState.Fresh(1);locked.lastSettledAt=1000;File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(locked));Call("Reload");yield return null;
            Assert.That(quick.isActiveAndEnabled,Is.True);Assert.That(quick.IsInteractable(),Is.False);
            Assert.That(quick.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo("Bonus"));
            WholeLoopSnapshot("quickboost-locked","Fresh private state has no bonus unlock and the disabled control names that prerequisite");
            TestContext.WriteLine("Visible quick boost rejects held contacts across Product Shop and pause, stale quote and Busy callback; fresh pointer and independent Submit on the same restored private pre-pointer state each spend exactly15 once. Repeats preserve state/files, and a later legal AUTO-gap pointer spends15 without stopping AUTO. No physical input or human timing proof.");
        }

        [UnityTest]
        public IEnumerator HiddenPausedAndBusyExpansionActionsCannotDispatchOrReviveAQuote()
        {
            yield return PrepareResources();
            yield return Preview("module.salvage", 0); Quote("buy", "module.salvage", 12, 0, 0);
            var primary = Field<Button>("expansionPrimaryButton");
            string before = State; var files = Files();
            yield return Back(); primary.onClick.Invoke(); Unchanged(before, files);
            yield return Preview("module.salvage", 0); Quote("buy", "module.salvage", 12, 0, 0);
            Call("OnApplicationPause", true);
            primary.onClick.Invoke(); Field<Button[]>("expansionCards")[0].onClick.Invoke(); Unchanged(before, files);
            Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null, "Pause must discard the old economic quote");
            Call("OnApplicationPause", false); primary.onClick.Invoke(); Unchanged(before, files);
            Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null, "Resume must not reconstruct old authorization");
            yield return Back(); Assert.That(Field<GameObject>("panel").activeSelf, Is.False);

            yield return Click(Field<Button>("spinButton"));
            Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game), Is.True);
            before = State; files = Files();
            primary.onClick.Invoke(); Field<Button[]>("expansionCards")[0].onClick.Invoke();
            Field<Button>("expansionShopButton").onClick.Invoke(); Call("ShowExpansionHome");
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            Unchanged(before, files);
            yield return Harness("Finish");
            yield return Preview("module.salvage", 0);
            var current = Quote("buy", "module.salvage", 12, 0, 0);
            int parts = Session.Expansion.Parts, potatoes = Session.Expansion.Potatoes, energy = Session.Expansion.Energy;
            long coins = Session.State.coins; uint seed = Session.State.seed;
            yield return Click(primary);
            Assert.That(Session.Expansion.OwnsModule("module.salvage"), Is.True);
            Assert.That(Session.Expansion.ModuleAt(1), Is.Null, "Purchase must not silently equip");
            Assert.That(Session.State.sequence, Is.EqualTo(current.Sequence));
            Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(current.Key));
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { parts - 12, potatoes, energy }));
            Assert.That(Session.State.coins, Is.EqualTo(coins)); Assert.That(Session.State.seed, Is.EqualTo(seed));
            before = State; files = Files(); primary.onClick.Invoke(); Unchanged(before, files);
            TestContext.WriteLine("One normal-speed free UI spin only. Hidden, paused, resumed-old and Busy expansion callbacks preserve their complete current snapshot/files; no restored quote.");
        }
        [SetUp] public void Isolate() { ui.Isolate(); }
        [UnityTearDown] public IEnumerator Restore() { yield return ui.RestorePrivateContext(); }
    }
}
#endif
