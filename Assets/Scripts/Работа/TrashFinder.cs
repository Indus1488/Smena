using System.Collections.Generic;
using UnityEngine;

// Чтобы мусор было невозможно потерять (пока швабра в руках и в мешке есть место):
//  - каждый кусок мусора светится так же, как предметы, на которые смотришь (HoverHighlight), — на любом расстоянии;
//  - если ближайший кусок вне экрана или за спиной — у края экрана стрелка к нему и расстояние.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class TrashFinder : MonoBehaviour
{
    public static bool Glowing { get; private set; }

    const string GlowTag = " [trashglow]";
    readonly Dictionary<Renderer, Material[]> saved = new Dictionary<Renderer, Material[]>();
    readonly Dictionary<Material, Material> glowOf = new Dictionary<Material, Material>();
    readonly List<Renderer> drop = new List<Renderer>();
    readonly HashSet<Renderer> seen = new HashSet<Renderer>();
    Texture2D arrow;
    GUIStyle st;
    TrashPiece nearest;
    float nearestDist, nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("TrashFinder").AddComponent<TrashFinder>();
    }

    static bool Active { get { return Inventory.hasMop && Inventory.trashBag < GameConfig.trashBagSize && TrashPiece.All.Count > 0 && !GameMenus.IsBlocking; } }

    void Start()
    {
        arrow = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                bool inside = y >= 4 && Mathf.Abs(x - 15.5f) < (y - 4) * 0.55f;   // треугольник остриём вниз (поворачиваем при рисовании)
                arrow.SetPixel(x, 31 - y, inside ? Color.white : new Color(1, 1, 1, 0));
            }
        arrow.Apply();
    }

    void Update()
    {
        nearest = null;
        bool on = Active;
        Glowing = on;
        Camera cam = Camera.main;

        if (on && cam != null)
        {
            nearestDist = float.MaxValue;
            foreach (TrashPiece tp in TrashPiece.All)
            {
                if (tp == null || !tp.isActiveAndEnabled) continue;
                float d = (tp.transform.position - cam.transform.position).magnitude;
                if (d < nearestDist) { nearestDist = d; nearest = tp; }
            }
        }

        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.2f;
        Refresh(on);
    }

    // Включает свечение у всех кусков мусора на складе и снимает его с подобранных
    void Refresh(bool on)
    {
        seen.Clear();
        if (on)
        {
            foreach (TrashPiece tp in TrashPiece.All)
            {
                if (tp == null || !tp.isActiveAndEnabled) continue;
                Renderer[] rs = tp.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < rs.Length; i++)
                {
                    Renderer r = rs[i];
                    if (r == null || !r.enabled) continue;
                    seen.Add(r);
                    Material[] cur = r.sharedMaterials;
                    if (saved.ContainsKey(r) && IsGlow(cur)) continue;   // уже светится
                    if (IsGlow(cur)) continue;
                    saved[r] = cur;
                    Material[] copy = new Material[cur.Length];
                    for (int k = 0; k < cur.Length; k++) copy[k] = Glow(cur[k]);
                    r.sharedMaterials = copy;
                }
            }
        }

        drop.Clear();
        foreach (KeyValuePair<Renderer, Material[]> kv in saved)
            if (!seen.Contains(kv.Key)) drop.Add(kv.Key);
        foreach (Renderer r in drop)
        {
            if (r != null && IsGlow(r.sharedMaterials)) r.sharedMaterials = saved[r];
            saved.Remove(r);
        }
    }

    static bool IsGlow(Material[] ms)
    {
        for (int i = 0; i < ms.Length; i++)
            if (ms[i] != null && ms[i].name.EndsWith(GlowTag)) return true;
        return false;
    }

    // Копия материала со свечением, как у HoverHighlight (одна копия на каждый исходный материал)
    Material Glow(Material src)
    {
        if (src == null) return null;
        Material m;
        if (glowOf.TryGetValue(src, out m) && m != null) return m;
        m = new Material(src);
        m.name = src.name + GlowTag;
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", HoverHighlight.glow);
        glowOf[src] = m;
        return m;
    }

    void OnDisable()
    {
        foreach (KeyValuePair<Renderer, Material[]> kv in saved)
            if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
        saved.Clear();
        Glowing = false;
    }

    void OnGUI()
    {
        if (nearest == null || !Active || Event.current.type != EventType.Repaint) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 v = cam.WorldToViewportPoint(nearest.transform.position);
        bool onScreen = v.z > 0f && v.x > 0.05f && v.x < 0.95f && v.y > 0.08f && v.y < 0.95f;
        if (onScreen) return;                                   // и так видно: светится

        // Направление на экране к мусору (за спиной — разворачиваем)
        Vector2 dir = new Vector2(v.x - 0.5f, v.y - 0.5f);
        if (v.z < 0f) dir = -dir;
        if (dir.sqrMagnitude < 1e-4f) dir = Vector2.down;
        dir.Normalize();
        float sw = Screen.width, sh = Screen.height;
        Vector2 c = new Vector2(sw / 2f, sh / 2f);
        float k = Mathf.Min((sw / 2f - 70f) / Mathf.Max(0.01f, Mathf.Abs(dir.x)), (sh / 2f - 70f) / Mathf.Max(0.01f, Mathf.Abs(dir.y)));
        Vector2 pos = c + new Vector2(dir.x, -dir.y) * k;

        float ang = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg + 90f;   // стрелка в текстуре смотрит вверх
        Matrix4x4 old = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang, pos);
        GUI.color = new Color(1f, 0.8f, 0.4f, 0.9f);
        GUI.DrawTexture(new Rect(pos.x - 14f, pos.y - 14f, 28f, 28f), arrow);
        GUI.matrix = old;

        if (st == null) { st = new GUIStyle(UITheme.Label()); st.fontSize = 16; st.alignment = TextAnchor.MiddleCenter; }
        GUI.color = Color.black;
        GUI.Label(new Rect(pos.x - 59f, pos.y + 17f, 120f, 22f), "мусор · " + Mathf.RoundToInt(nearestDist) + " м", st);
        GUI.color = new Color(1f, 0.85f, 0.55f);
        GUI.Label(new Rect(pos.x - 60f, pos.y + 16f, 120f, 22f), "мусор · " + Mathf.RoundToInt(nearestDist) + " м", st);
        GUI.color = Color.white;
    }
}
