using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using CandyCascade;

public static class CandyProject
{
    [MenuItem("Candy Cascade/Create or open game scene")]
    public static void Setup()
    {
        const string scene = "Assets/Scenes/CandyGarden.unity";
        if (File.Exists(scene)) EditorSceneManager.OpenScene(scene);
        else
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Candy Cascade").AddComponent<CandyGame>();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scene);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene, true) };
        PlayerSettings.companyName = "Candy Garden Studio";
        PlayerSettings.productName = "Candy Cascade";
        PlayerSettings.defaultScreenWidth = 1240; PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Candy Cascade/Validate game rules")]
    public static void Validate()
    {
        int checks = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); checks++; };
        check(CandyCreditDisplay.Format(3069.77m) == "306,977", "Game balance matches casino credits");
        check(CandyCreditDisplay.Format(100m) == "10,000", "Demo starting balance uses casino credits");
        check(CandyCreditDisplay.Format(1m) == "100" && CandyCreditDisplay.Format(.2m) == "20", "Displayed bets match wallet debits");
        check(CandyCreditDisplay.Format(12.34m) == "1,234" && CandyCreditDisplay.Format(0m) == "0", "Win display uses casino credits");
        var candySprites = CandySymbols.Load();
        check(candySprites.Length == 7 && candySprites[0].texture.width == 1774, "All seven sweets preserve atlas proportions");
        foreach (var sprite in candySprites)
            check(sprite.rect.width > 0 && sprite.rect.height > 0 && sprite.rect.xMax <= sprite.texture.width && sprite.rect.yMax <= sprite.texture.height, "Sweet sprite fits the atlas");
        foreach (string sound in new[] { "spin", "landing", "cascade", "win", "bonus", "click" })
        {
            var clip = Resources.Load<AudioClip>("Audio/" + sound);
            check(clip != null && clip.length > .05f && clip.channels == 2, "Stereo effect available: " + sound);
        }
        var game = new CandyEngine(12345);
        Action clear = () => { for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++) { game.Board[x, y] = (x + y) % 2; game.Hits[x, y] = 0; } };
        clear(); check(game.FindWins(1).Count == 0, "Diagonal adjacency must not win");
        for (int x = 0; x < 5; x++) game.Board[x, 0] = 5;
        var wins = game.FindWins(2); check(wins.Count == 1 && wins[0].Cells.Count == 5, "Five connected symbols win");
        check(wins[0].Payout == .8m, "Bet-scaled payout");
        game.Hits[0, 0] = 2; game.Hits[1, 0] = 3;
        wins = game.FindWins(2); check(wins[0].Payout == 4.8m, "Multipliers add within cluster");
        game.Remove(wins); check(game.Board[0, 0] == -1 && game.Hits[0, 0] == 3, "Remove updates cell history");
        game.Collapse(); foreach (int s in game.Board) check(s >= 0 && s <= 6, "Collapse must refill every cell");
        check(game.Hits[0, 0] == 3, "Multipliers stay at positions");
        clear();
        for (int x = 0; x < 5; x++) { game.Board[x, 0] = 5; game.Board[x, 6] = 3; }
        wins = game.FindWins(1);
        check(wins.Count == 2, "Bonus regression covers multiple clusters");
        game.Remove(wins);
        foreach (var win in wins) foreach (int p in win.Cells)
            check(game.Multiplier(p % 7, p / 7) == 2, "Every first bonus win creates 2x");
        game.Collapse(); game.Fill(false);
        foreach (var win in wins) foreach (int p in win.Cells)
        {
            check(game.Multiplier(p % 7, p / 7) == 2, "Bonus multipliers persist at every winning position");
            game.Board[p % 7, p / 7] = win.Symbol;
        }
        game.Remove(wins);
        foreach (var win in wins) foreach (int p in win.Cells)
            check(game.Multiplier(p % 7, p / 7) == 4, "Every repeat bonus win doubles to 4x");
        check(game.Multiplier(6, 3) == 0, "Non-winning positions stay unchanged");
        game.Hits[0, 0] = 8; game.Remove(wins);
        check(game.Multiplier(0, 0) == 128, "Repeat bonus win respects cap");
        clear(); for (int x = 0; x < 5; x++) game.Board[x, 0] = 5;
        game.Remove(game.FindWins(1));
        for (int x = 0; x < 5; x++) check(game.Multiplier(x, 0) == 2, "Every first base-game win creates 2x");
        clear(); game.Board[0, 0] = 4; game.Board[0, 1] = -1; game.Board[0, 2] = 2; game.Board[0, 3] = -1;
        game.Board[0, 4] = 3; game.Board[0, 5] = 1; game.Board[0, 6] = 0;
        game.Collapse(); check(game.Board[0, 2] == 4 && game.Board[0, 3] == 2 && game.Board[0, 4] == 3 && game.Board[0, 6] == 0, "Gravity preserves symbol ordering");
        // Check animation plans against the pre-collapse board, including empty and
        // full columns, scattered holes, stationary candies, and off-grid spawns.
        for (int pattern = 0; pattern < 8; pattern++)
        {
            clear();
            for (int x = 0; x < 7; x++) for (int y = 0; y < 7; y++)
                if ((x + y * 3) % 8 < pattern) game.Board[x, y] = -1;
            var before = (int[,])game.Board.Clone();
            var plan = game.Collapse(); var destinations = new bool[7, 7]; var sources = new bool[7, 7];
            check(plan.Count == 49, "Every destination has one animation binding");
            foreach (var fall in plan)
            {
                check(!destinations[fall.Column, fall.ToRow], "No duplicate destinations"); destinations[fall.Column, fall.ToRow] = true;
                check(fall.Symbol == game.Board[fall.Column, fall.ToRow], "View and model agree");
                if (fall.IsNew) check(fall.FromRow < 0, "New symbols originate above the grid");
                else
                {
                    check(!sources[fall.Column, fall.FromRow], "Surviving image used once"); sources[fall.Column, fall.FromRow] = true;
                    check(before[fall.Column, fall.FromRow] == fall.Symbol && fall.ToRow >= fall.FromRow, "Survivor identity and downward motion");
                    int holesBelow = 0;
                    for (int y = fall.FromRow + 1; y < 7; y++) if (before[fall.Column, y] < 0) holesBelow++;
                    check(fall.ToRow - fall.FromRow == holesBelow, "Fall distance equals removed cells below");
                }
            }
            for (int x = 0; x < 7; x++) for (int y = 0; y < 7; y++)
                check(sources[x, y] == (before[x, y] >= 0), "No survivor lost or empty cell reused as survivor");
        }
        for (int x = 0; x < 7; x++) for (int y = 0; y < 7; y++) game.Board[x, y] = -1;
        var emptyFalls = game.Collapse();
        foreach (var fall in emptyFalls) check(fall.IsNew && fall.FromRow == fall.ToRow - 7, "Empty grid enters as seven-symbol columns");
        game.Hits[0, 0] = 100; check(game.Multiplier(0, 0) == 128, "Multiplier cap");
        game.Fill(false); check(game.Hits[0, 0] == 100, "Bonus carries hit history");
        game.Fill(true); check(game.Hits[0, 0] == 0, "Paid spin resets hit history");
        clear(); for (int x = 0; x < 7; x++) game.Board[x, 0] = 6;
        check(game.FindWins(1).Count == 0 && game.ScatterCount() == 7, "Scatters never form a cluster");
        check(CandyEngine.FreeSpinAward(2, false) == 0 && CandyEngine.FreeSpinAward(3, false) == 10 && CandyEngine.FreeSpinAward(4, false) == 12 && CandyEngine.FreeSpinAward(5, true) == 5, "Bonus awards");
        check(CandyEngine.BasePay(0, 4) == 0 && CandyEngine.BasePay(5, 15) == 10, "Paytable boundaries");
        var a = new CandyEngine(98); var b = new CandyEngine(98); a.Fill(true); b.Fill(true);
        for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++) check(a.Board[x, y] == b.Board[x, y], "Seed repeatability");
        // Resolve many complete spins to catch incomplete refill and runaway cascade regressions.
        int cascades = 0;
        for (int spin = 0; spin < 1000; spin++)
        {
            a.Fill(true); int depth = 0;
            while (true) { var w = a.FindWins(1); if (w.Count == 0) break; a.Remove(w); a.Collapse(); depth++; if (depth >= 100) throw new Exception("Runaway cascade"); }
            cascades += depth;
        }
        check(cascades > 0, "Simulation exercises wins");
        Directory.CreateDirectory("Validation");
        File.WriteAllText("Validation/rules.txt", checks + " assertions passed; 1000 seeded spins resolved; " + cascades + " cascades.\n");
        Debug.Log("CANDY_RULES_PASS: " + checks);
    }
    public static void Build()
    {
        CandyWebBuild.Build();
    }

    [MenuItem("Candy Cascade/Build Windows demo" )]
    public static void BuildWindows()
    {
        Setup(); Validate();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/CandyGarden.unity" },
            locationPathName = "Builds/Windows/CandyCascade.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
        Debug.Log("CANDY_BUILD_PASS");
    }
}
