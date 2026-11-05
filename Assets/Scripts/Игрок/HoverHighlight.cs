using System.Collections.Generic;
using UnityEngine;

// Подсветка: на что смотрит игрок и с чем можно взаимодействовать (E), то слегка светится.
// Работает для всего с компонентом Interactable: ворота, ключ, рулетка, рохля, коробки, паллеты, швабра, мусор и т. д.
// У невидимых зон (двери-триггеры, доска для стикеров) подсвечивается то, что они изображают.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class HoverHighlight : MonoBehaviour
{
    public static Color glow = new Color(0.35f, 0.30f, 0.18f);   // цвет и сила подсветки
    const float Distance = 3f;                                   // как у кнопки E

    Interactable current;
    readonly List<Renderer> lit = new List<Renderer>();
    readonly List<Material[]> saved = new List<Material[]>();
    readonly List<Material> temp = new List<Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("HoverHighlight").AddComponent<HoverHighlight>();
    }

    void Update()
    {
        Interactable target = null;
        Camera cam = Camera.main;
        bool blocked = GameMenus.IsBlocking || PhoneUI.IsOpen || FinaleController.Locked;

        if (cam != null && !blocked)
        {
            RaycastHit hit;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, Distance))
                target = hit.collider.GetComponentInParent<Interactable>();
        }

        // Если объект «пустой» (например, уже недоступен), подсказка у него пустая — не подсвечиваем
        if (target != null && string.IsNullOrEmpty(target.prompt)) target = null;
        // Мусор, пока швабра в руках, и так светится весь (TrashFinder) — не трогаем его материалы
        if (target != null && TrashFinder.Glowing && target.GetComponentInParent<TrashPiece>() != null) target = null;

        if (target == current) return;
        Clear();
        current = target;
        if (current != null) Apply(current);
    }

    void OnDisable()
    {
        Clear();
    }

    // Какие видимые части относятся к этому объекту
    static List<Renderer> PartsOf(Interactable it)
    {
        List<Renderer> list = new List<Renderer>(it.GetComponentsInChildren<Renderer>());

        if (list.Count == 0)
        {
            // Двери-триггеры: подсвечиваем ворота того склада, к которому они относятся
            WarehouseDoor[] doors = Object.FindObjectsByType<WarehouseDoor>(FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++)
            {
                WarehouseDoor d = doors[i];
                if (d.outside != it && d.inside != it) continue;
                GameObject shown = (d.closedDoors != null && d.closedDoors.activeInHierarchy) ? d.closedDoors : d.openDoors;
                if (shown != null) list.AddRange(shown.GetComponentsInChildren<Renderer>());
            }

            // Зона перед доской для стикеров: подсвечиваем саму доску
            if (it.name == "StickerBoardZone" && it.transform.parent != null)
            {
                Transform board = it.transform.parent.Find("StickerBoard");
                if (board != null) list.AddRange(board.GetComponentsInChildren<Renderer>());
            }
        }
        return list;
    }

    void Apply(Interactable it)
    {
        List<Renderer> parts = PartsOf(it);
        for (int i = 0; i < parts.Count; i++)
        {
            Renderer r = parts[i];
            if (r == null || !r.enabled) continue;

            Material[] orig = r.sharedMaterials;
            Material[] copy = new Material[orig.Length];
            for (int k = 0; k < orig.Length; k++)
            {
                if (orig[k] == null) continue;
                Material m = new Material(orig[k]);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", glow);
                copy[k] = m;
                temp.Add(m);
            }
            lit.Add(r);
            saved.Add(orig);
            r.sharedMaterials = copy;
        }
    }

    void Clear()
    {
        for (int i = 0; i < lit.Count; i++)
            if (lit[i] != null) lit[i].sharedMaterials = saved[i];
        lit.Clear();
        saved.Clear();
        for (int i = 0; i < temp.Count; i++)
            if (temp[i] != null) Destroy(temp[i]);
        temp.Clear();
        current = null;
    }
}
