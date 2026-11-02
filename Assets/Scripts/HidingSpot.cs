using UnityEngine;
using UnityEngine.Events;

// Укрытие: E — спрятаться (присесть за паллетой или залезть в мусорный контейнер), E ещё раз — вылезти.
// Пока вы в укрытии, Володя вас не видит. Найдёт, только если:
//   - светит фонарик,
//   - сердце колотится (пульс выше GameConfig.hidePulseLimit) и он совсем рядом,
//   - он видел, как вы прятались (был ближе 6 м и гнался).
// Укрытия расставляет GameplayFixer (паллеты на складе и контейнер под эстакадой).
[RequireComponent(typeof(Interactable))]
public class HidingSpot : MonoBehaviour
{
    public static HidingSpot Current;          // где игрок прячется сейчас (null — не прячется)
    public static float EnteredAt;

    public Vector3 eyeWorld;                   // где окажутся глаза (камера) игрока
    public Vector3 lookAt;                     // куда смотреть из укрытия
    public string hidePrompt = "E — спрятаться за паллетой";

    Interactable it;
    Vector3 backPos;
    Quaternion backRot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Current = null;
    }

    void Awake()
    {
        it = GetComponent<Interactable>();
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(Toggle);
    }

    void Update()
    {
        it.prompt = Current == this ? "E — вылезти" : hidePrompt;
        if (Current == this && Input.GetKeyDown(KeyCode.E) && Time.time - EnteredAt > 0.3f && !InteractorSeesMe()) Exit();
    }

    // E внутри укрытия работает, даже если взгляд не попадает в сам объект
    bool InteractorSeesMe()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        RaycastHit hit;
        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 3f)) return false;
        return hit.collider.GetComponentInParent<HidingSpot>() == this;
    }

    void Toggle()
    {
        if (Current == this) { if (Time.time - EnteredAt > 0.3f) Exit(); }
        else if (Current == null) Enter();
    }

    void Enter()
    {
        if (!GameConfig.horror) { Hud.Say("Прятаться не от кого.", 2f); return; }
        if (PalletCargo.IsBusy()) { Hud.Say("Сначала поставь паллету (G).", 2f); return; }
        GameObject p = GameObject.Find("Player");
        if (p == null) return;

        backPos = p.transform.position;
        backRot = p.transform.rotation;
        Camera cam = Camera.main;
        float camH = cam != null ? cam.transform.position.y - p.transform.position.y : 0.6f;
        Teleport(p, eyeWorld - Vector3.up * camH);
        Vector3 d = lookAt - eyeWorld; d.y = 0f;
        if (d.sqrMagnitude > 0.01f) p.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);

        PlayerController.MoveLocked = true;
        Current = this;
        EnteredAt = Time.time;
        Sfx.Play("hide", 0.7f);
        Hud.Say("Ты спрятался. Не свети и дыши ровно (V).", 4f);
    }

    public void Exit()
    {
        if (Current != this) return;
        GameObject p = GameObject.Find("Player");
        if (p != null)
        {
            Teleport(p, backPos);
            p.transform.rotation = backRot;
        }
        PlayerController.MoveLocked = false;
        Current = null;
    }

    public static void ForceExit()
    {
        if (Current != null) Current.Exit();
    }

    static void Teleport(GameObject p, Vector3 pos)
    {
        CharacterController cc = p.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        p.transform.position = pos;
        if (cc != null) cc.enabled = true;
    }
}
