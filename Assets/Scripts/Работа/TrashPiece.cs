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
    public static readonly System.Collections.Generic.List<TrashPiece> All = new System.Collections.Generic.List<TrashPiece>();   // для TrashFinder

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

        All.Add(this);
        it = GetComponent<Interactable>();
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Clean);
    }

    void OnDestroy() { All.Remove(this); }
    void OnDisable() { All.Remove(this); }
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }

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
            Hud.Say("Нужна швабра. Она у левой стены.");
            return;
        }
        if (Inventory.trashBag >= GameConfig.trashBagSize)
        {
            Hud.Say("Мешок полный. Отнеси его в контейнер под эстакадой.", 3f);
            return;
        }

        Inventory.trashBag++;
        Sfx.Play("sweep", 0.8f);
        HandsView.Sweep();   // щётка делает взмах

        bool full = Inventory.trashBag >= GameConfig.trashBagSize;
        if (!extra)
        {
            Inventory.trashLeft--;
            if (Inventory.trashLeft <= 0 && !BoxSpawner.SellersStarted)
                Hud.Say(full ? "Склад чист. Мешок полный — отнеси его в контейнер под эстакадой." : "Склад чист. Скоро приедет первый селлер.", 4f);
            else if (full) Hud.Say("Мешок полный. Отнеси его в контейнер под эстакадой.", 3f);
        }
        else if (full) Hud.Say("Мешок полный. Отнеси его в контейнер под эстакадой.", 3f);
        Destroy(gameObject);
    }
}
