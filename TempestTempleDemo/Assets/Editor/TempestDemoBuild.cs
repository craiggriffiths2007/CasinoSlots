using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class TempestDemoBuild
{
    private const string Portfolio = @"C:\repos\craiggriffiths2007\portfolio";
    private const long GitHubFileLimit = 100L * 1024 * 1024;

    [MenuItem("Tools/Tempest Temple Demo/Build Web")]
    public static void BuildOnly() => Build(false);

    [MenuItem("Tools/Tempest Temple Demo/Build Web and Copy to Portfolio")]
    public static void BuildAndCopy() => Build(true);

    private static void Build(bool copyToPortfolio)
    {
        if (copyToPortfolio && !File.Exists(Path.Combine(Portfolio, "casino-demo.html")))
            throw new DirectoryNotFoundException("Could not find casino-demo.html in " + Portfolio);

        TempestProject.Validate();

        string buildFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/TempestTempleDemo"));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/TempestTemple.unity" },
            target = BuildTarget.WebGL,
            locationPathName = buildFolder,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Tempest Temple Web build failed.");

        if (copyToPortfolio)
        {
            string destination = Path.Combine(Portfolio, "games", "tempest-temple");
            CopyToPortfolio(buildFolder, destination);
            Debug.Log("Tempest Temple Demo copied to " + destination);
        }

        Debug.Log("Tempest Temple Demo built at " + buildFolder);
    }

    private static void CopyToPortfolio(string buildFolder, string destination)
    {
        const string entryPoint = "index.html";
        string sourceIndex = Path.Combine(buildFolder, entryPoint);
        if (!File.Exists(sourceIndex))
            throw new FileNotFoundException("The Unity build is missing its entry page.", sourceIndex);

        var files = new DirectoryInfo(buildFolder).GetFiles("*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (file.Length > GitHubFileLimit)
                throw new InvalidOperationException("The build exceeds GitHub's per-file limit: " + file.FullName);
        }

        Directory.CreateDirectory(destination);
        foreach (var file in files)
        {
            string relativePath = Path.GetRelativePath(buildFolder, file.FullName);
            if (relativePath == entryPoint)
                continue;

            string target = Path.Combine(destination, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            file.CopyTo(target, true);
        }
        File.Copy(sourceIndex, Path.Combine(destination, entryPoint), true);

        // Unity hashes its build files. Drop old payloads from the generated folder after updating the entry page.
        string payloadFolder = Path.Combine(destination, "Build");
        foreach (string file in Directory.EnumerateFiles(payloadFolder))
        {
            if (!File.Exists(Path.Combine(buildFolder, "Build", Path.GetFileName(file))))
                File.Delete(file);
        }
    }
}
