// Root only: stopped exact-project Pipeline script run --entry ArcadeUiAuthoring.Apply.
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ArcadeUiAuthoring
{
    public static object Apply()
    {
        if(Application.isPlaying||!string.Equals(Application.dataPath.Replace('\\','/'),"X:/PROJECTS/FINAL-SVALKA/Unity/Assets",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use the stopped exact-project Editor.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/JunkToNeon.unity")throw new InvalidOperationException("Open the existing game scene.");
        var before=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
        var game=before.OfType<JunkToNeon.JunkGame>().Single();
        if(before.OfType<Canvas>().Count()!=1||before.OfType<UnityEngine.EventSystems.EventSystem>().Count()!=1||game.Session!=null)
            throw new InvalidOperationException("Expected one existing Canvas/EventSystem and no Session.");
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)game.GetType().GetField(name,flags).GetValue(game);
        var sections=Field<UnityEngine.UI.Button[]>("sectionButtons");
        if(sections.Length!=7||sections.Any(b=>b==null)||Field<UnityEngine.UI.Button>("expansionEntryButton")==null)
            throw new InvalidOperationException("Preserve the existing seven routes and shop.");
        var references=new Dictionary<int,Dictionary<string,int>>();
        foreach(var c in before)
        {
            var map=new Dictionary<string,int>();var p=new SerializedObject(c).GetIterator();
            while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue!=null)
                map[p.propertyPath]=p.objectReferenceValue.GetInstanceID();
            references[c.GetInstanceID()]=map;
        }
        var oldObjects=new HashSet<int>(before.OfType<Transform>().Select(t=>t.GetInstanceID()));
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("JTN Arcade integration");
        Undo.RecordObjects(before.Cast<UnityEngine.Object>().ToArray(),"JTN Arcade existing Canvas");
        try
        {
            game.CreateArcadeIntegrationUi();
            foreach(var t in game.GetComponentsInChildren<Transform>(true))
                if(!oldObjects.Contains(t.GetInstanceID())&&t.parent!=null&&oldObjects.Contains(t.parent.GetInstanceID()))
                    Undo.RegisterCreatedObjectUndo(t.gameObject,"JTN Arcade controls");
            var cards=Field<UnityEngine.UI.Button[]>("arcadeCards");
            for(int i=0;i<cards.Length;i++)
            {
                float left=i%2*.52f,top=.78f-i/2*.195f;var r=(RectTransform)cards[i].transform;
                r.anchorMin=new Vector2(left,top-.175f);r.anchorMax=new Vector2(left+.48f,top);r.offsetMin=r.offsetMax=Vector2.zero;
            }
            Canvas.ForceUpdateCanvases();
            int kept=0;
            foreach(var c in before)
            {
                if(c==null)throw new InvalidOperationException("Existing component destroyed.");var so=new SerializedObject(c);
                foreach(var pair in references[c.GetInstanceID()])
                {
                    var p=so.FindProperty(pair.Key);
                    if(p==null||p.objectReferenceValue==null||p.objectReferenceValue.GetInstanceID()!=pair.Value)
                        throw new InvalidOperationException("Existing reference changed: "+c.name+"/"+pair.Key);
                    kept++;
                }
            }
            var hub=sections.Concat(new[]{Field<UnityEngine.UI.Button>("expansionEntryButton"),Field<UnityEngine.UI.Button>("arcadeEntryButton")}).ToArray();
            var targets=hub.Concat(cards).Concat(new[]{Field<UnityEngine.UI.Button>("arcadePrimaryButton"),Field<UnityEngine.UI.Button>("arcadeSecondaryButton")}).ToArray();
            foreach(var b in targets)
            {
                var r=(RectTransform)b.transform;
                if(r.rect.width<48||r.rect.height<48||b.GetComponent<JunkToNeon.JtnPanelPointerConsent>()==null)
                    throw new InvalidOperationException("Small or unguarded Arcade target: "+b.name+" "+r.rect.size);
            }
            var hubRoot=Field<RectTransform>("sectionsHubRoot");var bounds=new List<Rect>();
            foreach(var b in hub)
            {
                var r=(RectTransform)b.transform;var min=hubRoot.InverseTransformPoint(r.TransformPoint(r.rect.min));var max=hubRoot.InverseTransformPoint(r.TransformPoint(r.rect.max));
                var box=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
                if(bounds.Any(old=>old.Overlaps(box)))throw new InvalidOperationException("Overlapping Hub target: "+b.name);
                bounds.Add(box);
            }
            var flyers=Field<TMPro.TMP_Text[]>("rewardFeedbackLabels");
            if(flyers.Length!=8||flyers.Any(t=>t==null||t.raycastTarget))throw new InvalidOperationException("Feedback pool must be bounded and non-intercepting.");
            var cg=Field<RectTransform>("rewardFeedbackRoot").GetComponent<CanvasGroup>();
            if(cg==null||cg.blocksRaycasts||cg.interactable||game.Session!=null)throw new InvalidOperationException("Invalid store-free feedback authoring.");
            foreach(var c in game.GetComponentsInChildren<Component>(true))if(c!=null)EditorUtility.SetDirty(c);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,beforeComponents=before.Length,afterComponents=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Count(c=>c!=null),gameComponents=game.GetComponentsInChildren<Component>(true).Length,retainedReferences=kept,hubTargets=hub.Length,guardedTargets=targets.Length,feedbackLabels=flyers.Length,sessionAbsent=game.Session==null});
        }
        catch{Undo.RevertAllDownToGroup(group);throw;}
    }
}
