#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the minimal runtime entry scene when needed and builds the Android APK
/// for the GitHub Actions / GameCI remote build.
/// </summary>
public static class MyCityAndroidBuild
{
    private const string ScenePath = "Assets/Scenes/MyCity_Main.unity";
    private const string OutputPath = "build/Android/MyCity.apk";

    public static void PerformBuild()
    {
        EnsureMainScene();

        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.productName = "My City";
        PlayerSettings.applicationIdentifier = "com.zhyrinak1992.mycity";
        PlayerSettings.bundleVersion = "2.167";
        PlayerSettings.Android.targetArchitectures =
            AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new System.Exception("My City Android build stopped: no enabled scenes.");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception(
                "My City Android build failed: " + report.summary.result +
                "; errors: " + report.summary.totalErrors);

        Debug.Log("My City Android APK created: " + OutputPath +
                  "; size: " + report.summary.totalSize + " bytes");
    }

    private static void EnsureMainScene()
    {
        Directory.CreateDirectory("Assets/Scenes");

        if (!File.Exists(ScenePath))
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MyCity_Main";

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.Exception(
                    "Could not create " + ScenePath);
        }

        var enabledScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled && s.path != ScenePath)
            .ToList();

        enabledScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = enabledScenes.ToArray();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
