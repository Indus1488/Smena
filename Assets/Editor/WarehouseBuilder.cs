// «СМЕНА» — построитель склада и двора по фотографиям (версия 5).
// Положите файл в Assets/Editor/WarehouseBuilder.cs (заменив старый).
// Меню Unity: Smena → ...
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.Events;

public static class WarehouseBuilder
{
    const string RootName = "Warehouse";
    const string MatFolder = "Assets/Materials/Smena";

    // ---- Размеры в метрах. Поменяли цифру — построили заново. ----
    const float FloorY = 0.9f;   // высота пола склада над землёй (платформа)
    const float InnerW = 6.0f;   // ширина внутри
    const float InnerD = 12.0f;  // глубина внутри
    const float InnerH = 2.8f;   // высота потолка
    const float WallT = 0.3f;    // толщина бетонных стен
    const float DoorW = 3.2f;    // ширина проёма
    const float DoorH = 2.3f;    // высота проёма

    const float HW = InnerW / 2f;     // половина ширины внутри
    const float OW = HW + WallT;      // половина ширины снаружи
    const float CeilY = FloorY + InnerH;
    const float TopY = CeilY + WallT;

    const float CloneGap = 4.1f;                 // проход между соседними складами
    const float CloneStep = 2f * OW + CloneGap;  // шаг между центрами складов

    static Material concrete, concreteDark, plinthMat, osb, doorOpen, doorClosed, turquoise, rust, wood,
        palletWood, card, cardDark, tape, film, foil, foilDark, jackBlue, jackYellow, black, banner,
        bannerStripe, dirt, asphalt, tile, redDoor, grayDoor, glass, bag, lampMat, gate,
        stripeCream, stripeDark, silver, gantryMat, factoryBlue, chimneyRed, chimneyWhite, boardBrown, monsterMat, eyeMat, binGreen;

    // =====================================================================
    // МЕНЮ
    [MenuItem("Smena/Построить склад (с нуля)")]
    public static void BuildFresh() { BuildInternal(false); }

    [MenuItem("Smena/Обновить склад, сохранив мою расстановку")]
    public static void BuildKeepMine() { BuildInternal(true); }

    [MenuItem("Smena/Сохранить копию склада (бэкап)")]
    public static void BackupMenu()
    {
        GameObject old = GameObject.Find(RootName);
        if (old == null) { Debug.LogWarning("Объект Warehouse не найден."); return; }
        SaveBackup(old);
    }

    [MenuItem("Smena/Удалить склад")]
    public static void RemoveMenu()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    static void SaveBackup(GameObject go)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Backups"))
            AssetDatabase.CreateFolder("Assets", "Backups");
        string path = "Assets/Backups/Warehouse_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Debug.Log("Копия склада сохранена: " + path);
    }

    // =====================================================================
    static void BuildInternal(bool keepMine)
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Смена", "Сначала остановите игру (кнопка Play), потом стройте склад.", "Понятно");
            return;
        }
        GameObject player = GameObject.Find("Player");
        Transform keptInterior = null, keptYard = null;

        GameObject old = GameObject.Find(RootName);
        if (old != null)
        {
            if (keepMine)
            {
                keptInterior = old.transform.Find("Interior");
                keptYard = old.transform.Find("YardProps");
                if (keptInterior != null) keptInterior.SetParent(null, true);
                if (keptYard != null) keptYard.SetParent(null, true);
            }
            else
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Склад уже есть",
                    "Сборка с нуля удалит всё, что вы переставляли внутри объекта Warehouse.\nСохранить резервную копию в префаб?",
                    "Копия и пересобрать", "Отмена", "Пересобрать без копии");
                if (choice == 1) return;
                if (choice == 0) SaveBackup(old);
            }
            Undo.DestroyObjectImmediate(old);
        }

        MakeMaterials();
        if (player == null)
            Debug.LogWarning("Объект Player не найден. Склады построятся, но в компонентах Teleporter на дверях нужно будет вручную указать Player.");

        GameObject rootGO = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootGO, "Build warehouse");
        Transform root = rootGO.transform;

        BuildGround(root);
        BuildShell(root);
        BuildNeighbours(root);
        if (keptYard == null) BuildYardProps(root); else keptYard.SetParent(root, true);
        if (keptInterior == null) BuildInterior(root); else keptInterior.SetParent(root, true);
        BuildGameplay(root);
        BuildLights(root);

        // Два одинаковых склада слева, без интерьера: в первом будет монстр, во втором аккумуляторы
        // Самый левый (аккумуляторы) заперт: нужен ключ, спрятанный на стуле на улице.
        BuildClone(root, "WarehouseMonster", -CloneStep, player, false, false);
        BuildClone(root, "WarehouseBatteries", -2f * CloneStep, player, true, true);
        BuildMonster(root);
        BuildGazelle(root);
        BuildBusStop(root);

        // Основной склад
        BuildEntrance(root, player, 3.0f, false);

        if (player != null)
        {
            Undo.RecordObject(player.transform, "Move player");
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(0, 1.05f, -9f);
            player.transform.rotation = Quaternion.identity;
            if (cc != null) cc.enabled = true;
        }

        AssetDatabase.SaveAssets();
        TextureApplier.Apply();   // сразу накладываем текстуры (если они лежат в Assets/Textures/Smena)
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = rootGO;
        Debug.Log(keepMine ? "Склад обновлён, ваша расстановка сохранена." : "Склад построен с нуля.");
    }

    // Склад-двойник без интерьера: оболочка, двери, ступени, детали на крыше и свет
    static void BuildClone(Transform root, string name, float x, GameObject player, bool needsKey, bool hasBatteries)
    {
        Transform w = Group(name, root, new Vector3(x, 0, 0));
        BuildShell(w);
        BuildRoofDetails(w);
        // свет есть только в главном складе
        BuildEntrance(w, player, 2.0f, needsKey);
        if (hasBatteries) BuildBatteries(w);
    }

    // Аккумуляторы, разбросанные по самому левому складу (искать с фонариком)
    static void BuildBatteries(Transform w)
    {
        Transform g = Group("Batteries", w, Vector3.zero);
        float[,] spots = {
            { -2.2f, 3.2f, 20f },
            {  1.8f, 8.4f, 10f },
        };
        for (int i = 0; i < spots.GetLength(0); i++)
        {
            Transform b = Group("Battery", g, new Vector3(spots[i, 0], FloorY, spots[i, 1]), spots[i, 2]);
            NoCol(Cube("Body", b, new Vector3(0, 0.11f, 0), new Vector3(0.30f, 0.22f, 0.18f), foilDark));
            NoCol(Cube("Label", b, new Vector3(0, 0.11f, 0.092f), new Vector3(0.22f, 0.10f, 0.004f), tape));
            NoCol(Cube("TerminalL", b, new Vector3(-0.09f, 0.24f, 0), new Vector3(0.04f, 0.03f, 0.04f), foil));
            NoCol(Cube("TerminalR", b, new Vector3(0.09f, 0.24f, 0), new Vector3(0.04f, 0.03f, 0.04f), foil));

            // Увеличенный коллайдер, чтобы в аккумулятор было легко попасть лучом фонарика и взглядом
            BoxCollider bc = b.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.12f, 0);
            bc.size = new Vector3(0.5f, 0.4f, 0.4f);
            Interactable bi = b.gameObject.AddComponent<Interactable>();
            bi.prompt = "E — взять аккумулятор";
            TryAdd(b.gameObject, "BatteryPickup");
        }
    }

    // Володя: тонкий и слишком высокий силуэт со светящимися глазами. Выключен, пока его не позовёт ActController.
    static void BuildMonster(Transform root)
    {
        Transform m = Group("Volodya", root, new Vector3(-CloneStep, FloorY, 6f));
        NoCol(Cube("Torso", m, new Vector3(0, 1.15f, 0), new Vector3(0.45f, 1.0f, 0.28f), monsterMat));
        NoCol(Cube("Head", m, new Vector3(0, 1.9f, 0.06f), new Vector3(0.34f, 0.42f, 0.4f), monsterMat));
        NoCol(Cube("EyeL", m, new Vector3(-0.08f, 1.95f, 0.27f), new Vector3(0.07f, 0.05f, 0.03f), eyeMat));
        NoCol(Cube("EyeR", m, new Vector3(0.08f, 1.95f, 0.27f), new Vector3(0.07f, 0.05f, 0.03f), eyeMat));
        NoCol(CubeR("ArmL", m, new Vector3(-0.42f, 1.0f, 0.1f), new Vector3(0.08f, 1.5f, 0.08f), monsterMat, new Vector3(0, 0, 10)));
        NoCol(CubeR("ArmR", m, new Vector3(0.42f, 1.0f, 0.1f), new Vector3(0.08f, 1.5f, 0.08f), monsterMat, new Vector3(0, 0, -10)));
        NoCol(Cube("LegL", m, new Vector3(-0.12f, 0.45f, 0), new Vector3(0.12f, 0.9f, 0.12f), monsterMat));
        NoCol(Cube("LegR", m, new Vector3(0.12f, 0.45f, 0), new Vector3(0.12f, 0.9f, 0.12f), monsterMat));
        TryAdd(m.gameObject, "MonsterController");
        m.gameObject.SetActive(false);
    }

    // Газель: белый грузовик с синей кабиной. Выключена, пока её не вызовут по телефону.
    static void BuildGazelle(Transform root)
    {
        Transform gz = Group("Gazelle", root, new Vector3(-42f, 0f, -6.5f));
        NoCol(Cube("Chassis", gz, new Vector3(0, 0.55f, 0), new Vector3(2.0f, 0.3f, 6.2f), black));
        NoCol(Cube("Cargo", gz, new Vector3(0, 1.85f, -0.9f), new Vector3(2.1f, 2.1f, 4.2f), film));
        NoCol(Cube("Cab", gz, new Vector3(0, 1.45f, 2.3f), new Vector3(2.0f, 1.5f, 1.7f), jackBlue));
        NoCol(Cube("Windshield", gz, new Vector3(0, 1.75f, 3.17f), new Vector3(1.7f, 0.7f, 0.05f), glass));
        for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++)
                NoCol(Cyl("Wheel", gz, new Vector3(a == 0 ? -1.0f : 1.0f, 0.4f, b == 0 ? -1.8f : 1.8f),
                    new Vector3(0.8f, 0.15f, 0.8f), new Vector3(0, 0, 90), black));
        TryAdd(gz.gameObject, "GazelleController");
        gz.gameObject.SetActive(false);
    }

    // Остановка возле завода, где была лампа. Подойдёте в финале, и случится скример.
    static void BuildBusStop(Transform root)
    {
        Transform b = Group("BusStop", root, new Vector3(16.5f, 0f, 9f));
        NoCol(Cyl("Post", b, new Vector3(0, 1.5f, 0), new Vector3(0.06f, 1.5f, 0.06f), Vector3.zero, foilDark));
        NoCol(Cube("Sign", b, new Vector3(0, 3.1f, 0), new Vector3(0.7f, 0.6f, 0.05f), jackBlue));
        Cube("Bench", b, new Vector3(1.2f, 0.45f, 0), new Vector3(1.6f, 0.08f, 0.45f), wood);
    }

    // Вход в склад: снаружи ворота закрыты, изнутри открыты. Плюс метки появления и двери-триггеры.
    static void BuildEntrance(Transform w, GameObject player, float spawnInZ, bool needsKey)
    {
        GameObject closed, open;
        Transform doors = BuildDoors(w, out closed, out open);

        Transform spawnIn = Group("SpawnInside", w, new Vector3(0, FloorY + 1.05f, spawnInZ), 0f);
        Transform spawnOut = Group("SpawnOutside", w, new Vector3(0, 1.05f, -4.2f), 180f);

        GameObject outT = MakeDoorTrigger(w, "DoorTrigger_Outside", new Vector3(0, 1.55f, -2.2f),
            new Vector3(DoorW + 1.2f, 3.1f, 0.1f), "E — войти", player, spawnIn);
        GameObject inT = MakeDoorTrigger(w, "DoorTrigger_Inside", new Vector3(0, FloorY + InnerH / 2f, 0.6f),
            new Vector3(InnerW, InnerH, 0.1f), "E — выйти", player, spawnOut);

        Component wd = TryAdd(doors.gameObject, "WarehouseDoor");
        if (wd != null)
        {
            SetRef(wd, "outside", outT.GetComponent<Interactable>());
            SetRef(wd, "inside", inT.GetComponent<Interactable>());
            SetRef(wd, "enterTp", outT.GetComponent<Teleporter>());
            SetRef(wd, "exitTp", inT.GetComponent<Teleporter>());
            SetRef(wd, "closedDoors", closed);
            SetRef(wd, "openDoors", open);
            SetBool(wd, "needsKey", needsKey);
        }
        else
        {
            // Запасной вариант без замка и смены дверей
            WireTeleport(outT);
            WireTeleport(inT);
        }
    }

    // =====================================================================
    // МАТЕРИАЛЫ
    static Material Mat(string name, Color c, float smooth = 0.1f, float metal = 0f)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Smena");

        string path = MatFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        smooth = Mathf.Min(smooth, 0.05f);   // матовый ретро-вид (PS1)
        metal = 0f;
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void MakeMaterials()
    {
        concrete = Mat("Concrete", new Color(0.82f, 0.82f, 0.80f), 0.05f);
        concreteDark = Mat("ConcreteDark", new Color(0.52f, 0.53f, 0.53f), 0.05f);
        plinthMat = Mat("Plinth", new Color(0.70f, 0.69f, 0.64f), 0.05f);
        osb = Mat("OSB", new Color(0.68f, 0.55f, 0.36f), 0.1f);
        doorOpen = Mat("DoorOpen", new Color(0.70f, 0.76f, 0.78f), 0.3f, 0.3f);
        doorClosed = Mat("DoorClosed", new Color(0.55f, 0.27f, 0.13f), 0.2f, 0.3f);
        turquoise = Mat("Turquoise", new Color(0.20f, 0.65f, 0.70f), 0.2f);
        rust = Mat("Rust", new Color(0.35f, 0.20f, 0.12f), 0.1f, 0.3f);
        wood = Mat("Wood", new Color(0.30f, 0.20f, 0.12f), 0.1f);
        palletWood = Mat("PalletWood", new Color(0.55f, 0.40f, 0.22f), 0.05f);
        card = Mat("Cardboard", new Color(0.72f, 0.55f, 0.34f), 0.05f);
        cardDark = Mat("CardboardDark", new Color(0.60f, 0.45f, 0.27f), 0.05f);
        tape = Mat("TapeRed", new Color(0.80f, 0.10f, 0.10f), 0.4f);
        film = Mat("FilmWhite", new Color(0.92f, 0.93f, 0.95f), 0.7f);
        foil = Mat("Foil", new Color(0.72f, 0.75f, 0.78f), 0.6f, 0.7f);
        foilDark = Mat("FoilDark", new Color(0.25f, 0.27f, 0.28f), 0.4f, 0.3f);
        jackBlue = Mat("JackBlue", new Color(0.10f, 0.30f, 0.65f), 0.4f, 0.3f);
        jackYellow = Mat("JackYellow", new Color(0.90f, 0.70f, 0.10f), 0.3f);
        black = Mat("Black", new Color(0.05f, 0.05f, 0.05f), 0.2f);
        banner = Mat("BannerPurple", new Color(0.55f, 0.10f, 0.60f), 0.2f);
        bannerStripe = Mat("BannerStripe", new Color(0.92f, 0.85f, 0.95f), 0.2f);
        dirt = Mat("Dirt", new Color(0.36f, 0.31f, 0.26f), 0.0f);
        asphalt = Mat("Asphalt", new Color(0.16f, 0.16f, 0.17f), 0.1f);
        tile = Mat("Tile", new Color(0.80f, 0.76f, 0.62f), 0.1f);
        redDoor = Mat("RedDoor", new Color(0.62f, 0.20f, 0.17f), 0.2f, 0.2f);
        grayDoor = Mat("GrayDoor", new Color(0.55f, 0.58f, 0.58f), 0.3f, 0.3f);
        glass = Mat("Glass", new Color(0.15f, 0.20f, 0.25f), 0.8f);
        bag = Mat("Bag", new Color(0.04f, 0.04f, 0.05f), 0.5f);
        lampMat = Mat("LampWhite", new Color(0.95f, 0.95f, 0.9f), 0.3f);
        gate = Mat("Gate", new Color(0.20f, 0.22f, 0.24f), 0.3f, 0.4f);
        stripeCream = Mat("StripeCream", new Color(0.93f, 0.88f, 0.72f), 0.1f);
        stripeDark = Mat("StripeDark", new Color(0.18f, 0.18f, 0.20f), 0.1f);
        silver = Mat("Silver", new Color(0.66f, 0.68f, 0.70f), 0.6f, 0.5f);
        gantryMat = Mat("GantryRust", new Color(0.38f, 0.22f, 0.15f), 0.2f, 0.4f);
        factoryBlue = Mat("FactoryBlue", new Color(0.62f, 0.66f, 0.72f), 0.1f);
        chimneyRed = Mat("ChimneyRed", new Color(0.75f, 0.15f, 0.12f), 0.2f);
        chimneyWhite = Mat("ChimneyWhite", new Color(0.92f, 0.92f, 0.92f), 0.2f);
        binGreen = Mat("BinGreen", new Color(0.12f, 0.26f, 0.16f), 0.3f, 0.2f);
        boardBrown = Mat("BoardBrown", new Color(0.25f, 0.17f, 0.12f), 0.15f);
        monsterMat = Mat("Monster", new Color(0.02f, 0.02f, 0.025f), 0.35f);
        eyeMat = Mat("Eye", Color.white, 0.5f);
        eyeMat.EnableKeyword("_EMISSION");
        eyeMat.SetColor("_EmissionColor", new Color(3f, 3f, 3f));
        EditorUtility.SetDirty(eyeMat);
    }

    // =====================================================================
    // ПОМОЩНИКИ
    static Transform Group(string name, Transform parent, Vector3 pos, float rotY = 0f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, rotY, 0);
        return go.transform;
    }

    static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 size, Material m, float rotY = 0f)
    {
        return CubeR(name, parent, pos, size, m, new Vector3(0, rotY, 0));
    }

    static GameObject CubeR(string name, Transform parent, Vector3 pos, Vector3 size, Material m, Vector3 euler)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static GameObject Cyl(string name, Transform parent, Vector3 pos, Vector3 size, Vector3 euler, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static void NoCol(GameObject go)
    {
        Collider c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
    }

    static void Invisible(string name, Transform parent, Vector3 pos, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<BoxCollider>().size = size;
    }

    // Добавляет скрипт по имени. Если файла скрипта в проекте ещё нет — просто предупреждает.
    static Component TryAdd(GameObject go, string typeName)
    {
        System.Type t = System.Type.GetType(typeName + ", Assembly-CSharp");
        if (t == null)
        {
            Debug.LogWarning("Скрипт " + typeName + " не найден. Положите его в Assets/Scripts и соберите склад заново.");
            return null;
        }
        return go.AddComponent(t);
    }

    // Записывает ссылку на объект в поле скрипта (если скрипт и поле существуют)
    static void SetRef(Component c, string field, Object value)
    {
        if (c == null) return;
        SerializedObject so = new SerializedObject(c);
        SerializedProperty pr = so.FindProperty(field);
        if (pr == null) return;
        pr.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    static void SetFloat(Component c, string field, float value)
    {
        if (c == null) return;
        SerializedObject so = new SerializedObject(c);
        SerializedProperty pr = so.FindProperty(field);
        if (pr == null) return;
        pr.floatValue = value;
        so.ApplyModifiedProperties();
    }

    static void SetBool(Component c, string field, bool value)
    {
        if (c == null) return;
        SerializedObject so = new SerializedObject(c);
        SerializedProperty pr = so.FindProperty(field);
        if (pr == null) return;
        pr.boolValue = value;
        so.ApplyModifiedProperties();
    }

    // Поддон 1.2 x 0.8 м, верх на высоте 0.15 м
    static Transform Pallet(string name, Transform parent, Vector3 pos, float rotY)
    {
        Transform p = Group(name, parent, pos, rotY);
        Cube("Deck", p, new Vector3(0, 0.13f, 0), new Vector3(1.2f, 0.04f, 0.8f), palletWood);
        for (int i = -1; i <= 1; i++)
            Cube("Runner", p, new Vector3(i * 0.5f, 0.055f, 0), new Vector3(0.1f, 0.11f, 0.8f), palletWood);
        return p;
    }

    static void TapedBox(Transform parent, string name, Vector3 pos, Vector3 size, Material m, float rotY = 0f)
    {
        GameObject b = Cube(name, parent, pos, size, m, rotY);
        NoCol(Cube("Tape", b.transform, Vector3.zero, new Vector3(1.02f, 1.02f, 0.14f), tape));
        NoCol(Cube("Tape2", b.transform, Vector3.zero, new Vector3(0.14f, 1.02f, 1.02f), tape));
    }

    static Transform PalletJack(Transform parent, Vector3 pos, float rotY, Material body)
    {
        Transform j = Group("PalletJack", parent, pos, rotY);
        Cube("ForkL", j, new Vector3(-0.27f, 0.045f, 0.55f), new Vector3(0.16f, 0.09f, 1.1f), body);
        Cube("ForkR", j, new Vector3(0.27f, 0.045f, 0.55f), new Vector3(0.16f, 0.09f, 1.1f), body);
        Cube("Body", j, new Vector3(0, 0.17f, -0.05f), new Vector3(0.7f, 0.24f, 0.25f), body);
        Cyl("Handle", j, new Vector3(0, 0.70f, -0.15f), new Vector3(0.05f, 0.55f, 0.05f), Vector3.zero, black);
        Cube("Grip", j, new Vector3(0, 1.25f, -0.15f), new Vector3(0.45f, 0.05f, 0.05f), black);
        return j;
    }

    static void Chair(Transform parent, Vector3 pos, float rotY)
    {
        Transform c = Group("Chair", parent, pos, rotY);
        Cube("Seat", c, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.06f, 0.45f), black);
        Cube("Back", c, new Vector3(0, 0.75f, -0.2f), new Vector3(0.42f, 0.4f, 0.05f), black);
        for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++)
                Cube("Leg", c, new Vector3(a == 0 ? -0.19f : 0.19f, 0.22f, b == 0 ? -0.19f : 0.19f),
                    new Vector3(0.03f, 0.44f, 0.03f), foil);
    }

    static void Eyebolt(Transform parent, Vector3 pos)
    {
        NoCol(Cube("EyeL", parent, pos + new Vector3(-0.12f, 0.12f, 0), new Vector3(0.04f, 0.24f, 0.04f), black));
        NoCol(Cube("EyeR", parent, pos + new Vector3(0.12f, 0.12f, 0), new Vector3(0.04f, 0.24f, 0.04f), black));
        NoCol(Cube("EyeTop", parent, pos + new Vector3(0, 0.25f, 0), new Vector3(0.28f, 0.04f, 0.04f), black));
    }

    // =====================================================================
    // ЗЕМЛЯ, АСФАЛЬТ, ДОРОГА, НЕВИДИМЫЕ ГРАНИЦЫ
    static void BuildGround(Transform root)
    {
        Transform g = Group("Ground", root, Vector3.zero);

        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Dirt";
        plane.transform.SetParent(g, false);
        plane.transform.localPosition = new Vector3(0, -0.01f, -10f);
        plane.transform.localScale = new Vector3(10, 1, 10);
        plane.GetComponent<Renderer>().sharedMaterial = dirt;

        GameObject yard = GameObject.CreatePrimitive(PrimitiveType.Plane);
        yard.name = "AsphaltYard";
        yard.transform.SetParent(g, false);
        yard.transform.localPosition = new Vector3(OW + 1f + 15f, 0.005f, -5f);
        yard.transform.localScale = new Vector3(3, 1, 6);
        yard.GetComponent<Renderer>().sharedMaterial = asphalt;

        GameObject road = GameObject.CreatePrimitive(PrimitiveType.Plane);
        road.name = "Road";
        road.transform.SetParent(g, false);
        road.transform.localPosition = new Vector3(0, 0f, -24f);
        road.transform.localScale = new Vector3(10, 1, 1.2f);
        road.GetComponent<Renderer>().sharedMaterial = asphalt;

        Transform b = Group("Borders", root, Vector3.zero);
        Invisible("Border_W", b, new Vector3(-36.5f, 5, -10), new Vector3(1, 10, 100));
        // Восточная граница за эстакадой (там мусорка), северный край — от завода до неё
        Invisible("Border_E", b, new Vector3(44.5f, 5, -10), new Vector3(1, 10, 100));
        Invisible("Border_NE", b, new Vector3(38.5f, 5, 40f), new Vector3(13, 10, 1));
        Invisible("Border_S", b, new Vector3(-4f, 5, -18.5f), new Vector3(98, 10, 1));
    }

    // =====================================================================
    // БЕТОННАЯ КОРОБКА СКЛАДА
    static void BuildShell(Transform root)
    {
        Transform s = Group("Shell", root, Vector3.zero);
        float zMin = -0.8f;
        float zMax = InnerD + WallT;
        float plinthH = FloorY - 0.1f;
        float wallBottom = FloorY - 0.1f;
        float wallH = TopY - wallBottom;
        float wallCY = (TopY + wallBottom) / 2f;

        Cube("Plinth", s, new Vector3(0, plinthH / 2f, (zMin + zMax) / 2f), new Vector3(2 * OW, plinthH, zMax - zMin), plinthMat);
        Cube("Floor", s, new Vector3(0, FloorY - 0.05f, InnerD / 2f), new Vector3(InnerW, 0.1f, InnerD), osb);
        Cube("Ledge", s, new Vector3(0, FloorY - 0.05f, (zMin - WallT) / 2f), new Vector3(2 * OW, 0.1f, -WallT - zMin), plinthMat);
        Cube("Sill", s, new Vector3(0, FloorY - 0.05f, -WallT / 2f), new Vector3(DoorW, 0.1f, WallT), plinthMat);

        Cube("WallLeft", s, new Vector3(-(HW + WallT / 2f), wallCY, InnerD / 2f), new Vector3(WallT, wallH, InnerD + 2 * WallT), concrete);
        Cube("WallRight", s, new Vector3(HW + WallT / 2f, wallCY, InnerD / 2f), new Vector3(WallT, wallH, InnerD + 2 * WallT), concrete);
        Cube("WallBack", s, new Vector3(0, wallCY, InnerD + WallT / 2f), new Vector3(InnerW, wallH, WallT), concrete);

        float pw = (2 * OW - DoorW) / 2f;
        Cube("PillarLeft", s, new Vector3(-(DoorW / 2f + pw / 2f), wallCY, -WallT / 2f), new Vector3(pw, wallH, WallT), concrete);
        Cube("PillarRight", s, new Vector3(DoorW / 2f + pw / 2f, wallCY, -WallT / 2f), new Vector3(pw, wallH, WallT), concrete);
        float lintelH = TopY - (FloorY + DoorH);
        Cube("Lintel", s, new Vector3(0, FloorY + DoorH + lintelH / 2f, -WallT / 2f), new Vector3(2 * OW, lintelH, WallT), concrete);

        // Крыша лежит МЕЖДУ стенами: если она накрывает стены сверху, их грани совпадают и мерцают
        Cube("Roof", s, new Vector3(0, CeilY + WallT / 2f, InnerD / 2f), new Vector3(InnerW, WallT, InnerD), concreteDark);
    }

    // =====================================================================
    // ВОРОТА (закрытые снаружи, открытые изнутри) И СТУПЕНИ
    static Transform BuildDoors(Transform root, out GameObject closedGO, out GameObject openGO)
    {
        Transform d = Group("Doors", root, Vector3.zero);

        // Закрытые ворота: ржавые, с бирюзовой «заплаткой» и замком
        Transform closed = Group("Closed", d, Vector3.zero);
        float lw = DoorW / 2f;
        Cube("DoorLeaf_L", closed, new Vector3(-lw / 2f, FloorY + DoorH / 2f, -0.06f), new Vector3(lw - 0.04f, DoorH - 0.04f, 0.08f), doorClosed);
        Cube("DoorLeaf_R", closed, new Vector3(lw / 2f, FloorY + DoorH / 2f, -0.06f), new Vector3(lw - 0.04f, DoorH - 0.04f, 0.08f), doorClosed);
        NoCol(Cube("DoorPatch", closed, new Vector3(-lw / 2f, FloorY + 0.45f, -0.115f), new Vector3(lw - 0.1f, 0.9f, 0.02f), turquoise));
        NoCol(Cube("Padlock", closed, new Vector3(0, FloorY + 1.0f, -0.14f), new Vector3(0.1f, 0.14f, 0.05f), black));

        // Открытые ворота: распахнуты наружу (видны, когда вы внутри)
        Transform open = Group("Open", d, Vector3.zero);
        float leaf = DoorW / 2f - 0.05f;
        for (int k = 0; k < 2; k++)
        {
            float side = (k == 0) ? -1f : 1f;
            float x = side * (DoorW / 2f + 0.04f);
            float z = -WallT - leaf / 2f;
            Cube(k == 0 ? "DoorLeaf_L" : "DoorLeaf_R", open, new Vector3(x, FloorY + DoorH / 2f, z), new Vector3(0.06f, DoorH, leaf), doorOpen);
            Cube("Rib", open, new Vector3(x, FloorY + 0.10f, z), new Vector3(0.09f, 0.10f, leaf), rust);
            Cube("Rib", open, new Vector3(x, FloorY + DoorH / 2f, z), new Vector3(0.09f, 0.08f, leaf), rust);
            Cube("Rib", open, new Vector3(x, FloorY + DoorH - 0.10f, z), new Vector3(0.09f, 0.10f, leaf), rust);
        }

        Cube("Step1", d, new Vector3(0.5f, 0.15f, -1.575f), new Vector3(1.5f, 0.3f, 0.55f), wood);
        Cube("Step2", d, new Vector3(0.5f, 0.30f, -1.05f), new Vector3(1.5f, 0.6f, 0.5f), wood);

        open.gameObject.SetActive(false);
        closedGO = closed.gameObject;
        openGO = open.gameObject;
        return d;
    }

    // Детали на крыше: подъёмные петли, прожектор, молниеотводы
    static void BuildRoofDetails(Transform parent)
    {
        Transform r = Group("RoofDetails", parent, Vector3.zero);
        Eyebolt(r, new Vector3(-(OW - 0.6f), TopY + 0.01f, 0.3f));
        Eyebolt(r, new Vector3(OW - 0.6f, TopY + 0.01f, 0.3f));
        Eyebolt(r, new Vector3(-(OW - 0.6f), TopY + 0.01f, InnerD - 0.1f));
        Eyebolt(r, new Vector3(OW - 0.6f, TopY + 0.01f, InnerD - 0.1f));
        NoCol(Cube("FloodStand", r, new Vector3(0.6f, TopY + 0.2f, 0.9f), new Vector3(0.05f, 0.4f, 0.05f), black));
        NoCol(Cyl("Floodlight", r, new Vector3(0.6f, TopY + 0.5f, 0.9f), new Vector3(0.45f, 0.1f, 0.45f), new Vector3(90, 0, 0), black));
        NoCol(Cyl("LightningRod1", r, new Vector3(-1.4f, TopY + 3f, 2.0f), new Vector3(0.03f, 3f, 0.03f), Vector3.zero, black));
        NoCol(Cyl("LightningRod2", r, new Vector3(1.6f, TopY + 3f, InnerD * 0.6f), new Vector3(0.03f, 3f, 0.03f), Vector3.zero, black));
    }

    // =====================================================================
    // ОКРУЖЕНИЕ
    static void BuildNeighbours(Transform root)
    {
        Transform n = Group("Neighbours", root, Vector3.zero);
        float backFace = InnerD + WallT;

        // Длинное здание за складами, облицованное плиткой (идёт и за двумя клонами слева)
        Cube("TileBuilding", n, new Vector3(-10f, 2.6f, backFace + 5f), new Vector3(60, 5.2f, 10), tile);
        Cube("DoorRed", n, new Vector3(-(OW + 3.0f), 1.4f, backFace - 0.04f), new Vector3(1.3f, 2.8f, 0.08f), redDoor);
        Cube("DoorGray", n, new Vector3(-(OW + 1.1f), 1.1f, backFace - 0.04f), new Vector3(1.2f, 2.2f, 0.08f), grayDoor);
        float[] wx = { -34f, -29f, 8f, 13f };
        for (int i = 0; i < wx.Length; i++)
            Cube("Window", n, new Vector3(wx[i], 3.9f, backFace - 0.03f), new Vector3(1.6f, 1.0f, 0.06f), glass);

        // Завод: длинной стороной вдоль Z, перпендикулярно плиточному зданию. Центр по Z = 35.
        float factoryZ = 35f, factoryLen = 42f;
        float fz0 = factoryZ - factoryLen / 2f;      // южный торец, виден со двора
        Cube("Factory", n, new Vector3(26.5f, 5f, factoryZ), new Vector3(12, 10, factoryLen), factoryBlue);

        // Окна на западной стене (двигаются вместе с заводом)
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < 9; i++)
                NoCol(Cube("FactoryWindow", n, new Vector3(20.47f, row == 0 ? 3f : 7f, fz0 + 4f + i * 4f), new Vector3(0.06f, 1.2f, 3f), glass));

        // Окна на южном торце
        float[] sx = { 22.5f, 24.5f, 29.5f, 31.5f };
        for (int row = 0; row < 2; row++)
            for (int j = 0; j < sx.Length; j++)
                NoCol(Cube("FactoryWindow", n, new Vector3(sx[j], row == 0 ? 2.5f : 8.5f, fz0 - 0.03f), new Vector3(1.6f, 1.2f, 0.06f), glass));

        // Лампа на заводе, на южном торце, лицом ко двору (включится в акте 2 и разобьётся)
        Transform lamp = Group("FactoryLamp", n, new Vector3(22f, 3f, 43.5f), 180f);
        NoCol(Cyl("Housing", lamp, Vector3.zero, new Vector3(0.45f, 0.12f, 0.45f), new Vector3(90, 0, 0), lampMat));
        GameObject lg = new GameObject("Light");
        lg.transform.SetParent(lamp, false);
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 14f;
        l.intensity = 3f;
        l.color = new Color(1f, 0.9f, 0.7f);
        l.enabled = false; // выключена, пока не наступит 18:00

        // Ржавая эстакада стоит у здания завода, перед южным торцом
        Transform g = Group("Gantry", n, new Vector3(37f, 0, 20f));
        float[] px = { -4f, 4f };
        float[] pz = { -3f, 3f };
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                Cube("Post", g, new Vector3(px[i], 2.6f, pz[j]), new Vector3(0.22f, 5.2f, 0.22f), gantryMat);
        Cube("Deck", g, new Vector3(0, 3.6f, 0), new Vector3(8.2f, 0.15f, 6.2f), gantryMat);
        Cube("Roof", g, new Vector3(0, 5.25f, 0), new Vector3(8.6f, 0.12f, 6.6f), gantryMat);
        NoCol(Cube("RailFront", g, new Vector3(0, 4.2f, -3.05f), new Vector3(8.2f, 0.06f, 0.06f), gantryMat));
        NoCol(Cube("RailBack", g, new Vector3(0, 4.2f, 3.05f), new Vector3(8.2f, 0.06f, 0.06f), gantryMat));

        // Мусорка под эстакадой: сюда выкидывается мусорный мешок
        Extras.TrashBin(g, Vector3.zero, 0f, binGreen);

        // Центральный склад. Он ПОЛЫЙ: в 18:00 ворота поднимаются, и внутри видны тёмная глубина и надпись.
        Transform c = Group("CentralWarehouse", n, Vector3.zero);
        float faceZ = -30f, cw = 35f, ch = 22f, cdepth = 40f, gw = 6f, gh = 5f;
        float sideW = cw - gw / 2f;   // ширина левой и правой частей фасада

        Cube("FrontLeft", c, new Vector3(-(cw + gw / 2f) / 2f, ch / 2f, faceZ - 0.3f), new Vector3(sideW, ch, 0.6f), stripeCream);
        Cube("FrontRight", c, new Vector3((cw + gw / 2f) / 2f, ch / 2f, faceZ - 0.3f), new Vector3(sideW, ch, 0.6f), stripeCream);
        Cube("FrontTop", c, new Vector3(0, gh + (ch - gh) / 2f, faceZ - 0.3f), new Vector3(gw, ch - gh, 0.6f), stripeCream);
        Cube("Floor", c, new Vector3(0, 0.05f, faceZ - cdepth / 2f), new Vector3(2 * cw, 0.1f, cdepth), concreteDark);
        Cube("Ceiling", c, new Vector3(0, ch, faceZ - cdepth / 2f), new Vector3(2 * cw, 0.5f, cdepth), concreteDark);
        Cube("WallLeft", c, new Vector3(-cw, ch / 2f, faceZ - cdepth / 2f), new Vector3(0.6f, ch, cdepth), stripeCream);
        Cube("WallRight", c, new Vector3(cw, ch / 2f, faceZ - cdepth / 2f), new Vector3(0.6f, ch, cdepth), stripeCream);
        Cube("WallBack", c, new Vector3(0, ch / 2f, faceZ - cdepth), new Vector3(2 * cw, ch, 0.6f), stripeCream);

        // Тёмные полосы и окна на фасаде
        float[] bandY = { 4f, 8.5f, 13f, 17.5f };
        for (int i = 0; i < bandY.Length; i++)
        {
            if (i == 0)
            {
                NoCol(Cube("DarkBand", c, new Vector3(-19f, bandY[i], faceZ + 0.03f), new Vector3(sideW, 2.2f, 0.06f), stripeDark));
                NoCol(Cube("DarkBand", c, new Vector3(19f, bandY[i], faceZ + 0.03f), new Vector3(sideW, 2.2f, 0.06f), stripeDark));
            }
            else
            {
                NoCol(Cube("DarkBand", c, new Vector3(0, bandY[i], faceZ + 0.03f), new Vector3(2 * cw, 2.2f, 0.06f), stripeDark));
                NoCol(Cube("Windows", c, new Vector3(0, bandY[i] + 2.1f, faceZ + 0.03f), new Vector3(50, 0.9f, 0.06f), glass));
            }
        }

        // Ворота: закрыты, поднимаются в 18:00
        GameObject gateGO = Cube("CentralGate", c, new Vector3(0, gh / 2f, faceZ - 0.3f), new Vector3(gw - 0.02f, gh, 0.5f), gate);
        TryAdd(gateGO, "CentralGate");

        // Перегородка за воротами с надписью «Оно любит свет» (текст показывается субтитрами)
        Cube("Partition", c, new Vector3(0, 3f, faceZ - 8f), new Vector3(16f, 6f, 0.4f), concreteDark);
        NoCol(Cube("Inscription", c, new Vector3(0, 3.2f, faceZ - 7.78f), new Vector3(6f, 1.2f, 0.04f), tape));

        // 4 лампы на фасаде: стоят с начала, загораются в 17:30, в 18:00 их разбивает Володя
        Extras.FacadeLamps(c, lampMat, gantryMat);

        // Красно-белая труба на крыше
        for (int i = 0; i < 6; i++)
            Cyl("Chimney", c, new Vector3(-20f, 23f + i * 2f, faceZ - 15f), new Vector3(1.2f, 1f, 1.2f), Vector3.zero,
                (i % 2 == 0) ? chimneyRed : chimneyWhite);
    }

    // =====================================================================
    // ДВОР
    static void BuildYardProps(Transform root)
    {
        Transform y = Group("YardProps", root, Vector3.zero);

        BuildRoofDetails(y);

        // Баннер на правой стене
        Cube("Banner", y, new Vector3(OW + 0.02f, 2.9f, 1.8f), new Vector3(0.04f, 0.9f, 2.6f), banner);
        NoCol(Cube("BannerStripe", y, new Vector3(OW + 0.04f, 2.9f, 1.8f), new Vector3(0.02f, 0.14f, 2.2f), bannerStripe));

        // Щебень у входа
        System.Random rnd = new System.Random(3);
        for (int i = 0; i < 9; i++)
        {
            float x = -1.0f + (float)rnd.NextDouble() * 3.0f;
            float z = -2.3f + (float)rnd.NextDouble() * 0.5f;
            float s = 0.12f + (float)rnd.NextDouble() * 0.15f;
            NoCol(Cube("Rubble", y, new Vector3(x, s / 2f, z), new Vector3(s, s, s), concreteDark, (float)rnd.NextDouble() * 90f));
        }

        Pallet("PalletYard1", y, new Vector3(-5.8f, 0f, -3.4f), 20f);
        Pallet("PalletYard2", y, new Vector3(-6.8f, 0f, -4.3f), -35f);

        // Проход слева от склада
        float edge = -OW;
        Transform a = Group("Alley", y, Vector3.zero);
        Pallet("AlleyPallet", a, new Vector3(edge - 2.3f, 0f, InnerD * 0.8f), 10f);
        Cyl("Tire", a, new Vector3(edge - 0.8f, 0.13f, InnerD - 0.8f), new Vector3(0.7f, 0.13f, 0.7f), Vector3.zero, black);
        Cyl("Tire", a, new Vector3(edge - 0.8f, 0.39f, InnerD - 0.8f), new Vector3(0.7f, 0.13f, 0.7f), Vector3.zero, black);
        CubeR("LeaningBoard", a, new Vector3(edge - 0.45f, 1.0f, InnerD * 0.55f), new Vector3(0.05f, 2.0f, 1.0f), boardBrown, new Vector3(0, 0, -12));
        Chair(a, new Vector3(edge - 1.1f, 0f, InnerD * 0.45f), 110f);
        for (int i = 0; i < 6; i++)
        {
            float x = edge - 3.3f + (float)rnd.NextDouble() * 2.5f;
            float z = 2.0f + (float)rnd.NextDouble() * (InnerD - 3f);
            NoCol(Cube("Plank", a, new Vector3(x, 0.03f, z), new Vector3(0.6f, 0.04f, 0.09f), palletWood, (float)rnd.NextDouble() * 180f));
        }

    }

    // =====================================================================
    // ИНТЕРЬЕР: декор. Это группа, которую сохраняет пункт «сохранив мою расстановку».
    static void BuildInterior(Transform root)
    {
        Transform p = Group("Interior", root, Vector3.zero);
        float D = InnerD;

        // Паллета с коробками в красном скотче (задний правый угол)
        Transform pa = Pallet("PalletBoxes", p, new Vector3(HW - 0.8f, FloorY, D - 1.0f), 0f);
        TapedBox(pa, "Stack1", new Vector3(0, 0.40f, 0), new Vector3(1.1f, 0.5f, 0.75f), card);
        TapedBox(pa, "Stack2", new Vector3(0, 0.875f, 0), new Vector3(0.95f, 0.45f, 0.7f), cardDark);
        TapedBox(pa, "Stack3", new Vector3(0, 1.275f, 0), new Vector3(0.8f, 0.35f, 0.6f), card);

        // Паллета «WB FBS» в белой плёнке
        Transform pb = Pallet("PalletFBS", p, new Vector3(-(HW - 0.7f), FloorY, 7.0f), 90f);
        Cube("Wrap", pb, new Vector3(0, 0.60f, 0), new Vector3(1.15f, 0.9f, 0.78f), film);
        Cube("OpenBox1", pb, new Vector3(-0.28f, 1.15f, 0), new Vector3(0.45f, 0.35f, 0.5f), card, 8f);
        Cube("OpenBox2", pb, new Vector3(0.25f, 1.17f, 0.05f), new Vector3(0.5f, 0.38f, 0.5f), cardDark, -6f);

        // Паллета с рулонами фольги
        Transform pc = Pallet("PalletRolls", p, new Vector3(HW - 0.8f, FloorY, 5.8f), 90f);
        for (int j = 0; j < 4; j++)
            for (int i = 0; i < 4; i++)
            {
                Material m = ((i + j) % 3 == 0) ? foilDark : foil;
                Cyl("Roll", pc, new Vector3(-0.42f + i * 0.28f, 0.29f + j * 0.28f, 0),
                    new Vector3(0.28f, 0.4f, 0.28f), new Vector3(90, 0, 0), m);
            }

        // Куча рулонов в заднем левом углу
        System.Random rnd = new System.Random(7);
        for (int i = 0; i < 9; i++)
        {
            float x = -(HW - 0.3f) + (float)rnd.NextDouble() * 1.2f;
            float z = D - 2.5f + (float)rnd.NextDouble() * 1.6f;
            float y = FloorY + 0.14f + ((i % 3 == 2) ? 0.2f : 0f);
            Cyl("LooseRoll", p, new Vector3(x, y, z), new Vector3(0.28f, 0.5f, 0.28f),
                new Vector3(0, (float)rnd.NextDouble() * 180f, 90), (i % 2 == 0) ? foilDark : foil);
        }

        // Россыпь коробок: x, z, размеры, поворот
        float[,] boxes = {
            { -0.5f, D - 0.7f, 0.60f, 0.45f, 0.50f, 10f },
            {  0.2f, D - 0.7f, 0.50f, 0.40f, 0.45f, -15f },
            {  0.9f, D - 0.8f, 0.55f, 0.50f, 0.45f, 5f },
            {  HW - 0.6f, 8.6f, 0.50f, 0.40f, 0.40f, 25f },
            {  HW - 0.7f, 9.3f, 0.60f, 0.50f, 0.45f, -8f },
            { -(HW - 0.6f), 8.8f, 0.55f, 0.45f, 0.50f, 12f },
        };
        for (int i = 0; i < boxes.GetLength(0); i++)
            Cube("Box", p, new Vector3(boxes[i, 0], FloorY + boxes[i, 3] / 2f, boxes[i, 1]),
                new Vector3(boxes[i, 2], boxes[i, 3], boxes[i, 4]), (i % 2 == 0) ? card : cardDark, boxes[i, 5]);

        // Тёмные свёртки у входа слева
        CubeR("BlackBundle", p, new Vector3(-(HW - 0.55f), FloorY + 0.30f, 4.8f), new Vector3(0.9f, 0.6f, 0.75f), bag, new Vector3(0, 15, 0));
        CubeR("BlackBundle", p, new Vector3(-(HW - 1.1f), FloorY + 0.22f, 5.4f), new Vector3(0.7f, 0.45f, 0.6f), bag, new Vector3(0, -20, 0));
        CubeR("BlackBundle", p, new Vector3(-(HW - 0.6f), FloorY + 0.78f, 4.8f), new Vector3(0.6f, 0.35f, 0.5f), bag, new Vector3(0, 40, 0));

        // Доски у левой стены в глубине
        CubeR("LeaningBoard", p, new Vector3(-HW + 0.17f, FloorY + 0.85f, D - 0.6f), new Vector3(0.04f, 1.7f, 1.0f), boardBrown, new Vector3(0, 0, 10));
        CubeR("LeaningSheet", p, new Vector3(-HW + 0.15f, FloorY + 0.7f, D - 1.7f), new Vector3(0.03f, 1.4f, 1.2f), foilDark, new Vector3(0, 0, 10));

        // Складной стульчик слева у входа
        Cube("StoolSeat", p, new Vector3(-(HW - 0.6f), FloorY + 0.4f, 3.2f), new Vector3(0.3f, 0.03f, 0.3f), black);
        for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++)
                NoCol(Cube("StoolLeg", p, new Vector3(-(HW - 0.6f) + (a == 0 ? -0.12f : 0.12f), FloorY + 0.2f, 3.2f + (b == 0 ? -0.12f : 0.12f)),
                    new Vector3(0.02f, 0.4f, 0.02f), foil));

        Cyl("VentPipe", p, new Vector3(-HW + 0.15f, CeilY - 0.12f, D - 0.15f), new Vector3(0.14f, 0.12f, 0.14f), Vector3.zero, concreteDark);
        NoCol(Cube("CCTV", p, new Vector3(HW - 0.12f, CeilY - 0.1f, D - 0.12f), new Vector3(0.12f, 0.08f, 0.16f), black));
    }

    // =====================================================================
    // ГЕЙМПЛЕЙ-ОБЪЕКТЫ. Эта группа всегда собирается заново.
    static void BuildGameplay(Transform root)
    {
        Transform g = Group("Gameplay", root, Vector3.zero);
        float wx = HW;          // правая стена
        float deskZ = 3.2f;     // стол напротив, ближе к углу у входа

        // ---- Рабочий стол и доска для стикеров у правой стены ----
        Cube("DeskTop", g, new Vector3(wx - 0.45f, FloorY + 0.75f, deskZ), new Vector3(0.7f, 0.05f, 1.4f), wood);
        for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++)
                Cube("DeskLeg", g, new Vector3(wx - 0.45f + (a == 0 ? -0.3f : 0.3f), FloorY + 0.36f, deskZ + (b == 0 ? -0.6f : 0.6f)),
                    new Vector3(0.05f, 0.72f, 0.05f), wood);
        Cube("StickerBoard", g, new Vector3(wx - 0.025f, FloorY + 1.6f, deskZ), new Vector3(0.05f, 1.0f, 1.4f), wood);
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 3; c++)
                NoCol(Cube("Clip", g, new Vector3(wx - 0.055f, FloorY + 1.35f + r * 0.5f, deskZ - 0.5f + c * 0.5f),
                    new Vector3(0.03f, 0.05f, 0.06f), black));

        // Рулетка на столе
        Transform tapeT = Group("TapeMeasure", g, new Vector3(wx - 0.5f, FloorY + 0.775f, deskZ - 0.35f));
        NoCol(Cyl("Visual", tapeT, new Vector3(0, 0.02f, 0), new Vector3(0.12f, 0.02f, 0.12f), Vector3.zero, jackYellow));
        BoxCollider tc = tapeT.gameObject.AddComponent<BoxCollider>();
        tc.center = new Vector3(0, 0.04f, 0);
        tc.size = new Vector3(0.25f, 0.12f, 0.25f);
        Interactable it = tapeT.gameObject.AddComponent<Interactable>();
        it.prompt = "E — взять рулетку";
        TryAdd(tapeT.gameObject, "TapeMeasure");

        // Зона перед доской: сюда клеятся стикеры для FBS
        Transform zone = Group("StickerBoardZone", g, new Vector3(wx - 0.08f, FloorY + 1.6f, deskZ));
        BoxCollider zc = zone.gameObject.AddComponent<BoxCollider>();
        zc.size = new Vector3(0.12f, 1.0f, 1.4f);
        Interactable zi = zone.gameObject.AddComponent<Interactable>();
        zi.prompt = "Доска для стикеров";
        Component board = TryAdd(zone.gameObject, "StickerBoard");
        SetRef(board, "stickerMaterial", film);
        SetFloat(board, "facing", -1f);

        // ---- Пролог: швабра и мусор у входа ----
        Transform mop = Group("Mop", g, new Vector3(-HW + 0.35f, FloorY, 0.9f));
        CubeR("Handle", mop, new Vector3(0, 0.72f, 0), new Vector3(0.03f, 1.4f, 0.03f), wood, new Vector3(0, 0, 8));
        Cube("Head", mop, new Vector3(0.1f, 0.04f, 0), new Vector3(0.25f, 0.05f, 0.12f), jackBlue);
        BoxCollider mc = mop.gameObject.AddComponent<BoxCollider>();
        mc.center = new Vector3(0.05f, 0.75f, 0);
        mc.size = new Vector3(0.5f, 1.5f, 0.5f);
        Interactable mi = mop.gameObject.AddComponent<Interactable>();
        mi.prompt = "E — взять швабру";
        TryAdd(mop.gameObject, "MopItem");

        System.Random rnd = new System.Random(11);
        Material[] tm = { film, card, tape, concreteDark };
        for (int i = 0; i < 8; i++)
        {
            float x = -1.6f + (float)rnd.NextDouble() * 3.2f;
            float z = 0.9f + (float)rnd.NextDouble() * 0.9f;
            float s = 0.07f + (float)rnd.NextDouble() * 0.1f;
            Vector3 scale = new Vector3(s * 1.4f, s, s);
            Vector3 e = new Vector3((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f);
            GameObject t = CubeR("Trash", g, new Vector3(x, FloorY + s / 2f, z), scale, tm[i % tm.Length], e);
            // Увеличенный коллайдер (0.4 м), чтобы в мусор было легко попасть взглядом
            BoxCollider bc = t.GetComponent<BoxCollider>();
            bc.size = new Vector3(0.4f / scale.x, 0.4f / scale.y, 0.4f / scale.z);
            Interactable ti = t.AddComponent<Interactable>();
            ti.prompt = "Мусор. Нужна швабра";
            TryAdd(t, "TrashPiece");
        }

        // Спрятанный ключ на стуле в проходе слева от склада (нужен для самого левого склада)
        Transform key = Group("HiddenKey", g, new Vector3(-OW - 1.1f, 0.49f, InnerD * 0.45f));
        NoCol(Cube("Shaft", key, Vector3.zero, new Vector3(0.12f, 0.015f, 0.025f), foil));
        NoCol(Cube("Ring", key, new Vector3(-0.07f, 0, 0), new Vector3(0.045f, 0.015f, 0.045f), foil));
        BoxCollider kc = key.gameObject.AddComponent<BoxCollider>();
        kc.size = new Vector3(0.3f, 0.2f, 0.3f);
        Interactable ki = key.gameObject.AddComponent<Interactable>();
        ki.prompt = "E — взять ключ";
        TryAdd(key.gameObject, "KeyItem");

        // Одинокая коробка в правом углу у входа
        Cube("LoneBox", g, new Vector3(HW - 0.55f, FloorY + 0.25f, 1.4f), new Vector3(0.5f, 0.5f, 0.5f), card, 20f);

        // ---- Паллеты: место сборки ----
        Transform spot = Group("PalletSpot", g, new Vector3(0.4f, FloorY, 8.0f));
        NoCol(Cube("SpotMark", spot, new Vector3(0, 0.006f, 0), new Vector3(1.5f, 0.01f, 1.1f), jackYellow));
        Component ps = TryAdd(spot.gameObject, "PalletSpot");
        SetRef(ps, "palletMaterial", palletWood);
        SetRef(ps, "boxMaterial", card);
        SetRef(ps, "filmMaterial", film);

        // Рабочая рохля рядом с местом сборки
        Transform jack = PalletJack(g, new Vector3(-1.6f, FloorY, 8.3f), 90f, jackYellow);
        jack.name = "WorkJack";
        BoxCollider jc = jack.gameObject.AddComponent<BoxCollider>();
        jc.center = new Vector3(0, 0.65f, 0.45f);
        jc.size = new Vector3(0.9f, 1.3f, 1.6f);
        Interactable ji = jack.gameObject.AddComponent<Interactable>();
        ji.prompt = "E — взять рохлю";
        TryAdd(jack.gameObject, "WorkJack");

        // Зона отгрузки на улице (газель будет забирать паллеты отсюда)
        Transform ship = Group("ShipZone", g, new Vector3(-3.5f, 0f, -6.5f));
        NoCol(Cube("EdgeN", ship, new Vector3(0, 0.01f, 1.3f), new Vector3(2.7f, 0.02f, 0.1f), jackYellow));
        NoCol(Cube("EdgeS", ship, new Vector3(0, 0.01f, -1.3f), new Vector3(2.7f, 0.02f, 0.1f), jackYellow));
        NoCol(Cube("EdgeE", ship, new Vector3(1.3f, 0.01f, 0), new Vector3(0.1f, 0.02f, 2.7f), jackYellow));
        NoCol(Cube("EdgeW", ship, new Vector3(-1.3f, 0.01f, 0), new Vector3(0.1f, 0.02f, 2.7f), jackYellow));
        BoxCollider sc = ship.gameObject.AddComponent<BoxCollider>();
        sc.center = new Vector3(0, 0.05f, 0);     // тонкая плита на земле, чтобы паллеты внутри можно было выбрать взглядом
        sc.size = new Vector3(2.6f, 0.1f, 2.6f);
        Interactable si = ship.gameObject.AddComponent<Interactable>();
        si.prompt = "Зона отгрузки";
        TryAdd(ship.gameObject, "ShipZone");

        // ---- Точка, где появляются коробки селлеров ----
        Transform sp = Group("BoxSpawn", g, new Vector3(0.2f, FloorY, 4.6f));
        Component spawner = TryAdd(sp.gameObject, "BoxSpawner");
        SetRef(spawner, "boxMaterial", card);
        SetRef(spawner, "labelMaterial", film);
    }

    // =====================================================================
    // СВЕТ ВНУТРИ (группу InteriorLights можно выключать целиком в акте 2)
    static void BuildLights(Transform root)
    {
        Transform lights = Group("InteriorLights", root, Vector3.zero);
        for (float z = 2.5f; z < InnerD - 1f; z += 3.5f)
            Lamp(lights, new Vector3(0, CeilY - 0.25f, z));
    }

    static void Lamp(Transform parent, Vector3 pos)
    {
        GameObject go = new GameObject("Lamp");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 8f;
        l.intensity = 2.5f;
        l.color = new Color(1f, 0.95f, 0.85f);
        l.shadows = LightShadows.Soft;
        NoCol(Cube("Fixture", go.transform, new Vector3(0, 0.12f, 0), new Vector3(0.9f, 0.05f, 0.18f), lampMat));
    }

    // =====================================================================
    // ДВЕРИ-ТРИГГЕРЫ
    static GameObject MakeDoorTrigger(Transform root, string name, Vector3 pos, Vector3 size,
        string prompt, GameObject player, Transform target)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.AddComponent<BoxCollider>().size = size;

        Interactable it = go.AddComponent<Interactable>();
        it.prompt = prompt;
        if (it.onInteract == null) it.onInteract = new UnityEvent();

        Teleporter tp = go.AddComponent<Teleporter>();
        if (player != null) tp.player = player.transform;
        tp.target = target;

        EditorUtility.SetDirty(it);
        EditorUtility.SetDirty(tp);
        return go;
    }

    // Запасной вариант: прямое подключение телепорта к кнопке E (без замка и смены дверей)
    static void WireTeleport(GameObject trigger)
    {
        Interactable it = trigger.GetComponent<Interactable>();
        Teleporter tp = trigger.GetComponent<Teleporter>();
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        UnityEventTools.AddVoidPersistentListener(it.onInteract, tp.Go);
        EditorUtility.SetDirty(it);
    }
}