using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JunkToNeon.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath="Assets/Scenes/JunkToNeon.unity";
        [MenuItem("Junk to Neon/Create playable scene")]
        public static string CreateScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before editing the scene.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera));camera.tag="MainCamera";
            camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=new Color32(23,44,41,255);
            new GameObject("Junk to Neon",typeof(JunkGame));
            Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.companyName="JunkToNeonPrototype";PlayerSettings.productName="Свалка — Неон";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.junktoneon.unityprototype");
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth=390;PlayerSettings.defaultScreenHeight=844;
            PlayerSettings.resizableWindow=true;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion=(AndroidSdkVersions)36;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            var importer=AssetImporter.GetAtPath("Assets/Resources/house-stages.png") as TextureImporter;
            if(importer!=null){importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();}
            AssetDatabase.SaveAssets();
            return ScenePath;
        }
        public static string DockPhonePreview()
        {
            // Public EditorWindow dock preference: place a NEW live Game View in the Inspector's right-hand dock.
            var gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView",true);
            var inspector=typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow",true);
            var create=typeof(EditorWindow).GetMethods().First(m=>m.Name=="CreateWindow"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(Type[]));
            var view=(EditorWindow)create.MakeGenericMethod(gameView).Invoke(null,new object[]{new[]{inspector}});
            view.titleContent=new GUIContent("Phone · 390 × 844");
            view.Show();view.Focus();
            var size=gameView.GetProperty("targetSize",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            return "Live Game View requested in Inspector dock; actual position="+view.position+" target="+size?.GetValue(view);
        }
        public static string MakeCurrentSceneEditable()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before authoring.");
            var game=UnityEngine.Object.FindFirstObjectByType<JunkGame>();
            if(game==null)throw new InvalidOperationException("Open JunkToNeon scene first.");
            const string folder="Assets/Resources/Editable";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(folder+"/PrototypeFont.asset");
            if(font==null)
            {
                font=TMPro.TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("LiberationSans"));
                font.name="PrototypeFont";AssetDatabase.CreateAsset(font,folder+"/PrototypeFont.asset");
                AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
                font.TryAddCharacters("СВАЛКА НЕОН КАРТОШКА КОРОБКА ХЛАМ ГОЛУБЬ МОНЕТЫ ремонт Начать проект Закрыть КРУТИТЬ АВТО");
            }
            var texture=Resources.Load<Texture2D>("house-stages");var stages=new Sprite[3];int[] starts={0,652,1248},widths={652,596,694};
            for(int i=0;i<3;i++)
            {
                string path=folder+"/HouseStage"+i+".asset";stages[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(stages[i]==null){stages[i]=Sprite.Create(texture,new Rect(starts[i],0,widths[i],809),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);stages[i].name="HouseStage"+i;AssetDatabase.CreateAsset(stages[i],path);}
            }
            game.CreateEditableUi(font,stages);EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(game.gameObject.scene);
            var browser=typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser",true);
            var inspector=typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow",true);
            var create=typeof(EditorWindow).GetMethods().First(m=>m.Name=="CreateWindow"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(Type[]));
            ((EditorWindow)create.MakeGenericMethod(inspector).Invoke(null,new object[]{new[]{browser}})).Show();
            Selection.activeGameObject=game.transform.Find("Portrait Canvas/Safe Area/World 60%/House draft").gameObject;
            var sceneView=SceneView.lastActiveSceneView;if(sceneView!=null){sceneView.in2DMode=true;sceneView.FrameSelected();}
            Tools.current=Tool.Rect;
            return "Saved editable Canvas, house and controls in the scene; Inspector beside bottom Project dock.";
        }
        public static string BuildAndroid()
        {
            return BuildAndroidPlayer("JunkToNeon-debug.apk",BuildOptions.Development,"android-build.txt");
        }

        [MenuItem("Junk to Neon/Build compact Android APK")]
        public static string BuildAndroidCompact()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before build.");
            var target=UnityEditor.Build.NamedBuildTarget.Android;
            var codeGeneration=PlayerSettings.GetIl2CppCodeGeneration(target);
            var stripping=PlayerSettings.GetManagedStrippingLevel(target);
            bool minify=PlayerSettings.Android.minifyRelease;
            var settings=Resources.FindObjectsOfTypeAll<PlayerSettings>().Single();
            // Preserve absent platform overrides as well as their effective values.
            string settingsSnapshot=EditorJsonUtility.ToJson(settings);
            try
            {
                PlayerSettings.SetIl2CppCodeGeneration(target,UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
                PlayerSettings.SetManagedStrippingLevel(target,ManagedStrippingLevel.Low);
                PlayerSettings.Android.minifyRelease=true;
                return BuildAndroidPlayer("JunkToNeon.apk",BuildOptions.CompressWithLz4HC,"android-compact-build.txt");
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(settingsSnapshot,settings);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                if(PlayerSettings.GetIl2CppCodeGeneration(target)!=codeGeneration||PlayerSettings.GetManagedStrippingLevel(target)!=stripping||PlayerSettings.Android.minifyRelease!=minify)
                    throw new InvalidOperationException("Compact build settings were not restored.");
            }
        }

        static string BuildAndroidPlayer(string filename,BuildOptions options,string receiptName)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before build.");
            string destination=Path.GetFullPath(Path.Combine("Builds/Android",filename));Directory.CreateDirectory(Path.GetDirectoryName(destination));
            Environment.SetEnvironmentVariable("GRADLE_USER_HOME",Path.GetFullPath("../.work/gradle"));
            EditorUserBuildSettings.buildAppBundle=false;EditorUserBuildSettings.androidBuildSystem=AndroidBuildSystem.Gradle;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=destination,target=BuildTarget.Android,options=options});
            string receipt="result="+report.summary.result+"\nbytes="+report.summary.totalSize+"\nerrors="+report.summary.totalErrors+"\npath="+destination+"\ndurationSeconds="+report.summary.totalTime.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Directory.CreateDirectory("../docs/evidence/unity");File.WriteAllText("../docs/evidence/unity/"+receiptName,receipt);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception(receipt);
            return receipt;
        }
    }
}
