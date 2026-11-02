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
        lengthCm = Mathf.RoundToInt(Mathf.Max(s.x, s.z) * 100f);
        widthCm = Mathf.RoundToInt(Mathf.Min(s.x, s.z) * 100f);
        heightCm = Mathf.RoundToInt(s.y * 100f);

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
            case 0: it.prompt = Inventory.hasTape ? "E — измерить длину" : "Нужна рулетка"; break;
            case 1: it.prompt = "E — измерить ширину"; break;
            case 2: it.prompt = "E — измерить высоту"; break;
            case 3: it.prompt = "Назови цену в телефоне"; break;
            case 5:
                it.prompt = (order == OrderType.FBO)
                    ? "E — проверить QR (осталось " + workLeft + ")"
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
        if (step >= 3) return;

        Sfx.Play("tape_measure", 0.8f);
        if (step == 0) Hud.Say("Длина: " + lengthCm + " см");
        else if (step == 1) Hud.Say("Ширина: " + widthCm + " см");
        else Hud.Say("Высота: " + heightCm + " см");

        step++;
        UpdatePrompt();

        if (step == 3)
        {
            measured = true;
            Hud.StartPricing(this);
        }
    }

    static string Pick(params string[] v) { return v[Random.Range(0, v.Length)]; }

    // Жалобы селлеров приходят начальнику, а он пишет тебе
    static void AngerSms()
    {
        int n = Inventory.sellerAnger;
        string text = n == 1 ? "На тебя пожаловался селлер. Не задирай цены."
            : n < 4 ? "Ещё жалоба. Это уже " + n + "-я за смену."
            : "Жалоб уже " + n + ". Селлеры тебя запомнили. Будь осторожнее, когда стемнеет.";
        PhoneUI.AddMessage("Начальник", text);
    }

    // ---------- Сделка ----------
    // Вызывается из телефона, когда игрок нажал Enter
    public void Propose(int price)
    {
        float markup = (price - basePrice) / (float)basePrice;
        float tolerance = Random.Range(0.20f, 0.40f) * ActController.AngerFactor;   // сколько наценки этот селлер готов вытерпеть

        if (markup > tolerance)
        {
            // Слишком дорого: селлер уезжает без сделки
            step = 4;
            UpdatePrompt();
            Inventory.sellerAnger++;
            AngerSms();
            Hud.Say(Pick("Селлер: «Вы с ума сошли? Поеду в другое место.»", "Селлер: «За такие деньги я сам отвезу.»", "Селлер: «Грабёж. До свидания.»", "Селлер молча забирает коробку и уходит."), 4f);
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
            Hud.Say(Pick("Селлер молча платит. Он это запомнит.", "Селлер: «Ладно... Я вас запомнил.»", "Селлер платит и долго смотрит тебе в глаза.", "Селлер: «Дорого у вас. Но выбора нет.»"), 4f);
        }
        else
        {
            Hud.AddMoney(price);
            Hud.Say(Pick("Селлер: «Спасибо, всё честно.»", "Селлер: «Нормально. Буду к вам возить.»", "Селлер: «Отлично, спасибо.»", "Селлер кивает и платит."), 4f);
        }

        Invoke("StartWork", 2.5f);   // сначала даём прочитать реакцию, потом выдаём задание
        step = 4;
        UpdatePrompt();
    }

    // ---------- Задание ----------
    void StartWork()
    {
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
            workLeft = 2;
            AddLabel();
            Hud.ShowPhone("задача", "FBO: проверь QR на коробке", "Осталось: " + workLeft);
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

        workLeft--;
        if (workLeft > 0)
        {
            Sfx.Play("scan", 0.8f);
            Hud.Say("Сканирую QR…");
            Hud.ShowPhone("задача", "FBO: проверь QR на коробке", "Осталось: " + workLeft);
            UpdatePrompt();
        }
        else
        {
            Sfx.Play("scan", 0.8f);
            Hud.Say("QR совпал с заказом.");
            FinishWork();
        }
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
        Inventory.boxesReady++;
        TrashManager.OnBoxReady();
        Hud.ShowPhone("задача", "Коробка готова", "Готово коробок: " + Inventory.boxesReady);
        Invoke("Leave", 1.5f);
    }

    void Leave()
    {
        // стикеры остаются на доске (StickerBoard сам срывает старые, когда места кончаются)
        if (BoxSpawner.Instance != null) BoxSpawner.Instance.SpawnLater(4f);
        Destroy(gameObject);   // позже коробка будет отправляться в зону паллеты
    }

    // Белая этикетка с QR на верхней грани коробки
    void AddLabel()
    {
        if (labelMaterial == null) return;
        Vector3 s = transform.localScale;
        GameObject l = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(l.GetComponent<Collider>());
        l.name = "QRLabel";
        l.transform.SetParent(transform, false);
        l.transform.localPosition = new Vector3(0, 0.5f + 0.005f / s.y, 0);
        l.transform.localScale = new Vector3(0.25f / s.x, 0.01f / s.y, 0.18f / s.z);
        l.GetComponent<Renderer>().sharedMaterial = labelMaterial;
    }
}
