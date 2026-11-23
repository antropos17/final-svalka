#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using JunkToNeon.Core;
using JunkToNeon.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.BackNavigation
{
    public sealed class BackNavigationTests
    {
        const string ScenePath = "Assets/Scenes/JunkToNeon.unity";
        const string Override = "JTN_EDITOR_SAVE_DIRECTORY";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        string directory, previousOverride, previousClock;
        Scene scene, previousScene;
        Component game;
        Type gameType;
        GameObject inputHost;
        readonly Dictionary<string, string> normalFiles = new Dictionary<string, string>();
        readonly JsonStateCodec codec = new JsonStateCodec();
        JtnBackNavigation back;
        UnityEngine.InputSystem.Keyboard syntheticKeyboard, previousKeyboard;
        GameSession Session => (GameSession)gameType.GetProperty("Session").GetValue(game);
        T Field<T>(string name) => (T)gameType.GetField(name, Private).GetValue(game);
        void Call(string name) => gameType.GetMethod(name, Private | BindingFlags.Public).Invoke(game, null);
        IEnumerable<string> NormalPaths() => Directory.Exists(Application.persistentDataPath)
            ? Directory.GetFiles(Application.persistentDataPath, "jtn.*.json") : Array.Empty<string>();
        Dictionary<string, string> ObserveNormal()
        {
            var observation = new Dictionary<string, string>();
            foreach (var path in NormalPaths())
            {
                try { observation[path] = Convert.ToBase64String(File.ReadAllBytes(path)); }
                catch (IOException) { observation[path] = "Changing or inaccessible during read"; }
            }
            return observation;
        }
        Dictionary<string, string> Files() => Directory.GetFiles(directory).ToDictionary(Path.GetFileName,
            path => Convert.ToBase64String(File.ReadAllBytes(path)));

        [SetUp]
        public void Isolate()
        {
            previousOverride = Environment.GetEnvironmentVariable(Override);
            previousClock=Environment.GetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS");
            Environment.SetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS","1000");
            previousScene = SceneManager.GetActiveScene();
            scene = default; game = null; inputHost = null; directory = null;
            normalFiles.Clear();
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False, "Use a neutral test scene");
            directory = Path.Combine(Path.GetTempPath(), "jtn-back-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            TestContext.WriteLine("Owned Back fixture: " + Path.GetFullPath(directory));
            foreach (var entry in ObserveNormal()) normalFiles[entry.Key] = entry.Value;
            Environment.SetEnvironmentVariable(Override, directory);
            gameType = Type.GetType("JunkToNeon.JunkGame, Assembly-CSharp", true);
        }

        IEnumerator Load(bool corrupt, bool productionBinding = false)
        {
            var state = ModelState.Fresh(1);
            state.lastSettledAt=1000;
            if (corrupt)
            {
                File.WriteAllText(Path.Combine(directory, "jtn.save.v1.json"), "{corrupt primary");
                state.coins = 91;
                File.WriteAllText(Path.Combine(directory, "jtn.backup.v1.json"), codec.Encode(state));
                LogAssert.Expect(LogType.Warning, new Regex("Junk to Neon storage stopped:"));
            }
            else File.WriteAllText(Path.Combine(directory, "jtn.save.v1.json"), codec.Encode(state));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
            game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(gameType, true)).Single();
            var store = typeof(GameSession).GetField("store", Private).GetValue(Session);
            var storage = (FileSnapshotStorage)typeof(GameStore).GetField("storage", Private).GetValue(store);
            foreach (var field in typeof(FileSnapshotStorage).GetFields(Private).Where(field => field.FieldType == typeof(string)))
                Assert.That(Path.GetDirectoryName(Path.GetFullPath((string)field.GetValue(storage))),
                    Is.EqualTo(Path.GetFullPath(directory)), "Session storage must use this test's private directory: " + field.Name);
            back = game.GetComponent<JtnBackNavigation>();
            if(back == null) back = game.gameObject.AddComponent<JtnBackNavigation>();
            if(!productionBinding) back.Configure(() => Field<bool>("failed")
                    ? (Field<bool>("restoreConfirmation") ? BackContext.RestoreConfirmation : BackContext.BlockingError)
                    : (Field<GameObject>("panel").activeSelf ? BackContext.DismissiblePanel : BackContext.Root),
                () => Call("ClosePanel"), () => Call("Reload"), null, () => Call("StopAuto"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreviewBackClosesWithoutSelectingOrSpinningAndRootIsHarmless()
        {
            yield return Load(false);
            var before = Files();
            Call("ShowProject");
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True);
            Assert.That(back.RequestBack(), Is.True);
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
            Assert.That(back.RequestBack(), Is.False, "Same-frame duplicate must not reach a different context");
            yield return null;
            Assert.That(back.RequestBack(), Is.True, "Root request with no confirmation callback is safe");
            yield return null;
            back.RequestBack();
            Assert.That(Session.State.coins, Is.EqualTo(37));
            Assert.That(Session.State.sequence, Is.Zero);
            Assert.That(Session.State.selectedStage, Is.Zero);
            Assert.That(Session.State.lastSpin, Is.Null);
            Assert.That((bool)gameType.GetProperty("Busy").GetValue(game), Is.False);
            Assert.That(Files(), Is.EquivalentTo(before));
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator RestoreWarningBackCancelsWithoutWritingAndBlockingErrorStaysBlocked()
        {
            yield return Load(true);
            Assert.That(Session.RecoveryRequired && Session.BackupAvailable, Is.True);
            var before = Files();
            Assert.That(back.RequestBack(), Is.True);
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True);
            yield return null;
            Call("SelectProject"); // The existing action opens a warning; it must not restore yet.
            Assert.That(Field<bool>("restoreConfirmation"), Is.True);
            Call("ClosePanel"); // Existing keyboard branch's exact action demonstrates the gap.
            Assert.That(Field<bool>("restoreConfirmation"), Is.True, "Current ClosePanel ignores failed-state cancellation");
            Assert.That(Files(), Is.EquivalentTo(before));
            LogAssert.Expect(LogType.Warning, new Regex("Junk to Neon storage stopped:"));
            Assert.That(back.RequestBack(), Is.True);
            Assert.That(Field<bool>("restoreConfirmation"), Is.False);
            Assert.That(Session.RecoveryRequired, Is.True);
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True);
            Assert.That(Field<Button>("closeButton").gameObject.activeSelf, Is.False);
            foreach (var name in new[] { "spinButton", "autoButton", "projectButton" })
                Assert.That(Field<Button>(name).interactable, Is.False, name + " must stay blocked");
            yield return null;
            back.RequestBack();
            Assert.That(Session.RecoveryRequired, Is.True);
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True);
            Assert.That(Files(), Is.EquivalentTo(before), "Back must preserve every fixture file byte-for-byte");
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(2), "No recovery/preservation writes on cancel");
        }

        [UnityTest]
        public IEnumerator AdapterRequestsRootConfirmationAndCancelsWithoutConfirmingOrRepeating()
        {
            // Explicit adapter requests avoid altering Editor input settings or sending OS Escape.
            inputHost = new GameObject("Private Back input fixture");
            back = inputHost.AddComponent<JtnBackNavigation>();
            int closes = 0, cancels = 0, roots = 0, stops = 0;
            var context = BackContext.Root;
            back.Configure(() => context, () => closes++, () => cancels++, () => roots++, () => stops++);
            Assert.That(back.RequestBack(), Is.True);
            Assert.That(back.RequestBack(), Is.False);
            yield return null;
            yield return null;
            Assert.That(roots, Is.EqualTo(1), "One request opens confirmation; idle frames do not repeat it");
            Assert.That(closes, Is.Zero); Assert.That(cancels, Is.Zero); Assert.That(stops, Is.EqualTo(1));
            context = BackContext.RestoreConfirmation;
            back.RequestBack();
            yield return null;
            yield return null;
            Assert.That(cancels, Is.EqualTo(1)); Assert.That(roots, Is.EqualTo(1)); Assert.That(closes, Is.Zero);
            back.enabled = false;
            Assert.That(back.RequestBack(), Is.False, "Disabled adapter cannot route callbacks");
            Assert.That(Files(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ConnectedEscapeRoutesRootAndRestoreCancelWithoutDuplicateHandling()
        {
            yield return Load(false, true);
            var before = Files();
            previousKeyboard = UnityEngine.InputSystem.Keyboard.current;
            syntheticKeyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("PrivateBackKeyboard");
            syntheticKeyboard.MakeCurrent();
            var viewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView", true);
            UnityEditor.EditorWindow.GetWindow(viewType).Focus();
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(syntheticKeyboard,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
            yield return null;
            Assert.That(Field<bool>("exitPanel"), Is.True, "Queued Escape must reach the production Update route once");
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(syntheticKeyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
            yield return null;
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True, "Held/idle frames must not repeat Back");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(syntheticKeyboard,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
            yield return null;
            Assert.That(Field<GameObject>("panel").activeSelf, Is.False, "Second Escape cancels exit, without quitting");
            Assert.That(Files(), Is.EquivalentTo(before));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(syntheticKeyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default; game = null;
            yield return Load(true, true);
            before = Files();
            Call("SelectProject");
            Assert.That(Field<bool>("restoreConfirmation"), Is.True);
            UnityEditor.EditorWindow.GetWindow(viewType).Focus();
            yield return null;
            LogAssert.Expect(LogType.Warning, new Regex("Junk to Neon storage stopped:"));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(syntheticKeyboard,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
            yield return null;
            Assert.That(Field<bool>("restoreConfirmation"), Is.False, "Queued Escape safely cancels restore warning");
            Assert.That(Session.RecoveryRequired, Is.True);
            Assert.That(Field<GameObject>("panel").activeSelf, Is.True);
            Assert.That(Files(), Is.EquivalentTo(before));
            TestContext.WriteLine("Queued keyboard state reached production Back Update: root open/cancel and no-write restore cancel; no Input System settings changed.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(syntheticKeyboard != null && syntheticKeyboard.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(syntheticKeyboard);
            if(previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();

            if (inputHost != null) UnityEngine.Object.Destroy(inputHost);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Environment.SetEnvironmentVariable(Override, previousOverride);
            Environment.SetEnvironmentVariable("JTN_EDITOR_UTC_SECONDS",previousClock);
            var after = ObserveNormal();
            bool changed = !after.OrderBy(entry => entry.Key).SequenceEqual(normalFiles.OrderBy(entry => entry.Key));
            TestContext.WriteLine("Read-only ordinary-save observation changed during parallel run: " + changed + ". No restoration attempted; actual session storage was asserted private.");
            if (directory != null)
            {
                foreach (var name in new[] { "jtn.save.v1.json", "jtn.backup.v1.json" })
                {
                    var path = Path.Combine(directory, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                Directory.Delete(directory, false);
            }
        }
    }
}
#endif
