using System.Collections.Generic;
using UnityEngine;

// Объекты, которые собирает и построитель склада (в редакторе), и GameplayFixer (в игре, если склад не пересобран):
// мусорный бак под эстакадой и 4 лампы на фасаде полосатого центрального склада.
public static class Extras
{
    // Лампы на фасаде CentralWarehouse: x по фасаду, высота
    public static readonly float[] LampX = { -26f, -10f, 10f, 26f };
    public const float LampY = 6.5f;
    public const float FaceZ = -30f;

    // Мусорный контейнер (как во дворах): корпус шире кверху, две крышки (одна приоткрыта), рёбра, карманы, колёса
    public static Transform TrashBin(Transform parent, Vector3 localPos, float rotY, Material body)
    {
        Transform b = new GameObject("TrashBin").transform;
        b.SetParent(parent, false);
        b.localPosition = localPos;
        b.localRotation = Quaternion.Euler(0, rotY, 0);

        // Корпус — трапеция: низ 1.6 x 0.9, верх 1.8 x 1.1
        GameObject shell = new GameObject("Shell");
        shell.transform.SetParent(b, false);
        shell.AddComponent<MeshFilter>().sharedMesh = Trapezoid(new Vector2(1.6f, 0.9f), new Vector2(1.8f, 1.1f), 0.16f, 1.16f);
        shell.AddComponent<MeshRenderer>().sharedMaterial = body;

        // Верхний бортик
        NoCol(Part(PrimitiveType.Cube, "Rim", b, new Vector3(0, 1.17f, 0), new Vector3(1.86f, 0.05f, 1.16f), body));

        // Крышки на петлях у задней стенки; правая приоткрыта
        for (int i = 0; i < 2; i++)
        {
            Transform hinge = new GameObject("LidHinge").transform;
            hinge.SetParent(b, false);
            hinge.localPosition = new Vector3(i == 0 ? -0.46f : 0.46f, 1.2f, -0.58f);
            hinge.localRotation = Quaternion.Euler(i == 0 ? 0f : -14f, 0, 0);
            NoCol(Part(PrimitiveType.Cube, "Lid", hinge, new Vector3(0, 0.02f, 0.59f), new Vector3(0.9f, 0.04f, 1.18f), body));
            NoCol(Part(PrimitiveType.Cube, "LidHandle", hinge, new Vector3(0, 0.04f, 1.12f), new Vector3(0.3f, 0.04f, 0.06f), body));
        }

        // Вертикальные рёбра спереди и сзади
        for (int i = -1; i <= 1; i++)
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject rib = Part(PrimitiveType.Cube, "Rib", b, new Vector3(i * 0.55f, 0.66f, side * 0.5f), new Vector3(0.06f, 0.95f, 0.05f), body);
                rib.transform.localRotation = Quaternion.Euler(side * -5.7f, 0, 0);     // по наклону стенки
                NoCol(rib);
            }

        // Карманы для подъёма мусоровозом по бокам
        for (int side = -1; side <= 1; side += 2)
            NoCol(Part(PrimitiveType.Cube, "Pocket", b, new Vector3(side * 0.9f, 0.85f, 0), new Vector3(0.08f, 0.14f, 0.55f), body));

        // Колёса на вилках
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = new Vector3(i < 2 ? -0.68f : 0.68f, 0, i % 2 == 0 ? -0.34f : 0.34f);
            NoCol(Part(PrimitiveType.Cube, "Caster", b, p + new Vector3(0, 0.12f, 0), new Vector3(0.06f, 0.08f, 0.1f), body));
            GameObject w = Part(PrimitiveType.Cylinder, "Wheel", b, p + new Vector3(0, 0.07f, 0), new Vector3(0.13f, 0.025f, 0.13f), body);
            w.transform.localRotation = Quaternion.Euler(0, 0, 90);
            NoCol(w);
        }

        // Один коллайдер на весь контейнер: в него упирается игрок и попадает взгляд
        BoxCollider bc = b.gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.62f, 0);
        bc.size = new Vector3(1.86f, 1.24f, 1.16f);
        b.gameObject.AddComponent<Interactable>().prompt = "Мусорный контейнер";
        b.gameObject.AddComponent<TrashBin>();
        return b;
    }

    // Закрытая усечённая коробка (низ bottom, верх top, по X и Z), UV в метрах
    static Mesh Trapezoid(Vector2 bottom, Vector2 top, float y0, float y1)
    {
        float bx = bottom.x / 2f, bz = bottom.y / 2f, tx = top.x / 2f, tz = top.y / 2f;
        Vector3[] c =
        {
            new Vector3(-bx, y0, -bz), new Vector3(bx, y0, -bz), new Vector3(bx, y0, bz), new Vector3(-bx, y0, bz),
            new Vector3(-tx, y1, -tz), new Vector3(tx, y1, -tz), new Vector3(tx, y1, tz), new Vector3(-tx, y1, tz),
        };
        int[][] faces =
        {
            new[] { 0, 4, 5, 1 },   // перед (-Z)
            new[] { 2, 6, 7, 3 },   // зад (+Z)
            new[] { 3, 7, 4, 0 },   // лево (-X)
            new[] { 1, 5, 6, 2 },   // право (+X)
            new[] { 4, 7, 6, 5 },   // верх
            new[] { 0, 1, 2, 3 },   // низ
        };
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> tri = new List<int>();
        foreach (int[] f in faces)
        {
            Vector3 n = Vector3.Cross(c[f[1]] - c[f[0]], c[f[2]] - c[f[0]]).normalized;
            int start = v.Count;
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = c[f[k]];
                v.Add(p);
                if (Mathf.Abs(n.y) > 0.7f) uv.Add(new Vector2(p.x, p.z));
                else if (Mathf.Abs(n.x) > Mathf.Abs(n.z)) uv.Add(new Vector2(p.z, p.y));
                else uv.Add(new Vector2(p.x, p.y));
            }
            tri.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        Mesh m = new Mesh();
        m.name = "DumpsterShell";
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        m.RecalculateTangents();
        return m;
    }

    public static void FacadeLamps(Transform centralWarehouse, Material lampMat, Material armMat)
    {
        for (int i = 0; i < LampX.Length; i++)
        {
            Transform l = new GameObject("FacadeLamp").transform;
            l.SetParent(centralWarehouse, false);
            l.localPosition = new Vector3(LampX[i], LampY, FaceZ + 0.02f);
            l.localRotation = Quaternion.identity;      // смотрит во двор (+Z)

            NoCol(Part(PrimitiveType.Cube, "Arm", l, new Vector3(0, 0, 0.3f), new Vector3(0.06f, 0.06f, 0.6f), armMat));
            NoCol(Part(PrimitiveType.Cube, "Housing", l, new Vector3(0, -0.05f, 0.62f), new Vector3(0.5f, 0.1f, 0.28f), lampMat));

            GameObject lg = new GameObject("Light");
            lg.transform.SetParent(l, false);
            lg.transform.localPosition = new Vector3(0, -0.15f, 0.62f);
            lg.transform.localRotation = Quaternion.Euler(65f, 0, 0);
            Light light = lg.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 120f;
            light.range = 28f;
            light.intensity = 6f;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.shadows = LightShadows.None;
            light.enabled = false;      // включаются в 17:30 (CentralLamps)
        }
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 size, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static GameObject NoCol(GameObject go)
    {
        Collider c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        return go;
    }
}
