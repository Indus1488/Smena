using System.Collections;
using UnityEngine;

// Следит за актами.
// Акт 1 (16:00): работа.
// Акт 2 (18:00): скрип ворот центрального склада, на заводе загорается лампа, Володя пробегает по стене и разбивает её, наступает тьма.
//                Селлеры продолжают приезжать, но без света коробок не видно.
// Акт 3 (19:30): Володя охотится на вас, цены падают, селлеры злятся.
// Акт 4 (20:00): финал (FinaleController).
// Тестовые клавиши: F9 — акт 2, F10 — сразу акт 3, F11 — финал.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
// Субтитров нет: всё, что происходит, должно быть понятно по звуку (ваши записи кладутся в Assets/Resources).
public class ActController : MonoBehaviour
{
    public static int Act = 1;
    public static ActController Instance;

    // В акте 3 цены падают, а селлеры злятся быстрее
    public static float PriceFactor { get { return Act >= 3 ? 0.55f : 1f; } }
    public static float AngerFactor { get { return Act >= 3 ? 0.6f : 1f; } }

    bool sequenceRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Act = 1;
        GameObject go = new GameObject("ActController");
        Instance = go.AddComponent<ActController>();
        if (GameConfig.startInAct2) Instance.Invoke("StartAct2", 0.5f);
    }

    void Update()
    {
        bool d2 = GameConfig.debugKeys && Input.GetKeyDown(KeyCode.F9);
        bool d3 = GameConfig.debugKeys && Input.GetKeyDown(KeyCode.F10);

        if (Act == 1 && (Hud.minutes >= 18f * 60f || d2)) StartAct2();
        if (Act < 3 && ((Hud.minutes >= 19.5f * 60f && !sequenceRunning) || d3)) StartAct3();
    }

    // ================= АКТ 2 =================
    public void StartAct2()
    {
        if (Act >= 2) return;
        Act = 2;
        Hud.minutes = Mathf.Max(Hud.minutes, 18f * 60f);
        StartCoroutine(Act2Sequence());
    }

    IEnumerator Act2Sequence()
    {
        sequenceRunning = true;

        // 1. Оглушительный скрип: открываются ворота центрального склада
        GameObject gateGO = GameObject.Find("CentralGate");
        if (gateGO != null)
        {
            CentralGate cg = gateGO.GetComponent<CentralGate>();
            if (cg != null) cg.Open();
        }
        yield return StartCoroutine(Shake(1.8f));
        yield return new WaitForSeconds(2f);

        // 2. На заводе загорается тусклая лампа
        GameObject lampGO = GameObject.Find("FactoryLamp");
        Light lampLight = lampGO != null ? lampGO.GetComponentInChildren<Light>(true) : null;
        if (lampLight != null)
        {
            lampLight.intensity = 1.2f;
            lampLight.enabled = true;
        }

        // 3. Ждём, пока игрок посмотрит на лампу (но не дольше 25 секунд)
        float waited = 0f;
        while (waited < 25f && !(lampGO != null && LookingAt(lampGO.transform.position)))
        {
            waited += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(0.6f);

        // 4. Володя проносится по стене завода и разбивает лампу
        if (lampGO != null) yield return StartCoroutine(Streak(lampGO.transform));
        else SmashLamp();

        // 5. Тьма
        yield return StartCoroutine(Darken());
        Inventory.hasFlashlight = true;
        Hud.Say("Фонарик: Q", 5f);
        PhoneUI.AddMessage("Начальник", "Свет на районе отключили. Береги батарею и не свети зря. Запас можно купить в магазине.");

        sequenceRunning = false;
    }

    IEnumerator Shake(float seconds)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        Vector3 basePos = cam.transform.localPosition;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            cam.transform.localPosition = basePos + Random.insideUnitSphere * 0.04f;
            yield return null;
        }
        cam.transform.localPosition = basePos;
    }

    bool LookingAt(Vector3 p)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 d = p - cam.transform.position;
        return d.magnitude < 90f && Vector3.Dot(cam.transform.forward, d.normalized) > 0.9f;
    }

    // Володя бежит вдоль стены завода к лампе. Лампа повёрнута лицом ко двору, её forward — «от стены».
    IEnumerator Streak(Transform lampT)
    {
        MonsterController m = MonsterController.Find();
        if (m == null)
        {
            Debug.LogWarning("Володя не найден. Пересоберите склад: Smena, Построить склад (с нуля).");
            SmashLamp();
            yield break;
        }

        Vector3 lampPos = lampT.position;
        Vector3 normal = lampT.forward;      // от стены
        Vector3 dir = -lampT.right;          // вдоль стены

        m.gameObject.SetActive(true);
        m.mode = MonsterController.Mode.Scripted;
        Transform t = m.transform;
        t.rotation = Quaternion.LookRotation(dir, normal);   // ногами к стене
        t.position = lampPos - normal * 0.1f - dir * 30f;

        float travelled = 0f;
        bool smashed = false;
        while (travelled < 70f)
        {
            float step = 45f * Time.deltaTime;
            t.position += dir * step;
            travelled += step;
            if (!smashed && travelled >= 30f)
            {
                smashed = true;
                SmashLamp();
            }
            yield return null;
        }

        m.mode = MonsterController.Mode.Hidden;
        m.gameObject.SetActive(false);
    }

    void SmashLamp()
    {
        GameObject lampGO = GameObject.Find("FactoryLamp");
        if (lampGO == null) return;
        Light l = lampGO.GetComponentInChildren<Light>(true);
        if (l != null) l.enabled = false;
        Renderer r = lampGO.GetComponentInChildren<Renderer>();
        if (r != null) r.enabled = false;
    }

    void TurnOffInteriorLights()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == "InteriorLights") all[i].gameObject.SetActive(false);
    }

    Light[] DirectionalLights()
    {
        Light[] all = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i].type == LightType.Directional) n++;
        Light[] res = new Light[n];
        n = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i].type == LightType.Directional) res[n++] = all[i];
        return res;
    }

    // Плавное затемнение за 2.5 секунды
    IEnumerator Darken()
    {
        TurnOffInteriorLights();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        Color a0 = RenderSettings.ambientLight;
        Color a1 = new Color(0.012f, 0.014f, 0.025f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.015f, 0.015f, 0.025f);

        Light[] suns = DirectionalLights();
        float[] i0 = new float[suns.Length];
        for (int i = 0; i < suns.Length; i++) i0[i] = suns[i].intensity;

        float t = 0f;
        while (t < 2.5f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 2.5f);
            RenderSettings.ambientLight = Color.Lerp(a0, a1, k);
            RenderSettings.fogDensity = Mathf.Lerp(0f, 0.035f, k);
            for (int i = 0; i < suns.Length; i++) suns[i].intensity = Mathf.Lerp(i0[i], 0.03f, k);
            yield return null;
        }
        ApplyDarkFinal();
    }

    void ApplyDarkFinal()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.012f, 0.014f, 0.025f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.015f, 0.015f, 0.025f);
        RenderSettings.fogDensity = 0.035f;

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.02f);
        }

        Light[] suns = DirectionalLights();
        for (int i = 0; i < suns.Length; i++)
        {
            suns[i].intensity = 0.03f;
            suns[i].color = new Color(0.4f, 0.5f, 0.8f);
        }
    }

    // ================= АКТ 3 =================
    public void StartAct3()
    {
        if (Act >= 3) return;

        if (Act < 2)
        {
            // Прыжок сразу в акт 3 (тест): мгновенная тьма без кинематографа
            TurnOffInteriorLights();
            ApplyDarkFinal();
            SmashLamp();
        }

        Act = 3;
        Hud.minutes = Mathf.Max(Hud.minutes, 19.5f * 60f);
        Inventory.hasFlashlight = true;
        PhoneUI.AddMessage("Начальник", "Цены просели, селлеры нервные. Копи на газель и не светись зря.");

        MonsterController m = MonsterController.Find();
        if (m != null) m.BeginHunt();
        else Debug.LogWarning("Володя не найден. Пересоберите склад: Smena, Построить склад (с нуля).");
    }
}