using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// «Ощущение камеры»:
// - угол обзора 75°, при беге на Shift расширяется;
// - покачивание головы при ходьбе (сильнее при беге);
// - при страхе (пульс выше 90) камера «дышит», края экрана расходятся цветом (хроматическая аберрация).
// Камера кладётся в промежуточный объект CameraRig: покачивание двигает его, а остальные скрипты
// (тряски, укрытия, скример) по-прежнему работают с самой камерой.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class CameraFeel : MonoBehaviour
{
    public static CameraFeel Instance;

    public float baseFov = 75f;
    public float sprintFovAdd = 8f;
    public float bobWalk = 0.035f;     // высота покачивания при ходьбе, м
    public float bobSprint = 0.06f;

    Camera cam;
    Transform rig, player;
    CharacterController cc;
    Vector3 lastPos;
    float bobPhase, moveBlend, fear;
    Volume volume;
    ChromaticAberration ca;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Instance = new GameObject("CameraFeel").AddComponent<CameraFeel>();
    }

    void Start()
    {
        cam = Camera.main;
        if (cam == null) { enabled = false; return; }
        player = cam.transform.parent;
        if (player == null) { enabled = false; return; }
        cc = player.GetComponent<CharacterController>();

        // Промежуточный объект: стоит там, где была камера; камера — внутри него в нуле
        rig = new GameObject("CameraRig").transform;
        rig.SetParent(player, false);
        rig.localPosition = cam.transform.localPosition;
        rig.localRotation = Quaternion.identity;
        cam.transform.SetParent(rig, false);
        cam.transform.localPosition = Vector3.zero;
        cam.fieldOfView = baseFov;
        lastPos = player.position;

        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 40f;
        VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();
        ca = p.Add<ChromaticAberration>(true);
        ca.intensity.Override(0f);
        volume.sharedProfile = p;
    }

    void LateUpdate()
    {
        if (rig == null || cam == null) return;
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        Vector3 d = player.position - lastPos; d.y = 0f;
        float speed = d.magnitude / dt;
        lastPos = player.position;

        bool locked = FinaleController.Locked || PlayerController.MoveLocked || Time.timeScale == 0f;
        bool grounded = cc == null || cc.isGrounded;
        bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float targetMove = (!locked && grounded && speed > 0.5f) ? 1f : 0f;
        moveBlend = Mathf.MoveTowards(moveBlend, targetMove, dt * 4f);

        // Покачивание головы
        bobPhase += dt * (speed > 0.5f ? 1.6f + speed * 1.15f : 0f);
        float amp = (sprint ? bobSprint : bobWalk) * moveBlend;
        Vector3 bob = new Vector3(Mathf.Cos(bobPhase) * amp * 0.5f, Mathf.Abs(Mathf.Sin(bobPhase)) * amp - amp * 0.5f, 0f);

        // Страх: дыхание камеры
        fear = Mathf.MoveTowards(fear, GameConfig.horror ? Mathf.Clamp01((Inventory.pulse - 90f) / 50f) : 0f, dt * 0.8f);
        float breath = Mathf.Sin(Time.time * (1.2f + fear * 2.5f)) * 0.012f * fear;
        // Выглядывание из укрытия: голова уходит вбок и наклоняется
        float peekM = HidingSpot.PeekMeters;                 // сдвиг уже ограничен стенами
        float peek = peekM / 0.9f;
        Vector3 peekOff = new Vector3(peekM, -Mathf.Abs(peek) * 0.05f, 0f);
        rig.localPosition = Vector3.Lerp(rig.localPosition, RigBase() + bob + Vector3.up * breath + peekOff, 1f);
        rig.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.7f) * 0.6f * fear, 0f, Mathf.Cos(bobPhase) * 0.4f * moveBlend + Mathf.Sin(Time.time * 1.1f) * 0.8f * fear - peek * 10f);

        // Угол обзора
        if (!FinaleController.Locked)
        {
            float fov = baseFov + (sprint && moveBlend > 0.5f ? sprintFovAdd : 0f) - fear * 4f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, dt * 6f);
        }

        // Хроматическая аберрация при страхе
        if (ca != null)
        {
            ca.intensity.Override(fear * 0.7f);
            if (fear > 0.01f)
            {
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
            }
        }
    }

    Vector3 rigBase;
    bool rigBaseSet;

    Vector3 RigBase()
    {
        if (!rigBaseSet) { rigBase = rig.localPosition; rigBaseSet = true; }
        return rigBase;
    }

    // Пульс как дрожь 0..1 — для рук и предметов в руках
    public static float Tremor
    {
        get { return GameConfig.horror ? Mathf.Clamp01((Inventory.pulse - 105f) / 45f) : 0f; }
    }
}
