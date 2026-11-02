using UnityEngine;
using UnityEngine.Events;

// Вешается на рулетку. Нажали E — рулетка у вас в инвентаре.
[RequireComponent(typeof(Interactable))]
public class TapeMeasure : MonoBehaviour
{
    public static TapeMeasure Instance;

    void Awake()
    {
        Instance = this;
        Interactable it = GetComponent<Interactable>();
        it.prompt = "E — взять рулетку";
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Take);
    }

    void Take()
    {
        Inventory.hasTape = true;
        Sfx.Play("pickup", 0.8f);
        Hud.Say("Рулетка есть. Подойди к коробке селлера и нажми E.");
        gameObject.SetActive(false);
    }
}
