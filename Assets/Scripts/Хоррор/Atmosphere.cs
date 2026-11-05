using UnityEngine;

// Атмосфера (без звуков — их добавит автор):
// - 17:30–18:00: лампы в главном складе иногда мигают — намёк, что скоро что-то случится;
// - акт 3: края экрана темнеют, когда пульс высокий или Володя рядом;
// - Володя ближе 8 метров во время охоты: камера мелко дрожит.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class Atmosphere : MonoBehaviour
{
    Light[] mainLamps;
    float[] baseIntensity;
    float flickerUntil, nextFlicker;
    Texture2D vignette;
    float vignetteAlpha;
    Vector3 lastShake;
    Camera cam;
    MonsterController monster;
    float nextFind;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("Atmosphere").AddComponent<Atmosphere>();
    }

    void Start()
    {
        // Лампы только главного склада
        GameObject w = GameObject.Find("Warehouse");
        Transform lights = w != null ? w.transform.Find("InteriorLights") : null;
        mainLamps = lights != null ? lights.GetComponentsInChildren<Light>(true) : new Light[0];
        baseIntensity = new float[mainLamps.Length];
        for (int i = 0; i < mainLamps.Length; i++) baseIntensity[i] = mainLamps[i].intensity;

        vignette = MakeVignette(128);
    }

    // Круглая маска: прозрачная в центре, чёрная по краям
    static Texture2D MakeVignette(int n)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((d - 0.45f) / 0.75f);
                t.SetPixel(x, y, new Color(0f, 0f, 0f, a * a));
            }
        t.Apply();
        return t;
    }

    void Update()
    {
        FlickerLamps();

        if (monster == null && Time.time >= nextFind)
        {
            nextFind = Time.time + 1f;
            monster = MonsterController.Find();
        }

        // Сила затемнения краёв
        float target = 0f;
        if (ActController.Act == 3)
        {
            target = Mathf.Clamp01((Inventory.pulse - 80f) / 50f) * 0.8f;
            if (monster != null && monster.gameObject.activeInHierarchy && monster.mode == MonsterController.Mode.Hunt)
                target = Mathf.Max(target, Mathf.Clamp01((20f - monster.DistanceToPlayer()) / 20f));
        }
        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, target, Time.deltaTime * 0.8f);
    }

    void FlickerLamps()
    {
        if (mainLamps == null || mainLamps.Length == 0) return;
        bool window = ActController.Act == 1 && Hud.minutes >= 17.5f * 60f;
        if (!window) return;

        if (Time.time >= nextFlicker)
        {
            nextFlicker = Time.time + Random.Range(6f, 15f);
            flickerUntil = Time.time + Random.Range(0.3f, 0.9f);
        }

        bool flicker = Time.time < flickerUntil;
        for (int i = 0; i < mainLamps.Length; i++)
        {
            if (mainLamps[i] == null) continue;
            float k = flicker ? (Mathf.PerlinNoise(Time.time * 25f, i * 3.1f) > 0.45f ? 1f : 0.1f) : 1f;
            mainLamps[i].intensity = baseIntensity[i] * k;
        }
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Убираем прошлое дрожание, чтобы оно не накапливалось
        cam.transform.localPosition -= lastShake;
        lastShake = Vector3.zero;

        if (FinaleController.Locked || GameMenus.IsBlocking) return;
        if (monster == null || !monster.gameObject.activeInHierarchy || monster.mode != MonsterController.Mode.Hunt) return;

        float d = monster.DistanceToPlayer();
        if (d > 8f) return;
        float amp = Mathf.Lerp(0.03f, 0.004f, d / 8f);
        lastShake = Random.insideUnitSphere * amp;
        cam.transform.localPosition += lastShake;
    }

    void OnGUI()
    {
        if (vignetteAlpha <= 0.01f || vignette == null) return;
        if (Event.current.type != EventType.Repaint) return;
        GUI.depth = 500;   // поверх картинки, но под интерфейсом
        GUI.color = new Color(1f, 1f, 1f, vignetteAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
    }
}
