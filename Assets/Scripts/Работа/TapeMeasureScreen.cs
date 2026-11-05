using System;
using UnityEngine;

// Замер рулеткой «по-настоящему»: на экране крупно лента рулетки у края коробки.
// Игрок сам считывает число сантиметров у края и набирает его цифрами, Enter — записать.
// - Пульс высокий → руки дрожат: лента ходит ходуном, времени меньше.
// - Темно (после 18:00) без фонарика → цифры почти не видно.
// - Время вышло → селлер торопит, записывается то, что успел набрать.
public class TapeMeasureScreen : MonoBehaviour
{
    public static TapeMeasureScreen Instance;
    public static bool IsOpen { get { return Instance != null && Instance.open; } }

    bool open;
    int realCm;
    float windowStart;           // с какого сантиметра начинается видимый кусок ленты
    string title;
    string typed = "";
    float timeLeft, timeTotal;
    Action<int, bool> done;      // (введённое число, вышло ли время)
    GUIStyle bigS, smallS, numS;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Instance = new GameObject("TapeMeasureScreen").AddComponent<TapeMeasureScreen>();
    }

    public static void Open(string what, int realCm, Action<int, bool> onDone)
    {
        if (Instance == null) return;
        TapeMeasureScreen t = Instance;
        t.open = true;
        t.realCm = realCm;
        t.title = what;
        t.typed = "";
        t.done = onDone;
        t.windowStart = realCm - UnityEngine.Random.Range(6f, 17f);
        float tremor = CameraFeel.Tremor;
        t.timeTotal = Mathf.Lerp(14f, 6f, tremor);
        t.timeLeft = t.timeTotal;
        PlayerController.MoveLocked = true;
        Sfx.Play("tape_out", 0.8f);
    }

    void Update()
    {
        if (!open) return;
        timeLeft -= Time.deltaTime;

        foreach (char c in Input.inputString)
        {
            if (char.IsDigit(c) && typed.Length < 3) typed += c;
            else if (c == '\b' && typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
        }
        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if ((enter && typed.Length > 0) || timeLeft <= 0f) Close(timeLeft <= 0f);
    }

    void Close(bool timeout)
    {
        open = false;
        Sfx.Play("tape_in", 0.8f);
        PlayerController.MoveLocked = HidingSpot.Current != null;
        int v = 0;
        int.TryParse(typed, out v);
        Action<int, bool> cb = done;
        done = null;
        if (cb != null) cb(v, timeout);
    }

    void OnGUI()
    {
        if (!open) return;
        if (bigS == null)
        {
            bigS = new GUIStyle(UITheme.Label()) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            smallS = new GUIStyle(UITheme.Label()) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            numS = new GUIStyle(UITheme.Label()) { fontSize = 13, alignment = TextAnchor.UpperCenter };
            numS.normal.textColor = new Color(0.08f, 0.08f, 0.08f);
        }

        float W = Mathf.Min(760f, Screen.width - 40f), H = 250f;
        Rect panel = new Rect((Screen.width - W) / 2f, Screen.height - H - 120f, W, H);
        UITheme.DrawPanel(panel);
        GUI.Label(new Rect(panel.x, panel.y + 10, W, 28), title + ": сколько сантиметров у края коробки?", smallS);

        // Дрожь рук: лента смещается и чуть поворачивается
        float tremor = CameraFeel.Tremor;
        float jx = (Mathf.PerlinNoise(Time.time * 9f, 0.3f) - 0.5f) * 60f * tremor;
        float jy = (Mathf.PerlinNoise(0.7f, Time.time * 9f) - 0.5f) * 18f * tremor;
        float ang = (Mathf.PerlinNoise(Time.time * 6f, 5f) - 0.5f) * 6f * tremor;

        Rect tape = new Rect(panel.x + 20 + jx, panel.y + 50 + jy, W - 40, 70);
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang, tape.center);

        // Лента: жёлтая полоса с делениями. Видно 24 см.
        const float span = 24f;
        float pxPerCm = tape.width / span;
        GUI.color = new Color(0.95f, 0.78f, 0.15f);
        GUI.DrawTexture(tape, Texture2D.whiteTexture);
        GUI.color = new Color(0.1f, 0.1f, 0.1f);
        for (int mm = Mathf.CeilToInt(windowStart * 10f); mm <= (windowStart + span) * 10f; mm++)
        {
            float x = tape.x + (mm / 10f - windowStart) * pxPerCm;
            float h = mm % 10 == 0 ? 30f : (mm % 5 == 0 ? 20f : 11f);
            GUI.DrawTexture(new Rect(x, tape.y, mm % 10 == 0 ? 2f : 1f, h), Texture2D.whiteTexture);
            if (mm % 10 == 0)
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(x - 20, tape.y + 32, 40, 20), (mm / 10).ToString(), numS);
                GUI.color = new Color(0.1f, 0.1f, 0.1f);
            }
        }
        // Край коробки: картон закрывает ленту справа от отметки
        float edge = tape.x + (realCm - windowStart) * pxPerCm;
        GUI.color = new Color(0.55f, 0.40f, 0.24f);
        GUI.DrawTexture(new Rect(edge, tape.y - 14, tape.xMax - edge + 20, tape.height + 28), Texture2D.whiteTexture);
        GUI.color = new Color(0.32f, 0.22f, 0.12f);
        GUI.DrawTexture(new Rect(edge, tape.y - 14, 3f, tape.height + 28), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.matrix = m;

        // Темнота: без фонарика после 18:00 ленту почти не видно
        float dark = 0f;
        if (ActController.Act >= 2) dark = Inventory.flashlightOn ? 0.15f : 0.88f;
        if (dark > 0f)
        {
            GUI.color = new Color(0f, 0f, 0.01f, dark);
            GUI.DrawTexture(new Rect(panel.x + 4, panel.y + 40, W - 8, 100), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        GUI.Label(new Rect(panel.x, panel.y + 145, W, 40), (typed.Length > 0 ? typed : "__") + " см", bigS);

        // Время
        float k = Mathf.Clamp01(timeLeft / timeTotal);
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(new Rect(panel.x + 20, panel.y + 192, W - 40, 6), Texture2D.whiteTexture);
        GUI.color = k > 0.3f ? UITheme.Accent : new Color(0.9f, 0.25f, 0.2f);
        GUI.DrawTexture(new Rect(panel.x + 20, panel.y + 192, (W - 40) * k, 6), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(panel.x, panel.y + 206, W, 26),
            "Цифры — набрать,  Backspace — стереть,  Enter — записать" + (ActController.Act >= 2 && !Inventory.flashlightOn ? "   ·   темно: включи фонарик (Q)" : ""), smallS);
    }
}
