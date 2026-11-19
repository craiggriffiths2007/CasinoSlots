using UnityEditor;

public sealed class SweetSymbolImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Resources/Art/SweetSymbols.png") return;
        var texture = (TextureImporter)assetImporter;
        texture.textureType = TextureImporterType.Default;
        texture.npotScale = TextureImporterNPOTScale.None;
        texture.mipmapEnabled = false;
        texture.alphaIsTransparency = true;
        texture.maxTextureSize = 2048;
        texture.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
