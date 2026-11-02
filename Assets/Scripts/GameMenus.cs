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

        if (screen == Screen.Main)
        {
            GUI.Label(new Rect(0, y, UnityEngine.Screen.width, 90), "СМЕНА", titleS);
            GUI.Label(new Rect(0, y + 90, UnityEngine.Screen.width, 30), "16:00 — 20:00", subS);
            y += 180;
            if (GUI.Button(new Rect(x, y, w, 64), "Начать смену", buttonS)) Hide();
            if (GUI.Button(new Rect(x, y + 80, w, 64), "Настройки", buttonS)) { backTo = Screen.Main; screen = Screen.Settings; }
            if (GUI.Button(new Rect(x, y + 160, w, 64), "Выйти", buttonS)) Quit();
        }
        else if (screen == Screen.Pause)
        {
            GUI.Label(new Rect(0, y, UnityEngine.Screen.width, 90), "ПАУЗА", titleS);
            y += 150;
            if (GUI.Button(new Rect(x, y, w, 64), "Продолжить", buttonS)) Hide();
            if (GUI.Button(new Rect(x, y + 80, w, 64), "Настройки", buttonS)) { backTo = Screen.Pause; screen = Screen.Settings; }
            if (GUI.Button(new Rect(x, y + 160, w, 64), "Выйти из игры", buttonS)) Quit();
        }
        else
        {
            GUI.Label(new Rect(0, y, UnityEngine.Screen.width, 90), "НАСТРОЙКИ", titleS);
            y += 100;

            GUI.Label(new Rect(x, y, w, 30), "Громкость: " + Mathf.RoundToInt(volume * 100f) + "%", labelS);
            float v = GUI.HorizontalSlider(new Rect(x, y + 38, w, 24), volume, 0f, 1f);

            GUI.Label(new Rect(x, y + 80, w, 30), "Чувствительность мыши: " + sensitivity.ToString("0.0"), labelS);
            float s = GUI.HorizontalSlider(new Rect(x, y + 118, w, 24), sensitivity, 0.3f, 6f);

            if (!Mathf.Approximately(v, volume) || !Mathf.Approximately(s, sensitivity))
            {
                volume = v;
                sensitivity = s;
                PlayerPrefs.SetFloat("volume", volume);
                PlayerPrefs.SetFloat("sensitivity", sensitivity);
                Apply();
            }

            // Яркость
            GUI.Label(new Rect(x, y + 384, w, 30), "Яркость: " + (Brightness.Value >= 0 ? "+" : "") + Mathf.RoundToInt(Brightness.Value * 100f) + "%", labelS);
            float br = GUI.HorizontalSlider(new Rect(x, y + 422, w, 24), Brightness.Value, -1f, 1f);
            if (!Mathf.Approximately(br, Brightness.Value)) { Brightness.Value = br; PlayerPrefs.Save(); }

            // Графика: нажатие переключает Низкая → Обычная → Высокая; VHS включается отдельно
            if (GUI.Button(new Rect(x, y + 170, w, 52), "Графика: " + VisualOptions.QualityNames[VisualOptions.Quality], buttonS))
                VisualOptions.Quality = (VisualOptions.Quality + 1) % VisualOptions.QualityNames.Length;
            if (GUI.Button(new Rect(x, y + 232, w, 52), "VHS: " + (VisualOptions.Vhs ? "вкл" : "выкл"), buttonS))
                VisualOptions.Vhs = !VisualOptions.Vhs;

            if (GUI.Button(new Rect(x, y + 470, w, 64), "Назад (Esc)", buttonS)) screen = backTo;
        }
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
