using UnityEngine;
using UnityEngine.Events;

// Вешается на рабочую рохлю. E: взять её. После отгрузки паллеты рохля возвращается на место.
[RequireComponent(typeof(Interactable))]
public class WorkJack : MonoBehaviour
{
    public static WorkJack Instance;

    void Awake()
    {
        Instance = this;
        Interactable it = GetComponent<Interactable>();
        it.prompt = "E — взять рохлю";
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Take);
    }

    void Take()
    {
        Inventory.hasJack = true;
        Sfx.Play("pickup", 0.8f);
        Hud.Say("Рохля у тебя. Подойди к обмотанной паллете.", 4f);
        gameObject.SetActive(false);
    }

    public void Return()
    {
        gameObject.SetActive(true);
    }
}
