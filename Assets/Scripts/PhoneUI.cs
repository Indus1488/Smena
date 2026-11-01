using System.Collections.Generic;
using UnityEngine;

// Телефон. Tab — открыть и закрыть. Мышь становится свободной, пока телефон открыт.
// Приложения: Сообщения (инструктаж, СМС), Магазин (паллеты, батареи), Газель (вывоз паллет).
// Из других скриптов: PhoneUI.AddMessage("Кто", "Текст");
// Внешний вид сейчас простой (IMGUI). Когда пришлёте референсы UI, перерисуем.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class PhoneUI : MonoBehaviour
{
    public static PhoneUI Instance;
    public static bool Locked;       // финал закрывает телефон
    public static int unread;

    class Msg
    {
        public string from;
        public string text;
        public string time;
    }
    static List<Msg> messages = new List<Msg>();

    bool open;
    int closedFrame = -1;

    // Телефон открыт (или закрылся в этом кадре — чтобы тот же Esc не открыл паузу)
    public static bool IsOpen
    {
        get { return Instance != null && (Instance.open || Instance.closedFrame == Time.frameCount); }
    }
    int screen;          // 0 главный экран, 1 сообщения, 2 магазин, 3 газель
    int openedMsg = -1;
    Vector2 scroll;
    PlayerController pc;
    Interactor interactor;
    GUIStyle titleS, rightS, normalS, smallS, buttonS, rowS, wrapS;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        messages = new List<Msg>();
        unread = 0;
        Locked = false;
        Instance = new GameObject("PhoneUI").AddComponent<PhoneUI>();
    }

    public static void AddMessage(string from, string text)
    {
        Msg m = new Msg();
        m.from = from;
        m.text = text;
        m.time = Hud.TimeText();
        messages.Insert(0, m);
        unread++;
        Sfx.Play("phone_msg", 0.8f);
    }

    void Start()
    {
        // Сообщения при старте смены (самое важное лежит внизу списка, потому что новые сверху)
        AddMessage("Инструктаж FBO", "На коробке две этикетки с QR. Номер поставки на обеих должен совпадать с заказом. Совпало — принимаешь. Не совпало — не принимай и сообщи селлеру.");
        AddMessage("Начальник", "Готовые коробки клади на паллету, обматывай плёнкой, вези рохлей в жёлтую зону на улице. Газель вызывай в приложении «Газель» (1000 руб.). Стартовая паллета одна, остальные покупай в магазине.");
        AddMessage("Начальник", "Смена с 16:00 до 20:00. Приезжают селлеры. Меряй коробку рулеткой, назначай цену на телефоне. После сделки: FBS — клей стикеры на доску, FBO — проверь QR на коробке.");
    }

    void Update()
    {
        if (Locked)
        {
            if (open) Close();
            return;
        }
        if (GameMenus.IsBlocking) return;
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (open) Close(); else Open();
        }
        if (open && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    void Open()
    {
        open = true;
        screen = 0;
        openedMsg = -1;

        GameObject p = GameObject.Find("Player");
        pc = p != null ? p.GetComponent<PlayerController>() : null;
        if (pc != null) pc.enabled = false;

        Camera cam = Camera.main;
        interactor = cam != null ? cam.GetComponent<Interactor>() : null;
        if (interactor != null) interactor.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Close()
    {
        open = false;
        closedFrame = Time.frameCount;
        if (pc != null && !PalletCargo.IsLifting()) pc.enabled = true;   // пока качают рохлю, камера стоит
        if (interactor != null) interactor.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------- Покупки ----------
    void BuyPallet()
    {
        if (Hud.money < GameConfig.pricePallet) return;
        Hud.AddMoney(-GameConfig.pricePallet);
        Inventory.palletStock++;
        Sfx.Play("money", 0.7f);
        Hud.Say("Куплена паллета (-" + GameConfig.pricePallet + " руб.). Она во дворе.", 3f);
    }

    void BuyBattery()
    {
        if (Hud.money < GameConfig.priceBattery) return;
        Hud.AddMoney(-GameConfig.priceBattery);
        Inventory.spareBatteries++;
        Sfx.Play("money", 0.7f);
        Hud.Say("Куплена батарея (-" + GameConfig.priceBattery + " руб.)", 3f);
    }

    void CallGazelle()
    {
        if (GazelleController.Busy) return;
        if (Inventory.palletsReady <= 0)
        {
            Hud.Say("В жёлтой зоне нет паллет.", 3f);
            return;
        }
        if (Hud.money < GameConfig.priceGazelle) return;

        GazelleController g = GazelleController.Find();
        if (g == null)
        {
            Hud.Say("Газель не найдена: пересоберите склад.", 4f);
            return;
        }
        Hud.AddMoney(-GameConfig.priceGazelle);
        g.Call();
        AddMessage("Газель", "Выехал. Буду через минуту.");
    }

    // ---------- Рисование ----------
    void MakeStyles()
    {
        titleS = new GUIStyle(GUI.skin.label);
        titleS.fontSize = 26;
        titleS.fontStyle = FontStyle.Bold;
        titleS.normal.textColor = Color.white;

        rightS = new GUIStyle(titleS);
        rightS.alignment = TextAnchor.MiddleRight;
        rightS.fontSize = 22;
        rightS.fontStyle = FontStyle.Normal;

        normalS = new GUIStyle(GUI.skin.label);
        normalS.fontSize = 20;
        normalS.normal.textColor = Color.white;

        smallS = new GUIStyle(normalS);
        smallS.fontSize = 16;
        smallS.normal.textColor = new Color(0.7f, 0.72f, 0.78f);

        buttonS = new GUIStyle(GUI.skin.button);
        buttonS.fontSize = 22;

        rowS = new GUIStyle(GUI.skin.button);
        rowS.fontSize = 18;
        rowS.alignment = TextAnchor.MiddleLeft;
        rowS.wordWrap = true;

        wrapS = new GUIStyle(normalS);
        wrapS.wordWrap = true;
    }

    static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    void OnGUI()
    {
        if (Locked || Hud.hidden) return;
        if (titleS == null) MakeStyles();

        if (!open)
        {
            if (unread > 0)
                GUI.Label(new Rect(Screen.width - 380, 14, 360, 30), "Сообщения: " + unread + "   (Tab — телефон)", rightS);
            return;
        }

        GUI.depth = -50;
        float s = Mathf.Clamp(Screen.height / 900f, 0.6f, 1.3f);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float vw = Screen.width / s;
        float vh = Screen.height / s;

        Rect body = new Rect(vw - 440f, (vh - 700f) / 2f, 400f, 700f);
        Fill(body, new Color(0.03f, 0.03f, 0.05f));
        Rect scr = new Rect(body.x + 14f, body.y + 14f, body.width - 28f, body.height - 28f);
        Fill(scr, new Color(0.09f, 0.11f, 0.15f));

        GUI.Label(new Rect(scr.x + 14f, scr.y + 8f, 160f, 34f), Hud.TimeText(), titleS);
        GUI.Label(new Rect(scr.x + scr.width - 214f, scr.y + 8f, 200f, 34f), Hud.money + " руб.", rightS);

        float x = scr.x + 14f;
        float w = scr.width - 28f;
        float y = scr.y + 58f;

        if (screen == 0) DrawHome(x, y, w);
        else if (screen == 1) DrawMessages(scr, x, y, w);
        else if (screen == 2) DrawShop(x, y, w);
        else DrawGazelle(x, y, w);

        // Кнопка «Назад» или «Закрыть» (в списке сообщений своя кнопка при открытом сообщении)
        bool detail = (screen == 1 && openedMsg >= 0);
        if (!detail)
        {
            string label = (screen == 0) ? "Закрыть (Tab)" : "Назад";
            if (GUI.Button(new Rect(x, scr.yMax - 62f, w, 48f), label, buttonS))
            {
                if (screen == 0) Close();
                else
                {
                    screen = 0;
                    openedMsg = -1;
                }
            }
        }

        GUI.matrix = oldMatrix;
    }

    void DrawHome(float x, float y, float w)
    {
        string msgLabel = "Сообщения" + (unread > 0 ? "   (" + unread + " новых)" : "");
        if (GUI.Button(new Rect(x, y, w, 72f), msgLabel, buttonS))
        {
            screen = 1;
            openedMsg = -1;
            unread = 0;
            scroll = Vector2.zero;
        }
        if (GUI.Button(new Rect(x, y + 88f, w, 72f), "Магазин", buttonS)) screen = 2;
        if (GUI.Button(new Rect(x, y + 176f, w, 72f), "Газель", buttonS)) screen = 3;

        GUI.Label(new Rect(x, y + 270f, w, 30f), "Запас батарей: " + Inventory.spareBatteries, smallS);
        GUI.Label(new Rect(x, y + 296f, w, 30f), "Паллет в жёлтой зоне: " + Inventory.palletsReady, smallS);
    }

    static string Short(string text, int n)
    {
        if (text.Length <= n) return text;
        return text.Substring(0, n) + "…";
    }

    void DrawMessages(Rect scr, float x, float y, float w)
    {
        float h = scr.yMax - y - 74f;

        if (openedMsg >= 0 && openedMsg < messages.Count)
        {
            Msg m = messages[openedMsg];
            GUI.Label(new Rect(x, y, w, 34f), m.from + "   " + m.time, titleS);
            float th = wrapS.CalcHeight(new GUIContent(m.text), w - 28f);
            scroll = GUI.BeginScrollView(new Rect(x, y + 44f, w, h - 44f + 24f), scroll, new Rect(0, 0, w - 20f, th + 10f));
            GUI.Label(new Rect(0, 0, w - 28f, th + 10f), m.text, wrapS);
            GUI.EndScrollView();
            if (GUI.Button(new Rect(x, scr.yMax - 62f, w, 48f), "К списку", buttonS))
            {
                openedMsg = -1;
                scroll = Vector2.zero;
            }
            return;
        }

        if (messages.Count == 0)
        {
            GUI.Label(new Rect(x, y, w, 30f), "Сообщений нет.", normalS);
            return;
        }

        scroll = GUI.BeginScrollView(new Rect(x, y, w, h), scroll, new Rect(0, 0, w - 20f, messages.Count * 84f));
        for (int i = 0; i < messages.Count; i++)
        {
            Msg m = messages[i];
            string label = m.from + "   " + m.time + "\n" + Short(m.text, 46);
            if (GUI.Button(new Rect(0, i * 84f, w - 20f, 78f), label, rowS))
            {
                openedMsg = i;
                scroll = Vector2.zero;
            }
        }
        GUI.EndScrollView();
    }

    void DrawShop(float x, float y, float w)
    {
        GUI.Label(new Rect(x, y, w, 34f), "Магазин", titleS);

        GUI.Label(new Rect(x, y + 50f, w, 30f), "Паллета", normalS);
        GUI.Label(new Rect(x, y + 80f, w, 26f), GameConfig.pricePallet + " руб.", smallS);
        GUI.enabled = Hud.money >= GameConfig.pricePallet;
        if (GUI.Button(new Rect(x, y + 110f, w, 52f), "Купить паллету", buttonS)) BuyPallet();
        GUI.enabled = true;

        GUI.Label(new Rect(x, y + 190f, w, 30f), "Батарея для фонарика (в запасе: " + Inventory.spareBatteries + ")", normalS);
        GUI.Label(new Rect(x, y + 220f, w, 26f), GameConfig.priceBattery + " руб.", smallS);
        GUI.enabled = Hud.money >= GameConfig.priceBattery;
        if (GUI.Button(new Rect(x, y + 250f, w, 52f), "Купить батарею", buttonS)) BuyBattery();
        GUI.enabled = true;

        GUI.Label(new Rect(x, y + 330f, w, 60f), "Стартовая паллета одна. Остальные покупаются.", smallS);
    }

    void DrawGazelle(float x, float y, float w)
    {
        GUI.Label(new Rect(x, y, w, 34f), "Газель", titleS);
        GUI.Label(new Rect(x, y + 50f, w, 60f), "Приедет и заберёт все паллеты из жёлтой зоны.", wrapS);
        GUI.Label(new Rect(x, y + 120f, w, 28f), "Цена вызова: " + GameConfig.priceGazelle + " руб.", normalS);
        GUI.Label(new Rect(x, y + 152f, w, 28f), "Паллет в зоне: " + Inventory.palletsReady, normalS);

        bool canCall = !GazelleController.Busy && Inventory.palletsReady > 0 && Hud.money >= GameConfig.priceGazelle;
        GUI.enabled = canCall;
        if (GUI.Button(new Rect(x, y + 200f, w, 56f), "Вызвать газель", buttonS)) CallGazelle();
        GUI.enabled = true;

        string status = GazelleController.Busy ? "Газель в пути…"
            : (Inventory.palletsReady <= 0 ? "Сначала поставьте паллету в жёлтую зону."
            : (Hud.money < GameConfig.priceGazelle ? "Не хватает денег." : "Можно вызывать."));
        GUI.Label(new Rect(x, y + 270f, w, 60f), status, smallS);
    }
}
