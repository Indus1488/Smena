using System.Collections.Generic;
using UnityEngine;

// Двор как на настоящей приёмке: поверх грунта (HD-текстура Ground022) лежат
// тёмные мокрые пятна грязи, колеи от машин, ржавые люки и куски старого разбитого асфальта.
// Картинки — Assets/Resources/Decals/Ground. Раскладка одна и та же каждый запуск (seed),
// только на открытой земле (объект Dirt), не на асфальте и не внутри складов.
// На «Низкой» графике (без текстур) не показываются.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class GroundDecals : MonoBehaviour
{
    // Границы двора (как невидимые стены в построителе)
    const float MinX = -35f, MaxX = 43f, MinZ = -17.5f, MaxZ = 39f;

    readonly List<GameObject> all = new List<GameObject>();
    int lastMode = -1;
    float next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("GroundDecals").AddComponent<GroundDecals>();
    }

    void Start()
    {
        System.Random r = new System.Random(1704);
        Place(r, "mud_0", 6, 2.5f, 5f, 0.9f, 1f);
        Place(r, "mud_1", 5, 2.5f, 5f, 0.9f, 1f);
        Place(r, "mud_2", 5, 2f, 4f, 0.9f, 1f);
        Place(r, "rut", 10, 3f, 5f, 0.35f, 1f, true);
        Place(r, "asphalt_0", 3, 2f, 3.5f, 0.6f, 0.55f);
        Place(r, "asphalt_1", 3, 1.5f, 3f, 0.6f, 0.55f);
        Place(r, "asphalt_2", 3, 1.5f, 3f, 0.6f, 0.55f);
        Place(r, "asphalt_3", 3, 1.5f, 3f, 0.6f, 0.55f);
        Place(r, "manhole", 3, 0.85f, 0.95f, 1f, 1f);
        Apply();
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.5f;
        Apply();
    }

    void Apply()
    {
        int mode = VisualOptions.TextureMode;
        if (mode == lastMode) return;
        lastMode = mode;
        foreach (GameObject g in all) if (g != null) g.SetActive(mode >= 1);
    }

    // count штук картинки name, размер от min до max (метры), aspect — высота/ширина, tint — яркость,
    // along — вытянуть вдоль одной из осей двора (колеи идут вдоль проездов)
    void Place(System.Random r, string name, int count, float min, float max, float aspect, float tint, bool along = false)
    {
        Texture2D tex = Resources.Load<Texture2D>("Decals/Ground/" + name);
        if (tex == null) return;
        Material m = MakeMaterial(tex, tint);
        float texAspect = (float)tex.height / tex.width;
        int placed = 0;
        for (int tries = 0; tries < count * 25 && placed < count; tries++)
        {
            float x = Mathf.Lerp(MinX, MaxX, (float)r.NextDouble());
            float z = Mathf.Lerp(MinZ, MaxZ, (float)r.NextDouble());
            float size = Mathf.Lerp(min, max, (float)r.NextDouble());
            float yaw = along ? (r.Next(2) * 90f + (float)(r.NextDouble() * 16.0 - 8.0)) : (float)(r.NextDouble() * 360.0);
            if (!OnOpenGround(x, z, size * 0.5f, out float y)) continue;

            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "GroundDecal_" + name;
            q.transform.SetParent(transform, false);
            q.transform.position = new Vector3(x, y + 0.012f + placed * 0.0006f, z);   // чуть над землёй, чтобы не мерцало
            q.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            float h = name.StartsWith("asphalt") ? texAspect : aspect;
            q.transform.localScale = new Vector3(size, size * h, 1f);
            Renderer rd = q.GetComponent<Renderer>();
            rd.sharedMaterial = m;
            rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            all.Add(q);
            placed++;
        }
    }

    // Под точкой и по краям пятна — именно грунт двора (объект Dirt), не асфальт, не пол склада, не стена
    static bool OnOpenGround(float x, float z, float radius, out float y)
    {
        y = 0f;
        Vector3[] pts = { Vector3.zero, new Vector3(radius, 0, 0), new Vector3(-radius, 0, 0), new Vector3(0, 0, radius), new Vector3(0, 0, -radius) };
        for (int i = 0; i < pts.Length; i++)
        {
            RaycastHit hit;
            Vector3 from = new Vector3(x + pts[i].x, 30f, z + pts[i].z);
            if (!Physics.Raycast(from, Vector3.down, out hit, 60f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.name != "Dirt") return false;
            if (i == 0) y = hit.point.y;
        }
        return true;
    }

    static Material MakeMaterial(Texture2D tex, float tint)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 2990;
        m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        m.SetColor("_BaseColor", new Color(tint, tint, tint, 1f));
        m.SetFloat("_Smoothness", 0.25f);   // мокрая грязь чуть блестит
        return m;
    }
}
