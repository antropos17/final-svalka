// Run in the parent's STOPPED exact-project Editor with Pipeline script run,
// entry DirectNavigationAuthoring.Apply. Eval wraps code in a method and cannot
// accept these file-level using directives.
// This script does not save the scene: inspect the returned audit, then parent saves.
// Repeat execution is idempotent. Undo rolls back this group if validation fails.
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class DirectNavigationAuthoring
{
public static object Apply()
{
if(Application.isPlaying)throw new InvalidOperationException("Stop Play before authoring navigation.");
if(!string.Equals(Application.dataPath.Replace('\\','/'),"X:/PROJECTS/FINAL-SVALKA/Unity/Assets",StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Wrong project: "+Application.dataPath);
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.path!="Assets/Scenes/JunkToNeon.unity")throw new InvalidOperationException("Select the existing JunkToNeon scene.");
var games=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<JunkToNeon.JunkGame>(true)).ToArray();
if(games.Length!=1)throw new InvalidOperationException("Expected one existing JunkGame.");
var game=games[0];var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
T Field<T>(string name)=>(T)game.GetType().GetField(name,flags).GetValue(game);
var canvas=game.GetComponentsInChildren<Canvas>(true);
if(canvas.Length!=1||canvas[0].GetComponent<UnityEngine.UI.GraphicRaycaster>()==null)throw new InvalidOperationException("Preserve one existing Canvas/raycaster.");
if(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Count()!=1)
    throw new InvalidOperationException("Expected one existing EventSystem.");
var panel=Field<GameObject>("panel");var menu=Field<UnityEngine.UI.Button>("moreProjectsButton");var sections=Field<UnityEngine.UI.Button>("sectionsButton");
if(panel==null||menu==null||sections==null||Field<RectTransform>("sectionsHubRoot")==null)
    throw new InvalidOperationException("Missing authored baseline references; do not rebuild the Canvas.");
var scaler=canvas[0].GetComponent<UnityEngine.UI.CanvasScaler>();
if(scaler==null||scaler.uiScaleMode!=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
    throw new InvalidOperationException("Unexpected scaler; inspect before changing layout.");

var beforeObjects=game.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.GetInstanceID());
var references=new Dictionary<string,int>();var serialized=new SerializedObject(game);var property=serialized.GetIterator();
while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue!=null)
    references[property.propertyPath]=property.objectReferenceValue.GetInstanceID();
var audit=new List<object>();
Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("JTN direct navigation layout");
Undo.RecordObjects(game.GetComponentsInChildren<Component>(true).Cast<UnityEngine.Object>().Concat(new UnityEngine.Object[]{game}).ToArray(),"JTN direct navigation layout");
try
{
    game.CreateDirectNavigationUi();
    foreach(var t in game.GetComponentsInChildren<Transform>(true))
        if(!beforeObjects.ContainsKey(t.GetInstanceID())&&(t.parent==null||beforeObjects.ContainsKey(t.parent.GetInstanceID())))
            Undo.RegisterCreatedObjectUndo(t.gameObject,"JTN navigation objects");
    void Rect(RectTransform rect,Vector2 min,Vector2 max)
    {
        if(rect==null)throw new InvalidOperationException("Missing layout target.");
        if(rect.GetComponentInParent<UnityEngine.UI.LayoutGroup>()!=null)throw new InvalidOperationException("Inspect LayoutGroup ownership first: "+rect.name);
        var old=new {min=new[]{rect.anchorMin.x,rect.anchorMin.y},max=new[]{rect.anchorMax.x,rect.anchorMax.y},
            offsetMin=new[]{rect.offsetMin.x,rect.offsetMin.y},offsetMax=new[]{rect.offsetMax.x,rect.offsetMax.y}};
        rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
        audit.Add(new {target=rect.name,before=old,after=new {min=new[]{min.x,min.y},max=new[]{max.x,max.y},
            offsetMin=new[]{rect.offsetMin.x,rect.offsetMin.y},offsetMax=new[]{rect.offsetMax.x,rect.offsetMax.y}}});
        EditorUtility.SetDirty(rect);
    }
    // Shared modal gains room for readable three-row grids and existing long previews.
    Rect((RectTransform)panel.transform,new Vector2(.05f,.12f),new Vector2(.95f,.88f));
    Rect(Field<TMPro.TMP_Text>("panelTitle").rectTransform,new Vector2(.07f,.85f),new Vector2(.93f,.98f));
    Rect((RectTransform)Field<UnityEngine.UI.Button>("closeButton").transform,new Vector2(.07f,.03f),new Vector2(.93f,.14f));
    Rect(Field<RectTransform>("sectionsHubRoot"),new Vector2(.07f,.20f),new Vector2(.93f,.70f));
    Rect(Field<TMPro.TMP_Text>("navigationHubContext").rectTransform,new Vector2(.07f,.71f),new Vector2(.93f,.81f));
    Rect(Field<RectTransform>("navigationRoot"),new Vector2(.06f,.17f),new Vector2(.94f,.84f));
    // Existing objects and click handlers stay intact; target heights rise26 ->62.
    Rect((RectTransform)menu.transform,new Vector2(.06f,.68f),new Vector2(.55f,.81f));
    Rect((RectTransform)sections.transform,new Vector2(.58f,.68f),new Vector2(.94f,.81f));
    Rect(Field<TMPro.TMP_Text>("waveStatus").rectTransform,new Vector2(.06f,.82f),new Vector2(.94f,.86f));
    Rect(Field<TMPro.TMP_Text>("heading").rectTransform,new Vector2(.06f,.57f),new Vector2(.94f,.66f));
    Rect((RectTransform)Field<UnityEngine.UI.Button>("guideButton").transform,new Vector2(.71f,.57f),new Vector2(.94f,.67f));
    // Keep the saved-goal readout and progress track unobscured below the heading.
    var readout=Field<RectTransform>("world").Find("Goal Readout") as RectTransform;
    if(readout!=null)Rect(readout,new Vector2(.04f,.48f),new Vector2(.96f,.55f));
    menu.GetComponentInChildren<TMPro.TMP_Text>(true).text="Постройки";
    Field<UnityEngine.UI.Button>("sectionMapClose").GetComponentInChildren<TMPro.TMP_Text>(true).text="К разделам";
    // The modal background deliberately blocks the world and Spin while open.
    var panelImage=panel.GetComponent<UnityEngine.UI.Image>();
    if(panelImage==null)throw new InvalidOperationException("Missing existing modal Image.");
    panelImage.raycastTarget=true;
    Field<RectTransform>("navigationRoot").gameObject.SetActive(false);
    Field<TMPro.TMP_Text>("navigationHubContext").gameObject.SetActive(false);
    Canvas.ForceUpdateCanvases();

    serialized.Update();foreach(var pair in references)
    {
        var current=serialized.FindProperty(pair.Key);
        if(current==null||current.objectReferenceValue==null||current.objectReferenceValue.GetInstanceID()!=pair.Value)
            throw new InvalidOperationException("Existing serialized reference changed: "+pair.Key);
    }
    var targets=new[]{menu,sections,Field<UnityEngine.UI.Button>("closeButton"),Field<UnityEngine.UI.Button>("navigationEraButton")}.Concat(Field<UnityEngine.UI.Button[]>("navigationCards")).ToArray();
    foreach(var button in targets)
    {
        var rect=(RectTransform)button.transform;
        if(rect.rect.width<48||rect.rect.height<48)throw new InvalidOperationException($"Target below48 logical units: {button.name} {rect.rect.size}");
        if(button.GetComponent<JunkToNeon.JtnPanelPointerConsent>()==null)
        {
            // Legacy shared buttons previously received this observer only in Awake.
            // Author the same component through Undo so the stopped hierarchy is complete.
            Undo.AddComponent<JunkToNeon.JtnPanelPointerConsent>(button.gameObject);
        }
    }
    // Pairwise root-space bounds for the six cards, independent of visibility.
    var cards=Field<UnityEngine.UI.Button[]>("navigationCards");var root=Field<RectTransform>("navigationRoot");
    for(int a=0;a<cards.Length;a++)for(int b=a+1;b<cards.Length;b++)
        if(RectTransformUtility.CalculateRelativeRectTransformBounds(root,cards[a].transform).Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(root,cards[b].transform)))
            throw new InvalidOperationException("Navigation cards overlap.");
    EditorUtility.SetDirty(game);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);
    return Newtonsoft.Json.JsonConvert.SerializeObject(new {passed=true,saved=false,scene=scene.path,preservedReferences=references.Count,canvasCount=canvas.Length,audit,
        targets=targets.Select(b=>new {name=b.name,size=new[]{((RectTransform)b.transform).rect.width,((RectTransform)b.transform).rect.height}}).ToArray(),
        next="Inspect portrait TMP/topmost-pointer checks in Play using private saves, then parent saves the scene."});
}
catch(Exception error)
{
    Undo.RevertAllDownToGroup(group);
    return Newtonsoft.Json.JsonConvert.SerializeObject(new {passed=false,saved=false,error=error.ToString(),audit});
}
}
}
