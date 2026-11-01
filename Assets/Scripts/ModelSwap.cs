using System.Collections.Generic;
using UnityEngine;

// Подставляет настоящие 3D-модели вместо кубиков.
// Модели лежат в Assets/Resources/Models/<ключ>/ (например Models/pallet/, Models/box/).
// Если в папке несколько моделей, у разных объектов будут разные (удобно для коробок).
// Кубики не удаляются: у них просто выключается отображение, а коллайдеры и скрипты остаются как были.
// Модель сама подгоняется по размеру под то место, которое занимали кубики.
// Если для ключа моделей нет, объект остаётся кубиками. Поэтому модели можно добавлять по одной.
// Новые объекты (коробки селлеров, собранные паллеты, Газель) подхватываются автоматически раз в секунду.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class ModelSwap : MonoBehaviour
{
    struct Entry
    {
        public string key;        // папка в Resources/Models
        public string[] targets;  // имена объектов, которые заменяем
        public string[] parts;    // какие дочерние кубики спрятать (null = сам объект и все его дети)
        public bool uniform;      // true = сохранить пропорции модели, false = растянуть точно по кубикам
        public float rotY;        // доворот модели (0, 90, 180, 270), если она стоит задом наперёд
        public Entry(string k, string[] t, string[] p, bool u, float r = 0f)
        {
            key = k; targets = t; parts = p; uniform = u; rotY = r;
        }
    }

    static readonly Entry[] entries =
    {
        new Entry("pallet",      new[] { "PalletYard1", "PalletYard2", "AlleyPallet", "PalletBoxes", "PalletFBS", "PalletRolls", "PalletCargo", "StockPallet" },
                                 new[] { "Deck", "Runner" }, false),
        new Entry("pallet_jack", new[] { "PalletJack", "WorkJack" }, new[] { "ForkL", "ForkR", "Body", "Handle", "Grip" }, true),
        new Entry("box",         new[] { "Box", "LoneBox", "SellerBox", "Carton", "Stack1", "Stack2", "Stack3", "OpenBox1", "OpenBox2" }, null, false),
        new Entry("battery",     new[] { "Battery" }, new[] { "Body", "Label", "TerminalL", "TerminalR" }, true),
        new Entry("key",         new[] { "HiddenKey" }, new[] { "Shaft", "Ring" }, true),
        new Entry("mop",         new[] { "Mop" }, new[] { "Handle", "Head" }, true),
        new Entry("chair",       new[] { "Chair" }, new[] { "Seat", "Back", "Leg" }, true),
        new Entry("desk",        new[] { "Gameplay" }, new[] { "DeskTop", "DeskLeg" }, false),
        new Entry("tire",        new[] { "Tire" }, null, true),
        new Entry("roll",        new[] { "Roll", "LooseRoll" }, null, true),
        new Entry("trash_bag",   new[] { "BlackBundle" }, null, true),
        new Entry("trash",       new[] { "Trash" }, null, true),
        new Entry("lamp",        new[] { "Lamp" }, new[] { "Fixture" }, true),
        new Entry("gazelle",     new[] { "Gazelle" }, new[] { "Chassis", "Cargo", "Cab", "Windshield", "Wheel" }, true),
        new Entry("bus_stop",    new[] { "BusStop" }, null, true, 180f),
        new Entry("trash_bin",   new[] { "TrashBin" }, new[] { "Body", "Lid", "Rim", "Wheel" }, true),
        new Entry("street_lamp", new[] { "FacadeLamp" }, new[] { "Arm", "Housing" }, true),
    };

    // В этих папках один файл содержит сразу несколько моделей (например, 4 коробки): каждая станет отдельным вариантом
    static readonly string[] SplitKeys = { "box", "trash" };

    // Эти модели подгоняем по высоте: кубики-заготовки у них слишком тонкие или неточные по ширине
    static readonly string[] HeightFitKeys = { "bus_stop", "mop", "chair", "trash_bin", "gazelle", "street_lamp", "pallet_jack" };

    static void AddParts(Transform root, List<GameObject> list)
    {
        // Спускаемся через пустые обёртки (RootNode и т.п.)
        while (root.childCount == 1 && root.GetComponent<Renderer>() == null) root = root.GetChild(0);
        int added = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c.GetComponentInChildren<Renderer>(true) == null) continue;
            list.Add(c.gameObject);
            added++;
        }
        if (added == 0) list.Add(root.gameObject);
    }

    readonly Dictionary<string, Entry> byName = new Dictionary<string, Entry>();
    readonly Dictionary<string, GameObject[]> models = new Dictionary<string, GameObject[]>();
    readonly HashSet<Transform> done = new HashSet<Transform>();

    // Всё, что уже заменено: модель и спрятанные кубики (чтобы в настройках можно было вернуть простые)
    class Swapped { public GameObject holder; public List<Renderer> hidden; }
    readonly List<Swapped> swapped = new List<Swapped>();
    bool showing = true;
    float next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("ModelSwap").AddComponent<ModelSwap>();
    }

    void Start()
    {
        int found = 0;
        foreach (Entry e in entries)
        {
            List<GameObject> list = new List<GameObject>();
            foreach (GameObject g in Resources.LoadAll<GameObject>("Models/" + e.key))
            {
                if (g.GetComponentInChildren<Renderer>(true) == null) continue;
                if (System.Array.IndexOf(SplitKeys, e.key) >= 0) AddParts(g.transform, list);   // в одном файле несколько вариантов
                else list.Add(g);
            }
            if (list.Count == 0) continue;
            models[e.key] = list.ToArray();
            found += list.Count;
            foreach (string t in e.targets) byName[t] = e;
        }
        if (found == 0)
        {
            enabled = false;   // моделей пока нет — ничего не делаем
            return;
        }
        Scan();
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.5f;

        bool want = VisualOptions.UseModels;
        if (want != showing)
        {
            showing = want;
            for (int i = swapped.Count - 1; i >= 0; i--)
            {
                Swapped s = swapped[i];
                if (s.holder == null) { swapped.RemoveAt(i); continue; }
                s.holder.SetActive(want);
                foreach (Renderer r in s.hidden) if (r != null) r.enabled = !want;
            }
        }
        if (showing) Scan();
    }

    void Scan()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            Entry e;
            if (!byName.TryGetValue(t.name, out e)) continue;
            if (done.Contains(t)) continue;
            done.Add(t);
            Swap(t, e);
        }
    }

    // ---------- Замена ----------
    void Swap(Transform target, Entry e)
    {
        // 1. Какие кубики прячем
        List<Renderer> hide = new List<Renderer>();
        if (e.parts == null)
        {
            hide.AddRange(target.GetComponentsInChildren<Renderer>());
        }
        else
        {
            for (int i = 0; i < target.childCount; i++)
            {
                Transform c = target.GetChild(i);
                if (System.Array.IndexOf(e.parts, c.name) < 0) continue;
                hide.AddRange(c.GetComponentsInChildren<Renderer>());
            }
        }
        if (hide.Count == 0) return;

        // 2. Опора без масштаба родителя, повёрнутая как объект: в ней считаем размеры в настоящих метрах
        Transform holder = new GameObject("Model").transform;
        holder.SetParent(target, false);
        Vector3 ls = target.lossyScale;
        holder.localScale = new Vector3(1f / Mathf.Max(Mathf.Abs(ls.x), 1e-4f), 1f / Mathf.Max(Mathf.Abs(ls.y), 1e-4f), 1f / Mathf.Max(Mathf.Abs(ls.z), 1e-4f));

        Bounds want;
        if (!Measure(hide, holder, out want)) { Destroy(holder.gameObject); return; }

        // 3. Модель (из нескольких вариантов выбираем по позиции, чтобы у одинаковых коробок был разный вид)
        GameObject[] list = models[e.key];
        Vector3 p = target.position;
        int idx = Mathf.Abs(Mathf.RoundToInt(p.x * 73f + p.z * 31f + p.y * 17f)) % list.Length;
        GameObject m = Instantiate(list[idx], holder, false);
        m.name = list[idx].name;
        m.transform.localPosition = Vector3.zero;
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        FixMaterials(m, e.key);

        List<Renderer> mr = new List<Renderer>(m.GetComponentsInChildren<Renderer>());
        Bounds have;
        float rot = e.rotY;
        m.transform.localRotation = Quaternion.Euler(0, rot, 0);
        if (!Measure(mr, holder, out have)) { Destroy(holder.gameObject); return; }

        // Модель лежит «вдоль», а место «поперёк» — доворачиваем на 90°
        if (Elongated(want) && Elongated(have) && (want.size.x > want.size.z) != (have.size.x > have.size.z))
        {
            rot += 90f;
            m.transform.localRotation = Quaternion.Euler(0, rot, 0);
            Measure(mr, holder, out have);
        }

        // 4. Масштаб
        Vector3 k = new Vector3(Ratio(want.size.x, have.size.x), Ratio(want.size.y, have.size.y), Ratio(want.size.z, have.size.z));
        if (e.uniform)
        {
            float s = System.Array.IndexOf(HeightFitKeys, e.key) >= 0 ? k.y : Mathf.Min(k.x, Mathf.Min(k.y, k.z));
            k = new Vector3(s, s, s);
        }
        else if (Mathf.RoundToInt(rot / 90f) % 2 != 0)
        {
            k = new Vector3(k.z, k.y, k.x);   // у повёрнутой модели оси X и Z поменялись местами
        }
        m.transform.localScale = Vector3.Scale(m.transform.localScale, k);

        // 5. Ставим на место: по центру сверху, низом на пол
        Measure(mr, holder, out have);
        Vector3 shift = new Vector3(want.center.x - have.center.x, want.min.y - have.min.y, want.center.z - have.center.z);
        m.transform.localPosition += shift;

        foreach (Renderer r in hide) r.enabled = false;
        swapped.Add(new Swapped { holder = holder.gameObject, hidden = hide });
    }

    // Если при импорте текстуры не подключились (модель белая или розовая), подключаем их здесь:
    // берём <ключ>_color и <ключ>_normal из той же папки Resources/Models/<ключ>/
    static readonly Dictionary<Material, Material> fixedMats = new Dictionary<Material, Material>();

    // Общая починка материалов моделей (её же использует VolodyaModel)
    public static void FixModelMaterials(GameObject model, string key)
    {
        FixMaterials(model, key);
    }

    static void FixMaterials(GameObject model, string key)
    {
        Texture2D col = Resources.Load<Texture2D>("Models/" + key + "/" + key + "_color");
        Texture2D nrm = Resources.Load<Texture2D>("Models/" + key + "/" + key + "_normal");
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) return;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            Material[] ms = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < ms.Length; i++)
            {
                Material src = ms[i];
                if (src == null) continue;

                Material fixedM;
                if (!fixedMats.TryGetValue(src, out fixedM))
                {
                    // Любой материал модели переводим на обычный URP Lit: шейдеры из FBX (Autodesk, 3ds Max Physical,
                    // Standard) в нашей графике бывают розовыми или светятся
                    Texture baseTex = FindTex(src, false);
                    Texture bumpTex = FindTex(src, true);
                    if (baseTex == null) baseTex = col;
                    if (bumpTex == null) bumpTex = nrm;
                    Color c = Color.white;
                    if (src.HasProperty("_BaseColor")) c = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color")) c = src.GetColor("_Color");
                    if (baseTex != null && c.maxColorComponent < 0.2f) c = Color.white;
                    if (c.maxColorComponent > 1f) c /= c.maxColorComponent;
                    c.a = 1f;

                    fixedM = new Material(urp);
                    fixedM.name = src.name + " (Smena)";
                    fixedM.SetColor("_BaseColor", c);
                    if (baseTex != null)
                    {
                        fixedM.SetTexture("_BaseMap", baseTex);
                        fixedM.mainTexture = baseTex;
                    }
                    if (bumpTex != null)
                    {
                        fixedM.SetTexture("_BumpMap", bumpTex);
                        fixedM.EnableKeyword("_NORMALMAP");
                    }
                    fixedM.SetFloat("_Smoothness", 0.2f);
                    fixedM.SetFloat("_Metallic", 0f);
                    fixedM.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    fixedMats[src] = fixedM;
                }
                ms[i] = fixedM;
                changed = true;
            }
            if (changed) r.sharedMaterials = ms;
        }
    }

    // Ищет у материала текстуру цвета (или нормалей) по названию свойства
    static Texture FindTex(Material m, bool normal)
    {
        string[] props = m.GetTexturePropertyNames();
        Texture best = null;
        for (int i = 0; i < props.Length; i++)
        {
            string n = props[i].ToLowerInvariant();
            bool isNormal = n.Contains("normal") || n.Contains("bump");
            if (isNormal != normal) continue;
            if (!normal && !(n.Contains("base") || n.Contains("albedo") || n.Contains("diffuse") || n.Contains("main") || n.Contains("color"))) continue;
            if (n.Contains("emis") || n.Contains("rough") || n.Contains("metal") || n.Contains("spec") || n.Contains("occl")) continue;
            Texture t = m.GetTexture(props[i]);
            if (t == null) continue;
            if (n == "_basemap" || n == "_maintex" || n == "_bumpmap") return t;
            if (best == null) best = t;
        }
        return best;
    }

    static float Ratio(float want, float have)
    {
        return have > 1e-4f ? want / have : 1f;
    }

    static bool Elongated(Bounds b)
    {
        float a = b.size.x, c = b.size.z;
        return Mathf.Max(a, c) > Mathf.Min(a, c) * 1.15f;
    }

    // Габариты набора мешей в координатах опоры
    static bool Measure(List<Renderer> rs, Transform space, out Bounds result)
    {
        result = new Bounds();
        bool any = false;
        Matrix4x4 toSpace = space.worldToLocalMatrix;
        foreach (Renderer r in rs)
        {
            if (r == null) continue;
            Bounds lb;
            if (r is SkinnedMeshRenderer)
            {
                lb = ((SkinnedMeshRenderer)r).localBounds;
            }
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                lb = mf.sharedMesh.bounds;
            }
            Matrix4x4 mtx = toSpace * r.transform.localToWorldMatrix;
            Vector3 mn = lb.min, mx = lb.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z);
                Vector3 w = mtx.MultiplyPoint3x4(c);
                if (!any) { result = new Bounds(w, Vector3.zero); any = true; }
                else result.Encapsulate(w);
            }
        }
        return any;
    }
}
