using System.Collections;
using UnityEngine;

// Электрощиток на фасаде центрального склада, рядом с лампами (по стене к ним идёт кабель).
// Стоит с начала смены: серый ящик с жёлтым знаком «молния» и зелёной лампочкой.
// В 18:00, после пробежки по лампам, Володя спрыгивает к щитку и бьёт по нему (анимация attack):
// дверца отлетает, сыплются искры, лампочка гаснет — и на районе пропадает свет (ActController).
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class ElectricPanel : MonoBehaviour
{
    public static ElectricPanel Instance;
    public Vector3 Front { get; private set; }     // от стены наружу
    public Vector3 StandPoint { get; private set; } // где встаёт Володя, чтобы ударить

    Transform door;
    Light indicator, flash;
    Renderer indicatorR;
    bool broken;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Instance = new GameObject("ElectricPanel").AddComponent<ElectricPanel>();
    }

    IEnumerator Start()
    {
        yield return null;   // CentralLamps находит лампы в своём Start
        var lamps = CentralLamps.Lamps;
        if (lamps.Count == 0) { enabled = false; yield break; }

        Vector3 normal = lamps[0].forward;          // от стены
        Vector3 along = -lamps[0].right;            // вдоль стены, куда бежит Володя
        Transform last = lamps[0];
        foreach (Transform l in lamps) if (Vector3.Dot(l.position, along) > Vector3.Dot(last.position, along)) last = l;

        // Пол и стена в 2.5 м за последней лампой
        Vector3 p = last.position + along * 2.5f;
        float groundY = 0f;
        RaycastHit hit;
        if (Physics.Raycast(p + normal * 1.2f, Vector3.down, out hit, 30f, ~(1 << 2), QueryTriggerInteraction.Ignore)) groundY = hit.point.y;
        Vector3 probe = new Vector3(p.x, groundY + 1.45f, p.z) + normal * 1.5f;
        Vector3 wall = new Vector3(p.x, groundY + 1.45f, p.z);
        if (Physics.Raycast(probe, -normal, out hit, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore)) wall = hit.point;

        Front = normal;
        transform.position = wall + normal * 0.1f;
        transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
        StandPoint = new Vector3(wall.x, groundY, wall.z) + normal * 0.8f;
        Build(last.position, groundY);
    }

    void Build(Vector3 lampPos, float groundY)
    {
        Material body = Mat(new Color(0.42f, 0.45f, 0.43f), 0.35f, 0.3f);
        Material dark = Mat(new Color(0.12f, 0.12f, 0.13f), 0.1f, 0.2f);
        Material cable = Mat(new Color(0.05f, 0.05f, 0.05f), 0f, 0.3f);

        Part("Box", transform, new Vector3(0, 0, -0.01f), new Vector3(0.6f, 0.8f, 0.18f), body, PrimitiveType.Cube);
        Part("Inside", transform, new Vector3(0, 0, 0.075f), new Vector3(0.52f, 0.7f, 0.02f), dark, PrimitiveType.Cube);

        // Дверца на петлях слева
        door = new GameObject("DoorHinge").transform;
        door.SetParent(transform, false);
        door.localPosition = new Vector3(-0.3f, 0, 0.09f);
        Part("Door", door, new Vector3(0.3f, 0, 0.01f), new Vector3(0.6f, 0.8f, 0.02f), body, PrimitiveType.Cube);
        GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(sign.GetComponent<Collider>());
        sign.name = "Sign";
        sign.transform.SetParent(door, false);
        sign.transform.localPosition = new Vector3(0.3f, 0.12f, 0.022f);
        sign.transform.localRotation = Quaternion.Euler(0, 180, 0);
        sign.transform.localScale = new Vector3(0.22f, 0.2f, 1f);
        Material sm = Mat(Color.white, 0f, 0.2f);
        sm.mainTexture = SignTexture(); sm.SetTexture("_BaseMap", sm.mainTexture);
        sm.SetFloat("_AlphaClip", 1f); sm.SetFloat("_Cutoff", 0.5f); sm.EnableKeyword("_ALPHATEST_ON");
        sign.GetComponent<Renderer>().sharedMaterial = sm;

        // Лампочка «есть ток»
        GameObject ind = Part("Indicator", transform, new Vector3(0.2f, 0.32f, 0.12f), new Vector3(0.035f, 0.035f, 0.035f), null, PrimitiveType.Sphere);
        indicatorR = ind.GetComponent<Renderer>();
        Material im = Mat(new Color(0.2f, 1f, 0.3f), 0f, 0.6f);
        im.EnableKeyword("_EMISSION"); im.SetColor("_EmissionColor", new Color(0.3f, 2.5f, 0.5f));
        indicatorR.sharedMaterial = im;
        indicator = new GameObject("IndicatorLight").AddComponent<Light>();
        indicator.transform.SetParent(ind.transform, false);
        indicator.type = LightType.Point; indicator.range = 1.4f; indicator.intensity = 0.6f; indicator.color = new Color(0.4f, 1f, 0.5f);
        indicator.shadows = LightShadows.None;

        // Кабель от щитка вверх к лампам
        float top = Mathf.Max(groundY + 2.5f, lampPos.y);
        Vector3 c0 = transform.position + Vector3.up * 0.4f - transform.forward * 0.05f;
        Vector3 c1 = new Vector3(c0.x, top, c0.z);
        GameObject c = Part("Cable", null, (c0 + c1) / 2f, new Vector3(0.03f, (c1.y - c0.y) / 2f, 0.03f), cable, PrimitiveType.Cylinder);
        c.transform.SetParent(transform, true);

        flash = new GameObject("SparkFlash").AddComponent<Light>();
        flash.transform.SetParent(transform, false);
        flash.transform.localPosition = new Vector3(0, 0, 0.4f);
        flash.type = LightType.Point; flash.range = 7f; flash.intensity = 0f; flash.color = new Color(1f, 0.75f, 0.4f);
    }

    // Удар Володи: дверца отлетает, искры, вспышка, лампочка гаснет
    public void Break()
    {
        if (broken) return;
        broken = true;
        Sfx.PlayAt("panel_break", transform.position, 1f);
        StartCoroutine(BreakAnim());
        Sparks();
        if (indicator != null) indicator.enabled = false;
        if (indicatorR != null) { indicatorR.material.DisableKeyword("_EMISSION"); indicatorR.material.SetColor("_BaseColor", new Color(0.1f, 0.15f, 0.1f)); }
    }

    IEnumerator BreakAnim()
    {
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.25f);
            if (door != null) door.localRotation = Quaternion.Euler(18f * k, -125f * k, 9f * k);   // дверцу вырвало
            if (flash != null) flash.intensity = Mathf.Lerp(9f, 0f, t / 0.6f) * (Random.value > 0.3f ? 1f : 0.2f);
            yield return null;
        }
        if (flash != null) flash.intensity = 0f;
        // Ещё пару секунд коротит
        for (int i = 0; i < 6; i++)
        {
            yield return new WaitForSeconds(Random.Range(0.15f, 0.5f));
            if (flash != null) flash.intensity = 3f;
            Sparks(12);
            yield return new WaitForSeconds(0.05f);
            if (flash != null) flash.intensity = 0f;
        }
    }

    void Sparks(int count = 70)
    {
        GameObject g = new GameObject("Sparks");
        g.transform.position = transform.position + transform.forward * 0.12f;
        g.transform.rotation = Quaternion.LookRotation(transform.forward);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.2f; main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.5f, 0.15f));
        main.gravityModifier = 1.5f;
        main.maxParticles = 200;
        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 50f; sh.radius = 0.05f;
        var r = g.GetComponent<ParticleSystemRenderer>();
        Material m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetColor("_BaseColor", new Color(1f, 0.8f, 0.4f));
        r.sharedMaterial = m;
        r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 3f;
        ps.Play();
        Destroy(g, 2f);
    }

    GameObject Part(string name, Transform parent, Vector3 pos, Vector3 size, Material m, PrimitiveType type)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        if (parent != null) { g.transform.SetParent(parent, false); g.transform.localPosition = pos; }
        else g.transform.position = pos;
        g.transform.localScale = size;
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        if (type != PrimitiveType.Cube || name != "Box") Destroy(g.GetComponent<Collider>());
        return g;
    }

    static Material Mat(Color c, float metal, float smooth)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        return m;
    }

    // Жёлтый треугольник с чёрной молнией
    static Texture2D SignTexture()
    {
        const int N = 128;
        Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, true);
        Color yellow = new Color(1f, 0.82f, 0.1f), black = new Color(0.05f, 0.05f, 0.05f), none = new Color(0, 0, 0, 0);
        Vector2[] bolt = { new Vector2(70, 98), new Vector2(46, 58), new Vector2(64, 58), new Vector2(52, 22), new Vector2(84, 68), new Vector2(66, 68), new Vector2(78, 98) };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                bool inTri = v > 0.06f && v < 0.94f && Mathf.Abs(u - 0.5f) < (0.94f - v) * 0.58f;
                bool edge = inTri && !(v > 0.12f && Mathf.Abs(u - 0.5f) < (0.88f - v) * 0.58f - 0.02f);
                Color c = !inTri ? none : (edge || InPoly(bolt, x, y) ? black : yellow);
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    static bool InPoly(Vector2[] p, float x, float y)
    {
        bool c = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x) c = !c;
        return c;
    }
}
