// Модели из BlenderKit (Assets/Art/BlenderKit): FBX + текстуры в папке Textures (имена как в FBX).
// Скрипт:
//   1) настраивает импорт: карты нормалей помечаются как Normal map, текстуры не больше 2048, без камер и света;
//   2) собирает из FBX готовые модели для игры в Assets/Resources/Models/<папка>/:
//        pallet  — одна лежачая паллета из набора;
//        box     — три разные коробки (мятая, SQ01, коробка с тележки) — игра чередует их;
//        trolley — тележка с коробками целиком (её можно перетащить на сцену для атмосферы).
// Запускается сам один раз; повторить — меню Smena → Собрать модели BlenderKit.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class BlenderKitModels : AssetPostprocessor
{
    const string Root = "Assets/Art/BlenderKit";

    static bool Mine(string p) { return p.Replace('\\', '/').StartsWith(Root); }

    void OnPreprocessTexture()
    {
        if (!Mine(assetPath)) return;
        TextureImporter ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 2048;
        string n = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        if (n.Contains("normal")) ti.textureType = TextureImporterType.NormalMap;
        if (n.Contains("roughness") || n.Contains("metal")) ti.sRGBTexture = false;
    }

    void OnPreprocessModel()
    {
        if (!Mine(assetPath)) return;
        ModelImporter mi = (ModelImporter)assetImporter;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.materialSearch = ModelImporterMaterialSearch.RecursiveUp;   // текстуры ищутся по имени в папке Textures
    }

    // Что из какого файла брать: файл, объект внутри, папка игры, имя модели
    static readonly string[,] Parts =
    {
        { "sklad.fbx",    "WoodenPallet01.007", "pallet",  "pallet_wood" },
        { "sklad.fbx",    "Cardboard box",      "box",     "box_worn" },
        { "sklad.fbx",    "Box_1",              "box",     "box_tape" },
        { "box_sq01.fbx", "Cardboard Box SQ01", "box",     "box_sq01" },
        { "sklad.fbx",    "Trolley with Boxes", "trolley", "trolley" },
    };

    [InitializeOnLoadMethod]
    static void AutoOnce()
    {
        if (EditorPrefs.GetInt("SmenaBlenderKitV", 0) >= 1) return;
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/sklad.fbx") == null) return;   // ещё не импортировано
            EditorPrefs.SetInt("SmenaBlenderKitV", 1);
            Build();
        };
    }

    [MenuItem("Smena/Собрать модели BlenderKit")]
    static void Build()
    {
        int made = 0;
        for (int i = 0; i < Parts.GetLength(0); i++)
            if (Make(Root + "/" + Parts[i, 0], Parts[i, 1], Parts[i, 2], Parts[i, 3])) made++;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BlenderKit] собрано моделей: " + made + " из " + Parts.GetLength(0));
    }

    static bool Make(string fbx, string child, string folder, string name)
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (src == null) { Debug.LogWarning("[BlenderKit] нет файла " + fbx); return false; }
        Transform t = FindDeep(src.transform, child);
        if (t == null) { Debug.LogWarning("[BlenderKit] в " + fbx + " нет объекта " + child); return false; }

        // Копия объекта с его настоящим положением, поворотом и масштабом (как он стоял в файле)
        GameObject wrap = new GameObject(name);
        GameObject copy = Object.Instantiate(t.gameObject);
        copy.name = child;
        copy.transform.SetPositionAndRotation(t.position, t.rotation);
        copy.transform.localScale = t.lossyScale;
        copy.transform.SetParent(wrap.transform, true);

        // Ставим на пол по центру: низ на 0, середина по X и Z в 0
        Bounds b = new Bounds(); bool any = false;
        foreach (Renderer r in copy.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) { Object.DestroyImmediate(wrap); Debug.LogWarning("[BlenderKit] у " + child + " нет мешей"); return false; }
        copy.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

        string dir = "Assets/Resources/Models/" + folder;
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Resources/Models", folder);
        PrefabUtility.SaveAsPrefabAsset(wrap, dir + "/" + name + ".prefab");
        Object.DestroyImmediate(wrap);
        Debug.Log("[BlenderKit] " + child + " → " + dir + "/" + name + ".prefab (" + b.size.ToString("F2") + " м)");
        return true;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            Transform r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
