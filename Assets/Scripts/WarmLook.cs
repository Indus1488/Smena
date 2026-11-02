using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// «Тёплая» графика в духе Zort: включается пресетом Smena → Графика → «Тёплая (как Zort)» или «Тёплая + PS1».
// Что делает:
// - постобработка: мягкое свечение светлых мест (Bloom), киношная цветокоррекция (ACES), тёплый баланс,
//   чуть больше контраста и меньше насыщенности, затемнение краёв, лёгкое зерно;
// - сглаживание краёв (FXAA) и гладкая фильтрация текстур вместо «пиксельной»;
// - лампы в главном складе становятся тёплыми, как свет лампы накаливания, с мягкими тенями.
// Числа подбираются в методе BuildProfile ниже.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class WarmLook : MonoBehaviour
{
    Volume volume;
    bool active;
    float nextCheck;

    Light[] lamps;
    Color[] lampColor;
    float[] lampRange;

    readonly Dictionary<Texture, FilterMode> filters = new Dictionary<Texture, FilterMode>();

    // ---------- HD-текстуры (Assets/Resources/HD, обрабатываются из скачанных архивов) ----------
    // материал -> какие HD-текстуры подходят (берётся первая найденная)
    static readonly Dictionary<string, string[]> hdMap = new Dictionary<string, string[]>
    {
        { "Concrete",      new[] { "Concrete047A", "concrete_block_wall_02", "Concrete012" } },
        { "Plinth",        new[] { "Concrete047A", "concrete_block_wall_02", "Concrete012" } },
        { "ConcreteDark",  new[] { "Concrete012", "Concrete047A" } },
        { "Asphalt",       new[] { "Asphalt026B", "concrete_floor" } },
        { "OSB",           new[] { "Chipboard002", "oriented_strand_board" } },
        { "Wood",          new[] { "Planks011", "plank_flooring_03" } },
        { "PalletWood",    new[] { "Planks011", "plank_flooring_03" } },
        { "BoardBrown",    new[] { "Planks011", "plank_flooring_03" } },
        { "DoorOpen",      new[] { "Metal053B" } },
        { "GrayDoor",      new[] { "Metal053B" } },
        { "Rust",          new[] { "Rust009", "rusty_metal_04" } },
        { "GantryRust",    new[] { "Rust009", "rusty_metal_04" } },
        { "Gate",          new[] { "MetalPlates012", "rusty_metal_04" } },
        { "DoorClosed",    new[] { "MetalPlates012", "rusty_metal_04" } },
        { "RedDoor",       new[] { "MetalPlates012", "rusty_metal_04" } },
        { "Cardboard",     new[] { "Cardboard002", "Paper005" } },
        { "CardboardDark", new[] { "Cardboard002", "Paper005" } },
        { "Dirt",          new[] { "Gravel033" } },
        { "Tile",          new[] { "Tiles038" } },
        { "StripeCream",   new[] { "Plaster_cream" } },
        { "StripeDark",    new[] { "Plaster_dark" } },
        { "FactoryBlue",   new[] { "CorrugatedSteel005_factory" } },
        { "Turquoise",     new[] { "CorrugatedSteel007B" } },
        // серые подложки: цвет берётся из самого материала
        { "Foil",          new[] { "Metal009_neutral" } },
        { "FoilDark",      new[] { "Metal009_neutral" } },
        { "Silver",        new[] { "Metal009_neutral" } },
        { "JackBlue",      new[] { "PaintedMetal012_neutral" } },
        { "JackYellow",    new[] { "PaintedMetal012_neutral" } },
        { "ChimneyRed",    new[] { "Bricks016_neutral" } },
        { "ChimneyWhite",  new[] { "Bricks016_neutral" } },
        { "BinGreen",      new[] { "PaintedMetal012_neutral" } },
    };

    class Saved
    {
        public Texture main, bump;
        public bool normalKeyword;
    }
    readonly Dictionary<Material, Saved> savedMats = new Dictionary<Material, Saved>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("WarmLook").AddComponent<WarmLook>();
    }

    void Start()
    {
        GameObject w = GameObject.Find("Warehouse");
        Transform t = w != null ? w.transform.Find("InteriorLights") : null;
        lamps = t != null ? t.GetComponentsInChildren<Light>(true) : new Light[0];
        lampColor = new Color[lamps.Length];
        lampRange = new float[lamps.Length];
        for (int i = 0; i < lamps.Length; i++)
        {
            lampColor[i] = lamps[i].color;
            lampRange[i] = lamps[i].range;
        }
        Apply(PS1Look.Current.warm);
    }

    // Ночная подстройка «тёплой» графики: после 18:00 ACES и высокий контраст съедали всё, что освещено фонариком
    ColorAdjustments nightCA;
    Tonemapping nightTM;
    Vignette nightVG;
    float night;

    void NightAdjust()
    {
        if (!active || volume == null || volume.sharedProfile == null) return;
        if (nightCA == null)
        {
            volume.sharedProfile.TryGet(out nightCA);
            volume.sharedProfile.TryGet(out nightTM);
            volume.sharedProfile.TryGet(out nightVG);
        }
        float target = ActController.Act >= 2 ? 1f : 0f;
        night = Mathf.MoveTowards(night, target, Time.deltaTime / 3f);
        if (nightCA != null)
        {
            nightCA.postExposure.value = Mathf.Lerp(0.25f, 1.0f, night);
            nightCA.contrast.value = Mathf.Lerp(22f, -5f, night);
        }
        if (nightTM != null) nightTM.mode.value = night > 0.5f ? TonemappingMode.Neutral : TonemappingMode.ACES;
        if (nightVG != null) nightVG.intensity.value = Mathf.Lerp(0.32f, 0.2f, night);
    }

    void Update()
    {
        NightAdjust();
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.5f;
        bool want = PS1Look.Current.warm;
        if (want != active) Apply(want);
        UpdateHD();
    }

    void Apply(bool on)
    {
        active = on;

        if (on && volume == null)
        {
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = BuildProfile();
        }
        if (volume != null) volume.enabled = on;

        Camera cam = Camera.main;
        if (cam != null)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = on || Brightness.NeedsPost;
                data.antialiasing = on ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            }
        }

        // Тёплые лампы
        for (int i = 0; i < lamps.Length; i++)
        {
            if (lamps[i] == null) continue;
            lamps[i].color = on ? new Color(1f, 0.76f, 0.48f) : lampColor[i];
            lamps[i].range = on ? lampRange[i] * 1.25f : lampRange[i];
            lamps[i].shadows = LightShadows.Soft;
        }

        UpdateHD(true);
    }

    // HD-текстуры: по выбору в настройках (Авто — только в «тёплой» графике без PS1)

    // Текстуры по настройке графики: 0 — без текстур (однотонные цвета), 1 — ретро, 2 — HD
    int texMode = -1;

    void UpdateHD(bool force = false)
    {
        int mode = VisualOptions.TextureMode;
        if (!force && mode == texMode) return;
        texMode = mode;
        RestoreHD();                 // сначала всегда возвращаем исходные материалы, потом применяем выбранное
        if (mode == 2) SwapHD(true);
        else if (mode == 0) SwapNone();
        bool smooth = mode == 2;
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Material m = all[i].sharedMaterial;
            if (m == null) continue;
            Texture tex = m.mainTexture;
            if (tex == null) continue;
            if (!filters.ContainsKey(tex)) filters[tex] = tex.filterMode;
            tex.filterMode = smooth ? FilterMode.Bilinear : filters[tex];
        }
    }

    static VolumeProfile BuildProfile()
    {
        VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();

        Tonemapping tm = p.Add<Tonemapping>(true);
        tm.mode.Override(TonemappingMode.ACES);

        Bloom bloom = p.Add<Bloom>(true);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.75f);

        ColorAdjustments ca = p.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.25f);
        ca.contrast.Override(22f);
        ca.saturation.Override(-15f);

        WhiteBalance wb = p.Add<WhiteBalance>(true);
        wb.temperature.Override(14f);

        Vignette vig = p.Add<Vignette>(true);
        vig.intensity.Override(0.32f);
        vig.smoothness.Override(0.45f);

        FilmGrain grain = p.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.22f);
        grain.response.Override(0.8f);

        return p;
    }

    // Подставляет HD-текстуры в материалы склада (или возвращает обычные)
    void SwapHD(bool hd)
    {
        if (!hd)
        {
            RestoreHD();
            return;
        }
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Material m = all[i].sharedMaterial;
            if (m == null || savedMats.ContainsKey(m)) continue;
            string[] keys;
            if (!hdMap.TryGetValue(m.name, out keys)) continue;

            Texture2D color = null, normal = null;
            for (int k = 0; k < keys.Length && color == null; k++)
            {
                color = Resources.Load<Texture2D>("HD/" + keys[k] + "_color");
                if (color != null) normal = Resources.Load<Texture2D>("HD/" + keys[k] + "_normal");
            }
            if (color == null) continue;

            Saved s = new Saved();
            s.main = m.mainTexture;
            s.bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            s.normalKeyword = m.IsKeywordEnabled("_NORMALMAP");
            savedMats[m] = s;

            m.mainTexture = color;
            if (normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
        }
    }

    // «Низкая» графика: убираем текстуры, остаётся цвет материала
    void SwapNone()
    {
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Material m = all[i].sharedMaterial;
            if (m == null || savedMats.ContainsKey(m)) continue;
            if (!hdMap.ContainsKey(m.name) && m.name != "Monster") continue;

            Saved sv = new Saved();
            sv.main = m.mainTexture;
            sv.bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            sv.normalKeyword = m.IsKeywordEnabled("_NORMALMAP");
            savedMats[m] = sv;

            m.mainTexture = null;
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
        }
    }

    void RestoreHD()
    {
        foreach (KeyValuePair<Material, Saved> kv in savedMats)
        {
            Material m = kv.Key;
            if (m == null) continue;
            m.mainTexture = kv.Value.main;
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", kv.Value.bump);
            if (kv.Value.normalKeyword) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
        }
        savedMats.Clear();
    }

    // В редакторе материалы — это файлы проекта: после игры обязательно возвращаем всё как было
    void OnApplicationQuit()
    {
        RestoreHD();
    }

    void OnDestroy()
    {
        RestoreHD();
        // Возвращаем фильтрацию текстур как была (в редакторе это настройки самих файлов)
        foreach (KeyValuePair<Texture, FilterMode> kv in filters)
            if (kv.Key != null) kv.Key.filterMode = kv.Value;
    }
}
