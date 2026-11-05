using System;
using UnityEngine;

// Проверка FBO: крупно две этикетки с коробки и номер поставки из заказа.
// Игрок сам сверяет номера: Enter — совпадает (принять), Backspace — не совпадает (вернуть селлеру), E — отложить.
// Темно (после 18:00) без фонарика → этикетки почти не видно.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class LabelCheck : MonoBehaviour
{
    public static LabelCheck Instance;
    public static bool IsOpen { get { return Instance != null && Instance.open; } }
    public static float ClosedAt = -10f;

    bool open;
    float openedAt;
    string order, a, b, art;
    int seedA, seedB;
    Texture2D qrA, qrB, barA, barB;
    Action<bool?> done;      // true — «совпадает», false — «не совпадает», null — отложил
    GUIStyle titleS, textS, numS, smallS, hintS;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Instance = new GameObject("LabelCheck").AddComponent<LabelCheck>();
    }

    public static void Open(string orderNo, string labelA, string labelB, int seed, string article, Action<bool?> onDone)
    {
        if (Instance == null) return;
        LabelCheck c = Instance;
        c.Clear();
        c.open = true;
        c.openedAt = Time.time;
        c.order = orderNo; c.a = labelA; c.b = labelB; c.art = article;
        c.seedA = seed; c.seedB = seed + 1;
        c.qrA = LabelArt.QR(c.seedA); c.qrB = LabelArt.QR(c.seedB);
        c.barA = LabelArt.Barcode(c.seedA); c.barB = LabelArt.Barcode(c.seedB);
        c.done = onDone;
        PlayerController.MoveLocked = true;
    }

    void Update()
    {
        if (!open || Time.time - openedAt < 0.2f) return;   // то же нажатие E не закрывает сразу
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Close(true);
        else if (Input.GetKeyDown(KeyCode.Backspace)) Close(false);
        else if (Input.GetKeyDown(KeyCode.E)) Close(null);
    }

    void Close(bool? answer)
    {
        open = false;
        ClosedAt = Time.time;
        PlayerController.MoveLocked = HidingSpot.Current != null;
        Action<bool?> cb = done;
        done = null;
        Clear();
        if (cb != null) cb(answer);
    }

    void Clear()
    {
        foreach (Texture2D t in new[] { qrA, qrB, barA, barB }) if (t != null) Destroy(t);
        qrA = qrB = barA = barB = null;
    }

    void Styles()
    {
        titleS = new GUIStyle(UITheme.Label()); titleS.fontSize = 34; titleS.fontStyle = FontStyle.Bold;
        titleS.alignment = TextAnchor.MiddleCenter; titleS.normal.textColor = UITheme.Text;
        hintS = new GUIStyle(titleS); hintS.fontSize = 20; hintS.fontStyle = FontStyle.Normal; hintS.normal.textColor = UITheme.Dim;
        textS = new GUIStyle(UITheme.Label()); textS.fontSize = 18; textS.normal.textColor = new Color(0.1f, 0.1f, 0.1f);
        smallS = new GUIStyle(textS); smallS.fontSize = 14;
        numS = new GUIStyle(textS); numS.fontSize = 30; numS.fontStyle = FontStyle.Bold;
    }

    void OnGUI()
    {
        if (!open) return;
        if (titleS == null) Styles();
        GUI.depth = -150;
        float sw = Screen.width, sh = Screen.height;

        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(0, sh * 0.08f, sw, 50), "ПРОВЕРКА FBO", titleS);
        GUI.Label(new Rect(0, sh * 0.08f + 52, sw, 40), "Заказ в телефоне:  поставка № " + order, titleS);

        // Две этикетки рядом, чуть повёрнуты (как на коробке)
        float w = Mathf.Min(420f, sw * 0.38f), h = w * 0.68f;
        float y = sh * 0.30f;
        float tremor = CameraFeel.Tremor;
        DrawLabel(new Rect(sw / 2f - w - 30f, y, w, h), a, qrA, barA, -3f + Jit(1f, tremor), seedA);
        DrawLabel(new Rect(sw / 2f + 30f, y + 20f, w, h), b, qrB, barB, 2.5f + Jit(2f, tremor), seedB);

        // Темно без фонарика — почти ничего не видно
        if (ActController.Act >= 2 && !Inventory.flashlightOn)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(0, y - 20f, sw, h + 60f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, y + h + 50f, sw, 30), "Темно. Включи фонарик (F), чтобы разглядеть.", hintS);
        }

        GUI.Label(new Rect(0, sh - 120f, sw, 30), "Enter — номера совпадают, принять   ·   Backspace — не совпадают, вернуть селлеру", hintS);
        GUI.Label(new Rect(0, sh - 88f, sw, 30), "E — отложить", hintS);
    }

    static float Jit(float k, float tremor)
    {
        return (Mathf.PerlinNoise(Time.time * 9f, k) - 0.5f) * 6f * tremor;
    }

    void DrawLabel(Rect r, string number, Texture2D qr, Texture2D bar, float angle, int seed)
    {
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, r.center);

        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(r.x + 6, r.y + 8, r.width, r.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.94f, 0.9f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.08f, 0.08f, 0.09f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * 0.14f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle head = new GUIStyle(smallS); head.normal.textColor = Color.white; head.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(r.x + 12, r.y + 2, r.width - 24, r.height * 0.14f), "FBO   ·   ПОСТАВКА НА СКЛАД", head);

        float q = r.height * 0.52f;
        GUI.DrawTexture(new Rect(r.x + 12, r.y + r.height * 0.18f, q, q), qr);
        float tx = r.x + 24 + q;
        GUI.Label(new Rect(tx, r.y + r.height * 0.18f, r.width, 24), "Поставка", textS);
        GUI.Label(new Rect(tx, r.y + r.height * 0.18f + 22, r.width, 40), "№ " + number, numS);
        GUI.Label(new Rect(tx, r.y + r.height * 0.18f + 62, r.width, 24), "Короб 1/1", textS);
        GUI.Label(new Rect(tx, r.y + r.height * 0.18f + 84, r.width, 24), "Артикул " + art, smallS);

        Rect br = new Rect(r.x + 12, r.y + r.height * 0.75f, r.width - 24, r.height * 0.14f);
        GUI.DrawTexture(br, bar, ScaleMode.StretchToFill);
        GUI.Label(new Rect(br.x, br.yMax, br.width, 20), "4 " + (460000000 + seed % 99999999) + " 7", smallS);

        GUI.matrix = m;
    }
}
