using UnityEngine;
using UnityEngine.Events;

// Вешается на ключ, спрятанный на стуле на улице. Ключ открывает самый левый склад.
[RequireComponent(typeof(Interactable))]
public class KeyItem : MonoBehaviour
{
    void Awake()
    {
        Interactable it = GetComponent<Interactable>();
        it.prompt = "E — взять ключ";
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Take);
    }

    void Take()
    {
        Inventory.hasKey = true;
        Hud.Say("Ключ был спрятан на стуле. Он подходит к самому левому складу.", 5f);
        gameObject.SetActive(false);
    }
}
