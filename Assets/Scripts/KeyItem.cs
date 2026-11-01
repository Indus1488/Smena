using UnityEngine;
using UnityEngine.Events;

// Вешается на ключ, спрятанный на стуле на улице. Ключ открывает самый левый склад.
[RequireComponent(typeof(Interactable))]
public class KeyItem : MonoBehaviour
{
    public static KeyItem Instance;

    void Awake()
    {
        Instance = this;
        Interactable it = GetComponent<Interactable>();
        it.prompt = "E — взять ключ";
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Take);
    }

    void Take()
    {
        Inventory.hasKey = true;
        Sfx.Play("pickup", 0.8f);
        gameObject.SetActive(false);
    }
}
