using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Дополнения к графике:
// ВЫСОКАЯ — объёмный свет от ламп (светящиеся конусы в воздухе), мягкие тени у ламп и тёмные углы
//           (рассеянный свет слабее, свет дают только лампы).
// VHS     — затенение в углах и щелях (AO) и смазывание при движении (motion blur).
// Обычная и Высокая тоже с лёгким смазыванием при движении.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class GraphicsExtras : MonoBehaviour
{
    class Cone { public Light light; public GameObject go; }

    readonly List<Cone> cones = new List<Cone>();
    Material coneMat;
    Volume vhsVolume;
    MotionBlur blur;
    ScriptableRendererFeature ssao;
    bool ssaoWasActive;
    float ambient0;
    Color amb0, sky0, eq0, gr0;
    float next;
    bool first = true;
    int lastSig;
    FilmGrain grain;
    ColorAdjustments vhsColor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("GraphicsExtras").AddComponent<GraphicsExtras>();
    }

    void Start()
    {
        ambient0 = RenderSettings.ambientIntensity;
        amb0 = RenderSettings.ambientLight; sky0 = RenderSettings.ambientSkyColor;
        eq0 = RenderSettings.ambientEquatorColor; gr0 = RenderSettings.ambientGroundColor;
        FindSsao();

        vhsVolume = gameObject.AddComponent<Volume>();
        vhsVolume.isGlobal = true;
        vhsVolume.priority = 30f;
        VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();
        blur = p.Add<MotionBlur>(true);
        blur.intensity.Override(0.25f);
        blur.quality.Override(MotionBlurQuality.Low);
        // Для VHS: зерно плёнки и блёклые, чуть зеленоватые цвета кассеты
        grain = p.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.45f);
        grain.response.Override(0.6f);
        vhsColor = p.Add<ColorAdjustments>(true);
        vhsColor.saturation.Override(-22f);
        vhsColor.contrast.Override(-8f);
        vhsColor.colorFilter.Override(new Color(0.97f, 1f, 0.95f));
        vhsVolume.sharedProfile = p;
        vhsVolume.enabled = false;
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.5f;

        bool warm = VisualOptions.Warm, vhs = VisualOptions.Vhs;
        int q = VisualOptions.Quality;
        int sig = VisualOptions.Signature;
        if (first || sig != lastSig)
        {
            first = false;
            lastSig = sig;
            // Тёмные углы: в высокой графике рассеянного света меньше (только до 18:00 — потом темноту задаёт сюжет)
            if (ActController.Act < 2) SetAmbient(warm && VisualOptions.DarkCorners ? 0.55f : 1f);
            // Размытие в движении: в VHS сильнее, в Обычной и Высокой — лёгкое, в Низкой нет
            bool blurOn = VisualOptions.MotionBlur && (vhs || q > 0);
            bool tape = vhs && VisualOptions.VhsTape;
            vhsVolume.enabled = blurOn || tape;
            blur.active = blurOn;
            blur.intensity.Override(vhs ? 0.4f : 0.22f);
            grain.active = tape;
            vhsColor.active = tape;
            // AO: в VHS и в высокой графике (тёмные углы); в остальных — как было в настройках проекта
            if (ssao != null) ssao.SetActive(VisualOptions.AO && (vhs || q > 0));
        }

        if (vhsVolume.enabled)
        {
            Camera cam = Camera.main;
            UniversalAdditionalCameraData data = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
            if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
        }

        UpdateCones(warm && VisualOptions.VolumeLight);
        NoLampShadows();
    }

    // Рассеянный свет: в сцене он плоский (Flat), там работает только цвет, поэтому множим цвета
    void SetAmbient(float k)
    {
        RenderSettings.ambientIntensity = ambient0 * k;
        RenderSettings.ambientLight = amb0 * k;
        RenderSettings.ambientSkyColor = sky0 * k;
        RenderSettings.ambientEquatorColor = eq0 * k;
        RenderSettings.ambientGroundColor = gr0 * k;
    }

    // Светильники не отбрасывают тень: свет висит прямо под ними, и крепления рисовали на потолке
    // тёмные прямоугольники. Проверяем раз в 2 с — ModelSwap подставляет модели ламп чуть позже.
    float nextShadowFix;
    void NoLampShadows()
    {
        if (Time.unscaledTime < nextShadowFix) return;
        nextShadowFix = Time.unscaledTime + 2f;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || l.name == "FlashlightBeam") continue;
            Transform fixture = l.transform.parent != null ? l.transform.parent : l.transform;
            foreach (Renderer r in fixture.GetComponentsInChildren<Renderer>())
                if (r.shadowCastingMode != ShadowCastingMode.Off && r.bounds.SqrDistance(l.transform.position) < 1.44f && r.bounds.size.magnitude < 3f)
                    r.shadowCastingMode = ShadowCastingMode.Off;   // только сам светильник, не всё здание
        }
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.name.Contains("Lamp") && !t.name.Contains("Fixture")) continue;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
                if (r.shadowCastingMode != ShadowCastingMode.Off && r.bounds.size.magnitude < 3f) r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // ---------- Объёмный свет ----------
    void UpdateCones(bool on)
    {
        if (on && cones.Count == 0) BuildCones();
        foreach (Cone c in cones)
        {
            if (c.go == null) continue;
            bool lit = on && c.light != null && c.light.enabled && c.light.gameObject.activeInHierarchy && c.light.intensity > 0.05f;
            if (c.go.activeSelf != lit) c.go.SetActive(lit);
        }
    }

    void BuildCones()
    {
        coneMat = MakeConeMaterial();
        if (coneMat == null) return;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) continue;
            if (l.gameObject.name == "FlashlightBeam") continue;     // у фонарика конус слепил бы игрока
            Transform t = l.transform;
            GameObject g = new GameObject("LightCone");
            g.transform.SetParent(t, false);
            Vector3 ls = t.lossyScale;   // чтобы масштаб родителя не растягивал конус
            g.transform.localScale = new Vector3(1f / Mathf.Max(Mathf.Abs(ls.x), 1e-4f), 1f / Mathf.Max(Mathf.Abs(ls.y), 1e-4f), 1f / Mathf.Max(Mathf.Abs(ls.z), 1e-4f));
            float len, top, bottom;
            if (l.type == LightType.Spot)
            {
                len = Mathf.Min(l.range * 0.6f, 9f);
                top = 0.12f;
                bottom = Mathf.Tan(l.spotAngle * 0.5f * Mathf.Deg2Rad) * len * 0.8f;
                g.transform.localRotation = Quaternion.identity;            // конус вдоль луча (+Z)
            }
            else
            {
                // точечная лампа под потолком: конус вниз до пола
                len = 2.4f; top = 0.28f; bottom = 1.05f;
                g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // +Z смотрит вниз
            }
            // Несколько вложенных полупрозрачных конусов: в центре плотнее, к краю мягко гаснет
            float[] k = { 1f, 0.72f, 0.48f };
            for (int i = 0; i < k.Length; i++)
            {
                GameObject layer = new GameObject("Layer" + i);
                layer.transform.SetParent(g.transform, false);
                layer.AddComponent<MeshFilter>().sharedMesh = ConeMesh(top * k[i], bottom * k[i], len * (0.85f + 0.15f * k[i]));
                MeshRenderer r = layer.AddComponent<MeshRenderer>();
                r.sharedMaterial = coneMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            cones.Add(new Cone { light = l, go = g });
        }
    }

    // Конус вдоль +Z; v = 0 у лампы, 1 на конце (по нему текстура гасит свечение)
    static Mesh ConeMesh(float r0, float r1, float len)
    {
        const int seg = 24;
        List<Vector3> v = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg, c = Mathf.Cos(a), s = Mathf.Sin(a);
            v.Add(new Vector3(c * r0, s * r0, 0f)); uv.Add(new Vector2(i / (float)seg, 0f));
            v.Add(new Vector3(c * r1, s * r1, len)); uv.Add(new Vector2(i / (float)seg, 1f));
        }
        for (int i = 0; i < seg; i++)
        {
            int k = i * 2;
            tri.AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3 });     // снаружи
            tri.AddRange(new[] { k, k + 2, k + 1, k + 2, k + 3, k + 1 });     // изнутри (видно, когда стоишь под лампой)
        }
        Mesh m = new Mesh { name = "LightCone" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    static Material MakeConeMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) return null;
        Material m = new Material(sh);
        // Прозрачный аддитивный: свет только добавляется
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;

        // Текстура: ярко у лампы, к концу гаснет
        Texture2D t = new Texture2D(4, 64, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        {
            float k = y / 63f;
            float a = Mathf.Pow(1f - k, 2.2f) * 0.045f;   // слабо: три слоя складываются
            for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(1f, 0.85f, 0.62f, a));
        }
        t.Apply();
        m.SetTexture("_BaseMap", t);
        m.mainTexture = t;
        m.SetColor("_BaseColor", new Color(1f, 0.9f, 0.75f, 1f));
        return m;
    }

    // ---------- AO из настроек рендера ----------
    void FindSsao()
    {
        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        FieldInfo f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        ScriptableRendererData[] list = f != null ? f.GetValue(urp) as ScriptableRendererData[] : null;
        if (list == null) return;
        foreach (ScriptableRendererData d in list)
        {
            if (d == null) continue;
            foreach (ScriptableRendererFeature rf in d.rendererFeatures)
                if (rf != null && rf.GetType().Name == "ScreenSpaceAmbientOcclusion") { ssao = rf; ssaoWasActive = rf.isActive; return; }
        }
    }

    // В редакторе настройки рендера — это файл проекта: после игры возвращаем AO как было
    void OnDestroy()
    {
        if (ssao != null) ssao.SetActive(ssaoWasActive);
        if (ActController.Act < 2) SetAmbient(1f);
    }
}
