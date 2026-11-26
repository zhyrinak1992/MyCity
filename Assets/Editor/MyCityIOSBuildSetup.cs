#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Build.Reporting;

public static class MyCityIOSBuildSetup
{
    private const string ScenePath = "Assets/Scenes/MyCity_Main.unity";
    private const string BundleId = "com.mycity.simulator";

    [MenuItem("My City/iOS/1 - Prepare iPhone project")]
    public static void PrepareProject()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Build/iOS");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "MyCity_Main";
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.companyName = "My City Studio";
        PlayerSettings.productName = "My City";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.runInBackground = false;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.stripEngineCode = true;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("My City: базовая сцена и настройки iOS подготовлены. Следующий шаг — Build Profiles > iOS или облачная сборка.");
        EditorUtility.DisplayDialog("My City — iOS", "Подготовка завершена. Создана стартовая сцена и выставлены базовые настройки iPhone.\n\nЭто ещё не IPA: для IPA нужен Xcode на Mac или облачная macOS-сборка.", "Понятно");
    }

    [MenuItem("My City/iOS/2 - Export Xcode project")]
    public static void ExportXcodeProject()
    {
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("My City — iOS", "Сначала запусти: My City > iOS > 1 - Prepare iPhone project", "OK");
            return;
        }
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
        {
            EditorUtility.DisplayDialog("My City — iOS", "В Unity Hub для этой версии Unity не установлен модуль iOS Build Support. Установи его через Installs > версия Unity > Add modules.", "OK");
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/iOS/MyCity_Xcode",
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
            EditorUtility.DisplayDialog("My City — iOS", "Xcode-проект успешно экспортирован в Build/iOS/MyCity_Xcode. Для получения IPA нужен Xcode на macOS или облачная macOS-сборка.", "OK");
        else
            EditorUtility.DisplayDialog("My City — iOS", "Экспорт не завершён. Подробности смотри в Console и Editor.log. Результат: " + report.summary.result, "OK");
    }
}
#endif
