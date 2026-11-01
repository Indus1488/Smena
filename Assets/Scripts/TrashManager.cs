using System.Collections.Generic;
using UnityEngine;

// Мусор на складе:
// - при старте игры стартовый мусор раскидывается по полу главного склада в случайные места;
// - каждый раз, когда коробка становится готовой, появляется ещё 1–2 куска мусора.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class TrashManager : MonoBehaviour
{
    public static TrashManager Instance;

    // Пол главного склада (совпадает с размерами в построителе)
    const float FloorY = 0.9f, HalfW = 3.0f, Depth = 12.0f;

    Transform warehouse;
    readonly List<Material> mats = new List<Material>();
    readonly System.Random rnd = new System.Random();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Instance = new GameObject("TrashManager").AddComponent<TrashManager>();
    }

    // Вызывается, когда коробка стала готовой
    public static void OnBoxReady()
    {
        if (Instance == null) return;
        int n = 1 + Instance.rnd.Next(GameConfig.trashPerBoxMax);
        for (int i = 0; i < n; i++) Instance.SpawnOne();
    }

    void Start()
    {
        GameObject w = GameObject.Find("Warehouse");
        warehouse = w != null ? w.transform : null;

        // Запоминаем материалы стартового мусора и раскидываем его
        TrashPiece[] all = Object.FindObjectsByType<TrashPiece>(FindObjectsSortMode.None);
        foreach (TrashPiece t in all)
        {
            Renderer r = t.GetComponent<Renderer>();
            if (r != null && !mats.Contains(r.sharedMaterial)) mats.Add(r.sharedMaterial);
            Vector3 p;
            if (RandomFloorPoint(t.transform.lossyScale.y, out p)) t.transform.position = p;
        }
    }

    void SpawnOne()
    {
        if (Object.FindObjectsByType<TrashPiece>(FindObjectsSortMode.None).Length >= GameConfig.trashMaxOnFloor) return;

        float s = 0.07f + (float)rnd.NextDouble() * 0.1f;
        Vector3 scale = new Vector3(s * 1.4f, s, s);
        Vector3 pos;
        if (!RandomFloorPoint(s, out pos)) return;

        GameObject t = GameObject.CreatePrimitive(PrimitiveType.Cube);
        t.name = "Trash";
        if (warehouse != null) t.transform.SetParent(warehouse, true);
        t.transform.position = pos;
        t.transform.rotation = Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f);
        t.transform.localScale = scale;
        if (mats.Count > 0) t.GetComponent<Renderer>().sharedMaterial = mats[rnd.Next(mats.Count)];

        // Увеличенный коллайдер (0.4 м), чтобы в мусор было легко попасть взглядом
        BoxCollider bc = t.GetComponent<BoxCollider>();
        Vector3 ls = t.transform.lossyScale;
        bc.size = new Vector3(0.4f / ls.x, 0.4f / ls.y, 0.4f / ls.z);
        bc.isTrigger = true;   // через мусор можно ходить и ставить паллеты

        TrashPiece.nextIsExtra = true;
        t.AddComponent<TrashPiece>();
    }

    // Случайная свободная точка на полу склада: луч сверху должен попасть именно в пол, а не в коробку
    bool RandomFloorPoint(float height, out Vector3 point)
    {
        point = Vector3.zero;
        if (warehouse == null) return false;
        for (int tries = 0; tries < 30; tries++)
        {
            float x = -HalfW + 0.35f + (float)rnd.NextDouble() * (2f * HalfW - 0.7f);
            float z = 0.6f + (float)rnd.NextDouble() * (Depth - 1.2f);
            Vector3 top = warehouse.TransformPoint(new Vector3(x, FloorY + 1.8f, z));
            RaycastHit hit;
            if (!Physics.Raycast(top, Vector3.down, out hit, 3f, ~0, QueryTriggerInteraction.Ignore)) continue;
            float floorWorldY = warehouse.TransformPoint(new Vector3(0, FloorY, 0)).y;
            if (hit.normal.y < 0.9f || Mathf.Abs(hit.point.y - floorWorldY) > 0.05f) continue;
            point = hit.point + Vector3.up * (height / 2f);
            return true;
        }
        return false;
    }
}
