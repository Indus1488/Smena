using UnityEngine;

// Следит за актами. В 18:00 (или по клавише F9 для тестов) наступает акт 2:
// свет в складах гаснет, снаружи темнеет, на заводе загорается лампа, появляется фонарик.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class ActController : MonoBehaviour
{
    public static int Act = 1;
    public static ActController Instance;

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
        if (Act != 1) return;

        bool timeUp = Hud.minutes >= 18f * 60f;
        bool debug = GameConfig.debugKeys && Input.GetKeyDown(KeyCode.F9);
        if (timeUp || debug) StartAct2();
    }

    public void StartAct2()
    {
        if (Act == 2) return;
        Act = 2;
        Hud.minutes = Mathf.Max(Hud.minutes, 18f * 60f);

        // 1. Свет во всех складах гаснет
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == "InteriorLights") all[i].gameObject.SetActive(false);

        // 2. На заводе загорается лампа
        GameObject lamp = GameObject.Find("FactoryLamp");
        if (lamp != null)
        {
            Light l = lamp.GetComponentInChildren<Light>(true);
            if (l != null) l.enabled = true;
        }

        // 3. Снаружи темнеет: фон, окружающий свет, туман, солнце
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.025f, 0.028f, 0.045f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.03f);
        RenderSettings.fogDensity = 0.035f;

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.02f);
        }

        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional)
            {
                lights[i].intensity = 0.04f;
                lights[i].color = new Color(0.4f, 0.5f, 0.8f);
            }
        }

        // 4. Селлеры больше не приезжают, а у вас появляется фонарик
        if (BoxSpawner.Instance != null) BoxSpawner.Instance.Stop();
        Inventory.hasFlashlight = true;

        Hud.Say("18:00. Свет погас. Включите фонарик: Q.", 7f);
        Hud.ShowPhone("СМС", "«Свет отключили. Не ходи", "без фонарика и береги батарею»", 10f);
    }
}
