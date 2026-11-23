#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using JunkToNeon.Contracts;
using JunkToNeon.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JunkToNeon.Tests.PlayMode
{
    public sealed class ForegroundHitchPresentationTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly ReelUiLifecycleTests ui = new ReelUiLifecycleTests();
        readonly JsonStateCodec codec = new JsonStateCodec();
        Component Game => (Component)typeof(ReelUiLifecycleTests).GetField("game", Hidden).GetValue(ui);
        GameSession Session => (GameSession)Game.GetType().GetProperty("Session").GetValue(Game);
        bool Flag(string name) => (bool)Game.GetType().GetProperty(name).GetValue(Game);
        string DirectoryPath => (string)typeof(ReelUiLifecycleTests).GetField("directory", Hidden).GetValue(ui);

        [SetUp] public void Isolate() { ui.IsolateSave(); }
        sealed class FixtureStorage : ISnapshotStorage
        {
            public string Raw;
            public string ReadPrimary() => Raw; public string ReadBackup() => null;
            public void Write(string raw) { Raw = raw; }
        }
        ModelState SelectedHouse()
        {
            var state = ModelState.Fresh(1); state.coins = 200;
            var setup = GameSession.Open(new GameStore(new FixtureStorage { Raw = codec.Encode(state) }, codec), 1);
            setup.SelectChapterProject(GameIds.HouseRepair, 1); setup.AcknowledgeAutoHint(2);
            setup.SelectChapterProject(GameIds.HouseImprove, 3);
            Assert.That(setup.State.coins, Is.EqualTo(160));
            Assert.That(ChapterEconomy.SelectedProjectPrice(setup.Chapter), Is.EqualTo(480));
            return setup.State;
        }

        [UnityTest, Timeout(18000)]
        public IEnumerator PrecedingRealFrameHitchCannotConsumeNewSpinPresentation()
        {
            yield return (IEnumerator)typeof(ReelUiLifecycleTests).GetMethod("Prepare", Hidden).Invoke(ui, new object[] { SelectedHouse() });
            Assert.That(Path.GetPathRoot(DirectoryPath).Replace('\\', '/'), Is.EqualTo("X:/").IgnoreCase);
            Assert.That(Flag("Busy") || Flag("InBackground") || Flag("AutoRunning"), Is.False);
            var spin = Game.GetType().GetMethod("Spin");
            long beforeSequence = Session.State.sequence;
            int presentedBefore = (int)Game.GetType().GetProperty("PresentedFrames").GetValue(Game);

            yield return null; // A real test-runner frame, not a ScriptEval invocation.
            int stalledFrame = Time.frameCount;
            var hitch = System.Diagnostics.Stopwatch.StartNew();
            System.Threading.Thread.Sleep(4000); // One explicitly authorized main-thread hitch.
            hitch.Stop();
            yield return null; // Observe the next actual frame's unscaled delta.
            int startFrame = Time.frameCount;
            float deltaBeforeSpin = Time.unscaledDeltaTime;
            Assert.That(startFrame, Is.GreaterThan(stalledFrame));
            Assert.That(hitch.Elapsed.TotalSeconds, Is.GreaterThanOrEqualTo(3.8));
            Assert.That(Flag("InBackground"), Is.False);

            // Do not call the pointer helper here: its own yield would consume the
            // precise hitch frame under examination. Invoke the actual public handler.
            var presentation = System.Diagnostics.Stopwatch.StartNew();
            spin.Invoke(Game, null);
            double handlerSeconds = presentation.Elapsed.TotalSeconds;
            Assert.That(Flag("Busy"), Is.True);
            while (Flag("Busy") && presentation.Elapsed.TotalSeconds < 8) yield return null;
            presentation.Stop();

            var state = Session.State;
            bool primaryEqual = File.ReadAllText(Path.Combine(DirectoryPath, "jtn.save.v1.json")) == codec.Encode(state);
            TestContext.WriteLine("Hitch wall=" + hitch.Elapsed.TotalSeconds.ToString("F6") + "s; before-Spin unscaledDeltaTime=" + deltaBeforeSpin.ToString("F6") +
                "s; handler=" + handlerSeconds.ToString("F6") + "s; Spin-through-Busy=false wall=" + presentation.Elapsed.TotalSeconds.ToString("F6") +
                "s; frames=" + stalledFrame + "->" + startFrame + "->" + Time.frameCount + "; coins=" + state.coins + "; primaryEqual=" + primaryEqual);
            Assert.That(Flag("Busy") || Flag("InBackground") || Flag("AutoRunning"), Is.False);
            Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
            Assert.That(state.coins, Is.EqualTo(168)); Assert.That(state.sequence, Is.EqualTo(beforeSequence + 1));
            Assert.That(state.lastSpin.reward, Is.EqualTo(8)); Assert.That(state.lastSpin.bonus, Is.Zero);
            Assert.That(state.lastSpin.symbols, Is.EqualTo(new[] { 0, 0, 2 })); Assert.That(state.seed, Is.EqualTo(2647435461u));
            Assert.That(state.pending, Is.Null); Assert.That(state.houseStage, Is.EqualTo(1));
            Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo(GameIds.HouseImprove));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(480));
            Assert.That(primaryEqual, Is.True, "Full committed private snapshot must equal the live state");
            Assert.That((int)Game.GetType().GetProperty("PresentedFrames").GetValue(Game), Is.GreaterThan(presentedBefore));
            Assert.That(presentation.Elapsed.TotalSeconds, Is.GreaterThanOrEqualTo(3.45),
                "Time before this Spin must not shorten its new3.5-second foreground presentation");
        }

        [UnityTest, Timeout(18000)]
        public IEnumerator BackgroundWithoutFramesCannotConsumeRemainingForegroundPresentation()
        {
            yield return (IEnumerator)typeof(ReelUiLifecycleTests).GetMethod("Prepare", Hidden).Invoke(ui, new object[] { SelectedHouse() });
            Assert.That(Path.GetPathRoot(DirectoryPath).Replace('\\', '/'), Is.EqualTo("X:/").IgnoreCase);
            Assert.That(Flag("Busy") || Flag("InBackground") || Flag("AutoRunning"), Is.False);
            var spin = Game.GetType().GetMethod("Spin");
            var pause = Game.GetType().GetMethod("OnApplicationPause", Hidden);
            long beforeSequence = Session.State.sequence;
            yield return null;

            var foreground = System.Diagnostics.Stopwatch.StartNew();
            spin.Invoke(Game, null);
            Assert.That(Flag("Busy"), Is.True);
            pause.Invoke(Game, new object[] { true }); foreground.Stop();
            Assert.That(Flag("InBackground"), Is.True);
            string committedBeforeBackground = codec.Encode(Session.State);
            int suspendedFrame = Time.frameCount;
            var background = System.Diagnostics.Stopwatch.StartNew();
            System.Threading.Thread.Sleep(4000); // No engine frames occur between the callbacks.
            background.Stop();
            Assert.That(Time.frameCount, Is.EqualTo(suspendedFrame));
            foreground.Start(); pause.Invoke(Game, new object[] { false });
            Assert.That(Time.frameCount, Is.EqualTo(suspendedFrame), "Resume must be in the very same engine frame");
            Assert.That(codec.Encode(Session.State), Is.EqualTo(committedBeforeBackground), "Same-clock pause/resume must not grant or replay an economic command");
            yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(suspendedFrame));
            Assert.That(Flag("Busy"), Is.True, "The first frame after a no-frame absence consumed the remaining presentation");
            while (Flag("Busy") && foreground.Elapsed.TotalSeconds < 8) yield return null;
            foreground.Stop();

            var state = Session.State;
            bool primaryEqual = File.ReadAllText(Path.Combine(DirectoryPath, "jtn.save.v1.json")) == codec.Encode(state);
            TestContext.WriteLine("No-frame background wall=" + background.Elapsed.TotalSeconds.ToString("F6") +
                "s; foreground Spin-through-Busy=false excluding background=" + foreground.Elapsed.TotalSeconds.ToString("F6") +
                "s; frames=" + suspendedFrame + "->" + Time.frameCount + "; primaryEqual=" + primaryEqual);
            Assert.That(Flag("Busy") || Flag("InBackground") || Flag("AutoRunning"), Is.False);
            Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
            Assert.That(state.coins, Is.EqualTo(168)); Assert.That(state.sequence, Is.EqualTo(beforeSequence + 1));
            Assert.That(state.lastSpin.reward, Is.EqualTo(8)); Assert.That(state.lastSpin.bonus, Is.Zero);
            Assert.That(state.lastSpin.symbols, Is.EqualTo(new[] { 0, 0, 2 })); Assert.That(state.seed, Is.EqualTo(2647435461u));
            Assert.That(state.pending, Is.Null); Assert.That(state.houseStage, Is.EqualTo(1));
            Assert.That(Session.Chapter.SelectedProjectId, Is.EqualTo(GameIds.HouseImprove));
            Assert.That(ChapterEconomy.SelectedProjectPrice(Session.Chapter), Is.EqualTo(480));
            Assert.That(primaryEqual, Is.True);
            Assert.That(background.Elapsed.TotalSeconds, Is.GreaterThanOrEqualTo(3.8));
            Assert.That(foreground.Elapsed.TotalSeconds, Is.GreaterThanOrEqualTo(3.45),
                "A no-frame background interval must not count as foreground presentation time");
        }

        [UnityTearDown]
        public IEnumerator RestoreFixture()
        {
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                TestContext.WriteLine("Preserved failed hitch fixture: " + DirectoryPath);
                typeof(ReelUiLifecycleTests).GetField("directory", Hidden).SetValue(ui, null);
            }
            yield return ui.RestoreAndRemoveOnlyPrivateFixtures();
        }
    }
}
#endif
