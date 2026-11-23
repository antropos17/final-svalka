#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    [Category("ExpansionUtility5h")]
    public sealed class ExpansionUtilityUiTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly CombinedLifecycleTests ui = new CombinedLifecycleTests();
        readonly JsonStateCodec codec = new JsonStateCodec();
        Component Game => (Component)typeof(CombinedLifecycleTests).GetField("game", Hidden).GetValue(ui);
        string Folder => (string)typeof(CombinedLifecycleTests).GetField("directory", Hidden).GetValue(ui);
        GameSession Session => (GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        T Field<T>(string name) => (T)Game.GetType().GetField(name, Hidden).GetValue(Game);
        void Call(string name) => Game.GetType().GetMethod(name, Hidden | BindingFlags.Public).Invoke(Game, null);
        IEnumerator Harness(string name, params object[] args) =>
            (IEnumerator)typeof(CombinedLifecycleTests).GetMethod(name, Hidden).Invoke(ui, args);
        string State => codec.Encode(Session.State);
        Button Primary => Field<Button>("expansionPrimaryButton");
        Button Secondary => Field<Button>("expansionSecondaryButton");
        Dictionary<string, string> Files()
        {
            Assert.That(Directory.GetDirectories(Folder), Is.Empty);
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
            Assert.That(State, Is.EqualTo(state)); Assert.That(Files(), Is.EquivalentTo(files));
            Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
        }
        static Vector2 Center(Button button) => ((RectTransform)button.transform).rect.center;
        static RaycastResult Top(Button button, Vector2 local)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>();
            var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, button.transform.TransformPoint(local)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), button.name);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), button.name);
            return hits[0];
        }
        static void Target(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, button.name);
            var rect = ((RectTransform)button.transform).rect;
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(48), button.name); Assert.That(rect.height, Is.GreaterThanOrEqualTo(48), button.name);
            foreach (var point in new[] { rect.center, rect.min + Vector2.one * 4, rect.max - Vector2.one * 4,
                new Vector2(rect.xMin + 4, rect.yMax - 4), new Vector2(rect.xMax - 4, rect.yMin + 4) }) Top(button, point);
        }
        static PointerEventData Down(Button button, int id)
        {
            Target(button); var hit = Top(button, Center(button));
            var data = new PointerEventData(EventSystem.current) { pointerId = id, button = PointerEventData.InputButton.Left,
                eligibleForClick = true, pointerCurrentRaycast = hit, position = hit.screenPosition };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            data.pointerPress = button.gameObject; data.pointerClick = button.gameObject; return data;
        }
        static void Release(PointerEventData data, Button current)
        {
            data.pointerCurrentRaycast = Top(current, Center(current)); data.position = data.pointerCurrentRaycast.screenPosition;
            bool candidate = data.eligibleForClick && data.pointerClick == ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject);
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            if (candidate) ExecuteEvents.Execute(data.pointerClick, data, ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Click(Button button, int id = 601) { yield return null; Release(Down(button, id), button); }
        IEnumerator Back()
        {
            yield return null;
            var back = Game.GetComponents<Component>().Single(c => c.GetType().FullName == "JunkToNeon.Presentation.JtnBackNavigation");
            Assert.That((bool)back.GetType().GetMethod("RequestBack").Invoke(back, null), Is.True); yield return null;
        }
        void Fits()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in Field<RectTransform>("expansionRoot").GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, text.name + ": " + text.text);
                Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 2), text.name);
                Assert.That(text.textBounds.size.y, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name);
            }
            var panel = (RectTransform)Field<GameObject>("panel").transform;
            var bounds = new List<Bounds>();
            foreach (var button in Field<RectTransform>("expansionRoot").GetComponentsInChildren<Button>().Concat(new[] { Field<Button>("closeButton") }))
            {
                if (!button.isActiveAndEnabled) continue;
                var rect = (RectTransform)button.transform;
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(48)); Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(48));
                var current = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, rect);
                foreach (var previous in bounds) Assert.That(current.Intersects(previous), Is.False, button.name + " overlaps another control");
                bounds.Add(current);
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                { var local = panel.InverseTransformPoint(corner); Assert.That(panel.rect.Contains(new Vector2(local.x, local.y)), Is.True, button.name); }
                if (button.IsInteractable()) Target(button);
            }
        }
        IEnumerator Home()
        {
            if(Field<GameObject>("panel").activeSelf&&Field<int>("wavePanel")==50)yield break;
            yield return ReturnToGame();
            yield return Click(Field<Button>("productAvatar"));
            yield return Click(Field<Button[]>("productCards")[2]);
            yield return Click(Field<Button>("expansionEntryButton"));
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(50)); yield return null;
        }
        IEnumerator ReturnToGame()
        {
            for (int depth = 0; Field<GameObject>("panel").activeSelf && depth < 6; depth++) yield return Back();
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
        }
        IEnumerator Category(int category)
        {
            if(category==1){yield return ReturnToGame();yield return Click(Field<Button[]>("productTabs")[3]);yield return Click(Field<Button[]>("productCards")[1]);}
            else{yield return Home();yield return Click(Field<Button[]>("expansionCards")[category]);}
            yield return null;Fits();
        }
        IEnumerator Offer(string id, int category)
        {
            string before = State; var files = Files(); yield return Category(category);
            int index = Array.IndexOf(Field<string[]>("expansionCardIds"), id); Assert.That(index, Is.GreaterThanOrEqualTo(0), id);
            yield return Click(Field<Button[]>("expansionCards")[index]); yield return null; Fits(); Unchanged(before, files);
        }
        ExpansionQuote Quote(string action, string id, int parts = 0, int potatoes = 0, int energy = 0, int slot = 0)
        {
            var q = Field<ExpansionQuote>("expansionQuote"); Assert.That(q, Is.Not.Null);
            Assert.That(q.Action, Is.EqualTo(action)); Assert.That(q.Id, Is.EqualTo(id)); Assert.That(q.Slot, Is.EqualTo(slot));
            Assert.That(q.ExpectedSequence, Is.EqualTo(Session.State.sequence)); Assert.That(q.Sequence, Is.EqualTo(Session.State.sequence + 1));
            Assert.That(new[] { (long)q.PartsCost, q.PotatoesCost, q.EnergyCost, q.CoinsCost, q.CoinsReward }, Is.EqualTo(new long[] { parts, potatoes, energy, 0, 0 }));
            return q;
        }
        sealed class Memory : ISnapshotStorage
        {
            public string raw; public string ReadPrimary() => raw; public string ReadBackup() => null; public void Write(string value) { raw = value; }
        }
        GameSession Funded(int parts = 200, int potatoes = 200, int energy = 10, long oldPrice = 0,
            bool allFamilies=false,bool complete=false,long? wallet=null)
        {
            ModelState state;
            if (oldPrice == 540)
                state = codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../Checks/ConstructionPricing/evidence/cadence-r12/v2/v2-selected540.json"))));
            else
            {
                state = ModelState.Fresh(1); state.houseStage = 1; state.autoHintSeen = 1;
                if (oldPrice != 0) { Assert.That(oldPrice, Is.EqualTo(600)); state.selectedStage = 2; }
            }
            if(allFamilies||complete)
            {
                Assert.That(oldPrice,Is.Zero);
                string era=complete?EraIds.WorkshopYard:EraIds.Junkyard;int level=complete?5:1;
                var progress=ChapterCatalog.Rules.FromStages(new FamilyStages(era,FamilyIds.All.Select(id=>new FamilyStage(id,level))));
                long entry=complete?13:0;state.sequence=progress.CompletedUpgradeIds.Count;state.revision=state.sequence*2;
                state.houseStage=Math.Min(level,2);state.workshopBuilt=1;state.autoHintSeen=1;
                state.wave=new WaveProgress(1,1,WaveProgress.Empty.Delivery,JunkToNeon.Upgrades.UpgradeOwnership.Empty);
                state.chapter=new ChapterState(2,JunkToNeon.Content.ChapterContentIds.RuleRevision,progress,JunkToNeon.Upgrades.SkillOwnership.Empty,null,entry,entry);
            }
            state.coins = wallet??(oldPrice == 0 ? 1000 : oldPrice - 5); state.lastSettledAt = 1000; GameLogic.Validate(state);
            var setup = GameSession.Open(new GameStore(new Memory { raw = codec.Encode(state) }, codec), 1, initializeJourney: true);
            setup.SelectExpansionSpeed("speed.fast", setup.State.sequence, 1000); state = setup.State;
            // Clearly synthetic private initial funding through the public legacy
            // codec; actual free adoption receipt is retained. No earned history.
            var f = ExpansionCodec.EncodeState(state.expansion).Split('|');
            Assert.That(f[0], Is.EqualTo("expansion.state.v1")); Assert.That(f.Length, Is.EqualTo(21));
            f[1] = parts.ToString(System.Globalization.CultureInfo.InvariantCulture);
            f[2] = potatoes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            f[3] = energy.ToString(System.Globalization.CultureInfo.InvariantCulture);
            state.expansion = ExpansionCodec.DecodeState(string.Join("|", f)); GameLogic.Validate(state);
            Assert.That(state.expansion.ForegroundSpins, Is.Zero); Assert.That(state.expansion.LastResourceReceipt, Is.Null);
            return GameSession.Open(new GameStore(new Memory { raw = codec.Encode(state) }, codec), 1, initializeJourney: true);
        }
        IEnumerator Load(GameSession prepared)
        {
            if(Game==null)yield return Harness("Load",prepared.State);
            else
            {
                Assert.That(Session.RecoveryRequired,Is.False);Assert.That(Field<GameObject>("panel"),Is.Not.Null);
                File.WriteAllText(Path.Combine(Folder,"jtn.save.v1.json"),codec.Encode(prepared.State));Call("Reload");
            }
            yield return null;Assert.That(Session.RecoveryRequired,Is.False,Session.Error);
            Assert.That(State,Is.EqualTo(codec.Encode(prepared.State)));Assert.That(Field<GameObject>("panel").activeSelf,Is.False);
        }
        static string[] Slots(ExpansionState view) => Enumerable.Range(1, 3).Select(view.ModuleAt).ToArray();
        void OldGoal(long price)
        {
            Assert.That(Session.State.coins, Is.EqualTo(price - 5)); Assert.That(Session.State.seed, Is.EqualTo(1u));
            Assert.That(Session.State.houseStage, Is.EqualTo(1)); Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo("house.improve"));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(price));
        }
        ModelState OlderModel(ModelState state)
        {
            Assert.That(state.pending, Is.Null); Assert.That(state.lastSpin, Is.Null);
            var copy = state.Copy(); copy.expansion = null; return copy;
        }
        IEnumerator Buy(string id, int category, int parts, int potatoes)
        {
            yield return Offer(id, category); var q = Quote("buy", id, parts, potatoes);
            if (id.StartsWith("workbench.", StringComparison.Ordinal) || id == "theme.frost" || id == "theme.rose")
                Assert.That(q.RuleRevision, Is.EqualTo("expansion.rules.v2"));
            int p = Session.Expansion.Parts, t = Session.Expansion.Potatoes, e = Session.Expansion.Energy;
            long coins = Session.State.coins; uint seed = Session.State.seed;
            yield return Click(Primary); yield return null; Fits();
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { p - parts, t - potatoes, e }));
            Assert.That(Session.State.coins, Is.EqualTo(coins)); Assert.That(Session.State.seed, Is.EqualTo(seed));
            Assert.That(Session.State.sequence, Is.EqualTo(q.Sequence)); Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(q.Key));
        }
        [UnityTest]
        public IEnumerator PaidWorkbenchAndPotatoFocusProduceOnePresentedFourteenUnitBoostedBonus()
        {
            yield return Load(Funded(energy: 20));
            yield return Buy("bonus.unlock", 5, 20, 10);
            yield return Buy("workbench.bonus.1", 5, 30, 20); Assert.That(Session.Expansion.BonusWorkbenchLevel, Is.EqualTo(1));
            yield return Buy("workbench.bonus.2", 5, 40, 30); Assert.That(Session.Expansion.BonusWorkbenchLevel, Is.EqualTo(2));
            yield return Offer("focus.potatoes", 5); var focus = Quote("focus", "focus.potatoes");
            Assert.That(focus.RuleRevision, Is.EqualTo("expansion.rules.v2"));
            yield return Click(Primary); Assert.That(Session.State.sequence, Is.EqualTo(focus.Sequence));
            Assert.That(Session.Expansion.BonusFocusId, Is.EqualTo("focus.potatoes"));
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 110, 140, 20 }));
            Assert.That(Slots(Session.Expansion), Is.EqualTo(new string[] { null, null, null }));
            yield return Offer("boost.next-bonus", 5); var boost = Quote("boost", "boost.next-bonus", energy: 15);
            yield return Click(Primary); Assert.That(Session.State.sequence, Is.EqualTo(boost.Sequence));
            Assert.That(Session.Expansion.Energy, Is.EqualTo(5)); Assert.That(Session.Expansion.BoostArmed, Is.True);

            // Four public foreground operations prepare 4/5; only the fifth is
            // claimed as a real Runtime/uGUI presentation. No time/RNG bypass.
            for (int spin = 0; spin < 4; spin++) Session.ExecuteChapterSpin(Session.State.sequence + 1, 1000);
            Assert.That(Session.State.seed, Is.EqualTo(1495045943u)); Assert.That(Session.State.coins, Is.EqualTo(1032));
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 116, 146, 9 }));
            Assert.That(Session.Expansion.BonusProgress, Is.EqualTo(4)); Assert.That(Session.Expansion.BonusRolls, Is.Zero);
            yield return ReturnToGame(); int frames = (int)Game.GetType().GetProperty("PresentedFrames").GetValue(Game);
            yield return Click(Field<Button>("spinButton"));
            Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game), Is.True);
            Assert.That(Field<TMP_Text>("expansionBonusResult").text, Is.EqualTo("…"), "Current bonus may not be revealed ahead of presentation");
            yield return Harness("Finish");
            Assert.That((int)Game.GetType().GetProperty("PresentedFrames").GetValue(Game), Is.GreaterThan(frames));
            Assert.That(Session.State.seed, Is.EqualTo(87470069u)); Assert.That(Session.State.coins, Is.EqualTo(1040));
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 117, 162, 10 }));
            var receipt = Session.Expansion.LastResourceReceipt;
            Assert.That(new[] { receipt.Parts, receipt.Potatoes, receipt.Energy, receipt.BonusParts, receipt.BonusPotatoes }, Is.EqualTo(new[] { 1, 16, 1, 0, 14 }));
            Assert.That(receipt.BonusSymbol, Is.Zero); Assert.That(receipt.Boosted, Is.True); Assert.That(Session.Expansion.BoostArmed, Is.False);
            Assert.That(Session.Expansion.BonusSeed, Is.EqualTo(686983322u)); Assert.That(Session.Expansion.BonusRolls, Is.EqualTo(1));
            Assert.That(Session.Expansion.BonusProgress, Is.Zero);
            Assert.That(Field<TMP_Text>("expansionBonusResult").text, Does.Contain("Parts +0").And.Contain("Potatoes +14").And.Contain("×2 included"));
            Assert.That(Field<TMP_Text>("expansionBonusResult").text,Does.Not.Contain("Boost used"));
            yield return PurchaseChoicePortrait.Capture(Game,"positive-boost-zero-fourteen","expansionBonusResult");
            string before = State; var files = Files(); yield return Home(); yield return Click(Secondary); yield return null; Fits();
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("Workbench 2/2").And.Contain("focus: Potatoes")); Unchanged(before, files);
            TestContext.WriteLine("One presented fifth bonus: raw5/0 +workbenchII1/1 ->focus0/7 ->boost0/14; base1Part2Potatoes separately yields total1/16, no cap clipping or second multiplier.");
        }
        GameSession FullBonusBoundary(bool boosted,int spins)
        {
            var setup=Funded(energy:20);setup.BuyExpansion(setup.GetExpansionOffer("bonus.unlock"),1000);
            // Declared synthetic initial full stores, before any resource spin
            // history; the actual arming and subsequent bonus use public actions.
            var state=setup.State;var f=ExpansionCodec.EncodeState(state.expansion).Split('|');
            Assert.That(f[0],Is.EqualTo("expansion.state.v1"));Assert.That(f.Length,Is.EqualTo(21));f[1]="200";f[2]="200";
            state.expansion=ExpansionCodec.DecodeState(string.Join("|",f));GameLogic.Validate(state);
            setup=GameSession.Open(new GameStore(new Memory{raw=codec.Encode(state)},codec),1,initializeJourney:true);
            if(boosted)setup.ExecuteExpansion(setup.GetExpansionAction("boost",ExpansionCatalog.NextBonusBoost),1000);
            for(int i=0;i<spins;i++)setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
            Assert.That(setup.Expansion.Parts,Is.EqualTo(200));Assert.That(setup.Expansion.Potatoes,Is.EqualTo(200));return setup;
        }
        [UnityTest] public IEnumerator BoostedZeroBonusExplainsUsedChargeOnlyAfterReveal()
        {
            foreach(var item in new[]{new{boosted=true,spins=4},new{boosted=false,spins=4},new{boosted=true,spins=3}})
            {
                yield return Load(FullBonusBoundary(item.boosted,item.spins));
                Assert.That(Field<TMP_Text>("expansionBonusResult").text,Does.Not.Contain("Boost used"));
                long diamonds=Session.Arcade.Diamonds;int energy=Session.Expansion.Energy;long sequence=Session.State.sequence;
                Assert.That(energy,Is.EqualTo(item.boosted?5+item.spins:20+item.spins));
                yield return Click(Field<Button>("spinButton"));Assert.That((bool)Game.GetType().GetProperty("Busy").GetValue(Game),Is.True);
                Assert.That(Field<TMP_Text>("expansionBonusResult").text,Is.EqualTo("…"));
                Game.GetType().GetMethod("Render",Hidden).Invoke(Game,new object[]{null,null});Assert.That(Field<TMP_Text>("expansionBonusResult").text,Is.EqualTo("…"));
                yield return Harness("Finish");var r=Session.Expansion.LastResourceReceipt;
                Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Assert.That(Session.Expansion.Energy,Is.EqualTo(energy+1));
                Assert.That(Session.Arcade.Diamonds,Is.EqualTo(diamonds));Assert.That(r.BonusParts,Is.Zero);Assert.That(r.BonusPotatoes,Is.Zero);
                Assert.That(Session.Expansion.BoostArmed,Is.EqualTo(item.boosted&&item.spins==3));
                if(item.spins==4)
                {
                    Assert.That(r.BonusSymbol,Is.Zero);Assert.That(r.Boosted,Is.EqualTo(item.boosted));
                    Assert.That(Field<TMP_Text>("expansionBonusResult").text,Is.EqualTo("Parts +0\nPotatoes +0"+(item.boosted?"\nBoost used":"")));
                    if(item.boosted)yield return PurchaseChoicePortrait.Capture(Game,"boosted-zero-revealed","expansionBonusResult");
                }
                else{Assert.That(r.BonusSymbol,Is.EqualTo(-1));Assert.That(r.Boosted,Is.False);Assert.That(Field<TMP_Text>("expansionBonusResult").text,Is.EqualTo("Boost\nwaiting"));}
                string before=State;var files=Files();string text=Field<TMP_Text>("expansionBonusResult").text;
                int effects=(int)Game.GetType().GetProperty("RewardFeedbackVisibleCount").GetValue(Game);
                Game.GetType().GetMethod("Render",Hidden).Invoke(Game,new object[]{null,null});Assert.That(Field<TMP_Text>("expansionBonusResult").text,Is.EqualTo(text));
                Assert.That((int)Game.GetType().GetProperty("RewardFeedbackVisibleCount").GetValue(Game),Is.EqualTo(effects));Unchanged(before,files);
                Call("Reload");yield return null;Unchanged(before,files);Assert.That(Field<TMP_Text>("expansionBonusResult").text,Does.Not.Contain("Boost used"));
            }
        }
        [UnityTest]
        public IEnumerator OwnedPresetsAreReadOnlyUntilFreshConsentAndPreserveSaved540And600Goals()
        {
            foreach (long price in new[] { 540L, 600L })
            {
                if (price == 600) { yield return ui.RestorePrivateContext(); ui.Isolate(); }
                var setup = Funded(oldPrice: price);
                foreach (string id in new[] { "module.salvage", "module.harvest" })
                    setup.BuyExpansion(setup.GetExpansionOffer(id), 1000);
                yield return Load(setup); OldGoal(price);
                Assert.That(ExpansionEconomy.PreviewPreset(Session.Expansion, "preset.energy"), Is.EqualTo(new string[] { "module.salvage", null, null }));
                string before = State; var files = Files();
                yield return Offer("preset.energy", 1);
                Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("Slot 2: locked").And.Contain("Slot 3: locked"));
                yield return Back(); Unchanged(before, files);
                Assert.That(Session.Expansion.OwnsModule("module.dynamo") || Session.Expansion.OwnsModule("module.combo"), Is.False);
                foreach (string id in new[] { "module.packing", "module.combo", "module.dynamo", "socket.2", "socket.3" })
                    Session.BuyExpansion(Session.GetExpansionOffer(id), 1000);
                string[] parts = { "module.salvage", "module.packing", "module.combo" };
                for (int slot = 1; slot <= 3; slot++) Session.EquipExpansionModule(slot, parts[slot - 1], Session.State.sequence, 1000);
                OldGoal(price);
                Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 46, 144, 10 }));
                before = State; files = Files();
                Assert.That(ExpansionEconomy.PreviewPreset(Session.Expansion, "preset.parts"), Is.EqualTo(parts));
                yield return Offer("preset.parts", 1); Assert.That(Primary.gameObject.activeInHierarchy, Is.False);
                Primary.onClick.Invoke(); Session.ApplyExpansionPreset("preset.parts", Session.State.sequence, 1000);
                Unchanged(before, files); Assert.That(Session.Expansion.FormatVersion, Is.EqualTo(1), "No-op preview must not adopt utilityV2");
                yield return Back(); Unchanged(before, files);

                yield return Offer("preset.harvest", 1); Quote("preset", "preset.harvest");
                var button = Primary; var held = Down(button, 611); var oldEvent = button.onClick;
                yield return Offer("preset.energy", 1); var current = Quote("preset", "preset.energy");
                Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Energy per Pigeon spin: +0 → +1").And.Contain("Pigeon spin = one or more Pigeons; bonus once."));
                if(price==540)yield return PurchaseChoicePortrait.Capture(Game,"owned-energy-preview","expansionBody");
                Assert.That(Primary, Is.SameAs(button)); before = State; files = Files();
                var older = OlderModel(Session.State);
                Release(held, button); Unchanged(before, files);
                yield return Click(button, 612);
                Assert.That(Slots(Session.Expansion), Is.EqualTo(new[] { "module.dynamo", "module.combo", "module.salvage" }));
                Assert.That(Session.State.sequence, Is.EqualTo(current.Sequence)); Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(current.Key));
                Assert.That(Session.Expansion.FormatVersion, Is.EqualTo(2)); OldGoal(price);
                if(price==540)yield return PurchaseChoicePortrait.Capture(Game,"owned-energy-result","expansionBody");
                older.sequence = current.Sequence; older.revision = older.sequence * 2;
                Assert.That(codec.Encode(OlderModel(Session.State)), Is.EqualTo(codec.Encode(older)), "Preset changes no older game facts beyond normal sequence/revision");
                Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 46, 144, 10 }));
                before = State; files = Files(); oldEvent.Invoke(); Unchanged(before, files);

                yield return Offer("preset.harvest", 1); Quote("preset", "preset.harvest");
                Session.EquipExpansionModule(1, "module.harvest", Session.State.sequence, 1000);
                before = State; files = Files(); Primary.onClick.Invoke(); Unchanged(before, files); OldGoal(price);
                Assert.That(Slots(Session.Expansion), Is.EqualTo(new[] { "module.harvest", "module.combo", "module.salvage" }),
                    "A stale preset must not overwrite the newer legitimate manual loadout");
            }
        }
        Image[] Frames() => Field<Image[]>("reelImages").Select(image => image.transform.parent.GetComponent<Image>()).ToArray();
        void FrameColors(Color[] expected)
        {
            var frames = Frames(); Assert.That(frames.Length, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
                Assert.That(Vector4.Distance(frames[i].color, expected[i]), Is.LessThan(.0001f), "Primary frame " + i + " tint");
        }
        [UnityTest]
        public IEnumerator NewPalettePurchasesNeedSeparateApplyAndEarlierReloadRestoresAuthoredFrames()
        {
            yield return Load(Funded()); string earlier = State;
            var authored = Frames().Select(frame => frame.color).ToArray();
            string[] ids = { "theme.frost", "theme.rose" };
            Color[] colors = { new Color(226f / 255, 242f / 255, 243f / 255, 1), new Color(248f / 255, 229f / 255, 234f / 255, 1) };
            for (int index = 0; index < 2; index++)
            {
                string previousTheme = index == 0 ? "theme.neon" : "theme.frost";
                var previousColors = Frames().Select(frame => frame.color).ToArray();
                yield return Buy(ids[index], 2, 15, 5);
                Assert.That(Session.Expansion.OwnsTheme(ids[index]), Is.True);
                Assert.That(Session.Expansion.ThemeId, Is.EqualTo(previousTheme), "Ownership purchase must not silently apply");
                FrameColors(previousColors);
                yield return Offer(ids[index], 2); var apply = Quote("theme", ids[index]);
                int parts = Session.Expansion.Parts, potatoes = Session.Expansion.Potatoes, energy = Session.Expansion.Energy;
                yield return Click(Primary); yield return null;
                Assert.That(Session.Expansion.ThemeId, Is.EqualTo(ids[index]));
                Assert.That(Session.State.sequence, Is.EqualTo(apply.Sequence)); Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(apply.Key));
                Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { parts, potatoes, energy }));
                var expected = Enumerable.Repeat(colors[index], 3).ToArray(); FrameColors(expected);
                string saved = State; var files = Files(); Call("Reload"); yield return null;
                Unchanged(saved, files); Assert.That(Session.Expansion.ThemeId, Is.EqualTo(ids[index])); FrameColors(expected);
            }
            Assert.That(Session.Expansion.FormatVersion, Is.EqualTo(2));
            // Actual earlier private snapshot, not a reconstructed ownership flag.
            File.WriteAllText(Path.Combine(Folder, "jtn.save.v1.json"), earlier);
            var olderFiles = Files(); Call("Reload"); yield return null;
            Unchanged(earlier, olderFiles); Assert.That(Session.Expansion.ThemeId, Is.EqualTo("theme.neon"));
            Assert.That(Session.Expansion.OwnsTheme("theme.frost") || Session.Expansion.OwnsTheme("theme.rose"), Is.False);
            Assert.That(Session.Expansion.FormatVersion, Is.EqualTo(1)); FrameColors(authored);
        }
        string[] VisibleIds()
        {
            var cards = Field<Button[]>("expansionCards");
            return Field<string[]>("expansionCardIds").Where((id, index) => cards[index].gameObject.activeInHierarchy).ToArray();
        }
        [UnityTest]
        public IEnumerator AffordablePurchaseFilterAndCurrentLoadoutInfoAreReadOnlyAndExplainAnEmptyResult()
        {
            var setup = Funded(parts: 32, potatoes: 10, energy: 0);
            setup.BuyExpansion(setup.GetExpansionOffer("equipment.house.1"), 1000);
            yield return Load(setup);
            string before = State; var files = Files();
            yield return Category(0);
            Assert.That(VisibleIds(), Is.EquivalentTo(new[] { "module.salvage", "module.packing", "module.harvest", "module.combo", "module.dynamo" }));
            yield return Click(Secondary); yield return null; Fits();
            Assert.That(VisibleIds(), Is.EquivalentTo(new[] { "module.salvage", "module.packing" }));
            int index = Array.IndexOf(Field<string[]>("expansionCardIds"), "module.salvage");
            yield return Click(Field<Button[]>("expansionCards")[index]); yield return null; Fits();
            Quote("buy", "module.salvage", 12); yield return Back();
            Assert.That(Field<int>("expansionFilteredCategory"), Is.EqualTo(0));
            Assert.That(VisibleIds(), Is.EquivalentTo(new[] { "module.salvage", "module.packing" })); Unchanged(before, files);
            yield return Home(); Assert.That(Field<int>("expansionFilteredCategory"), Is.EqualTo(-1)); Unchanged(before, files);

            // Genuine production buy/equip establish a real current loadout; this
            // is separate from the read-only filter/info observations that follow.
            Session.BuyExpansion(Session.GetExpansionOffer("module.salvage"), 1000);
            Session.EquipExpansionModule(1, "module.salvage", Session.State.sequence, 1000);
            before = State; files = Files();
            yield return Category(0); yield return Click(Secondary); yield return null; Fits();
            Assert.That(VisibleIds(), Is.Empty, "Owned free-to-equip modules are not new affordable purchases");
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("Nothing available to buy"));
            Assert.That(Session.Expansion.Parts, Is.Zero); Assert.That(Session.Expansion.FormatVersion, Is.EqualTo(1)); Unchanged(before, files);
            yield return Click(Primary); yield return null; Fits();
            Assert.That(Field<int>("expansionFilteredCategory"), Is.EqualTo(-1));
            Assert.That(Field<int>("expansionOwnedCategory"), Is.EqualTo(0));
            Assert.That(VisibleIds(), Is.EqualTo(new[] { "module.salvage" })); Unchanged(before, files);
            yield return Click(Primary); yield return null;
            Assert.That(VisibleIds().Length, Is.EqualTo(5)); Unchanged(before, files);
            yield return Home(); yield return Click(Secondary); yield return null; Fits();
            string body = Field<TMP_Text>("expansionBody").text;
            Assert.That(body, Does.Contain("Slot 1: Salvage Module").And.Contain("Slot 2: locked").And.Contain("Slot 3: locked"));
            foreach (string equipment in new[] { "House 1/2", "Workshop 0/2", "Recycler 0/2", "Pigeon Loft 0/2", "Cart 0/2", "Power Source 0/2" })
                Assert.That(body, Does.Contain(equipment));
            Assert.That(body, Does.Contain("20 parts").And.Contain("10 potatoes"));
            Primary.onClick.Invoke(); Field<Button>("selectButton").onClick.Invoke(); Unchanged(before, files);
            yield return Back(); Unchanged(before, files);
            Call("Reload"); yield return null; Unchanged(before, files);
            yield return Category(0); Assert.That(Field<int>("expansionFilteredCategory"), Is.EqualTo(-1));
            Assert.That(VisibleIds().Length, Is.EqualTo(5)); Unchanged(before, files);
        }
        IEnumerator ResourceClick(Button button, int id = 901)
        {
            yield return Click(button, id); int frame = Time.frameCount; yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
        }
        GameSession AllEquipmentCheckpoint()
        {
            // Exact existing producer checkpoint, SHA-pinned/portable; declared
            // synthetic initial funding, then real Core commands. No state setter.
            const string raw = "eyJzY2hlbWFWZXJzaW9uIjoxLCJjb250ZW50VmVyc2lvbiI6MSwicmV2aXNpb24iOjk0LCJzZXF1ZW5jZSI6NDcsImNvaW5zIjo2OTAsImhvdXNlU3RhZ2UiOjIsInNlbGVjdGVkU3RhZ2UiOjAsInNlZWQiOjEsImF1dG9IaW50U2VlbiI6MCwid29ya3Nob3BCdWlsdCI6MSwibGFzdFNldHRsZWRBdCI6MCwibGFzdFNldHRsZW1lbnRTZWVuIjowLCJsYXN0U2V0dGxlbWVudCI6bnVsbCwibGFzdFNwaW4iOm51bGwsInBlbmRpbmciOm51bGwsIndhdmUiOnsiZm9ybWF0IjoxLCJyZWN5Y2xlciI6MSwicGlnZW9uTG9mdCI6MSwiZGVsaXZlcnkiOnsic2VxdWVuY2UiOjAsImNhcnJ5IjowLCJjYWRlbmNlIjozLCJrZXkiOm51bGx9LCJzbG90Ijp7InZlcnNpb24iOjEsImxldmVsIjowLCJyZXZpc2lvbiI6MCwic2VxdWVuY2UiOjAsIndhbGxldEJlZm9yZSI6MCwiaG91c2VTdGFnZSI6MH19LCJjaGFwdGVyIjp7ImZvcm1hdCI6MiwicnVsZSI6ImNoYXB0ZXIucnVsZXMudjEiLCJlcmEiOiJ3b3Jrc2hvcC15YXJkIiwic3RhZ2VzIjpbNSw1LDUsNSw1LDVdLCJza2lsbHMiOiJ7XCJ2ZXJzaW9uXCI6MixcInJlY2VpcHRzXCI6W119Iiwic2VsZWN0ZWQiOm51bGwsImVudHJ5IjoxMywic2VlbiI6MTMsInByaWNpbmciOiJjb25zdHJ1Y3Rpb24ucHJpY2VzLnYzIn0sImpvdXJuZXkiOnsiZm9ybWF0IjoxLCJydWxlIjoiam91cm5leS5ydWxlcy52MSIsImFkb3B0ZWQiOjEsIm9yZGVycyI6Im9yZGVycy1zdGF0ZS52MXwyN3xvcmRlci5nYXRoZXItcG90YXRvZXN8MTJ8MzJ8MzJ8OGM1Y2Y2ZjkyZGQwNGE1ZGJjOTU4NmYxOTcxMmEzYTA3NTZhYmJiNjhiNTk3NDJjOTVlYjcyNWI1ZmM4ZmNiYnwzMiwzMSwwLDAsMCwwfC0iLCJndWlkYW5jZSI6ImcxfDF8MHwwfDI2fDZmMmQzM2Y5MmRiYjQxN2FmMTY0MzUzMTViZmE1ZWJmODM4OGVhZDZmNTE1ZjA4ODU3NDg3MzY3ZGVhYzNiOTh8cG93ZXItc291cmNlLndvcmtzaG9wLXlhcmQudXBncmFkZS0yfDI2fDB8LXwwIiwiY29tbWFuZFNlcXVlbmNlIjoyNywiY29tbWFuZEtpbmQiOiJvcmRlci1hY2NlcHQiLCJjb21tYW5kSWQiOiJvcmRlci5nYXRoZXItcG90YXRvZXMiLCJjb21tYW5kSW5zdGFuY2UiOjAsImNvbW1hbmRDb21wbGV0aW9uIjowfSwiZXhwYW5zaW9uIjoiZXhwYW5zaW9uLnN0YXRlLnYxfDE1NXwxNTV8MjB8MHwxfDF8MnwwfDB8MHwwfDI3Mzg5NTg3MDB8MHx0aGVtZS5uZW9ufDMzfC0sLSwtfDIsMiwyLDIsMiwyfC18NDcsNTQ4ZDVjZjlkMTIyYTNjNmIyOGI4MjQyZGJiNmIwZWEyZjI0ZTkwZGFkOTQ4ZTQxNWE5MzRkZTdlMzM3YTM4NixidXkscG90YXRvLnlpZWxkLjIsMHxzcGVlZC5mYXN0In0=";
            var bytes = Convert.FromBase64String(raw);
            using (var sha = System.Security.Cryptography.SHA256.Create())
                Assert.That(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""), Is.EqualTo("65BF5DB79D8C1CA0430C51E304CA15120594DA37481AD976762432B4087602BB"));
            return GameSession.Open(new GameStore(new Memory { raw = System.Text.Encoding.UTF8.GetString(bytes) }, codec), 1);
        }
        [UnityTest, Category("ResourcePolish10h")]
        public IEnumerator PurchasedSocketReturnsToItsModuleAndNeedsFreshSeparateInstallation()
        {
            var setup = Funded(oldPrice: 540);
            foreach (string id in new[] { "module.salvage", "module.packing" }) setup.BuyExpansion(setup.GetExpansionOffer(id), 1000);
            yield return Load(setup); OldGoal(540); var lower = OlderModel(Session.State);
            Assert.That(Session.Expansion.Parts, Is.EqualTo(176)); Assert.That(Slots(Session.Expansion), Is.EqualTo(new string[] { null, null, null }));
            yield return Offer("module.salvage", 0); string before = State; var files = Files();
            yield return ResourceClick(Primary); Fits(); Assert.That(Field<int>("wavePanel"), Is.EqualTo(53));
            Assert.That(Field<string>("expansionModuleToEquip"), Is.EqualTo("module.salvage"));
            yield return ResourceClick(Field<Button[]>("expansionCards")[1]); Fits(); var socket = Quote("buy", "socket.2", 30); Unchanged(before, files);
            var oldConfirm = Primary.onClick; var held = Down(Primary, 911); yield return ResourceClick(Primary, 912);
            Assert.That(Session.State.sequence, Is.EqualTo(socket.Sequence)); Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(socket.Key));
            Assert.That(Session.Expansion.Parts, Is.EqualTo(146)); Assert.That(Session.Expansion.SocketCount, Is.EqualTo(2));
            Assert.That(Slots(Session.Expansion), Is.EqualTo(new string[] { null, null, null }), "A socket purchase must never install automatically"); OldGoal(540);
            before = State; files = Files(); yield return Back(); Fits(); Unchanged(before, files);
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(53)); Assert.That(Field<TMP_Text>("expansionTitle").text, Is.EqualTo("Where should the module be installed?"));
            Assert.That(Field<string>("expansionModuleToEquip"), Is.EqualTo("module.salvage"));
            Assert.That(Field<Button[]>("expansionCards")[1].GetComponentInChildren<TMP_Text>().text, Does.Contain("Empty · install"));
            yield return ResourceClick(Field<Button[]>("expansionCards")[1]); Fits(); Unchanged(before, files);
            var equip = Field<ExpansionQuote>("expansionQuote"); Assert.That(equip.Action, Is.EqualTo("equip")); Assert.That(equip.Id, Is.EqualTo("module.salvage"));
            Assert.That(equip.Slot, Is.EqualTo(2)); Assert.That(equip.ExpectedSequence, Is.EqualTo(Session.State.sequence));
            Assert.That(new[] { equip.PartsCost, equip.PotatoesCost, equip.EnergyCost }, Is.EqualTo(new[] { 0, 0, 0 }));
            Release(held, Primary); Unchanged(before, files); oldConfirm.Invoke(); Unchanged(before, files);
            Assert.That(Field<ExpansionQuote>("expansionQuote").Key, Is.EqualTo(equip.Key));
            yield return ResourceClick(Primary, 913);
            Assert.That(Session.State.sequence, Is.EqualTo(equip.Sequence)); Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(equip.Key));
            Assert.That(Slots(Session.Expansion), Is.EqualTo(new string[] { null, "module.salvage", null }));
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 146, 200, 10 })); OldGoal(540);
            lower.sequence += 2; lower.revision = lower.sequence * 2;
            Assert.That(codec.Encode(OlderModel(Session.State)), Is.EqualTo(codec.Encode(lower)));

            yield return Offer("module.packing", 0); yield return ResourceClick(Primary); yield return ResourceClick(Field<Button[]>("expansionCards")[2]); Fits();
            Quote("buy", "socket.3", 60, 30); var stale = Primary.onClick;
            Session.SelectExpansionSpeed("speed.normal", Session.State.sequence, 1000); before = State; files = Files();
            stale.Invoke(); Unchanged(before, files); Assert.That(Session.Expansion.SocketCount, Is.EqualTo(2));
            Assert.That(Field<string>("expansionModuleToEquip"), Is.Null, "A refused stale purchase must not restore installation intent");
            yield return Offer("module.packing", 0); yield return ResourceClick(Primary); yield return ResourceClick(Field<Button[]>("expansionCards")[2]);
            var paused = Primary.onClick; before = State; files = Files();
            Game.GetType().GetMethod("OnApplicationPause", Hidden).Invoke(Game, new object[] { true }); paused.Invoke(); Unchanged(before, files);
            Assert.That(Field<string>("expansionModuleToEquip"), Is.Null);
            Game.GetType().GetMethod("OnApplicationPause", Hidden).Invoke(Game, new object[] { false }); paused.Invoke(); Unchanged(before, files);
            int resumeFrame = Time.frameCount; yield return null; Assert.That(Time.frameCount, Is.GreaterThan(resumeFrame));
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True);
            Assert.That(Field<RectTransform>("expansionRoot").gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<bool>("expansionContextValid"), Is.False); Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null);
            Assert.That(new[] { Field<int>("expansionPrimaryAction"), Field<int>("expansionSecondaryAction") }, Is.EqualTo(new[] { 0, 0 }));
            Assert.That(Field<string>("expansionModuleToEquip"), Is.Null);
            Assert.That(Field<Button[]>("expansionCards").All(button => !button.interactable), Is.True);
            Assert.That(new[] { Primary, Secondary, Field<Button>("expansionPreviousButton"), Field<Button>("expansionNextButton") }.All(button => !button.interactable), Is.True);
            var coverTitle = Field<TMP_Text>("panelTitle"); var coverBody = Field<TMP_Text>("panelBody"); var coverClose = Field<Button>("closeButton");
            Assert.That(coverTitle.text, Is.EqualTo("Slot & Shop"));
            Assert.That(coverBody.text, Is.EqualTo("This window must be reopened. Tap “Close”, then choose “Sections” → “Slot & Shop”."));
            Assert.That(coverClose.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Close"));
            Canvas.ForceUpdateCanvases();
            foreach (var text in new[] { coverTitle, coverBody, coverClose.GetComponentInChildren<TMP_Text>() })
            {
                Assert.That(text.isActiveAndEnabled, Is.True); text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, text.name + ": " + text.text);
                Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 2), text.name);
                Assert.That(text.textBounds.size.y, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name);
            }
            Target(coverClose); Unchanged(before, files);
            yield return Back(); Call("Reload"); yield return null; Unchanged(before, files);
            Assert.That(Field<string>("expansionModuleToEquip"), Is.Null); Assert.That(Session.Expansion.ModuleAt(2), Is.EqualTo("module.salvage")); OldGoal(540);
            TestContext.WriteLine("Owned/uninstalled salvage ->closed2 ->explicit30Part buy ->Back returns same module's expanded picker, never auto-equips. Old held/captured confirm cannot equip the newer quote; fresh free equip is separate. Stale socket3 and pause/reload restore no module intent, spend/equip nothing.");
        }
        [UnityTest, Category("ResourcePolish10h")]
        public IEnumerator CompletedEquipmentExplainsItsFiniteLimitAndLowerReloadKeepsWorkbenchCopyTruthful()
        {
            var completed=AllEquipmentCheckpoint();completed.SettleOffline(1000);
            Assert.That(completed.State.lastSettledAt,Is.EqualTo(1000));Assert.That(completed.State.coins,Is.EqualTo(690));
            yield return Load(completed);
            string before = State; var files = Files(); string[] families = { "house", "workshop", "recycler", "pigeon-loft", "cart", "power-source" };
            foreach (string family in families) Assert.That(Session.Expansion.EquipmentLevel(family), Is.EqualTo(2));
            yield return Category(3); yield return ResourceClick(Secondary); Fits(); Assert.That(VisibleIds(), Is.Empty);
            Assert.That(Field<TMP_Text>("expansionBody").text, Is.EqualTo("All equipment is upgraded to 2/2. There are no further upgrades here. Tap “All” to see the effects."));
            Assert.That(Secondary.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("All")); Unchanged(before, files);
            yield return ResourceClick(Secondary); Fits();
            Assert.That(VisibleIds(), Is.EqualTo(new[] { "equipment.house.2", "equipment.workshop.2", "equipment.recycler.2", "equipment.pigeon-loft.2", "equipment.cart.2", "equipment.power-source.2" }));
            Unchanged(before, files); yield return Back(); Unchanged(before, files);

            var lower = Funded(parts: 0, potatoes: 0, oldPrice: 600).State;
            File.WriteAllText(Path.Combine(Folder, "jtn.save.v1.json"), codec.Encode(lower)); Call("Reload"); yield return null;
            before = State; files = Files(); OldGoal(600); yield return Category(3); yield return ResourceClick(Secondary); Fits();
            Assert.That(VisibleIds(), Is.Empty); Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("Continue spinning").And.Not.Contain("All equipment is upgraded").And.Not.Contain("There are no further upgrades here"));
            Unchanged(before, files); yield return Offer("workbench.bonus.1", 5); Fits();
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("The original total stays the same; distribution may reduce losses when storage is full.").And.Not.Contain("The total resources will not increase"));
            Assert.That(Session.Expansion.BonusWorkbenchLevel, Is.Zero); Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null);
            Unchanged(before, files); OldGoal(600);
            TestContext.WriteLine("Exact existing all-equipment checkpoint: six2/2 tracks ->Buyable empty completed/no further levels/All, never misleading spins. Genuine separate lower fixture loses completed copy; locked WorkbenchI inspection truthfully separates raw sum from possible reduced cap loss, with0IO/no buy/Spin.");
        }
        [UnityTest]
        public IEnumerator EnergyBoostPreviewComparesDiamondAlternativeWithoutChangingItsConfirmation()
        {
            yield return Load(Funded(parts: 200, potatoes: 200, energy: 30, oldPrice: 600));
            string before = State; var files = Files(); yield return Offer("boost.next-bonus", 5); Fits();
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("The same boost costs 1 diamonds in “Mini-games & Diamonds”.").And.Contain("Unavailable for diamonds now.").And.Contain("Parts and potato storage are currently full.").And.Not.Contain("Until bonus:"));
            Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null); Assert.That(Primary.gameObject.activeInHierarchy, Is.False);
            Assert.That(Session.Arcade.IsAdopted, Is.False); Assert.That(Session.Arcade.Diamonds, Is.Zero); Unchanged(before, files); OldGoal(600);

            var unlocked = Funded(parts: 200, potatoes: 200, energy: 30, oldPrice: 600);
            unlocked.BuyExpansion(unlocked.GetExpansionOffer("bonus.unlock"), 1000);
            var full = unlocked.State;
            // Declared INITIAL wallet funding follows the existing Funded codec
            // convention; the actual unlock/ownership/receipts/RNG are untouched.
            var fields = ExpansionCodec.EncodeState(full.expansion).Split('|'); Assert.That(fields.Length, Is.EqualTo(21));
            fields[1] = fields[2] = "200"; full.expansion = ExpansionCodec.DecodeState(string.Join("|", fields)); GameLogic.Validate(full);
            File.WriteAllText(Path.Combine(Folder, "jtn.save.v1.json"), codec.Encode(full)); Call("Reload"); yield return null;
            before = State; files = Files(); yield return Offer("boost.next-bonus", 5); var noDiamondEnergy = Quote("boost", "boost.next-bonus", energy: 15); Fits();
            Assert.That(Primary.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Activate\n15 energy"));
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("Unavailable for diamonds now.").And.Contain("Until bonus: 5 new spins").And.Contain("Parts and potato storage are currently full."));
            Assert.That(Primary.gameObject.activeInHierarchy, Is.True, "Current full stocks warn but do not block a legal boost");
            Assert.That(Session.Arcade.IsAdopted, Is.False); Assert.That(ExpansionCodec.EncodeState(Session.State.expansion).Split('|').Length, Is.EqualTo(21));
            Unchanged(before, files); yield return Back(); Call("Reload"); yield return null; Unchanged(before, files);
            yield return Offer("boost.next-bonus", 5); Assert.That(Quote("boost", "boost.next-bonus", energy: 15).Key, Is.EqualTo(noDiamondEnergy.Key));
            Unchanged(before, files); OldGoal(600);

            // Existing tracked positive-Diamond receipt; no invented grant or
            // mutable currency/ownership setter, and no new foreground draw.
            var positive = codec.Decode(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../Checks/ArcadeCore/Evidence/claim-committed.json"))));
            positive.lastSettledAt = 1000;
            var earned = GameSession.Open(new GameStore(new Memory { raw = codec.Encode(positive) }, codec), 1);
            Assert.That(earned.RecoveryRequired, Is.False, earned.Error); Assert.That(earned.Arcade.Diamonds, Is.EqualTo(3));
            earned.BuyExpansion(earned.GetExpansionOffer("bonus.unlock"), 1000);
            File.WriteAllText(Path.Combine(Folder, "jtn.save.v1.json"), codec.Encode(earned.State)); Call("Reload"); yield return null;
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 2, 8, 16 }));
            long coins = Session.State.coins; uint seed = Session.State.seed; string paidDiamondClaim = Session.Arcade.LastClaim.Key;
            before = State; files = Files(); yield return Offer("boost.next-bonus", 5); var energy = Quote("boost", "boost.next-bonus", energy: 15); Fits();
            Assert.That(Primary.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Activate\n15 energy"));
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("Doubles resources for the next bonus only, within storage limits.").And.Not.Contain("Boosts the next bonus reward within storage limits."));
            var alternative = Session.GetArcadeQuote("buy", "arcade.boost.next-bonus", Session.State.sequence);
            Assert.That(alternative.ExpectedSequence, Is.EqualTo(energy.ExpectedSequence)); Assert.That(alternative.DiamondsCost, Is.EqualTo(1));
            Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.SameAs(energy), "The informational alternative cannot replace the retained Energy confirmation");
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("The same boost costs 1 diamonds in “Mini-games & Diamonds”.").And.Contain("You can buy it separately and save energy.").And.Contain("Until bonus: 5 new spins").And.Not.Contain("Parts and potato storage are currently full."));
            Unchanged(before, files); var oldConfirm = Primary.onClick; yield return ResourceClick(Primary, 931);
            Assert.That(Session.State.sequence, Is.EqualTo(energy.Sequence)); Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(energy.Key));
            Assert.That(Session.Expansion.LastCommand.Action, Is.EqualTo("boost")); Assert.That(Session.Expansion.BoostArmed, Is.True);
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 2, 8, 1 }));
            Assert.That(Session.Arcade.Diamonds, Is.EqualTo(3)); Assert.That(Session.Arcade.LastClaim.Key, Is.EqualTo(paidDiamondClaim));
            Assert.That(Session.State.coins, Is.EqualTo(coins)); Assert.That(Session.State.seed, Is.EqualTo(seed));
            before = State; files = Files(); oldConfirm.Invoke(); Unchanged(before, files);
            yield return Offer("boost.next-bonus", 5); Fits(); Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null);
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("Both methods activate the same boost; they do not stack.").And.Contain("Until bonus: 5 new spins").And.Not.Contain("You can buy it separately and save energy."));
            oldConfirm.Invoke(); Unchanged(before, files); Assert.That(Session.Arcade.Diamonds, Is.EqualTo(3));
            TestContext.WriteLine("Reciprocal Energy preview: locked/noD/legacy-absent/full-current-cap reads0IO and no adoption/forecast; both-caps notice does not block legal Energy15. Actual same-sequence1D alternative never replaces Energy quote; explicit Energy16->1 leaves earned3D/claim/Coins/RNG/resources intact and arms one shared charge. Armed/old callback cannot stack or repay.");
        }
        [UnityTest]
        public IEnumerator EmptyEquipmentNamesUnbuiltTracksWithoutMislabelingAnOwnedUnpaidTrack()
        {
            yield return Load(Funded(oldPrice: 540)); var lower = OlderModel(Session.State);
            yield return Buy("equipment.house.1", 3, 20, 10);
            yield return Buy("equipment.house.2", 3, 40, 20); OldGoal(540);
            string[] families = { "house", "workshop", "recycler", "pigeon-loft", "cart", "power-source" };
            var stages = ChapterCatalog.Rules.Stages(Session.Chapter.Progress);
            Assert.That(families.Select(stages.AbsoluteLevel), Is.EqualTo(new[] { 1, 0, 0, 0, 0, 0 }));
            Assert.That(families.Select(Session.Expansion.EquipmentLevel), Is.EqualTo(new[] { 2, 0, 0, 0, 0, 0 }));
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 140, 170, 10 }));
            Assert.That(Session.Expansion.ForegroundSpins, Is.Zero); Assert.That(Session.Expansion.LastResourceReceipt, Is.Null);
            lower.sequence += 2; lower.revision = lower.sequence * 2;
            Assert.That(codec.Encode(OlderModel(Session.State)), Is.EqualTo(codec.Encode(lower)));
            string before = State; var files = Files();
            yield return Category(3); yield return ResourceClick(Secondary); Fits(); Assert.That(VisibleIds(), Is.Empty);
            const string unbuilt = "New equipment requires a building: Workshop, Recycler, Pigeon Loft, Cart, Power Source. Each has its own track. See “All” for requirements.";
            Assert.That(Field<TMP_Text>("expansionBody").text, Is.EqualTo(unbuilt));
            Assert.That(Secondary.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("All"));
            Assert.That(Primary.gameObject.activeInHierarchy, Is.False); Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null);
            Primary.onClick.Invoke(); Field<Button>("selectButton").onClick.Invoke(); Unchanged(before, files); OldGoal(540);
            yield return ResourceClick(Secondary); Fits();
            Assert.That(VisibleIds(), Is.EqualTo(new[] { "equipment.house.2", "equipment.workshop.1", "equipment.recycler.1", "equipment.pigeon-loft.1", "equipment.cart.1", "equipment.power-source.1" }));
            Unchanged(before, files);
            string[] names = { "Workshop", "Recycler", "Pigeon Loft", "Cart", "Power Source" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = Array.IndexOf(Field<string[]>("expansionCardIds"), "equipment." + families[i + 1] + ".1");
                Assert.That(index, Is.GreaterThanOrEqualTo(0));
                yield return ResourceClick(Field<Button[]>("expansionCards")[index]); Fits();
                Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("First build: " + names[i] + "."));
                Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null); Assert.That(Primary.gameObject.activeInHierarchy, Is.False);
                Primary.onClick.Invoke(); Field<Button>("selectButton").onClick.Invoke(); Unchanged(before, files);
                yield return Back(); Fits(); Unchanged(before, files); OldGoal(540);
            }
            yield return ResourceClick(Secondary); Fits(); Assert.That(VisibleIds(), Is.Empty);
            Assert.That(Field<TMP_Text>("expansionBody").text, Is.EqualTo(unbuilt)); Unchanged(before, files);
            yield return ReturnToGame(); Call("Reload"); yield return null; Unchanged(before, files); OldGoal(540);

            // Independent existing private-funding convention, then a real I
            // purchase; ownership/history are never set to manufacture a gate.
            var poor = Funded(parts: 20, potatoes: 10, oldPrice: 600);
            poor.BuyExpansion(poor.GetExpansionOffer("equipment.house.1"), 1000);
            File.WriteAllText(Path.Combine(Folder, "jtn.save.v1.json"), codec.Encode(poor.State)); Call("Reload"); yield return null;
            Assert.That(Session.Expansion.EquipmentLevel("house"), Is.EqualTo(1));
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes }, Is.EqualTo(new[] { 0, 0 }));
            before = State; files = Files(); OldGoal(600);
            yield return Category(3); yield return ResourceClick(Secondary); Fits(); Assert.That(VisibleIds(), Is.Empty);
            Assert.That(Field<TMP_Text>("expansionBody").text, Is.EqualTo("Nothing available to buy here. Continue spinning or tap “All” to see owned and locked items."));
            Primary.onClick.Invoke(); Field<Button>("selectButton").onClick.Invoke(); Unchanged(before, files);
            yield return ResourceClick(Secondary); Fits();
            int house = Array.IndexOf(Field<string[]>("expansionCardIds"), "equipment.house.2"); Assert.That(house, Is.GreaterThanOrEqualTo(0));
            yield return ResourceClick(Field<Button[]>("expansionCards")[house]); Fits();
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("Missing: 40 parts, 20 potatoes").And.Not.Contain("First build"));
            Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null); Assert.That(Primary.gameObject.activeInHierarchy, Is.False);
            Primary.onClick.Invoke(); Field<Button>("selectButton").onClick.Invoke(); Unchanged(before, files); OldGoal(600);
            yield return Back(); yield return ReturnToGame(); Call("Reload"); yield return null; Unchanged(before, files); OldGoal(600);
            Assert.That(Session.Expansion.ForegroundSpins, Is.Zero); Assert.That(Session.Expansion.LastResourceReceipt, Is.Null);
            TestContext.WriteLine("Actual House equipmentI/II + five unbuilt independent families: empty Buyable names only those five building gates despite sufficient resources; All/five locked previews read0IO, preserve old540/seed. Separate poor ownedHouseI retains resource-shortage guidance and unpaidII, old600/full state/files; no Spin, selection, automatic purchase or forged ownership.");
        }
        [UnityTest]
        public IEnumerator FinitePurchaseChoicesShowExactRemainingBalancesAndFreshAcquiredEffects()
        {
            yield return Load(Funded(30,30,oldPrice:540));
            yield return Offer("trade.parts",4);Assert.That(Field<ExpansionQuote>("expansionQuote"),Is.Not.Null);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Not.Contain("After purchase:"));
            yield return Offer("module.salvage",0);var module=Quote("buy","module.salvage",12);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("After purchase: 18 parts."));
            yield return Click(Primary);Assert.That(Session.Expansion.Parts,Is.EqualTo(18));Assert.That(Session.State.sequence,Is.EqualTo(module.Sequence));
            Assert.That(Slots(Session.Expansion),Is.EqualTo(new string[]{null,null,null}));
            Assert.That(Primary.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo("Choose a slot"));
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("does not install the module yet").And.Not.Contain("Equipment:"));OldGoal(540);
            Assert.Throws<InvalidOperationException>(()=>Session.GetExpansionOffer("trade.parts"));
            yield return Offer("trade.parts",4);Assert.That(Field<ExpansionQuote>("expansionQuote"),Is.Null);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Not.Contain("After purchase:"));
            yield return Offer("module.salvage",0);Assert.That(Field<TMP_Text>("expansionBody").text,Does.Not.Contain("After purchase:"));

            yield return Load(Funded(oldPrice:540));yield return Offer("equipment.house.1",3);
            var house=Quote("buy","equipment.house.1",20,10);var stale=Primary.onClick;
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("After purchase: 180 parts; 190 potatoes."));
            yield return PurchaseChoicePortrait.Capture(Game,"house-capacity-preview","expansionBody");
            yield return Click(Primary);Fits();
            Assert.That(Session.State.sequence,Is.EqualTo(house.Sequence));Assert.That(new[]{Session.Expansion.Parts,Session.Expansion.Potatoes,Session.Expansion.Energy},Is.EqualTo(new[]{180,190,10}));
            Assert.That(new[]{Session.Expansion.PartsCap,Session.Expansion.PotatoesCap},Is.EqualTo(new[]{250,250}));OldGoal(540);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Parts/Potatoes capacity: 200/200 → 250/250; no refill.").And.Not.Contain("per future"));
            Assert.That(Field<TMP_Text>("expansionBody").text.Split(new[]{"Spent:"},StringSplitOptions.None).Length,Is.EqualTo(2));
            yield return PurchaseChoicePortrait.Capture(Game,"house-capacity-result","expansionBody");
            string before=State;var files=Files();stale.Invoke();Unchanged(before,files);
            Call("Reload");yield return null;Unchanged(before,files);yield return Offer("equipment.house.2",3);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Not.Contain("no refill").And.Not.Contain("Parts/Potatoes capacity: 200/200 → 250/250"));Unchanged(before,files);
            yield return Load(Funded(parts:0,potatoes:0,oldPrice:600));yield return Offer("equipment.house.1",3);
            Assert.That(Field<ExpansionQuote>("expansionQuote"),Is.Null);Assert.That(Field<TMP_Text>("expansionBody").text,Does.Not.Contain("After purchase:"));OldGoal(600);

            yield return Load(Funded(oldPrice:600));yield return Buy("potato.yield.1",4,15,15);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Potato yield: +0 → +1 per future Potato symbol; caps apply."));
            yield return Buy("potato.yield.2",4,30,30);OldGoal(600);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Potato yield: +1 → +2 per future Potato symbol; caps apply."));
            yield return PurchaseChoicePortrait.Capture(Game,"yield-II-result","expansionBody");
            yield return Load(Funded());yield return Buy("bonus.unlock",5,20,10);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Unlocked: resource bonus every 5 new spins; no extra Coins."));
            yield return Buy("workbench.bonus.1",5,30,20);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("free selection is separate").And.Not.Contain("Workbench raw addition:"));
            Assert.That(Primary.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo("Inspect focuses"));
            yield return Buy("workbench.bonus.2",5,40,30);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Workbench raw addition: 0/0 → +1/+1 Parts/Potatoes per future bonus before focus, boost and caps."));
            Assert.That(new[]{Session.Expansion.Parts,Session.Expansion.Potatoes},Is.EqualTo(new[]{110,140}));
            yield return PurchaseChoicePortrait.Capture(Game,"workbench-II-result","expansionBody");

            // Validated synthetic initial family ownership, not invented paid history.
            yield return Load(Funded(allFamilies:true));yield return Buy("equipment.workshop.1",3,25,15);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Equipment: +0 → +1 parts per future spin; caps apply."));
            yield return PurchaseChoicePortrait.Capture(Game,"workshop-equipment-result","expansionBody");
            yield return Buy("equipment.pigeon-loft.1",3,20,10);
            Assert.That(Session.Expansion.Energy,Is.EqualTo(10));Assert.That(Session.Expansion.EnergyCap,Is.EqualTo(35));
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Energy capacity: 30 → 35; no refill."));
            foreach(var item in new[]{new[]{"recycler","Trash symbol"},new[]{"cart","Box symbol"}})
            {
                yield return Buy("equipment."+item[0]+".1",3,20,10);
                Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Equipment: +0 → +1 parts per future "+item[1]+"; caps apply."));
            }
            yield return Load(Funded(complete:true));yield return Buy("equipment.power-source.1",3,20,10);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Equipment: +0 → +1 energy per future spin; caps apply."));
            Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(24));
            yield return PurchaseChoicePortrait.Capture(Game,"optional-24-equipment-result","expansionBody");
            TestContext.WriteLine("Visible product/Profile/Sections resource route; actual quotes and purchases preserve saved540/600 and24/24. Independent30-12=18 and200/200-20/10=180/190 with250/250caps; production versus capacity, bonus/workbench/yield and existing module/focus continuations remain distinct. No current production credit, auto-install, changed RNG or replay payment.");
        }
        GameSession FocusFixture(string focus,int parts,int potatoes)
        {
            var setup=Funded(energy:30,oldPrice:540,wallet:0);
            for(int i=0;i<8;i++)setup.ExecuteChapterSpin(setup.State.sequence+1,1000);
            Assert.That(setup.State.coins,Is.EqualTo(59));Assert.That(setup.State.seed,Is.EqualTo(3652395599u));
            setup.BuyExpansion(setup.GetExpansionOffer("bonus.unlock"),1000);setup.BuyExpansion(setup.GetExpansionOffer("workbench.bonus.1"),1000);
            setup.ExecuteExpansion(setup.GetExpansionAction("focus",focus),1000);
            // Declared private INITIAL wallet funding, retaining actual public
            // spin/ownership/focus receipts; no paid claim or future RNG is forged.
            var state=setup.State;var f=ExpansionCodec.EncodeState(state.expansion).Split('|');Assert.That(f[0],Is.EqualTo("expansion.state.v3"));
            var a=Encoding.UTF8.GetString(Convert.FromBase64String(f[23])).Split(';');a[3]="3";
            f[1]=parts.ToString();f[2]=potatoes.ToString();f[3]="30";f[23]=Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(";",a)));
            state.expansion=ExpansionCodec.DecodeState(string.Join("|",f));GameLogic.Validate(state);
            return GameSession.Open(new GameStore(new Memory{raw=codec.Encode(state)},codec),1,initializeJourney:true);
        }
        [UnityTest]
        public IEnumerator FocusedBoostPreviewsExplainCurrentHeadroomWithoutBlockingArmingOrQuickboost()
        {
            var cases=new[]{
                new[]{"parts","200","160","yes"},new[]{"potatoes","160","200","yes"},
                new[]{"balanced","200","160","no"},new[]{"parts","160","200","no"},
                new[]{"potatoes","200","160","no"},new[]{"parts","160","160","no"},
                new[]{"balanced","160","160","no"},new[]{"balanced","200","200","no"}};
            for(int index=0;index<cases.Length;index++)
            {
                var c=cases[index];int parts=int.Parse(c[1]),potatoes=int.Parse(c[2]);yield return Load(FocusFixture("focus."+c[0],parts,potatoes));
                string before=State;var files=Files();long sequence=Session.State.sequence;
                string expected="Saved focus: "+char.ToUpperInvariant(c[0][0])+c[0].Substring(1)+". Room now: "+(200-parts)+" parts; "+(200-potatoes)+" potatoes.";
                yield return Click(Field<Button>("productEnergy"));yield return Click(Field<Button[]>("productCards")[0]);yield return null;
                if(index==0)yield return PurchaseChoicePortrait.Capture(Game,"energy-parts-full","expansionBody");Fits();
                var energy=Quote("boost","boost.next-bonus",energy:15);var staleEnergy=Primary.onClick;
                string text=Field<TMP_Text>("expansionBody").text;Assert.That(text,Does.Contain(expected).And.Contain("Spin income is separate."));
                Assert.That(text.Contains("can credit 0 materials and consume the charge"),Is.EqualTo(c[3]=="yes"));
                Unchanged(before,files);yield return ReturnToGame();
                yield return Click(Field<Button>("productEnergy"));yield return Click(Field<Button[]>("productCards")[1]);yield return null;
                var diamond=Field<ArcadeQuote>("arcadeQuote");Assert.That(diamond.ExpectedSequence,Is.EqualTo(energy.ExpectedSequence));Assert.That(diamond.DiamondsCost,Is.EqualTo(1));
                text=Field<TMP_Text>("arcadeBody").text;Assert.That(text,Does.Contain(expected).And.Contain("Spin income is separate."));
                Assert.That(text.Contains("can credit 0 materials and consume the charge"),Is.EqualTo(c[3]=="yes"));
                Target(Field<Button>("arcadePrimaryButton"));Unchanged(before,files);staleEnergy.Invoke();Unchanged(before,files);
                if(index==1)yield return PurchaseChoicePortrait.Capture(Game,"diamond-potatoes-full","arcadeBody");
                if(index==1)
                {
                    var stale=Field<Button>("arcadePrimaryButton").onClick;yield return Click(Field<Button>("arcadePrimaryButton"));
                    Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Assert.That(Session.Arcade.Diamonds,Is.EqualTo(2));Assert.That(Session.Expansion.Energy,Is.EqualTo(30));
                    Assert.That(Session.Expansion.BoostArmed,Is.True);before=State;files=Files();stale.Invoke();Unchanged(before,files);
                }
                yield return ReturnToGame();
                if(index==0)
                {
                    yield return Click(Field<Button>("productEnergy"));yield return Click(Field<Button[]>("productCards")[0]);yield return Click(Primary);
                    Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Assert.That(Session.Expansion.Energy,Is.EqualTo(15));Assert.That(Session.Arcade.Diamonds,Is.EqualTo(3));
                    Assert.That(Session.Expansion.BoostArmed,Is.True);yield return ReturnToGame();
                }
                if(index==cases.Length-1)
                {
                    Assert.That(Field<GameObject>("panel").activeSelf,Is.False);yield return Click(Field<Button>("expansionQuickBoostButton"));
                    Assert.That(Field<GameObject>("panel").activeSelf,Is.False,"Quickboost must remain direct, without a new confirmation");
                    Assert.That(Session.State.sequence,Is.EqualTo(sequence+1));Assert.That(Session.Expansion.Energy,Is.EqualTo(15));Assert.That(Session.Arcade.Diamonds,Is.EqualTo(3));
                    Assert.That(Session.Expansion.BoostArmed,Is.True);
                }
                Assert.That(new[]{Session.Expansion.Parts,Session.Expansion.Potatoes},Is.EqualTo(new[]{parts,potatoes}));
                Assert.That(Session.Expansion.BonusFocusId,Is.EqualTo("focus."+c[0]));Assert.That(Session.State.coins,Is.EqualTo(59));Assert.That(Session.State.seed,Is.EqualTo(3652395599u));
                Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter),Is.EqualTo(540));
            }
        }
        IEnumerator LoadoutModule(int slot,string id)
        {
            string before=State;var files=Files();yield return Category(1);
            int socket=Array.IndexOf(Field<string[]>("expansionCardIds"),"socket."+slot);Assert.That(socket,Is.GreaterThanOrEqualTo(0));
            yield return Click(Field<Button[]>("expansionCards")[socket]);yield return null;Fits();
            if(id=="module.none")yield return Click(Secondary);
            else
            {
                int module=Array.IndexOf(Field<string[]>("expansionCardIds"),id);Assert.That(module,Is.GreaterThanOrEqualTo(0));
                yield return Click(Field<Button[]>("expansionCards")[module]);
            }
            yield return null;Fits();Unchanged(before,files);
        }
        void FreeLoadoutCommitted(ModelState before,ExpansionQuote quote,string[] expected)
        {
            Assert.That(Session.State.sequence,Is.EqualTo(quote.Sequence));Assert.That(Slots(Session.Expansion),Is.EqualTo(expected));
            var receipt=Session.Expansion.LastCommand;Assert.That(receipt.Key,Is.EqualTo(quote.Key));Assert.That(receipt.Action,Is.EqualTo(quote.Action));Assert.That(receipt.Id,Is.EqualTo(quote.Id));Assert.That(receipt.Slot,Is.EqualTo(quote.Slot));
            var older=OlderModel(before);older.sequence=Session.State.sequence;older.revision=Session.State.revision;
            Assert.That(codec.Encode(OlderModel(Session.State)),Is.EqualTo(codec.Encode(older)),"Free configuration changed older economic/goal facts");
            foreach(var property in typeof(ExpansionState).GetProperties(BindingFlags.Public|BindingFlags.Instance))
                if(property.Name!="FormatVersion"&&property.Name!="LastCommand")Assert.That(property.GetValue(Session.Expansion),Is.EqualTo(property.GetValue(before.expansion)),property.Name);
            foreach(var offer in ExpansionCatalog.Offers.Where(o=>o.Kind=="module"))Assert.That(Session.Expansion.OwnsModule(offer.Id),Is.EqualTo(before.expansion.OwnsModule(offer.Id)),offer.Id);
            foreach(string family in FamilyIds.All)Assert.That(Session.Expansion.EquipmentLevel(family),Is.EqualTo(before.expansion.EquipmentLevel(family)));
            Assert.That(Session.Expansion.FormatVersion,Is.GreaterThanOrEqualTo(before.expansion.FormatVersion));Fits();
        }
        static void LoadoutSubmit(Button button)
        {Target(button);ExecuteEvents.Execute(button.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);}
        [UnityTest]
        public IEnumerator FreeLoadoutComparisonsUseActualTriggersAndFreshAppliedDifferences()
        {
            var setup=Funded(oldPrice:540);
            foreach(string id in new[]{"module.salvage","module.harvest"})setup.BuyExpansion(setup.GetExpansionOffer(id),1000);
            setup.EquipExpansionModule(1,"module.harvest",setup.State.sequence,1000);yield return Load(setup);OldGoal(540);
            string before=State;var files=Files();yield return Offer("preset.energy",1);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Module bonuses").And.Contain("Parts per Trash: +0 → +1").And.Contain("Potatoes per Potato: +1 → +0").And.Contain("Dynamo not owned; module Energy stays +0. Ordinary Energy is separate."));
            yield return PurchaseChoicePortrait.Capture(Game,"energy-fallback-preview","expansionBody");
            yield return Back();Unchanged(before,files);yield return Offer("preset.energy",1);var fallback=Quote("preset","preset.energy");var prior=Session.State;var oldEvent=Primary.onClick;
            LoadoutSubmit(Primary);yield return null;FreeLoadoutCommitted(prior,fallback,new[]{"module.salvage",null,null});OldGoal(540);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Parts per Trash: +0 → +1").And.Contain("Potatoes per Potato: +1 → +0").And.Not.Contain("Energy per Pigeon spin"));
            yield return PurchaseChoicePortrait.Capture(Game,"energy-fallback-result","expansionBody");
            before=State;files=Files();oldEvent.Invoke();Session.ExecuteExpansion(fallback,1000);Unchanged(before,files);
            yield return Category(1);yield return Click(Field<Button[]>("expansionCards")[1]);yield return null;Fits();
            Assert.That(Field<ExpansionQuote>("expansionQuote").Action,Is.EqualTo("buy"));Assert.That(Field<TMP_Text>("expansionBody").text,Does.Not.Contain("Module bonuses"));Unchanged(before,files);
            yield return Category(1);yield return Click(Field<Button[]>("expansionCards")[0]);yield return null;
            Assert.That(Field<string[]>("expansionCardIds"),Does.Not.Contain("module.dynamo"));Unchanged(before,files);

            setup=Funded(oldPrice:600);
            foreach(string id in new[]{"module.salvage","module.packing","module.harvest","module.combo","module.dynamo","socket.2","socket.3"})setup.BuyExpansion(setup.GetExpansionOffer(id),1000);
            foreach(var pair in new[]{new[]{"1","module.salvage"},new[]{"2","module.packing"},new[]{"3","module.dynamo"}})setup.EquipExpansionModule(int.Parse(pair[0]),pair[1],setup.State.sequence,1000);
            yield return Load(setup);OldGoal(600);Assert.That(new[]{Session.Expansion.Parts,Session.Expansion.Potatoes},Is.EqualTo(new[]{46,144}));
            yield return Offer("preset.harvest",1);var harvest=Quote("preset","preset.harvest");
            string text=Field<TMP_Text>("expansionBody").text;
            foreach(string row in new[]{"Parts per Trash: +1 → +0","Parts per Box: +1 → +0","Potatoes per Potato: +0 → +1","Parts per matching spin: +0 → +1","Potatoes per matching spin: +0 → +1"})Assert.That(text,Does.Contain(row));
            Assert.That(text,Does.Not.Contain("Energy per Pigeon spin"));yield return PurchaseChoicePortrait.Capture(Game,"five-trigger-preset-preview","expansionBody");
            prior=Session.State;yield return Click(Primary);yield return null;FreeLoadoutCommitted(prior,harvest,new[]{"module.harvest","module.combo","module.dynamo"});OldGoal(600);
            yield return PurchaseChoicePortrait.Capture(Game,"five-trigger-preset-result","expansionBody");
            yield return LoadoutModule(1,"module.salvage");var salvage=Quote("equip","module.salvage",slot:1);prior=Session.State;yield return Click(Primary);yield return null;
            FreeLoadoutCommitted(prior,salvage,new[]{"module.salvage","module.combo","module.dynamo"});
            yield return LoadoutModule(1,"module.packing");Quote("equip","module.packing",slot:1);var held=Down(Primary,631);oldEvent=Primary.onClick;
            yield return LoadoutModule(1,"module.harvest");yield return LoadoutModule(1,"module.packing");var packing=Quote("equip","module.packing",slot:1);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Parts per Trash: +1 → +0").And.Contain("Parts per Box: +0 → +1").And.Not.Contain("matching spin:"));
            before=State;files=Files();Release(held,Primary);oldEvent.Invoke();Unchanged(before,files);
            yield return PurchaseChoicePortrait.Capture(Game,"replace-trigger-preview","expansionBody");
            prior=Session.State;LoadoutSubmit(Primary);yield return null;FreeLoadoutCommitted(prior,packing,new[]{"module.packing","module.combo","module.dynamo"});OldGoal(600);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Parts per Trash: +1 → +0").And.Contain("Parts per Box: +0 → +1"));
            yield return LoadoutModule(2,"module.none");var remove=Quote("equip","module.none",slot:2);prior=Session.State;
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Parts per matching spin: +1 → +0").And.Contain("Potatoes per matching spin: +1 → +0"));
            yield return Click(Primary);yield return null;FreeLoadoutCommitted(prior,remove,new[]{"module.packing",null,"module.dynamo"});
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("You keep the module."));Assert.That(Session.Expansion.OwnsModule("module.combo"),Is.True);
            yield return PurchaseChoicePortrait.Capture(Game,"remove-combo-result","expansionBody");
            before=State;files=Files();Call("Reload");yield return null;Unchanged(before,files);Session.ExecuteExpansion(remove,1000);Unchanged(before,files);
            Assert.That(Field<TMP_Text>("message").text,Does.Not.Contain("Module bonuses"));
            yield return Category(1);yield return Click(Field<Button[]>("expansionCards")[1]);yield return null;Fits();Assert.That(Secondary.gameObject.activeSelf,Is.False,"Empty slot has no removal action");Unchanged(before,files);
            yield return LoadoutModule(2,"module.dynamo");Assert.That(Field<ExpansionQuote>("expansionQuote"),Is.Null);Assert.That(Primary.gameObject.activeSelf,Is.False);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Not.Contain("Module bonuses"));Unchanged(before,files);

            setup=Funded(complete:true);
            foreach(string id in new[]{"module.salvage","module.combo","socket.2"})setup.BuyExpansion(setup.GetExpansionOffer(id),1000);
            setup.EquipExpansionModule(1,"module.salvage",setup.State.sequence,1000);setup.EquipExpansionModule(2,"module.combo",setup.State.sequence,1000);
            yield return Load(setup);yield return Offer("preset.energy",1);var permutation=Quote("preset","preset.energy");prior=Session.State;
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Slot arrangement will change; module production effects stay the same."));
            yield return PurchaseChoicePortrait.Capture(Game,"permutation-preview","expansionBody");yield return Click(Primary);yield return null;
            FreeLoadoutCommitted(prior,permutation,new[]{"module.combo","module.salvage",null});Assert.That(Session.Chapter.SelectedProjectId,Is.Null);Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count,Is.EqualTo(24));
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Slot arrangement changed; module production effects stay the same."));
            yield return PurchaseChoicePortrait.Capture(Game,"permutation-result","expansionBody");
            before=State;files=Files();yield return Offer("preset.energy",1);Assert.That(Field<ExpansionQuote>("expansionQuote"),Is.Null);Assert.That(Primary.gameObject.activeSelf,Is.False);
            Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Slots already match. No changes needed.").And.Not.Contain("will change").And.Not.Contain("Module bonuses"));
            Session.ApplyExpansionPreset("preset.energy",Session.State.sequence,1000);Session.ExecuteExpansion(permutation,1000);Unchanged(before,files);
            Call("Reload");yield return null;Unchanged(before,files);Assert.That(Field<TMP_Text>("message").text,Does.Not.Contain("Slot arrangement changed"));
        }
        [SetUp] public void Isolate() { ui.Isolate(); }
        IEnumerator UpgradePreview(string id,int category,string expected,bool available,string capture=null)
        {
            string before=State;var files=Files();int frame=Time.frameCount;
            yield return Offer(id,category);Assert.That(Time.frameCount,Is.GreaterThan(frame));
            string body=Field<TMP_Text>("expansionBody").text;Assert.That(body,Does.Contain(expected));
            Assert.That(Primary.gameObject.activeSelf,Is.EqualTo(available));
            var quote=Field<ExpansionQuote>("expansionQuote");
            if(available){Assert.That(quote,Is.Not.Null);Assert.That(quote.Id,Is.EqualTo(id));Assert.That(quote.ExpectedSequence,Is.EqualTo(Session.State.sequence));}
            else {Assert.That(quote,Is.Null);Primary.onClick.Invoke();Unchanged(before,files);}
            if(capture!=null)yield return PurchaseChoicePortrait.Capture(Game,capture,"expansionBody");
            Unchanged(before,files);yield return Back();yield return ReturnToGame();Unchanged(before,files);
        }
        [UnityTest]public IEnumerator YieldAndStoragePreviewsDescribeOnlyTheirPurchaseAndActualCapacity()
        {
            var setup=Funded(allFamilies:true);yield return Load(setup);
            yield return UpgradePreview("potato.yield.2",4,"After purchase, this level adds +1 Potato per Potato symbol on future spins. Level I is bought separately; module bonuses are separate.",false,"yield-locked");
            Assert.That(setup.Expansion.PotatoYieldLevel,Is.Zero);
            yield return UpgradePreview("potato.yield.1",4,"Permanent potato increase: +0 → +1",true);
            setup.BuyExpansion(setup.GetExpansionOffer("potato.yield.1"),1000);yield return Load(setup);
            yield return UpgradePreview("potato.yield.2",4,"Permanent potato increase: +1 → +2",true);
            setup.BuyExpansion(setup.GetExpansionOffer("potato.yield.2"),1000);yield return Load(setup);
            yield return UpgradePreview("potato.yield.2",4,"Permanent potato increase: +2 → +2",false);
            foreach(string family in new[]{"house","pigeon-loft"})
            {
                setup=Funded(allFamilies:true);
                for(int level=0;level<=2;level++)
                {
                    yield return Load(setup);int next=Math.Min(level+1,2);
                    int[] house={200,250,300},energy={30,35,40};
                    string expected=family=="house"?$"Storage capacity: Parts {house[level]} → {house[next]}; Potatoes {house[level]} → {house[next]}. No refill.":$"Energy capacity: {energy[level]} → {energy[next]}. No refill.";
                    yield return UpgradePreview("equipment."+family+"."+next,3,expected,level<2,level==1?family+"-next":null);
                    if(level<2)setup.BuyExpansion(setup.GetExpansionOffer("equipment."+family+"."+(level+1)),1000);
                }
            }
            TestContext.WriteLine("Ten read-only previews: locked/available/owned yield plus both storage tracks at 0/1/2. Public visible routes, advancing frames, full state and file bytes/timestamps exact; pure private purchase setup occurs outside inspection intervals.");
        }
        [UnityTest]public IEnumerator ChangedUpgradePreviewsFitShortPortraitWithoutPurchasing()
        {
            var setup=Funded(allFamilies:true);setup.BuyExpansion(setup.GetExpansionOffer("equipment.house.1"),1000);setup.BuyExpansion(setup.GetExpansionOffer("equipment.pigeon-loft.1"),1000);
            yield return Load(setup);
            yield return UpgradePreview("potato.yield.2",4,"this level adds +1 Potato",false,"yield-locked-short");
            yield return UpgradePreview("equipment.house.2",3,"Storage capacity: Parts 250 → 300; Potatoes 250 → 300. No refill.",true,"house-short");
            yield return UpgradePreview("equipment.pigeon-loft.2",3,"Energy capacity: 35 → 40. No refill.",true,"energy-short");
            string before=State;var files=Files();int frame=Time.frameCount;
            yield return Click(Field<Button[]>("productTabs")[4]);yield return Click(Field<Button[]>("productCards")[3]);yield return Click(Field<Button[]>("navigationCards")[4]);
            Assert.That(Time.frameCount,Is.GreaterThan(frame));Assert.That(Field<TMP_Text>("panelBody").text,Does.StartWith("Branch bonus: +0 → +1 Coins per delivery (3 Pigeons). Odds unchanged."));
            yield return PurchaseChoicePortrait.Capture(Game,"skill-delivery-short","panelBody");Unchanged(before,files);yield return Back();yield return ReturnToGame();Unchanged(before,files);
        }
        [UnityTearDown] public IEnumerator Restore() { yield return ui.RestorePrivateContext(); }
    }
    internal static class PurchaseChoicePortrait
    {
        [Serializable] sealed class Measure
        {public string name,text;public float font,width,height,boundsWidth,boundsHeight;public bool overflow;}
        [Serializable] sealed class Snapshot
        {public string name,target;public int frame,width,height;public float dpi;public bool stateExact;public Measure[] texts;}
        internal static IEnumerator Capture(Component game,string name,string field)
        {
            string upgrade=Environment.GetEnvironmentVariable("JTN_UPGRADE_CAPTURE_DIRECTORY");string zero=Environment.GetEnvironmentVariable("JTN_ZERO_CAPTURE_DIRECTORY");string intent=Environment.GetEnvironmentVariable("JTN_INTENT_CAPTURE_DIRECTORY");string root=Environment.GetEnvironmentVariable("JTN_LOADOUT_CAPTURE_DIRECTORY");bool loadout=!string.IsNullOrEmpty(root);if(!loadout)root=Environment.GetEnvironmentVariable("JTN_PURCHASE_CAPTURE_DIRECTORY");if(!string.IsNullOrEmpty(intent))root=intent;if(!string.IsNullOrEmpty(zero))root=zero;if(string.IsNullOrEmpty(root))root=Environment.GetEnvironmentVariable("JTN_VOCAB_CAPTURE_DIRECTORY");if(!string.IsNullOrEmpty(upgrade))root=upgrade;if(string.IsNullOrEmpty(root))yield break;
            string vocabulary=Environment.GetEnvironmentVariable("JTN_VOCAB_CAPTURE_DIRECTORY");if(!string.IsNullOrEmpty(vocabulary))root=vocabulary;
            root=Path.GetFullPath(root);string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,!string.IsNullOrEmpty(upgrade)?"../../.work/upgrade-preview-clarity-20261103/captures":!string.IsNullOrEmpty(vocabulary)?"../../.work/economy-vocabulary-20261103/portraits":!string.IsNullOrEmpty(zero)?"../../.work/zero-outcome-20261102/portraits":!string.IsNullOrEmpty(intent)?"../../.work/active-intent-20261102/portraits":loadout?"../../.work/loadout-purpose-20261102/portraits":"../../.work/purchase-choice-20261102/portraits"));
            Assert.That(root.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Directory.CreateDirectory(root);if(!string.IsNullOrEmpty(upgrade))Assert.That(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length,Is.LessThan(16));if(!string.IsNullOrEmpty(vocabulary))Assert.That(Directory.GetFiles(allowed,"*.png",SearchOption.AllDirectories).Length,Is.LessThan(24));
            string path=Path.Combine(root,name+".png");if(File.Exists(path))path=Path.Combine(root,name+"-"+Guid.NewGuid().ToString("N")+".png");
            var session=(GameSession)game.GetType().GetProperty("Session").GetValue(game);var codec=new JsonStateCodec();string state=codec.Encode(session.State);
            var target=(TMP_Text)game.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
            Canvas.ForceUpdateCanvases();target.ForceMeshUpdate();Assert.That(target.isTextOverflowing,Is.False,target.text);
            Assert.That(target.textBounds.size.x,Is.LessThanOrEqualTo(target.rectTransform.rect.width+2));Assert.That(target.textBounds.size.y,Is.LessThanOrEqualTo(target.rectTransform.rect.height+2));
            var texts=game.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.isActiveAndEnabled).Select(t=>{t.ForceMeshUpdate();return new Measure{name=t.name,text=t.text,font=t.fontSize,width=t.rectTransform.rect.width,height=t.rectTransform.rect.height,boundsWidth=t.textBounds.size.x,boundsHeight=t.textBounds.size.y,overflow=t.isTextOverflowing};}).ToArray();
            ScreenCapture.CaptureScreenshot(path);File.WriteAllText(Path.ChangeExtension(path,".state.json"),state,new UTF8Encoding(false));
            for(int frame=0;frame<40&&(!File.Exists(path)||new FileInfo(path).Length==0);frame++)yield return null;
            Assert.That(File.Exists(path),Is.True);Assert.That(codec.Encode(session.State),Is.EqualTo(state));
            File.WriteAllText(Path.ChangeExtension(path,".json"),JsonUtility.ToJson(new Snapshot{name=name,target=target.name,frame=Time.frameCount,width=Screen.width,height=Screen.height,dpi=Screen.dpi,stateExact=true,texts=texts},true),new UTF8Encoding(false));
            TestContext.WriteLine("Purchase-choice portrait: "+path);
        }
    }
}
#endif
