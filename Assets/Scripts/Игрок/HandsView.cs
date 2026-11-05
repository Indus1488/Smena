using System.Collections.Generic;
using UnityEngine;

// Руки от первого лица. Модель — любой гуманоид с Mixamo в Assets/Resources/Models/player_arms/
// (от него видны только руки: голова и шея прячутся, тело остаётся за камерой).
// - В правой руке — предмет, выбранный в инвентаре (рулетка, ключ, швабра...). Рохлю в руку не берём.
// - Когда горит фонарик — он в левой руке.
// - Пульс высокий → руки и предмет дрожат (CameraFeel.Tremor).
// - Руки чуть отстают от поворота мыши.
// Если модели нет, ничего не происходит. Позу задаёт код; числа позы — поля ниже (можно крутить в Inspector во время Play).
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class HandsView : MonoBehaviour
{
    public static HandsView Instance;
    public static bool Showcase;   // F5: модель целиком перед камерой (проверка, что она вообще рисуется)

    [Header("Где руки относительно камеры")]
    public Vector3 offset = new Vector3(0f, -0.04f, -0.06f);

    [Header("Где кисти относительно камеры (x вправо, y вверх, z вперёд), метры")]
    public Vector3 rightIdle = new Vector3(0.17f, -0.25f, 0.34f);
    public Vector3 rightHoldPos = new Vector3(0.16f, -0.21f, 0.31f);
    public Vector3 leftIdle = new Vector3(-0.17f, -0.26f, 0.33f);
    public Vector3 leftLightPos = new Vector3(-0.16f, -0.21f, 0.31f);
    [Header("Куда смотрят локти (относительно плеча)")]
    public Vector3 elbowHint = new Vector3(0.45f, -0.6f, -0.2f);
    [Header("Швабра")]
    // Швабра как в Viscera Cleanup: верх ручки у правого нижнего угла экрана, ручка идёт по диагонали к щётке на полу впереди.
    public Vector3 mopRightHold = new Vector3(0.24f, -0.44f, 0.20f);   // правый кулак на конце ручки (камера: x вправо, y вверх, z вперёд)
    public float mopLeftDown = 0.42f;                                  // левая рука ниже по ручке, ближе к щётке (м)
    public float mopReach = 1.35f;                                     // щётка впереди игрока (м)
    public float mopSide = -0.12f;                                     // и чуть левее центра (м)
    [Tooltip("Если ладони смотрят не туда (наружу вместо внутрь) — включи")]
    public bool flipPalm = false;

    [Header("Старое (мышцы Unity, -1..1), сейчас не используется")]
    public float holdDownUp = -0.25f;
    public float holdFrontBack = 0.8f;
    public float holdForearm = 0.15f;
    public float holdGrip = -0.9f;      // насколько сжаты пальцы, когда что-то держишь (-1 — кулак, 1 — растопырены)

    [Header("Левая рука с фонариком")]
    public float lightDownUp = -0.35f;
    public float lightFrontBack = 0.7f;
    public float lightForearm = 0.25f;

    Camera cam;
    Animator anim;
    HumanPoseHandler handler;
    HumanPose pose;
    readonly Dictionary<string, int> muscle = new Dictionary<string, int>();
    readonly List<int> rightFingers = new List<int>(), leftFingers = new List<int>();
    Transform model, rightHand, leftHand, rightHold, leftHold;
    Transform rUpper, rLower, lUpper, lLower;
    string heldId = "-";
    GameObject heldItem, lightItem;
    Vector2 sway;
    float rightRaise, leftRaise;
    // Швабра: щётка стоит на полу перед игроком и ездит по нему, руки держат ручку
    Transform mopRoot, mopHandle, mopCap, mopHead;
    float sweepPhase, sweepUntil;
    const float MopLength = 1.5f;
    readonly List<Renderer> armRenderers = new List<Renderer>();

    // Вызывается, когда убираешь мусор: щётка делает пару взмахов
    public static void Sweep()
    {
        if (Instance != null) Instance.sweepUntil = Time.time + 0.7f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("HandsView").AddComponent<HandsView>();
    }

    void Start()
    {
        Instance = this;
        Showcase = false;
        cam = Camera.main;
        GameObject prefab = null;
        foreach (GameObject g in Resources.LoadAll<GameObject>("Models/player_arms"))
        {
            Animator a = g.GetComponentInChildren<Animator>();
            if (a != null && a.avatar != null && a.avatar.isHuman) { prefab = g; break; }
        }
        if (cam == null || prefab == null)
        {
            Debug.Log("[Hands] руки не показаны: " + (cam == null ? "нет камеры" : "нет гуманоида в Resources/Models/player_arms"));
            enabled = false; return;
        }
        Debug.Log("[Hands] руки загружены: " + prefab.name + " (видны, когда в руке предмет или горит фонарик)");

        // У Player в сцене масштаб был (0, 1, 0): дети камеры (руки, предметы) сплющивались в ноль и пропадали.
        // Возвращаем 1, а сам объект игрока прячем, чтобы его не было видно.
        // Чиним всю цепочку: камера, CameraRig, Player (у камеры в сцене тоже был масштаб 0 по X и Z)
        for (Transform t = cam.transform; t != null; t = t.parent)
        {
            Vector3 rs = t.localScale;
            if (Mathf.Abs(rs.x) < 0.01f || Mathf.Abs(rs.y) < 0.01f || Mathf.Abs(rs.z) < 0.01f)
            {
                t.localScale = Vector3.one;
                Debug.Log("[Hands] масштаб " + t.name + " был " + rs + ", исправлен на (1, 1, 1)");
            }
        }
        MeshRenderer playerMesh = cam.transform.root.GetComponent<MeshRenderer>();
        if (playerMesh != null) playerMesh.enabled = false;

        // Капсула игрока стала настоящего размера (раньше масштаб 0 делал её нулевой ширины).
        // Чтобы лучи прицела (E, подсветка, рохля) не упирались в собственную капсулу, когда смотришь вниз,
        // игрок — на слое Ignore Raycast. Стены и пол его по-прежнему держат.
        GameObject playerGo = cam.transform.root.gameObject;
        playerGo.layer = 2;
        CharacterController ccPlayer = playerGo.GetComponent<CharacterController>();
        if (ccPlayer != null && ccPlayer.radius > 0.3f) ccPlayer.radius = 0.3f;   // не застревать в проходах

        GameObject m = Instantiate(prefab, cam.transform, false);
        m.name = "PlayerArms";
        model = m.transform;
        anim = m.GetComponentInChildren<Animator>();
        anim.enabled = false;
        anim.runtimeAnimatorController = null;
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r is SkinnedMeshRenderer) ((SkinnedMeshRenderer)r).updateWhenOffscreen = true;
        }
        ModelSwap.FixModelMaterials(m, "player_arms");

        // От модели оставляем только предплечья и кисти (как руки в шутерах): тело, голову и ноги вырезаем из меша
        KeepOnlyForearms(m);

        rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        rUpper = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rLower = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        lUpper = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        lLower = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightHold = new GameObject("RightHold").transform; rightHold.SetParent(cam.transform, false);
        leftHold = new GameObject("LeftHold").transform; leftHold.SetParent(cam.transform, false);

        handler = new HumanPoseHandler(anim.avatar, anim.transform);
        pose = new HumanPose();
        string[] names = HumanTrait.MuscleName;
        for (int i = 0; i < names.Length; i++)
        {
            muscle[names[i]] = i;
            bool finger = names[i].Contains("Thumb") || names[i].Contains("Index") || names[i].Contains("Middle") || names[i].Contains("Ring") || names[i].Contains("Little");
            if (finger && names[i].Contains("Stretched"))
            {
                if (names[i].StartsWith("Right") || names[i].StartsWith("RightHand")) rightFingers.Add(i);
                else leftFingers.Add(i);
            }
        }
        cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.02f);
    }

    // Вырезаем из всех мешей треугольники, которые не двигаются вместе с предплечьями и кистями.
    // Кости тела остаются (по ним считается поза), просто их кожа и одежда больше не рисуются.
    void KeepOnlyForearms(GameObject root)
    {
        HashSet<Transform> keep = new HashSet<Transform>();
        foreach (HumanBodyBones hb in new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm })
        {
            Transform t = anim.GetBoneTransform(hb);
            if (t != null) foreach (Transform d in t.GetComponentsInChildren<Transform>(true)) keep.Add(d);
        }
        int kept = 0;
        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh src = smr.sharedMesh;
            if (src == null) { smr.enabled = false; continue; }
            if (!src.isReadable)
            {
                Debug.Log("[Hands] меш " + src.name + " нечитаемый — нужен переимпорт (Smena → Переимпортировать модели)");
                continue;
            }
            Transform[] bones = smr.bones;
            BoneWeight[] bw = src.boneWeights;
            bool[] ok = new bool[bw.Length];
            for (int i = 0; i < bw.Length; i++)
            {
                float w = 0f;
                if (In(bones, bw[i].boneIndex0, keep)) w += bw[i].weight0;
                if (In(bones, bw[i].boneIndex1, keep)) w += bw[i].weight1;
                if (In(bones, bw[i].boneIndex2, keep)) w += bw[i].weight2;
                if (In(bones, bw[i].boneIndex3, keep)) w += bw[i].weight3;
                ok[i] = w >= 0.35f;      // и немного у локтя, чтобы руку не обрезало ровным краем
            }
            Mesh dst = Instantiate(src);
            dst.name = src.name + "_forearms";
            int total = 0;
            for (int sub = 0; sub < src.subMeshCount; sub++)
            {
                int[] tri = src.GetTriangles(sub);
                List<int> nt = new List<int>();
                for (int j = 0; j + 2 < tri.Length; j += 3)
                    if (ok[tri[j]] && ok[tri[j + 1]] && ok[tri[j + 2]]) { nt.Add(tri[j]); nt.Add(tri[j + 1]); nt.Add(tri[j + 2]); }
                dst.SetTriangles(nt, sub);
                total += nt.Count / 3;
            }
            if (total == 0) { smr.enabled = false; Destroy(dst); }
            else { smr.sharedMesh = dst; kept += total; armRenderers.Add(smr); }
        }
        Debug.Log("[Hands] оставлены предплечья и кисти: " + kept + " треугольников");
    }

    static bool In(Transform[] bones, int i, HashSet<Transform> set)
    {
        return i >= 0 && i < bones.Length && bones[i] != null && set.Contains(bones[i]);
    }

    // Ручка швабры проходит через точку grip (середину правой ладони) и упирается щёткой в пол впереди.
    // Щётка ездит влево-вправо при ходьбе и уборке. Возвращает направление ручки (от щётки вверх).
    Vector3 PlaceMopThrough(Vector3 grip, float dt, float move)
    {
        Transform ct = cam.transform;
        Vector3 fwd = Vector3.ProjectOnPlane(ct.forward, Vector3.up);
        if (fwd.sqrMagnitude < 1e-4f) fwd = ct.root.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        bool sweeping = Time.time < sweepUntil;
        sweepPhase += dt * (move * 5f + (sweeping ? 11f : 0f));
        float side = Mathf.Sin(sweepPhase) * (sweeping ? 0.3f : 0.12f * move);

        float floorY = ct.root.position.y - 1.03f;
        RaycastHit hit;
        Vector3 probe = new Vector3(ct.position.x, grip.y, ct.position.z) + fwd * mopReach;
        if (Physics.Raycast(new Vector3(probe.x, grip.y + 0.2f, probe.z), Vector3.down, out hit, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore)) floorY = hit.point.y;

        Vector3 body = new Vector3(ct.position.x, floorY, ct.position.z);
        Vector3 brush = body + fwd * mopReach + right * (mopSide + side);   // щётка на полу впереди, ручка по диагонали к правому кулаку
        brush.y = floorY + 0.005f;
        Vector3 joint = brush + Vector3.up * 0.05f;                    // шарнир над тряпкой
        Vector3 dir = (grip - joint).normalized;
        float len = (grip - joint).magnitude + 0.10f;                  // ручка чуть торчит над кулаком
        mopRoot.position = joint;
        Vector3 f = Vector3.ProjectOnPlane(fwd, dir);
        mopRoot.rotation = Quaternion.LookRotation(f.sqrMagnitude > 1e-4f ? f.normalized : fwd, dir);
        mopHandle.localScale = new Vector3(1f, len * 0.5f, 1f);       // цилиндр Unity высотой 2 → ровно len
        mopCap.localPosition = new Vector3(0f, len, 0f);
        mopHead.position = brush;                                      // тряпка всегда лежит плашмя на полу
        mopHead.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        return dir;
    }

    // Середина ладони: от запястья к пальцам и чуть от ладони наружу (туда кладём предмет)
    Vector3 PalmCenter(bool rightSide)
    {
        Transform hand = rightSide ? rightHand : leftHand;
        Transform idx = anim.GetBoneTransform(rightSide ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        Transform lit = anim.GetBoneTransform(rightSide ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
        Transform mid = anim.GetBoneTransform(rightSide ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
        if (idx == null || lit == null || mid == null) return hand.position;
        Vector3 across = idx.position - lit.position, along = mid.position - hand.position;
        Vector3 palm = (rightSide ? Vector3.Cross(along, across) : Vector3.Cross(across, along)).normalized;
        if (flipPalm) palm = -palm;
        return hand.position + along * 0.75f + palm * 0.035f;
    }

    // Разворачивает предплечье вокруг своей оси, чтобы ладонь смотрела в нужную сторону (палец к пальцу — по костям кисти)
    void TwistPalm(Transform lower, Transform hand, bool rightSide, Vector3 want)
    {
        if (lower == null || hand == null) return;
        Transform idx = anim.GetBoneTransform(rightSide ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        Transform lit = anim.GetBoneTransform(rightSide ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
        Transform mid = anim.GetBoneTransform(rightSide ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
        if (idx == null || lit == null || mid == null) return;
        Vector3 across = idx.position - lit.position;      // от мизинца к указательному
        Vector3 along = mid.position - hand.position;       // к пальцам
        Vector3 palm = rightSide ? Vector3.Cross(along, across) : Vector3.Cross(across, along);
        if (flipPalm) palm = -palm;
        Vector3 axis = (hand.position - lower.position).normalized;
        Vector3 a = Vector3.ProjectOnPlane(palm, axis), b = Vector3.ProjectOnPlane(want, axis);
        if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return;
        float ang = Vector3.SignedAngle(a, b, axis);
        lower.rotation = Quaternion.AngleAxis(ang, axis) * lower.rotation;
    }

    // Две кости (плечо → локоть → кисть): ставим кисть в точку target (в координатах камеры),
    // локоть уходит в сторону hint (смещение от плеча, тоже в координатах камеры)
    void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 targetCam, Vector3 hintCam)
    {
        if (upper == null || lower == null || hand == null) return;
        Transform ct = cam.transform;
        Vector3 a = upper.position, b = lower.position, c = hand.position;
        Vector3 t = ct.TransformPoint(targetCam);
        float ab = (b - a).magnitude, bc = (c - b).magnitude;
        if (ab < 1e-4f || bc < 1e-4f) return;
        Vector3 at = t - a;
        float d = Mathf.Clamp(at.magnitude, Mathf.Abs(ab - bc) + 0.01f, ab + bc - 0.005f);
        Vector3 dir = at.normalized;
        // Где окажется локоть: на окружности вокруг линии плечо-кисть, в сторону подсказки
        float x = (ab * ab - bc * bc + d * d) / (2f * d);
        float h = Mathf.Sqrt(Mathf.Max(0f, ab * ab - x * x));
        Vector3 hint = ct.TransformDirection(hintCam);
        Vector3 side = Vector3.ProjectOnPlane(hint, dir);
        if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(-ct.up, dir);
        Vector3 elbow = a + dir * x + side.normalized * h;
        // Поворачиваем плечо к локтю, потом предплечье к кисти
        upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
        b = lower.position; c = hand.position;
        Vector3 hitPoint = a + dir * d;
        lower.rotation = Quaternion.FromToRotation(c - b, hitPoint - b) * lower.rotation;
    }

    // Отчёт для отладки (F5): где модель, её масштаб, руки относительно камеры, что с мешами
    public void Report()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder("[Hands] ОТЧЁТ\n");
        sb.AppendLine("enabled=" + enabled + " model=" + (model != null ? model.name : "null") + " active=" + (model != null && model.gameObject.activeInHierarchy));
        if (cam != null) sb.AppendLine("camera=" + cam.name + " near=" + cam.nearClipPlane + " mask=" + cam.cullingMask + " lossyScale=" + cam.transform.lossyScale);
        if (model != null)
        {
            sb.AppendLine("model localPos=" + model.localPosition.ToString("F3") + " lossyScale=" + model.lossyScale.ToString("F4") + " layer=" + model.gameObject.layer);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Vector3 c = cam.transform.InverseTransformPoint(r.bounds.center);
                SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
                sb.AppendLine("  " + r.name + " " + r.GetType().Name + " on=" + r.enabled + "/" + r.gameObject.activeInHierarchy
                    + " mesh=" + (smr != null ? (smr.sharedMesh != null ? smr.sharedMesh.name : "NULL") : "-")
                    + " mats=" + r.sharedMaterials.Length + " shader=" + (r.sharedMaterial != null ? r.sharedMaterial.shader.name : "NULL")
                    + " centerCam=" + c.ToString("F2") + " size=" + r.bounds.size.ToString("F2"));
            }
        }
        if (rightHand != null) sb.AppendLine("rightHand cam=" + cam.transform.InverseTransformPoint(rightHand.position).ToString("F3"));
        if (leftHand != null) sb.AppendLine("leftHand cam=" + cam.transform.InverseTransformPoint(leftHand.position).ToString("F3"));
        Transform head = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head != null) sb.AppendLine("head cam=" + cam.transform.InverseTransformPoint(head.position).ToString("F3"));
        sb.AppendLine("held=" + heldId + " item=" + (heldItem != null) + " rightRaise=" + rightRaise.ToString("F2") + " selected=" + InventoryBar.SelectedId);
        Debug.Log(sb.ToString());
    }

    void OnDestroy()
    {
        if (handler != null) handler.Dispose();
    }

    void Set(string n, float v)
    {
        int i;
        if (muscle.TryGetValue(n, out i)) pose.muscles[i] = Mathf.Clamp(v, -1f, 1f);
    }

    void LateUpdate()
    {
        if (handler == null || cam == null) return;
        float dt = Time.deltaTime;
        bool hidden = Hud.hidden && FinaleController.Locked;
        model.gameObject.SetActive(!hidden);
        if (hidden) return;

        if (Showcase)
        {
            // Проверка: модель стоит целиком в 2 м перед камерой лицом к нам, голова на месте
            model.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.localPosition = new Vector3(0f, -1.2f, 2.2f);
            foreach (Renderer ar in armRenderers) if (ar != null) ar.enabled = true;   // в проверке видно всегда
            return;
        }

        // Что в руках
        string want = InventoryBar.SelectedId;
        if (want == "jack" || want == "bag" || TapeMeasureScreen.IsOpen && Inventory.hasTape) want = TapeMeasureScreen.IsOpen ? "tape" : "";
        if (want != heldId) SwapHeld(want);
        bool lightOn = Inventory.hasFlashlight && Inventory.flashlightOn;
        if (lightOn && lightItem == null) lightItem = MakeFlashlight(leftHold);
        if (lightItem != null) lightItem.SetActive(lightOn);

        rightRaise = Mathf.MoveTowards(rightRaise, heldItem != null ? 1f : 0f, dt * 3f);
        leftRaise = Mathf.MoveTowards(leftRaise, lightOn ? 1f : 0f, dt * 3f);

        // Покачивание за мышью (руки чуть отстают)
        Vector2 mouse = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        sway = Vector2.Lerp(sway, -mouse * 0.012f, dt * 8f);

        // Дрожь от пульса
        float tr = CameraFeel.Tremor;
        float n1 = (Mathf.PerlinNoise(Time.time * 11f, 1.3f) - 0.5f) * 2f * tr;
        float n2 = (Mathf.PerlinNoise(2.1f, Time.time * 13f) - 0.5f) * 2f * tr;

        // Поза
        handler.GetHumanPose(ref pose);
        Set("Spine Front-Back", 0f); Set("Chest Front-Back", 0f);
        // Руки в позе просто опущены и чуть согнуты — куда поставить кисти, решает IK ниже
        Set("Right Arm Down-Up", -0.6f); Set("Left Arm Down-Up", -0.6f);
        foreach (string side in new[] { "Right", "Left" })
        {
            // всё остальное в руке — в ноль каждый кадр (иначе поза копила бы повороты от IK)
            Set(side + " Arm Front-Back", 0f); Set(side + " Arm Twist In-Out", 0f);
            Set(side + " Forearm Twist In-Out", 0f); Set(side + " Hand In-Out", 0f);
            Set(side + " Shoulder Down-Up", 0f); Set(side + " Shoulder Front-Back", 0f);
        }
        Set("Right Forearm Stretch", 0.5f); Set("Left Forearm Stretch", 0.5f);
        Set("Right Hand Down-Up", n2 * 0.25f); Set("Left Hand Down-Up", n1 * 0.25f);
        bool mop = mopRoot != null;
        float rightUse = rightRaise, leftUse = mop && !lightOn ? rightRaise : leftRaise;
        foreach (int i in rightFingers) pose.muscles[i] = Mathf.Lerp(0.2f, holdGrip, rightUse);
        foreach (int i in leftFingers) pose.muscles[i] = Mathf.Lerp(0.2f, holdGrip, leftUse);
        handler.SetHumanPose(ref pose);

        // Модель держим у камеры: голова (спрятанная) — на уровне глаз, плечи прямо под камерой, чтобы руки дотягивались в кадр
        Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
        model.localRotation = Quaternion.identity;
        if (head != null)
        {
            Vector3 headLocal = cam.transform.InverseTransformPoint(head.position) - model.localPosition;
            model.localPosition = -headLocal + offset + new Vector3(sway.x, sway.y, 0f) + new Vector3(n1, n2, 0f) * 0.006f;
        }

        // Кисти. Пустые руки уходят вниз из кадра (как в большинстве игр): видна только рука, которая что-то держит.
        Transform ct = cam.transform;
        Vector3 jiggle = new Vector3(sway.x, sway.y, 0f) * 0.6f + new Vector3(n1, n2, 0f) * 0.01f;
        float move = Mathf.Clamp01(Input.GetAxis("Vertical") * Input.GetAxis("Vertical") + Input.GetAxis("Horizontal") * Input.GetAxis("Horizontal"));
        float walk = Mathf.Sin(Time.time * 7f) * move * 0.012f;
        Vector3 rightAway = new Vector3(0.3f, -0.95f, -0.05f), leftAway = new Vector3(-0.3f, -0.95f, -0.05f);
        Vector3 rt = Vector3.Lerp(rightAway, rightHoldPos, rightRaise) + jiggle + Vector3.up * walk;
        Vector3 lt = Vector3.Lerp(leftAway, leftLightPos, leftRaise) + jiggle - Vector3.up * walk;
        Vector3 rPalm = (-ct.right * 0.7f - ct.up * 0.7f).normalized;   // ладонь влево-вниз: держит предмет перед собой
        Vector3 lPalm = (ct.right * 0.7f - ct.up * 0.7f).normalized;

        if (mop)
        {
            // Швабра. Сначала правая рука в естественной позе (низко справа, кулак вбок),
            // потом ручку проводим ЧЕРЕЗ середину её ладони к полу впереди — ручка всегда в кулаке.
            // Кулак держим относительно тела (поворот только по горизонтали), а не взгляда:
            // смотришь вверх — швабра остаётся у пояса, а не взлетает вместе с камерой
            Vector3 flatF = Vector3.ProjectOnPlane(ct.forward, Vector3.up);
            if (flatF.sqrMagnitude < 1e-4f) flatF = ct.root.forward;
            Quaternion yaw = Quaternion.LookRotation(flatF.normalized, Vector3.up);
            rt = ct.InverseTransformPoint(ct.position + yaw * mopRightHold) + jiggle + Vector3.up * walk;
            rPalm = (-ct.right * 0.8f + ct.forward * 0.3f).normalized;
            SolveArm(rUpper, rLower, rightHand, rt, new Vector3(elbowHint.x, elbowHint.y, elbowHint.z));
            TwistPalm(rLower, rightHand, true, rPalm);
            Vector3 grip = PalmCenter(true);
            Vector3 dir = PlaceMopThrough(grip, dt, move);

            if (!lightOn)
            {
                // Левая — ниже по той же ручке, ближе к щётке; IK ставит запястье, поэтому второй проход по промаху ладони
                Vector3 gl = grip - dir * mopLeftDown;
                lt = ct.InverseTransformPoint(gl);
                lPalm = (ct.right * 0.7f - ct.up * 0.5f).normalized;   // хват сверху
                SolveArm(lUpper, lLower, leftHand, lt, new Vector3(-elbowHint.x, elbowHint.y, elbowHint.z));
                TwistPalm(lLower, leftHand, false, lPalm);
                lt += ct.InverseTransformVector(gl - PalmCenter(false));
                SolveArm(lUpper, lLower, leftHand, lt, new Vector3(-elbowHint.x, elbowHint.y, elbowHint.z));
                TwistPalm(lLower, leftHand, false, lPalm);
            }
            else
            {
                SolveArm(lUpper, lLower, leftHand, lt, new Vector3(-elbowHint.x, elbowHint.y, elbowHint.z));
                TwistPalm(lLower, leftHand, false, lPalm);
            }
        }
        else
        {
            SolveArm(rUpper, rLower, rightHand, rt, new Vector3(elbowHint.x, elbowHint.y, elbowHint.z));
            SolveArm(lUpper, lLower, leftHand, lt, new Vector3(-elbowHint.x, elbowHint.y, elbowHint.z));
            TwistPalm(rLower, rightHand, true, rPalm);
            TwistPalm(lLower, leftHand, false, lPalm);
        }

        // Обе руки без дела — модель не рисуем совсем
        bool anyHand = rightUse > 0.02f || leftUse > 0.02f;
        foreach (Renderer ar in armRenderers) if (ar != null && ar.enabled != anyHand) ar.enabled = anyHand;

        // Предметы — в ладонях, смотрят туда же, куда камера
        if (rightHand != null) { rightHold.position = PalmCenter(true); rightHold.rotation = cam.transform.rotation; }
        if (leftHand != null) { leftHold.position = PalmCenter(false); leftHold.rotation = cam.transform.rotation; }
    }

    // ---------- Предметы ----------
    void SwapHeld(string id)
    {
        heldId = id;
        if (heldItem != null) Destroy(heldItem);
        heldItem = null;
        mopRoot = null;
        switch (id)
        {
            case "tape":   // 3D-модель из Resources/Models/tape, если есть; иначе рулетка из кубиков
                heldItem = Resources.LoadAll<GameObject>("Models/tape").Length > 0
                    ? FromModel("Models/tape", 0.08f, rightHold, Vector3.zero, new Vector3(0, 90, 0))
                    : MakeTape(rightHold);
                break;
            case "key": heldItem = FromModel("Models/key", 0.09f, rightHold, new Vector3(0, 0, 0.02f), new Vector3(0, 90, 0)); break;
            case "mop":
                mopRoot = new GameObject("HeldMop").transform;
                MakeMop(mopRoot);      // своя швабра из деталей: ручка всегда точно по линии рук, тряпка лежит на полу
                heldItem = mopRoot.gameObject;
                break;
            case "light": heldItem = null; break;    // фонарик и так в левой руке, когда горит
            case "bat": heldItem = Part(PrimitiveType.Cube, rightHold, new Vector3(0, 0, 0.05f), new Vector3(0.07f, 0.05f, 0.04f), new Color(0.12f, 0.12f, 0.13f)); break;
        }
    }

    GameObject FromModel(string folder, float size, Transform parent, Vector3 pos, Vector3 euler)
    {
        GameObject[] list = Resources.LoadAll<GameObject>(folder);
        if (list.Length == 0) return null;
        GameObject root = new GameObject("Held");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = pos;
        root.transform.localRotation = Quaternion.Euler(euler);
        GameObject m = Instantiate(list[0], root.transform, false);
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        ModelSwap.FixModelMaterials(m, folder.Substring(folder.LastIndexOf('/') + 1));
        Bounds b = new Bounds(); bool any = false;
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (any)
        {
            float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (big > 1e-4f) m.transform.localScale *= size / big;
            // центр модели — в ладонь
            b = new Bounds(); any = false;
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            m.transform.position += root.transform.position - b.center;
        }
        return root;
    }

    // Швабра как на складе: синяя металлическая ручка, чёрный наконечник, пластиковая планка и серая тряпка с бахромой
    void MakeMop(Transform root)
    {
        mopHandle = new GameObject("Handle").transform;
        mopHandle.SetParent(root, false);
        Part(PrimitiveType.Cylinder, mopHandle, new Vector3(0f, 1f, 0f), new Vector3(0.026f, 1f, 0.026f), new Color(0.16f, 0.34f, 0.58f));
        mopCap = Part(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.032f, 0.05f, 0.032f), new Color(0.06f, 0.06f, 0.07f)).transform;

        mopHead = new GameObject("Head").transform;
        mopHead.SetParent(root, false);
        Color plastic = new Color(0.12f, 0.12f, 0.13f), cloth = new Color(0.56f, 0.53f, 0.47f), clothDark = new Color(0.42f, 0.40f, 0.35f);
        Part(PrimitiveType.Cube, mopHead, new Vector3(0f, 0.048f, 0f), new Vector3(0.045f, 0.05f, 0.045f), plastic);      // шарнир
        Part(PrimitiveType.Cube, mopHead, new Vector3(0f, 0.03f, 0f), new Vector3(0.40f, 0.03f, 0.075f), plastic);        // планка
        Part(PrimitiveType.Cube, mopHead, new Vector3(0f, 0.01f, 0f), new Vector3(0.44f, 0.018f, 0.24f), cloth);           // тряпка
        System.Random r = new System.Random(5);
        for (int i = 0; i < 16; i++)                                                                                       // бахрома спереди и сзади
        {
            float x = -0.2f + (i % 8) * 0.057f + (float)r.NextDouble() * 0.02f;
            float z = (i < 8 ? 1f : -1f) * (0.12f + (float)r.NextDouble() * 0.03f);
            GameObject st = Part(PrimitiveType.Cube, mopHead, new Vector3(x, 0.004f, z), new Vector3(0.022f, 0.006f, 0.07f), (i % 3 == 0) ? clothDark : cloth);
            st.transform.localRotation = Quaternion.Euler(0f, (float)(r.NextDouble() - 0.5) * 30f, 0f);
        }
    }

    GameObject MakeTape(Transform parent)
    {
        GameObject root = new GameObject("Held");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;   // точка держания уже в центре ладони
        Part(PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(0.075f, 0.075f, 0.035f), new Color(0.95f, 0.75f, 0.1f));
        Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0, 0.019f), new Vector3(0.045f, 0.002f, 0.045f), new Color(0.1f, 0.1f, 0.1f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Part(PrimitiveType.Cube, root.transform, new Vector3(0, -0.03f, 0.03f), new Vector3(0.025f, 0.002f, 0.05f), new Color(0.9f, 0.85f, 0.3f));
        return root;
    }

    GameObject MakeFlashlight(Transform parent)
    {
        GameObject root = new GameObject("HeldLight");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0, 0, 0.06f);
        Part(PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(0.035f, 0.07f, 0.035f), new Color(0.08f, 0.08f, 0.09f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        GameObject lens = Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0, 0.072f), new Vector3(0.042f, 0.004f, 0.042f), new Color(1f, 0.95f, 0.8f));
        lens.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Material lm = lens.GetComponent<Renderer>().material;
        lm.EnableKeyword("_EMISSION");
        lm.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.7f) * 2f);
        return root;
    }

    static GameObject Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 size, Color c)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        Destroy(g.GetComponent<Collider>());
        g.name = "Visual";     // не «Cube»: TextureNoStretch и ModelSwap такие не трогают
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        Renderer r = g.GetComponent<Renderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(sh != null ? sh : Shader.Find("Standard"));
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }
}
