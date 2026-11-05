using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Меню Smena → Окрестности:
//  «Собрать в сцене» — строит полосу у центрального склада (дорога, бордюр, газон, заборы, контейнер, кольца,
//     мешки, коробки, столбы) обычными объектами в сцене под «Okrestnosti». Дальше всё двигаешь сам.
//     Сгенерированные меши, текстуры и материалы сохраняются в Assets/Art/Okrestnosti/Generated,
//     готовые куски для перетаскивания — в Assets/Prefabs/Okrestnosti.
//  «Добавить зону травы» / «Добавить зону деревьев» — прямоугольник ScatterArea там, куда смотрит окно Scene.
public static class OkrestnostiTools
{
    const string GenDir = "Assets/Art/Okrestnosti/Generated";
    const string PrefabDir = "Assets/Prefabs/Okrestnosti";

    // Какие куски сохранять префабами: имя объекта в сцене → имя префаба
    static readonly string[,] PrefabNames =
    {
        { "FencePanel", "Сетка_секция_2.5м" },
        { "FenceSlab", "Бетонная_плита_забора" },
        { "SlabPost", "Бетонный_столбик_забора" },
        { "Pole", "Столб_ЛЭП" },
        { "WellRing", "Колодезное_кольцо" },
        { "BigBag", "Биг-бэг" },
        { "OpenCarton", "Коробка_раскрытая" },
        { "FlatCarton", "Картон_плоский" },
        { "CurbStone", "Бордюр_1м" },
        { "ShipContainer", "Морской_контейнер" },
        { "Sheet", "Профнастил_лист" },
        { "Chunk", "Обломок_бетона" },
    };

    [MenuItem("Smena/Окрестности/Собрать в сцене")]
    static void Build()
    {
        GameObject old = GameObject.Find(GrassPatch.RootName);
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("Окрестности",
                "В сцене уже есть «" + GrassPatch.RootName + "». Собрать заново? Твоя расстановка внутри него пропадёт (можно вернуть Ctrl+Z).",
                "Собрать заново", "Отмена")) return;
            Undo.DestroyObjectImmediate(old);
        }

        GameObject root = GrassPatch.BuildForEditor();
        SaveGenerated(root);
        Undo.RegisterCreatedObjectUndo(root, "Окрестности");

        // Старый асфальт Road под щебёнкой больше не нужен (коллайдер оставляем)
        GameObject road = GameObject.Find("Road");
        if (road != null)
        {
            Renderer r = road.GetComponent<Renderer>();
            if (r != null) { Undo.RecordObject(r, "Road"); r.enabled = false; }
        }

        int prefabs = SavePrefabs(root);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[Okrestnosti] собрано в сцене. Префабов: " + prefabs + " (" + PrefabDir + "). Не забудь сохранить сцену (Ctrl+S).");
    }

    [MenuItem("Smena/Окрестности/Добавить зону травы")]
    static void AddGrass() { AddArea("Трава", new Vector3(10f, 1f, 4f), 0.6f, false); }

    [MenuItem("Smena/Окрестности/Добавить зону деревьев")]
    static void AddTrees() { AddArea("Деревья", new Vector3(20f, 1f, 6f), 0.03f, true); }

    static void AddArea(string name, Vector3 size, float density, bool trees)
    {
        GameObject parent = GameObject.Find(GrassPatch.RootName);
        GameObject g = new GameObject(name);
        if (parent != null) g.transform.SetParent(parent.transform, true);
        Vector3 p = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        g.transform.position = new Vector3(p.x, 0f, p.z);
        g.transform.localScale = size;
        ScatterArea a = g.AddComponent<ScatterArea>();
        a.perSquareMeter = density;
        a.castShadows = trees;
        a.alwaysVisible = trees;
        a.clumping = trees ? 0.2f : 0.4f;
        a.sizeRange = trees ? new Vector2(0.7f, 1.3f) : new Vector2(0.8f, 1.25f);
        a.asObjects = trees;
        a.seed = Random.Range(1, 9999);
        TryAssign(a, trees);
        Undo.RegisterCreatedObjectUndo(g, name);
        Selection.activeGameObject = g;
        EditorSceneManager.MarkSceneDirty(g.scene);
    }

    // Сразу подставляем модели из Assets/Art/Vegetation (если они там есть)
    static void TryAssign(ScatterArea a, bool trees)
    {
        string[] names = trees ? new[] { "tree_small_02", "fir_sapling_medium", "fir_sapling" }
                               : new[] { "grass_medium_01", "grass_medium_02", "grass_bermuda_01" };
        List<GameObject> list = new List<GameObject>();
        foreach (string n in names)
        {
            GameObject g = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vegetation/" + n + "/" + n + "_2k.fbx");
            if (g != null) list.Add(g);
        }
        a.models = list.ToArray();
    }

    // Всё, что сгенерировано кодом (меши, текстуры, материалы), — в файлы, иначе после сохранения сцены оно пропадёт
    static void SaveGenerated(GameObject root)
    {
        EnsureFolder(GenDir);
        Dictionary<Object, Object> done = new Dictionary<Object, Object>();
        int n = 0;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh m = mf.sharedMesh;
            if (m == null || AssetDatabase.Contains(m)) continue;
            if (!done.ContainsKey(m)) { AssetDatabase.CreateAsset(m, Unique(GenDir + "/" + m.name + ".asset")); done[m] = m; n++; }
        }
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            foreach (Material mat in r.sharedMaterials)
            {
                if (mat == null || AssetDatabase.Contains(mat) || done.ContainsKey(mat)) continue;
                foreach (string prop in mat.GetTexturePropertyNames())
                {
                    Texture t = mat.GetTexture(prop);
                    if (t == null || AssetDatabase.Contains(t) || done.ContainsKey(t)) continue;
                    t.name = string.IsNullOrEmpty(t.name) ? "Tex" : t.name;
                    AssetDatabase.CreateAsset(t, Unique(GenDir + "/" + t.name + ".asset"));
                    done[t] = t; n++;
                }
                string nm = MaterialName(r, mat);
                mat.name = nm;
                AssetDatabase.CreateAsset(mat, Unique(GenDir + "/" + nm + ".mat"));
                done[mat] = mat; n++;
            }
        AssetDatabase.SaveAssets();
        Debug.Log("[Okrestnosti] сохранено файлов: " + n + " в " + GenDir);
    }

    static string MaterialName(Renderer r, Material m)
    {
        if (!string.IsNullOrEmpty(m.name)) return m.name;
        Transform t = r.transform;
        while (t.parent != null && t.parent.name != GrassPatch.RootName) t = t.parent;
        return "Okr_" + t.name + "_" + r.name;
    }

    static int SavePrefabs(GameObject root)
    {
        EnsureFolder(PrefabDir);
        int n = 0;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < PrefabNames.GetLength(0); i++)
        {
            foreach (Transform t in all)
            {
                if (t.name != PrefabNames[i, 0]) continue;
                GameObject copy = Object.Instantiate(t.gameObject);
                copy.name = PrefabNames[i, 1];
                copy.transform.position = Vector3.zero;
                PrefabUtility.SaveAsPrefabAsset(copy, PrefabDir + "/" + PrefabNames[i, 1] + ".prefab");
                Object.DestroyImmediate(copy);
                n++;
                break;
            }
        }
        return n;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    static string Unique(string path) { return AssetDatabase.GenerateUniqueAssetPath(path); }
}
