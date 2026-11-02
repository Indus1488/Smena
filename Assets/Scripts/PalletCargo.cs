using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Паллета.
// Сборка: E — положить готовую коробку (8 штук) -> E x3 — обмотать плёнкой.
// Перевозка: возьмите рохлю, посмотрите на паллету и нажмите F (или E на обмотанной),
//            качайте мышью вверх-вниз 8 раз -> паллета едет перед вами.
// G — поставить паллету где угодно. E в жёлтой зоне — оставить на отгрузку.
[RequireComponent(typeof(Interactable))]
public class PalletCargo : MonoBehaviour
{
    public enum State { Loading, Full, Wrapped, Lifting, Carried, Delivered }

    public static PalletCargo Carried;                                   // паллета, которую вы везёте
    public static List<PalletCargo> All = new List<PalletCargo>();       // все паллеты в игре

    public State state = State.Loading;
    public int capacity = 8;
    public int count;
    public int pumpsNeeded = 8;

    Material woodMat, boxMat, filmMat;
    Interactable it;
    BoxCollider col;
    State stateBefore;
    int wrapStep;
    int pumps;
    bool pumpUp;           // ручка рохли поднята (ждём движения вниз)
    float pumpTravel;      // сколько мышь прошла в текущую сторону
    const float PumpStroke = 6f;   // длина движения мышью для половины качка
    PlayerController lookLock;     // пока качаем, камера не крутится
    Vector3 liftBase;
    float speedBefore;
    System.Random rnd = new System.Random();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Carried = null;
        All = new List<PalletCargo>();
    }

    // Есть ли рядом с точкой паллета (кроме той, что вы везёте)
    public static bool AnyNear(Vector3 p, float radius)
    {
        for (int i = 0; i < All.Count; i++)
        {
            PalletCargo c = All[i];
            if (c == null || c == Carried) continue;
            if (Vector3.Distance(c.transform.position, p) < radius) return true;
        }
        return false;
    }

    // Везёте или поднимаете ли сейчас какую-то паллету
    // Качают ли сейчас какую-то паллету (камера в это время не крутится)
    public static bool IsLifting()
    {
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].state == State.Lifting) return true;
        return false;
    }

    // Прервать подъём (например, если Володя поймал игрока): паллета опускается, камера снова крутится
    public static void CancelLifting()
    {
        for (int i = 0; i < All.Count; i++)
        {
            PalletCargo p = All[i];
            if (p == null || p.state != State.Lifting) continue;
            p.transform.position = p.liftBase;
            p.state = (p.stateBefore == State.Delivered) ? State.Wrapped : p.stateBefore;
            if (p.lookLock != null) p.lookLock.enabled = true;
            p.lookLock = null;
        }
    }

    public static bool IsBusy()
    {
        if (Carried != null) return true;
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].state == State.Lifting) return true;
        return false;
    }

    // Можно ли отправить эту паллету (она обмотана)
    public bool CanShip
    {
        get { return stateBefore == State.Wrapped || stateBefore == State.Delivered; }
    }

    void Awake()
    {
        All.Add(this);
        it = GetComponent<Interactable>();
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(OnInteract);
    }

    void OnDestroy()
    {
        All.Remove(this);
        if (Carried == this) Carried = null;
    }

    // Строит деревянный поддон и коллайдер для взаимодействия
    public void Init(Material wood, Material box, Material film)
    {
        capacity = GameConfig.palletCapacity;
        woodMat = wood;
        boxMat = box;
        filmMat = film;

        Part("Deck", new Vector3(0, 0.13f, 0), new Vector3(1.2f, 0.04f, 0.8f), woodMat);
        for (int i = -1; i <= 1; i++)
            Part("Runner", new Vector3(i * 0.5f, 0.055f, 0), new Vector3(0.1f, 0.11f, 0.8f), woodMat);

        col = gameObject.AddComponent<BoxCollider>();
        UpdateCollider();
    }

    GameObject Part(string name, Vector3 pos, Vector3 size, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    float CargoHeight()
    {
        return ((count + 3) / 4) * 0.3f;
    }

    void UpdateCollider()
    {
        float h = Mathf.Max(0.4f, 0.15f + CargoHeight());
        col.size = new Vector3(1.2f, h, 0.8f);
        col.center = new Vector3(0, h / 2f, 0);
    }

    void AddBoxVisual()
    {
        int i = count - 1;
        int layer = i / 4;
        int slot = i % 4;
        float x = (slot % 2 == 0) ? -0.28f : 0.28f;
        float z = (slot / 2 == 0) ? -0.19f : 0.19f;
        GameObject b = Part("Carton", new Vector3(x, 0.30f + layer * 0.3f, z), new Vector3(0.55f, 0.29f, 0.37f), boxMat);
        b.transform.localRotation = Quaternion.Euler(0, (float)(rnd.NextDouble() * 8.0 - 4.0), 0);
    }

    bool IsLiftable()
    {
        return state == State.Loading || state == State.Full || state == State.Wrapped || state == State.Delivered;
    }

    // Смотрит ли игрок на эту паллету
    bool LookingAtMe()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        RaycastHit hit;
        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 3f)) return false;
        return hit.collider.GetComponentInParent<PalletCargo>() == this;
    }

    void Update()
    {
        UpdatePrompt();

        if (state == State.Lifting)
        {
            // Качаем рохлей: мышь вверх, потом вниз — это один качок
            float dy = (PhoneUI.IsOpen || GameMenus.IsBlocking) ? 0f : Input.GetAxisRaw("Mouse Y");
            pumpTravel += dy;
            if (!pumpUp && pumpTravel > PumpStroke)
            {
                pumpUp = true;            // ручка поднята, теперь вниз
                pumpTravel = 0f;
            }
            else if (pumpUp && pumpTravel < -PumpStroke)
            {
                pumpUp = false;           // ручка опущена: качок засчитан
                pumpTravel = 0f;
                pumps++;
                Sfx.Play("jack_pump", 0.7f);
                transform.position = liftBase + Vector3.up * (0.1f * pumps / pumpsNeeded);
                Hud.Say("Качай рохлю: мышь вверх-вниз " + pumps + "/" + pumpsNeeded, 30f);
                if (pumps >= pumpsNeeded) PickUp();
            }
            if (!pumpUp && pumpTravel < 0f) pumpTravel = 0f;   // в начале качка лишнее движение вниз не копится
            if (pumpUp && pumpTravel > 0f) pumpTravel = 0f;
        }
        else if (state == State.Carried)
        {
            if (Input.GetKeyDown(KeyCode.G)) PutDown();
        }
        else if (Input.GetKeyDown(KeyCode.F) && Inventory.hasJack && !IsBusy() && IsLiftable() && LookingAtMe())
        {
            StartLift();
        }
    }

    void UpdatePrompt()
    {
        string hint = (Inventory.hasJack && !IsBusy() && IsLiftable()) ? "   |   F — поднять рохлей" : "";

        switch (state)
        {
            case State.Loading:
                it.prompt = (Inventory.boxesReady > 0
                    ? "E — положить коробку (" + count + "/" + capacity + ", в очереди " + Inventory.boxesReady + ")"
                    : "Паллета " + count + "/" + capacity + ". Нужны готовые коробки") + hint;
                break;
            case State.Full:
                it.prompt = "E — обмотать плёнкой (" + wrapStep + "/3)" + hint;
                break;
            case State.Wrapped:
                it.prompt = Inventory.hasJack ? "E — поднять рохлей" : "Паллета готова. Нужна рохля";
                break;
            case State.Lifting:
                it.prompt = "Качай рохлю: мышь вверх-вниз";
                break;
            case State.Delivered:
                it.prompt = "Паллета ждёт газель" + hint;
                break;
            default:
                it.prompt = "";
                break;
        }
    }

    void OnInteract()
    {
        switch (state)
        {
            case State.Loading:
                if (Inventory.boxesReady <= 0)
                {
                    Hud.Say("Готовых коробок нет. Сначала прими заказы.");
                    return;
                }
                Inventory.boxesReady--;
                count++;
                AddBoxVisual();
                UpdateCollider();
                if (count >= capacity)
                {
                    state = State.Full;
                    Hud.Say("Паллета заполнена. Обмотай её плёнкой.");
                }
                else
                {
                    Hud.Say("Коробка на паллете: " + count + "/" + capacity, 2f);
                }
                break;

            case State.Full:
                wrapStep++;
                Sfx.Play("film_wrap", 0.8f);
                if (wrapStep < 3) Hud.Say("Обматываю плёнкой: " + wrapStep + "/3", 2f);
                else Wrap();
                break;

            case State.Wrapped:
                if (!Inventory.hasJack)
                {
                    Hud.Say("Нужна рохля. Жёлтая стоит у места сборки.");
                    return;
                }
                if (IsBusy()) return;
                StartLift();
                break;

            case State.Delivered:
                Hud.Say("Паллета ждёт газель. Переставить её можно рохлей (F).");
                break;
        }
    }

    void Wrap()
    {
        state = State.Wrapped;
        float ch = CargoHeight();
        Part("Film", new Vector3(0, 0.15f + ch / 2f, 0), new Vector3(1.22f, ch, 0.82f), filmMat);
        Hud.Say("Паллета обмотана. Бери рохлю.");
    }

    // ---------- Подъём, перевозка, постановка ----------
    void StartLift()
    {
        stateBefore = state;

        if (state == State.Delivered)
        {
            // Паллету забирают из зоны отгрузки
            Inventory.palletsReady--;
            if (ShipZone.Instance != null) ShipZone.Instance.Remove(this);
        }

        state = State.Lifting;
        pumps = 0;
        pumpUp = false;
        pumpTravel = 0f;
        liftBase = transform.position;

        GameObject pl = GameObject.Find("Player");
        lookLock = pl != null ? pl.GetComponent<PlayerController>() : null;
        if (lookLock != null) lookLock.enabled = false;   // мышь качает рохлю, а не крутит камеру

        Hud.Say("Качай рохлю: мышь вверх-вниз 0/" + pumpsNeeded, 30f);
    }

    // Паллета на рохле: едет перед игроком, скорость падает вдвое
    void PickUp()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Hud.Say("Объект Player не найден.");
            return;
        }

        if (lookLock != null) lookLock.enabled = true;
        lookLock = null;

        state = State.Carried;
        Carried = this;
        col.enabled = false;

        transform.SetParent(player.transform, true);
        transform.localPosition = new Vector3(0, -0.9f, 2.0f);
        transform.localRotation = Quaternion.identity;

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            speedBefore = pc.speed;
            pc.speed = speedBefore * 0.5f;
        }
        Hud.Say("Паллета на рохле. Вези в жёлтую зону во дворе (E). G — поставить здесь.", 7f);
    }

    // G: поставить паллету там, где вы сейчас её везёте
    void PutDown()
    {
        Vector3 pos = transform.position;

        // В жёлтой зоне паллета ставится на отгрузку
        if (ShipZone.Instance != null && ShipZone.Instance.Contains(pos))
        {
            if (CanShip) ShipZone.Instance.TryDeliver(this);
            else Hud.Say("В зону отгрузки можно ставить только обмотанные паллеты.");
            return;
        }

        // Ищем пол под паллетой
        float y = pos.y;
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out hit, 6f)) y = hit.point.y;

        float yaw = Mathf.Round(transform.eulerAngles.y / 90f) * 90f;
        State back = (stateBefore == State.Delivered) ? State.Wrapped : stateBefore;
        Release(new Vector3(pos.x, y, pos.z), Quaternion.Euler(0, yaw, 0), back);
        Hud.Say("Паллета поставлена. F — поднять снова, R — вернуть рохлю.", 4f);
    }

    // Вызывается зоной отгрузки
    public void Deliver(Vector3 worldPos, Quaternion rot)
    {
        Release(worldPos, rot, State.Delivered);
        Inventory.palletsReady++;
        Inventory.hasJack = false;
        if (WorkJack.Instance != null) WorkJack.Instance.Return();
        Hud.Say("Паллета в зоне отгрузки. Паллет к отправке: " + Inventory.palletsReady, 5f);
    }

    void Release(Vector3 pos, Quaternion rot, State newState)
    {
        transform.SetParent(null, true);
        transform.position = pos;
        transform.rotation = rot;

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null && speedBefore > 0f) pc.speed = speedBefore;
        }

        col.enabled = true;
        state = newState;
        Carried = null;
    }
}
