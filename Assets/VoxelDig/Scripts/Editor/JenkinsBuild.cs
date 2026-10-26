using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

public class JenkinsBuild
{
    [MenuItem("MyEditorExtension/JenkinsBuild", false)]
    public static void Build()
    {
        // 現在のビルドターゲットの取得
        var target = EditorUserBuildSettings.activeBuildTarget;
        if (target == BuildTarget.StandaloneWindows) target = BuildTarget.StandaloneWindows64;

        // 登録されているシーンを取得
        List<string> sceneList = new List<string>();
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.enabled)
            {
                sceneList.Add(scene.path);
            }
        }

        // プレイヤー作成
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = sceneList.ToArray();

        // ローカルのROMを吐き出すパスを決定
        string path = "../DevelopBuild/CleanCat.exe";
        buildPlayerOptions.locationPathName = path;
        buildPlayerOptions.options = BuildOptions.Development;

        //引数のPlatform向けを指定
        buildPlayerOptions.target = target;

        // ROM自体のビルド
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
            Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
        else if (summary.result == BuildResult.Failed)
            Debug.Log("Build failed");


        Debug.Log("Build Done");
    }
}