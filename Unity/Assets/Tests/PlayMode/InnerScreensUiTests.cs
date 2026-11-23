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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JunkToNeon.Tests.PlayMode
{
    public sealed class InnerScreensUiTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        const string ScenePath = "Assets/Scenes/JunkToNeon.unity";
        const string SaveOverride = "JTN_EDITOR_SAVE_DIRECTORY", ClockOverride = "JTN_EDITOR_UTC_SECONDS";
        readonly JsonStateCodec codec = new JsonStateCodec();
        string folder, previousSave, previousClock;
        Scene scene, previousScene;
        Component game;
        Type gameType;
        GameSession Session => (GameSession)gameType.GetProperty("Session").GetValue(game);
        T Field<T>(string name) => (T)gameType.GetField(name, Hidden).GetValue(game);
        void Call(string name) => gameType.GetMethod(name, Hidden | BindingFlags.Public).Invoke(game, null);
        string State => codec.Encode(Session.State);
        Dictionary<string,string> Files()
        {
            Assert.That(Directory.GetDirectories(folder), Is.Empty);
            var paths = Directory.GetFiles(folder); Assert.That(paths.Length, Is.LessThanOrEqualTo(8));
            foreach (var path in paths)
            {
                Assert.That((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, Is.True);
                Assert.That(new FileInfo(path).Length, Is.LessThanOrEqualTo(262148));
            }
            return paths.ToDictionary(Path.GetFileName, path => Convert.ToBase64String(File.ReadAllBytes(path)) + "|" + File.GetLastWriteTimeUtc(path).Ticks);
        }
        void Unchanged(string state, Dictionary<string,string> files)
        {
            Assert.That(State, Is.EqualTo(state)); Assert.That(Files(), Is.EquivalentTo(files));
            Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
        }
        static RaycastResult Top(Button button, Vector2 local)
        {
            var canvas = button.GetComponentInParent<Canvas>();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, button.transform.TransformPoint(local)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), button.name);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), button.name);
            return hits[0];
        }
        static void Target(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, button.name);
            Canvas.ForceUpdateCanvases(); var rect = ((RectTransform)button.transform).rect;
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(48), button.name); Assert.That(rect.height, Is.GreaterThanOrEqualTo(48), button.name);
            foreach (var point in new[] { rect.center, new Vector2(rect.center.x, rect.yMin + 6),
                new Vector2(rect.center.x, rect.yMax - 6), new Vector2(rect.xMin + 6, rect.center.y),
                new Vector2(rect.xMax - 6, rect.center.y) }) Top(button, point);
        }
        IEnumerator Click(Button button)
        {
            yield return null; Target(button); int frame = Time.frameCount;
            var hit = Top(button, ((RectTransform)button.transform).rect.center);
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 851, button = PointerEventData.InputButton.Left,
                eligibleForClick = true, position = hit.screenPosition, pointerCurrentRaycast = hit };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            pointer.pointerPress = button.gameObject; pointer.pointerClick = button.gameObject;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null; Assert.That(Time.frameCount, Is.GreaterThan(frame));
        }
        static void Fits(TMP_Text text)
        {
            text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, text.name + ": " + text.text);
            Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 2), text.name);
            Assert.That(text.textBounds.size.y, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name);
        }
        void VisiblePage(string rootName, string titleName, string bodyName)
        {
            Canvas.ForceUpdateCanvases(); var root = Field<RectTransform>(rootName);
            Assert.That(root.gameObject.activeInHierarchy, Is.True, rootName);
            var title = Field<TMP_Text>(titleName); var body = Field<TMP_Text>(bodyName);
            Assert.That(title.isActiveAndEnabled && body.isActiveAndEnabled, Is.True); Fits(title); Fits(body);
            var panel = (RectTransform)Field<GameObject>("panel").transform;
            var wallet = Field<RectTransform>("productChrome").Find("Coin Wallet") as RectTransform;
            Assert.That(wallet, Is.Not.Null); Assert.That(wallet.gameObject.activeInHierarchy, Is.True);
            var canvas = panel.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var panelCorners = new Vector3[4]; var walletCorners = new Vector3[4];
            panel.GetWorldCorners(panelCorners); wallet.GetWorldCorners(walletCorners);
            float panelTop = panelCorners.Max(corner => RectTransformUtility.WorldToScreenPoint(camera, corner).y);
            float walletBottom = walletCorners.Min(corner => RectTransformUtility.WorldToScreenPoint(camera, corner).y);
            Assert.That(panelTop, Is.LessThan(walletBottom), "Modal must sit below the visible Coin Wallet");
            var titleBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, title.transform);
            var bodyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, body.transform);
            Assert.That(titleBounds.Intersects(bodyBounds), Is.False, "Title and content overlap");
            foreach (var text in root.GetComponentsInChildren<TMP_Text>()) if (text.isActiveAndEnabled) Fits(text);
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                var rect = ((RectTransform)button.transform).rect;
                Assert.That(rect.width, Is.GreaterThanOrEqualTo(48), button.name); Assert.That(rect.height, Is.GreaterThanOrEqualTo(48), button.name);
                Assert.That(bodyBounds.Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(panel, button.transform)), Is.False,
                    button.name + " overlaps content");
                if (button.isActiveAndEnabled && button.IsInteractable()) Target(button);
            }
            Target(Field<Button>("closeButton"));
        }
        static void StyledCard(Button button, string iconName)
        {
            Assert.That(button.targetGraphic.GetType().FullName, Is.EqualTo("JunkToNeon.JtnProductSurface"));
            Assert.That(button.targetGraphic.isActiveAndEnabled && button.targetGraphic.raycastTarget, Is.True);
            var icon = button.transform.Find(iconName); Assert.That(icon, Is.Not.Null, iconName);
            var graphic = icon.GetComponent<Graphic>(); Assert.That(graphic, Is.Not.Null);
            Assert.That(graphic.GetType().FullName, Is.EqualTo("JunkToNeon.JtnProductIcon"));
            Assert.That(graphic.isActiveAndEnabled, Is.True); Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(graphic.GetComponent<CanvasRenderer>(), Is.Not.Null);
        }
        [SetUp] public void Isolate()
        {
            previousSave = Environment.GetEnvironmentVariable(SaveOverride); previousClock = Environment.GetEnvironmentVariable(ClockOverride);
            previousScene = SceneManager.GetActiveScene();
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False, "Use a neutral test scene");
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.work/inner-ui-20261101/06/private", Guid.NewGuid().ToString("N")));
            Assert.That(Path.GetPathRoot(folder), Is.EqualTo("X:\\").IgnoreCase); Directory.CreateDirectory(folder);
            Environment.SetEnvironmentVariable(SaveOverride, folder); Environment.SetEnvironmentVariable(ClockOverride, "1000");
            gameType = Type.GetType("JunkToNeon.JunkGame, Assembly-CSharp", true);
        }
        IEnumerator Traverse(string state, Dictionary<string,string> files)
        {
            int objects = game.GetComponentsInChildren<RectTransform>(true).Length;
            int surfaces = game.GetComponentsInChildren<Graphic>(true).Count(g => g.GetType().FullName == "JunkToNeon.JtnProductSurface");
            int icons = game.GetComponentsInChildren<Graphic>(true).Count(g => g.GetType().FullName == "JunkToNeon.JtnProductIcon");
            Call("CreateExpansionUi"); Call("CreateArcadeUi"); Call("CreateInnerUi");
            Call("CreateExpansionUi"); Call("CreateArcadeUi"); Call("CreateInnerUi");
            Assert.That(game.GetComponentsInChildren<RectTransform>(true).Length, Is.EqualTo(objects));
            Assert.That(game.GetComponentsInChildren<Graphic>(true).Count(g => g.GetType().FullName == "JunkToNeon.JtnProductSurface"), Is.EqualTo(surfaces));
            Assert.That(game.GetComponentsInChildren<Graphic>(true).Count(g => g.GetType().FullName == "JunkToNeon.JtnProductIcon"), Is.EqualTo(icons)); Unchanged(state, files);
            var tabs = Field<Button[]>("productTabs");
            yield return Click(tabs[2]); yield return Click(Field<Button[]>("productCards")[0]);
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(62)); VisiblePage("arcadeRoot", "arcadeTitle", "arcadeBody");
            Assert.That(Field<TMP_Text>("arcadeTitle").text, Is.EqualTo("Memory"));
            Assert.That(Field<TMP_Text>("arcadeBody").text, Does.Contain("Start: 1 ticket").And.Contain("Reward"));
            foreach (string bandName in new[] { "arcadeHeaderBand", "arcadeResourceBand" })
            {
                var band = Field<RectTransform>(bandName); var surface = band.GetComponent<Graphic>();
                Assert.That(surface, Is.Not.Null); Assert.That(surface.GetType().FullName, Is.EqualTo("JunkToNeon.JtnProductSurface"));
                Assert.That(surface.isActiveAndEnabled, Is.True); Assert.That(surface.raycastTarget, Is.False);
            }
            var headerIcon = Field<RectTransform>("arcadeHeaderBand").Find("Arcade Header Icon");
            Assert.That(headerIcon, Is.Not.Null); var headerGraphic = headerIcon.GetComponent<Graphic>();
            Assert.That(headerGraphic.GetType().FullName, Is.EqualTo("JunkToNeon.JtnProductIcon"));
            Assert.That(headerGraphic.isActiveAndEnabled, Is.True); Assert.That(headerGraphic.raycastTarget, Is.False);
            Assert.That(Field<TMP_Text>("arcadeSectionCaption").text, Is.EqualTo("YARD FINDS"));
            Assert.That(Field<RectTransform>("arcadeItemBand").gameObject.activeSelf, Is.False);
            Assert.That(Field<RectTransform>("arcadeResultBand").gameObject.activeSelf, Is.False);
            Assert.That(Field<RectTransform>("arcadeRoundTrack").gameObject.activeSelf, Is.False);
            Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null); Assert.That(Field<Button>("arcadePrimaryButton").interactable, Is.False); Unchanged(state, files);

            yield return Click(tabs[3]); yield return Click(Field<Button[]>("productCards")[0]);
            StyledCard(Field<Button[]>("expansionCards")[0], "Expansion Icon");
            var strip = Field<RectTransform>("expansionRoot").Find("Expansion Resource Strip");
            Assert.That(strip, Is.Not.Null); var stripSurface = strip.GetComponent<Graphic>();
            Assert.That(stripSurface, Is.Not.Null); Assert.That(stripSurface.GetType().FullName, Is.EqualTo("JunkToNeon.JtnProductSurface"));
            Assert.That(stripSurface.isActiveAndEnabled, Is.True); Assert.That(stripSurface.raycastTarget, Is.False);
            Assert.That((Color32)stripSurface.color, Is.EqualTo(new Color32(230, 225, 207, 255)), "The resource strip is the cream E6E1CF surface with dark text");
            Assert.That(Field<TMP_Text>("expansionResources").text, Does.Contain("Parts: 0").And.Contain("Potatoes: 0"));
            yield return Click(Field<Button[]>("expansionCards")[0]);
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(52)); VisiblePage("expansionRoot", "expansionTitle", "expansionBody");
            Assert.That(Field<TMP_Text>("expansionTitle").text, Is.EqualTo("Salvage Module"));
            Assert.That(Field<TMP_Text>("expansionBody").text, Does.Contain("Cost: 12 parts").And.Contain("Missing: 12 parts"));
            Assert.That(Field<ExpansionQuote>("expansionQuote"), Is.Null);
            Assert.That(Field<Button>("expansionPrimaryButton").gameObject.activeInHierarchy, Is.False); Unchanged(state, files);

            yield return Click(tabs[4]); yield return Click(Field<Button[]>("productCards")[0]);
            StyledCard(Field<Button[]>("arcadeCards")[0], "Arcade Card Icon");
            yield return Click(Field<Button[]>("arcadeCards")[0]);
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(62)); VisiblePage("arcadeRoot", "arcadeTitle", "arcadeBody");
            Assert.That(Field<TMP_Text>("arcadeTitle").text, Is.EqualTo("Slot Level I"));
            Assert.That(Field<TMP_Text>("arcadeBody").text, Does.Contain("Cost: 2 diamonds + 20 parts").And.Contain("Not enough diamonds: 2").And.Contain("Not enough parts: 20"));
            Assert.That(Field<ArcadeQuote>("arcadeQuote"), Is.Null); Assert.That(Field<Button>("arcadePrimaryButton").interactable, Is.False); Unchanged(state, files);

            yield return Click(tabs[4]); yield return Click(Field<Button[]>("productCards")[3]);
            Assert.That(Field<int>("wavePanel"), Is.EqualTo(31));
            var skillCard = Field<Button[]>("navigationCards")[0];
            Assert.That(skillCard.targetGraphic.GetType().FullName, Is.EqualTo("JunkToNeon.JtnProductSurface"));
            Assert.That(skillCard.targetGraphic.isActiveAndEnabled && skillCard.targetGraphic.raycastTarget, Is.True);
            var statusMark = skillCard.transform.Find("Inner Navigation Status"); Assert.That(statusMark, Is.Not.Null);
            var statusGraphic = statusMark.GetComponent<Graphic>(); Assert.That(statusGraphic, Is.Not.Null);
            Assert.That(statusGraphic.isActiveAndEnabled, Is.True); Assert.That(statusGraphic.raycastTarget, Is.False);
            yield return Click(Field<Button[]>("navigationCards")[0]);
            var title = Field<TMP_Text>("panelTitle"); var body = Field<TMP_Text>("panelBody");
            Assert.That(title.isActiveAndEnabled && body.isActiveAndEnabled, Is.True); Fits(title); Fits(body);
            Assert.That(title.text, Is.EqualTo("Skill · Coins 1"));
            Assert.That(body.text, Does.Contain("Price: 120 Coins.").And.Contain("Build first:"));
            Assert.That(Field<Button>("selectButton").gameObject.activeInHierarchy, Is.False);
            var confirmationMark = Field<Button>("selectButton").transform.Find("Inner Confirm Mark");
            Assert.That(confirmationMark, Is.Not.Null); Assert.That(confirmationMark.gameObject.activeInHierarchy, Is.False);
            Assert.That(confirmationMark.GetComponent<Graphic>().raycastTarget, Is.False);
            Assert.That(Field<Button>("closeButton").targetGraphic.GetType().FullName, Is.EqualTo("JunkToNeon.JtnProductSurface"));
            var panel = (RectTransform)Field<GameObject>("panel").transform;
            Assert.That(RectTransformUtility.CalculateRelativeRectTransformBounds(panel, title.transform).Intersects(
                RectTransformUtility.CalculateRelativeRectTransformBounds(panel, body.transform)), Is.False, "Skill title and content overlap");
            Target(Field<Button>("closeButton")); Unchanged(state, files);
        }
        [UnityTest] public IEnumerator AuthoredAndFallbackInnerScreensPreserveReadOnlyContentAndTargets()
        {
            var initial = ModelState.Fresh(1); initial.lastSettledAt = 1000;
            File.WriteAllText(Path.Combine(folder, "jtn.save.v1.json"), codec.Encode(initial));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(ScenePath); Assert.That(scene.isLoaded, Is.True); SceneManager.SetActiveScene(scene);
            game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(gameType, true)).Single();
            yield return null; Assert.That(Session.RecoveryRequired, Is.False, Session.Error);
            Assert.That(Field<bool>("sceneUiAuthored"), Is.True);
            var store = typeof(GameSession).GetField("store", Hidden).GetValue(Session);
            var storage = (FileSnapshotStorage)typeof(GameStore).GetField("storage", Hidden).GetValue(store);
            foreach (var field in typeof(FileSnapshotStorage).GetFields(Hidden).Where(f => f.FieldType == typeof(string)))
                Assert.That(Path.GetDirectoryName(Path.GetFullPath((string)field.GetValue(storage))), Is.EqualTo(folder), "Storage escaped the private fixture");
            string state = State; var files = Files();
            yield return Traverse(state, files);

            UnityEngine.Object.Destroy(game.gameObject); yield return null;
            var replacement = new GameObject("Fallback inner screen verification"); replacement.SetActive(false);
            game = replacement.AddComponent(gameType); replacement.SetActive(true); yield return null;
            Assert.That(Field<bool>("sceneUiAuthored"), Is.False); Unchanged(state, files);
            yield return Traverse(state, files);
        }
        [UnityTearDown] public IEnumerator Restore()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Environment.SetEnvironmentVariable(SaveOverride, previousSave); Environment.SetEnvironmentVariable(ClockOverride, previousClock);
            if (folder == null) yield break;
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            { TestContext.WriteLine("Preserved private fixture: " + folder); yield break; }
            foreach (string name in new[] { "jtn.save.v1.json", "jtn.backup.v1.json", "jtn.pending-write.v1.json", "jtn.interrupted-write.v1.json" })
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) File.Delete(path);
            }
            Directory.Delete(folder, false);
        }
    }
}
#endif
