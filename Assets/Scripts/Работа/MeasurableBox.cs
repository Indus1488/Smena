using UnityEngine;
using UnityEngine.Events;

public enum OrderType { None, FBS, FBO }

// Коробка селлера. Путь коробки:
// 1) замер рулеткой (длина, ширина, высота) -> 2) выбор цены на телефоне -> 3) сделка ->
// 4) задание: FBS (клеить стикеры на доску) или FBO (проверить QR на коробке) -> 5) коробка готова.
[RequireComponent(typeof(Interactable))]
public class MeasurableBox : MonoBehaviour
{
    public static MeasurableBox ActiveFbsBox;   // коробка, для которой сейчас нужны стикеры

    public int lengthCm, widthCm, heightCm;
    public int basePrice;
    public bool measured;
    public OrderType order;
    public int workLeft;
    public Material labelMaterial;              // белая этикетка для QR (задаёт BoxSpawner)
    public bool isRoll;                         // партия рулонов: меряем один (длина и диаметр), потом FBS
    public int rollCount;                       // сколько рулонов в партии

    int realL, realW, realH;   // настоящие размеры (то, что игрок вводит, — в lengthCm/widthCm/heightCm)
    bool overMeasured;         // намерил заметно больше настоящего — селлер пожалуется

    // FBO: номер поставки в заказе и на двух этикетках (иногда на одной этикетке номер с ошибкой)
    string orderNo, labelA, labelB, article;
    int labelSeed;
    bool labelsMatch;

    int step;   // 0 длина, 1 ширина, 2 высота, 3 выбор цены, 4 сделка отклонена, 5 работа, 6 готово
    Interactable it;

    public int WorkLeft { get { return workLeft; } }

    void Awake()
    {
        it = GetComponent<Interactable>();
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(OnInteract);

        // Размеры берём из масштаба куба (1 единица = 1 метр)
        Vector3 s = transform.localScale;
        realL = Mathf.RoundToInt(Mathf.Max(s.x, s.z) * 100f);
        realW = Mathf.RoundToInt(Mathf.Min(s.x, s.z) * 100f);
        realH = Mathf.RoundToInt(s.y * 100f);
        lengthCm = realL; widthCm = realW; heightCm = realH;

        // Цена зависит от объёма: 100 руб. + 3 руб. за литр, округляем до десятков
        float liters = lengthCm * widthCm * heightCm / 1000f;
        basePrice = Mathf.RoundToInt((100f + liters * 3f) * ActController.PriceFactor / 10f) * 10;

        UpdatePrompt();
    }

    void OnDestroy()
    {
        if (ActiveFbsBox == this) ActiveFbsBox = null;
    }

    void Update()
    {
        // Пока не измерили первое, подсказка меняется, когда игрок берёт рулетку
        if (step == 0) UpdatePrompt();
    }

    void UpdatePrompt()
    {
        switch (step)
        {
            case 0: it.prompt = Inventory.hasTape ? (isRoll ? "E — измерить длину рулона" : "E — измерить длину") : "Нужна рулетка"; break;
            case 1: it.prompt = isRoll ? "E — измерить диаметр" : "E — измерить ширину"; break;
            case 2: it.prompt = "E — измерить высоту"; break;
            case 3: it.prompt = "Назови цену в телефоне"; break;
            case 5:
                it.prompt = (order == OrderType.FBO)
                    ? "E — сверить этикетки с заказом"
                    : "Стикеры клеим на доску у стены";
                break;
            default: it.prompt = ""; break;
        }
    }

    void OnInteract()
    {
        if (step == 5)
        {
            DoWorkOnBox();
            return;
        }
        if (!Inventory.hasTape)
        {
            Hud.Say("Нужна рулетка. Она на столе у правой стены.");
            return;
        }
        if (step >= 3 || TapeMeasureScreen.IsOpen) return;

        Sfx.Play("tape_measure", 0.8f);
        int dim = step;
        string what = dim == 0 ? "Длина" : (dim == 1 ? (isRoll ? "Диаметр" : "Ширина") : "Высота");
        int real = dim == 0 ? realL : (dim == 1 ? realW : realH);
        TapeMeasureScreen.Open(what, real, (v, timeout) => OnMeasured(dim, what, v, timeout));
    }

    // Игрок считал и ввёл число
    void OnMeasured(int dim, string what, int v, bool timeout)
    {
        if (this == null) return;
        if (timeout) Hud.Say(Seller("wait", "Селлер: «Долго ещё?»", "Селлер: «Можно побыстрее?»", "Селлер вздыхает и смотрит на часы."), 3f);
        if (v <= 0) v = (dim == 0 ? realL : (dim == 1 ? realW : realH)) + Random.Range(-15, 16);   // ничего не набрал — «на глаз»
        if (dim == 0) lengthCm = v; else if (dim == 1) widthCm = v; else heightCm = v;
        Hud.Say(what + ": " + v + " см", 2f);

        step++;
        if (isRoll && step == 2) { heightCm = widthCm; step = 3; }   // у рулона высота = диаметр
        UpdatePrompt();

        if (step == 3)
        {
            measured = true;
            // Цена — по тому, что ты намерил. Намерил больше настоящего — селлер это заметит.
            float liters = lengthCm * widthCm * heightCm / 1000f;
            basePrice = Mathf.Max(50, Mathf.RoundToInt((100f + liters * 3f) * ActController.PriceFactor / 10f) * 10);
            float realLiters = realL * realW * realH / 1000f;
            overMeasured = liters > realLiters * 1.12f;
            Hud.StartPricing(this);
        }
    }

    static string Pick(params string[] v) { return v[Random.Range(0, v.Length)]; }

    // Фраза селлера + его голос, если записан: Resources/Voice/seller_<группа>_<номер варианта>
    string Seller(string group, params string[] v)
    {
        int i = Random.Range(0, v.Length);
        if (v[i].Contains("«")) Sfx.Voice("seller_" + group + "_" + (i + 1), transform.position, 1f);
        return v[i];
    }

    // Жалобы селлеров приходят начальнику, а он звонит тебе
    static void AngerSms()
    {
        int n = Inventory.sellerAnger;
        if (n == 1) PhoneCall.Boss("boss_complaint_1", "На тебя селлер пожаловался. Не задирай цены.");
        else if (n < 4) PhoneCall.Boss("boss_complaint_again", "Опять жалоба на тебя. Ты что там творишь?", "Ещё жалоба. Это уже " + n + "-я за смену.");
        else PhoneCall.Boss("boss_complaint_many", "Жалоб на тебя уже куча. Селлеры тебя запомнили. Будь осторожнее, когда стемнеет.",
                            "Жалоб уже " + n + ". Селлеры тебя запомнили. Будь осторожнее, когда стемнеет.");
    }

    // ---------- Сделка ----------
    // Вызывается из телефона, когда игрок нажал Enter
    public void Propose(int price)
    {
        if (overMeasured)
        {
            Inventory.sellerAnger++;
            AngerSms();
            Hud.Say(Seller("wrong", "Селлер: «Вы неправильно померили! Тут меньше!»", "Селлер перемеряет сам и качает головой.", "Селлер: «Я напишу жалобу. Коробка меньше.»"), 4f);
            overMeasured = false;
        }
        float markup = (price - basePrice) / (float)basePrice;
        float tolerance = Random.Range(0.20f, 0.40f) * ActController.AngerFactor;   // сколько наценки этот селлер готов вытерпеть

        if (markup > tolerance)
        {
            // Слишком дорого: селлер уезжает без сделки
            step = 4;
            UpdatePrompt();
            Inventory.sellerAnger++;
            AngerSms();
            Hud.Say(Seller("refuse", "Селлер: «Вы с ума сошли? Поеду в другое место.»", "Селлер: «За такие деньги я сам отвезу.»", "Селлер: «Грабёж. До свидания.»", "Селлер молча забирает коробку и уходит."), 4f);
            Hud.ShowPhone("ответ", "Селлер отказался", "Заработано: 0 руб.");
            Destroy(gameObject, 3f);
            if (BoxSpawner.Instance != null) BoxSpawner.Instance.SpawnLater(7f);
            return;
        }

        if (markup > 0.08f)
        {
            Inventory.sellerAnger++;
            AngerSms();
            Hud.AddMoney(price);
            Hud.Say(Seller("paid_angry", "Селлер молча платит. Он это запомнит.", "Селлер: «Ладно... Я вас запомнил.»", "Селлер платит и долго смотрит тебе в глаза.", "Селлер: «Дорого у вас. Но выбора нет.»"), 4f);
        }
        else
        {
            Hud.AddMoney(price);
            Hud.Say(Seller("paid_ok", "Селлер: «Спасибо, всё честно.»", "Селлер: «Нормально. Буду к вам возить.»", "Селлер: «Отлично, спасибо.»", "Селлер кивает и платит."), 4f);
        }

        Invoke("StartWork", 2.5f);   // сначала даём прочитать реакцию, потом выдаём задание
        step = 4;
        UpdatePrompt();
    }

    // ---------- Задание ----------
    void StartWork()
    {
        if (isRoll)
        {
            // Рулоны — всегда FBS: на всю партию пара стикеров на доску
            step = 5;
            order = OrderType.FBS;
            workLeft = GameConfig.rollStickers;
            ActiveFbsBox = this;
            Hud.ShowPhone("задача", "FBS: рулоны, " + rollCount + " шт.", "Стикеров на доску: " + workLeft);
            UpdatePrompt();
            return;
        }
        step = 5;
        order = (Random.value < 0.5f) ? OrderType.FBS : OrderType.FBO;

        if (order == OrderType.FBS)
        {
            workLeft = Random.Range(3, 6);
            ActiveFbsBox = this;
            Hud.ShowPhone("задача", "FBS: клей стикеры на доску", "Осталось: " + workLeft);
        }
        else
        {
            workLeft = 1;
            MakeNumbers();
            AddLabel();
            Hud.ShowPhone("задача", "FBO: поставка № " + orderNo, "Сверь номер на обеих этикетках (E)");
        }
        UpdatePrompt();
    }

    void DoWorkOnBox()
    {
        if (order != OrderType.FBO)
        {
            Hud.Say("Стикеры клеятся на доску над столом у правой стены.");
            return;
        }
        if (LabelCheck.IsOpen || Time.time - LabelCheck.ClosedAt < 0.3f) return;   // то же нажатие E
        Sfx.Play("scan", 0.6f);
        LabelCheck.Open(orderNo, labelA, labelB, labelSeed, article, OnChecked);
    }

    // Ответ игрока: true — «совпадает», false — «не совпадает», null — отложил
    void OnChecked(bool? saidMatch)
    {
        if (saidMatch == null) { Hud.Say("Проверка отложена."); return; }
        if (saidMatch.Value)
        {
            if (!labelsMatch)
            {
                // Пропустил пересорт: сейчас никто не заметит, штраф придёт позже
                Penalty.Later(Random.Range(25f, 45f), 300,
                    "Пересорт по поставке № " + orderNo + ": на коробке был чужой номер. Штраф 300 руб. Смотри внимательнее.",
                    "boss_fine_mismatch", "Ты пересорт пропустил. На коробке чужой номер был. Триста рублей штраф. Смотри внимательнее.");
            }
            Sfx.Play("scan", 0.8f);
            Hud.Say("Принято.");
            FinishWork();
        }
        else
        {
            if (!labelsMatch)
            {
                Hud.Say(Seller("relabel", "Селлер: «Блин, перепутали…» Переклеил этикетку.",
                             "Селлер вздохнул и переклеил этикетку.",
                             "Селлер: «Ой. Сейчас исправлю.» Этикетка переклеена."), 4f);
            }
            else
            {
                Inventory.sellerAnger++;
                Hud.Say(Seller("match", "Селлер: «Всё же совпадает! Глаза разуй.»",
                             "Селлер: «Там тот же номер. Ты издеваешься?»",
                             "Селлер молча тычет пальцем в номер. Совпадает."), 4f);
                PhoneCall.Boss("boss_complaint_reject", "Селлер жалуется: ты завернул нормальную поставку. Сверяй номера внимательнее.");
            }
            FinishWork();
        }
    }

    // Номер поставки вида 4815-273; в трети случаев на одной этикетке ошибка в одну цифру или переставлены две соседние
    void MakeNumbers()
    {
        orderNo = Random.Range(1000, 10000) + "-" + Random.Range(100, 1000);
        article = Random.Range(1000000, 9999999).ToString();
        labelSeed = Random.Range(1, 100000);
        labelA = orderNo; labelB = orderNo;
        labelsMatch = Random.value > 0.33f;
        if (!labelsMatch)
        {
            string bad = Spoil(orderNo);
            if (Random.value < 0.5f) labelA = bad; else labelB = bad;
        }
    }

    static string Spoil(string n)
    {
        char[] c = n.ToCharArray();
        for (int tries = 0; tries < 20; tries++)
        {
            int i = Random.Range(0, c.Length - 1);
            if (c[i] == '-') continue;
            if (Random.value < 0.5f && c[i + 1] != '-' && c[i + 1] != c[i])
            {
                char t = c[i]; c[i] = c[i + 1]; c[i + 1] = t;   // переставлены две соседние цифры
                return new string(c);
            }
            char d = (char)('0' + Random.Range(0, 10));
            if (d != c[i]) { c[i] = d; return new string(c); }   // одна цифра другая
        }
        return n + "1";
    }

    // Вызывается доской, когда на неё наклеен стикер
    public void OnStickerStuck()
    {
        workLeft--;
        if (workLeft > 0)
        {
            Hud.Say("Стикер на доске. Осталось: " + workLeft);
            Hud.ShowPhone("задача", "FBS: клей стикеры на доску", "Осталось: " + workLeft);
        }
        else
        {
            Hud.Say("Все стикеры на доске.");
            FinishWork();
        }
    }

    void FinishWork()
    {
        step = 6;
        UpdatePrompt();
        ActiveFbsBox = null;
        if (isRoll)
        {
            Inventory.rollsReady += rollCount;
            Hud.ShowPhone("задача", "Рулоны готовы: " + rollCount + " шт.", "Готово рулонов: " + Inventory.rollsReady);
        }
        else
        {
            Inventory.boxesReady++;
            TrashManager.OnBoxReady();
            Hud.ShowPhone("задача", "Коробка готова", "Готово коробок: " + Inventory.boxesReady);
        }
        Invoke("Leave", 1.5f);
    }

    void Leave()
    {
        // стикеры остаются на доске (StickerBoard сам срывает старые, когда места кончаются)
        if (BoxSpawner.Instance != null) BoxSpawner.Instance.SpawnLater(4f);
        Destroy(gameObject);   // позже коробка будет отправляться в зону паллеты
    }

    // Две этикетки с QR и штрихкодом: сверху и на боковой грани (картинку рисует LabelArt)
    void AddLabel()
    {
        // Свой непрозрачный материал: в сцене сюда подставлен материал плёнки (он прозрачный, этикетку не было видно)
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material m = sh != null ? new Material(sh) : (labelMaterial != null ? new Material(labelMaterial) : null);
        if (m == null) return;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        Texture2D tex = LabelArt.Sticker(labelSeed);
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        // Плоские квадраты, а не кубики: TextureNoStretch переразмечает текстуры у всех кубов «в метрах», и на кубике
        // от этикетки оставался один край картинки (пустая бумага)
        Vector3 s = transform.localScale;
        Label(m, new Vector3(0, 0.5f + 0.004f / s.y, 0), new Vector3(0.25f / s.x, 0.17f / s.z, 1f), Quaternion.Euler(90f, 0f, 0f));   // сверху
        Label(m, new Vector3(0, 0.05f, -0.5f - 0.004f / s.z), new Vector3(0.25f / s.x, 0.17f / s.y, 1f), Quaternion.identity);       // сбоку
    }

    void Label(Material m, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        GameObject l = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(l.GetComponent<Collider>());
        l.name = "QRLabel";
        l.transform.SetParent(transform, false);
        l.transform.localPosition = pos;
        l.transform.localRotation = rot;
        l.transform.localScale = scale;
        l.GetComponent<Renderer>().sharedMaterial = m;
    }
}
