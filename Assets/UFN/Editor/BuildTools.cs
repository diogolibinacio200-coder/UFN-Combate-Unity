using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UFN.App;

namespace UFN.Editor
{
    public static class BuildTools
    {
        [MenuItem("UFN/Preparar projeto")]
        public static void Prepare()
        {
            PlayerSettings.companyName="Diogo Liberalesso Inácio";
            PlayerSettings.productName="UFN Combate Unity";
            PlayerSettings.bundleVersion="0.7.0";
            PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=1;settings.ApplyModifiedPropertiesWithoutUndo();}
            Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Assets/Prefabs");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var game=new GameObject("UFN • Aplicativo");game.AddComponent<GameApp>();
            PrefabUtility.SaveAsPrefabAsset(game,"Assets/Prefabs/UFNApplication.prefab");
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/UFN.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/UFN.unity",true)};
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("UFN_PROJECT_PREPARED");
        }
        public static void BuildWindows()
        {
            Prepare();string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string output=Path.Combine(root,"Builds/Windows/UFNCombateUnity.exe");
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--ufn-build-output");if(index>=0&&index+1<args.Length)output=Path.GetFullPath(args[index+1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/UFN.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            Debug.Log("UFN_BUILD_RESULT "+report.summary.result+" errors="+report.summary.totalErrors+" bytes="+report.summary.totalSize);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed");
        }
        static void ConfigureLegacyAxes()
        {
            // Compatibility axes are used only by menu navigation while controls use Input System.
            var assets=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");if(assets.Length==0)return;
            var manager=new SerializedObject(assets[0]);var axes=manager.FindProperty("m_Axes");
            for(int player=1;player<=2;player++)for(int axis=0;axis<2;axis++)
            {
                string name="J"+player+(axis==0?"X":"Y");bool found=false;
                for(int i=0;i<axes.arraySize;i++)if(axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue==name)found=true;
                if(found)continue;int n=axes.arraySize;axes.InsertArrayElementAtIndex(n);var e=axes.GetArrayElementAtIndex(n);
                e.FindPropertyRelative("m_Name").stringValue=name;
                foreach(string f in new[]{"descriptiveName","descriptiveNegativeName","negativeButton","positiveButton","altNegativeButton","altPositiveButton"})e.FindPropertyRelative(f).stringValue="";
                e.FindPropertyRelative("gravity").floatValue=0;e.FindPropertyRelative("dead").floatValue=.19f;e.FindPropertyRelative("sensitivity").floatValue=1;e.FindPropertyRelative("snap").boolValue=false;e.FindPropertyRelative("invert").boolValue=false;e.FindPropertyRelative("type").intValue=2;e.FindPropertyRelative("axis").intValue=axis;e.FindPropertyRelative("joyNum").intValue=player;
            }
            manager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
