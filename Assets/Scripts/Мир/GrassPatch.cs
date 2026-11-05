using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Полоса между двором и полосатым центральным складом (там, где был асфальт Road), как на фото приёмки:
// щебёночная дорога, бетонный бордюр, газон с высоким сухим бурьяном, зелёная 3D-сетка забора,
// ржавый морской контейнер, бетонное колодезное кольцо, молодые деревца с жёлтыми листьями,
// выброшенные коробки в траве, белые биг-бэги за забором, серый профнастил, столбы с проводами.
// По краям карты: бетонный забор из плит, за ним ёлки и берёзы (чтобы мир не обрывался в пустоту).
// Игрок туда не заходит (граница двора) — это вид со двора.
//
// Расставлять самому: Smena → Окрестности → Собрать в сцене. Всё появится в сцене объектом «Okrestnosti»,
// его детей можно двигать, копировать (Ctrl+D) и удалять, сцена сохраняется как обычно.
// Готовые куски для перетаскивания — Assets/Prefabs/Okrestnosti. Трава и деревья — зоны ScatterArea.
// Пока «Okrestnosti» в сцене нет, полоса собирается сама при запуске игры (запасной вариант).
public class GrassPatch : MonoBehaviour
{
    const float RoadNear = -16.6f;    // щебёнка заходит во двор на пару метров
    const float CurbZ = -21.2f;       // бордюр между дорогой и газоном
    const float FenceZ = -25.4f;      // сетка-забор
    const float FaceZ = -29.65f;      // фасад центрального склада (-30)
    const float MinX = -49.5f, MaxX = 49.5f;
    const float GateHalf = 4.2f;      // проезд к воротам центрального склада

    public const string RootName = "Okrestnosti";
    Transform root;
    bool editMode;
    float panelX = 999f;              // у щитка ничего не ставим, туда бежит Володя
    System.Random rnd = new System.Random(2611);

    Material gravel, curb, lawn, fence, fencePost, container, containerDark, ring, carton, bag,
             profnastil, concretePanel, pole, wire, chunk;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("GrassPatch").AddComponent<GrassPatch>();
    }

    IEnumerator Start()
    {
        GameObject road = GameObject.Find("Road");
        if (road != null) { Renderer rr = road.GetComponent<Renderer>(); if (rr != null) rr.enabled = false; }
        if (GameObject.Find(RootName) != null) yield break;     // расставлено в сцене руками — ничего не строим

        // Щиток вешается на фасад в своём Start — ждём пару кадров, чтобы знать, где он
        yield return null; yield return null; yield return null;
        if (ElectricPanel.Instance != null && ElectricPanel.Instance.StandPoint != Vector3.zero)
            panelX = ElectricPanel.Instance.transform.position.x;
        root = transform;
        BuildEverything();
    }

    // Для редактора (Smena → Окрестности → Собрать в сцене): строит всё под новым объектом «Okrestnosti»
    public static GameObject BuildForEditor()
    {
        GameObject go = new GameObject(RootName);
        GrassPatch gp = go.AddComponent<GrassPatch>();
        gp.root = go.transform;
        gp.editMode = true;
        gp.BuildEverything();
        DestroyImmediate(gp);
        return go;
    }

    void BuildEverything()
    {
        MakeMaterials();
        BuildRoad();
        BuildCurb();
        BuildLawn();
        BuildFence();
        BuildContainer(new Vector3(-19f, 0f, -27.75f));
        BuildRings();
        BuildBigBags();
        BuildProfnastil(19.5f, 25f);
        BuildCartons();
        BuildPoles();
        BuildConcreteFence();
        // Трава и деревья — не кодом, а моделями через зоны ScatterArea
        Debug.Log("[Okrestnosti] полоса у центрального склада собрана");
    }

    // В редакторе щитка ещё нет: он висит в 2.5 м за крайней лампой, сторона заранее неизвестна — оставляем место с обеих
    bool NearPanel(float x, float r)
    {
        if (editMode) return Mathf.Abs(Mathf.Abs(x) - (TrashBinAndLamps.LampX[TrashBinAndLamps.LampX.Length - 1] + 2.5f)) < r;
        return Mathf.Abs(x - panelX) < r;
    }

    void Kill(Object o) { if (editMode) DestroyImmediate(o); else Destroy(o); }
    float R(float a, float b) { return Mathf.Lerp(a, b, (float)rnd.NextDouble()); }

    // =====================================================================
    // ДОРОГА, БОРДЮР, ГАЗОН

    void BuildRoad()
    {
        // Ближний край неровный, чтобы не было «линейки» на стыке с грунтом двора
        List<Vector3> v = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> tri = new List<int>();
        int n = 100;
        for (int i = 0; i <= n; i++)
        {
            float x = Mathf.Lerp(MinX, MaxX, i / (float)n);
            float zn = RoadNear + R(-0.7f, 0.7f);
            v.Add(new Vector3(x, 0.008f, CurbZ)); v.Add(new Vector3(x, 0.008f, zn));
            uv.Add(new Vector2(x, CurbZ)); uv.Add(new Vector2(x, zn));
            if (i > 0) { int a = (i - 1) * 2; tri.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 }); }
        }
        // Проезд к воротам центрального склада
        int b = v.Count;
        v.Add(new Vector3(-GateHalf, 0.008f, FaceZ)); v.Add(new Vector3(-GateHalf, 0.008f, CurbZ));
        v.Add(new Vector3(GateHalf, 0.008f, FaceZ)); v.Add(new Vector3(GateHalf, 0.008f, CurbZ));
        for (int i = b; i < v.Count; i++) uv.Add(new Vector2(v[i].x, v[i].z));
        tri.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
        MeshObject("GravelRoad", v, uv, tri, gravel, null);
    }

    void BuildCurb()
    {
        Transform t = Group("Curb");
        for (float x = MinX + 0.5f; x < MaxX; x += 1.0f)
        {
            if (Mathf.Abs(x) < GateHalf + 0.3f) continue;
            if (rnd.NextDouble() < 0.08) continue;                    // выбитый кусок
            float tilt = rnd.NextDouble() < 0.12 ? R(-14f, 14f) : R(-2f, 2f);
            Box("CurbStone", t, new Vector3(x + R(-0.03f, 0.03f), 0.06f, CurbZ + R(-0.04f, 0.04f)),
                new Vector3(0.97f, 0.22f, 0.16f), curb, new Vector3(tilt, R(-2f, 2f), 0));
        }
        // Обломки бетона у бордюра и у проезда
        for (int i = 0; i < 18; i++)
        {
            float x = R(MinX, MaxX); float s = R(0.08f, 0.3f);
            Box("Chunk", t, new Vector3(x, s * 0.3f, R(CurbZ - 0.6f, RoadNear - 0.8f)), new Vector3(s, s * 0.6f, s * 0.8f), chunk,
                new Vector3(R(0, 30f), R(0, 360f), R(0, 30f)));
        }
    }

    void BuildLawn()
    {
        List<Vector3> v = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> tri = new List<int>();
        AddLawnRect(v, uv, tri, MinX, -GateHalf, FaceZ, CurbZ - 0.1f);
        AddLawnRect(v, uv, tri, GateHalf, MaxX, FaceZ, CurbZ - 0.1f);
        MeshObject("Lawn", v, uv, tri, lawn, null);
    }

    static void AddLawnRect(List<Vector3> v, List<Vector2> uv, List<int> tri, float x0, float x1, float z0, float z1)
    {
        int b = v.Count;
        v.Add(new Vector3(x0, 0.012f, z0)); v.Add(new Vector3(x0, 0.012f, z1));
        v.Add(new Vector3(x1, 0.012f, z0)); v.Add(new Vector3(x1, 0.012f, z1));
        for (int i = b; i < v.Count; i++) uv.Add(new Vector2(v[i].x / 2f, v[i].z / 2f));
        tri.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
    }

    // =====================================================================
    // ЗАБОРЫ

    void BuildFence()
    {
        Transform t = Group("MeshFence");
        const float step = 2.5f;
        Mesh panel = FencePanelMesh(step, 2.03f);
        for (float x = -45f; x < 45f - 0.01f; x += step)
        {
            float x1 = x + step;
            if (x1 > -GateHalf - 0.6f && x < GateHalf + 0.6f) continue;        // открытый проезд
            if (x1 > 19.5f && x < 25f) continue;                                 // тут профнастил
            if (NearPanel((x + x1) / 2f, 2.6f)) continue;                        // проход к щитку
            FencePanel(t, panel, new Vector3(x, 0, FenceZ), R(-1.5f, 1.5f));
        }
        // Столбы у проезда, как у распахнутых ворот
        Box("GatePost", t, new Vector3(-GateHalf - 0.6f, 1.3f, FenceZ), new Vector3(0.1f, 2.6f, 0.1f), fencePost, Vector3.zero);
        Box("GatePost", t, new Vector3(GateHalf + 0.6f, 1.3f, FenceZ), new Vector3(0.1f, 2.6f, 0.1f), fencePost, Vector3.zero);
    }

    // Одна секция сетки 2.5 м со столбом слева. Начало секции — в точке pos, тянется по +X.
    // Чтобы продлить забор, копируй секцию и сдвигай на 2.5 м.
    void FencePanel(Transform parent, Mesh panel, Vector3 pos, float lean)
    {
        Transform s = Group("FencePanel", parent);
        s.localPosition = pos;
        Box("FencePost", s, new Vector3(0, 1.165f, 0), new Vector3(0.06f, 2.33f, 0.06f), fencePost, Vector3.zero);
        GameObject g = new GameObject("Mesh");
        g.transform.SetParent(s, false);
        g.transform.localRotation = Quaternion.Euler(lean, 0, 0);
        g.AddComponent<MeshFilter>().sharedMesh = panel;
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = fence;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
    }

    static Mesh FencePanelMesh(float w, float h)
    {
        Mesh m = new Mesh { name = "FencePanel" };
        m.vertices = new[] { new Vector3(0, 0.06f, 0), new Vector3(0, 0.06f + h, 0), new Vector3(w, 0.06f, 0), new Vector3(w, 0.06f + h, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(w / 0.4f, 0), new Vector2(w / 0.4f, 1) };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };      // лицом во двор
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // Серый профнастил (как рядом с контейнером на фото)
    void BuildProfnastil(float x0, float x1)
    {
        Transform t = Group("Profnastil");
        for (float x = x0; x < x1 - 0.01f; x += 1.15f)
        {
            float w = Mathf.Min(1.15f, x1 - x);
            if (NearPanel(x + w / 2f, 2.6f)) continue;
            Box("Sheet", t, new Vector3(x + w / 2f, 1.1f, FenceZ + R(-0.02f, 0.02f)), new Vector3(w, 2.2f, 0.025f), profnastil, new Vector3(0, R(-1f, 1f), 0));
        }
        Box("Rail", t, new Vector3((x0 + x1) / 2f, 0.4f, FenceZ + 0.05f), new Vector3(x1 - x0, 0.05f, 0.04f), fencePost, Vector3.zero);
        Box("Rail", t, new Vector3((x0 + x1) / 2f, 1.8f, FenceZ + 0.05f), new Vector3(x1 - x0, 0.05f, 0.04f), fencePost, Vector3.zero);
    }

    // Бетонный забор из плит по краям карты: дальние концы полосы, запад и восток.
    // За ним кончается земля — плиты это прячут.
    void BuildConcreteFence()
    {
        Transform t = Group("ConcreteFence");
        ConcreteRun(t, new Vector3(-49.6f, 0, -29.6f), new Vector3(-35.2f, 0, -29.6f));
        ConcreteRun(t, new Vector3(35.2f, 0, -29.6f), new Vector3(49.6f, 0, -29.6f));
        ConcreteRun(t, new Vector3(-49.6f, 0, -29.6f), new Vector3(-49.6f, 0, 24f));
        ConcreteRun(t, new Vector3(49.6f, 0, -29.6f), new Vector3(49.6f, 0, 39.5f));
    }

    void ConcreteRun(Transform t, Vector3 a, Vector3 b)
    {
        Vector3 dir = (b - a); float len = dir.magnitude; dir /= len;
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 90f;
        int n = Mathf.CeilToInt(len / 4f);
        for (int i = 0; i < n; i++)
        {
            Vector3 c = a + dir * (i * 4f + 2f);
            float hh = R(2.35f, 2.5f);
            Box("FenceSlab", t, new Vector3(c.x, hh / 2f, c.z), new Vector3(3.96f, hh, 0.16f), concretePanel,
                new Vector3(R(-1.2f, 1.2f), yaw + R(-0.6f, 0.6f), 0));
            Box("SlabPost", t, new Vector3(a.x + dir.x * i * 4f, 1.3f, a.z + dir.z * i * 4f), new Vector3(0.25f, 2.6f, 0.25f), concretePanel, new Vector3(0, yaw, 0));
        }
    }

    // =====================================================================
    // ПРЕДМЕТЫ

    // Ржавый 40-футовый контейнер, стоит за сеткой у стены склада
    void BuildContainer(Vector3 c)
    {
        Transform t = Group("ShipContainer");
        t.position = c;
        t.rotation = Quaternion.Euler(0, 0.8f, 0);
        const float L = 12.19f, H = 2.59f, W = 2.44f;
        Box("Body", t, new Vector3(0, H / 2f, 0), new Vector3(L - 0.1f, H - 0.12f, W - 0.06f), container, Vector3.zero);
        // Гофра на длинных стенках: вертикальные рёбра
        for (float x = -L / 2f + 0.25f; x < L / 2f - 0.2f; x += 0.28f)
            for (int s = -1; s <= 1; s += 2)
                Box("Rib", t, new Vector3(x, H / 2f, s * (W / 2f - 0.01f)), new Vector3(0.1f, H - 0.3f, 0.05f), container, Vector3.zero);
        // Рама: угловые стойки, верхние и нижние балки
        for (int i = -1; i <= 1; i += 2)
            for (int j = -1; j <= 1; j += 2)
                Box("Corner", t, new Vector3(i * (L / 2f - 0.08f), H / 2f, j * (W / 2f - 0.08f)), new Vector3(0.17f, H, 0.17f), containerDark, Vector3.zero);
        for (int j = -1; j <= 1; j += 2)
        {
            Box("RailTop", t, new Vector3(0, H - 0.06f, j * (W / 2f - 0.02f)), new Vector3(L, 0.12f, 0.08f), containerDark, Vector3.zero);
            Box("RailBottom", t, new Vector3(0, 0.08f, j * (W / 2f - 0.02f)), new Vector3(L, 0.16f, 0.1f), containerDark, Vector3.zero);
        }
        // Двери с торца: две створки, 4 запорные штанги
        float ex = L / 2f + 0.01f;
        Box("DoorSeam", t, new Vector3(ex, H / 2f, 0), new Vector3(0.03f, H - 0.3f, 0.03f), containerDark, Vector3.zero);
        for (int k = 0; k < 4; k++)
        {
            float z = -W / 2f + 0.3f + k * (W - 0.6f) / 3f;
            Cyl("LockBar", t, new Vector3(ex + 0.05f, H / 2f, z), new Vector3(0.04f, H / 2f - 0.12f, 0.04f), containerDark);
        }
    }

    // Бетонные колодезные кольца в траве
    void BuildRings()
    {
        Mesh tube = Tube(0.75f, 0.63f, 0.9f, 28);
        PlaceMesh("WellRing", tube, ring, new Vector3(-9.6f, 0f, -24.2f), new Vector3(0, 20f, 0));
        PlaceMesh("WellRing", tube, ring, new Vector3(-33f, -0.25f, -23.4f), new Vector3(8f, 0, 4f));
        PlaceMesh("WellRing", tube, ring, new Vector3(31.5f, 0f, -26.8f), new Vector3(0, 0, 0));
    }

    // Белые мешки-биг-бэги за сеткой у стены склада
    void BuildBigBags()
    {
        Transform t = Group("BigBags");
        for (int row = 0; row < 2; row++)
            for (float x = 6.2f; x < 18.5f; x += R(1.0f, 1.25f))
            {
                if (rnd.NextDouble() < 0.15) continue;
                float z = -28.95f + row * 1.05f, s = R(0.88f, 0.98f), hh = s * R(0.85f, 1.0f);
                Transform b = Group("BigBag", t);
                b.position = new Vector3(x, 0, z); b.rotation = Quaternion.Euler(0, R(-8f, 8f), 0);
                Box("Sack", b, new Vector3(0, hh / 2f, 0), new Vector3(s, hh, s), bag, Vector3.zero);
                Box("SackTop", b, new Vector3(0, hh + 0.04f, 0), new Vector3(s * 0.8f, 0.08f, s * 0.8f), bag, Vector3.zero);
                for (int i = -1; i <= 1; i += 2)
                    for (int j = -1; j <= 1; j += 2)
                        Box("Loop", b, new Vector3(i * s * 0.42f, hh + 0.1f, j * s * 0.42f), new Vector3(0.05f, 0.2f, 0.05f), bag, new Vector3(0, 45, 0));
            }
    }

    // Выброшенные коробки: раскрытые в бурьяне, сплющенные на дороге
    void BuildCartons()
    {
        Transform t = Group("Cartons");
        Vector3[] open = { new Vector3(-27f, 0, -23.2f), new Vector3(12.8f, 0, -22.1f) };
        foreach (Vector3 p in open)
        {
            if (NearPanel(p.x, 1.5f)) continue;
            OpenCarton(t, p, R(0.35f, 0.55f), R(0f, 360f), rnd.NextDouble() < 0.4 ? R(15f, 70f) : 0f);
        }
        for (int i = 0; i < 2; i++)
        {
            float x = R(-34f, 42f);
            if (Mathf.Abs(x) < GateHalf) continue;
            Box("FlatCarton", t, new Vector3(x, 0.02f, R(CurbZ + 0.3f, -18.8f)), new Vector3(R(0.5f, 0.9f), 0.012f, R(0.4f, 0.7f)), carton,
                new Vector3(R(-3f, 3f), R(0, 360f), R(-3f, 3f)));
        }
    }

    void OpenCarton(Transform parent, Vector3 p, float s, float yaw, float tip)
    {
        Transform c = Group("OpenCarton", parent);
        c.position = p; c.rotation = Quaternion.Euler(tip, yaw, 0);
        float w = s, d = s * 0.75f, h = s * 0.7f, th = 0.008f;
        Box("Bottom", c, new Vector3(0, th, 0), new Vector3(w, th, d), carton, Vector3.zero);
        Box("Wall", c, new Vector3(-w / 2f, h / 2f, 0), new Vector3(th, h, d), carton, Vector3.zero);
        Box("Wall", c, new Vector3(w / 2f, h / 2f, 0), new Vector3(th, h, d), carton, Vector3.zero);
        Box("Wall", c, new Vector3(0, h / 2f, -d / 2f), new Vector3(w, h, th), carton, Vector3.zero);
        Box("Wall", c, new Vector3(0, h / 2f, d / 2f), new Vector3(w, h, th), carton, Vector3.zero);
        // Клапаны разошлись в стороны
        Box("Flap", c, new Vector3(0, h + 0.05f, -d / 2f - 0.12f), new Vector3(w, th, d * 0.45f), carton, new Vector3(-55f, 0, 0));
        Box("Flap", c, new Vector3(0, h + 0.05f, d / 2f + 0.12f), new Vector3(w, th, d * 0.45f), carton, new Vector3(60f, 0, 0));
        if (rnd.NextDouble() < 0.5) Box("Flap", c, new Vector3(w / 2f + 0.1f, h + 0.03f, 0), new Vector3(w * 0.4f, th, d), carton, new Vector3(0, 0, -50f));
    }

    // Бетонные столбы с проводами вдоль дороги
    void BuildPoles()
    {
        Transform t = Group("Poles");
        float[] xs = { -46f, -24f, -2f, 20f, 42f };
        List<Vector3> tops = new List<Vector3>();
        foreach (float x0 in xs)
        {
            float x = Mathf.Abs(x0) < GateHalf + 1f ? x0 - (GateHalf + 1.2f) : x0;
            Vector3 b = new Vector3(x, 0, CurbZ - 0.7f);
            Transform pg = Group("Pole", t);
            pg.localPosition = b;
            pg.localRotation = Quaternion.Euler(R(-1.5f, 1.5f), 0, R(-1f, 1f));
            Box("Shaft", pg, new Vector3(0, 4.5f, 0), new Vector3(0.24f, 9f, 0.24f), pole, Vector3.zero);
            Box("CrossArm", pg, new Vector3(0, 8.6f, 0), new Vector3(0.1f, 0.1f, 1.6f), containerDark, Vector3.zero);
            tops.Add(b + new Vector3(0, 8.65f, 0));
        }
        for (int i = 0; i + 1 < tops.Count; i++)
            for (int k = -1; k <= 1; k += 2)
                Wire(t, tops[i] + new Vector3(0, 0, k * 0.7f), tops[i + 1] + new Vector3(0, 0, k * 0.7f), 0.9f);
    }

    void Wire(Transform t, Vector3 a, Vector3 b, float sag)
    {
        const int seg = 10;
        Vector3 prev = a;
        for (int i = 1; i <= seg; i++)
        {
            float k = i / (float)seg;
            Vector3 p = Vector3.Lerp(a, b, k) + Vector3.down * sag * 4f * k * (1f - k);
            Vector3 mid = (prev + p) / 2f, dir = p - prev;
            GameObject g = Box("Wire", t, mid, new Vector3(0.025f, 0.025f, dir.magnitude), wire, Vector3.zero);
            g.transform.rotation = Quaternion.LookRotation(dir);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            prev = p;
        }
    }

    // =====================================================================
    // МАТЕРИАЛЫ

    void MakeMaterials()
    {
        gravel = Lit("Gravel033", new Color(0.80f, 0.74f, 0.66f), 2.5f, 0.08f);
        curb = Lit("Concrete047A", new Color(0.78f, 0.77f, 0.74f), 1.5f, 0.1f);
        chunk = Lit("Concrete012", new Color(0.7f, 0.69f, 0.66f), 1f, 0.1f);
        lawn = Lit("Grass004", new Color(0.86f, 0.84f, 0.62f), 2f, 0.05f);
        ring = Lit("Concrete012", new Color(0.82f, 0.8f, 0.76f), 1.2f, 0.1f);
        container = Lit("CorrugatedSteel007B", new Color(0.62f, 0.26f, 0.15f), 1.5f, 0.25f, 0.3f);
        containerDark = Lit("Rust009", new Color(0.45f, 0.22f, 0.14f), 1f, 0.2f, 0.3f);
        carton = Lit("Cardboard002", new Color(0.86f, 0.72f, 0.52f), 0.8f, 0.05f);
        bag = Lit(null, new Color(0.9f, 0.9f, 0.88f), 1f, 0.25f);
        profnastil = Lit("CorrugatedSteel005_factory", new Color(0.66f, 0.7f, 0.68f), 1.2f, 0.3f, 0.4f);
        concretePanel = Lit("Concrete012", new Color(0.66f, 0.66f, 0.64f), 2f, 0.08f);
        pole = Lit("Concrete047A", new Color(0.72f, 0.72f, 0.7f), 1.5f, 0.1f);
        fencePost = Lit(null, new Color(0.13f, 0.27f, 0.18f), 1f, 0.35f, 0.4f);
        wire = Lit(null, new Color(0.08f, 0.08f, 0.08f), 1f, 0.2f);

        fence = Cutout(FenceTexture(), 0.45f, new Color(1, 1, 1));
        string[] names = { "Gravel", "Curb", "ConcreteChunk", "Lawn", "WellRing", "Container", "ContainerFrame", "Carton", "BigBag",
                           "Profnastil", "ConcreteSlab", "Pole", "FencePost", "Wire", "FenceMesh" };
        Material[] mats = { gravel, curb, chunk, lawn, ring, container, containerDark, carton, bag, profnastil, concretePanel, pole, fencePost, wire, fence };
        for (int i = 0; i < mats.Length; i++) mats[i].name = "Okr_" + names[i];
        fence.SetFloat("_Smoothness", 0.35f);
        fence.SetTexture("_BaseMap", fence.mainTexture);
    }

    static Material Lit(string tex, Color tint, float metersPerRepeat, float smooth, float metal = 0f)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (tex != null)
        {
            Texture2D c = Resources.Load<Texture2D>("HD/" + tex + "_color");
            Texture2D n = Resources.Load<Texture2D>("HD/" + tex + "_normal");
            if (c != null) { m.SetTexture("_BaseMap", c); m.mainTexture = c; }
            if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            float k = 1f / metersPerRepeat;
            m.SetTextureScale("_BaseMap", new Vector2(k, k));
        }
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        return m;
    }

    static Material Cutout(Texture2D t, float cutoff, Color tint)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", t); m.mainTexture = t;
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", cutoff); m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_Smoothness", 0.05f);
        m.renderQueue = 2450;
        return m;
    }

    // Сетка 3D-забора: вертикальные прутья через 5 см, горизонтальные через 20 см, три «гиба» по высоте.
    // Текстура — кусок 40 см шириной и на всю высоту панели.
    static Texture2D FenceTexture()
    {
        const int W = 128, H = 512;
        Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, true);
        t.name = "Okr_FenceMesh";
        Color[] px = new Color[W * H];
        Color green = new Color(0.14f, 0.29f, 0.19f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float mx = x / (float)W * 0.4f, my = y / (float)H * 2.03f;
                float dv = Mathf.Abs(mx - Mathf.Round(mx / 0.05f) * 0.05f);
                float dh = Mathf.Abs(my - Mathf.Round(my / 0.2f) * 0.2f);
                bool bend = Mathf.Abs(my - 0.5f) < 0.035f || Mathf.Abs(my - 1.0f) < 0.035f || Mathf.Abs(my - 1.5f) < 0.035f;
                bool on = dv < 0.0062f || dh < 0.006f || (bend && (Mathf.Abs(my - Mathf.Round(my * 2f) / 2f) > 0.022f)) || my > 2.0f || my < 0.012f;
                float shade = 0.85f + 0.3f * Mathf.PerlinNoise(x * 0.3f, y * 0.05f);
                px[y * W + x] = on ? new Color(green.r * shade, green.g * shade, green.b * shade, 1f) : new Color(green.r, green.g, green.b, 0f);
            }
        t.SetPixels(px); t.Apply(true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        t.mipMapBias = -0.8f;
        return t;
    }

    // =====================================================================
    // ПОСТРОЕНИЕ

    Transform Group(string name, Transform parent = null)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent != null ? parent : root, false);
        return t;
    }

    // Куб без коллайдера (игрок туда не ходит, а лучи «взгляда» не должны цепляться)
    GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, Vector3 euler)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Kill(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    GameObject Cyl(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        Kill(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    void PlaceMesh(string name, Mesh mesh, Material m, Vector3 pos, Vector3 euler)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(root, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(euler);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
    }

    GameObject MeshObject(string name, List<Vector3> v, List<Vector2> uv, List<int> tri, Material m, List<Vector3> normals, bool unused = false)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
        if (normals != null) mesh.SetNormals(normals); else mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GameObject g = new GameObject(name);
        g.transform.SetParent(root, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    // Труба (бетонное кольцо): внешняя и внутренняя стенки и верхний торец
    static Mesh Tube(float ro, float ri, float h, int seg)
    {
        List<Vector3> v = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            float u = a * ro;
            v.Add(d * ro); v.Add(d * ro + Vector3.up * h); v.Add(d * ri); v.Add(d * ri + Vector3.up * h);
            uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, h)); uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, h));
            if (i == 0) continue;
            int p = (i - 1) * 4, c = i * 4;
            tri.AddRange(new[] { p, p + 1, c, c, p + 1, c + 1 });                 // снаружи
            tri.AddRange(new[] { p + 2, c + 2, p + 3, p + 3, c + 2, c + 3 });     // внутри
            tri.AddRange(new[] { p + 1, p + 3, c + 1, c + 1, p + 3, c + 3 });     // верх
        }
        Mesh m = new Mesh { name = "Okr_WellRing" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
}
