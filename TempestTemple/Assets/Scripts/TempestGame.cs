using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TempestTemple
{
    public sealed class TempestGame : MonoBehaviour
    {
        const float Pitch = 112;
        static readonly string[] Names = { "Sapphire", "Emerald", "Ruby", "Amethyst", "Star", "Crystal", "Temple", "Crown", "Sun", "Trident Scatter", "Lightning" };
        readonly TempestEngine engine = new TempestEngine(Environment.TickCount);
        readonly List<Button> betButtons = new List<Button>();
        readonly Image[] cells = new Image[30];
        const int MaxBubbles = 1000;
        sealed class Bubble
        {
            public Image Image;
            public Vector2 Origin, Velocity;
            public float Age, Lifetime, Wobble;
            public bool Trail;
        }
        readonly List<Bubble> bubbles = new List<Bubble>();
        readonly Stack<Bubble> bubblePool = new Stack<Bubble>();
        Sprite[] symbols; Sprite bubbleSprite;
        Font font; RectTransform root, board, effectsLayer;
        Text balanceLabel, betLabel, winLabel, status, multiplierLabel, freeLabel, spinLabel, speedLabel, musicLabel, soundLabel;
        Button spinButton, resetButton, buyButton, confirmBuyButton; GameObject help, buyDialog, bonusDialog;
        Text buyDetails, bonusTitle, bonusDetails;
        bool buyingBonus, awaitingBonus; long confirmedBet;
        TempestAudio audioPlayer;
        TempestCasinoBridge casino;
        Text modeLabel;
        bool accountReady = true, requestFailed;
        long balance = 10000, bet = 20, revealed;
        int remaining, multiplier = 1;
        bool busy, fast;
        string captureDir;

        void Awake()
        {
            Application.targetFrameRate = 60;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var camera = new GameObject("Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Hex("032638");
            audioPlayer = gameObject.AddComponent<TempestAudio>();
            symbols = Enumerable.Range(0, 11).Select(i => Resources.Load<Sprite>("Art/symbol-" + i)).ToArray();
            if (symbols.Any(s => s == null)) throw new InvalidOperationException("Missing Tempest symbol artwork.");
            bubbleSprite = MakeBubble();
            var canvas = new GameObject("Tempest Temple UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1380, 800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = Rect(canvas.transform, "Game frame", 0, 0, 1380, 800);
            new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>().sendNavigationEvents = false;
            BuildUI();
            Draw(Enumerable.Range(0, 30).Select(i => (i * 7 + i / 6) % 11).ToArray());
#if UNITY_WEBGL && !UNITY_EDITOR
            accountReady = false; balance = 0;
            status.text = "Connecting to the casino…";
            resetButton.gameObject.SetActive(false);
            casino = gameObject.AddComponent<TempestCasinoBridge>();
            casino.Initialize((credits, demo) => {
                balance = credits; accountReady = true;
                modeLabel.text = demo ? "PRACTICE CREDITS" : "CASINO CREDITS";
                status.text = "Land 8 matching symbols anywhere to win"; Refresh();
            }, (round, credits) => StartCoroutine(PlayRound(round, credits)), message => {
                requestFailed = true; accountReady = false; busy = false;
                status.text = message + " Reload the page to continue."; Refresh();
            });
#endif
            Refresh();
            string[] args = Environment.GetCommandLineArgs();
            int captureArg = Array.IndexOf(args, "-tempestCapture");
            if (captureArg >= 0 && captureArg + 1 < args.Length)
            {
                captureDir = args[captureArg + 1];
                StartCoroutine(Smoke());
            }
        }
        void BuildUI()
        {
            var backdrop = Box(root, "Underwater background", 0, 0, 1600, 1067, Color.white);
            backdrop.sprite = Resources.Load<Sprite>("Art/Undersea");
            Box(root, "Soft shade", 0, 0, 1600, 1067, new Color(.005f,.04f,.07f,.24f));
            var content = Rect(root, "Compact game content", 0, 70, 1380, 940);
            modeLabel = Label(content, "PLAY MONEY  /  UNITY EDITION", 0, 314, 720, 25, 13, Hex("B8DDE0"));

            board = Rect(content, "Six by five reel window", 0, -3, 6 * Pitch, 5 * Pitch);
            board.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < 30; i++)
            {
                var pos = Position(i);
                cells[i] = Box(board, "Symbol " + i, pos.x, pos.y, Pitch-10, Pitch-10, Color.white);
                cells[i].preserveAspect = true;
                var shadow = cells[i].gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0,.04f,.07f,.6f); shadow.effectDistance = new Vector2(0,-3);
            }
            effectsLayer = Rect(content, "Bubble effects", 0, -3, 672, 560);
            Box(content, "Storm panel", -525, 4, 230, 355, new Color(.005f,.10f,.15f,.55f));
            Label(content, "STORM POWER", -525, 142, 220, 32, 16, Hex("FBE7AD"), true);
            multiplierLabel = Label(content, "×1", -525, 64, 220, 85, 61, Hex("FFF0AE"), true);
            Label(content, "Lightning amplifies\nyour tumble wins", -525, -15, 215, 66, 18, Hex("D8F4F5"));
            Label(content, "MULTIPLIERS  2×–500×", -525, -86, 220, 34, 16, Hex("FFEAB1"));
            Label(content, "Bonus power applies\nwith a new lightning", -525, -135, 215, 55, 15, Hex("9DD9DF"));
            Box(content, "Free spins panel", 525, 4, 230, 355, new Color(.005f,.10f,.15f,.55f));
            Label(content, "FREE SPINS", 525, 142, 220, 32, 16, Hex("FBE7AD"), true);
            freeLabel = Label(content, "0", 525, 64, 220, 85, 61, Hex("FFF0AE"), true);
            Label(content, "4 scatters unlock\n15 free spins", 525, -15, 215, 66, 18, Hex("D8F4F5"));
            Label(content, "3 IN THE BONUS", 525, -86, 220, 34, 16, Hex("FFEAB1"));
            Label(content, "Award 5 extra spins", 525, -135, 215, 55, 15, Hex("9DD9DF"));
            status = Label(content, "Land 8 matching symbols anywhere to win", 0, -305, 1200, 39, 21, Hex("FFF0BD"));
            Box(content, "Control bar", 0, -397, 1300, 119, new Color(.005f,.10f,.15f,.9f));
            Label(content, "BALANCE", -515, -365, 200, 25, 13, Hex("ABD1D6"));
            balanceLabel = Label(content, "", -515, -408, 205, 48, 29, Color.white, true);
            Label(content, "BET", -280, -365, 150, 25, 13, Hex("ABD1D6"));
            betLabel = Label(content, "", -280, -408, 100, 43, 27, Color.white, true);
            betButtons.Add(Button(content, "−", -370, -408, 45, 44, () => ChangeBet(-20)));
            betButtons.Add(Button(content, "+", -190, -408, 45, 44, () => ChangeBet(20)));
            Label(content, "ROUND WIN", -45, -365, 190, 25, 13, Hex("ABD1D6"));
            winLabel = Label(content, "0", -45, -408, 190, 43, 29, Hex("FFF0BD"), true);
            spinButton = Button(content, "SPIN", 525, -397, 224, 78, BeginSpin, true);
            spinLabel = spinButton.GetComponentInChildren<Text>(); spinLabel.fontSize = 29;
            speedLabel = Button(content, "QUICK: OFF", 260, -374, 180, 38, () => { fast = !fast; Refresh(); }).GetComponentInChildren<Text>();
            musicLabel = Button(content, "MUSIC: ON", 200, -420, 110, 34, () => { audioPlayer.ToggleMusic(); Refresh(); }).GetComponentInChildren<Text>();
            soundLabel = Button(content, "SOUND: ON", 320, -420, 110, 34, () => { audioPlayer.ToggleEffects(); Refresh(); }).GetComponentInChildren<Text>();
            Button(content, "HOW TO PLAY", -525, -224, 225, 42, () => help.SetActive(true));
            resetButton = Button(content, "RESET DEMO", 525, -224, 225, 42, () => { if (busy) return; balance = 10000; revealed = 0; remaining = 0; multiplier = 1; status.text = "Practice balance reset to 10,000"; Refresh(); });
            buyButton = Button(content, "BUY BONUS · 100×", 525, -278, 225, 42, ShowBuyBonus, true);
            BuildHelp();
            BuildDialogs();
        }
        void BuildDialogs()
        {
            buyDialog = Box(root, "Buy bonus confirmation", 0, 0, 1380, 940, new Color(0,.025f,.05f,.96f)).gameObject;
            buyDialog.GetComponent<Image>().raycastTarget = true;
            var card = Box(buyDialog.transform, "Purchase card", 0, 0, 810, 490, Hex("0C3947"));
            Label(card.transform, "ENTER THE TEMPLE", 0, 168, 760, 65, 36, Hex("FFF0BA"), true);
            buyDetails = Label(card.transform, "", 0, 30, 730, 190, 25, Color.white);
            Button(card.transform, "CANCEL", -177, -165, 290, 58, () => { buyDialog.SetActive(false); Refresh(); });
            confirmBuyButton = Button(card.transform, "CONFIRM PURCHASE", 177, -165, 320, 58, ConfirmBuyBonus, true);
            buyDialog.SetActive(false);
            bonusDialog = Box(root, "Bonus awarded", 0, 0, 1380, 940, new Color(0,.025f,.05f,.96f)).gameObject;
            bonusDialog.GetComponent<Image>().raycastTarget = true;
            var bonusCard = Box(bonusDialog.transform, "Bonus card", 0, 0, 850, 620, Hex("0C3947"));
            var crest = Box(bonusCard.transform, "Trident scatter", 0, 165, 170, 170, Color.white);
            crest.sprite = symbols[9]; crest.preserveAspect = true;
            bonusTitle = Label(bonusCard.transform, "BONUS UNLOCKED", 0, 38, 800, 70, 40, Hex("FFF0BA"), true);
            bonusDetails = Label(bonusCard.transform, "", 0, -75, 780, 135, 28, Color.white);
            Button(bonusCard.transform, "CONTINUE", 0, -228, 330, 65, () => { awaitingBonus = false; bonusDialog.SetActive(false); }, true);
            bonusDialog.SetActive(false);
        }
        void ShowBuyBonus()
        {
            if (busy || !accountReady || requestFailed) return;
            confirmedBet = bet;
            long cost = checked(confirmedBet * 100);
            buyDetails.text = "15 FREE SPINS\nBet: " + confirmedBet.ToString("N0") + " credits\nCost: " + cost.ToString("N0") + " credits (100× bet)\n" +
                (balance >= cost ? "This amount will be deducted from your balance." : "Not enough credits for this purchase.");
            confirmBuyButton.interactable = balance >= cost;
            buyDialog.SetActive(true); Refresh();
        }
        void ConfirmBuyBonus()
        {
            if (!buyDialog.activeSelf || busy || !accountReady || requestFailed || bet != confirmedBet || balance < confirmedBet * 100) return;
            buyDialog.SetActive(false); buyingBonus = true; busy = true;
            status.text = "Opening the temple bonus…"; Refresh();
#if UNITY_WEBGL && !UNITY_EDITOR
            casino.BuyBonus((int)confirmedBet);
#else
            StartCoroutine(PlayRound(engine.BuyBonus(confirmedBet)));
#endif
        }
        IEnumerator NotifyBonus(int awarded, bool purchased, bool retrigger)
        {
            bonusTitle.text = purchased ? "BONUS PURCHASED" : retrigger ? "BONUS EXTENDED" : "BONUS UNLOCKED";
            bonusDetails.text = (retrigger ? "+" : "") + awarded + " FREE SPINS\n" + remaining + " spins ready to play\nNew lightning collects and activates storm power";
            awaitingBonus = true; bonusDialog.SetActive(true); audioPlayer.Play("bonus");
            if (!string.IsNullOrEmpty(captureDir))
            {
                yield return Capture(purchased ? "tempest-bonus-purchased" : retrigger ? "tempest-bonus-extended" : "tempest-bonus-won");
                awaitingBonus = false; bonusDialog.SetActive(false);
            }
            while (awaitingBonus) yield return null;
        }
        void BuildHelp()
        {
            help = Box(root, "How to play overlay", 0, 0, 1380 / .82f, 800 / .82f, new Color(0,.025f,.05f,.97f)).gameObject;
            help.transform.localScale = Vector3.one * .82f;
            help.GetComponent<Image>().raycastTarget = true;
            Label(help.transform, "HOW TO PLAY", 0, 363, 1100, 58, 38, Hex("FFF0BA"), true);
            Label(help.transform, "8 OR MORE MATCHING SYMBOLS ANYWHERE", 0, 292, 1100, 38, 23, Hex("AFE8E7"), true);
            Label(help.transform, "Winners burst into bubbles. New symbols tumble down and wins repeat.\nPayouts below multiply the bet, for 8–9 / 10–11 / 12+ matching symbols.", 0, 232, 1120, 69, 20, Color.white);
            for (int i = 0; i < 9; i++)
            {
                int col = i % 3, row = i / 3; float x = (col-1)*370, y = 135-row*83;
                var icon = Box(help.transform, Names[i], x-132, y, 58, 58, Color.white); icon.sprite = symbols[i]; icon.preserveAspect = true;
                Label(help.transform, Names[i] + "\n" + (TempestEngine.PayUnits(i,8)/20f).ToString("0.##") + " / " + (TempestEngine.PayUnits(i,10)/20f).ToString("0.##") + " / " + (TempestEngine.PayUnits(i,12)/20f).ToString("0.##") + "×", x+35, y, 265, 67, 19, Hex("FFF0BD"));
            }
            Label(help.transform, "LIGHTNING & FREE SPINS", 0, -122, 1100, 37, 23, Hex("AFE8E7"), true);
            Label(help.transform, "Lightning values range from 2× to 500× and add together after all tumbles.\nIn free spins, winning lightning adds to the bank. The bank applies only with new lightning.\n4 / 5 / 6+ base-game scatters pay 3× / 5× / 100× and award 15 free spins.\n3+ free-spin scatters award 5 extra spins. Special symbols stay during tumbles.", 0, -225, 1190, 156, 19, Color.white);
            Label(help.transform, "Buy bonus: 100× bet, including a four-scatter trigger spin and 15 free spins.\nBet: 20–1,000 credits in steps of 20. Maximum round win: 5,000× bet. Up to 12 winning tumbles per spin.\nVirtual credits. More frequent, smaller wins. Target return 96.5%; not certified RTP.", 0, -351, 1190, 83, 16, Hex("ABD1D6"));
            Button(help.transform, "BACK TO GAME", 0, -424, 300, 48, () => help.SetActive(false), true);
            help.SetActive(false);
        }
        void ChangeBet(int change) { if (!busy) { bet = Math.Max(20, Math.Min(1000, bet + change)); Refresh(); } }
        void Refresh()
        {
            balanceLabel.text = balance.ToString("N0"); betLabel.text = bet.ToString("N0"); winLabel.text = revealed.ToString("N0");
            freeLabel.text = remaining.ToString(); multiplierLabel.text = "×" + multiplier;
            spinButton.interactable = accountReady && !requestFailed && !busy && !buyDialog.activeSelf && !help.activeSelf && !bonusDialog.activeSelf && balance >= bet;
            buyButton.interactable = accountReady && !requestFailed && !busy && !buyDialog.activeSelf;
            spinLabel.text = requestFailed ? "RELOAD" : busy ? "PLAYING…" : "SPIN";
            foreach (var b in betButtons) b.interactable = !busy && !buyDialog.activeSelf;
            resetButton.interactable = !busy && !buyDialog.activeSelf;
            speedLabel.text = fast ? "QUICK: ON" : "QUICK: OFF";
            musicLabel.text = audioPlayer.MusicOn ? "MUSIC: ON" : "MUSIC: OFF";
            soundLabel.text = audioPlayer.EffectsOn ? "SOUND: ON" : "SOUND: OFF";
        }
        // Browser shortcuts call this entry point; native players poll the same keys.
        public void KeyboardSpin(string unused) => BeginSpin();
        void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                BeginSpin();
#endif
        }
        void BeginSpin()
        {
            if (busy || buyDialog.activeSelf || help.activeSelf || bonusDialog.activeSelf || !accountReady || requestFailed || balance < bet) return;
            buyingBonus = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            busy = true; status.text = "Calling the storm…"; Refresh();
            casino.Spin((int)bet);
#else
            var round = engine.Play(bet);
            StartCoroutine(PlayRound(round));
#endif
        }
        IEnumerator PlayRound(TempestRound round, long? serverBalance = null)
        {
            busy = true; revealed = 0; remaining = 0; multiplier = 1;
            balance = serverBalance ?? checked(balance - bet * (buyingBonus ? 100 : 1) + round.TotalWin); Refresh();
            for (int turnIndex = 0; turnIndex < round.Turns.Count; turnIndex++)
            {
                var turn = round.Turns[turnIndex];
                if (turn.FreeSpin) remaining--;
                status.text = turn.FreeSpin ? "Free spin · calling the storm…" : "Calling the storm…"; Refresh();
                yield return ClearBoard();
                for (int f = 0; f < turn.Frames.Count; f++)
                {
                    var frame = turn.Frames[f];
                    TempestFrame previous = f > 0 ? turn.Frames[f-1] : null;
                    if (previous != null) yield return Pop(previous.Winning);
                    yield return Drop(frame.Board, previous);
                    if (frame.Win > 0)
                    {
                        status.text = "Tumble win · " + frame.Win.ToString("N0") + " credits";
                        audioPlayer.Play("win");
                        yield return Highlight(frame.Winning);
                    }
                    else yield return Pause(.28f);
                }
                // Reveal power only when collected from the final board, as in the engine.
                multiplier = turn.FreeSpin ? Math.Max(1, turn.Bank) : turn.Multiplier; revealed += turn.Win; remaining += turn.Awarded; Refresh();
                if (turn.Multiplier > 1 && turn.Win > 0)
                {
                    status.text = "Storm power ×" + turn.Multiplier + " · " + turn.Win.ToString("N0") + " credits";
                    yield return Pause(.7f);
                }
                if (turn.Awarded > 0)
                {
                    status.text = "+" + turn.Awarded + " FREE SPINS · " + remaining + " remaining";
                    yield return NotifyBonus(turn.Awarded, buyingBonus && !turn.FreeSpin, turn.FreeSpin);
                }
            }
            remaining = 0; busy = false; buyingBonus = false;
            status.text = round.Capped ? "MAXIMUM WIN · 5,000× bet!" : revealed > 0 ? "Round complete · " + revealed.ToString("N0") + " credits won" : "The storm settles · try another spin";
            if (balance < bet) status.text += casino != null ? " · lower your bet or return to the casino" : " · lower your bet or reset the demo";
            Refresh();
            if (casino != null) casino.Finished();
        }
        float Speed => fast ? 3.5f : 1f;
        IEnumerator Pause(float duration)
        { float elapsed = 0; while (elapsed < duration) { elapsed += Time.deltaTime * Speed; yield return null; } }
        static Vector2 Position(int i) => new Vector2((i % 6 - 2.5f) * Pitch, (2 - i / 6) * Pitch);
        void Draw(int[] data)
        {
            for (int i = 0; i < 30; i++)
            {
                cells[i].sprite = symbols[data[i]]; cells[i].name = Names[data[i]];
                cells[i].rectTransform.anchoredPosition = Position(i); cells[i].rectTransform.localScale = Vector3.one;
                cells[i].color = Color.white; cells[i].enabled = true;
            }
        }
        IEnumerator ClearBoard()
        {
            audioPlayer.Play("spin"); float elapsed = 0;
            while (elapsed < .62f)
            {
                for (int i = 0; i < 30; i++)
                {
                    float t = Mathf.Clamp01((elapsed - (4-i/6)*.025f - (i%6)*.018f)/.42f);
                    var next = Position(i) + Vector2.down * (700*t*t);
                    BubbleTrail(cells[i].rectTransform.anchoredPosition, next);
                    cells[i].rectTransform.anchoredPosition = next;
                }
                elapsed += Time.deltaTime * Speed; yield return null;
            }
        }
        IEnumerator Drop(int[] data, TempestFrame previous)
        {
            Draw(data); var starts = new Vector2[30];
            for (int col = 0; col < 6; col++)
            {
                var rows = previous == null ? new[] {-5,-4,-3,-2,-1} : TempestEngine.SourceRows(col, previous.Winning);
                for (int row = 0; row < 5; row++)
                {
                    int i = row*6+col; starts[i] = Position(i) + Vector2.up * ((row-rows[row])*Pitch);
                    cells[i].rectTransform.anchoredPosition = starts[i];
                }
            }
            float elapsed = 0;
            while (elapsed < .67f)
            {
                for (int i = 0; i < 30; i++)
                {
                    float t = Mathf.Clamp01((elapsed-(i%6)*.03f)/.5f);
                    float ease = t < .88f ? (t/.88f)*(t/.88f)*1.012f : Mathf.Lerp(1.012f,1,(t-.88f)/.12f);
                    var next = Vector2.LerpUnclamped(starts[i], Position(i), ease);
                    BubbleTrail(cells[i].rectTransform.anchoredPosition, next);
                    cells[i].rectTransform.anchoredPosition = next;
                }
                elapsed += Time.deltaTime * Speed; yield return null;
            }
            for (int i = 0; i < 30; i++) cells[i].rectTransform.anchoredPosition = Position(i);
            audioPlayer.Play("drop");
        }
        IEnumerator Highlight(int[] winning)
        {
            float elapsed = 0;
            float nextBubbles = 0;
            while (elapsed < .72f)
            {
                if (elapsed >= nextBubbles)
                {
                    foreach (int i in winning) EmitBubbles(Position(i), 3, false);
                    nextBubbles += .09f;
                }
                float pulse = Mathf.Abs(Mathf.Sin(elapsed/.72f*Mathf.PI*2));
                foreach (int i in winning)
                { cells[i].rectTransform.localScale = Vector3.one*(1+.09f*pulse); cells[i].color = Color.Lerp(Color.white, Hex("FFF2A4"), pulse*.45f); }
                elapsed += Time.deltaTime * Speed; yield return null;
            }
            foreach (int i in winning) { cells[i].color = Color.white; cells[i].rectTransform.localScale = Vector3.one; }
        }
        IEnumerator Pop(int[] winning)
        {
            audioPlayer.Play("pop"); foreach (int i in winning) EmitBubbles(Position(i), 28, false);
            float elapsed = 0;
            while (elapsed < .32f)
            {
                float t = elapsed/.32f;
                float scale = t < .3f ? Mathf.Lerp(1,1.16f,t/.3f) : Mathf.Lerp(1.16f,0,(t-.3f)/.7f);
                foreach (int i in winning) { cells[i].rectTransform.localScale = Vector3.one*scale; cells[i].color = new Color(1,1,1,1-t*t); }
                elapsed += Time.deltaTime*Speed; yield return null;
            }
            foreach (int i in winning) cells[i].enabled = false;
        }
        void BubbleTrail(Vector2 from, Vector2 to)
        {
            float distance = Vector2.Distance(from, to);
            if (distance < .5f) return;
            // Sample the travelled path so quick mode also leaves a continuous wake.
            int count = Mathf.Clamp(Mathf.CeilToInt(distance / 15f), 1, 8);
            for (int i = 0; i < count; i++)
            {
                var origin = Vector2.Lerp(from, to, (i + .5f) / count);
                // Include a small area beyond both reel edges, without filling the UI.
                if (origin.y < -335 || origin.y > 335) continue;
                EmitBubbles(origin, 1, true);
            }
        }
        void EmitBubbles(Vector2 origin, int count, bool trail)
        {
            for (int i = 0; i < count && bubbles.Count < MaxBubbles; i++)
            {
                Bubble bubble;
                if (bubblePool.Count > 0) bubble = bubblePool.Pop();
                else
                {
                    bubble = new Bubble { Image = Box(effectsLayer, "Bubble", 0, 0, 1, 1, Color.white) };
                    bubble.Image.sprite = bubbleSprite;
                }
                bubble.Age = 0;
                bubble.Trail = trail;
                bubble.Lifetime = trail ? UnityEngine.Random.Range(1.5f,1.9f) : UnityEngine.Random.Range(.8f,1.35f);
                bubble.Origin = origin + new Vector2(UnityEngine.Random.Range(-32f,32f), UnityEngine.Random.Range(-24f,24f));
                bubble.Velocity = new Vector2(UnityEngine.Random.Range(-45f,45f), trail ? UnityEngine.Random.Range(-100f,-55f) : UnityEngine.Random.Range(85f,185f));
                bubble.Wobble = UnityEngine.Random.Range(0f, Mathf.PI*2);
                float size = trail ? UnityEngine.Random.Range(5f,15f) : UnityEngine.Random.Range(8f,26f);
                bubble.Image.rectTransform.sizeDelta = Vector2.one * size;
                bubble.Image.rectTransform.anchoredPosition = bubble.Origin;
                bubble.Image.rectTransform.localScale = Vector3.one * .65f;
                bubble.Image.color = new Color(.75f,1,1,.8f);
                bubble.Image.gameObject.SetActive(true);
                bubbles.Add(bubble);
            }
        }
        void LateUpdate()
        {
            int visibleBubbles = 0;
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var bubble = bubbles[i];
                bubble.Age += Time.deltaTime * Speed;
                float t = bubble.Age / bubble.Lifetime;
                if (t >= 1)
                {
                    bubble.Image.gameObject.SetActive(false);
                    bubblePool.Push(bubble);
                    bubbles.RemoveAt(i);
                    continue;
                }
                // Upward buoyancy reverses a falling trail within .23-.42 seconds.
                float buoyancy = bubble.Trail ? 120f : 70f;
                var offset = bubble.Velocity * bubble.Age + Vector2.up * (buoyancy * bubble.Age * bubble.Age);
                offset.x += Mathf.Sin(bubble.Wobble + bubble.Age * 9f) * 8f - Mathf.Sin(bubble.Wobble) * 8f;
                bubble.Image.rectTransform.anchoredPosition = bubble.Origin + offset;
                float shrink = bubble.Trail ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(.75f,1f,t)) : 0;
                float scale = Mathf.Lerp(.65f,1.15f,Mathf.Min(1,t*4)) * (1-shrink);
                bubble.Image.rectTransform.localScale = Vector3.one * scale;
                bubble.Image.color = new Color(.75f,1,1,.8f * (bubble.Trail ? 1-shrink : 1-t*t));
                if (Mathf.Abs(bubble.Image.rectTransform.anchoredPosition.y) < 400 && bubble.Image.color.a > .08f)
                    visibleBubbles++;
            }
            audioPlayer.UpdateBubbles(visibleBubbles);
        }
        static Sprite MakeBubble()
        {
            var texture = new Texture2D(64,64,TextureFormat.RGBA32,false);
            for (int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                float r = Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/30;
                float ring = Mathf.Clamp01(1-Mathf.Abs(r-.87f)/.085f)*.85f;
                float glint = Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,y),new Vector2(21,43))/7);
                texture.SetPixel(x,y,new Color(.75f,1,1,r>1?0:Mathf.Max(ring,glint)));
            }
            texture.Apply(); return Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f));
        }
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#"+value,out var c); return c; }
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var r = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f); r.anchoredPosition = new Vector2(x,y); r.sizeDelta = new Vector2(w,h); return r;
        }
        Image Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        { var r=Rect(parent,name,x,y,w,h); var image=r.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return image; }
        Text Label(Transform parent,string content,float x,float y,float w,float h,int size,Color color,bool bold=false)
        {
            var r=Rect(parent,content,x,y,w,h); var label=r.gameObject.AddComponent<Text>(); label.font=font; label.text=content;
            label.fontSize=size; label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal; label.color=color; label.alignment=TextAnchor.MiddleCenter;
            label.raycastTarget=false; label.horizontalOverflow=HorizontalWrapMode.Wrap; label.verticalOverflow=VerticalWrapMode.Truncate;
            return label;
        }
        Button Button(Transform parent,string title,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,bool gold=false)
        {
            var image=Box(parent,title,x,y,w,h,gold?Hex("E8CD85"):Hex("164653")); image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image; button.onClick.AddListener(action);
            var colors=button.colors; colors.highlightedColor=new Color(.85f,1,1); colors.pressedColor=new Color(.6f,.8f,.8f); colors.disabledColor=new Color(.45f,.5f,.5f); button.colors=colors;
            Label(image.transform,title,0,0,w-6,h-4,gold?20:14,gold?Hex("142F38"):Hex("E1F6F3"),true);
            return button;
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            // Explicit offscreen rendering also works when the diagnostic window is hidden.
            var canvas=root.GetComponentInParent<Canvas>();
            var camera=Camera.allCameras[0];
            var target=new RenderTexture(1380,800,24,RenderTextureFormat.ARGB32);
            var previousTarget=camera.targetTexture; var previousActive=RenderTexture.active;
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10;
            camera.targetTexture=target; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active=target;
            var texture=new Texture2D(1380,800,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1380,800),0,0); texture.Apply();
            File.WriteAllBytes(Path.Combine(captureDir,name+".png"),texture.EncodeToPNG());
            camera.targetTexture=previousTarget; RenderTexture.active=previousActive;
            canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null;
            target.Release(); Destroy(target); Destroy(texture);
        }
        IEnumerator Smoke()
        {
            Directory.CreateDirectory(captureDir);
            yield return null; yield return Capture("tempest-ready");
            ShowBuyBonus(); yield return Capture("tempest-buy-confirmation"); buyDialog.SetActive(false); Refresh();
            help.SetActive(true); yield return Capture("tempest-rules"); help.SetActive(false);
            fast=true;
            long expected=balance; int turns=0;
            for(int seed=0;seed<12;seed++)
            {
                var round=new TempestEngine(seed).Play(bet); expected=expected-bet+round.TotalWin;
                yield return PlayRound(round); turns+=round.Turns.Count;
                if(balance!=expected || busy || remaining!=0 || revealed!=round.TotalWin) throw new Exception("Runtime round settlement failed");
            }
            // Exercise a real bonus round without changing the production RNG.
            TempestRound bonusRound=null;
            for(int seed=0;seed<100000;seed++) { var candidate=new TempestEngine(seed).Play(bet); if(candidate.Turns.Count>1) { bonusRound=candidate; break; } }
            if(bonusRound==null) throw new Exception("No bonus fixture found");
            expected=balance-bet+bonusRound.TotalWin;
            yield return PlayRound(bonusRound); turns+=bonusRound.Turns.Count;
            if(balance!=expected || busy || remaining!=0) throw new Exception("Bonus settlement failed");
            buyingBonus = true;
            var bought = new TempestEngine(73).BuyBonus(bet); expected = balance - bet * 100 + bought.TotalWin;
            yield return PlayRound(bought);
            if (balance != expected || busy || remaining != 0 || buyingBonus) throw new Exception("Bought bonus settlement failed");
            yield return Capture("tempest-round-complete");
            // Capture a winning frame from an actual evaluated round.
            var winning=bonusRound.Turns.SelectMany(t=>t.Frames).FirstOrDefault(f=>f.Win>0);
            if(winning!=null) { Draw(winning.Board); status.text="Tumble win · "+winning.Win+" credits"; foreach(int i in winning.Winning) cells[i].rectTransform.localScale=Vector3.one*1.08f; yield return Capture("tempest-winning"); }
            File.WriteAllText(Path.Combine(captureDir,"runtime-smoke.txt"),"PASS: 13 regular rounds including a natural bonus, plus a purchased bonus; "+turns+" regular-round turns animated. Balances, free spins, dialogs and controls verified.\n");
            Debug.Log("TEMPEST_RUNTIME_PASS"); Application.Quit();
        }
        void OnDestroy()
        { if(bubbleSprite!=null) { Destroy(bubbleSprite.texture); Destroy(bubbleSprite); } }
    }
}

