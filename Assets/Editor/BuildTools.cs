using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Configuração e build por linha de comando:
///   Unity -batchmode -quit -projectPath . -executeMethod BuildTools.Setup
///   Unity -batchmode -quit -projectPath . -executeMethod BuildTools.BuildWindows
public static class BuildTools
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Boneco Gore/Configurar projeto")]
    public static void Setup()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Main").AddComponent<MainScene>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.companyName = "Andraus";
        PlayerSettings.productName = "Boneco Gore Sandbox";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.usePlayerLog = true;
        AssetDatabase.SaveAssets();
        Debug.Log("BuildTools: projeto configurado");
    }

    [MenuItem("Boneco Gore/Gerar executável Windows")]
    public static void BuildWindows()
    {
        if (!File.Exists(ScenePath)) Setup();
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/BonecoGore.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("BuildTools: build " + report.summary.result + " erros=" + report.summary.totalErrors + " tamanho=" + report.summary.totalSize);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
