using UnityEngine;

// Главное меню и пауза.
// - При запуске: экран «СМЕНА» с кнопками «Начать смену», «Настройки», «Выйти». Игра стоит, пока вы не нажмёте «Начать».
//   Отключить меню для быстрых тестов: devShowMainMenu = false в GameConfig.cs.
// - Esc во время игры: пауза (игра останавливается). Там громкость и чувствительность мыши.
//   Настройки сохраняются между запусками.
// Внешний вид пока простой. Когда пришлёте референсы UI, перерисуем.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class GameMenus : MonoBehaviour
{
    public static bool IsBlocking;   // меню открыто: остальные скрипты не реагируют на Esc

    enum Screen { None, Main, Pause, Settings }
    Screen screen = Screen.None;
    Screen backTo = Screen.None;

    float volume = 1f;
    float sensitivity = 2f;
    PlayerController pc;
    Interactor interactor;
    GUIStyle titleS, subS, buttonS, labelS;

    // Рукописное главное меню (картинки в Resources/UI/Menu). Если картинок нет — обычные кнопки.
    Texture2D titleTex;
    Texture2D[] btnTex, btnHover;
    static readonly string[] BtnNames = { "continue", "newgame", "settings", "exit" };
    bool menuArtLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        IsBlocking = false;
        new GameObject("GameMenus").AddComponent<GameMenus>();
    }

    void Start()
    {
        volume = PlayerPrefs.GetFloat("volume", 1f);
        sensitivity = PlayerPrefs.GetFloat("sensitivity", 2f);
        Apply();

        if (GameConfig.showMainMenu) Show(Screen.Main);
    }

    void Apply()
    {
        AudioListener.volume = volume;
        FindPlayer();
        if (pc != null) pc.mouseSensitivity = sensitivity;
    }

    void FindPlayer()
    {
        if (pc == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) pc = p.GetComponent<PlayerController>();
        }
        if (interactor == null && Camera.main != null) interactor = Camera.main.GetComponent<Interactor>();
    }

    void Show(Screen s)
    {
        screen = s;
        IsBlocking = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        FindPlayer();
        if (pc != null) pc.enabled = false;
        if (interactor != null) interactor.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Hide()
    {
        screen = Screen.None;
        IsBlocking = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (FinaleController.Locked) return;   // в финале управление не возвращаем
        if (pc != null && !PalletCargo.IsLifting()) pc.enabled = true;
        if (interactor != null) interactor.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (screen == Screen.None)
        {
            if (PhoneUI.IsOpen) return;   // Esc сначала закрывает телефон
            Show(Screen.Pause);
        }
        else if (screen == Screen.Pause) Hide();
        else if (screen == Screen.Settings) screen = backTo;
    }

    // ---------- Рисование ----------
    void MakeStyles()
    {
        titleS = new GUIStyle(UITheme.Label());
        titleS.fontSize = 72;
        titleS.fontStyle = FontStyle.Bold;
        titleS.alignment = TextAnchor.MiddleCenter;
        titleS.normal.textColor = Color.white;

        subS = new GUIStyle(titleS);
        subS.fontSize = 22;
        subS.fontStyle = FontStyle.Normal;
        subS.normal.textColor = new Color(0.65f, 0.65f, 0.7f);

        buttonS = new GUIStyle(UITheme.Button());
        buttonS.fontSize = 26;

        labelS = new GUIStyle(UITheme.Label());
        labelS.fontSize = 22;
        labelS.normal.textColor = Color.white;
    }

    void OnGUI()
    {
        if (screen == Screen.None) return;
        if (titleS == null) MakeStyles();
        GUI.depth = -300;   // поверх всего

        GUI.color = new Color(0f, 0f, 0f, screen == Screen.Main ? 1f : 0.85f);
        GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 420f;
        float x = (UnityEngine.Screen.width - w) / 2f;
        float y = UnityEngine.Screen.height * 0.22f;

        if (screen == Screen.Main && DrawMainArt()) { }
        else if (screen == Screen.Main)
        {
            GUI.Label(new Rect(0, y, UnityEngine.Screen.width, 90), "СМЕНА", titleS);
            GUI.Label(new Rect(0, y + 90, UnityEngine.Screen.width, 30), "16:00 — 20:00", subS);
            y += 180;
            if (UiSound.Button(new Rect(x, y, w, 64), "Начать смену", buttonS)) Hide();
            if (UiSound.Button(new Rect(x, y + 80, w, 64), "Настройки", buttonS)) { backTo = Screen.Main; screen = Screen.Settings; }
            if (UiSound.Button(new Rect(x, y + 160, w, 64), "Выйти", buttonS)) Quit();
        }
        else if (screen == Screen.Pause)
        {
            GUI.Label(new Rect(0, y, UnityEngine.Screen.width, 90), "ПАУЗА", titleS);
            y += 150;
            if (UiSound.Button(new Rect(x, y, w, 64), "Продолжить", buttonS)) Hide();
            if (UiSound.Button(new Rect(x, y + 80, w, 64), "Настройки", buttonS)) { backTo = Screen.Pause; screen = Screen.Settings; }
            if (UiSound.Button(new Rect(x, y + 160, w, 64), "Выйти из игры", buttonS)) Quit();
        }
        else
        {
            DrawSettings();
        }
    }

    // ---------- Настройки: слева звук, мышь и яркость, справа каждый параметр графики ----------
    void DrawSettings()
    {
        float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
        float colW = Mathf.Min(420f, sw * 0.42f), gap = 40f;
        float lx = sw / 2f - colW - gap / 2f, rx = sw / 2f + gap / 2f;
        float y = sh * 0.12f;
        GUI.Label(new Rect(0, y, sw, 90), "НАСТРОЙКИ", titleS);
        y += 110f;

        // Левая колонка
        float ly = y;
        GUI.Label(new Rect(lx, ly, colW, 30), "Громкость: " + Mathf.RoundToInt(volume * 100f) + "%", labelS);
        float v = UiSound.Slider(new Rect(lx, ly + 38, colW, 24), volume, 0f, 1f);
        ly += 80f;
        GUI.Label(new Rect(lx, ly, colW, 30), "Чувствительность мыши: " + sensitivity.ToString("0.0"), labelS);
        float s2 = UiSound.Slider(new Rect(lx, ly + 38, colW, 24), sensitivity, 0.3f, 6f);
        ly += 80f;
        if (!Mathf.Approximately(v, volume) || !Mathf.Approximately(s2, sensitivity))
        {
            volume = v;
            sensitivity = s2;
            PlayerPrefs.SetFloat("volume", volume);
            PlayerPrefs.SetFloat("sensitivity", sensitivity);
            Apply();
        }
        GUI.Label(new Rect(lx, ly, colW, 30), "Яркость: " + (Brightness.Value >= 0 ? "+" : "") + Mathf.RoundToInt(Brightness.Value * 100f) + "%", labelS);
        float br = UiSound.Slider(new Rect(lx, ly + 38, colW, 24), Brightness.Value, -1f, 1f);
        if (!Mathf.Approximately(br, Brightness.Value)) { Brightness.Value = br; PlayerPrefs.Save(); }

        // Правая колонка: графика
        float ry = y, bh = 46f, step = 54f;
        GUI.Label(new Rect(rx, ry, colW, 30), "ГРАФИКА", labelS);
        ry += 38f;
        if (UiSound.Button(new Rect(rx, ry, colW, bh), "Качество: " + VisualOptions.QualityNames[VisualOptions.Quality], buttonS))
            VisualOptions.Quality = (VisualOptions.Quality + 1) % VisualOptions.QualityNames.Length;
        ry += step;
        bool high = VisualOptions.Quality == 2;
        VisualOptions.VolumeLight = Toggle(new Rect(rx, ry, colW, bh), "Объёмный свет", VisualOptions.VolumeLight, high); ry += step;
        VisualOptions.DarkCorners = Toggle(new Rect(rx, ry, colW, bh), "Тёмные углы", VisualOptions.DarkCorners, high); ry += step;
        bool post = VisualOptions.Quality > 0 || VisualOptions.Vhs;
        VisualOptions.AO = Toggle(new Rect(rx, ry, colW, bh), "Затенение (AO)", VisualOptions.AO, post); ry += step;
        VisualOptions.MotionBlur = Toggle(new Rect(rx, ry, colW, bh), "Размытие в движении", VisualOptions.MotionBlur, post); ry += step;
        VisualOptions.Vhs = Toggle(new Rect(rx, ry, colW, bh), "VHS (крупные пиксели)", VisualOptions.Vhs, true); ry += step;
        VisualOptions.VhsTape = Toggle(new Rect(rx, ry, colW, bh), "Эффекты кассеты", VisualOptions.VhsTape, VisualOptions.Vhs); ry += step;
        if (!high) GUI.Label(new Rect(rx, ry, colW, 26), "Свет и тёмные углы — только в «Высокой»", subS);

        float by = Mathf.Max(ly + 90f, ry + 40f);
        if (UiSound.Button(new Rect((sw - 420f) / 2f, Mathf.Min(by, sh - 80f), 420f, 60f), "Назад (Esc)", buttonS)) screen = backTo;
    }

    // Кнопка-переключатель «Название: вкл/выкл»; недоступная рисуется блёкло и не нажимается
    bool Toggle(Rect r, string name, bool value, bool enabled)
    {
        bool was = GUI.enabled;
        GUI.enabled = enabled;
        bool clicked = UiSound.Button(r, name + ":  " + (value ? "вкл" : "выкл"), buttonS);
        GUI.enabled = was;
        return clicked ? !value : value;
    }

    void LoadMenuArt()
    {
        menuArtLoaded = true;
        titleTex = Resources.Load<Texture2D>("UI/Menu/smena_title");
        btnTex = new Texture2D[BtnNames.Length];
        btnHover = new Texture2D[BtnNames.Length];
        for (int i = 0; i < BtnNames.Length; i++)
        {
            btnTex[i] = Resources.Load<Texture2D>("UI/Menu/btn_" + BtnNames[i]);
            btnHover[i] = Resources.Load<Texture2D>("UI/Menu/btn_" + BtnNames[i] + "_hover");
        }
    }

    // Главное меню из рукописных надписей. При наведении буквы заливаются белым.
    // «Продолжить» пока неактивна: сохранений в игре нет.
    bool DrawMainArt()
    {
        if (!menuArtLoaded) LoadMenuArt();
        if (titleTex == null) return false;
        for (int i = 0; i < btnTex.Length; i++) if (btnTex[i] == null) return false;

        float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
        // Заголовок
        float th = sh * 0.24f, tw = th * titleTex.width / titleTex.height;
        GUI.DrawTexture(new Rect((sw - tw) / 2f, sh * 0.07f, tw, th), titleTex);

        // Кнопки: один масштаб для всех, чтобы буквы были одной высоты
        float k = sh * 0.075f / 228f;
        float y = sh * 0.38f;
        Vector2 mouse = Event.current.mousePosition;
        for (int i = 0; i < btnTex.Length; i++)
        {
            Texture2D t = btnTex[i];
            float w = t.width * k, h = t.height * k;
            Rect r = new Rect((sw - w) / 2f, y, w, h);
            y += sh * 0.135f;
            bool active = i != 0;   // «Продолжить»: сохранений пока нет
            if (!active)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                GUI.DrawTexture(r, t);
                GUI.color = Color.white;
                continue;
            }
            bool hover = r.Contains(mouse);
            GUI.DrawTexture(r, hover && btnHover[i] != null ? btnHover[i] : t);
            UiSound.Hover(r);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                UiSound.Click();
                if (i == 1) Hide();
                else if (i == 2) { backTo = Screen.Main; screen = Screen.Settings; }
                else if (i == 3) Quit();
            }
        }
        return true;
    }

    void Quit()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
