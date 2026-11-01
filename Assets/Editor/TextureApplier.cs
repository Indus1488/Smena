// «СМЕНА» — накладывает текстуры на материалы склада.
// 1. Положите папку Textures (16 файлов .png) в Assets/Textures/Smena/.
// 2. Положите этот файл в Assets/Editor/.
// 3. Меню Unity: Smena → Применить текстуры.
// После пересборки склада («Построить склад») этот пункт нужно нажать ещё раз:
// построитель перекрашивает материалы обратно в простые цвета.
using UnityEngine;
using UnityEditor;

public static class TextureApplier
{
    const string TexFolder = "Assets/Textures/Smena";
    const string MatFolder = "Assets/Materials/Smena";

    // материал -> текстура, размер одного повтора в метрах, оттенок (умножается на текстуру)
    struct Map
    {
        public string mat, tex;
        public float meters;
        public Color tint;
        public Map(string m, string t, float s, Color c) { mat = m; tex = t; meters = s; tint = c; }
    }

    static Color W(float v) { return new Color(v, v, v); }

    static readonly Map[] maps =
    {
        new Map("Concrete",      "concrete",     2.0f, W(1.00f)),
        new Map("ConcreteDark",  "concrete",     2.0f, W(0.60f)),
        new Map("Plinth",        "concrete",     1.5f, W(0.85f)),
        new Map("OSB",           "osb",          1.2f, W(1.00f)),
        new Map("DoorOpen",      "metal_door",   2.2f, W(1.00f)),
        new Map("DoorClosed",    "rust",         1.4f, W(1.00f)),
        new Map("Turquoise",     "turquoise",    1.2f, W(1.00f)),
        new Map("Rust",          "rust",         0.8f, W(0.85f)),
        new Map("Gate",          "rust",         2.0f, W(0.55f)),
        new Map("GantryRust",    "rust",         1.0f, W(0.80f)),
        new Map("Wood",          "pallet_wood",  1.0f, W(0.55f)),
        new Map("BoardBrown",    "pallet_wood",  1.0f, W(0.45f)),
        new Map("PalletWood",    "pallet_wood",  0.8f, W(1.00f)),
        new Map("Cardboard",     "cardboard",    0.6f, W(1.00f)),
        new Map("CardboardDark", "cardboard",    0.6f, W(0.82f)),
        new Map("FilmWhite",     "film",         1.0f, W(1.00f)),
        new Map("Dirt",          "dirt",         3.0f, W(1.00f)),
        new Map("Asphalt",       "asphalt",      3.0f, W(1.00f)),
        new Map("Tile",          "tile",         1.6f, W(1.00f)),
        new Map("RedDoor",       "rust",         1.4f, new Color(1.0f, 0.55f, 0.5f)),
        new Map("GrayDoor",      "metal_door",   2.2f, W(0.85f)),
        new Map("StripeCream",   "facade_cream", 6.0f, W(1.00f)),
        new Map("StripeDark",    "facade_dark",  6.0f, W(1.00f)),
        new Map("FactoryBlue",   "factory",      6.0f, W(1.00f)),
        new Map("Monster",       "monster",      0.5f, W(1.00f)),
    };

    [MenuItem("Smena/Применить текстуры")]
    public static void Apply()
    {
        int done = 0, missing = 0;
        foreach (Map m in maps)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/" + m.mat + ".mat");
            Texture2D tex = LoadTexture(m.tex);
            if (mat == null) continue;                      // такого материала нет — пропускаем
            if (tex == null) { missing++; continue; }

            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(1f / m.meters, 1f / m.meters);   // повтор в метрах (см. WorldUV.cs)
            mat.color = m.tint;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(mat);
            done++;
        }
        AssetDatabase.SaveAssets();

        if (missing > 0)
            Debug.LogWarning("Не найдено текстур: " + missing + ". Положите файлы .png в " + TexFolder);
        Debug.Log("Текстуры наложены на материалов: " + done + ". В редакторе они растянуты, правильно видно в Play.");
    }

    // Загружает текстуру и выставляет ретро-настройки импорта: чёткие пиксели, без сглаживания
    static Texture2D LoadTexture(string name)
    {
        string path = TexFolder + "/" + name + ".png";
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return null;

        bool changed = false;
        if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; changed = true; }
        if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; changed = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
        if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        if (changed) ti.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
