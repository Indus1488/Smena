using UnityEngine;
using UnityEngine.Events;

// Вешается на каждый кусок мусора у входа. Со шваброй в руках E убирает его.
[RequireComponent(typeof(Interactable))]
public class TrashPiece : MonoBehaviour
{
    Interactable it;

    void Awake()
    {
        Inventory.trashTotal++;
        Inventory.trashLeft++;

        it = GetComponent<Interactable>();
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Clean);
    }

    void Update()
    {
        it.prompt = Inventory.hasMop ? "E — убрать мусор" : "Мусор. Нужна швабра";
    }

    void Clean()
    {
        if (!Inventory.hasMop)
        {
            Hud.Say("Нужна швабра у стены.");
            return;
        }

        Inventory.trashLeft--;
        if (Inventory.trashLeft <= 0) Hud.Say("Вход чист. Скоро приедет первый селлер.", 4f);
        else Hud.Say("Мусора осталось: " + Inventory.trashLeft, 2f);
        Destroy(gameObject);
    }
}
