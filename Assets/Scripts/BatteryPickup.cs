using UnityEngine;
using UnityEngine.Events;

// Вешается на каждый аккумулятор в самом левом складе. E — взять: запас батарей для фонарика +1.
[RequireComponent(typeof(Interactable))]
public class BatteryPickup : MonoBehaviour
{
    void Awake()
    {
        Interactable it = GetComponent<Interactable>();
        it.prompt = "E — взять аккумулятор";
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Take);
    }

    void Take()
    {
        Inventory.spareBatteries++;
        Sfx.Play("pickup", 0.8f);
        Hud.Say("Аккумулятор взят. Запас: " + Inventory.spareBatteries + ". T — сменить батарею фонарика.", 4f);
        Destroy(gameObject);
    }
}
