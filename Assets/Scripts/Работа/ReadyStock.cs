using System.Collections.Generic;
using UnityEngine;

// Готовые коробки и рулоны видно на складе — как стопку пустых паллет во дворе.
// Коробки стоят на местах декоративных коробок (Box в Interior и LoneBox у входа), по две в стопку;
// рулоны — на месте кучи рулонов в углу (LooseRoll). Декоративные убираются.
// Положил коробку на паллету — одна пропала со склада.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class ReadyStock : MonoBehaviour
{
    struct Slot { public Vector3 pos; public Quaternion rot; public Vector3 size; public Material mat; public Transform parent; }

    readonly List<Slot> boxSlots = new List<Slot>(), rollSlots = new List<Slot>();
    readonly List<GameObject> boxes = new List<GameObject>(), rolls = new List<GameObject>();
    const int BoxesPerSlot = 2, RollsPerSlot = 2;
    bool ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("ReadyStock").AddComponent<ReadyStock>();
    }

    void Start()
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            bool box = (t.name == "Box" && t.parent != null && t.parent.name == "Interior") || t.name == "LoneBox";
            bool roll = t.name == "LooseRoll";
            if (t.name == "BlackBundle") { t.gameObject.SetActive(false); continue; }   // чёрные «мешки» у входа: не похожи, убраны
            if (!box && !roll) continue;
            Renderer r = t.GetComponent<Renderer>();
            Slot s = new Slot
            {
                pos = t.position, rot = t.rotation, size = t.lossyScale, parent = t.parent,
                mat = r != null ? r.sharedMaterial : null
            };
            if (box) boxSlots.Add(s); else rollSlots.Add(s);
            t.gameObject.SetActive(false);   // декор больше не нужен
        }
        // Сначала заполняются места ближе к месту сборки паллет
        GameObject spot = GameObject.Find("PalletSpot");
        if (spot != null)
        {
            Vector3 p = spot.transform.position;
            boxSlots.Sort((a, b) => (a.pos - p).sqrMagnitude.CompareTo((b.pos - p).sqrMagnitude));
        }
        ready = true;
    }

    void Update()
    {
        if (!ready) return;
        Sync(boxes, boxSlots, BoxesPerSlot, Inventory.boxesReady, MakeBox);
        Sync(rolls, rollSlots, RollsPerSlot, Inventory.rollsReady, MakeRoll);
    }

    delegate GameObject Maker(Slot s, int level);

    static void Sync(List<GameObject> shown, List<Slot> slots, int perSlot, int want, Maker make)
    {
        if (slots.Count == 0) return;
        want = Mathf.Clamp(want, 0, slots.Count * perSlot);
        while (shown.Count < want)
        {
            int i = shown.Count;
            shown.Add(make(slots[i % slots.Count], i / slots.Count));   // сначала по одной на каждое место, потом второй ярус
        }
        while (shown.Count > want)
        {
            Destroy(shown[shown.Count - 1]);
            shown.RemoveAt(shown.Count - 1);
        }
    }

    GameObject MakeBox(Slot s, int level)
    {
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = "ReadyBox";                                   // ModelSwap подставит 3D-коробку
        if (s.parent != null) b.transform.SetParent(s.parent, true);
        b.transform.position = s.pos + Vector3.up * (s.size.y * level);
        b.transform.rotation = s.rot * Quaternion.Euler(0f, level * 17f - 8f, 0f);
        b.transform.localScale = new Vector3(s.size.x * (1f - level * 0.08f), s.size.y, s.size.z * (1f - level * 0.08f));
        if (s.mat != null) b.GetComponent<Renderer>().sharedMaterial = s.mat;
        return b;
    }

    GameObject MakeRoll(Slot s, int level)
    {
        GameObject r = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(r.GetComponent<Collider>());
        r.name = "LooseRoll";                                  // как декоративные: ModelSwap подставит модель рулона
        if (s.parent != null) r.transform.SetParent(s.parent, true);
        r.transform.position = s.pos + Vector3.up * (s.size.x * level);
        r.transform.rotation = s.rot * Quaternion.Euler(0f, 0f, level * 12f);
        r.transform.localScale = s.size;
        r.GetComponent<Renderer>().sharedMaterial = s.mat != null ? s.mat : PalletCargo.RollMaterial();
        return r;
    }
}
