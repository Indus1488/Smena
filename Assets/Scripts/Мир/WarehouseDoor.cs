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
    public bool soundOnly;          // войти нельзя: по E только звук (склад с монстром)
    public string soundOnlyClip = "monster_door";   // ваш звук: Assets/Resources/monster_door
    bool stayOpen;    // ворота распахнуты «сами» (акт 2) и больше не закрываются

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
            outside.prompt = (soundOnly && !stayOpen) ? "E" : (Locked() ? "Заперто. Нужен ключ" : "E — войти");
    }

    void Enter()
    {
        if (soundOnly && !stayOpen)
        {
            Sfx.Play(soundOnlyClip, 1f);
            return;
        }
        if (Locked())
        {
            Sfx.Play("locked", 0.9f);
            Hud.Say("Заперто. Ключ должен быть где-то на улице.");
            return;
        }
        Sfx.Play("door_open", 0.9f);
        if (enterTp != null) enterTp.Go();
        Invoke("ShowOpen", 0.45f);     // меняем двери, пока экран чёрный
    }

    void Exit()
    {
        Sfx.Play("door_close", 0.9f);
        if (exitTp != null) exitTp.Go();
        Invoke("ShowClosed", 0.45f);
    }

    // Ворота открываются сами, снаружи (скрип в 18:00)
    public void ForceOpen()
    {
        stayOpen = true;
        ShowOpen();
    }

    void ShowOpen()
    {
        if (closedDoors != null) closedDoors.SetActive(false);
        if (openDoors != null) openDoors.SetActive(true);
    }

    void ShowClosed()
    {
        if (stayOpen) return;
        if (closedDoors != null) closedDoors.SetActive(true);
        if (openDoors != null) openDoors.SetActive(false);
    }
}
