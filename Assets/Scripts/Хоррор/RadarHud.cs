using UnityEngine;

// Радар (покупается в магазине, только хоррор-режим). Круг в правом нижнем углу:
//  - по кругу бежит луч; Володя ближе 30 м — красная точка, на каждом проходе луча — «пинг»;
//  - он охотится, он рядом (ближе 10 м), а ты не спрятался — радар мигает красным: сейчас найдёт.
// Пока он дома, на радаре пусто — значит, можно работать.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class RadarHud : MonoBehaviour
{
    const float Range = 30f, Size = 170f;
    Texture2D disc, dot;
    float sweep, lastPing;
    GUIStyle st;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("RadarHud").AddComponent<RadarHud>();
    }

    static bool Visible { get { return GameConfig.horror && Inventory.hasRadar && ActController.Act >= 2 && !Hud.hidden && !GameMenus.IsBlocking; } }

    void Start()
    {
        const int N = 128;
        disc = new Texture2D(N, N, TextureFormat.RGBA32, false);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = new Vector2(x - 63.5f, y - 63.5f).magnitude / 64f;
                float a = d > 1f ? 0f : (d > 0.95f ? 1f : (Mathf.Abs(d - 0.5f) < 0.012f ? 0.6f : 0.82f));
                Color c = d > 0.95f ? new Color(0.45f, 0.8f, 0.5f) : (Mathf.Abs(d - 0.5f) < 0.012f ? new Color(0.3f, 0.6f, 0.35f) : new Color(0.02f, 0.07f, 0.04f));
                disc.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
        disc.Apply();
        dot = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d = new Vector2(x - 7.5f, y - 7.5f).magnitude / 8f;
                dot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f)));
            }
        dot.Apply();
    }

    void Update()
    {
        if (!Visible) return;
        float before = sweep;
        sweep = Mathf.Repeat(sweep + Time.deltaTime * 180f, 360f);   // один оборот за 2 с
        MonsterController m = MonsterController.Find();
        if (m == null || !m.gameObject.activeInHierarchy || !OnRadar(m)) return;
        Vector3 rel; float dist;
        if (!Relative(m, out rel, out dist)) return;
        // Пинг, когда луч проходит над точкой
        float ang = Mathf.Repeat(Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg, 360f);
        bool passed = before <= sweep ? (ang > before && ang <= sweep) : (ang > before || ang <= sweep);
        if (passed && Time.time - lastPing > 0.5f) { lastPing = Time.time; Sfx.Play("radar_ping", Mathf.Lerp(1f, 0.4f, dist / Range)); }
    }

    // Дома (между охотами) его на радаре нет — видно только, когда он вышел
    static bool OnRadar(MonsterController m) { return m.mode != MonsterController.Mode.Hunt || !m.IsAway; }

    // Где Володя относительно взгляда игрока (x — вправо, z — вперёд)
    static bool Relative(MonsterController m, out Vector3 rel, out float dist)
    {
        rel = Vector3.zero; dist = 999f;
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 d = m.transform.position - cam.transform.position; d.y = 0f;
        dist = d.magnitude;
        if (dist > Range) return false;
        Vector3 f = cam.transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
        rel = Quaternion.Inverse(Quaternion.LookRotation(f.normalized, Vector3.up)) * d;
        return true;
    }

    void OnGUI()
    {
        if (!Visible || Event.current.type != EventType.Repaint || disc == null) return;
        float sw = Screen.width, sh = Screen.height;
        Rect r = new Rect(sw - Size - 24f, sh - Size - 120f, Size, Size);
        Vector2 c = r.center;
        MonsterController m = MonsterController.Find();
        Vector3 rel; float dist;
        rel = Vector3.zero; dist = 999f;
        bool seen = m != null && m.gameObject.activeInHierarchy && OnRadar(m) && Relative(m, out rel, out dist);
        bool danger = seen && m.IsHunting && dist < 10f && HidingSpot.Current == null;

        GUI.color = danger && Mathf.Repeat(Time.time, 0.4f) < 0.2f ? new Color(1f, 0.25f, 0.2f, 1f) : Color.white;
        GUI.DrawTexture(r, disc);
        // луч
        Matrix4x4 old = GUI.matrix;
        GUIUtility.RotateAroundPivot(sweep, c);
        GUI.color = new Color(0.4f, 1f, 0.5f, 0.55f);
        GUI.DrawTexture(new Rect(c.x - 1f, r.y + 6f, 2f, Size / 2f - 6f), Texture2D.whiteTexture);
        GUI.matrix = old;
        // игрок в центре
        GUI.color = new Color(0.6f, 1f, 0.7f);
        GUI.DrawTexture(new Rect(c.x - 4f, c.y - 4f, 8f, 8f), dot);
        // Володя
        if (seen)
        {
            Vector2 p = c + new Vector2(rel.x, -rel.z) / Range * (Size / 2f - 8f);
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 8f);
            GUI.color = new Color(1f, 0.15f, 0.12f, pulse);
            GUI.DrawTexture(new Rect(p.x - 7f, p.y - 7f, 14f, 14f), dot);
        }
        if (st == null) { st = new GUIStyle(UITheme.Label()); st.fontSize = 14; st.alignment = TextAnchor.MiddleCenter; }
        GUI.color = new Color(0.6f, 1f, 0.7f, 0.8f);
        GUI.Label(new Rect(r.x, r.yMax + 2f, Size, 20f), danger ? "РЯДОМ" : (seen ? Mathf.RoundToInt(dist) + " м" : "чисто"), st);
        GUI.color = Color.white;
    }
}
