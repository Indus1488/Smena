using UnityEngine;

// Модель Володи. Если в Assets/Resources/Models/volodya/ лежит модель-гуманоид (например, с Mixamo),
// используется она: руки, шея и ноги вытягиваются, спина сутулится, походку задаёт код (без файлов анимаций).
// Если модели нет — как раньше:
// При запуске игры кубики, из которых его собрал построитель, заменяются
// на вытянутую «органическую» фигуру: слишком длинные ноги и руки до колен, сутулая спина,
// длинная шея, голова чуть набок, тонкие пальцы, светящиеся глаза.
// Пока он движется, ноги и руки качаются (простая походка), стоя он едва покачивается.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class VolodyaModel : MonoBehaviour
{
    Transform legL, legR, shinL, shinR, armL, armR, foreL, foreR, spine, head;
    Vector3 lastPos;
    float phase;
    MonsterController mc;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        MonsterController m = MonsterController.Find();
        if (m == null) return;
        if (m.GetComponent<VolodyaModel>() == null) m.gameObject.AddComponent<VolodyaModel>();
    }

    void Awake()
    {
        mc = GetComponent<MonsterController>();
        Build();
        lastPos = transform.position;
    }

    // ---------- Сборка ----------
    Material skin, eye;

    void Build()
    {
        // Материалы берём у старых кубиков, потом кубики убираем
        Renderer oldBody = FindRenderer("Torso");
        Renderer oldEye = FindRenderer("EyeL");
        skin = oldBody != null ? oldBody.sharedMaterial : null;
        eye = oldEye != null ? oldEye.sharedMaterial : null;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name == "ScareLight") continue;
            Destroy(c.gameObject);
        }

        if (BuildFromModel()) return;

        Transform root = Pivot("Body", transform, Vector3.zero, Vector3.zero);

        // Ноги: бедро и голень, суставы как шарниры
        legL = Pivot("HipL", root, new Vector3(-0.14f, 1.25f, 0f), Vector3.zero);
        legR = Pivot("HipR", root, new Vector3(0.14f, 1.25f, 0f), Vector3.zero);
        Limb(legL, 0.68f, 0.085f);
        Limb(legR, 0.68f, 0.085f);
        shinL = Pivot("KneeL", legL, new Vector3(0, -0.66f, 0), new Vector3(-12, 0, 0));
        shinR = Pivot("KneeR", legR, new Vector3(0, -0.66f, 0), new Vector3(-12, 0, 0));
        Limb(shinL, 0.62f, 0.065f);
        Limb(shinR, 0.62f, 0.065f);
        Part(PrimitiveType.Capsule, shinL, new Vector3(0, -0.62f, 0.08f), new Vector3(0.09f, 0.16f, 0.09f), new Vector3(90, 0, 0), skin);
        Part(PrimitiveType.Capsule, shinR, new Vector3(0, -0.62f, 0.08f), new Vector3(0.09f, 0.16f, 0.09f), new Vector3(90, 0, 0), skin);

        // Сутулая спина: позвоночник наклонён вперёд
        spine = Pivot("Spine", root, new Vector3(0, 1.25f, 0), new Vector3(28, 0, 0));
        Part(PrimitiveType.Capsule, spine, new Vector3(0, 0.4f, 0), new Vector3(0.36f, 0.45f, 0.24f), Vector3.zero, skin);
        // Рёбра проступают
        for (int i = 0; i < 4; i++)
            Part(PrimitiveType.Capsule, spine, new Vector3(0, 0.3f + i * 0.11f, 0.1f), new Vector3(0.05f, 0.17f, 0.05f), new Vector3(0, 0, 90), skin);

        // Длинная шея и голова, чуть набок
        Transform neck = Pivot("Neck", spine, new Vector3(0, 0.82f, 0), new Vector3(-10, 0, 0));
        Limb(neck, 0.3f, 0.05f, true);
        head = Pivot("Head", neck, new Vector3(0, 0.32f, 0.02f), new Vector3(-30, 0, 14));
        Part(PrimitiveType.Sphere, head, new Vector3(0, 0.13f, 0.03f), new Vector3(0.22f, 0.32f, 0.25f), Vector3.zero, skin);
        Part(PrimitiveType.Sphere, head, new Vector3(-0.05f, 0.16f, 0.13f), new Vector3(0.045f, 0.03f, 0.03f), Vector3.zero, eye);
        Part(PrimitiveType.Sphere, head, new Vector3(0.05f, 0.16f, 0.13f), new Vector3(0.045f, 0.03f, 0.03f), Vector3.zero, eye);
        // Рот: тёмная щель
        Part(PrimitiveType.Cube, head, new Vector3(0, 0.03f, 0.14f), new Vector3(0.09f, 0.012f, 0.02f), Vector3.zero, skin);

        // Руки до колен
        armL = Pivot("ShoulderL", spine, new Vector3(-0.22f, 0.72f, 0), new Vector3(-20, 0, 6));
        armR = Pivot("ShoulderR", spine, new Vector3(0.22f, 0.72f, 0), new Vector3(-20, 0, -6));
        Limb(armL, 0.72f, 0.05f);
        Limb(armR, 0.72f, 0.05f);
        foreL = Pivot("ElbowL", armL, new Vector3(0, -0.7f, 0), new Vector3(-15, 0, 0));
        foreR = Pivot("ElbowR", armR, new Vector3(0, -0.7f, 0), new Vector3(-15, 0, 0));
        Limb(foreL, 0.7f, 0.04f);
        Limb(foreR, 0.7f, 0.04f);
        Fingers(foreL);
        Fingers(foreR);
    }

    Renderer FindRenderer(string name)
    {
        Transform t = transform.Find(name);
        return t != null ? t.GetComponent<Renderer>() : null;
    }

    static Transform Pivot(string name, Transform parent, Vector3 pos, Vector3 euler)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(euler);
        return t;
    }

    GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // Сегмент конечности длиной len, растущий вниз (или вверх) от шарнира
    void Limb(Transform pivot, float len, float thick, bool up = false)
    {
        float dir = up ? 1f : -1f;
        Part(PrimitiveType.Capsule, pivot, new Vector3(0, dir * len / 2f, 0), new Vector3(thick * 2f, len / 2f, thick * 2f), Vector3.zero, skin);
    }

    void Fingers(Transform fore)
    {
        for (int i = 0; i < 4; i++)
        {
            float x = -0.045f + i * 0.03f;
            Part(PrimitiveType.Capsule, fore, new Vector3(x, -0.84f, 0.02f), new Vector3(0.018f, 0.12f, 0.018f),
                new Vector3(-10 - i * 4, 0, (i - 1.5f) * 6f), skin);
        }
    }

    // ---------- Модель-гуманоид ----------
    // Числа характера: насколько он вытянут и сутул (меняйте, если выглядит не так)
    public float height = 2.45f;        // рост, м
    public float armStretch = 1.35f;    // руки длиннее обычного
    public float neckStretch = 1.5f;
    public float legStretch = 1.12f;
    public float hunch = 0.5f;          // сутулость (наклон спины вперёд), -1..1
    public float headTilt = 0.4f;       // голова набок

    Animator anim;

    // Голова (для скримера: её ставят прямо перед камерой)
    public Transform Head
    {
        get
        {
            if (anim != null) { Transform h = anim.GetBoneTransform(HumanBodyBones.Head); if (h != null) return h; }
            return head;
        }
    }
    HumanPoseHandler poseHandler;
    HumanPose pose;
    readonly System.Collections.Generic.Dictionary<string, int> muscle = new System.Collections.Generic.Dictionary<string, int>();

    bool BuildFromModel()
    {
        GameObject prefab = Resources.Load<GameObject>("Models/volodya/volodya");
        if (prefab == null) return false;
        Animator pa = prefab.GetComponentInChildren<Animator>();
        if (pa == null || pa.avatar == null || !pa.avatar.isHuman)
        {
            Debug.LogWarning("Модель Володи найдена, но не гуманоид: проверьте Rig → Humanoid. Пока остаётся модель из капсул.");
            return false;
        }

        GameObject m = Instantiate(prefab, transform, false);
        m.name = "VolodyaMesh";
        anim = m.GetComponentInChildren<Animator>();
        anim.runtimeAnimatorController = null;
        anim.applyRootMotion = false;
        anim.enabled = false;      // позу ставит код через HumanPoseHandler, аниматор не должен её сбрасывать
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        ModelSwap.FixModelMaterials(m, "volodya");   // текстура и нормали из Resources/Models/volodya
        foreach (SkinnedMeshRenderer sr in m.GetComponentsInChildren<SkinnedMeshRenderer>()) sr.updateWhenOffscreen = true;

        // Вытягиваем кости
        Stretch(HumanBodyBones.LeftUpperArm, armStretch);
        Stretch(HumanBodyBones.RightUpperArm, armStretch);
        Stretch(HumanBodyBones.Neck, neckStretch);
        Stretch(HumanBodyBones.LeftUpperLeg, legStretch);
        Stretch(HumanBodyBones.RightUpperLeg, legStretch);

        // Рост
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (any && b.size.y > 0.1f) m.transform.localScale *= height / b.size.y;

        // Светящиеся глаза у головы
        Transform headBone = anim.GetBoneTransform(HumanBodyBones.Head);
        if (headBone != null && eye != null)
        {
            Vector3 f = transform.forward, r = transform.right, u = transform.up;
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(e.GetComponent<Collider>());
                e.name = "Eye";
                e.transform.position = headBone.position + u * 0.07f * height / 1.8f + f * 0.11f * height / 1.8f + r * i * 0.035f * height / 1.8f;
                e.transform.localScale = new Vector3(0.035f, 0.022f, 0.02f) * height / 1.8f;
                e.transform.SetParent(headBone, true);
                e.GetComponent<Renderer>().sharedMaterial = eye;
            }
        }

        poseHandler = new HumanPoseHandler(anim.avatar, anim.transform);
        pose = new HumanPose();
        string[] names = HumanTrait.MuscleName;
        for (int i = 0; i < names.Length; i++) muscle[names[i]] = i;
        Debug.Log("Володя: модель-гуманоид подключена (" + prefab.name + ")");
        return true;
    }

    void OnDestroy()
    {
        if (poseHandler != null) poseHandler.Dispose();
    }

    void Stretch(HumanBodyBones bone, float k)
    {
        Transform t = anim.GetBoneTransform(bone);
        if (t != null) t.localScale *= k;
    }

    void SetMuscle(string name, float v)
    {
        int i;
        if (muscle.TryGetValue(name, out i)) pose.muscles[i] = Mathf.Clamp(v, -1f, 1f);
    }

    void HumanoidPose(float walk, float s, bool scripted)
    {
        poseHandler.GetHumanPose(ref pose);
        float idle = 1f - walk;
        float breath = Mathf.Sin(Time.time * 1.3f) * 0.05f * idle;
        float twitch = (Mathf.PerlinNoise(Time.time * 2f, 7f) > 0.8f) ? 0.35f : 0f;

        SetMuscle("Spine Front-Back", hunch + breath + (scripted ? 0.3f : 0f));
        SetMuscle("Chest Front-Back", hunch * 0.6f);
        SetMuscle("Neck Nod Down-Up", -0.3f);
        SetMuscle("Head Nod Down-Up", -0.35f);
        SetMuscle("Head Tilt Left-Right", headTilt + twitch * idle);

        SetMuscle("Left Upper Leg Front-Back", 0.55f * s * walk);
        SetMuscle("Right Upper Leg Front-Back", -0.55f * s * walk);
        SetMuscle("Left Lower Leg Stretch", 0.8f - 1.1f * Mathf.Max(0f, -s) * walk);
        SetMuscle("Right Lower Leg Stretch", 0.8f - 1.1f * Mathf.Max(0f, s) * walk);

        // Длинные руки висят почти до колен и раскачиваются
        SetMuscle("Left Arm Down-Up", -0.65f);
        SetMuscle("Right Arm Down-Up", -0.65f);
        SetMuscle("Left Arm Front-Back", 0.25f - 0.5f * s * walk);
        SetMuscle("Right Arm Front-Back", 0.25f + 0.5f * s * walk);
        SetMuscle("Left Forearm Stretch", 0.65f);
        SetMuscle("Right Forearm Stretch", 0.65f);

        poseHandler.SetHumanPose(ref pose);
    }

    // ---------- Походка ----------
    void LateUpdate()
    {
        Vector3 d = transform.position - lastPos;
        d.y = 0f;
        float speed = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
        lastPos = transform.position;

        // В сценах «по стене» и в скримере он несётся слишком быстро для шагов — замираем в рывке
        bool running = mc != null && mc.scriptRun;
        bool scripted = mc != null && mc.mode == MonsterController.Mode.Scripted && !running;
        float walk = scripted ? 0f : Mathf.Clamp01(speed / 2f);

        phase += Time.deltaTime * (2.5f + Mathf.Min(speed, 6f) * 2.2f);
        float s = Mathf.Sin(phase);

        if (poseHandler != null)
        {
            HumanoidPose(walk, s, scripted);
            return;
        }
        if (legL == null) return;   // модель не собралась — не сыпем ошибками каждый кадр
        float swing = 32f * walk;

        legL.localRotation = Quaternion.Euler(s * swing, 0, 0);
        legR.localRotation = Quaternion.Euler(-s * swing, 0, 0);
        shinL.localRotation = Quaternion.Euler(-12 - Mathf.Max(0, -s) * 40f * walk, 0, 0);
        shinR.localRotation = Quaternion.Euler(-12 - Mathf.Max(0, s) * 40f * walk, 0, 0);
        armL.localRotation = Quaternion.Euler(-20 - s * swing * 0.8f, 0, 6);
        armR.localRotation = Quaternion.Euler(-20 + s * swing * 0.8f, 0, -6);

        // Стоя — еле заметное «дыхание» и дёрганье головы
        float idle = 1f - walk;
        spine.localRotation = Quaternion.Euler(28 + Mathf.Sin(Time.time * 1.3f) * 2f * idle + (scripted ? 20f : 0f), 0, 0);
        float twitch = (Mathf.PerlinNoise(Time.time * 2f, 7f) > 0.8f) ? 18f : 0f;
        head.localRotation = Quaternion.Euler(-30, twitch * idle, 14);
    }
}
