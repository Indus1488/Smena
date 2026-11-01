using UnityEngine;
using UnityEngine.Events;

// Вешается на швабру. Нажали E: швабра у вас, можно убирать мусор у входа.
[RequireComponent(typeof(Interactable))]
public class MopItem : MonoBehaviour
{
    public static MopItem Instance;

    void Awake()
    {
        Instance = this;
        Interactable it = GetComponent<Interactable>();
        it.prompt = "E — взять швабру";
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Take);
    }

    void Take()
    {
        Inventory.hasMop = true;
        Sfx.Play("pickup", 0.8f);
        Hud.Say("Швабра у вас. Уберите мусор у входа.");
        gameObject.SetActive(false);
    }
}
