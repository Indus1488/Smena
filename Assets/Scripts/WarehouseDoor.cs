using UnityEngine;
using UnityEngine.Events;

// Дверь склада. Снаружи ворота закрыты, а когда вы заходите внутрь, они «открываются».
// Если needsKey включён, войти можно только с ключом.
public class WarehouseDoor : MonoBehaviour
{
    public Interactable outside;     // кнопка E на двери снаружи
    public Interactable inside;      // кнопка E на двери изнутри
    public Teleporter enterTp;       // переносит внутрь
    public Teleporter exitTp;        // переносит на улицу
    public GameObject closedDoors;   // закрытые ворота (видны снаружи)
    public GameObject openDoors;     // открытые ворота (видны изнутри)
    public bool needsKey;

    void Awake()
    {
        Hook(outside, Enter);
        Hook(inside, Exit);
        ShowClosed();
    }

    static void Hook(Interactable it, UnityAction action)
    {
        if (it == null) return;
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(action);
    }

    bool Locked()
    {
        return needsKey && !Inventory.hasKey;
    }

    void Update()
    {
        if (outside != null)
            outside.prompt = Locked() ? "Заперто. Нужен ключ" : "E — войти";
    }

    void Enter()
    {
        if (Locked())
        {
            Hud.Say("Дверь заперта. Ключ где-то рядом, поищите на улице.");
            return;
        }
        if (enterTp != null) enterTp.Go();
        Invoke("ShowOpen", 0.45f);     // меняем двери, пока экран чёрный
    }

    void Exit()
    {
        if (exitTp != null) exitTp.Go();
        Invoke("ShowClosed", 0.45f);
    }

    void ShowOpen()
    {
        if (closedDoors != null) closedDoors.SetActive(false);
        if (openDoors != null) openDoors.SetActive(true);
    }

    void ShowClosed()
    {
        if (closedDoors != null) closedDoors.SetActive(true);
        if (openDoors != null) openDoors.SetActive(false);
    }
}
