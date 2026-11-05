using System.Collections.Generic;
using UnityEngine;

// Следы Володи во втором акте (18:00–19:30), пока он сам ещё не охотится.
// Раз в 50–90 секунд за спиной у игрока (6–12 м, вне поля зрения) появляется дорожка тёмных мокрых следов,
// ведущая к тому месту, где игрок только что стоял. Через полторы минуты следы медленно высыхают.
// Работает только в хоррор-режиме (GameConfig.horror).
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class VolodyaTraces : MonoBehaviour
{
    class Print { public GameObject go; public Material mat; public float born; }

    readonly List<Print> prints = new List<Print>();
    Transform player;
    Texture2D footTex;
    float next;
    const float Life = 90f, FadeTime = 20f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("VolodyaTraces").AddComponent<VolodyaTraces>();
    }

    void Start()
    {
        next = Time.time + 25f;
    }

    void Update()
    {
        Fade();
        if (!GameConfig.horror || ActController.Act != 2) return;
        if (Time.time < next) return;
        next = Time.time + Random.Range(50f, 90f);

        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            player = p.transform;
        }
        TrySpawnTrail();
    }

    void TrySpawnTrail()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 goal = player.position;
        Vector3 back = -cam.transform.forward; back.y = 0f;
        if (back.sqrMagnitude < 0.01f) return;
        back.Normalize();

        // Ищем точку начала за спиной, на полу и вне поля зрения
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * back;
            Vector3 start = goal + dir * Random.Range(6f, 12f);
            Vector3 hitPos, hitNormal;
            if (!Floor(start, goal.y, out hitPos, out hitNormal)) continue;
            if (Visible(cam, hitPos)) continue;
            // Между началом и игроком не должно быть стены
            if (Physics.Linecast(hitPos + Vector3.up * 0.5f, goal + Vector3.up * 0.5f, out RaycastHit wall)
                && wall.transform.root != player.root) continue;
            SpawnTrail(hitPos, goal);
            return;
        }
    }

    bool Floor(Vector3 p, float nearY, out Vector3 pos, out Vector3 normal)
    {
        pos = normal = Vector3.zero;
        RaycastHit hit;
        if (!Physics.Raycast(new Vector3(p.x, nearY + 1.2f, p.z), Vector3.down, out hit, 3.5f)) return false;
        if (hit.normal.y < 0.9f) return false;
        if (Physics.CheckSphere(hit.point + Vector3.up * 0.5f, 0.3f)) return false;   // внутри мебели/стены
        pos = hit.point; normal = hit.normal;
        return true;
    }

    static bool Visible(Camera cam, Vector3 p)
    {
        Vector3 v = cam.WorldToViewportPoint(p);
        return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
    }

    void SpawnTrail(Vector3 from, Vector3 to)
    {
        if (footTex == null) footTex = MakeFootTexture();
        Vector3 d = to - from; d.y = 0f;
        float len = d.magnitude;
        Vector3 dir = d / Mathf.Max(len, 0.01f);
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        int steps = Mathf.Clamp(Mathf.FloorToInt((len - 1.2f) / 0.62f), 3, 14);   // не доходят до самого игрока
        for (int i = 0; i < steps; i++)
        {
            Vector3 p = from + dir * (i * 0.62f) + side * ((i % 2 == 0) ? 0.13f : -0.13f);
            Vector3 hp, hn;
            if (!Floor(p, from.y, out hp, out hn)) continue;
            MakePrint(hp + hn * 0.004f, dir, i % 2 == 0, Time.time);
        }
    }

    void MakePrint(Vector3 pos, Vector3 dir, bool right, float born)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = "Footprint";
        q.transform.position = pos;
        q.transform.rotation = Quaternion.LookRotation(Vector3.down, dir) * Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
        q.transform.localScale = new Vector3(right ? -0.13f : 0.13f, 0.32f, 1f);

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        Material m = new Material(sh);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 2990;
        m.SetTexture("_BaseMap", footTex);
        m.SetColor("_BaseColor", new Color(0.05f, 0.035f, 0.03f, 0.85f));
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        prints.Add(new Print { go = q, mat = m, born = born });
    }

    void Fade()
    {
        for (int i = prints.Count - 1; i >= 0; i--)
        {
            Print p = prints[i];
            float age = Time.time - p.born;
            if (p.go == null || age > Life) { if (p.go != null) Destroy(p.go); prints.RemoveAt(i); continue; }
            float a = age < Life - FadeTime ? 0.85f : 0.85f * (Life - age) / FadeTime;
            p.mat.SetColor("_BaseColor", new Color(0.05f, 0.035f, 0.03f, a));
        }
    }

    // Босой след: стопа (вытянутый овал, уже в середине) + пять пальцев. Пальцы сверху (+V).
    static Texture2D MakeFootTexture()
    {
        const int W = 48, H = 120;
        Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                float a = 0f;
                // пятка
                a = Mathf.Max(a, Blob(u, v, 0.5f, 0.16f, 0.30f, 0.14f));
                // свод стопы (узкий, смещён к внешней стороне)
                a = Mathf.Max(a, Blob(u, v, 0.58f, 0.42f, 0.22f, 0.18f) * 0.85f);
                // подушечка
                a = Mathf.Max(a, Blob(u, v, 0.5f, 0.68f, 0.40f, 0.12f));
                // пальцы
                float[] tx = { 0.22f, 0.40f, 0.56f, 0.70f, 0.82f };
                float[] ty = { 0.88f, 0.92f, 0.90f, 0.86f, 0.81f };
                float[] tr = { 0.10f, 0.075f, 0.07f, 0.065f, 0.06f };
                for (int k = 0; k < 5; k++) a = Mathf.Max(a, Blob(u, v, tx[k], ty[k], tr[k], tr[k] * 0.55f));
                // неровный мокрый край
                a *= 0.75f + 0.25f * Mathf.PerlinNoise(u * 9f, v * 9f);
                px[y * W + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    static float Blob(float u, float v, float cx, float cy, float rx, float ry)
    {
        float dx = (u - cx) / (rx * 0.5f), dy = (v - cy) / ry;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01((1.1f - d) * 4f);
    }
}
