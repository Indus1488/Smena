using UnityEngine;

// Страховка. Если при сборке склада скрипты не прикрепились к объектам
// (например, файлы скриптов добавили уже после сборки), при запуске игры они подключаются сами.
// Если всё подключено правильно, этот скрипт ничего не делает.
public static class GameplayFixer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Run()
    {
        Ensure<MopItem>("Mop");
        Ensure<WorkJack>("WorkJack");
        Ensure<ShipZone>("ShipZone");
        Ensure<KeyItem>("HiddenKey");
        Ensure<CentralGate>("CentralGate");

        PalletSpot spot = Ensure<PalletSpot>("PalletSpot");
        if (spot != null)
        {
            if (spot.palletMaterial == null) spot.palletMaterial = MaterialOf("AlleyPallet");
            if (spot.boxMaterial == null) spot.boxMaterial = MaterialOf("LoneBox");
            if (spot.filmMaterial == null) spot.filmMaterial = MaterialOf("Wrap");
        }

        // Крыши складов: убираем наложение на стены (иначе крыша мерцает)
        Transform[] roofs = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < roofs.Length; i++)
        {
            Transform r = roofs[i];
            if (r.name != "Roof" || r.parent == null || r.parent.name != "Shell") continue;
            Vector3 sc = r.localScale;
            if (sc.x < 1f || sc.z < 1f) continue;
            Transform back = r.parent.Find("WallBack");
            if (back == null) continue;
            float wall = back.localScale.z;                                 // толщина стены
            if (Mathf.Abs(back.localScale.x + 2f * wall - sc.x) > 0.05f) continue;   // уже исправлено или не тот размер
            r.localScale = new Vector3(sc.x - 2f * wall, sc.y, sc.z - 2f * wall);
            Vector3 p = r.localPosition;
            r.localPosition = new Vector3(p.x, p.y - 0.01f, p.z);
        }

        // Свет только в главном складе
        string[] dark = { "WarehouseMonster", "WarehouseBatteries" };
        for (int d = 0; d < dark.Length; d++)
        {
            GameObject w = GameObject.Find(dark[d]);
            Transform lights = w != null ? w.transform.Find("InteriorLights") : null;
            if (lights != null) lights.gameObject.SetActive(false);
        }

        // Склад с монстром: по E только звук, Володя стоит внутри с самого начала
        GameObject mw = GameObject.Find("WarehouseMonster");
        WarehouseDoor md = mw != null ? mw.GetComponentInChildren<WarehouseDoor>(true) : null;
        if (md != null) md.soundOnly = true;
        MonsterController vol = MonsterController.Find();
        if (vol != null) vol.ReturnHome();

        // Аккумуляторы в самом левом складе
        Transform[] pieces = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i].name != "Battery") continue;
            if (pieces[i].GetComponent<BatteryPickup>() == null)
            {
                pieces[i].gameObject.AddComponent<BatteryPickup>();
                Debug.Log("Подключён скрипт BatteryPickup к аккумулятору");
            }
        }

        // Весь мусор у входа
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name != "Trash") continue;
            if (all[i].GetComponent<TrashPiece>() == null)
            {
                all[i].gameObject.AddComponent<TrashPiece>();
                Debug.Log("Подключён скрипт TrashPiece к куску мусора");
            }
        }

        // Мусорный контейнер под эстакадой (если склада собран раньше — ставим новый, старый ящик убираем)
        GameObject oldBin = GameObject.Find("TrashBin");
        if (oldBin == null || oldBin.transform.Find("Shell") == null)
        {
            GameObject g = GameObject.Find("Gantry");
            Material binMat = null;
            if (oldBin != null)
            {
                Renderer br = oldBin.GetComponentInChildren<Renderer>();
                if (br != null) binMat = br.sharedMaterial;
                oldBin.name = "TrashBin_old";
                Object.Destroy(oldBin);
            }
            if (g != null) Extras.TrashBin(g.transform, Vector3.zero, 0f, binMat != null ? binMat : BinMaterial());
        }

        // 4 лампы на фасаде центрального склада
        if (GameObject.Find("FacadeLamp") == null)
        {
            GameObject c = GameObject.Find("CentralWarehouse");
            GameObject g = GameObject.Find("Gantry");
            Transform post = g != null ? g.transform.Find("Post") : null;
            if (c != null) Extras.FacadeLamps(c.transform, MaterialOf("Fixture"),
                post != null ? post.GetComponent<Renderer>().sharedMaterial : null);
        }

        FixKeyChair();
        FixBorders();
        AddHidingSpots();

        // Невидимая стена перед воротами складов (зона «E — войти»): делаем проходимой
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == "DoorTrigger_Outside")
            {
                BoxCollider c = t.GetComponent<BoxCollider>();
                if (c != null) c.isTrigger = true;
            }

        // Машина у склада больше не нужна (если осталась от старой сборки склада)
        GameObject van = GameObject.Find("ParkedVan");
        if (van != null) Object.Destroy(van);
    }

    // Стул у входа убран, ключ лежит на стуле в проходе слева от склада
    static void FixKeyChair()
    {
        Transform alleyChair = null;
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t.name != "Chair" || t.parent == null) continue;
            if (t.parent.name == "YardProps") Object.Destroy(t.gameObject);
            else if (t.parent.name == "Alley") alleyChair = t;
        }
        GameObject key = GameObject.Find("HiddenKey");
        if (key == null || alleyChair == null) return;
        Vector3 seat = alleyChair.TransformPoint(new Vector3(0, 0.49f, 0));
        if ((key.transform.position - seat).sqrMagnitude > 0.04f) key.transform.position = seat;
    }

    // Граница карты раньше стояла перед заводом и не пускала к мусорке под эстакадой
    static void FixBorders()
    {
        GameObject e = GameObject.Find("Border_E");
        if (e == null || e.transform.localPosition.x > 40f) return;   // уже исправлено (склад пересобран)
        e.transform.localPosition = new Vector3(44.5f, 5f, -10f);
        GameObject s = GameObject.Find("Border_S");
        if (s != null)
        {
            s.transform.localPosition = new Vector3(-4f, 5f, -18.5f);
            BoxCollider sc = s.GetComponent<BoxCollider>();
            if (sc != null) sc.size = new Vector3(98f, 10f, 1f);
        }
        GameObject ne = new GameObject("Border_NE");
        ne.transform.SetParent(e.transform.parent, false);
        ne.transform.localPosition = new Vector3(38.5f, 5f, 40f);
        ne.AddComponent<BoxCollider>().size = new Vector3(13f, 10f, 1f);
    }

    // Укрытия: за паллетами на складе и в мусорном контейнере под эстакадой
    static void AddHidingSpots()
    {
        GameObject w = GameObject.Find("Warehouse");
        Vector3 door = w != null ? w.transform.position : Vector3.zero;
        string[] pallets = { "PalletBoxes", "PalletRolls", "PalletFBS" };
        foreach (string n in pallets)
        {
            GameObject p = GameObject.Find(n);
            if (p == null || p.GetComponent<HidingSpot>() != null) continue;
            Vector3 away = p.transform.position - door; away.y = 0f; away.Normalize();
            Interactable it = p.GetComponent<Interactable>();
            if (it == null) it = p.AddComponent<Interactable>();
            HidingSpot hs = p.AddComponent<HidingSpot>();
            hs.eyeWorld = p.transform.position + away * 0.85f + Vector3.up * 0.95f;    // присел за паллетой
            hs.lookAt = door;
            hs.hidePrompt = "E — спрятаться за паллетой";
        }

        GameObject bin = GameObject.Find("TrashBin");
        if (bin != null && bin.transform.Find("HideZone") == null)
        {
            // Отдельная зона у крышки: смотришь на крышку — залезть, на бок — выкинуть мешок
            GameObject z = new GameObject("HideZone");
            z.transform.SetParent(bin.transform, false);
            z.transform.localPosition = new Vector3(0f, 1.28f, 0f);
            BoxCollider c = z.AddComponent<BoxCollider>();
            c.size = new Vector3(1.9f, 0.22f, 1.2f);
            z.AddComponent<Interactable>();
            HidingSpot hs = z.AddComponent<HidingSpot>();
            hs.eyeWorld = bin.transform.TransformPoint(new Vector3(0.4f, 1.02f, 0.25f));   // под приоткрытой крышкой
            hs.lookAt = bin.transform.TransformPoint(new Vector3(0.4f, 1f, 4f));
            hs.hidePrompt = "E — залезть в контейнер";
        }
    }

    static Material BinMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) return MaterialOf("Gate");
        Material m = new Material(sh);
        m.name = "BinGreen";
        m.color = new Color(0.12f, 0.26f, 0.16f);
        return m;
    }

    static T Ensure<T>(string objectName) where T : Component
    {
        GameObject go = GameObject.Find(objectName);
        if (go == null)
        {
            Debug.LogWarning("Не найден объект " + objectName + ". Соберите склад заново: Smena, Построить склад (с нуля).");
            return null;
        }
        T c = go.GetComponent<T>();
        if (c == null)
        {
            c = go.AddComponent<T>();
            Debug.Log("Подключён скрипт " + typeof(T).Name + " к объекту " + objectName);
        }
        return c;
    }

    // Берёт материал у существующего объекта (или у его детей), чтобы паллета выглядела как остальные
    static Material MaterialOf(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        if (go == null) return null;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) r = go.GetComponentInChildren<Renderer>();
        return r != null ? r.sharedMaterial : null;
    }
}
