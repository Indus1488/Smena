using UnityEngine;

// Интерфейс: часы, деньги, сообщения и «телефон» с выбором цены.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class Hud : MonoBehaviour
{
    public static Hud Instance;
    public static int money;
    public static bool hidden;      // финал прячет интерфейс
    public static int totalEarned;  // всего заработано за смену (для итогов)
    public static float minutes = 16f * 60f;       // игровое время в минутах от полуночи (16:00)
    public static float minutesPerSecond = 0.5f;   // сколько игровых минут проходит за реальную секунду

    string message = "";
    float messageUntil;
    bool phoneVisible;
    float phoneUntil;
    string phoneTitle = "", phoneLine1 = "", phoneLine2 = "";

    // Выбор цены
    bool pricing;
    int pricingFrame;
    MeasurableBox box;
    int price;

    GUIStyle big, normal, small, centered, phoneTitleStyle, phoneBox, popupStyle;

    // Отклик на деньги: сумма «набегает», рядом всплывает +N / −N
    float shownMoney;
    int lastHour = -1;
    float hourFlash;
    float flashUntil; bool flashPlus;
    struct Popup { public string text; public bool plus; public float born; }
    readonly System.Collections.Generic.List<Popup> popups = new System.Collections.Generic.List<Popup>();
    const float PopupLife = 1.8f;
    Texture2D phoneBg;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        money = 0;
        hidden = false;
        totalEarned = 0;
        minutes = 16f * 60f;
        GameObject go = new GameObject("HUD");
        Instance = go.AddComponent<Hud>();
    }

    void Update()
    {
        // В тестовом режиме время идёт быстро, в итоговой версии (fastTestTime = false) вдвое медленнее
        minutes += Time.deltaTime * (GameConfig.fastTestTime ? 0.5f : 0.25f);

        if (phoneUntil > 0f && Time.time > phoneUntil)
        {
            phoneVisible = false;
            phoneUntil = 0f;
        }

        if (pricing && box != null)
        {
            // Цена меняется колесом мыши или стрелками, шаг 10 руб.
            float scroll = Input.mouseScrollDelta.y;
            if (Input.GetKeyDown(KeyCode.RightArrow) || scroll > 0) price += 10;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || scroll < 0) price -= 10;
            price = Mathf.Clamp(price, box.basePrice, box.basePrice * 2);

            if (Time.frameCount > pricingFrame && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                pricing = false;
                box.Propose(price);
            }
        }
    }

    // ---- Что можно вызывать из других скриптов ----
    public static void Say(string text, float seconds = 3f)
    {
        if (Instance == null) return;
        Instance.message = text;
        Instance.messageUntil = Time.time + seconds;
    }

    public static void ShowPhone(string title, string line1, string line2, float seconds = 0f)
    {
        if (Instance == null) return;
        Instance.pricing = false;
        Instance.phoneTitle = title;
        Instance.phoneLine1 = line1;
        Instance.phoneLine2 = line2;
        Instance.phoneVisible = true;
        Instance.phoneUntil = seconds > 0f ? Time.time + seconds : 0f;
    }

    public static void StartPricing(MeasurableBox b)
    {
        if (Instance == null) return;
        Instance.box = b;
        Instance.price = b.basePrice;
        Instance.pricing = true;
        Instance.pricingFrame = Time.frameCount;   // Enter, которым записали замер, не должен сразу назвать цену
        Instance.phoneVisible = true;
        Instance.phoneUntil = 0f;
    }

    public static void HidePhone()
    {
        if (Instance == null) return;
        Instance.phoneVisible = false;
        Instance.pricing = false;
    }

    public static string TimeText()
    {
        int total = Mathf.FloorToInt(minutes);
        return string.Format("{0:00}:{1:00}", (total / 60) % 24, total % 60);
    }

    public static void AddMoney(int amount)
    {
        money += amount;
        if (amount > 0) totalEarned += amount;
        if (amount > 0) Sfx.Play("money", 0.7f);
        if (Instance != null && amount != 0)
        {
            Instance.popups.Add(new Popup { text = (amount > 0 ? "+" : "−") + Mathf.Abs(amount) + " руб.", plus = amount > 0, born = Time.unscaledTime });
            Instance.flashUntil = Time.unscaledTime + 0.6f;
            Instance.flashPlus = amount > 0;
        }
    }

    // ---- Рисование ----
    void MakeStyles()
    {
        big = new GUIStyle(UITheme.Label());
        big.fontSize = 34;
        big.fontStyle = FontStyle.Bold;
        big.normal.textColor = UITheme.Text;

        normal = new GUIStyle(UITheme.Label());
        normal.fontSize = 22;
        normal.normal.textColor = UITheme.Text;

        small = new GUIStyle(normal);
        small.fontSize = 16;
        small.normal.textColor = UITheme.Dim;

        centered = new GUIStyle(normal);
        centered.alignment = TextAnchor.MiddleCenter;
        centered.fontSize = 26;

        phoneTitleStyle = new GUIStyle(normal);
        phoneTitleStyle.fontSize = 24;
        phoneTitleStyle.fontStyle = FontStyle.Bold;
        phoneTitleStyle.normal.textColor = UITheme.Accent;

        phoneBg = new Texture2D(1, 1);
        phoneBg.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.08f, 0.92f));
        phoneBg.Apply();
        phoneBox = new GUIStyle(UITheme.Panel());
        phoneBox.normal.background = phoneBg;
    }

    void DrawMoney()
    {
        // Сумма догоняет настоящую: быстро при большой разнице, но всегда секунду-полторы
        float diff = money - shownMoney;
        if (Event.current.type != EventType.Repaint) { }
        else if (Mathf.Abs(diff) < 1f) shownMoney = money;
        else shownMoney += Mathf.Sign(diff) * Mathf.Max(Mathf.Abs(diff) * 3f, 40f) * Time.unscaledDeltaTime;
        if ((diff > 0 && shownMoney > money) || (diff < 0 && shownMoney < money)) shownMoney = money;

        Color c = normal.normal.textColor;
        if (Time.unscaledTime < flashUntil)
        {
            float k = (flashUntil - Time.unscaledTime) / 0.6f;
            normal.normal.textColor = Color.Lerp(c, flashPlus ? new Color(0.55f, 0.95f, 0.5f) : new Color(1f, 0.4f, 0.35f), k);
        }
        Shadowed(new Rect(20, 58, 300, 32), Mathf.RoundToInt(shownMoney) + " руб.", normal);
        normal.normal.textColor = c;

        if (popupStyle == null)
        {
            popupStyle = new GUIStyle(normal);
            popupStyle.fontStyle = FontStyle.Bold;
        }
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            Popup p = popups[i];
            float t = (Time.unscaledTime - p.born) / PopupLife;
            if (t >= 1f) { popups.RemoveAt(i); continue; }
            float a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            Color pc = p.plus ? new Color(0.55f, 0.95f, 0.5f, a) : new Color(1f, 0.4f, 0.35f, a);
            popupStyle.normal.textColor = new Color(0f, 0f, 0f, a);
            Rect r = new Rect(170, 58 - t * 26f, 220, 32);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), p.text, popupStyle);
            popupStyle.normal.textColor = pc;
            GUI.Label(r, p.text, popupStyle);
        }
    }

    void Shadowed(Rect r, string text, GUIStyle st)
    {
        Color c = st.normal.textColor;
        st.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
        st.normal.textColor = c;
        GUI.Label(r, text, st);
    }

    void OnGUI()
    {
        if (hidden) return;
        if (big == null) MakeStyles();

        int total = Mathf.FloorToInt(minutes);
        int h = (total / 60) % 24;
        int m = total % 60;
        // Часы: двоеточие мигает, на смене часа цифры на секунду вспыхивают янтарным и чуть увеличиваются
        if (h != lastHour) { if (lastHour >= 0) hourFlash = Time.unscaledTime + 1.2f; lastHour = h; }
        float hf = Mathf.Clamp01((hourFlash - Time.unscaledTime) / 1.2f);
        Color bc = big.normal.textColor; int bs = big.fontSize;
        big.normal.textColor = Color.Lerp(bc, UITheme.Accent, hf);
        big.fontSize = Mathf.RoundToInt(bs * (1f + 0.15f * hf));
        string colon = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? ":" : " ";
        Shadowed(new Rect(20, 14 - 4f * hf, 300, 44 + 8f * hf), string.Format("{0:00}{2}{1:00}", h, m, colon), big);
        big.normal.textColor = bc; big.fontSize = bs;
        DrawMoney();
        Shadowed(new Rect(20, 92, 400, 24), "Готово коробок: " + Inventory.boxesReady + (Inventory.rollsReady > 0 ? "   рулонов: " + Inventory.rollsReady : ""), small);
        Shadowed(new Rect(20, 116, 400, 24), "Паллет к отправке: " + Inventory.palletsReady, small);
        if (ActController.Act >= 3 && GameConfig.horror)
            Shadowed(new Rect(20, 164, 500, 24), "Пульс: " + Mathf.RoundToInt(Inventory.pulse) + "   V — отдышаться", small);
        if (Inventory.hasFlashlight)
            Shadowed(new Rect(20, 140, 500, 24),
                "Фонарик " + Mathf.CeilToInt(Inventory.flashlightBattery) + "%   ·   F — вкл/выкл   ·   R — батарейка", small);

        if (Time.time < messageUntil)
            Shadowed(new Rect(0, Screen.height * 0.75f, Screen.width, 44), message, centered);

        if (phoneVisible)
        {
            float height = pricing ? 330f : 230f;
            Rect r = new Rect(Screen.width - 400, Screen.height - height - 20, 380, height);
            GUI.Box(r, GUIContent.none, phoneBox);

            if (pricing && box != null)
            {
                int percent = Mathf.RoundToInt((price - box.basePrice) * 100f / box.basePrice);
                GUI.Label(new Rect(r.x + 20, r.y + 14, r.width - 40, 34), "ЦЕНА ПРИЁМКИ", phoneTitleStyle);
                GUI.Label(new Rect(r.x + 20, r.y + 60, r.width - 40, 30),
                    box.lengthCm + " x " + box.widthCm + " x " + box.heightCm + " см", normal);
                GUI.Label(new Rect(r.x + 20, r.y + 96, r.width - 40, 30), "Базовая: " + box.basePrice + " руб.", normal);
                GUI.Label(new Rect(r.x + 20, r.y + 132, r.width - 40, 30), "Твоя: " + price + " руб. (+" + percent + "%)", phoneTitleStyle);
                // Полоса-ползунок (двигается колесом мыши или стрелками)
                GUI.HorizontalSlider(new Rect(r.x + 20, r.y + 180, r.width - 40, 20), price, box.basePrice, box.basePrice * 2);
                GUI.Label(new Rect(r.x + 20, r.y + 215, r.width - 40, 24), "Колесо мыши или стрелки — цена", small);
                GUI.Label(new Rect(r.x + 20, r.y + 242, r.width - 40, 24), "Enter — назвать цену селлеру", small);
            }
            else
            {
                GUI.Label(new Rect(r.x + 20, r.y + 14, r.width - 40, 34), "ТЕЛЕФОН: " + phoneTitle, phoneTitleStyle);
                GUI.Label(new Rect(r.x + 20, r.y + 80, r.width - 40, 34), phoneLine1, normal);
                GUI.Label(new Rect(r.x + 20, r.y + 124, r.width - 40, 34), phoneLine2, normal);
            }
        }
    }
}
