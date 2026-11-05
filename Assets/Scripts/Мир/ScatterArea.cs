using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

// Зона рассыпки: трава, бурьян, кусты, деревья — моделями, а не кодом.
// Как пользоваться: Smena → Окрестности → Добавить зону травы (или пустой объект + этот компонент).
//   - зона — это прямоугольник: двигай, крути и растягивай объект (Scale X и Z = ширина и длина в метрах);
//   - в поле «Models» перетащи модели или префабы из Project (можно несколько — будут вперемешку);
//   - «Per Square Meter» — густота, «Size Range» — разброс размера, «Clumping» — растёт пятнами;
//   - «Seed» — другой случайный узор.
// Видно сразу в редакторе. Рисуется через GPU-инстансинг (тысячи пучков без тысяч объектов в сцене).
// На «Низкой» графике в игре не рисуется (кроме зон с галочкой Always Visible — для деревьев).
[ExecuteAlways]
public class ScatterArea : MonoBehaviour
{
    [Tooltip("Модели, которые рассыпать. Перетащи сюда модели или префабы из окна Project.")]
    public GameObject[] models;
    [Tooltip("Сколько штук на квадратный метр (траве 0.3–2, деревьям 0.01–0.05)")]
    public float perSquareMeter = 0.6f;
    [Tooltip("Случайный масштаб: от и до")]
    public Vector2 sizeRange = new Vector2(0.8f, 1.25f);
    [Tooltip("0 — ровно по всей зоне, 1 — пятнами с проплешинами")]
    [Range(0f, 1f)] public float clumping = 0.4f;
    [Tooltip("Случайный наклон, градусов")]
    public float tilt = 4f;
    public int seed = 1;
    [Tooltip("Ставить на землю (луч вниз до коллайдера)")]
    public bool snapToGround = true;
    [Tooltip("Тени от моделей (деревьям — да, траве лучше нет)")]
    public bool castShadows = false;
    [Tooltip("Показывать и на «Низкой» графике")]
    public bool alwaysVisible = false;
    [Tooltip("Дальше этого расстояния (м) от камеры зона не рисуется")]
    public float drawDistance = 90f;
    public int maxCount = 20000;
    [Tooltip("Ставить настоящими объектами, а не инстансингом. Для деревьев: работают упрощённые копии вдали (Mesh LOD).")]
    public bool asObjects = false;

    class Part
    {
        public Mesh mesh; public int sub; public Material mat; public Matrix4x4 local;
        public readonly List<Matrix4x4> list = new List<Matrix4x4>();
        public Matrix4x4[] arr;
    }

    readonly List<Part> parts = new List<Part>();
    readonly List<GameObject> spawned = new List<GameObject>();
    bool shownLow = true;
    Bounds bounds;
    bool dirty = true;
    static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();

    public int Count { get; private set; }

    void OnEnable()
    {
        dirty = true;
        RenderPipelineManager.beginCameraRendering += Draw;
        if (asObjects) Rebuild();
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Draw;
        ClearSpawned();
    }

    void OnValidate() { dirty = true; }

    void Update()
    {
        if (transform.hasChanged) { transform.hasChanged = false; dirty = true; }
        if (asObjects && dirty) Rebuild();
        if (asObjects && Application.isPlaying)
        {
            bool show = alwaysVisible || VisualOptions.TextureMode >= 1;
            if (show != shownLow) { shownLow = show; foreach (GameObject g in spawned) if (g != null) g.SetActive(show); }
        }
    }

    void ClearSpawned()
    {
        foreach (GameObject g in spawned)
            if (g != null) { if (Application.isPlaying) Destroy(g); else DestroyImmediate(g); }
        spawned.Clear();
    }

    public void Rebuild()
    {
        dirty = false;
        parts.Clear();
        ClearSpawned();
        Count = 0;
        if (models == null || models.Length == 0) return;

        // Части каждой модели (меш + материал + положение внутри модели); у LODGroup берём LOD0
        List<List<Part>> per = new List<List<Part>>();
        foreach (GameObject model in models)
        {
            List<Part> lp = new List<Part>();
            per.Add(lp);
            if (model == null) continue;
            IEnumerable<Renderer> rs = model.GetComponentsInChildren<MeshRenderer>(true);
            LODGroup lod = model.GetComponentInChildren<LODGroup>(true);
            if (lod != null && lod.GetLODs().Length > 0) rs = lod.GetLODs()[0].renderers;
            Matrix4x4 toModel = model.transform.worldToLocalMatrix;
            foreach (Renderer r in rs)
            {
                if (r == null) continue;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Material[] mats = r.sharedMaterials;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                {
                    Material m = mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null;
                    if (m == null) continue;
                    Part p = new Part { mesh = mf.sharedMesh, sub = s, mat = Convert(m), local = toModel * r.transform.localToWorldMatrix };
                    lp.Add(p); parts.Add(p);
                }
            }
        }

        Vector3 sc = transform.lossyScale;
        float area = Mathf.Abs(sc.x * sc.z);
        int n = Mathf.Min(maxCount, Mathf.RoundToInt(area * perSquareMeter));
        System.Random r0 = new System.Random(seed);
        bounds = new Bounds(transform.position, Vector3.zero);
        float lo = Mathf.Min(sizeRange.x, sizeRange.y), hi = Mathf.Max(sizeRange.x, sizeRange.y);

        for (int i = 0; i < n * 3 && Count < n; i++)
        {
            float u = (float)r0.NextDouble() - 0.5f, v = (float)r0.NextDouble() - 0.5f;
            Vector3 p = transform.TransformPoint(new Vector3(u, 0f, v));
            if (clumping > 0f)
            {
                float noise = Mathf.PerlinNoise(p.x * 0.22f + seed * 13.1f, p.z * 0.22f + seed * 7.7f);
                float keep = Mathf.Lerp(1f, Mathf.Clamp01(noise * 2.2f - 0.6f), clumping);
                if (r0.NextDouble() > keep) continue;
            }
            if (snapToGround)
            {
                RaycastHit hit;
                if (Physics.Raycast(p + Vector3.up * 15f, Vector3.down, out hit, 40f, ~(1 << 2), QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            }
            int k = r0.Next(models.Length);
            float s = Mathf.Lerp(lo, hi, (float)r0.NextDouble());
            Quaternion q = Quaternion.Euler(((float)r0.NextDouble() - 0.5f) * 2f * tilt, (float)r0.NextDouble() * 360f, ((float)r0.NextDouble() - 0.5f) * 2f * tilt);
            if (asObjects)
            {
                if (models[k] == null) continue;
                GameObject g = Instantiate(models[k], p, q);
                g.transform.localScale = models[k].transform.localScale * s;
                g.hideFlags = HideFlags.HideAndDontSave;       // не сохраняется в сцене и не мешается в Hierarchy — двигай саму зону
                spawned.Add(g);
                Count++;
                continue;
            }
            Matrix4x4 inst = Matrix4x4.TRS(p, q, Vector3.one * s);
            foreach (Part part in per[k]) part.list.Add(inst * part.local);
            bounds.Encapsulate(p);
            Count++;
        }
        foreach (Part part in parts) { part.arr = part.list.ToArray(); part.list.Clear(); }
        bounds.Expand(new Vector3(4f, 20f, 4f));
    }

    void Draw(ScriptableRenderContext ctx, Camera cam)
    {
        if (this == null || !isActiveAndEnabled) return;
        if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
        if (Application.isPlaying && !alwaysVisible && VisualOptions.TextureMode < 1) return;
        if (dirty) Rebuild();
        if (Count == 0 || asObjects) return;
        if (bounds.SqrDistance(cam.transform.position) > drawDistance * drawDistance) return;

        foreach (Part part in parts)
        {
            if (part.arr == null || part.arr.Length == 0 || part.mat == null) continue;
            RenderParams rp = new RenderParams(part.mat)
            {
                camera = cam,
                worldBounds = bounds,
                shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer
            };
            for (int start = 0; start < part.arr.Length; start += 1023)
                Graphics.RenderMeshInstanced(rp, part.mesh, part.sub, part.arr, Mathf.Min(1023, part.arr.Length - start), start);
        }
    }

    // Материал модели → копия с инстансингом. Если шейдер не URP (модель пришла со Standard),
    // делаем URP Lit с той же картинкой; прозрачность картинки → вырез (листья, травинки), видно с двух сторон.
    static Material Convert(Material src)
    {
        Material m;
        if (converted.TryGetValue(src, out m) && m != null) return m;
        string sh = src.shader != null ? src.shader.name : "";
        bool urp = sh.StartsWith("Universal Render Pipeline") || sh.StartsWith("Shader Graphs");
        if (urp) m = new Material(src);
        else
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Texture tex = src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
            if (tex == null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            Color c = src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
            m.SetColor("_BaseColor", c);
            Texture n = src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null;
            if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Smoothness", 0.1f);
            Texture2D t2 = tex as Texture2D;
            if (t2 != null && GraphicsFormatUtility.HasAlphaChannel(t2.graphicsFormat))
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f);
                m.renderQueue = 2450;
            }
        }
        m.name = src.name + " (scatter)";
        m.enableInstancing = true;
        converted[src] = m;
        return m;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 0.3f, 0.6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0.02f, 1f));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 0.3f, 0.15f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, new Vector3(1f, 0.02f, 1f));
    }
}
