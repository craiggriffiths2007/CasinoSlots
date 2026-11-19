using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CandyCascade
{
    [Serializable]
    sealed class ServerSpinResult
    {
        public string requestId;
        public int[] initialBoard;
        public int[] initialHits;
        public ServerCascade[] cascades;
        public long win;
        public long baseWin;
        public long balance;
        public int freeSpinsAwarded;
        public bool isBonusPurchase;
        public long cost;
        public ServerSpinResult[] spins;
        public int remaining;
    }

    [Serializable]
    sealed class ServerCascade
    {
        public int[] boardBefore;
        public int[] boardAfter;
        public int[] hitsBefore;
        public int[] hitsAfter;
        public long win;
        public long baseWin;
        public ServerWin[] wins;
    }

    [Serializable]
    sealed class ServerWin
    {
        public int symbol;
        public int[] cells;
        public long win;
        public long baseWin;
    }

    public sealed class CandyGame : MonoBehaviour
    {
        CasinoWallet casinoWallet;
        readonly CandyEngine engine = new CandyEngine(Environment.TickCount);
        Image[,] candies = new Image[7, 7];
        readonly Image[,] multiplierTiles = new Image[7, 7];
        readonly Text[,] badges = new Text[7, 7];
        readonly List<Button> lockedButtons = new List<Button>();
        readonly decimal[] bets = { .2m, .5m, 1m, 2m, 5m, 10m };
        readonly Color[] colors = { Hex("FF769F"), Hex("77DCCE"), Hex("BBA0FF"), Hex("FFB96C"), Hex("70C7FF"), Hex("FFF088"), Hex("FFF3CE") };
        Sprite round, disc; Sprite[] symbols;
        Font font; RectTransform root, board; Text balanceText, betText, winText, statusText, bonusText, spinText, autoText, soundText;
        GameObject help; CandyAudio candyAudio;
        GameObject buyConfirmation;
        GameObject freeSpinsNotification;
        Text freeSpinsNotificationText;
        Text buyPriceText, buyConfirmationText;
        Button confirmBuyButton;
        decimal pendingBonusBet;
        decimal balance, spinWin, bonusWin; int betIndex = 2, freeSpins;
        bool busy, autoPlay, muted, fast; Button spinButton;
        const float Cell = 90;

        void Awake()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            // Match the compact layout even if an older, wider window size was saved.
            int windowHeight = Mathf.Min(800, Mathf.RoundToInt(Display.main.systemHeight * .85f));
            Screen.SetResolution(Mathf.RoundToInt(windowHeight * 1240f / 800f), windowHeight, FullScreenMode.Windowed);
#endif
            Application.targetFrameRate = 60;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            round = MakeShape(-2); disc = MakeShape(-1);
            symbols = CandySymbols.Load();
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.gameObject.AddComponent<AudioListener>();
            candyAudio = gameObject.AddComponent<CandyAudio>();
            var canvas = new GameObject("Candy Cascade UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1240, 800);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            // Keep the compact game frame fitted inside wide or tall windows.
            root = new GameObject("Game frame", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            root.sizeDelta = new Vector2(1240, 800);
            new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>().sendNavigationEvents = false;
            BuildUI();
            engine.Fill(true);
            SyncBoard();
            Refresh();

            casinoWallet = gameObject.AddComponent<CasinoWallet>();
            StartCoroutine(casinoWallet.LoadAccount(OnCasinoBalanceLoaded));

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-candyCapture") >= 0)
                StartCoroutine(Capture());
        }

        void OnCasinoBalanceLoaded(decimal casinoBalance)
        {
            balance = casinoBalance;
            Refresh();

            Debug.Log($"Candy Casino balance: {balance:N2}");
        }

        void BuildUI()
        {
            SweetFactoryBackdrop.Create(root);
            Label(root, "PLAY MONEY  /  DEMO", -470, 300, 290, 30, 15, Hex("B8DFCF"), TextAnchor.MiddleLeft);
            ButtonAt(root, "HOW TO PLAY", 435, 300, 200, 40, () => help.SetActive(true), Hex("403451"));

            // An invisible viewport clips entering/exiting candies at the grid edges.
            var viewport = new GameObject("Symbol viewport", typeof(RectTransform), typeof(RectMask2D));
            board = viewport.GetComponent<RectTransform>(); board.SetParent(root, false);
            board.anchoredPosition = new Vector2(0, -27); board.sizeDelta = new Vector2(7 * Cell, 7 * Cell);
            // Create all tiles first so they stay above the backdrop but below
            // every candy and badge, including symbols moving during a tumble.
            for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++)
            {
                Vector2 p = CellPosition(x, y);
                var tile = Box(board, "Multiplier tile", p.x, p.y, Cell - 4, Cell - 4, Color.clear);
                tile.sprite = null; tile.type = Image.Type.Simple;
                tile.raycastTarget = false; tile.enabled = false;
                multiplierTiles[x, y] = tile;
            }
            for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++)
            {
                Vector2 p = CellPosition(x, y);
                candies[x, y] = Box(board, "Candy", p.x, p.y, 84, 84, Color.white);
                candies[x, y].type = Image.Type.Simple; candies[x, y].preserveAspect = true;
                candies[x, y].raycastTarget = false;
            }
            for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++)
            {
                Vector2 p = CellPosition(x, y);
                badges[x, y] = Label(board, "", p.x + 20, p.y - 31, 50, 26, 18, Hex("FFF4A1"), TextAnchor.MiddleCenter, true);
            }

            Label(root, "SWEET\nCONNECTIONS", -470, 205, 180, 70, 22, Hex("FFF0D8"), TextAnchor.MiddleCenter, true);
            Label(root, "5+", -470, 123, 175, 66, 51, Hex("FF97B8"), TextAnchor.MiddleCenter, true);
            Label(root, "matching candies\nconnected side by side\nmake a winning cluster", -470, 49, 180, 95, 17, Hex("CCBED7"));
            Label(root, "POP. DROP. REPEAT.", -470, -63, 190, 40, 16, Hex("89E5D2"), TextAnchor.MiddleCenter, true);
            Label(root, "Winning candies pop.\nNew sweets tumble in.\nThe chain keeps going.", -470, -131, 180, 98, 17, Hex("CCBED7"));
            Label(root, "MULTIPLIERS UP TO", -470, -232, 190, 30, 13, Hex("C4A3DA"));
            Label(root, "128×", -470, -274, 180, 48, 36, Hex("FFE89B"), TextAnchor.MiddleCenter, true);

            Label(root, "THIS SPIN", 470, 245, 260, 35, 16, Hex("D7B7D8"));
            winText = Label(root, "0.00", 470, 175, 260, 84, 62, Hex("FFF0B8"), TextAnchor.MiddleCenter, true);
            statusText = Label(root, "Ready for something sweet?", 470, 87, 260, 82, 20, Color.white);
            bonusText = Label(root, "THE SUGAR GARDEN", 470, -52, 260, 43, 22, Hex("A8F0D1"), TextAnchor.MiddleCenter, true);
            Label(root, "3+ golden stars award free spins.\nMultiplier cells stay throughout the bonus.", 470, -120, 260, 85, 17, Hex("CCDDD7"));
            ButtonAt(root, "BUY FREE SPINS", 470, -237, 260, 58, ShowBuyConfirmation, Hex("56406D"), true);
            buyPriceText = Label(root, "", 470, -285, 260, 25, 16, Hex("FFE7B3"));

            Label(root, "BALANCE", -495, -374, 210, 27, 13, Hex("AF9BBC"), TextAnchor.MiddleLeft);
            balanceText = Label(root, "", -495, -410, 210, 42, 30, Color.white, TextAnchor.MiddleLeft, true);
            Label(root, "BET / SPIN", -265, -374, 160, 27, 13, Hex("AF9BBC"));
            betText = Label(root, "", -265, -410, 130, 42, 29, Color.white, TextAnchor.MiddleCenter, true);
            ButtonAt(root, "−", -350, -411, 46, 46, () => ChangeBet(-1), Hex("3D3150"), true);
            ButtonAt(root, "+", -180, -411, 46, 46, () => ChangeBet(1), Hex("3D3150"), true);
            var auto = ButtonAt(root, "AUTO: OFF", -45, -402, 145, 60, () => { autoPlay = !autoPlay; Refresh(); if (autoPlay && !busy) BeginSpin(); }, Hex("3D3150")); autoText = auto.GetComponentInChildren<Text>();
            ButtonAt(root, "SPEED", 115, -402, 130, 60, () => { fast = !fast; statusText.text = fast ? "Quick tumbles enabled" : "Relaxed tumbles enabled"; }, Hex("3D3150"));
            spinButton = ButtonAt(root, "SPIN  ›", 480, -402, 220, 76, BeginSpin, Hex("F28FB3")); spinText = spinButton.GetComponentInChildren<Text>(); spinText.fontSize = 30;
            var sound = ButtonAt(root, "SOUND ON", 275, -402, 140, 60, () => { muted = !muted; candyAudio.Muted = muted; Refresh(); }, Hex("3D3150")); soundText = sound.GetComponentInChildren<Text>();
            // Raise the play area after removing the title; the board is centered at x = 0.
            foreach (RectTransform child in root)
            {
                if (child.name == "Sweet factory background") continue;
                child.anchoredPosition += Vector2.up * 65;
            }
            BuildHelp();
            BuildBuyConfirmation();
            BuildFreeSpinsNotification();
        }

        void BuildHelp()
        {
            help = Box(root, "Rules overlay", 0, 0, 2400, 1600, Color.black).gameObject;
            var panel = Box(help.transform, "Rules", 0, 0, 990, 790, Color.clear);
            Label(panel.transform, "A guide to the good stuff", 0, 325, 850, 70, 40, Hex("FFE7B3"), TextAnchor.MiddleCenter, true);
            Label(panel.transform,
                "CLUSTERS & TUMBLES\nConnect 5 or more identical candies horizontally or vertically. Diagonals do not count. Winning candies disappear; new candies drop in until no cluster wins remain.\n\n" +
                "MULTIPLIER CELLS\nIn both base-game and free spins, the first win at a position creates 2× for future wins. Each repeat win doubles it to 4×, 8× ... up to 128×. Existing multipliers in a winning cluster are added and applied to that cluster. Cells reset each paid spin and persist across free spins.\n\n" +
                "GOLDEN STARS\nStars do not form clusters. At the end of a spin, 3 stars award 10 free spins, plus 2 for each extra star. During free spins, 3+ stars add 5 spins. Free spins use the triggering bet.\n\n" +
                "PAYOUTS (MULTIPLES OF BET)\n5 candies: pink 0.12 / mint 0.16 / violet 0.20 / orange 0.25 / yellow 0.40. Multiply by 2 for 6–7, 4 for 8–9, 7 for 10–11, 12 for 12–14, or 25 for 15+.\n\n" +
                "Demo credits only. Balance resets on restart. Each spin is capped at 5,000× bet or 100 tumbles. This original prototype has no certified RTP or real-money integration.",
                0, -5, 855, 550, 21, Hex("E6DCEA"), TextAnchor.UpperLeft);
            ButtonAt(panel.transform, "LET’S PLAY", 0, -325, 300, 60, () => { help.SetActive(false); if (!busy && (freeSpins > 0 || autoPlay)) BeginSpin(); }, Hex("AEDECC"));
            help.SetActive(false);
        }
        void ChangeBet(int delta) { if (busy || freeSpins > 0) return; betIndex = Mathf.Clamp(betIndex + delta, 0, bets.Length - 1); Refresh(); }
        void BuildBuyConfirmation()
        {
            var overlay = Box(root, "Buy free spins overlay", 0, 0, 2400, 1600, new Color(0, 0, 0, .8f));
            overlay.sprite = null;
            buyConfirmation = overlay.gameObject;
            var panel = Box(overlay.transform, "Buy free spins panel", 0, 0, 640, 310, Hex("302039"));
            buyConfirmationText = Label(panel.transform, "", 0, 55, 570, 120, 28, Hex("FFE7B3"), TextAnchor.MiddleCenter, true);
            ButtonAt(panel.transform, "CANCEL", -145, -90, 230, 60, () => buyConfirmation.SetActive(false), Hex("E6DCEA"));
            confirmBuyButton = ButtonAt(panel.transform, "BUY", 145, -90, 230, 60, ConfirmBuyBonus, Hex("F28FB3"));
            buyConfirmation.SetActive(false);
        }
        void BuildFreeSpinsNotification()
        {
            var overlay = Box(root, "Free spins won overlay", 0, 0, 2400, 1600, new Color(0, 0, 0, .8f));
            overlay.sprite = null;
            freeSpinsNotification = overlay.gameObject;
            var panel = Box(overlay.transform, "Free spins won panel", 0, 0, 640, 310, Hex("302039"));
            freeSpinsNotificationText = Label(panel.transform, "", 0, 55, 570, 120, 28, Hex("FFE7B3"), TextAnchor.MiddleCenter, true);
            ButtonAt(panel.transform, "OK", 0, -90, 230, 60, () => freeSpinsNotification.SetActive(false), Hex("F28FB3"));
            freeSpinsNotification.SetActive(false);
        }
        IEnumerator NotifyFreeSpinsWon(int awarded)
        {
            freeSpinsNotificationText.text = "You won " + awarded + " free spins!";
            freeSpinsNotification.SetActive(true);
            // Diagnostic previews acknowledge the notification automatically.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-candyCapture") >= 0)
            {
                yield return new WaitForSeconds(.35f);
                freeSpinsNotification.SetActive(false);
            }
            // Keep the spin busy until acknowledged, including when autoplay is on.
            while (freeSpinsNotification.activeSelf) yield return null;
        }
        void ShowBuyConfirmation()
        {
            if (busy || freeSpins > 0 || casinoWallet == null || !casinoWallet.IsLoaded || casinoWallet.SpinPending) return;
            autoPlay = false;
            pendingBonusBet = bets[betIndex];
            decimal cost = pendingBonusBet * 100m;
            buyConfirmationText.text = "Buy 10 free spins for " + CandyCreditDisplay.Format(cost) + " credits?";
            confirmBuyButton.interactable = balance >= cost;
            if (balance < cost) buyConfirmationText.text += "\nNot enough credits.";
            buyConfirmation.SetActive(true);
            Refresh();
        }
        void ConfirmBuyBonus()
        {
            if (!buyConfirmation.activeSelf || busy || freeSpins > 0 || casinoWallet == null || !casinoWallet.IsLoaded || casinoWallet.SpinPending) return;
            decimal cost = pendingBonusBet * 100m;
            if (pendingBonusBet != bets[betIndex] || balance < cost) { ShowBuyConfirmation(); return; }
            buyConfirmation.SetActive(false);
#if UNITY_WEBGL && !UNITY_EDITOR
            busy = true;
            statusText.text = "Buying 10 free spins…";
            if (string.IsNullOrEmpty(casinoWallet.BuyFreeSpins(pendingBonusBet)))
            {
                busy = false;
                statusText.text = "Retry the purchase at the original bet to resolve the pending request.";
            }
            Refresh();
            return;
#else
            balance -= cost;
            Array.Clear(engine.Hits, 0, engine.Hits.Length); freeSpins = 10; bonusWin = 0; BeginSpin();
#endif
        }
        public void KeyboardSpin(string unused)
        {
            if (autoPlay || freeSpins > 0 || balance < bets[betIndex]) return;
            BeginSpin();
        }
        void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-candyCapture") >= 0) return;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                KeyboardSpin("");
#endif
        }
        void BeginSpin()
        {
            if (busy ||
                help.activeSelf ||
                buyConfirmation.activeSelf ||
                freeSpinsNotification.activeSelf ||
                casinoWallet == null ||
                !casinoWallet.IsLoaded ||
                casinoWallet.SpinPending)
                return;

#if !UNITY_WEBGL || UNITY_EDITOR
            // Editor/desktop previews have no MVC host; use the local practice engine.
            StartCoroutine(Spin());
            return;
#else
            // Demo/free-spin mode can stay local for the moment.
            if (freeSpins > 0)
            {
                StartCoroutine(Spin());
                return;
            }

            decimal bet = bets[betIndex];

            if (balance < bet)
            {
                autoPlay = false;

                statusText.text =
                    "Not enough credits. Lower the bet.";

                Refresh();
                return;
            }

            busy = true;
            spinWin = 0;
            bonusWin = 0;

            statusText.text =
                "A fresh handful of happiness";

            Refresh();
            candyAudio.Spin();

            string requestId =
                casinoWallet.BeginSpin(bet);

            if (string.IsNullOrEmpty(requestId))
            {
                busy = false;

                statusText.text =
                    "Unable to start spin.";

                Refresh();
            }
#endif
        }
        public void OnCasinoSpinLoaded(string json)
        {
            try
            {
                var result =
                    UnityEngine.JsonUtility.FromJson<ServerSpinResult>(json);

                if (result == null ||
                    result.initialBoard == null ||
                    result.initialBoard.Length != 49)
                {
                    throw new Exception(
                        "Invalid Candy Cascade spin response.");
                }

                StartCoroutine(result.isBonusPurchase ? AnimateServerBonus(result) : result.freeSpinsAwarded > 0 ? AnimateServerAward(result) : AnimateServerSpin(result));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);

                busy = false;
                autoPlay = false;

                statusText.text =
                    "Unable to read spin result.";

                Refresh();
            }
        }
        public void OnCasinoSpinFailed(string error)
        {
            Debug.LogError(
                "Candy server spin failed: " + error);

            busy = false;
            autoPlay = false;

            statusText.text =
                "Spin failed. Please try again.";

            Refresh();
        }
        void SetServerState(
            int[] boardData,
            int[] hitsData = null)
        {
            if (boardData == null ||
                boardData.Length != 49)
                throw new Exception(
                    "Invalid server board.");

            for (int y = 0; y < 7; y++)
            {
                for (int x = 0; x < 7; x++)
                {
                    int p = y * 7 + x;

                    engine.Board[x, y] =
                        boardData[p];

                    engine.Hits[x, y] =
                        hitsData != null &&
                        hitsData.Length == 49
                            ? hitsData[p]
                            : 0;
                }
            }
        }
        IEnumerator AnimateServerAward(ServerSpinResult receipt)
        {
            if (receipt.spins == null || receipt.spins.Length < receipt.freeSpinsAwarded)
                throw new Exception("Missing awarded free-spin receipt.");
            busy = true;
            long totalWin = receipt.win;
            long bonusTotal = totalWin - receipt.baseWin;
            var baseSpin = UnityEngine.JsonUtility.FromJson<ServerSpinResult>(UnityEngine.JsonUtility.ToJson(receipt));
            baseSpin.win = receipt.baseWin;
            baseSpin.balance = receipt.balance - bonusTotal;
            yield return AnimateServerSpin(baseSpin, true);
            yield return NotifyFreeSpinsWon(receipt.freeSpinsAwarded);
            receipt.win = bonusTotal;
            yield return AnimateServerBonus(receipt);
            receipt.win = totalWin;
        }
        IEnumerator AnimateServerBonus(ServerSpinResult receipt)
        {
            if (receipt.spins == null || receipt.spins.Length < 10)
                throw new Exception("Invalid bonus purchase receipt.");
            busy = true;
            bonusWin = 0;
            balance = (receipt.balance - receipt.win) / 100m;
            candyAudio.Bonus();
            foreach (var spin in receipt.spins)
            {
                freeSpins = spin.remaining;
                bonusWin += spin.win / 100m;
                spin.balance = decimal.ToInt64((balance + spin.win / 100m) * 100m);
                yield return AnimateServerSpin(spin, true);
            }
            balance = receipt.balance / 100m;
            freeSpins = 0;
            spinWin = receipt.win / 100m;
            statusText.text = "Garden complete\nTotal won " + CandyCreditDisplay.Format(spinWin);
            busy = false;
            Refresh();
        }
        IEnumerator AnimateServerSpin(
    ServerSpinResult result, bool bonusSequence = false)
        {
            decimal bet =
                bets[betIndex];

            yield return AnimateExit();


            // ---------------------------------------------
            // Initial board supplied by the server
            // ---------------------------------------------

            SetServerState(
                result.initialBoard,
                result.initialHits ?? (result.cascades != null && result.cascades.Length > 0 ? result.cascades[0].hitsBefore : null));

            RefreshBadges();


            var initialFalls =
                new List<CandyEngine.Fall>(49);


            for (int y = 0; y < 7; y++)
            {
                for (int x = 0; x < 7; x++)
                {
                    initialFalls.Add(
                        new CandyEngine.Fall(
                            x,
                            y - 7,
                            y,
                            engine.Board[x, y]));
                }
            }


            yield return AnimateFalls(
                initialFalls,
                true);


            // ---------------------------------------------
            // Cascades supplied by server
            // ---------------------------------------------

            int tumble = 0;

            if (result.cascades != null)
            {
                foreach (var cascade in result.cascades)
                {
                    tumble++;

                    SetServerState(
                        cascade.boardBefore,
                        cascade.hitsBefore);

                    RefreshBadges();
                    SyncBoard();


                    var visualWins =
                        new List<CandyEngine.Cluster>();


                    if (cascade.wins != null)
                    {
                        foreach (var serverWin in cascade.wins)
                        {
                            var cluster =
                                new CandyEngine.Cluster
                                {
                                    Symbol =
                                        serverWin.symbol,

                                    Payout =
                                        serverWin.win / 100m
                                };


                            if (serverWin.cells != null)
                            {
                                cluster.Cells.AddRange(
                                    serverWin.cells);
                            }


                            visualWins.Add(cluster);
                        }
                    }


                    decimal amount =
                        cascade.win / 100m;


                    spinWin += amount;


                    statusText.text =
                        "SWEET!  +" +
                        CandyCreditDisplay.Format(amount) +
                        "\nCascade " +
                        tumble;


                    Refresh();

                    candyAudio.Cascade(tumble);


                    yield return AnimatePop(
                        visualWins);


                    yield return new WaitForSeconds(
                        fast ? .04f : .10f);


                    // -------------------------------------
                    // Animate from boardBefore to boardAfter
                    // -------------------------------------

                    SetServerState(
                        cascade.boardBefore,
                        cascade.hitsAfter);
                    RefreshBadges();

                    foreach (var win in visualWins)
                    {
                        foreach (int p in win.Cells)
                        {
                            engine.Board[
                                p % 7,
                                p / 7] = -1;
                        }
                    }


                    var falls =
                         BuildServerFalls(
                             cascade.boardBefore,
                             cascade.boardAfter,
                             visualWins);


                    SetServerState(
                        cascade.boardAfter,
                        cascade.hitsAfter);


                    yield return AnimateFalls(
                        falls,
                        false);
                }
            }


            // ---------------------------------------------
            // Server is authoritative for money
            // ---------------------------------------------

            balance =
                result.balance / 100m;

            spinWin =
                result.win / 100m;


            if (result.freeSpinsAwarded > 0)
            {
                candyAudio.Bonus();
                statusText.text =
                    result.freeSpinsAwarded +
                    " FREE SPINS AWARDED";
            }
            else if (spinWin == 0)
            {
                statusText.text =
                    "A new mix awaits";
            }
            else
            {
                candyAudio.Win(spinWin / bet);
                statusText.text =
                    "Delicious! " +
                    CandyCreditDisplay.Format(spinWin) +
                    " won";
            }


            Refresh();





            if (bonusSequence) yield break;
            if (result.freeSpinsAwarded > 0)
                yield return NotifyFreeSpinsWon(result.freeSpinsAwarded);
            busy = false;

            Refresh();


            if (autoPlay)
                BeginSpin();
        }
        List<CandyEngine.Fall> BuildServerFalls(
    int[] before,
    int[] after,
    List<CandyEngine.Cluster> wins)
        {
            var removed =
                new HashSet<int>();


            foreach (var win in wins)
            {
                foreach (int p in win.Cells)
                    removed.Add(p);
            }


            var falls =
                new List<CandyEngine.Fall>();


            for (int x = 0; x < 7; x++)
            {
                var survivors =
                    new List<int>();


                for (int y = 6; y >= 0; y--)
                {
                    int p =
                        y * 7 + x;


                    if (!removed.Contains(p) &&
                        before[p] >= 0)
                    {
                        survivors.Add(y);
                    }
                }


                int destination =
                    6;


                foreach (int fromY in survivors)
                {
                    falls.Add(
                        new CandyEngine.Fall(
                            x,
                            fromY,
                            destination,
                            before[fromY * 7 + x]));

                    destination--;
                }


                int newNumber = 0;


                while (destination >= 0)
                {
                    int symbol =
                        after[destination * 7 + x];


                    falls.Add(
                        new CandyEngine.Fall(
                            x,
                            -1 - newNumber,
                            destination,
                            symbol));


                    newNumber++;
                    destination--;
                }
            }


            return falls;
        }
        IEnumerator Spin()
        {
            bool bonus = freeSpins > 0; decimal bet = bets[betIndex];
            if (!bonus && balance < bet) { autoPlay = false; statusText.text = "Not enough demo credits. Lower the bet or restart."; Refresh(); yield break; }
            busy = true; spinWin = 0;
            if (bonus) freeSpins--; else { balance -= bet; bonusWin = 0; }
            statusText.text = bonus ? "Welcome to the sugar garden" : "A fresh handful of happiness";
            Refresh(); candyAudio.Spin();
            yield return AnimateExit();
            engine.Fill(!bonus); RefreshBadges();
            var initialFalls = new List<CandyEngine.Fall>(49);
            for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++)
                initialFalls.Add(new CandyEngine.Fall(x, y - 7, y, engine.Board[x, y]));
            yield return AnimateFalls(initialFalls, true);
            int tumble = 0;
            while (tumble < 100)
            {
                var wins = engine.FindWins(bet); if (wins.Count == 0) break;
                tumble++; decimal amount = 0;
                foreach (var win in wins) amount += win.Payout;
                amount = Math.Min(amount, bet * 5000m - spinWin);
                spinWin += amount; balance += amount; if (bonus) bonusWin += amount;
                statusText.text = "SWEET!  +" + CandyCreditDisplay.Format(amount) + "\nCascade " + tumble;
                Refresh(); candyAudio.Cascade(tumble);
                yield return AnimatePop(wins);
                engine.Remove(wins); RefreshBadges();
                yield return new WaitForSeconds(fast ? .04f : .10f);
                yield return AnimateFalls(engine.Collapse(), false);
                if (spinWin >= bet * 5000m) { statusText.text = "Maximum spin win reached!"; break; }
            }
            int award = CandyEngine.FreeSpinAward(engine.ScatterCount(), bonus);
            if (award > 0)
            {
                if (!bonus) bonusWin = 0;
                freeSpins += award; statusText.text = "+" + award + " FREE SPINS!\nThe garden is blooming"; candyAudio.Bonus();
            }
            else if (bonus && freeSpins == 0) statusText.text = "Garden complete\nTotal won " + CandyCreditDisplay.Format(bonusWin);
            else if (spinWin == 0) statusText.text = "A new mix awaits";
            else statusText.text = "Delicious! " + CandyCreditDisplay.Format(spinWin) + " won";
            if (award == 0 && spinWin > 0) candyAudio.Win(spinWin / bet);
            Refresh();
            if (!bonus && award > 0) yield return NotifyFreeSpinsWon(award);
            busy = false; Refresh();
            if (freeSpins > 0 || autoPlay) BeginSpin();
        }
        static Vector2 CellPosition(int column, int row) => new Vector2((column - 3) * Cell, (3 - row) * Cell);

        IEnumerator AnimateExit()
        {
            // Clear the old symbols before changing the model. Each column follows
            // the previous one out through the bottom of the invisible viewport.
            float duration = fast ? .19f : .34f, stagger = fast ? .012f : .025f;
            float elapsed = 0;
            while (elapsed < duration + 6 * stagger)
            {
                elapsed += Time.deltaTime;
                for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++)
                {
                    float t = Mathf.Clamp01((elapsed - x * stagger) / duration);
                    var icon = candies[x, y];
                    icon.rectTransform.anchoredPosition = CellPosition(x, y) + Vector2.down * (8 * Cell * t * t);
                    icon.transform.localScale = new Vector3(1 - .09f * t, 1 + .13f * t, 1);
                }
                yield return null;
            }
            foreach (var icon in candies) icon.enabled = false;
        }

        IEnumerator AnimatePop(List<CandyEngine.Cluster> wins)
        {
            var orderedCells = new List<int>();
            foreach (var win in wins) foreach (int p in win.Cells)
                if (!orderedCells.Contains(p)) orderedCells.Add(p);
            // Row zero is the top: sweep each winning row left-to-right, bottom first.
            orderedCells.Sort((a, b) =>
            {
                int rowOrder = (b / 7).CompareTo(a / 7);
                return rowOrder != 0 ? rowOrder : (a % 7).CompareTo(b % 7);
            });
            float stagger = fast ? .018f : .045f;
            var popDelays = new float[49];
            for (int i = 0; i < orderedCells.Count; i++) popDelays[orderedCells[i]] = i * stagger;
            var sparks = new List<Image>(); var origins = new List<Vector2>(); var directions = new List<Vector2>();
            var sparkDelays = new List<float>();
            foreach (var win in wins) foreach (int p in win.Cells)
            {
                Vector2 origin = CellPosition(p % 7, p / 7);
                for (int i = 0; i < 10; i++)
                {
                    float angle = (i * 36 + p * 17) * Mathf.Deg2Rad;
                    var spark = Box(board, "Candy sparkle", origin.x, origin.y, 14, 14, Color.Lerp(colors[win.Symbol], Color.white, .45f));
                    spark.sprite = disc; spark.raycastTarget = false; spark.enabled = false;
                    sparks.Add(spark); origins.Add(origin); directions.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                    sparkDelays.Add(popDelays[p]);
                }
            }
            float elapsed = 0, duration = fast ? .32f : .60f;
            float total = duration + Mathf.Max(0, orderedCells.Count - 1) * stagger;
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                foreach (int p in orderedCells)
                {
                    float t = Mathf.Clamp01((elapsed - popDelays[p]) / duration);
                    float pop = Mathf.Clamp01((t - .38f) / .62f);
                    var icon = candies[p % 7, p / 7];
                    float scale = t < .38f ? Mathf.Lerp(1, 1.4f, t / .38f) : Mathf.Lerp(1.4f, 0, pop * pop);
                    icon.transform.localScale = Vector3.one * scale;
                    Color color = Color.white;
                    color.a = 1 - pop; icon.color = color;
                }
                for (int i = 0; i < sparks.Count; i++)
                {
                    float t = Mathf.Clamp01((elapsed - sparkDelays[i]) / duration);
                    float pop = Mathf.Clamp01((t - .38f) / .62f);
                    sparks[i].enabled = pop > 0;
                    sparks[i].rectTransform.anchoredPosition = origins[i] + directions[i] * (16 + 65 * pop) + Vector2.down * (18 * pop * pop);
                    Color color = sparks[i].color; color.a = 1 - pop; sparks[i].color = color;
                    sparks[i].transform.localScale = Vector3.one * (1 - .45f * pop);
                }
                yield return null;
            }
            foreach (var win in wins) foreach (int p in win.Cells) candies[p % 7, p / 7].enabled = false;
            foreach (var spark in sparks) Destroy(spark.gameObject);
        }

        IEnumerator AnimateFalls(List<CandyEngine.Fall> falls, bool freshSpin)
        {
            var next = new Image[7, 7];
            var recycled = new Queue<Image>[7];
            for (int x = 0; x < 7; x++)
            {
                recycled[x] = new Queue<Image>();
                for (int y = 0; y < 7; y++) if (freshSpin || !candies[x, y].enabled) recycled[x].Enqueue(candies[x, y]);
            }
            float speed = fast ? .55f : 1;
            var columnDelays = new float[7];
            for (int column = 1; column < columnDelays.Length; column++)
            {
                // Positive, cumulative gaps keep the initial reels in left-to-right order.
                float gap = freshSpin ? UnityEngine.Random.Range(.15f, .21f) : .025f;
                columnDelays[column] = columnDelays[column - 1] + gap * speed;
            }
            var durations = new float[falls.Count]; var delays = new float[falls.Count];
            float total = 0, settle = .16f * speed;
            var landingTimes = new float[7]; var landed = new bool[7];
            for (int i = 0; i < falls.Count; i++)
            {
                var fall = falls[i];
                var icon = fall.IsNew ? recycled[fall.Column].Dequeue() : candies[fall.Column, fall.FromRow];
                next[fall.Column, fall.ToRow] = icon;
                icon.sprite = symbols[fall.Symbol]; icon.color = Color.white; icon.enabled = true;
                icon.transform.localScale = Vector3.one;
                icon.rectTransform.anchoredPosition = CellPosition(fall.Column, fall.FromRow);
                int distance = fall.ToRow - fall.FromRow;
                if (distance == 0) continue; // A candy already resting on the bottom stays still.
                durations[i] = Mathf.Sqrt(2 * distance * Cell / 1850f) * speed;
                delays[i] = columnDelays[fall.Column];
                landingTimes[fall.Column] = Mathf.Max(landingTimes[fall.Column], delays[i] + durations[i]);
                total = Mathf.Max(total, delays[i] + durations[i] + settle);
            }
            candies = next;
            float elapsed = 0;
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                for (int column = 0; column < 7; column++)
                    if (!landed[column] && landingTimes[column] > 0 && elapsed >= landingTimes[column])
                    { candyAudio.Land(column); landed[column] = true; }
                for (int i = 0; i < falls.Count; i++)
                {
                    if (durations[i] == 0) continue;
                    var fall = falls[i]; var icon = candies[fall.Column, fall.ToRow];
                    float local = elapsed - delays[i];
                    Vector2 destination = CellPosition(fall.Column, fall.ToRow);
                    if (local < durations[i])
                    {
                        float t = Mathf.Clamp01(local / durations[i]);
                        icon.rectTransform.anchoredPosition = Vector2.LerpUnclamped(CellPosition(fall.Column, fall.FromRow), destination, t * t);
                        float stretch = .075f * Mathf.Sin(t * Mathf.PI);
                        icon.transform.localScale = new Vector3(1 - stretch, 1 + stretch, 1);
                    }
                    else
                    {
                        float t = Mathf.Clamp01((local - durations[i]) / settle);
                        float bounce = Mathf.Sin(t * Mathf.PI) * (1 - t);
                        icon.rectTransform.anchoredPosition = destination + Vector2.up * (10 * bounce);
                        float squash = Mathf.Sin(t * Mathf.PI * 2) * (1 - t) * .14f;
                        icon.transform.localScale = new Vector3(1 + squash, 1 - squash, 1);
                    }
                }
                yield return null;
            }
            SyncBoard();
        }

        void SyncBoard()
        {
            for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++)
            {
                int s = engine.Board[x, y];
                candies[x, y].enabled = s >= 0;
                if (s >= 0) { candies[x, y].sprite = symbols[s]; candies[x, y].color = Color.white; }
                candies[x, y].transform.localScale = Vector3.one;
                candies[x, y].rectTransform.anchoredPosition = CellPosition(x, y);
            }
            RefreshBadges();
        }
        void RefreshBadges()
        {
            for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++)
            {
                int m = engine.Multiplier(x, y);
                badges[x, y].text = m > 0 ? m + "×" : engine.Hits[x, y] > 0 ? "•" : "";
                var tile = multiplierTiles[x, y];
                tile.enabled = m >= 2;
                // Equal opacity steps per doubling: 8% at 2x, 80% at 128x.
                Color tint = Hex("FF9900");
                tint.a = m >= 2 ? Mathf.Lerp(.08f, .80f, Mathf.Clamp01((Mathf.Log(m, 2) - 1) / 6)) : 0;
                tile.color = tint;
            }
        }
        // HUD changes must never snap symbols while an animation is in progress.
        void Refresh()
        {
            balanceText.text = CandyCreditDisplay.Format(balance); betText.text = CandyCreditDisplay.Format(bets[betIndex]); winText.text = CandyCreditDisplay.Format(spinWin);
            buyPriceText.text = CandyCreditDisplay.Format(bets[betIndex] * 100m) + " credits";
            bonusText.text = freeSpins > 0 ? freeSpins + " FREE SPINS LEFT" : "THE SUGAR GARDEN";
            spinButton.interactable =
                        casinoWallet != null &&
                        casinoWallet.IsLoaded &&
                        !busy &&
                        freeSpins == 0; spinText.text = busy ? "TUMBLING" : "SPIN  ›";
            foreach (var b in lockedButtons)
            {
                b.interactable =
                    casinoWallet != null &&
                    casinoWallet.IsLoaded &&
                    !busy &&
                    freeSpins == 0;
            }
            autoText.text = autoPlay ? "AUTO: ON" : "AUTO: OFF";
            soundText.text = muted ? "SOUND OFF" : "SOUND ON";
        }
        Image Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<Image>(); img.sprite = round; img.type = Image.Type.Sliced; img.color = color; img.raycastTarget = true; return img;
        }
        Text Label(Transform parent, string value, float x, float y, float w, float h, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter, bool bold = false)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
            var txt = go.GetComponent<Text>(); txt.font = font; txt.text = value; txt.fontSize = size; txt.color = color; txt.alignment = alignment; txt.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; txt.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .85f); shadow.effectDistance = new Vector2(1, -2);
            return txt;
        }
        Button ButtonAt(Transform parent, string title, float x, float y, float w, float h, UnityEngine.Events.UnityAction action, Color color, bool lockDuringSpin = false)
        {
            var img = Box(parent, title, x, y, w, h, Color.clear); var b = img.gameObject.AddComponent<Button>();
            var c = b.colors; c.highlightedColor = new Color(1.15f, 1.15f, 1.15f); c.pressedColor = new Color(.8f, .8f, .8f); c.disabledColor = new Color(.55f, .55f, .55f); b.colors = c;
            var label = Label(img.transform, title, 0, 0, w - 12, h - 4, 17, title == "SPIN  ›" ? color : Hex("E6DCEA"), TextAnchor.MiddleCenter, true);
            b.targetGraphic = label;
            b.onClick.AddListener(() => { if (title != "SPIN  ›") candyAudio.Click(); action(); }); if (lockDuringSpin) lockedButtons.Add(b); return b;
        }
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
        Sprite MakeShape(int kind)
        {
            const int n = 128; var texture = new Texture2D(n, n, TextureFormat.RGBA32, false); texture.name = "Original candy " + kind;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float px = (x + .5f - n / 2f) / (n / 2f), py = (y + .5f - n / 2f) / (n / 2f), d;
                float ax = Mathf.Abs(px), ay = Mathf.Abs(py), r = Mathf.Sqrt(px * px + py * py);
                if (kind == -2) d = new Vector2(Mathf.Max(ax - .65f, 0), Mathf.Max(ay - .65f, 0)).magnitude - .3f;
                else if (kind == -1 || kind == 1) d = r - .83f;
                else if (kind == 0) { float yy = py + .1f; d = Mathf.Pow(px * px + yy * yy - .42f, 3) - px * px * yy * yy * yy; d *= 5; }
                else if (kind == 2) d = ax * .85f + ay * .65f - .68f;
                else if (kind == 3) d = Mathf.Pow(ax, 4) + Mathf.Pow(ay, 4) - .32f;
                else if (kind == 4) d = r - (.7f + .1f * Mathf.Cos(Mathf.Atan2(py, px) * 6));
                else if (kind == 5) d = Mathf.Sqrt(px * px * 1.7f + py * py * .8f) - .74f;
                else d = r - (.61f + .22f * Mathf.Cos((Mathf.Atan2(py, px) - Mathf.PI / 2) * 5));
                float alpha = Mathf.Clamp01(-d * 80);
                float lighting = kind < 0 ? 1 : Mathf.Clamp01(.70f + .20f * py + .18f * (1 - r));
                bool shine = kind >= 0 && Mathf.Pow((px + .22f) / .28f, 2) + Mathf.Pow((py - .38f) / .12f, 2) < 1;
                texture.SetPixel(x, y, new Color(shine ? 1 : lighting, shine ? 1 : lighting, shine ? 1 : lighting, alpha));
            }
            texture.Apply(); texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, kind == -2 ? new Vector4(25, 25, 25, 25) : Vector4.zero);
        }
        IEnumerator CaptureFrame(string name)
        {
            yield return new WaitForEndOfFrame();
            var canvas = root.GetComponentInParent<Canvas>();
            var camera = Camera.allCameras[0];
            var target = new RenderTexture(1240, 800, 24, RenderTextureFormat.ARGB32);
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1240, 800, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1240, 800), 0, 0); texture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", name), texture.EncodeToPNG());
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
            target.Release(); Destroy(target); Destroy(texture);
        }
        IEnumerator Capture()
        {
            yield return new WaitForSeconds(8);
            // Deterministic showcase, including the reported casino credit value.
            OnCasinoBalanceLoaded(3069.77m);
            for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++) engine.Board[x, y] = (x + 2 * y) % 7;
            SyncBoard();
            yield return CaptureFrame("Preview.png");
            yield return new WaitForSeconds(2);
            bool captureMotion = Array.IndexOf(Environment.GetCommandLineArgs(), "-candyMotionCapture") >= 0;
            if (captureMotion) StartCoroutine(RecordMotion("Spin"));
            help.SetActive(true); KeyboardSpin("");
            if (busy) throw new Exception("Keyboard spin bypassed help dialog");
            help.SetActive(false);
            ShowBuyConfirmation(); KeyboardSpin("");
            if (busy || !buyConfirmation.activeSelf) throw new Exception("Keyboard spin confirmed a purchase");
            buyConfirmation.SetActive(false);
            KeyboardSpin("");
            while (busy || freeSpins > 0) yield return null;
            yield return new WaitForSeconds(3);
            // Guaranteed bottom-row win exercises large survivor drops and top refills.
            for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++)
            { engine.Board[x, y] = (x + y) % 2; engine.Hits[x, y] = 0; }
            for (int x = 0; x < 5; x++) engine.Board[x, 6] = 5;
            SyncBoard();
            var before = (Image[,])candies.Clone();
            var wins = engine.FindWins(1);
            if (captureMotion) StartCoroutine(RecordMotion("Cascade"));
            yield return AnimatePop(wins); engine.Remove(wins); RefreshBadges();
            var falls = engine.Collapse();
            yield return AnimateFalls(falls, false);
            var distinct = new HashSet<Image>();
            foreach (var fall in falls)
            {
                var icon = candies[fall.Column, fall.ToRow];
                if (!distinct.Add(icon) || Vector2.Distance(icon.rectTransform.anchoredPosition, CellPosition(fall.Column, fall.ToRow)) > .1f
                    || icon.sprite != symbols[fall.Symbol] || !icon.enabled)
                    throw new Exception("Invalid visual binding after cascade at " + fall.Column + "," + fall.ToRow + ": actual position " + icon.rectTransform.anchoredPosition + ", expected " + CellPosition(fall.Column, fall.ToRow) + ", actual sprite " + icon.sprite.name + ", expected " + symbols[fall.Symbol].name + ", enabled " + icon.enabled + ", busy " + busy);
                if (!fall.IsNew && icon != before[fall.Column, fall.FromRow]) throw new Exception("Surviving candy identity lost");
            }
            yield return new WaitForSeconds(3);
            fast = true;
            ShowBuyConfirmation();
            ConfirmBuyBonus();
            while (busy || freeSpins > 0) yield return null;
            if (balance < 0 || !spinButton.interactable) throw new Exception("Invalid end-of-bonus state");
            help.SetActive(true);
            yield return new WaitForSeconds(.5f);
            yield return CaptureFrame("Rules.png");
            yield return new WaitForSeconds(1);
            Debug.Log("CANDY_RUNTIME_SMOKE_PASS");
            Application.Quit();
        }
        IEnumerator RecordMotion(string name)
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", "Motion");
            System.IO.Directory.CreateDirectory(path);
            for (int i = 0; i < 40; i++)
            {
                // Exercise HUD controls during animation: they must not move symbols.
                Vector2 position = candies[0, 0].rectTransform.anchoredPosition;
                Refresh();
                if (candies[0, 0].rectTransform.anchoredPosition != position) throw new Exception("HUD snapped an animated symbol");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(path, name + i.ToString("D3") + ".png"));
                yield return new WaitForSeconds(.065f);
            }
        }
    }
}
