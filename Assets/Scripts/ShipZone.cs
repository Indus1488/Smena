using UnityEngine;
using UnityEngine.Events;

// Вешается на зону отгрузки перед складом. С паллетой на рохле нажмите E, чтобы оставить её здесь.
// Паллету можно снова поднять рохлей (F) и переставить. Позже сюда приедет газель.
[RequireComponent(typeof(Interactable))]
public class ShipZone : MonoBehaviour
{
    public static ShipZone Instance;

    Interactable it;
    PalletCargo[] taken = new PalletCargo[4];

    // Четыре места для паллет внутри зоны
    readonly Vector3[] slots =
    {
        new Vector3(-0.65f, 0f, -0.65f),
        new Vector3( 0.65f, 0f, -0.65f),
        new Vector3(-0.65f, 0f,  0.65f),
        new Vector3( 0.65f, 0f,  0.65f),
    };

    void Awake()
    {
        Instance = this;
        it = GetComponent<Interactable>();
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(OnInteract);
    }

    int Count()
    {
        int n = 0;
        for (int i = 0; i < taken.Length; i++)
            if (taken[i] != null) n++;
        return n;
    }

    void Update()
    {
        it.prompt = PalletCargo.Carried != null
            ? "E — оставить паллету здесь"
            : "Зона отгрузки (паллет: " + Count() + ")";
    }

    // Находится ли точка внутри жёлтой зоны
    public bool Contains(Vector3 worldPoint)
    {
        Vector3 l = transform.InverseTransformPoint(worldPoint);
        return Mathf.Abs(l.x) < 1.4f && Mathf.Abs(l.z) < 1.4f;
    }

    // Газель погрузила все паллеты. Возвращает, сколько их было.
    public int ClearAll()
    {
        int n = 0;
        for (int i = 0; i < taken.Length; i++)
        {
            if (taken[i] != null)
            {
                Destroy(taken[i].gameObject);
                taken[i] = null;
                n++;
            }
        }
        return n;
    }

    public void Remove(PalletCargo p)
    {
        for (int i = 0; i < taken.Length; i++)
            if (taken[i] == p) taken[i] = null;
    }

    public bool TryDeliver(PalletCargo p)
    {
        int free = -1;
        for (int i = 0; i < taken.Length; i++)
            if (taken[i] == null) { free = i; break; }

        if (free < 0)
        {
            Hud.Say("Зона заполнена. Нужна газель.");
            return false;
        }

        taken[free] = p;
        Vector3 pos = transform.TransformPoint(slots[free]);
        pos.y = transform.position.y;
        p.Deliver(pos, transform.rotation);
        return true;
    }

    void OnInteract()
    {
        PalletCargo p = PalletCargo.Carried;
        if (p == null)
        {
            Hud.Say("Сюда привозят обмотанные паллеты для газели.");
            return;
        }
        if (!p.CanShip)
        {
            Hud.Say("В зону отгрузки можно ставить только обмотанные паллеты.");
            return;
        }
        TryDeliver(p);
    }
}