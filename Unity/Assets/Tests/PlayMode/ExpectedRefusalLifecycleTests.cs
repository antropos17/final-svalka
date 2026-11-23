#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using JunkToNeon.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    // Reuse the qualified scene/save/pointer fixture, not a parser or Runtime stand-in.
    public sealed class ExpectedRefusalLifecycleTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui = new ReelUiLifecycleTests();
        readonly JsonStateCodec codec = new JsonStateCodec();
        object FixtureField(string name) => typeof(ReelUiLifecycleTests).GetField(name, Hidden).GetValue(ui);
        Component Game => (Component)FixtureField("game");
        GameSession Session => (GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        T Field<T>(string name) => (T)Game.GetType().GetField(name, Hidden).GetValue(Game);
        bool Flag(string name) => (bool)Game.GetType().GetProperty(name).GetValue(Game);
        IEnumerator Harness(string name, params object[] args) => (IEnumerator)typeof(ReelUiLifecycleTests).GetMethod(name, Hidden).Invoke(ui, args);
        void Runtime(string name, params object[] args) => Game.GetType().GetMethod(name, Hidden | BindingFlags.Public).Invoke(Game, args);
        void Clock(long value) => Environment.SetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        IEnumerator Click(string field, int index = -1) => Harness("PointerClick", field, index);
        string PrivateDirectory => (string)FixtureField("directory");

        [SetUp] public void Isolate() { ui.IsolateSave(); }

        Dictionary<string, string> Inventory()
        {
            Assert.That(Path.GetPathRoot(PrivateDirectory).Replace('\\', '/'), Is.EqualTo("X:/").IgnoreCase, "The inherited per-process Temp must be on X:");
            return Directory.GetFiles(PrivateDirectory, "jtn.*.json").ToDictionary(Path.GetFileName,
                path => File.GetLastWriteTimeUtc(path).Ticks + ":" + Convert.ToBase64String(File.ReadAllBytes(path)));
        }
        void NoWrites(Dictionary<string, string> before)
        { Assert.That(Inventory(), Is.EquivalentTo(before), "Expected refusal changed private primary/backup bytes, inventory or last-write metadata"); }
        void Healthy()
        {
            Assert.That(Session.RecoveryRequired || Field<bool>("failed"), Is.False, Session.Error);
            Assert.That(Session.Error, Is.Null);
            Assert.That(Field<Button>("reloadButton").gameObject.activeSelf, Is.False);
            var diagnostics = (IReadOnlyList<DiagnosticRecord>)Game.GetType().GetProperty("LocalDiagnostics").GetValue(Game);
            Assert.That(diagnostics.Any(item => item.Event.Kind == LocalEventKind.PersistenceError), Is.False, "Expected refusal was mislabeled as persistence failure");
        }
        IEnumerator OpenSkill()
        {
            // Skills are reached through Upgrades > Skills; the legacy root menu is hidden by the product navigation.
            yield return Click("productTabs", 4); yield return Click("productCards", 3); yield return Click("navigationCards", 0);
        }
        IEnumerator OpenReadyOrder()
        { yield return Click("productTabs", 2); yield return Click("productCards", 2); yield return Click("navigationCards", 0); }
        IEnumerator ResumeWithoutIncome(long resumeAt)
        {
            Clock(1001); Runtime("OnApplicationPause", true);
            Assert.That(Flag("InBackground"), Is.True); yield return null;
            Clock(resumeAt); Runtime("OnApplicationPause", false);
            Assert.That(Flag("InBackground") || Flag("AutoRunning"), Is.False);
            Assert.That(Field<bool>("offlineSummaryPanel"), Is.False, "This case intentionally retains the displayed quote, with no return-income modal");
            yield return null;
        }

        sealed class MemoryFixture : ISnapshotStorage
        {
            public string Raw;
            public string ReadPrimary() => Raw; public string ReadBackup() => null;
            public void Write(string raw) { Raw = raw; }
        }
        GameSession Fixture(ModelState state) => GameSession.Open(new GameStore(new MemoryFixture { Raw = codec.Encode(state) }, codec), 1);
        ModelState SelectedHouse()
        {
            var state = ModelState.Fresh(1); state.coins = 200;
            var fixture = Fixture(state);
            fixture.SelectChapterProject(GameIds.HouseRepair, fixture.State.sequence + 1);
            fixture.AcknowledgeAutoHint(fixture.State.sequence + 1);
            fixture.SelectChapterProject(GameIds.HouseImprove, fixture.State.sequence + 1);
            Assert.That(fixture.State.coins, Is.EqualTo(160));
            Assert.That(ChapterEconomy.SelectedProjectPrice(fixture.Chapter), Is.EqualTo(480), "Current V3 new-selection price changed");
            return fixture.State;
        }
        ModelState ReadyOrderAtWalletLimit()
        {
            var state = ModelState.Fresh(1); state.coins = 10000;
            var fixture = Fixture(state);
            foreach (string era in new[] { EraIds.Junkyard, EraIds.WorkshopYard })
            {
                if (era == EraIds.WorkshopYard) { fixture.EnterWorkshopYard(fixture.State.sequence + 1); fixture.AcknowledgeEraTransition(fixture.State.sequence + 1); }
                foreach (var family in FamilyIds.All) for (int level = 1; level <= 2; level++)
                {
                    string project = ChapterIds.Project(era, family, level);
                    fixture.SelectChapterProject(project, fixture.State.sequence + 1);
                    if (project == GameIds.HouseRepair) fixture.AcknowledgeAutoHint(fixture.State.sequence + 1);
                }
            }
            fixture.AcceptOrder("order.sort-salvage", fixture.State.sequence + 1);
            for (int i = 0; i < 16; i++) fixture.ExecuteChapterSpin(fixture.State.sequence + 1);
            Assert.That(fixture.State.coins, Is.EqualTo(1062), "Independent V3: 10000-9310+372; V3 lowered House II by 60, Cart by 20 and Power Source by 10");
            Assert.That(fixture.Journey.Orders.Ready, Is.True);
            state = fixture.State;
            // Explicit private boundary funding; not an offline grant or a player snapshot.
            state.coins = long.MaxValue - 100; GameLogic.Validate(state);
            return state;
        }

        [UnityTest]
        public IEnumerator ResumeStalesSkillWithoutWritesButActualCorruptionStillBlocks()
        {
            var state = SelectedHouse(); long start = state.sequence;
            yield return Harness("Prepare", state); yield return OpenSkill();
            Assert.That(Field<Button>("selectButton").gameObject.activeInHierarchy, Is.True);
            yield return ResumeWithoutIncome(1061); // No workshop: even a full minute grants zero.
            Assert.That(Session.State.sequence, Is.EqualTo(start + 2));
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True);
            var before = Inventory(); string snapshot = codec.Encode(Session.State);
            yield return Click("selectButton");
            NoWrites(before); Assert.That(codec.Encode(Session.State), Is.EqualTo(snapshot)); Healthy();
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            Assert.That(Session.Chapter.Skills.Owns(SkillIds.Universal1), Is.False);
            Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo(GameIds.HouseImprove));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(480));
            Assert.That(Session.State.coins, Is.EqualTo(160));
            yield return Click("spinButton"); yield return Harness("FinishPresentation"); Healthy();
            Assert.That(Session.State.coins, Is.EqualTo(168)); Assert.That(Session.State.sequence, Is.EqualTo(start + 3));
            Assert.That(Session.State.lastSpin.symbols, Is.EqualTo(new[] { 0, 0, 2 }));
            Assert.That(Session.State.houseStage, Is.EqualTo(1));

            const string corrupt = "{private expected-refusal corruption fixture";
            File.WriteAllText(Path.Combine(PrivateDirectory, "jtn.save.v1.json"), corrupt);
            before = Inventory();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Junk to Neon storage stopped:"));
            Runtime("Reload");
            Assert.That(Session.RecoveryRequired && Field<bool>("failed"), Is.True);
            Assert.That(Field<Button>("spinButton").interactable, Is.False);
            Runtime("Spin"); Runtime("ToggleAuto"); NoWrites(before);
            Assert.That(File.ReadAllText(Path.Combine(PrivateDirectory, "jtn.save.v1.json")), Is.EqualTo(corrupt));
            TestContext.WriteLine("Actual pause/resume invalidated a visible skill quote: no writes/charge/hidden480 purchase, healthy pair+8 still works; genuine private corruption then blocks without reset.");
        }

        [UnityTest]
        public IEnumerator ResumedOrderQuoteAndFreshOverflowRefuseWithoutBlockingLaterSpendAndClaim()
        {
            yield return Harness("Prepare", ReadyOrderAtWalletLimit()); yield return OpenReadyOrder();
            long start = Session.State.sequence, instance = Session.Journey.Orders.InstanceSequence;
            uint seed = Session.State.seed; long spin = Session.State.lastSpin.sequence;
            yield return ResumeWithoutIncome(1002); // Workshop owned, but only one second elapsed.
            Assert.That(Session.State.sequence, Is.EqualTo(start + 2));
            var before = Inventory(); string snapshot = codec.Encode(Session.State);
            yield return Click("selectButton"); NoWrites(before); Healthy();
            Assert.That(codec.Encode(Session.State), Is.EqualTo(snapshot));
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            yield return OpenReadyOrder(); before = Inventory();
            yield return Click("selectButton"); // Fresh quote: adding120 to MaxValue-100 overflows.
            NoWrites(before); Healthy();
            Assert.That(Session.State.coins, Is.EqualTo(long.MaxValue - 100));
            Assert.That(Session.State.sequence, Is.EqualTo(start + 2));
            Assert.That(Session.Journey.Orders.Ready && Session.Journey.Orders.InstanceSequence == instance, Is.True);
            Assert.That(Session.Chapter.SelectedProjectId, Is.Null);
            yield return OpenSkill(); yield return Click("selectButton"); // Explicit120 debit creates room.
            Assert.That(Session.State.coins, Is.EqualTo(long.MaxValue - 220)); Healthy();
            yield return OpenReadyOrder(); yield return Click("selectButton");
            Assert.That(Session.State.coins, Is.EqualTo(long.MaxValue - 100));
            Assert.That(Session.State.sequence, Is.EqualTo(start + 4));
            Assert.That(Session.Journey.Orders.ActiveOrderId, Is.Null);
            Assert.That(Session.State.seed, Is.EqualTo(seed)); Assert.That(Session.State.lastSpin.sequence, Is.EqualTo(spin));
            Assert.That(Flag("Busy") || Flag("AutoRunning"), Is.False); Healthy();
            TestContext.WriteLine("Stale resumed order quote and fresh120 overflow each wrote nothing; healthy actual UI then spent120 and claimed120 once, preserving RNG/spin history and selected-none state.");
        }

        [UnityTearDown]
        public IEnumerator RestoreFixture()
        {
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                TestContext.WriteLine("Preserved failed expected-refusal fixture: " + PrivateDirectory);
                typeof(ReelUiLifecycleTests).GetField("directory", Hidden).SetValue(ui, null);
            }
            yield return ui.RestoreAndRemoveOnlyPrivateFixtures();
        }
    }
}
#endif
