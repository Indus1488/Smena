// Картинки главного меню (Assets/Resources/UI/Menu): без сжатия и без подгонки под степень двойки,
// чтобы рукописные буквы оставались чёткими и с правильными пропорциями.
using UnityEditor;
using UnityEngine;

public class MenuTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Resources/UI/Menu/")) return;
        TextureImporter ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.maxTextureSize = 2048;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
