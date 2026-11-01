// «СМЕНА» — настройки импорта HD-текстур из Assets/Resources/HD:
// файлы *_normal становятся картами нормалей, всё до 1024 пикселей, гладкая фильтрация.
using UnityEditor;

public class HDTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/HD/")) return;
        TextureImporter ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 1024;
        ti.mipmapEnabled = true;
        ti.filterMode = UnityEngine.FilterMode.Bilinear;
        ti.wrapMode = UnityEngine.TextureWrapMode.Repeat;
        if (assetPath.EndsWith("_normal.png") || assetPath.EndsWith("_normal.jpg"))
            ti.textureType = TextureImporterType.NormalMap;
    }
}
