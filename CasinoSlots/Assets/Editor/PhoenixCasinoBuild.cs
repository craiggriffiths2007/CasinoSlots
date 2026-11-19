using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PhoenixCasinoBuild
{
    [MenuItem("Tools/Firebird/Build and copy to Casino")]
    public static void BuildAndCopy()
    {
        Build(true);
    }

    [MenuItem("Tools/Firebird/Build only")]
    public static void BuildOnly()
    {
        Build(false);
    }

    private static void Build(bool copyToCasino)
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string staging = Path.Combine(project, "Builds", "PhoenixSlot2Credits");
        string destination = @"C:\repos\craiggriffiths2007\Casino\Casino\wwwroot\games\phoenix-slot-2";
        if (copyToCasino && !Directory.Exists(Path.Combine(destination, "..", "..")))
            throw new DirectoryNotFoundException("Casino wwwroot folder was not found: " + destination);

        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/Web.asset");
        if (profile == null) throw new InvalidOperationException("Web build profile was not found.");
        BuildProfile.SetActiveBuildProfile(profile);
        PlayerSettings.WebGL.template = "PROJECT:Casino";
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 700;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        // Changed assets get new URLs so cached builds cannot mix incompatible files.
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.productName = "Firebird";
        AssetDatabase.SaveAssets();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
        {
            buildProfile = profile,
            locationPathName = staging,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Firebird Web build failed; the hosted build was not replaced.");

        if (!copyToCasino)
        {
            Debug.Log("Firebird built successfully at " + staging);
            return;
        }

        // Only publish files after the complete build succeeds. Keep old files for rollback.
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(staging.Length + 1);
            if (relative == "index.html") continue;
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
        }
        File.Copy(Path.Combine(staging, "index.html"), Path.Combine(destination, "index.html"), true);
        Debug.Log("Firebird with casino credits built and copied successfully. Restart the casino and reload /Game/PhoenixSlot2.");
    }
}
