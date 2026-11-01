using UnityEngine;

// Надписи на стенах:
// - фиолетовый баннер на правой стене склада получает текст (Resources/Decals/banner);
// - когда становится совсем темно (после 18:00, когда выдали фонарик), на левой наружной стене склада,
//   в проходе к соседним складам, появляется красная надпись (Resources/Decals/scary).
//   Появляется, пока игрок на эту стену не смотрит, и видна только при свете фонарика.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class Decals : MonoBehaviour
{
    // Размеры склада (как в построителе)
    const float OW = 3.3f, FloorY = 0.9f;

    GameObject scary;
    bool shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("Decals").AddComponent<Decals>();
    }

    void Start()
    {
        // ---- Баннер ----
        GameObject banner = GameObject.Find("Banner");
        Texture2D bt = Resources.Load<Texture2D>("Decals/banner");
        if (banner != null && bt != null)
        {
            Transform b = banner.transform;
            Vector3 n = b.right;                         // баннер висит на правой стене, наружу смотрит +X
            Vector3 s = b.lossyScale;
            GameObject q = Quad("BannerText", b.position + n * (s.x / 2f + 0.006f), n, new Vector2(s.z, s.y), bt, false);
            q.transform.SetParent(b.parent, true);
            GameObject stripe = GameObject.Find("BannerStripe");
            if (stripe != null) stripe.SetActive(false);     // полоса уже нарисована на картинке
        }

        // ---- Страшная надпись (пока спрятана) ----
        GameObject w = GameObject.Find("Warehouse");
        Texture2D st = Resources.Load<Texture2D>("Decals/scary");
        if (w != null && st != null)
        {
            Vector3 pos = w.transform.TransformPoint(new Vector3(-OW - 0.012f, FloorY + 1.7f, 9.3f));
            scary = Quad("ScaryWord", pos, -w.transform.right, new Vector2(4.4f, 1.1f), st, true);
            scary.transform.SetParent(w.transform, true);
            scary.SetActive(false);
        }
    }

    void Update()
    {
        if (shown || scary == null) return;
        if (ActController.Act < 2 || !Inventory.hasFlashlight) return;

        // Появляется, только когда игрок не видит это место
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 d = scary.transform.position - cam.transform.position;
            if (d.magnitude < 45f && Vector3.Dot(cam.transform.forward, d.normalized) > 0.2f) return;
        }
        scary.SetActive(true);
        shown = true;
    }

    // Плоская картинка на стене. normal — куда смотрит лицевая сторона.
    static GameObject Quad(string name, Vector3 pos, Vector3 normal, Vector2 size, Texture2D tex, bool cutout)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.position = pos;
        q.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
        q.transform.localScale = new Vector3(size.x, size.y, 1f);

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(sh != null ? sh : Shader.Find("Standard"));
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        if (cutout)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.4f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = 2450;
        }
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return q;
    }
}
