using UnityEngine;

// Вешается на объект PalletSpot (жёлтая метка на полу).
// Если на метке нет паллеты, через секунду там появляется новая пустая.
// Рохля возвращается через инвентарь: выбрать и нажать X.
public class PalletSpot : MonoBehaviour
{
    public static PalletSpot Instance;

    public Material palletMaterial;   // подставляются построителем склада
    public Material boxMaterial;
    public Material filmMaterial;

    float nextCheck;
    bool firstDone;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 1f;
            if (!PalletCargo.AnyNear(transform.position, 0.9f))
            {
                if (!firstDone)
                {
                    firstDone = true;
                    Spawn();                       // стартовая паллета (бесплатная)
                }
                else if (Inventory.palletStock > 0)
                {
                    Inventory.palletStock--;       // следующие паллеты нужно покупать
                    Spawn();
                }
            }
        }

        // Рохлю возвращают через инвентарь: выбрать её и нажать X (InventoryBar)

        // Тестовая клавиша: сразу получить 4 готовые коробки
        if (GameConfig.debugKeys && Input.GetKeyDown(KeyCode.P))
        {
            Inventory.boxesReady += 4;
            Hud.Say("Тест: +4 готовые коробки", 2f);
        }
    }

    void Spawn()
    {
        GameObject go = new GameObject("PalletCargo");
        go.transform.position = transform.position;
        go.transform.rotation = transform.rotation;
        PalletCargo p = go.AddComponent<PalletCargo>();
        p.Init(palletMaterial, boxMaterial, filmMaterial);
    }
}