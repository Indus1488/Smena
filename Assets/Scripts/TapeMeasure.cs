using UnityEngine;
using UnityEngine.Events;

// Вешается на рулетку. Нажали E — рулетка у вас в инвентаре.
[RequireComponent(typeof(Interactable))]
public class TapeMeasure : MonoBehaviour
{
    void Awake()
    {
        Interactable it = GetComponent<Interactable>();
        it.prompt = "E — взять рулетку";
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Take);
    }

    void Take()
    {
        Inventory.hasTape = true;
        Hud.Say("Рулетка у вас. Подойдите к коробке и нажмите E.");
        gameObject.SetActive(false);
    }
}
