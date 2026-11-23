// Run through Pipeline script eval_file against the explicitly selected Editor in Edit Mode.
var root=new UnityEngine.GameObject("Private safe-area regression",typeof(UnityEngine.RectTransform),typeof(UnityEngine.Canvas));
int samples=0;
try{
 var canvas=root.GetComponent<UnityEngine.Canvas>();canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceOverlay;
 var child=new UnityEngine.GameObject("Content",typeof(UnityEngine.RectTransform));child.transform.SetParent(root.transform,false);
 var rect=child.GetComponent<UnityEngine.RectTransform>();rect.anchorMin=UnityEngine.Vector2.zero;rect.anchorMax=UnityEngine.Vector2.one;
 rect.offsetMin=new UnityEngine.Vector2(12,18);rect.offsetMax=new UnityEngine.Vector2(-14,-20);
 var fitter=child.AddComponent<JunkToNeon.Platform.JtnSafeArea>();
 // MonoBehaviour callbacks do not auto-run in Edit Mode without ExecuteAlways.
 fitter.SendMessage("OnEnable");
 var apply=typeof(JunkToNeon.Platform.JtnSafeArea).GetMethod("Apply",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 double[][] cases={new double[]{360,800,0,0,360,800,0,0,1,1},new double[]{360,800,0,0,360,760,0,0,1,.95},new double[]{360,800,0,24,360,776,0,.03,1,1},new double[]{720,1280,0,40,720,1200,0,.03125,1,.96875}};
 foreach(var c in cases){
  if(!JunkToNeon.Platform.SafeAreaGeometry.TryCalculate(c[0],c[1],c[2],c[3],c[4],c[5],out var a))throw new System.Exception("Valid geometry rejected");
  for(int n=0;n<1000;n++){
   apply.Invoke(fitter,new object[]{a});
   if(UnityEngine.Vector2.Distance(rect.anchorMin,new UnityEngine.Vector2((float)c[6],(float)c[7]))>.00001f||UnityEngine.Vector2.Distance(rect.anchorMax,new UnityEngine.Vector2((float)c[8],(float)c[9]))>.00001f)throw new System.Exception("Anchor mismatch");
   if(rect.offsetMin!=new UnityEngine.Vector2(12,18)||rect.offsetMax!=new UnityEngine.Vector2(-14,-20))throw new System.Exception("Padding drift");samples++;
  }
 }
 fitter.enabled=false;fitter.enabled=true;fitter.SendMessage("OnEnable");fitter.SendMessage("OnApplicationPause",false);fitter.SendMessage("OnApplicationFocus",true);
 if(rect.offsetMin!=new UnityEngine.Vector2(12,18)||rect.offsetMax!=new UnityEngine.Vector2(-14,-20))throw new System.Exception("Resume padding drift");
 var g=UnityEngine.Object.FindFirstObjectByType<JunkToNeon.JunkGame>();var safe=g.transform.Find("Portrait Canvas/Safe Area");
 if(safe.GetComponentsInParent<JunkToNeon.Platform.JtnSafeArea>().Length!=1||safe.GetComponentsInChildren<JunkToNeon.Platform.JtnSafeArea>().Length!=1)throw new System.Exception("Duplicate fitter");
 return new {passed=true,samples,rectTransformPadding=true,resume=true,singlePrimaryFitter=true};
}catch(System.Exception error){return new {passed=false,error=error.ToString()};}
finally{UnityEngine.Object.DestroyImmediate(root);}
