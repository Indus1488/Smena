using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Следит за актами.
// Акт 1 (16:00): работа.
// 17:30: загораются 4 лампы на фасаде полосатого центрального склада (CentralLamps).
// Акт 2 (18:00): скрип: распахиваются ворота среднего склада, Володя пробегает по фасаду центрального склада и разбивает лампы, наступает тьма.
//                Селлеры продолжают приезжать, но без света коробок не видно.
// Акт 3 (19:30): Володя охотится на вас, цены падают, селлеры злятся.
// Акт 4 (20:00): финал (FinaleController).
// Тестовые клавиши: F9 — акт 2, F10 — сразу акт 3, F11 — финал.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
// Субтитров нет: всё, что происходит, должно быть понятно по звуку (ваши записи кладутся в Assets/Resources).
public class ActController : MonoBehaviour
{
    public static int Act = 1;
    public static bool NightStarted;   // темнота наступила — с этого момента идут охоты
    public static ActController Instance;

    // В акте 3 цены падают, а селлеры злятся быстрее
    public static float PriceFactor { get { return Act >= 3 ? 0.55f : 1f; } }
    public static float AngerFactor { get { return Act >= 3 ? 0.6f : 1f; } }

    bool sequenceRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Act = 1;
        NightStarted = false;
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
        // (ворота CentralGate полосатого здания не трогаем)
        GameObject mid = GameObject.Find("WarehouseMonster");
        WarehouseDoor door = mid != null ? mid.GetComponentInChildren<WarehouseDoor>(true) : null;
        if (door != null)
        {
            door.ForceOpen();
            Sfx.PlayAt("gate_creak", door.transform.position, 1f);
        }
        else Debug.LogWarning("Не найден склад WarehouseMonster с дверью: ворота в 18:00 не откроются.");
        yield return StartCoroutine(Shake(1.8f));
        yield return new WaitForSeconds(2f);

        // 2. Лампы на фасаде центрального склада горят с 17:30 (если ещё нет — включаем сейчас)
        if (CentralLamps.Instance != null) CentralLamps.Instance.TurnOn();
        List<Transform> lamps = CentralLamps.Lamps;

        // 3. Ждём, пока игрок посмотрит на лампы (но не дольше 25 секунд)
        float waited = 0f;
        while (waited < 25f && !LookingAtAny(lamps))
        {
            waited += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(0.6f);

        // 4. Володя проносится по фасаду центрального склада и разбивает все лампы
        if (lamps.Count > 0) yield return StartCoroutine(Streak(lamps));
        else SmashLamp();

        // 4б. Спрыгивает к щитку и бьёт по нему — искры, и свет гаснет
        yield return StartCoroutine(PanelScene());

        // 5. Тьма
        yield return StartCoroutine(Darken());
        Inventory.hasFlashlight = true;
        Hud.Say("В кармане фонарик. F — включить.", 5f);
        PhoneCall.Boss("boss_blackout", "Свет на районе вырубило. Работаем дальше. Батарейки береги, лишние — в магазине. И про норму помни. Не подведи.",
            "Свет на районе вырубило. Работаем дальше. Батарейки береги, лишние — в магазине. По норме: " + GameConfig.quotaPallets + (GameConfig.quotaPallets == 1 ? " паллета" : " паллеты") + " до конца смены. Увезено " + Inventory.palletsShipped + ".");

        // Ночь: Володя начинает выходить на охоты (расписание — HuntDirector)
        MonsterController mon = MonsterController.Find();
        if (mon != null && GameConfig.horror && Act < 3) mon.BeginHunt();
        NightStarted = true;

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

    bool LookingAtAny(List<Transform> lamps)
    {
        for (int i = 0; i < lamps.Count; i++)
            if (lamps[i] != null && LookingAt(lamps[i].position)) return true;
        return false;
    }

    // Володя бежит по фасаду центрального склада и по пути разбивает лампы.
    // Лампы смотрят во двор: их forward — «от стены», right — вдоль стены.
    IEnumerator Streak(List<Transform> lamps)
    {
        MonsterController m = MonsterController.Find();
        if (m == null || !GameConfig.horror)
        {
            if (m == null) Debug.LogWarning("Володя не найден. Пересоберите склад: Smena, Построить склад (с нуля).");
            yield return new WaitForSeconds(0.5f);
            SmashLamp();       // без хоррора лампы просто гаснут
            yield break;
        }

        Vector3 normal = lamps[0].forward;   // от стены
        Vector3 dir = -lamps[0].right;       // вдоль стены

        // Где по пути стоит каждая лампа
        float minP = float.MaxValue, maxP = float.MinValue;
        Transform first = lamps[0];
        float[] proj = new float[lamps.Count];
        for (int i = 0; i < lamps.Count; i++)
        {
            proj[i] = Vector3.Dot(lamps[i].position, dir);
            if (proj[i] < minP) { minP = proj[i]; first = lamps[i]; }
            if (proj[i] > maxP) maxP = proj[i];
        }
        const float runUp = 25f;
        bool[] smashed = new bool[lamps.Count];

        m.gameObject.SetActive(true);
        m.mode = MonsterController.Mode.Scripted;
        Transform t = m.transform;
        t.rotation = Quaternion.LookRotation(dir, normal);   // ногами к стене
        t.position = first.position - normal * 0.1f - dir * runUp;

        float travelled = 0f, total = runUp * 2f + (maxP - minP);
        while (travelled < total)
        {
            float step = 45f * Time.deltaTime;
            t.position += dir * step;
            travelled += step;
            for (int i = 0; i < lamps.Count; i++)
            {
                if (smashed[i] || travelled < runUp + (proj[i] - minP)) continue;
                smashed[i] = true;
                if (CentralLamps.Instance != null) CentralLamps.Instance.Smash(lamps[i]);
            }
            yield return null;
        }

        // После пробежки по стене снова стоит в своём складе; если за это время начался акт 3 (F10) — сразу охотится
        if (Act >= 3) m.BeginHunt();
        else m.ReturnHome();
    }

    // Володя у щитка: стоит лицом к стене, бьёт (анимация attack), щиток искрит
    IEnumerator PanelScene()
    {
        ElectricPanel panel = ElectricPanel.Instance;
        if (panel == null || panel.StandPoint == Vector3.zero) yield break;
        MonsterController m = MonsterController.Find();
        if (m == null || !GameConfig.horror) { panel.Break(); yield break; }

        m.gameObject.SetActive(true);
        m.mode = MonsterController.Mode.Scripted;
        m.transform.position = panel.StandPoint;
        m.transform.rotation = Quaternion.LookRotation(-panel.Front, Vector3.up);
        yield return new WaitForSeconds(0.5f);

        VolodyaModel vm = m.GetComponent<VolodyaModel>();
        float len = vm != null ? vm.PlayOnce("attack") : 0f;
        if (len <= 0f) len = 1f;
        yield return new WaitForSeconds(len * 0.45f);   // момент удара в анимации
        panel.Break();
        yield return new WaitForSeconds(len * 0.55f + 0.8f);

        if (Act >= 3) m.BeginHunt();
        else m.ReturnHome();
    }

    void SmashLamp()
    {
        if (CentralLamps.Instance != null) CentralLamps.Instance.SmashAll();
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

        float r0 = RenderSettings.reflectionIntensity;
        float t = 0f;
        while (t < 2.5f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 2.5f);
            RenderSettings.ambientLight = Color.Lerp(a0, a1, k);
            RenderSettings.reflectionIntensity = Mathf.Lerp(r0, 0.03f, k);   // отражения неба ночью почти гаснут
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
        RenderSettings.reflectionIntensity = 0.03f;   // иначе гладкие и металлические вещи светятся отражением дневного неба

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
        PhoneCall.Boss("boss_night", "Цены просели, селлеры злые. Отгрузи что успеешь. И не свети фонарём почём зря — мало ли кто увидит.");

        NightStarted = true;
        MonsterController m = MonsterController.Find();
        if (m != null) { if (m.mode != MonsterController.Mode.Hunt) m.BeginHunt(); }   // уже охотится с 18:00 — не сбиваем
        else Debug.LogWarning("Володя не найден. Пересоберите склад: Smena, Построить склад (с нуля).");
    }
}