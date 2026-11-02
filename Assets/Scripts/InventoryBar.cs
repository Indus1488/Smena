using System.Collections.Generic;
using UnityEngine;

// Видимый инвентарь внизу экрана: что у вас с собой.
// 1–6 — выбрать предмет, X — положить выбранный предмет на место (туда, где его взяли).
// Рулетку, швабру, ключ и рохлю можно вернуть; фонарик, батареи и мусорный мешок — нет
// (мешок выкидывается в мусорку под эстакадой).
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class InventoryBar : MonoBehaviour
{
    struct Slot
    {
        public string id, title, sub;
        public Slot(string i, string t, string s) { id = i; title = t; sub = s; }
    }

    readonly List<Slot> slots = new List<Slot>();
    int selected;
    GUIStyle titleS, subS, numS, hintS;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("InventoryBar").AddComponent<InventoryBar>();
    }

    void Collect()
    {
        slots.Clear();
        if (Inventory.hasTape) slots.Add(new Slot("tape", "Рулетка", ""));
        if (Inventory.hasMop) slots.Add(new Slot("mop", "Швабра", ""));
        if (Inventory.hasJack) slots.Add(new Slot("jack", "Рохля", ""));
        if (Inventory.hasKey) slots.Add(new Slot("key", "Ключ", "от склада"));
        if (Inventory.hasFlashlight) slots.Add(new Slot("light", "Фонарик", Mathf.CeilToInt(Inventory.flashlightBattery) + "%"));
        if (Inventory.spareBatteries > 0) slots.Add(new Slot("bat", "Батареи", "× " + Inventory.spareBatteries));
        if (Inventory.trashBag > 0) slots.Add(new Slot("bag", "Мешок", Inventory.trashBag + "/" + GameConfig.trashBagSize));
        if (selected >= slots.Count) selected = Mathf.Max(0, slots.Count - 1);
    }

    void Update()
    {
        Collect();
        if (PhoneUI.IsOpen || GameMenus.IsBlocking || Hud.hidden) return;

        for (int i = 0; i < slots.Count && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) selected = i;

        if (Input.GetKeyDown(KeyCode.X) && slots.Count > 0) PutBack(slots[selected].id);
    }

    void PutBack(string id)
    {
        switch (id)
        {
            case "tape":
                Inventory.hasTape = false;
                if (TapeMeasure.Instance != null) TapeMeasure.Instance.gameObject.SetActive(true);
                break;
            case "mop":
                Inventory.hasMop = false;
                if (MopItem.Instance != null) MopItem.Instance.gameObject.SetActive(true);
                break;
            case "key":
                Inventory.hasKey = false;
                if (KeyItem.Instance != null) KeyItem.Instance.gameObject.SetActive(true);
                break;
            case "jack":
                if (PalletCargo.IsBusy()) { Hud.Say("Сначала поставь паллету (G).", 2f); return; }
                Inventory.hasJack = false;
                if (WorkJack.Instance != null) WorkJack.Instance.Return();
                break;
            case "bag":
                Hud.Say("Мешок — в контейнер под эстакадой.", 3f);
                return;
            default:
                Hud.Say("Это остаётся с тобой.", 2f);
                return;
        }
        Sfx.Play("pickup", 0.6f);
        Hud.Say("Положил на место.", 2f);
    }

    void OnGUI()
    {
        if (Hud.hidden || slots.Count == 0) return;
        if (titleS == null) MakeStyles();

        const float w = 104f, h = 62f, gap = 8f;
        float total = slots.Count * w + (slots.Count - 1) * gap;
        float x0 = (Screen.width - total) / 2f, y = Screen.height - h - 34f;

        for (int i = 0; i < slots.Count; i++)
        {
            Rect r = new Rect(x0 + i * (w + gap), y, w, h);
            bool sel = i == selected;
            if (sel)
            {
                GUI.color = UITheme.Accent;
                GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            UITheme.DrawPanel(r);

            GUI.Label(new Rect(r.x + 5, r.y + 2, 20, 18), (i + 1).ToString(), numS);
            GUI.Label(new Rect(r.x, r.y + 14, r.width, 24), slots[i].title, titleS);
            if (slots[i].sub.Length > 0) GUI.Label(new Rect(r.x, r.y + 36, r.width, 20), slots[i].sub, subS);
        }
        GUI.Label(new Rect(0, y + h + 6, Screen.width, 22), "1–" + Mathf.Min(slots.Count, 9) + " — выбрать,  X — положить на место", hintS);
    }

    void MakeStyles()
    {
        titleS = new GUIStyle(UITheme.Label()) { fontSize = 17, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        titleS.normal.textColor = UITheme.Text;
        subS = new GUIStyle(UITheme.Label()) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        subS.normal.textColor = UITheme.Accent;
        numS = new GUIStyle(UITheme.Label()) { fontSize = 13 };
        numS.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        hintS = new GUIStyle(UITheme.Label()) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        hintS.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
    }
}
