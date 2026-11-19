using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Reel : MonoBehaviour
{
    private Symbol[] symbols;
    public event System.Action SymbolLanded;

    private GameObject phoenixObject;
    private SpriteRenderer phoenixRenderer;

    private bool phoenixActive;

    public bool IsPhoenixActive => phoenixActive;
    private Reel phoenixVisualOwner;
    private int phoenixSpan = 1;
    private float phoenixSpacing = 2f;
    private static Sprite doublePhoenixSprite, triplePhoenixSprite;
    public Reel PhoenixVisualOwner => phoenixVisualOwner != null ? phoenixVisualOwner : this;
    private Vector3 PhoenixPosition => Vector3.right * ((phoenixSpan - 1) * phoenixSpacing * .5f);

    public void ConfigurePhoenixVisual(Reel owner, int span, float spacing)
    {
        phoenixVisualOwner = owner;
        phoenixSpan = Mathf.Max(1, span);
        phoenixSpacing = spacing;
    }

    public Symbol GetSymbol(int row)
    {
        return symbols[row];
    }

    public void Initialise(
        Symbol symbolPrefab,
        int rowCount,
        float verticalSpacing)
    {
        symbols = new Symbol[rowCount];

        for (int row = 0; row < rowCount; row++)
        {
            Symbol symbol =
                Instantiate(symbolPrefab, transform);

            symbol.SetRowPosition(
                row,
                rowCount,
                verticalSpacing);

            symbols[row] = symbol;
        }

        CreatePhoenixRenderer();
    }

    public void SetBoard(
        SymbolType[,] board,
        int reelIndex,
        SymbolLibrary symbolLibrary,
        float verticalSpacing)
    {
        if (IsFullPhoenix(board, reelIndex))
        {
            ShowPhoenixInstant(
                symbolLibrary,
                verticalSpacing);

            return;
        }

        HidePhoenix();

        for (int row = 0; row < symbols.Length; row++)
        {
            SymbolType type =
                board[row, reelIndex];

            Sprite sprite =
                symbolLibrary.GetSprite(type);

            symbols[row].SetSymbol(
                type,
                sprite);

            symbols[row].SetRowPosition(
                row,
                symbols.Length,
                verticalSpacing);

            symbols[row].SetVisible(true);
        }
    }

    // Every piece has its own flight and landing, ordered bottom to top.
    private const float PieceDelay = 0.16f;
    private const float LandingDuration = 0.12f;

    private IEnumerator Fall(Symbol symbol, Vector3 start, Vector3 target,
        float duration, float delay)
    {
        symbol.transform.localPosition = start;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            symbol.transform.localPosition = Vector3.Lerp(start, target, t * t);
            if (t >= 1f) SymbolLanded?.Invoke();
            yield return null;
        }

        // A small rebound after impact, with an exact final row position.
        elapsed = 0f;
        float bounce = Mathf.Min(0.12f, Mathf.Abs(start.y - target.y) * 0.06f);
        while (elapsed < LandingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / LandingDuration);
            symbol.transform.localPosition = target +
                Vector3.up * (Mathf.Sin(t * Mathf.PI) * bounce * (1f - t));
            yield return null;
        }
        symbol.transform.localPosition = target;
    }

    private IEnumerator FallAway(Transform piece, float distance, float duration, float delay)
    {
        Vector3 start = piece.localPosition;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            piece.localPosition = start + Vector3.down * (distance * t * t);
            yield return null;
        }
        piece.localPosition = start + Vector3.down * distance;
    }

    public IEnumerator DropOut(float distance, float duration, float delay)
    {
        if (phoenixActive)
        {
            yield return FallAway(phoenixObject.transform, distance, duration, delay);
            yield break;
        }

        List<Coroutine> flights = new();
        for (int row = symbols.Length - 1; row >= 0; row--)
            flights.Add(StartCoroutine(FallAway(symbols[row].transform, distance,
                duration, delay + (symbols.Length - 1 - row) * 0.055f)));
        foreach (Coroutine flight in flights)
            yield return flight;
    }

    public IEnumerator DropIn(float distance, float duration, float delay, float verticalSpacing)
    {
        HidePhoenix();
        List<Coroutine> flights = new();
        // Feed each piece from the same point above the top row.
        Vector3 entrance = GetRowPosition(0, verticalSpacing) + Vector3.up * distance;
        float longestFall = entrance.y - GetRowPosition(symbols.Length - 1, verticalSpacing).y;
        for (int row = symbols.Length - 1; row >= 0; row--)
        {
            Symbol symbol = symbols[row];
            symbol.SetVisible(true);
            Vector3 target = GetRowPosition(row, verticalSpacing);
            float flightTime = duration * Mathf.Sqrt((entrance.y - target.y) / longestFall);
            flights.Add(StartCoroutine(Fall(symbol, entrance, target, flightTime,
                delay + (symbols.Length - 1 - row) * PieceDelay)));
        }
        foreach (Coroutine flight in flights)
            yield return flight;
    }

    public IEnumerator CascadeToBoard(
        SymbolType[,] boardBefore,
        SymbolType[,] boardAfter,
        int reelIndex,
        List<SymbolPosition> removedPositions,
        SymbolLibrary symbolLibrary,
        float verticalSpacing,
        bool becomesPhoenix,
        bool expiresPhoenix)
    {
        bool[] removed =
            new bool[symbols.Length];

        foreach (SymbolPosition position in removedPositions)
        {
            if (position.Reel == reelIndex)
            {
                removed[position.Row] = true;
            }
        }

        if (expiresPhoenix)
        {
            HidePhoenix();

            for (int row = 0; row < removed.Length; row++)
            {
                removed[row] = true;
            }
        }

        int removedCount = 0;

        for (int row = 0; row < removed.Length; row++)
        {
            if (removed[row])
                removedCount++;
        }

        if (becomesPhoenix)
        {
            foreach (Symbol symbol in symbols)
            {
                symbol.SetVisible(false);
            }

            yield return ShowPhoenixRise(
                symbolLibrary,
                verticalSpacing);

            yield break;
        }

        if (removedCount == 0)
        {
            SetBoard(
                boardAfter,
                reelIndex,
                symbolLibrary,
                verticalSpacing);

            yield break;
        }

        // Keep each survivor's original renderer and sprite throughout the fall.
        // Build a new row lookup so moving one symbol cannot overwrite another.
        Symbol[] nextSymbols = new Symbol[symbols.Length];
        List<Symbol> survivors = new();
        List<Vector3> survivorStarts = new();
        List<Vector3> survivorTargets = new();
        int incomingRow = 0;
        int survivorRow = removedCount;

        for (int row = 0; row < symbols.Length; row++)
        {
            Symbol symbol = symbols[row];

            if (removed[row])
            {
                symbol.SetVisible(false);
                nextSymbols[incomingRow++] = symbol;
                continue;
            }

            nextSymbols[survivorRow] = symbol;
            survivors.Add(symbol);
            survivorStarts.Add(symbol.transform.localPosition);
            survivorTargets.Add(GetRowPosition(survivorRow, verticalSpacing));
            survivorRow++;
        }

        symbols = nextSymbols;

        // Survivors and replacements fall together; nothing waits in mid-air.
        List<Coroutine> flights = new();
        const float gravity = 70f;
        for (int i = 0; i < survivors.Count; i++)
        {
            float distance = survivorStarts[i].y - survivorTargets[i].y;
            if (distance <= 0.001f)
                continue;
            flights.Add(StartCoroutine(Fall(survivors[i], survivorStarts[i],
                survivorTargets[i], Mathf.Sqrt(2f * distance / gravity), 0f)));
        }

        Vector3 entrance = GetRowPosition(0, verticalSpacing) + Vector3.up * (verticalSpacing * 1.5f);
        for (int row = removedCount - 1; row >= 0; row--)
        {
            Symbol symbol = symbols[row];
            SymbolType type = boardAfter[row, reelIndex];
            symbol.transform.localPosition = entrance;
            symbol.SetSymbol(type, symbolLibrary.GetSprite(type));
            Vector3 target = GetRowPosition(row, verticalSpacing);
            float duration = Mathf.Sqrt(2f * (entrance.y - target.y) / gravity);
            flights.Add(StartCoroutine(Fall(symbol, entrance, target, duration,
                reelIndex * 0.055f + (removedCount - 1 - row) * PieceDelay)));
        }
        foreach (Coroutine flight in flights)
            yield return flight;
    }

    public IEnumerator BurnPhoenix()
    {
        if (!phoenixActive)
            yield break;
        yield return Symbol.BurnRenderer(phoenixRenderer, isWild: true);
        HidePhoenix();
    }

    public IEnumerator PulsePhoenix(float duration)
    {
        if (!phoenixActive) yield break;
        yield return Symbol.PulseRenderer(phoenixRenderer, phoenixObject.transform, duration);
    }
    public void HidePhoenix()
    {
        if (phoenixRenderer != null)
        {
            phoenixRenderer.enabled = false;
            Symbol.SetWildFire(phoenixRenderer, false);
        }

        phoenixActive = false;
    }

    private void CreatePhoenixRenderer()
    {
        phoenixObject =
            new GameObject(
                "Phoenix Full Reel");

        phoenixObject
            .transform
            .SetParent(
                transform,
                false);

        phoenixRenderer =
            phoenixObject
                .AddComponent<SpriteRenderer>();

        phoenixRenderer.maskInteraction =
            SpriteMaskInteraction
                .VisibleInsideMask;

        phoenixRenderer.sortingOrder = 10;

        if (symbols.Length > 0)
        {
            phoenixRenderer.sharedMaterial =
                symbols[0]
                    .Renderer
                    .sharedMaterial;
        }

        phoenixRenderer.enabled = false;
    }

    private bool IsFullPhoenix(
        SymbolType[,] board,
        int reelIndex)
    {
        for (int row = 0;
             row < symbols.Length;
             row++)
        {
            if (board[row, reelIndex] !=
                SymbolType.Phoenix)
            {
                return false;
            }
        }

        return true;
    }

    private void ShowPhoenixInstant(
        SymbolLibrary symbolLibrary,
        float verticalSpacing)
    {
        foreach (Symbol symbol in symbols)
        {
            symbol.SetVisible(false);
        }

        phoenixActive = true;
        if (PhoenixVisualOwner != this)
        {
            phoenixRenderer.enabled = false;
            Symbol.SetWildFire(phoenixRenderer, false);
            return;
        }

        Sprite sprite = GetPhoenixSprite(symbolLibrary);

        phoenixRenderer.sprite = sprite;

        phoenixObject
            .transform
            .localPosition =
                PhoenixPosition;

        phoenixObject
            .transform
            .localScale =
                CalculatePhoenixScale(
                    sprite,
                    verticalSpacing);

        phoenixRenderer.color =
            Color.white;

        phoenixRenderer.enabled =
            true;

        phoenixActive = true;
        Symbol.SetWildFire(phoenixRenderer, true);
    }

    private IEnumerator ShowPhoenixRise(
        SymbolLibrary symbolLibrary,
        float verticalSpacing)
    {
        foreach (Symbol symbol in symbols) symbol.SetVisible(false);
        phoenixActive = true;
        if (PhoenixVisualOwner != this)
        {
            phoenixRenderer.enabled = false;
            Symbol.SetWildFire(phoenixRenderer, false);
            yield break;
        }
        Sprite sprite = GetPhoenixSprite(symbolLibrary);
        phoenixRenderer.sprite = sprite;
        phoenixObject.transform.localPosition = PhoenixPosition;
        phoenixObject.transform.localScale = CalculatePhoenixScale(sprite, verticalSpacing);
        phoenixRenderer.color = Color.white;
        phoenixRenderer.enabled = true;
        phoenixActive = true;

        // Keep the full-size sprite stationary while the burn edge reveals it upward.
        yield return Symbol.BurnRenderer(phoenixRenderer, Symbol.BurnDuration, reveal: true, isWild: true);
        Symbol.SetWildFire(phoenixRenderer, true);
    }
    private Sprite GetPhoenixSprite(SymbolLibrary library)
    {
        if (phoenixSpan == 1) return library.GetSprite(SymbolType.Phoenix);
        if (doublePhoenixSprite == null)
            doublePhoenixSprite = library.TrimSprite(Resources.Load<Sprite>("PhoenixArt/phoenix-double"));
        if (triplePhoenixSprite == null)
            triplePhoenixSprite = library.TrimSprite(Resources.Load<Sprite>("PhoenixArt/phoenix-triple"));
        var sprite = phoenixSpan == 2 ? doublePhoenixSprite : triplePhoenixSprite;
        if (sprite == null) throw new System.InvalidOperationException("Missing grouped Phoenix artwork.");
        return sprite;
    }

    private Vector3 GetRowPosition(
        int row,
        float verticalSpacing)
    {
        float startY =
            ((symbols.Length - 1) *
             verticalSpacing) / 2f;

        return new Vector3(
            0f,
            startY -
            row * verticalSpacing,
            0f);
    }

    private Vector3 CalculatePhoenixScale(
        Sprite sprite,
        float verticalSpacing)
    {
        if (sprite == null)
            return Vector3.one;

        Vector2 size =
            sprite.bounds.size;

        float maxWidth = phoenixSpan == 1 ? 2.2f : phoenixSpan * phoenixSpacing - .12f;

        float maxHeight =
            ((symbols.Length - 1) *
             verticalSpacing) +
            1.8f;

        float scale =
            Mathf.Min(
                maxWidth / size.x,
                maxHeight / size.y);

        return new Vector3(
            scale,
            scale,
            1f);
    }
}
