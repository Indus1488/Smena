using UnityEngine;
using UnityEngine.Events;

// Мусорный бак под эстакадой. E — выкинуть мусорный мешок.
[RequireComponent(typeof(Interactable))]
public class TrashBin : MonoBehaviour
{
    Interactable it;

    void Awake()
    {
        it = GetComponent<Interactable>();
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Throw);
    }

    void Update()
    {
        it.prompt = Inventory.trashBag > 0 ? "E — выкинуть мешок" : "Мусорка";
    }

    void Throw()
    {
        if (Inventory.trashBag <= 0) return;
        Inventory.trashBag = 0;
        Sfx.PlayAt("trash_bag", transform.position, 1f);
        Hud.Say("Мешок выкинут.", 2f);
    }
}
