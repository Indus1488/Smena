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

    // ---------- Эффекты кассеты (VHS: «эффекты кассеты» в настройках) ----------
    // Полосы развёртки, шум, дрожь картинки, цветная кайма, полоса трекинга, рябь внизу кадра, надпись PLAY и счётчик.
    Texture2D scan, noise;
    GUIStyle osd, osdSmall;
    float trackStart = -10f, trackLen, nextTrack = 6f, trackY;
    float jitterUntil, jitterX;

    void MakeTapeTextures()
    {
        scan = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        scan.wrapMode = TextureWrapMode.Repeat; scan.filterMode = FilterMode.Point;
        scan.SetPixels(new[] { new Color(0, 0, 0, 0.32f), new Color(0, 0, 0, 0.10f), new Color(0, 0, 0, 0f), new Color(0, 0, 0, 0.06f) });
        scan.Apply();
        noise = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        noise.wrapMode = TextureWrapMode.Repeat; noise.filterMode = FilterMode.Point;
        Color[] px = new Color[128 * 128];
        System.Random r = new System.Random(7);
        for (int i = 0; i < px.Length; i++) { float v = (float)r.NextDouble(); px[i] = new Color(v, v, v, 1f); }
        noise.SetPixels(px); noise.Apply();
    }

    void DrawTape(float sw, float sh)
    {
        if (scan == null) MakeTapeTextures();
        float t = Time.unscaledTime;

        // Полоса трекинга: иногда по кадру ползёт шумная полоса
        if (t >= nextTrack)
        {
            trackStart = t; trackLen = Random.Range(0.6f, 1.6f); trackY = Random.Range(0.1f, 0.8f);
            nextTrack = t + Random.Range(7f, 16f);
        }
        float tk = (t - trackStart) / trackLen;
        if (tk >= 0f && tk <= 1f)
        {
            float y = (trackY + tk * 0.25f) * sh, h = sh * 0.05f;
            GUI.color = new Color(1f, 1f, 1f, 0.35f * Mathf.Sin(tk * Mathf.PI));
            GUI.DrawTextureWithTexCoords(new Rect(0, y, sw, h), noise, new Rect(Random.value, Random.value, sw / 64f, 0.06f));
            GUI.color = new Color(0f, 0f, 0f, 0.25f * Mathf.Sin(tk * Mathf.PI));
            GUI.DrawTexture(new Rect(0, y + h, sw, h * 0.5f), Texture2D.whiteTexture);
        }

        // Рябь внизу кадра (как у переключения видеоголовок)
        float bh = sh * 0.025f;
        GUI.color = new Color(1f, 1f, 1f, 0.22f);
        GUI.DrawTextureWithTexCoords(new Rect(Random.Range(-6f, 6f), sh - bh, sw, bh), noise, new Rect(Random.value, Random.value, sw / 48f, 0.03f));

        // Шум по всему кадру
        GUI.color = new Color(1f, 1f, 1f, 0.06f);
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, sw, sh), noise, new Rect(Random.value, Random.value, sw / 256f, sh / 256f));

        // Полосы развёртки (по 4 пикселя экрана)
        GUI.color = Color.white;
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, sw, sh), scan, new Rect(0, 0, 1, sh / 4f));

        // Надпись на экране, как у видеомагнитофона
        if (osd == null)
        {
            osd = new GUIStyle(UITheme.Label()); osd.fontSize = Mathf.RoundToInt(sh * 0.038f); osd.fontStyle = FontStyle.Bold;
            osdSmall = new GUIStyle(osd); osdSmall.fontSize = Mathf.RoundToInt(sh * 0.026f);
        }
        bool blink = Mathf.Repeat(t, 1.6f) < 1.1f;
        int sec = Mathf.FloorToInt(Time.timeSinceLevelLoad);
        string counter = string.Format("{0}:{1:00}:{2:00}", sec / 3600, (sec / 60) % 60, sec % 60);
        float x = sw - sh * 0.36f, y0 = sh - sh * 0.2f;
        OsdText(new Rect(x, y0, sh * 0.34f, sh * 0.05f), blink ? "▶ PLAY" : "", osd);
        OsdText(new Rect(x, y0 + sh * 0.05f, sh * 0.34f, sh * 0.04f), "SP  " + counter, osdSmall);
        if (tk >= 0f && tk <= 1f) OsdText(new Rect(x, y0 - sh * 0.05f, sh * 0.34f, sh * 0.04f), "TRACKING", osdSmall);
        GUI.color = Color.white;
    }

    static void OsdText(Rect r, string text, GUIStyle st)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
        GUI.color = new Color(0.92f, 0.95f, 1f, 0.85f);
        GUI.Label(r, text, st);
    }

    void OnGUI()
    {
        if (!active || rt == null) return;
        if (Event.current.type != EventType.Repaint) return;
        GUI.depth = 1000;   // позади всего интерфейса
        float sw = Screen.width, sh = Screen.height;
        bool tape = VisualOptions.VhsTape;

        // Картинка; с эффектами кассеты — с лёгкой дрожью по горизонтали и цветной каймой
        float jx = 0f;
        if (tape)
        {
            if (Time.unscaledTime > jitterUntil && Random.value < 0.02f) { jitterUntil = Time.unscaledTime + Random.Range(0.05f, 0.2f); jitterX = Random.Range(-5f, 5f); }
            jx = Time.unscaledTime < jitterUntil ? jitterX : 0f;
        }
        GUI.DrawTexture(new Rect(jx, 0, sw, sh), rt, ScaleMode.StretchToFill, false);
        if (tape)
        {
            GUI.color = new Color(1f, 0.15f, 0.1f, 0.16f);
            GUI.DrawTexture(new Rect(jx + 3f, 0, sw, sh), rt, ScaleMode.StretchToFill, true);
            GUI.color = new Color(0.1f, 0.3f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(jx - 3f, 0, sw, sh), rt, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            DrawTape(sw, sh);
        }
    }
}
