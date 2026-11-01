using UnityEngine;
using UnityEngine.Events;

// Кусок мусора на полу склада. Со шваброй в руках E убирает его в мусорный мешок.
// Мешок вмещает GameConfig.trashBagSize кусков; полный мешок нужно выкинуть в мусорку под эстакадой.
// Стартовый мусор считается для пролога (пока его не убрали, селлеры не едут).
// Мусор, который появляется после готовых коробок, создаёт TrashManager.
[RequireComponent(typeof(Interactable))]
public class TrashPiece : MonoBehaviour
{
    public static bool nextIsExtra;   // TrashManager ставит true перед созданием нового куска

    Interactable it;
    bool extra;

    void Awake()
    {
        extra = nextIsExtra;
        nextIsExtra = false;
        if (!extra)
        {
            Inventory.trashTotal++;
            Inventory.trashLeft++;
        }

        it = GetComponent<Interactable>();
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Clean);
    }

    void Update()
    {
        if (!Inventory.hasMop) it.prompt = "Мусор. Нужна швабра";
        else if (Inventory.trashBag >= GameConfig.trashBagSize) it.prompt = "Мешок полный";
        else it.prompt = "E — убрать мусор";
    }

    void Clean()
    {
        if (!Inventory.hasMop)
        {
            Hud.Say("Нужна швабра у стены.");
            return;
        }
        if (Inventory.trashBag >= GameConfig.trashBagSize)
        {
            Hud.Say("Мешок полный. Выкинь его в мусорку под эстакадой.", 3f);
            return;
        }

        Inventory.trashBag++;
        Sfx.Play("sweep", 0.8f);

        bool full = Inventory.trashBag >= GameConfig.trashBagSize;
        if (!extra)
        {
            Inventory.trashLeft--;
            if (Inventory.trashLeft <= 0 && !BoxSpawner.SellersStarted)
                Hud.Say(full ? "Склад чист. Мешок полный — выкинь его в мусорку под эстакадой." : "Склад чист. Скоро приедет первый селлер.", 4f);
            else if (full) Hud.Say("Мешок полный. Выкинь его в мусорку под эстакадой.", 3f);
        }
        else if (full) Hud.Say("Мешок полный. Выкинь его в мусорку под эстакадой.", 3f);
        Destroy(gameObject);
    }
}
