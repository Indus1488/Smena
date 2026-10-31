using UnityEngine;
using UnityEngine.Events;

// Вешается на швабру. Нажали E: швабра у вас, можно убирать мусор у входа.
[RequireComponent(typeof(Interactable))]
public class MopItem : MonoBehaviour
{
    void Awake()
    {
        Interactable it = GetComponent<Interactable>();
        it.prompt = "E — взять швабру";
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Take);
    }

    void Take()
    {
        Inventory.hasMop = true;
        Hud.Say("Швабра у вас. Уберите мусор у входа.");
        gameObject.SetActive(false);
    }
}
