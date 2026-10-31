using UnityEngine;

// Вешается на объект PalletSpot (жёлтая метка на полу).
// Если на метке нет паллеты, через секунду там появляется новая пустая.
// R — вернуть рохлю на место, если она у вас в руках и вы ничего не везёте.
public class PalletSpot : MonoBehaviour
{
    public static PalletSpot Instance;

    public Material palletMaterial;   // подставляются построителем склада
    public Material boxMaterial;
    public Material filmMaterial;

    float nextCheck;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 1f;
            if (!PalletCargo.AnyNear(transform.position, 0.9f)) Spawn();
        }

        // Вернуть рохлю на место
        if (Input.GetKeyDown(KeyCode.R) && Inventory.hasJack && !PalletCargo.IsBusy())
        {
            Inventory.hasJack = false;
            if (WorkJack.Instance != null) WorkJack.Instance.Return();
            Hud.Say("Рохля возвращена на место.", 2f);
        }

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
