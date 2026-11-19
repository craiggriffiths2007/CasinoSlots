using UnityEditor;
using UnityEngine;

// Keep the 5x4 atlas intact: resizing to a square or mipmaps would bleed frames.
public sealed class FlameFlipbookImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Resources/FlameFlipbook20.png") return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
