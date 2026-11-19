using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SymbolDefinition
{
    public SymbolType type;
    public Sprite sprite;
}

public class SymbolLibrary : MonoBehaviour
{
    [SerializeField]
    private List<SymbolDefinition> symbols = new();
    private readonly Dictionary<Sprite, Sprite> trimmedSprites = new();

    public Sprite GetSprite(SymbolType type)
    {
        foreach (SymbolDefinition symbol in symbols)
        {
            if (symbol.type == type)
                return TrimSprite(symbol.sprite);
        }
        Debug.LogError($"Sprite not found for symbol: {type}");
        return null;
    }

    // Crop the sprite rectangle rather than resampling its artwork. FitSprite
    // then sizes the visible design, not the transparent generation canvas.
    public Sprite TrimSprite(Sprite source)
    {
        if (source == null || !source.texture.isReadable) return source;
        if (trimmedSprites.TryGetValue(source, out Sprite cached)) return cached;
        Texture2D texture = source.texture;
        Color32[] pixels = texture.GetPixels32();
        Rect original = source.rect;
        int left = Mathf.RoundToInt(original.xMin), bottom = Mathf.RoundToInt(original.yMin);
        int right = Mathf.RoundToInt(original.xMax), top = Mathf.RoundToInt(original.yMax);
        int minX = right, minY = top, maxX = left - 1, maxY = bottom - 1;
        for (int y = bottom; y < top; y++)
            for (int x = left; x < right; x++)
            {
                if (pixels[y * texture.width + x].a <= 8) continue;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
        if (maxX < minX || maxY < minY) return source;
        // Two pixels preserve antialiased edges and prevent texture bleeding.
        minX = Mathf.Max(left, minX - 2); minY = Mathf.Max(bottom, minY - 2);
        maxX = Mathf.Min(right - 1, maxX + 2); maxY = Mathf.Min(top - 1, maxY + 2);
        var rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        var trimmed = Sprite.Create(texture, rect, new Vector2(.5f, .5f),
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        trimmed.name = source.name + " (trimmed)";
        trimmedSprites.Add(source, trimmed);
        return trimmed;
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in trimmedSprites.Values) Destroy(sprite);
        trimmedSprites.Clear();
    }
}
