using UnityEngine;

// Подсказки для игрока:
// - вверху по центру строка «Задача: ...» — что делать прямо сейчас (меняется сама по ходу смены);
// - точка-прицел в центре экрана;
// - F1 — список всех клавиш.
// Отключить подсказку задачи можно в GameConfig (showObjectives).
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class Objectives : MonoBehaviour
{
    string task = "";
    float nextUpdate;
    bool showKeys;
    GUIStyle taskS, keysS;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("Objectives").AddComponent<Objectives>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) showKeys = !showKeys;

        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + 0.4f;
        task = CurrentTask();
    }

    // ---------- Что делать сейчас ----------
    string CurrentTask()
    {
        if (ActController.Act >= 4) return "Смена окончена. Иди на остановку у завода.";

        // Полный мусорный мешок
        if (Inventory.trashBag >= GameConfig.trashBagSize)
            return "Мешок полный: отнеси его в контейнер под эстакадой";

        // Пролог: мусор на складе
        if (!GameConfig.skipPrologue && Inventory.trashLeft > 0)
            return Inventory.hasMop
                ? "Убери мусор на складе (осталось " + Inventory.trashLeft + ")"
                : "Возьми швабру у левой стены и убери мусор на складе";

        string prefix = "";
        if (ActController.Act >= 2 && Inventory.hasFlashlight && !Inventory.flashlightOn && Inventory.flashlightBattery > 0f && ActController.Act == 2)
            prefix = "Темно — фонарик на Q. ";
        if (ActController.Act >= 3 && GameConfig.horror)
            prefix = "Тише: свет и сердце его приманивают (V — отдышаться). ";

        // Перевозка паллеты важнее всего остального
        PalletCargo carried = PalletCargo.Carried;
        if (carried != null) return prefix + "Отвези паллету в жёлтую зону на улице (E), Q — поставить здесь";
        if (PalletCargo.IsLifting()) return prefix + "Качай рохлю: мышь вверх-вниз";

        // Коробка селлера
        MeasurableBox[] boxes = Object.FindObjectsByType<MeasurableBox>(FindObjectsSortMode.None);
        if (boxes.Length > 0)
        {
            MeasurableBox b = boxes[0];
            if (!Inventory.hasTape) return prefix + "Возьми рулетку со стола у правой стены";
            if (!b.measured) return prefix + "Измерь коробку рулеткой: E, считай отметку у края, набери цифры";
            if (b.order == OrderType.FBS && b.WorkLeft > 0)
                return prefix + "FBS: клей стикеры на доску у стола (осталось " + b.WorkLeft + ")";
            if (b.order == OrderType.FBO && b.WorkLeft > 0)
                return prefix + "FBO: сверь номер поставки на этикетках с заказом (E). Правила — в телефоне (Tab)";
            if (b.order == OrderType.None) return prefix + "Назначь цену: колесо мыши, Enter — предложить";
        }

        // Паллеты
        PalletCargo loading = null, full = null, wrapped = null;
        for (int i = 0; i < PalletCargo.All.Count; i++)
        {
            PalletCargo p = PalletCargo.All[i];
            if (p == null) continue;
            if (p.state == PalletCargo.State.Loading && loading == null) loading = p;
            if (p.state == PalletCargo.State.Full && full == null) full = p;
            if (p.state == PalletCargo.State.Wrapped && wrapped == null) wrapped = p;
        }
        if (full != null) return prefix + "Обмотай полную паллету плёнкой (E три раза)";
        if (wrapped != null)
            return prefix + (Inventory.hasJack ? "Подними обмотанную паллету рохлей (Q)" : "Возьми жёлтую рохлю у паллеты");
        if (Inventory.boxesReady > 0 || Inventory.rollsReady > 0)
        {
            if (loading != null) return prefix + (Inventory.boxesReady > 0
                ? "Положи готовые коробки на паллету (" + Inventory.boxesReady + " шт.)"
                : "Положи готовые рулоны на паллету (" + Inventory.rollsReady + " шт.)");
            if (Inventory.palletStock <= 0) return prefix + "Нет пустой паллеты: купи в телефоне (Tab → Магазин), её привезут во двор";
        }

        // Газель
        if (Inventory.palletsReady > 0)
        {
            if (GazelleController.Busy) return prefix + "Газель в пути";
            return prefix + (Hud.money >= GameConfig.priceGazelle
                ? "Вызови газель в телефоне (Tab → Газель)"
                : "Копи на газель: нужно " + GameConfig.priceGazelle + " руб.");
        }

        if (ActController.Act >= 2 && Inventory.flashlightBattery <= 0f && Inventory.spareBatteries <= 0)
            return prefix + "Фонарик сел: батарейки в крайнем левом складе (нужен ключ) или в магазине";

        return prefix + "Жди следующего селлера";
    }

    // ---------- Рисование ----------
    void MakeStyles()
    {
        taskS = new GUIStyle(UITheme.Label());
        taskS.fontSize = 20;
        taskS.alignment = TextAnchor.UpperCenter;
        taskS.wordWrap = true;
        taskS.normal.textColor = new Color(1f, 0.93f, 0.7f);

        keysS = new GUIStyle(UITheme.Label());
        keysS.fontSize = 18;
        keysS.normal.textColor = UITheme.Text;
    }

    void Shadowed(Rect r, string text, GUIStyle st)
    {
        Color c = st.normal.textColor;
        st.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
        st.normal.textColor = c;
        GUI.Label(r, text, st);
    }

    void OnGUI()
    {
        if (GameMenus.IsBlocking || PhoneUI.IsOpen) return;
        if (taskS == null) MakeStyles();
        GUI.depth = -20;

        // Прицел
        if (!FinaleController.Locked)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.DrawTexture(new Rect(Screen.width / 2f - 2f, Screen.height / 2f - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // Задача
        bool finale = ActController.Act >= 4;
        if (GameConfig.showObjectives && !string.IsNullOrEmpty(task) && (!Hud.hidden || finale) && !FinaleController.Locked)
        {
            float w = Mathf.Min(900f, Screen.width - 40f);
            Shadowed(new Rect((Screen.width - w) / 2f, 18f, w, 60f), "Задача: " + task, taskS);
        }

        // Клавиши
        if (!Hud.hidden && !finale)
        {
            if (!showKeys)
            {
                Shadowed(new Rect(20, Screen.height - 34, 300, 26), "F1 — клавиши", keysS);
                return;
            }
            UITheme.DrawPanel(new Rect(14, Screen.height - 444, 470, 430));
            string keys =
                "WASD — ходить, Shift — бежать, мышь — смотреть\n" +
                "E — действие (взять, измерить, войти)\n" +
                "Tab — телефон, Esc — пауза\n" +
                "Колесо / стрелки, Enter — цена\n" +
                "Q — поднять / поставить паллету рохлей\n" +
                "мышь вверх-вниз — качать рохлю\n" +
                "X — вернуть рохлю (когда она выбрана)\n" +
                "F — фонарик, R — сменить батарейку\n" +
                "1–6 — выбрать предмет, X — положить на место\n" +
                "V — отдышаться (держи пробел)\n" +
                "В укрытии: A / D — выглянуть\n" +
                "F1 — скрыть эту подсказку" +
                (GameConfig.debugKeys ? "\nТест: F2 +1000 руб., F3 +8 коробок, F4 +30 мин, F5 руки" : "");
            GUI.Label(new Rect(28, Screen.height - 432, 450, 414), keys, keysS);
        }
    }
}
