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
        // Пролог: сначала нужно убрать мусор у входа, потом приходит СМС и приезжает первый селлер
        if (!started && ReadyToStart())
        {
            started = true;
            SellersStarted = true;
            if (!GameConfig.skipPrologue && Inventory.trashTotal > 0)
                PhoneUI.AddMessage("Начальник", "Первые селлеры подъехали. Начинай.");
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
}