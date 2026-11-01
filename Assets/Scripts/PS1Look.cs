using UnityEngine;

// Ретро-вид в духе PS1: игра рисуется в низком разрешении с чёткими пикселями и растягивается на весь экран.
// Интерфейс (часы, телефон, надписи) остаётся чётким поверх картинки.
//
// Пресеты графики переключаются меню Unity: Smena → Графика → ... (работает и прямо во время Play).
// Выбор хранится в PlayerPrefs, поэтому сохраняется между запусками.
// Без выбранного пресета действует GameConfig (ps1Look, ps1Height).
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class PS1Look : MonoBehaviour
{
    // ---------- Пресеты ----------
    public struct Preset
    {
        public string name;
        public bool ps1;
        public int height;
        public bool warm;   // «тёплая» графика: постобработка, тёплый свет, гладкие текстуры (WarmLook.cs)
        public Preset(string n, bool p, int h) { name = n; ps1 = p; height = h; warm = false; }
        public Preset(string n, bool p, int h, bool w) { name = n; ps1 = p; height = h; warm = w; }
    }

    // Вид картинки собирается из настроек графики (VisualOptions): VHS — низкое разрешение, «Высокая» — тёплая картинка
    public static Preset Current
    {
        get
        {
            return new Preset(VisualOptions.QualityNames[VisualOptions.Quality], VisualOptions.Vhs, VisualOptions.VhsHeight, VisualOptions.Warm);
        }
    }

    // ---------- Работа ----------
    RenderTexture rt;
    Camera cam;
    Camera screenCam;
    int lastW, lastH, lastHeight;
    bool active;
    float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("PS1Look").AddComponent<PS1Look>();
    }

    void Start()
    {
        // Пока PS1 включён, основная камера рисует в маленькую текстуру, а не на экран. Без «экранной» камеры
        // Unity пишет «Display 1 No cameras rendering». Эта пустая камера рисует только чёрный фон под картинку.
        screenCam = gameObject.AddComponent<Camera>();
        screenCam.cullingMask = 0;
        screenCam.clearFlags = CameraClearFlags.SolidColor;
        screenCam.backgroundColor = Color.black;
        screenCam.depth = -100;
        screenCam.useOcclusionCulling = false;
        Apply();
    }

    void Apply()
    {
        Preset p = Current;
        cam = Camera.main;
        Release();

        active = p.ps1 && cam != null;
        screenCam.enabled = active;
        if (!active) return;

        QualitySettings.antiAliasing = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

        int h = Mathf.Max(120, p.height);
        int w = Mathf.RoundToInt(h * (float)Screen.width / Mathf.Max(1, Screen.height));
        rt = new RenderTexture(w, h, 24);
        rt.filterMode = FilterMode.Point;
        rt.useMipMap = false;
        rt.antiAliasing = 1;
        cam.targetTexture = rt;

        lastW = Screen.width;
        lastH = Screen.height;
        lastHeight = p.height;
    }

    void Release()
    {
        if (cam != null) cam.targetTexture = null;
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }

    void Update()
    {
        // Раз в полсекунды проверяем, не сменили ли пресет в меню
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 0.5f;
            Preset p = Current;
            if (p.ps1 != active || (active && p.height != lastHeight)) { Apply(); return; }
        }
        if (active && (cam == null || rt == null || Screen.width != lastW || Screen.height != lastH)) Apply();
    }

    void OnDestroy()
    {
        Release();
    }

    void OnGUI()
    {
        if (!active || rt == null) return;
        if (Event.current.type != EventType.Repaint) return;
        GUI.depth = 1000;   // позади всего интерфейса
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rt, ScaleMode.StretchToFill, false);
    }
}
