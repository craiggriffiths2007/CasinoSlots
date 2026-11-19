using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;
using TempestTemple;

public static class TempestProject
{
    const string Scene = "Assets/Scenes/TempestTemple.unity";
    [MenuItem("Tempest Temple/Create or open game scene")]
    public static void Setup()
    {
        if (File.Exists(Scene)) EditorSceneManager.OpenScene(Scene);
        else
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Tempest Temple").AddComponent<TempestGame>();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), Scene);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
        PlayerSettings.companyName = "Craigs Casino"; PlayerSettings.productName = "Tempest Temple Demo";
        PlayerSettings.defaultScreenWidth = 1380; PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.defaultWebScreenWidth = 1380; PlayerSettings.defaultWebScreenHeight = 940;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.craigscasino.tempesttemple");
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Tempest Temple/Validate game rules")]
    public static void Validate()
    {
        int checks=0;
        Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
        int[,] pays={{2,5,12},{3,7,20},{4,10,35},{4,9,30},{5,10,50},{15,20,120},{20,50,150},{100,250,500},{25,100,250}};
        for(int symbol=0;symbol<9;symbol++)foreach(int count in new[]{7,8,9,10,11,12,30})
        {
            int[] board=Enumerable.Repeat(9,30).ToArray();for(int i=0;i<count;i++)board[i]=symbol;
            var frame=TempestEngine.Evaluate(board,20);
            check(frame.Win==(count<8?0:pays[symbol,count>=12?2:count>=10?1:0]),"Frequent-win paytable boundary");
            check(frame.Winning.Length==(count<8?0:count),"Winning positions");
        }
        foreach(long invalid in new long[]{-10,0,10,19,21,30,1001})
        {bool rejected=false;try{new TempestEngine(0).Play(invalid);}catch(ArgumentOutOfRangeException){rejected=true;}check(rejected,"Invalid bet rejected");}
        for(int mask=0;mask<32;mask++)
        {
            var removed=Enumerable.Range(0,5).Where(r=>(mask&(1<<r))!=0).Select(r=>r*6+2).ToArray();
            var rows=TempestEngine.SourceRows(2,removed);int added=removed.Length;
            check(rows.Length==5 && rows.Distinct().Count()==5,"Every source bound once");
            for(int row=0;row<5;row++)check(row<added?rows[row]==row-added:rows[row]<=row && !removed.Contains(rows[row]*6+2),"Gravity");
        }
        int bank=7;
        check(TempestMath.Engine.ApplyMultiplier(true,50,0,ref bank)==1 && bank==7,"Bank needs new lightning");
        check(TempestMath.Engine.ApplyMultiplier(true,0,500,ref bank)==1 && bank==7,"Dry lightning not collected");
        check(TempestMath.Engine.ApplyMultiplier(true,50,9,ref bank)==16 && bank==16,"Bank accumulation");
        check(TempestMath.Engine.ApplyMultiplier(true,50,500,ref bank)==516,"No old 500x bank cap");
        int bonusRounds=0;
        for(int seed=0;seed<3000;seed++)foreach(bool buy in new[]{false,true})
        {
            long bet=seed%3==0?20:seed%3==1?40:1000;
            var engine=new TempestEngine(seed);var round=buy?engine.BuyBonus(bet):engine.Play(bet);
            check(round.TotalWin==round.Turns.Sum(t=>t.Win) && round.TotalWin<=bet*5000,"Round settlement and cap");
            check(!round.Turns[0].FreeSpin && round.Turns.Skip(1).All(t=>t.FreeSpin),"Trigger and free-spin phases");
            if(round.Turns.Count>1)bonusRounds++;
            if(buy)check(round.Turns[0].Frames.All(f=>f.Board.Count(s=>s==9)==4),"Guaranteed four-scatter purchase");
            int remaining=0;
            foreach(var turn in round.Turns)
            {
                check(turn.Frames.Count<=12,"Cascade limit");
                if(turn.FreeSpin)remaining--;remaining+=turn.Awarded;
                check(remaining>=0,"Free-spin count");
                if(turn.FreeSpin && turn.Frames.Last().Board.All(s=>s!=10))check(turn.Multiplier==1,"No multiplier without new lightning");
            }
            check(remaining==0 || round.Capped,"No unsettled bonus");
        }
        check(bonusRounds>3000,"Natural bonus coverage");
        var capped=new TempestEngine(n=>n-1).Play(20);
        check(!capped.Capped && capped.TotalWin==3000 && capped.Turns[0].Frames.Count==12,"Twelve winning tumbles are paid then stop");
        for(int i=0;i<11;i++)check(Resources.Load<Sprite>("Art/symbol-"+i)!=null,"Symbol import");
        check(Resources.Load<Sprite>("Art/Undersea")!=null,"Backdrop");
        Directory.CreateDirectory("Validation");
        File.WriteAllText("Validation/rules.txt",checks+" assertions passed: Frequent-win paytable, multiplier semantics, bought/natural bonuses, gravity and caps.\n");
        Debug.Log("TEMPEST_RULES_PASS: "+checks);
    }
    [MenuItem("Tempest Temple/Build Windows demo")]
    public static void BuildWindows()
    {
        Setup(); Validate();
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{Scene},locationPathName="Builds/Windows/TempestTemple.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Windows build failed: "+report.summary.result);
        Debug.Log("TEMPEST_WINDOWS_PASS");
    }
    public static void BuildWebGL() => TempestDemoBuild.BuildOnly();
}

// Applies to the normal Build button as well as the Tempest Temple menu.
public sealed class TempestWebCacheSettings : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        AssetDatabase.SaveAssets();
    }
}

public sealed class TempestArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/Art/")) return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.mipmapEnabled=false; importer.alphaIsTransparency=true; importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=assetPath.Contains("/symbol-") ? 512 : 2048; importer.textureCompression=TextureImporterCompression.Uncompressed;
    }
}


