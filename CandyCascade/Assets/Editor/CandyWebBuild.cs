using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class CandyWebBuild
{
    [MenuItem("Candy Cascade/Build Web casino")]
    public static void Build()
    {
        CandyProject.Setup();
        CandyProject.Validate();
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.template = "PROJECT:Casino";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/CandyGarden.unity" },
            locationPathName = "Builds/WebBuildCandy", target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Candy Web build failed: " + report.summary.result);
        UnityEngine.Debug.Log("CANDY_WEB_BUILD_PASS");
    }
}
