using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Batch-only visual verification. It does not save changes to MainGame.
[InitializeOnLoad]
public static class PhoenixSymbolArtValidation
{
    const string Key = "PhoenixSymbolArtValidation.Active";
    static double next;
    static int stage;
    static PhoenixSymbolArtValidation()
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying) EditorApplication.delayCall += Resume;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                EditorApplication.update -= Tick;
                stage = 0;
                next = EditorApplication.timeSinceStartup + 2;
                EditorApplication.update += Tick;
            }
        };
    }

    static void Resume() { stage = 0; next = EditorApplication.timeSinceStartup + 2; EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity");
        var library = UnityEngine.Object.FindFirstObjectByType<SymbolLibrary>();
        if (library == null) throw new Exception("Missing SymbolLibrary");
        foreach (SymbolType type in Enum.GetValues(typeof(SymbolType)))
            Check(library.GetSprite(type), type.ToString());
        Check(Resources.Load<Sprite>("PhoenixArt/phoenix-double"), "double Phoenix");
        Check(Resources.Load<Sprite>("PhoenixArt/phoenix-triple"), "triple Phoenix");
        Directory.CreateDirectory("Validation/Phoenix2Art");
        File.WriteAllText("Validation/Phoenix2Art/import-check.txt", "PASS: all 14 sprites resolve, contain transparency and readable pixels.\n");
        EditorSettings.enterPlayModeOptionsEnabled = false;
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    static void Check(Sprite sprite, string name)
    {
        if (sprite == null || !sprite.texture.isReadable) throw new Exception(name + " is missing or unreadable");
        bool clear = false, solid = false;
        foreach (var pixel in sprite.texture.GetPixels32())
        {
            clear |= pixel.a == 0;
            solid |= pixel.a > 230;
        }
        if (!clear || !solid) throw new Exception(name + " has invalid transparency");
        Debug.Log("ART VERIFIED: " + name + " " + sprite.rect);
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            var machine = UnityEngine.Object.FindFirstObjectByType<SlotMachine>();
            if (machine == null) throw new Exception("Missing running SlotMachine");
            if (stage > 0) Capture(new[] { "symbols", "double-wild", "triple-wild" }[stage - 1]);
            if (stage == 3)
            {
                SessionState.SetBool(Key, false);
                EditorApplication.update -= Tick;
                Debug.Log("PHOENIX ART VALIDATION PASSED");
                EditorApplication.Exit(0);
                return;
            }
            var board = new SymbolType[3, 5];
            SymbolType[] choices = { SymbolType.Bell, SymbolType.BlueOrb, SymbolType.Cherry,
                SymbolType.Diamond, SymbolType.GreenOrb, SymbolType.Lemon, SymbolType.Phoenix2,
                SymbolType.Plum, SymbolType.RedOrb, SymbolType.Seven, SymbolType.YellowOrb };
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 5; c++)
                    board[r, c] = choices[(r * 4 + c) % choices.Length];
            int span = stage == 0 ? 1 : stage + 1;
            for (int r = 0; r < 3; r++)
                for (int c = 5 - span; c < 5; c++) board[r, c] = SymbolType.Phoenix;
            typeof(SlotMachine).GetMethod("ShowBoard", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(machine, new object[] { board });
            stage++;
            next = EditorApplication.timeSinceStartup + 1;
        }
        catch (Exception e)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static void Capture(string name)
    {
        Camera camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
            if (canvas.isRootCanvas)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
            }
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes("Validation/Phoenix2Art/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = previous;
        RenderTexture.active = active;
        UnityEngine.Object.Destroy(image);
        target.Release();
        UnityEngine.Object.Destroy(target);
    }
}
