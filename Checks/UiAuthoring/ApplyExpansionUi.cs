// Parent only: Pipeline script run --entry ExpansionUiAuthoring.Apply.
// Stopped exact-project Editor, existing scene. Does not save; Undo on failure.
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ExpansionUiAuthoring
{
    public static object Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play before expansion authoring.");
        if(!string.Equals(Application.dataPath.Replace('\\','/'),"X:/PROJECTS/FINAL-SVALKA/Unity/Assets",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Wrong project: "+Application.dataPath);
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/JunkToNeon.unity")throw new InvalidOperationException("Select the existing JunkToNeon scene.");
        var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
        var games=components.OfType<JunkToNeon.JunkGame>().ToArray();
        if(games.Length!=1||components.OfType<Canvas>().Count()!=1||components.OfType<UnityEngine.EventSystems.EventSystem>().Count()!=1)
            throw new InvalidOperationException("Expected the one existing game/Canvas/EventSystem.");
        var game=games[0];var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)game.GetType().GetField(name,flags).GetValue(game);
        var sections=Field<UnityEngine.UI.Button[]>("sectionButtons");
        if(sections==null||sections.Length<7||sections.Take(7).Any(b=>b==null)||Field<TMPro.TMP_FontAsset>("font")==null)
            throw new InvalidOperationException("Missing existing Hub/font references. Do not rebuild.");
        var controls=Field<UnityEngine.UI.Button>("spinButton").transform.parent;
        var captions=controls.GetComponentsInChildren<TMPro.TMP_Text>(true).Where(t=>t.name=="НАХОДКИ ДЛЯ ДВОРА"||t.text=="НАХОДКИ ДЛЯ ДВОРА").ToArray();
        if(captions.Length!=1)throw new InvalidOperationException("Inspect the existing Controls caption before authoring.");
        var scaler=components.OfType<Canvas>().Single().GetComponent<UnityEngine.UI.CanvasScaler>();
        if(scaler==null||scaler.uiScaleMode!=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
            throw new InvalidOperationException("Unexpected existing Canvas scaler.");

        // All pre-existing serialized component references, including external font,
        // sprite, script and material assets. Creation may append new references only.
        var references=new Dictionary<int,Dictionary<string,int>>();
        foreach(var component in components)
        {
            var map=new Dictionary<string,int>();var serialized=new SerializedObject(component);var property=serialized.GetIterator();
            while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue!=null)
                map[property.propertyPath]=property.objectReferenceValue.GetInstanceID();
            references[component.GetInstanceID()]=map;
        }
        var oldObjects=new HashSet<int>(components.OfType<Transform>().Select(t=>t.GetInstanceID()));
        var audit=new List<object>();Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("JTN expansion UI");
        Undo.RecordObjects(components.Cast<UnityEngine.Object>().ToArray(),"JTN expansion UI");
        try
        {
            game.CreateExpansionUi();
            foreach(var t in game.GetComponentsInChildren<Transform>(true))
                if(!oldObjects.Contains(t.GetInstanceID())&&t.parent!=null&&oldObjects.Contains(t.parent.GetInstanceID()))
                    Undo.RegisterCreatedObjectUndo(t.gameObject,"JTN expansion UI objects");
            void Rect(RectTransform rect,Vector2 min,Vector2 max)
            {
                if(rect==null||rect.GetComponentInParent<UnityEngine.UI.LayoutGroup>()!=null)throw new InvalidOperationException("Unexpected layout ownership.");
                var before=new {min=new[]{rect.anchorMin.x,rect.anchorMin.y},max=new[]{rect.anchorMax.x,rect.anchorMax.y},offsetMin=new[]{rect.offsetMin.x,rect.offsetMin.y},offsetMax=new[]{rect.offsetMax.x,rect.offsetMax.y}};
                rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;EditorUtility.SetDirty(rect);
                audit.Add(new {target=rect.name,before,after=new {min=new[]{min.x,min.y},max=new[]{max.x,max.y},offsetMin=new[]{0f,0f},offsetMax=new[]{0f,0f}}});
            }
            Rect((RectTransform)Field<UnityEngine.UI.Button>("moreProjectsButton").transform,new Vector2(.06f,.68f),new Vector2(.37f,.81f));
            Rect((RectTransform)Field<UnityEngine.UI.Button>("sectionsButton").transform,new Vector2(.68f,.68f),new Vector2(.94f,.81f));
            Rect((RectTransform)Field<UnityEngine.UI.Button>("expansionShopButton").transform,new Vector2(.40f,.68f),new Vector2(.65f,.81f));
            Rect((RectTransform)sections[6].transform,new Vector2(.03f,.01f),new Vector2(.47f,.24f));
            Rect((RectTransform)Field<UnityEngine.UI.Button>("expansionEntryButton").transform,new Vector2(.53f,.01f),new Vector2(.97f,.24f));
            Rect(captions[0].rectTransform,new Vector2(.04f,.90f),new Vector2(.30f,.99f));
            captions[0].text="НАХОДКИ";EditorUtility.SetDirty(captions[0]);
            // No world/HUD/reel/font/settings reconstruction; all other authored
            // anchors remain. Runtime temporarily adapts the3frames only if unlocked.
            Canvas.ForceUpdateCanvases();
            foreach(var component in components)
            {
                if(component==null)throw new InvalidOperationException("Existing component destroyed.");
                var serialized=new SerializedObject(component);
                foreach(var pair in references[component.GetInstanceID()])
                {
                    var p=serialized.FindProperty(pair.Key);
                    if(p==null||p.objectReferenceValue==null||p.objectReferenceValue.GetInstanceID()!=pair.Value)
                        throw new InvalidOperationException("Existing reference changed: "+component.name+"/"+pair.Key);
                }
            }
            var targets=new[]{Field<UnityEngine.UI.Button>("moreProjectsButton"),Field<UnityEngine.UI.Button>("sectionsButton"),
                Field<UnityEngine.UI.Button>("expansionEntryButton"),Field<UnityEngine.UI.Button>("expansionShopButton"),Field<UnityEngine.UI.Button>("expansionQuickBoostButton"),
                Field<UnityEngine.UI.Button>("expansionPreviousButton"),Field<UnityEngine.UI.Button>("expansionNextButton"),
                Field<UnityEngine.UI.Button>("expansionPrimaryButton"),Field<UnityEngine.UI.Button>("expansionSecondaryButton")}
                .Concat(Field<UnityEngine.UI.Button[]>("expansionCards")).ToArray();
            foreach(var button in targets)
            {
                var r=(RectTransform)button.transform;
                if(r.rect.width<48||r.rect.height<48)throw new InvalidOperationException("Target below48 logical units: "+button.name);
                if(button.GetComponent<JunkToNeon.JtnPanelPointerConsent>()==null)throw new InvalidOperationException("Missing pointer consent: "+button.name);
            }
            var root=Field<RectTransform>("expansionRoot");var cards=Field<UnityEngine.UI.Button[]>("expansionCards");
            for(int i=0;i<cards.Length;i++)for(int j=i+1;j<cards.Length;j++)
                if(RectTransformUtility.CalculateRelativeRectTransformBounds(root,cards[i].transform).Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(root,cards[j].transform)))
                    throw new InvalidOperationException("Expansion cards overlap.");
            EditorUtility.SetDirty(game);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new {passed=true,saved=false,scene=scene.path,preservedComponents=components.Length,preservedReferences=references.Values.Sum(m=>m.Count),audit,
                targets=targets.Select(b=>new {name=b.name,size=new[]{((RectTransform)b.transform).rect.width,((RectTransform)b.transform).rect.height}}).ToArray()});
        }
        catch(Exception error)
        {
            Undo.RevertAllDownToGroup(group);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new {passed=false,saved=false,error=error.ToString(),audit});
        }
    }
}
