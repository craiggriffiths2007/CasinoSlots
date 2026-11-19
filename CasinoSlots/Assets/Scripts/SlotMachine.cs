using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SlotMachine : MonoBehaviour
{
    [SerializeField]
    private bool forceWin;

    [SerializeField]
    private SymbolLibrary symbolLibrary;

    [SerializeField]
    private Symbol symbolPrefab;

    [SerializeField]
    private int reelCount = 5;

    [SerializeField]
    private int rowCount = 3;

    [SerializeField]
    private float horizontalSpacing = 2f;

    [SerializeField]
    private float verticalSpacing = 2f;

    private Reel[] reels;

    private SlotEngine slotEngine;

    private bool isSpinning;
    private static readonly int[] BetAmounts = { 10, 25, 50, 100 };
    private Dropdown betSelector;
    private Text winNotification;
    private Button spinButton;
    private Font hudFont;
    private PhoenixAudio gameAudio;
    private Text balanceNotification;
    private Text spinCaption;
    private CasinoCreditClient credits = null;
    private readonly Image[] ladderRungs = new Image[8];
    private readonly Text[] ladderLabels = new Text[8];
    private Text runningWinCounter, freeSpinCounter, bonusMessage;
    private long runningWin;
    private Sprite spinButtonSprite;
    private Texture2D spinButtonTexture;
    private GameObject bonusOverlay;


    private readonly List<LineRenderer> winningLines = new();
    private Material winningLineMaterial;
    private const float PaylineTimingScale = 2f / 3f;
    private const float PaylineDuration = 1.0f * PaylineTimingScale;
    // Visual randomness must never consume the slot engine's UnityEngine.Random stream.
    private readonly System.Random flameRandom = new System.Random();

    private float FlameRandom(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)flameRandom.NextDouble());
    }
    private readonly List<ParticleSystem> winningLineFires = new();


    private void Start()
    {
        slotEngine =
            new SlotEngine();

        gameAudio = gameObject.AddComponent<PhoenixAudio>();
        // Tight framing leaves room for the controls below the three reel rows.
        if (Camera.main != null)
        {
            Camera.main.orthographicSize = 4.2f;
            var cameraPosition = Camera.main.transform.position;
            cameraPosition.y = -0.45f;
            Camera.main.transform.position = cameraPosition;
        }
        CreateReels();
        CreateGameHud();

#if UNITY_WEBGL && !UNITY_EDITOR
        // This is decorative only. Paid outcomes always come from the casino server.
        var initial = new SymbolType[3, 5];
        for (int row = 0; row < 3; row++)
            for (int reel = 0; reel < 5; reel++)
                initial[row, reel] = (SymbolType)((row * 5 + reel) % 6);
        ShowBoard(initial);
        credits = new CasinoCreditClient();
        StartCoroutine(ConnectCredits());
#else
        ShowBoard(slotEngine.Spin().InitialBoard);
        balanceNotification.text = "EDITOR DEMO";
#endif
    }

    private void CreateGameHud()
    {
        // Reuse the scene's wired Spin button and its canvas.
        spinButton = GameObject.Find("SpinButton").GetComponent<Button>();
        Canvas canvas = spinButton.GetComponentInParent<Canvas>();
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 700f);
        scaler.matchWidthOrHeight = 0.5f;
        hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");



        GameObject footer = new GameObject("Firebird controls", typeof(RectTransform), typeof(Image));
        footer.transform.SetParent(canvas.transform, false);
        RectTransform footerRect = (RectTransform)footer.transform;
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.sizeDelta = new Vector2(0f, 126f);
        footer.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.06f, 0.94f);

        spinButton.transform.SetParent(footer.transform, false);
        PlaceHud((RectTransform)spinButton.transform, new Vector2(1f, 0f),
            new Vector2(-116f, 79f), new Vector2(200f, 48f));
        RoundSpinButton();
        spinButton.GetComponent<Image>().color = new Color(0.12f, 0.42f, 0.84f);
        // Replace only its label, preserving the existing scene click binding.
        foreach (Transform child in spinButton.transform)
            child.gameObject.SetActive(false);
        Text spinLabel = CreateHudText("Spin label", spinButton.transform, "SPIN", 24);
        spinCaption = spinLabel;
        RectTransform spinLabelRect = (RectTransform)spinLabel.transform;
        spinLabelRect.anchorMin = Vector2.zero;
        spinLabelRect.anchorMax = Vector2.one;
        spinLabelRect.sizeDelta = Vector2.zero;

        Text betLabel = CreateHudText("Bet label", footer.transform, "Bet", 17);
        PlaceHud((RectTransform)betLabel.transform, new Vector2(0.5f, 0f),
            new Vector2(-110f, 115f), new Vector2(120f, 24f));

        GameObject selector = DefaultControls.CreateDropdown(new DefaultControls.Resources());
        selector.name = "Bet amount";
        selector.transform.SetParent(footer.transform, false);
        PlaceHud((RectTransform)selector.transform, new Vector2(0.5f, 0f),
            new Vector2(-110f, 79f), new Vector2(120f, 48f));
        betSelector = selector.GetComponent<Dropdown>();
        betSelector.ClearOptions();
        betSelector.AddOptions(BetAmounts.Select(amount => amount.ToString()).ToList());
        foreach (Text text in selector.GetComponentsInChildren<Text>(true))
        {
            text.font = hudFont;
            text.fontSize = 21;
            text.color = new Color(0.08f, 0.1f, 0.14f);
        }
        // Open upwards so all four amounts remain visible above the footer.
        betSelector.template.anchorMin = new Vector2(0f, 1f);
        betSelector.template.anchorMax = new Vector2(1f, 1f);
        betSelector.template.pivot = new Vector2(0.5f, 0f);
        betSelector.template.anchoredPosition = new Vector2(0f, 4f);
        Transform arrow = selector.transform.Find("Arrow");
        if (arrow != null)
            arrow.gameObject.SetActive(false);
        Text arrowLabel = CreateHudText("Bet arrow", selector.transform, "v", 18);
        arrowLabel.color = Color.black;
        PlaceHud((RectTransform)arrowLabel.transform, new Vector2(1f, 0.5f),
            new Vector2(-14f, 0f), new Vector2(20f, 24f));
        RectTransform itemRect = (RectTransform)betSelector.itemText.transform.parent;
        itemRect.sizeDelta = new Vector2(0f, 34f);
        ((RectTransform)itemRect.parent).sizeDelta = new Vector2(0f, 42f);
        betSelector.template.sizeDelta = new Vector2(0f, 160f);
        betSelector.RefreshShownValue();

        balanceNotification = CreateHudText("Credits", footer.transform, "CREDITS --", 21);
        PlaceHud((RectTransform)balanceNotification.transform, new Vector2(0.5f, 0f),
            new Vector2(-370f, 79f), new Vector2(300f, 48f));
        balanceNotification.resizeTextForBestFit = true;
        balanceNotification.resizeTextMinSize = 12;
        balanceNotification.resizeTextMaxSize = 21;

        GameObject soundControl = new GameObject("Sound toggle", typeof(RectTransform), typeof(Image), typeof(Button));
        soundControl.transform.SetParent(footer.transform, false);
        PlaceHud((RectTransform)soundControl.transform, new Vector2(0.5f, 0f),
            new Vector2(160f, 79f), new Vector2(170f, 44f));
        soundControl.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f);
        Text soundLabel = CreateHudText("Sound label", soundControl.transform,
            gameAudio.Muted ? "SOUND OFF" : "SOUND ON", 18);
        RectTransform soundLabelRect = (RectTransform)soundLabel.transform;
        soundLabelRect.anchorMin = Vector2.zero;
        soundLabelRect.anchorMax = Vector2.one;
        soundLabelRect.sizeDelta = Vector2.zero;
        Button soundButton = soundControl.GetComponent<Button>();
        soundButton.targetGraphic = soundControl.GetComponent<Image>();
        soundButton.onClick.AddListener(() =>
        {
            gameAudio.ToggleMute();
            soundLabel.text = gameAudio.Muted ? "SOUND OFF" : "SOUND ON";
        });

        winNotification = CreateHudText("Win notification", footer.transform, "", 24);
        PlaceHud((RectTransform)winNotification.transform, new Vector2(0.5f, 0f),
            new Vector2(0f, 25f), new Vector2(560f, 40f));
        winNotification.color = new Color(1f, 0.82f, 0.38f);
        CreateCascadeLadder(canvas.transform);
    }

    private void CreateCascadeLadder(Transform canvas)
    {
        freeSpinCounter = CreateHudText("Free spins remaining", canvas, "", 20);
        PlaceHud((RectTransform)freeSpinCounter.transform, new Vector2(1f, .5f), new Vector2(-91, 254), new Vector2(168, 64));
        freeSpinCounter.color = new Color(1f, .82f, .38f);
        freeSpinCounter.resizeTextForBestFit = true;
        freeSpinCounter.resizeTextMinSize = 14;
        freeSpinCounter.resizeTextMaxSize = 20;
        freeSpinCounter.gameObject.SetActive(false);
        for (int i = 0; i < 8; i++)
        {
            var rung = new GameObject("Cascade tier " + (i + 1), typeof(RectTransform), typeof(Image));
            rung.transform.SetParent(canvas, false);
            PlaceHud((RectTransform)rung.transform, new Vector2(1f, .5f), new Vector2(-91, -182 + i * 53), new Vector2(150, 47));
            ladderRungs[i] = rung.GetComponent<Image>();
            ladderRungs[i].sprite = spinButtonSprite;
            ladderRungs[i].type = Image.Type.Sliced;
            ladderRungs[i].raycastTarget = false;
            int award = CascadeLadder.Award(i + 1);
            var label = CreateHudText("Tier label", rung.transform,
                award > 0 ? $"{award} SPINS" : "", 15);
            ladderLabels[i] = label;
            PlaceHud((RectTransform)label.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(142, 43));
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 15;
        }
        runningWinCounter = CreateHudText("Running win total", canvas, "", 26);
        PlaceHud((RectTransform)runningWinCounter.transform, new Vector2(.5f, 1f), new Vector2(0, -26), new Vector2(550, 40));
        runningWinCounter.color = new Color(1f, .82f, .38f);
        runningWinCounter.resizeTextForBestFit = true;
        runningWinCounter.resizeTextMinSize = 14;
        runningWinCounter.resizeTextMaxSize = 26;
        runningWinCounter.gameObject.SetActive(false);
        bonusOverlay = new GameObject("Free spins awarded", typeof(RectTransform), typeof(Image));
        bonusOverlay.transform.SetParent(canvas, false);
        var overlayRect = (RectTransform)bonusOverlay.transform;
        overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        bonusOverlay.GetComponent<Image>().color = new Color(0, 0, 0, .85f);
        bonusMessage = CreateHudText("Bonus award", bonusOverlay.transform, "", 34);
        PlaceHud((RectTransform)bonusMessage.transform, new Vector2(.5f, .5f), new Vector2(0, 45), new Vector2(650, 130));
        var ok = new GameObject("OK", typeof(RectTransform), typeof(Image), typeof(Button));
        ok.transform.SetParent(bonusOverlay.transform, false);
        PlaceHud((RectTransform)ok.transform, new Vector2(.5f, .5f), new Vector2(0, -80), new Vector2(210, 60));
        ok.GetComponent<Image>().color = new Color(.65f, .34f, .08f);
        ok.GetComponent<Button>().onClick.AddListener(() => bonusOverlay.SetActive(false));
        var caption = CreateHudText("OK label", ok.transform, "OK", 26);
        PlaceHud((RectTransform)caption.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(210, 60));
        bonusOverlay.SetActive(false);
        runningWinCounter.transform.SetAsLastSibling();
        freeSpinCounter.transform.SetAsLastSibling();
        UpdateLadder(0);
    }

    private void UpdateLadder(int count)
    {
        for (int i = 0; i < ladderRungs.Length; i++)
        {
            bool selected = i + 1 == Mathf.Min(count, 8) && count > 0;
            ladderRungs[i].color = selected
                ? new Color(.95f, .51f, .09f, .98f)
                : i < count ? new Color(.57f, .30f, .08f, .9f) : new Color(.16f, .12f, .09f, .85f);
            ladderLabels[i].color = new Color(1f, 1f, 1f, selected ? 1f : 0f);
        }
    }

    private void RoundSpinButton()
    {
        const int size = 32;
        const float radius = 12f;
        spinButtonTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        spinButtonTexture.name = "Rounded spin button";
        spinButtonTexture.wrapMode = TextureWrapMode.Clamp;
        spinButtonTexture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + .5f - size * .5f) - (size * .5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + .5f - size * .5f) - (size * .5f - radius), 0f);
                pixels[y * size + x] = new Color(1f, 1f, 1f,
                    Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy)));
            }
        spinButtonTexture.SetPixels(pixels);
        spinButtonTexture.Apply();
        spinButtonSprite = Sprite.Create(spinButtonTexture, new Rect(0, 0, size, size),
            new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        Image image = spinButton.GetComponent<Image>();
        image.overrideSprite = null;
        image.sprite = spinButtonSprite;
        image.type = Image.Type.Sliced;
    }

    private IEnumerator CountCascadeWin(long amount, bool freeSpin)
    {
        if (!freeSpin)
        {
            yield return new WaitForSeconds(.25f);
            yield break;
        }
        long previous = runningWin;
        runningWin += amount;
        runningWinCounter.gameObject.SetActive(true);
        float elapsed = 0f;
        const float duration = .25f;
        while (elapsed < duration)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            long displayed = previous + (long)System.Math.Round(amount * (double)progress);
            runningWinCounter.text = $"TOTAL WIN {displayed:N0} credits";
            elapsed += Time.deltaTime;
            yield return null;
        }
        runningWinCounter.text = $"TOTAL WIN {runningWin:N0} credits";
    }

    private Text CreateHudText(string name, Transform parent, string value, int size)
    {
        GameObject label = new GameObject(name, typeof(RectTransform), typeof(Text));
        label.transform.SetParent(parent, false);
        Text text = label.GetComponent<Text>();
        text.font = hudFont;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void PlaceHud(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void CreateReels()
    {
        reels =
            new Reel[reelCount];

        float startX =
            -((reelCount - 1) *
              horizontalSpacing) / 2f;

        for (int reelIndex = 0;
             reelIndex < reelCount;
             reelIndex++)
        {
            GameObject reelObject =
                new GameObject(
                    $"Reel {reelIndex + 1}");

            reelObject.transform
                .SetParent(
                    transform,
                    false);

            reelObject.transform
                .localPosition =
                    new Vector3(
                        startX +
                        reelIndex *
                        horizontalSpacing,
                        0f,
                        0f);

            Reel reel =
                reelObject.AddComponent<
                    Reel>();

            reel.SymbolLanded += gameAudio.Land;
            reel.Initialise(
                symbolPrefab,
                rowCount,
                verticalSpacing);

            reels[reelIndex] =
                reel;
        }
    }

    private void LateUpdate()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !Application.isFocused || isSpinning ||
            spinButton == null || !spinButton.IsActive() || !spinButton.IsInteractable() ||
            (bonusOverlay != null && bonusOverlay.activeSelf))
            return;
        if (!keyboard.spaceKey.wasPressedThisFrame && !keyboard.enterKey.wasPressedThisFrame &&
            !keyboard.numpadEnterKey.wasPressedThisFrame)
            return;

        // Let focused controls (such as the bet dropdown) handle their own keys.
        // Run after UI updates so submitting the Spin button cannot spin twice.
        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null)
        {
            Selectable control = selected.GetComponentInParent<Selectable>();
            if (control != null && control != spinButton) return;
        }
        Spin();
    }

    public void Spin()
    {
        if (isSpinning)
            return;

        gameAudio.Begin();
        if (credits != null)
        {
            StartCoroutine(credits.Ready ? RequestCreditSpin() : ConnectCredits());
            return;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        winNotification.text = "Open this game from your casino to play.";
#else
        slotEngine.ForceWin = forceWin;
        int bet = BetAmounts[betSelector.value];
        var result = slotEngine.Spin(bet);
        // Award the highest reached tier once, after the base spin finishes.
        int award = CascadeLadder.Award(result.Cascades.Count);
        result.FreeSpinsAwarded = award;
        slotEngine.ForceWin = false;
        int remaining = award;
        while (remaining > 0)
        {
            remaining--;
            var bonus = slotEngine.Spin(bet, true);
            result.FreeSpins.Add(bonus);
            bonus.FreeSpinsAwarded = 0;
        }
        StartCoroutine(SpinRoutine(result));
#endif
    }

    private IEnumerator ConnectCredits()
    {
        isSpinning = true;
        betSelector.interactable = false;
        spinButton.interactable = false;
        winNotification.text = "Connecting to casino...";
        yield return credits.Connect();
        winNotification.text = credits.Error ?? "";
        isSpinning = false;
        RefreshCreditControls();
    }

    private IEnumerator RequestCreditSpin()
    {
        isSpinning = true;
        betSelector.interactable = false;
        spinButton.interactable = false;
        winNotification.text = credits.HasPending ? "Recovering spin..." : "Placing bet...";
        yield return credits.Spin(BetAmounts[betSelector.value]);
        if (credits.Result == null)
        {
            winNotification.text = credits.Error;
            isSpinning = false;
            RefreshCreditControls();
            yield break;
        }
        balanceNotification.text = $"CREDITS {credits.Balance}";
        yield return SpinRoutine(credits.Result);
    }

    private void RefreshCreditControls()
    {
        if (credits == null) return;
        if (credits.Ready) balanceNotification.text = $"CREDITS {credits.Balance}";
        spinCaption.text = credits.HasPending || !credits.Ready ? "RETRY" : "SPIN";
        spinButton.interactable = !isSpinning;
        betSelector.interactable = !isSpinning && credits.Ready && !credits.HasPending;
    }

    private IEnumerator SpinRoutine(
        SpinResult result, bool freeSpin = false)
    {
        isSpinning = true;
        betSelector.interactable = false;
        spinButton.interactable = false;
        winNotification.text = "";
        UpdateLadder(0);
        for (int i = 0; i < ladderLabels.Length; i++)
        {
            ladderRungs[i].gameObject.SetActive(!freeSpin);
            ladderLabels[i].gameObject.SetActive(!freeSpin);
            int award = freeSpin ? CascadeLadder.Retrigger(i + 1) : CascadeLadder.Award(i + 1);
            ladderLabels[i].text = award > 0 ? $"{award} SPINS" : "";
        }
        if (!freeSpin)
        {
            freeSpinCounter.text = "";
            freeSpinCounter.gameObject.SetActive(false);
            runningWin = 0;
            runningWinCounter.text = "TOTAL WIN 0 credits";
            runningWinCounter.gameObject.SetActive(false);
        }

        gameAudio.Drop();
        Coroutine[] flights = new Coroutine[reelCount];
        for (int reel = 0; reel < reelCount; reel++)
            flights[reel] = StartCoroutine(reels[reel].DropOut(8f, 0.32f, reel * 0.025f));
        foreach (Coroutine flight in flights)
            yield return flight;

        yield return new WaitForSeconds(0.12f);
        ShowBoard(result.InitialBoard);
        for (int reel = 0; reel < reelCount; reel++)
            flights[reel] = StartCoroutine(DropInWithSound(reel));
        foreach (Coroutine flight in flights)
            yield return flight;

        // Animate every cascade already
        // calculated by SlotEngine.
        int cascadeCount = 0;
        foreach (CascadeResult cascade
                 in result.Cascades)
        {
            winNotification.text = cascade.Win > 0 ? $"WIN {cascade.Win} credits" : "MATCH · less than 1 credit";
            UpdateLadder(++cascadeCount);
            gameAudio.StartFire(false);
            yield return BurnWinningLines(cascade.Wins);
            gameAudio.StopFire();
            gameAudio.StartFire(true);
            yield return RemoveWins(cascade.Wins);
            gameAudio.StopFire();
            gameAudio.Drop();
            if (cascade.PhoenixReels.Count > 0) gameAudio.Phoenix();
            ConfigurePhoenixGroups(cascade.BoardAfter);

            List<SymbolPosition>
                removedPositions =
                    cascade.Wins
                        .SelectMany(
                            w => w.Positions)
                        .GroupBy(
                            p => new
                            {
                                p.Row,
                                p.Reel
                            })
                        .Select(
                            g => g.First())
                        .ToList();

            Coroutine[] cascadeRoutines =
                new Coroutine[reelCount];

            for (int reel = 0;
                 reel < reelCount;
                 reel++)
            {
                bool becomesPhoenix =
                    cascade.PhoenixReels
                        .Contains(reel);

                bool expiresPhoenix =
                    cascade
                        .ExpiredPhoenixReels
                        .Contains(reel);

                cascadeRoutines[reel] =
                    StartCoroutine(
                       reels[reel].CascadeToBoard(
    cascade.BoardBefore,
    cascade.BoardAfter,
    reel,
    removedPositions,
    symbolLibrary,
    verticalSpacing,
    becomesPhoenix,
    expiresPhoenix));
            }

            // All were started above, so they
            // continue concurrently.
            for (int reel = 0;
                 reel < reelCount;
                 reel++)
            {
                yield return
                    cascadeRoutines[reel];
            }

            // Count bonus cascades only; retain the same pause on paid spins.
            yield return CountCascadeWin(cascade.Win, freeSpin);
        }

        winNotification.text = result.TotalWin > 0
            ? $"TOTAL WIN {result.TotalWin} credits"
            : "No win";
        if (freeSpin) yield break;
        if (result.FreeSpins.Count > 0)
        {
            int remaining = result.FreeSpinsAwarded > 0 ? result.FreeSpinsAwarded : result.FreeSpins.Count;
            // Bonus winnings start at zero and survive non-winning free spins.
            runningWin = 0;
            runningWinCounter.text = "TOTAL WIN 0 credits";
            runningWinCounter.gameObject.SetActive(true);
            freeSpinCounter.gameObject.SetActive(true);
            freeSpinCounter.text = $"SPINS REMAINING\n{remaining}";
            bonusMessage.text = $"You won {remaining} free spins!";
            bonusOverlay.SetActive(true);
            // Celebrate the bonus award, rather than each total-win display.
            gameAudio.Payout();
            while (bonusOverlay.activeSelf) yield return null;
            long bonusWin = 0;
            for (int i = 0; i < result.FreeSpins.Count; i++)
            {
                remaining--;
                freeSpinCounter.text = $"SPINS REMAINING\n{remaining}";
                yield return SpinRoutine(result.FreeSpins[i], true);
                bonusWin += result.FreeSpins[i].TotalWin;
                int extra = result.FreeSpins[i].FreeSpinsAwarded;
                if (extra > 0)
                {
                    remaining += extra;
                    freeSpinCounter.text = $"SPINS REMAINING\n{remaining}";
                    winNotification.text = $"+{extra} FREE SPINS";
                    yield return new WaitForSeconds(.8f);
                }
                yield return new WaitForSeconds(.45f);
            }
            freeSpinCounter.text = "";
            freeSpinCounter.gameObject.SetActive(false);
            runningWinCounter.gameObject.SetActive(false);
            winNotification.text = $"BONUS WIN {bonusWin}  •  TOTAL {result.TotalWin + bonusWin} credits";
        }
        for (int i = 0; i < ladderRungs.Length; i++)
        {
            ladderRungs[i].gameObject.SetActive(true);
            ladderLabels[i].gameObject.SetActive(true);
            int award = CascadeLadder.Award(i + 1);
            ladderLabels[i].text = award > 0 ? $"{award} SPINS" : "";
        }
        UpdateLadder(0);
        credits?.FinishSpinPresentation();
        isSpinning = false;
        betSelector.interactable = true;
        spinButton.interactable = true;
        RefreshCreditControls();
    }

    private IEnumerator DropInWithSound(int reel)
    {
        yield return reels[reel].DropIn(
            verticalSpacing * 1.5f, 0.42f, reel * 0.24f, verticalSpacing);
    }

    private void ShowWinningLines(List<WinResult> wins)
    {
        ClearWinningLines();
        if (winningLineMaterial == null)
        {
            winningLineMaterial = new Material(Shader.Find("UI/Default"));
            winningLineMaterial.name = "Winning Paylines";
            winningLineMaterial.mainTexture = Texture2D.whiteTexture;
        }

        var paths = new List<(string Name, Vector3[] Points, bool FireOnly)>();
        foreach (WinResult win in wins)
        {
            paths.Add(($"Winning line {win.PaylineIndex + 1}", win.Positions.OrderBy(p => p.Reel)
                .Select(p => new Vector3(
                    (p.Reel - (reelCount - 1) * .5f) * horizontalSpacing,
                    ((rowCount - 1) * .5f - p.Row) * verticalSpacing, 0f)).ToArray(), false));
        }
        // Shared symbols get one closed, four-sided outline, even on multiple paylines.
        foreach (var cell in wins.SelectMany(w => w.Positions).Select(p => (p.Row, p.Reel)).Distinct())
        {
            float x = (cell.Reel - (reelCount - 1) * .5f) * horizontalSpacing;
            float y = ((rowCount - 1) * .5f - cell.Row) * verticalSpacing;
            float halfWidth = horizontalSpacing * .46f;
            float halfHeight = verticalSpacing * .46f;
            paths.Add(($"Winning symbol box {cell.Reel}:{cell.Row}", new[] {
                new Vector3(x - halfWidth, y - halfHeight, 0),
                new Vector3(x - halfWidth, y + halfHeight, 0),
                new Vector3(x + halfWidth, y + halfHeight, 0),
                new Vector3(x + halfWidth, y - halfHeight, 0),
                new Vector3(x - halfWidth, y - halfHeight, 0)
            }, true));
        }
            // Lines and boxes share the same fire and cleanup timeline.
        foreach (var outline in paths)
        {
            GameObject path = new GameObject(outline.Name);
            path.transform.SetParent(transform, false);
            LineRenderer line = path.AddComponent<LineRenderer>();
            line.sharedMaterial = winningLineMaterial;
            line.useWorldSpace = false;
            line.widthMultiplier = 0.095f;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.sortingLayerID = symbolPrefab.Renderer.sortingLayerID;
            line.sortingOrder = 30;
            Color color = new Color(1f, 0.98f, 0.72f);
            line.startColor = color;
            line.endColor = color;
            line.positionCount = outline.Points.Length;
            line.SetPositions(outline.Points);
            // A dark rim separates fire from bright artwork; orange surrounds the hot core.
            if (!outline.FireOnly)
            {
                AddPaylineLayer(line, "Contrast rim", 0.27f, new Color(0.3f, 0.035f, 0.005f, 0.9f), 28);
                AddPaylineLayer(line, "Orange glow", 0.19f, new Color(1f, 0.32f, 0.015f, 0.95f), 29);
            }
            // Keep the box path for particle placement without rendering a solid border.
            line.enabled = !outline.FireOnly;
            winningLines.Add(line);

            // Reuse the symbol flame texture and lifetime, emitting along the path.
            ParticleSystem fire = Symbol.CreateFire(symbolPrefab.Renderer,
                new Bounds(transform.position, Vector3.one), Symbol.BurnDuration);
            fire.gameObject.name = outline.Name + " fire";
            fire.transform.SetParent(path.transform, false);
            fire.transform.localPosition = Vector3.zero;
            var emission = fire.emission;
            emission.enabled = false;
            var shape = fire.shape;
            shape.enabled = false;
            var main = fire.main;
            main.maxParticles = 1024;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.46f);
            // Slow both sources of motion; keep each flame's rise slightly different.
            var velocity = fire.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.125f, 0.125f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.75f, 1.5f);
            var flameColor = fire.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, .95f, .5f), 0f),
                    new GradientColorKey(new Color(1f, .4f, .02f), .5f),
                    new GradientColorKey(new Color(.8f, .08f, .01f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, .12f),
                    new GradientAlphaKey(.9f, .7f),
                    new GradientAlphaKey(.6f, .9f),
                    new GradientAlphaKey(0f, 1f)
                });
            flameColor.color = gradient;
            var flameSize = fire.sizeOverLifetime;
            flameSize.enabled = true;
            flameSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(.18f, .88f),
                new Keyframe(.65f, .75f), new Keyframe(.9f, .4f),
                new Keyframe(1f, 0f)));
            var renderer = fire.GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerID = line.sortingLayerID;
            renderer.sortingOrder = line.sortingOrder + 1;
            winningLineFires.Add(fire);
        }
    }

    private void AddPaylineLayer(LineRenderer core, string name, float width, Color color, int order)
    {
        var layer = new GameObject(name).AddComponent<LineRenderer>();
        layer.transform.SetParent(core.transform, false);
        layer.sharedMaterial = winningLineMaterial;
        layer.useWorldSpace = false;
        layer.widthMultiplier = width;
        layer.numCapVertices = layer.numCornerVertices = 6;
        layer.sortingLayerID = core.sortingLayerID;
        layer.sortingOrder = order;
        layer.startColor = layer.endColor = color;
        layer.positionCount = core.positionCount;
        var points = new Vector3[core.positionCount];
        core.GetPositions(points);
        layer.SetPositions(points);
    }

    private IEnumerator BurnWinningLines(List<WinResult> wins)
    {
        float paylineDuration = PaylineDuration * 2f * 0.8f;
        float emberDuration = Symbol.EmberDuration * PaylineTimingScale * 1.5f * 2f * 0.8f;
        float totalDuration = (paylineDuration + emberDuration - 0.10f) * 0.8f;
        paylineDuration *= 0.8f;
        ShowWinningLines(wins);
        var pulses = new List<Coroutine>();
        var phoenixOwners = new HashSet<Reel>();
        foreach (var position in wins.SelectMany(w => w.Positions)
                     .Select(p => (p.Row, p.Reel)).Distinct())
        {
            Reel reel = reels[position.Reel];
            if (reel.IsPhoenixActive)
            {
                Reel owner = reel.PhoenixVisualOwner;
                if (phoenixOwners.Add(owner))
                    pulses.Add(StartCoroutine(owner.PulsePhoenix(totalDuration)));
            }
            else
                pulses.Add(StartCoroutine(reel.GetSymbol(position.Row).Pulse(totalDuration)));
        }
        var layers = winningLines.Select(line => line.GetComponentsInChildren<LineRenderer>()).ToArray();
        var initialColors = layers.Select(group => group.Select(layer => layer.startColor).ToArray()).ToArray();
        float elapsed = 0f;
        float nextEmission = 0f;
        while (elapsed < totalDuration)
        {
            float t = Mathf.Clamp01(elapsed / paylineDuration);
            for (int i = 0; i < winningLines.Count; i++)
            {
                LineRenderer line = winningLines[i];
                // Use the existing ember pause to finish the fade just before symbols burn.
                float opacity = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(paylineDuration * 0.30f, totalDuration, elapsed));
                for (int layerIndex = 0; layerIndex < layers[i].Length; layerIndex++)
                {
                    Color color = initialColors[i][layerIndex];
                    color.a *= opacity;
                    layers[i][layerIndex].startColor = layers[i][layerIndex].endColor = color;
                }
                if (elapsed < paylineDuration && elapsed >= nextEmission)
                    EmitLineFlames(line, winningLineFires[i], Mathf.Lerp(1f, 0.6f, t),
                        1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.75f, 1f, t)));
            }
            // Emit at a fixed cadence rather than multiplying density by frame rate.
            if (elapsed >= nextEmission)
                nextEmission = elapsed + FlameRandom(0.025f, 0.05f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (LineRenderer line in winningLines)
            foreach (LineRenderer layer in line.GetComponentsInChildren<LineRenderer>())
                layer.enabled = false;
        foreach (ParticleSystem fire in winningLineFires)
            fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        // Restore size and brightness before the burn takes over the sprite material.
        foreach (Coroutine pulse in pulses)
            yield return pulse;
        ClearWinningLines();
    }

    private void EmitLineFlames(LineRenderer line, ParticleSystem fire, float intensity, float emissionAmount)
    {
        for (int segment = 1; segment < line.positionCount; segment++)
        {
            Vector3 start = line.transform.TransformPoint(line.GetPosition(segment - 1));
            Vector3 end = line.transform.TransformPoint(line.GetPosition(segment));
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / 0.12f));
            for (int sample = 0; sample < count; sample++)
            {
                // Thin births across the whole path, without shortening existing flames.
                if (FlameRandom(0f, 1f) >= emissionAmount * 0.5f) continue;
                float fraction = (sample + FlameRandom(0.1f, 0.9f)) / count;
                Vector3 birthplace = Vector3.Lerp(start, end, fraction);
                float flicker = Symbol.FireVariation(birthplace, 29f);
                var particle = new ParticleSystem.EmitParams
                {
                    position = birthplace +
                        new Vector3(FlameRandom(-0.055f, 0.055f), FlameRandom(-0.09f, 0.09f), 0f),
                    velocity = new Vector3(FlameRandom(-0.22f, 0.22f) + .10f * flicker, FlameRandom(0.02f, 0.5f), 0f),
                    startSize = FlameRandom(0.12f, 0.46f) * (0.4f + 0.6f * intensity) * (1f + .15f * flicker),
                    startLifetime = FlameRandom(Symbol.EmberDuration * 0.35f, Symbol.EmberDuration) * PaylineTimingScale * 1.5f,
                    rotation = FlameRandom(-12f, 12f),
                    startColor = new Color(1f, FlameRandom(0.55f, 1f), FlameRandom(0.2f, 0.7f), FlameRandom(.7f, 1f))
                };
                fire.Emit(particle, 1);
                Symbol.EmitFireEmbers(fire, particle.position, 0.2f);
            }
        }
    }

    private void ClearWinningLines()
    {
        foreach (ParticleSystem fire in winningLineFires)
            if (fire != null) Symbol.ReleaseFireEmbers(fire);
        foreach (LineRenderer line in winningLines)
        {
            if (line == null)
                continue;
            line.enabled = false;
            Destroy(line.gameObject);
        }
        winningLines.Clear();
        winningLineFires.Clear();
    }

    private void OnDestroy()
    {
        if (spinButtonSprite != null)
            Destroy(spinButtonSprite);
        if (spinButtonTexture != null)
            Destroy(spinButtonTexture);
        if (winningLineMaterial != null)
            Destroy(winningLineMaterial);
    }

    private IEnumerator RemoveWins(
        List<WinResult> wins)
    {
        HashSet<(int Row, int Reel)>
            positions =
                wins
                    .SelectMany(
                        w => w.Positions)
                    .Select(
                        p => (p.Row, p.Reel))
                    .ToHashSet();

        List<Coroutine> burns = new();
        HashSet<Reel> phoenixReelsBurned = new();

        foreach (var position
                 in positions)
        {
            Reel reel =
                reels[position.Reel];

            // Full Phoenix is an overlay, not
            // three normal symbols.
            if (reel.IsPhoenixActive)
            {
                var owner = reel.PhoenixVisualOwner;
                if (phoenixReelsBurned.Add(owner))
                    burns.Add(StartCoroutine(owner.BurnPhoenix()));
                continue;
            }

            burns.Add(StartCoroutine(
                reel
                    .GetSymbol(
                        position.Row)
                    .Disappear()));
        }

        foreach (Coroutine burn in burns)
        {
            yield return burn;
        }
    }

    private void ShowBoard(
        SymbolType[,] board)
    {
        ConfigurePhoenixGroups(board);
        for (int reel = 0;
             reel < reelCount;
             reel++)
        {
            reels[reel].SetBoard(
                board,
                reel,
                symbolLibrary,
                verticalSpacing);
        }
    }

    private void ConfigurePhoenixGroups(SymbolType[,] board)
    {
        bool FullPhoenix(int column)
        {
            for (int row = 0; row < board.GetLength(0); row++)
                if (board[row, column] != SymbolType.Phoenix) return false;
            return true;
        }
        for (int column = 0; column < reelCount;)
        {
            int start = column++;
            if (FullPhoenix(start))
                while (column < reelCount && FullPhoenix(column)) column++;
            int span = column - start;
            for (int member = start; member < column; member++)
                reels[member].ConfigurePhoenixVisual(reels[start], span, horizontalSpacing);
        }
    }
}

