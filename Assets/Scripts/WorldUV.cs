using System.Collections.Generic;
using UnityEngine;

// Чтобы текстуры не растягивались на больших кубах (стена 12 м и коробка 40 см выглядят одинаково плотно),
// при запуске игры у каждого куба и плоскости пересчитываются текстурные координаты «в метрах».
// Материал задаёт, сколько метров занимает один повтор текстуры (это делает Smena → Применить текстуры).
// Новые объекты (коробки селлеров, паллеты) подхватываются автоматически раз в секунду.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class WorldUV : MonoBehaviour
{
    readonly HashSet<MeshFilter> done = new HashSet<MeshFilter>();
    readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
    float next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("WorldUV").AddComponent<WorldUV>();
    }

    void Start()
    {
        Scan();
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 1f;
        Scan();
    }

    void Scan()
    {
        MeshFilter[] all = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            MeshFilter mf = all[i];
            if (done.Contains(mf)) continue;
            done.Add(mf);

            Mesh src = mf.sharedMesh;
            if (src == null) continue;
            bool cube = src.name == "Cube";
            bool plane = src.name == "Plane";
            if (!cube && !plane) continue;

            Vector3 s = mf.transform.lossyScale;
            s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            string key = (cube ? "c" : "p") + Mathf.Round(s.x * 100f) + "_" + Mathf.Round(s.y * 100f) + "_" + Mathf.Round(s.z * 100f);

            Mesh m;
            if (!cache.TryGetValue(key, out m))
            {
                m = Remap(src, s, cube);
                cache[key] = m;
            }
            mf.sharedMesh = m;
        }
    }

    // Новая копия меша с координатами текстуры в метрах
    static Mesh Remap(Mesh src, Vector3 s, bool cube)
    {
        Mesh m = Instantiate(src);
        m.name = src.name + "_WorldUV";
        Vector3[] v = m.vertices;
        Vector3[] n = m.normals;
        Vector2[] uv = new Vector2[v.Length];

        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = new Vector3(v[i].x * s.x, v[i].y * s.y, v[i].z * s.z);
            if (!cube)
            {
                uv[i] = new Vector2(p.x, p.z);
                continue;
            }
            Vector3 a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
            if (a.x >= a.y && a.x >= a.z) uv[i] = new Vector2(p.z * Mathf.Sign(n[i].x), p.y);      // боковые грани
            else if (a.y >= a.z) uv[i] = new Vector2(p.x, p.z * Mathf.Sign(n[i].y));                // верх и низ
            else uv[i] = new Vector2(-p.x * Mathf.Sign(n[i].z), p.y);                               // перед и зад
        }
        m.uv = uv;
        return m;
    }
}
