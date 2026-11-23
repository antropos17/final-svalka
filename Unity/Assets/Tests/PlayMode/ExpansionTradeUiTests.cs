#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using JunkToNeon.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    [Category("ExpansionTrade5h"),Category("ExpansionQol5h")]
    public sealed class ExpansionTradeUiTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly CombinedLifecycleTests ui = new CombinedLifecycleTests();
        readonly JsonStateCodec codec = new JsonStateCodec();
        Component Game => (Component)typeof(CombinedLifecycleTests).GetField("game", Hidden).GetValue(ui);
        string Folder => (string)typeof(CombinedLifecycleTests).GetField("directory", Hidden).GetValue(ui);
        GameSession Session => (GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        T Field<T>(string name) => (T)Game.GetType().GetField(name, Hidden).GetValue(Game);
        IEnumerator Harness(string name, params object[] args) =>
            (IEnumerator)typeof(CombinedLifecycleTests).GetMethod(name, Hidden).Invoke(ui, args);
        string State => codec.Encode(Session.State);
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
        static RaycastResult Top(Button button, Vector2 point)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                button.transform.TransformPoint(point));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), button.name);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), button.name);
            return hits[0];
        }
        static PointerEventData Down(Button button, int id)
        {
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, button.name);
            var rect = ((RectTransform)button.transform).rect;
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(48)); Assert.That(rect.height, Is.GreaterThanOrEqualTo(48));
            foreach (var point in new[] { rect.center, rect.min + Vector2.one * 4, rect.max - Vector2.one * 4,
                new Vector2(rect.xMin + 4, rect.yMax - 4), new Vector2(rect.xMax - 4, rect.yMin + 4) }) Top(button, point);
            var hit = Top(button, rect.center);
            var data = new PointerEventData(EventSystem.current) { pointerId = id, button = PointerEventData.InputButton.Left,
                eligibleForClick = true, pointerCurrentRaycast = hit, position = hit.screenPosition };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            data.pointerPress = button.gameObject; data.pointerClick = button.gameObject;
            return data;
        }
        static void Release(PointerEventData data, Button button)
        {
            data.pointerCurrentRaycast = Top(button, Center(button)); data.position = data.pointerCurrentRaycast.screenPosition;
            bool candidate = data.eligibleForClick && data.pointerClick == ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject);
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            if (candidate) ExecuteEvents.Execute(data.pointerClick, data, ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Click(Button button, int id = 501)
        { yield return null; Release(Down(button, id), button); yield return null; }

        sealed class Memory : ISnapshotStorage
        {
            public string raw;
            public string ReadPrimary() => raw;
            public string ReadBackup() => null;
            public void Write(string value) { raw = value; }
        }
        ModelState Prepared(long price)
        {
            ModelState state;
            if (price == 540)
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../Checks/Milestones/fixtures/v2-selected540.json"));
                byte[] raw = File.ReadAllBytes(path);
                using (var sha = SHA256.Create())
                    Assert.That(BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", ""),
                        Is.EqualTo("CAD546964362B38ECCD057F98248F40FF43E6920839DC9125EEF5758F0CE678E"), "Copied old540 source fixture changed");
                state = codec.Decode(File.ReadAllText(path));
            }
            else
            {
                Assert.That(price, Is.EqualTo(600));
                state = ModelState.Fresh(1); state.houseStage = 1; state.selectedStage = 2; state.autoHintSeen = 1;
            }
            // Explicit synthetic funding/legacy-selected boundary, not an earned
            // playthrough. V2 selection comes from the unchanged frozen fixture;
            // V1 is the supported absent-pricing legacy format. No paid receipt
            // or historical spin is invented or rewritten.
            state.coins = price - 5; state.lastSettledAt = 1000; GameLogic.Validate(state);
            var setup = GameSession.Open(new GameStore(new Memory { raw = codec.Encode(state) }, codec), 1, initializeJourney: true);
            Assert.That(ChapterEconomy.SelectedProjectPrice(setup.Chapter), Is.EqualTo(price));
            setup.SelectExpansionSpeed("speed.fast", setup.State.sequence, 1000); // Genuine zero-cost adoption command.
            state = setup.State;
            var fields = ExpansionCodec.EncodeState(state.expansion).Split('|');
            Assert.That(fields.Length, Is.EqualTo(21)); Assert.That(fields[0], Is.EqualTo("expansion.state.v1"));
            fields[1] = "60"; // Synthetic initial Parts only; no resource/history receipt is fabricated.
            state.expansion = ExpansionCodec.DecodeState(string.Join("|", fields));
            GameLogic.Validate(state);
            Assert.That(state.expansion.ForegroundSpins, Is.Zero); Assert.That(state.expansion.LastResourceReceipt, Is.Null);
            Assert.That(state.expansion.LastCommand.Action, Is.EqualTo("speed"));
            return state;
        }
        ExpansionQuote Quote()
        {
            var q = Field<ExpansionQuote>("expansionQuote"); Assert.That(q, Is.Not.Null);
            Assert.That(q.Action, Is.EqualTo("buy")); Assert.That(q.Id, Is.EqualTo("trade.parts")); Assert.That(q.Slot, Is.Zero);
            Assert.That(q.ExpectedSequence, Is.EqualTo(Session.State.sequence)); Assert.That(q.Sequence, Is.EqualTo(Session.State.sequence + 1));
            Assert.That(new[] { (long)q.PartsCost, q.PotatoesCost, q.EnergyCost, q.CoinsCost, q.CoinsReward }, Is.EqualTo(new long[] { 20, 0, 0, 0, 20 }));
            return q;
        }
        ModelState WithoutExpansion(ModelState state)
        {
            Assert.That(state.pending, Is.Null); Assert.That(state.lastSpin, Is.Null, "Prepared exchange boundary has no spin history");
            var copy = state.Copy(); copy.expansion = null;
            return copy;
        }
        void Committed(ModelState lowerBefore, long price, int trades, ExpansionQuote q)
        {
            Assert.That(Session.State.coins, Is.EqualTo(price - 5 + trades * 20));
            Assert.That(Session.State.seed, Is.EqualTo(1u)); Assert.That(Session.State.houseStage, Is.EqualTo(1));
            Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo("house.improve"));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(price));
            Assert.That(Session.Chapter.Progress.CompletedUpgradeIds.Count, Is.EqualTo(1));
            var expected = lowerBefore.Copy(); expected.sequence += trades; expected.revision = expected.sequence * 2;
            expected.coins = price - 5 + trades * 20;
            Assert.That(codec.Encode(WithoutExpansion(Session.State)), Is.EqualTo(codec.Encode(expected)),
                "Exchange may only change wallet and normal sequence/revision in the complete older game state");
            Assert.That(new[] { Session.Expansion.Parts, Session.Expansion.Potatoes, Session.Expansion.Energy }, Is.EqualTo(new[] { 60 - trades * 20, 0, 0 }));
            Assert.That(Session.State.sequence, Is.EqualTo(q.Sequence)); Assert.That(Session.Expansion.LastCommand.Key, Is.EqualTo(q.Key));
            Assert.That(Session.Expansion.LastCommand.Id, Is.EqualTo("trade.parts"));
            Assert.That(Session.State.pending, Is.Null); Assert.That(File.ReadAllText(Path.Combine(Folder, "jtn.save.v1.json")), Is.EqualTo(State));
        }
        IEnumerator Trade(long price)
        {
            yield return Harness("Load", Prepared(price)); yield return null;
            Assert.That(Session.State.coins, Is.EqualTo(price - 5)); Assert.That(Session.Expansion.Parts, Is.EqualTo(60));
            var lower = WithoutExpansion(Session.State);
            string before = State; var files = Files();
            yield return Click(Field<Button[]>("productTabs")[3]);
            yield return Click(Field<Button[]>("productCards")[4]);
            int index = Array.IndexOf(Field<string[]>("expansionCardIds"), "trade.parts"); Assert.That(index, Is.GreaterThanOrEqualTo(0));
            yield return Click(Field<Button[]>("expansionCards")[index]); Unchanged(before, files);
            var first = Quote(); var primary = Field<Button>("expansionPrimaryButton"); var oldPrimaryEvent = primary.onClick;
            yield return Click(primary); Committed(lower, price, 1, first);
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("Credited: 20 coins."));
            before = State; files = Files(); oldPrimaryEvent.Invoke(); oldPrimaryEvent.Invoke(); Unchanged(before, files);

            var repeat = Field<Button>("expansionSecondaryButton"); var oldRepeatEvent = repeat.onClick;
            var second = Quote(); Assert.That(second.Key, Is.Not.EqualTo(first.Key));
            var held = Down(repeat, 511);
            // Separate fresh contact explicitly authorizes only the second trade.
            Release(Down(repeat, 512), repeat); Committed(lower, price, 2, second);
            yield return null;
            Assert.That(Field<Button>("expansionSecondaryButton"), Is.SameAs(repeat));
            var third = Quote(); Assert.That(Session.Expansion.Parts, Is.EqualTo(20), "Third exchange must still be affordable for the stale-input challenge");
            Assert.That(third.Key, Is.Not.EqualTo(second.Key));
            before = State; files = Files();
            Release(held, repeat); Unchanged(before, files);
            oldRepeatEvent.Invoke(); oldRepeatEvent.Invoke(); oldPrimaryEvent.Invoke(); Unchanged(before, files);
            Assert.That(Field<ExpansionQuote>("expansionQuote").Key, Is.EqualTo(third.Key), "Stale input must retain the fresh affordable third offer without executing it");
            TestContext.WriteLine("Simulated actual-scene uGUI: saved" + price + " goal stays unpaid/priced; wallet " + (price - 5) + "->" + (price + 15) + "->" + (price + 35) + "; Parts60->40->20. Old held/rebound events cannot authorize an affordable third exchange.");
            if(price==540)
            {
                // A separate explicit Submit spends the final affordable quote;
                // the exhausted result must not retain an actionable next trade.
                ExecuteEvents.Execute(repeat.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
                yield return null;Committed(lower,price,3,third);
                Assert.That(Session.State.coins,Is.EqualTo(595));
                Assert.That(Field<ExpansionQuote>("expansionQuote"),Is.Null);
                Assert.That(repeat.gameObject.activeInHierarchy,Is.False);
                Assert.That(Field<TMP_Text>("expansionBody").text,Does.Contain("Next exchange:").And.Contain("Missing: 20 parts"));
                before=State;files=Files();oldRepeatEvent.Invoke();oldPrimaryEvent.Invoke();Unchanged(before,files);
                var back=Game.GetComponent("JtnBackNavigation");
                Assert.That((bool)back.GetType().GetMethod("RequestBack").Invoke(back,null),Is.True);
                yield return null;Unchanged(before,files);Assert.That(Field<int>("wavePanel"),Is.EqualTo(51));
                TestContext.WriteLine("Separate Submit performs only third540 exchange575->595, Parts20->0; exhausted repeat has no quote/CTA and names20 missing Parts; production Back preserves full state/files.");
            }
        }
        [SetUp] public void Isolate() { ui.Isolate(); }
        [UnityTest] public IEnumerator PartsExchangeKeepsSaved540GoalAndRequiresFreshRepeatConsent() { yield return Trade(540); }
        [UnityTest] public IEnumerator PartsExchangeKeepsSaved600GoalAndRequiresFreshRepeatConsent() { yield return Trade(600); }
        [UnityTearDown] public IEnumerator Restore() { yield return ui.RestorePrivateContext(); }
    }
}
#endif
