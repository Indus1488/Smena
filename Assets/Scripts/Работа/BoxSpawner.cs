using UnityEngine;

// Вешается на объект BoxSpawn. Через несколько секунд после старта «приезжает селлер»
// и ставит коробку случайного размера. После каждой сделки приезжает следующий.
// Клавиша B (для проверки) даёт +1 готовую коробку.
public class BoxSpawner : MonoBehaviour
{
    public static BoxSpawner Instance;
    public static bool SellersStarted;   // пролог закончен, селлеры поехали

    public Material boxMaterial;       // подставляется построителем склада
    public Material labelMaterial;     // белая этикетка для QR (тоже подставляется построителем)
    public float firstDelay = 5f;      // через сколько секунд появится первая коробка
    public KeyCode debugKey = KeyCode.B;

    GameObject current;

    void Awake()
    {
        Instance = this;
        SellersStarted = false;
    }

    bool started;

    void Update()
    {
        if (SellersStarted && Hud.minutes >= GameConfig.rollsFrom - 10f && Hud.minutes < GameConfig.rollsTo) AnnounceRolls();

        // Пролог: сначала нужно убрать мусор у входа, потом приходит СМС и приезжает первый селлер
        if (!started && ReadyToStart())
        {
            started = true;
            SellersStarted = true;
            if (!GameConfig.skipPrologue && Inventory.trashTotal > 0)
                PhoneCall.Boss("boss_sellers", "Всё, первые селлеры подъехали. Начинай.");
            Invoke("SpawnBox", firstDelay);
        }

        // Тестовая клавиша B: +1 готовая коробка (сразу можно класть на паллету)
        if (GameConfig.debugKeys && Input.GetKeyDown(debugKey))
        {
            Inventory.boxesReady++;
            TrashManager.OnBoxReady();
            Hud.Say("Тест: +1 готовая коробка (всего " + Inventory.boxesReady + ")", 2f);
        }
    }

    bool ReadyToStart()
    {
        return GameConfig.skipPrologue || Inventory.trashTotal == 0 || Inventory.trashLeft <= 0;
    }

    // В акте 2 селлеры больше не приезжают
    public void Stop()
    {
        CancelInvoke();
        if (current != null) Destroy(current);
        enabled = false;
    }

    public void SpawnLater(float seconds)
    {
        CancelInvoke("SpawnBox");
        Invoke("SpawnBox", seconds);
    }

    public void SpawnBox()
    {
        CancelInvoke("SpawnBox");
        if (current != null) Destroy(current);
        if (Hud.minutes >= GameConfig.rollsFrom && Hud.minutes < GameConfig.rollsTo) { SpawnRoll(); return; }

        // Размеры в сантиметрах
        int length = Random.Range(30, 71);
        int width = Random.Range(25, 51);
        int height = Random.Range(20, 51);
        Vector3 size = new Vector3(length / 100f, height / 100f, width / 100f);

        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = "SellerBox";
        b.transform.position = transform.position + Vector3.up * (size.y / 2f);
        b.transform.rotation = Quaternion.Euler(0, Random.Range(-25f, 25f), 0);
        b.transform.localScale = size;
        if (boxMaterial != null) b.GetComponent<Renderer>().sharedMaterial = boxMaterial;
        MeasurableBox mb = b.AddComponent<MeasurableBox>();
        mb.labelMaterial = labelMaterial;

        current = b;
        Hud.HidePhone();
        Hud.Say("Приехал селлер. Коробка у входа.");
    }

    // Событие «рулоны» (GameConfig.rollsFrom..rollsTo): селлер привозит партию одинаковых рулонов.
    // Меряешь один — длину и диаметр. Коллайдер и размеры — у невидимого «кубика», виден цилиндр внутри.
    bool rollsAnnounced;

    // Начальник звонит за 10 минут до рулонов (или когда первый рулон уже приехал, если время перемотали)
    void AnnounceRolls()
    {
        if (rollsAnnounced) return;
        rollsAnnounced = true;
        PhoneCall.Boss("boss_rolls",
            "Слушай. С пяти до без двадцати шесть к тебе будут приезжать рулоны, партиями. Меряешь один рулон — длину и диаметр. Это FBS: на партию пара стикеров на доску. Рулоны клади на паллету вместе с коробками.",
            "С 17:00 до 17:40 пойдут рулоны, партиями. Меряй один рулон — длину и диаметр. Это FBS: на партию пара стикеров на доску. Складывай на паллету вместе с коробками.");
    }
    void SpawnRoll()
    {
        int length = Random.Range(60, 121);
        int dia = Random.Range(15, 31);
        Vector3 size = new Vector3(length / 100f, dia / 100f, dia / 100f);
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = "SellerRoll";
        b.transform.position = transform.position + Vector3.up * (size.y / 2f);
        b.transform.rotation = Quaternion.Euler(0, Random.Range(-25f, 25f), 0);
        b.transform.localScale = size;
        b.GetComponent<Renderer>().enabled = false;

        GameObject roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(roll.GetComponent<Collider>());
        roll.name = "Roll";                                    // ModelSwap подставит модель рулона, если она есть
        roll.transform.SetParent(b.transform, false);
        roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // ось цилиндра — вдоль длины
        roll.transform.localScale = new Vector3(1f, 0.5f, 1f);          // цилиндр Unity высотой 2 → ровно длина
        roll.GetComponent<Renderer>().sharedMaterial = PalletCargo.RollMaterial();

        MeasurableBox mb = b.AddComponent<MeasurableBox>();
        mb.isRoll = true;
        mb.rollCount = Random.Range(GameConfig.rollsMin, GameConfig.rollsMax + 1);
        current = b;
        Hud.HidePhone();
        AnnounceRolls();
        Hud.Say("Приехал селлер с партией рулонов. Они у входа.");
    }
}