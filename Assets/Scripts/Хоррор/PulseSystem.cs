using UnityEngine;

// Пульс и дыхание (акт 3).
// Пульс растёт, когда Володя близко, и падает, когда вы спокойны.
// V — «Дыхание» (как рыбалка в Stardew Valley): держи ПРОБЕЛ, чтобы белая черта была в гуляющей зелёной зоне.
//   В зоне шкала спокойствия растёт, вне зоны — убывает. Полная — успокоился (30 с пульс растёт медленно).
//   Пустая — дыхание сбилось, Володя подходит на 3 м.
// Свой звук сердца положите в Assets/Resources под именем heartbeat.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class PulseSystem : MonoBehaviour
{
    public static PulseSystem Instance;

    // Мини-игра «дыхание» (как рыбалка в Stardew Valley):
    // держи ПРОБЕЛ — отметка поднимается, отпустишь — падает. Держи её в зелёной зоне, зона сама гуляет.
    // Пока отметка в зоне, шкала спокойствия заполняется; вне зоны — убывает. Заполнилась — успокоился.
    const float ZoneSize = 0.24f;      // высота зелёной зоны (доля шкалы)
    const float FillTime = 6f;         // сколько секунд в зоне нужно, чтобы успокоиться
    const float CalmTime = 30f;        // после успокоения пульс ещё столько секунд растёт медленно
    float marker, markerVel;           // отметка вдоха (0 — низ, 1 — верх)
    float zone = 0.4f, zoneTarget = 0.4f, nextZoneMove;
    float calm;                        // шкала спокойствия 0..1
    float calmUntil;
    float zoneVel;

    bool breathing;
    string result = "";
    float resultUntil;
    float panic;
    float savedSpeed;
    PlayerController pc;
    MonsterController monster;
    float nextFind;
    bool inDark;           // рядом ни одной горящей лампы и фонарик выключен
    float nextDark;
    Light[] lights;
    AudioSource heart;
    GUIStyle bigStyle, midStyle, smallStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Instance = new GameObject("PulseSystem").AddComponent<PulseSystem>();
    }

    void Awake()
    {
        heart = gameObject.AddComponent<AudioSource>();
        heart.loop = true;
        heart.playOnAwake = false;
        heart.clip = Resources.Load<AudioClip>("heartbeat");
    }

    void Update()
    {
        if (!GameConfig.horror) { Inventory.pulse = 70f; if (heart.isPlaying) heart.Stop(); return; }
        if (ActController.Act != 3)
        {
            if (heart.isPlaying) heart.Stop();
            return;
        }

        if (monster == null && Time.time >= nextFind)
        {
            nextFind = Time.time + 1f;
            monster = MonsterController.Find();
        }

        float dist = 999f;
        if (monster != null && monster.gameObject.activeInHierarchy) dist = monster.DistanceToPlayer();

        float fear = Mathf.Clamp01((35f - dist) / 35f) * 60f;
        if (breathing) fear *= 0.4f;

        if (Time.time >= nextDark) { nextDark = Time.time + 0.5f; inDark = CheckDark(); }

        float target = 70f + fear + panic;
        if (Inventory.flashlightOn) target += 4f;
        // В темноте страшнее: пульс выше и растёт быстрее (в укрытии чуть меньше, иначе прятаться было бы нельзя)
        if (inDark) target += HidingSpot.Current != null ? 4f : 10f;
        if (Inventory.Coffee) target -= 12f;   // кофе: сердце спокойнее
        target = Mathf.Max(55f, target);
        // Пульс растёт плавно (за пару секунд не взлетает), падает быстрее.
        // Сразу после успокоения страх действует слабее и пульс поднимается совсем медленно.
        bool calmed = Time.time < calmUntil;
        if (calmed) target = Mathf.Min(target, 70f + fear * 0.5f + Mathf.Max(0f, panic));
        float rate;
        if (target > Inventory.pulse) rate = calmed ? 1.2f : (inDark ? 5f : 3.5f);
        else rate = 8f;
        Inventory.pulse = Mathf.MoveTowards(Inventory.pulse, target, rate * Time.deltaTime);
        panic = Mathf.MoveTowards(panic, 0f, 1.5f * Time.deltaTime);

        UpdateHeartAudio();

        if (Input.GetKeyDown(KeyCode.V))
        {
            if (breathing) StopBreathing(); else StartBreathing();
        }
        if (breathing) UpdateBreathing();
    }

    bool CheckDark()
    {
        if (Inventory.flashlightOn) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;
        lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        Vector3 p = cam.transform.position;
        foreach (Light l in lights)
        {
            if (l == null || !l.enabled || !l.gameObject.activeInHierarchy || l.intensity < 0.05f) continue;
            if (l.type == LightType.Directional) { if (l.intensity > 0.3f) return false; continue; }
            if ((l.transform.position - p).sqrMagnitude < l.range * l.range * 0.45f) return false;
        }
        return true;
    }

    void UpdateHeartAudio()
    {
        if (heart.clip == null) return;
        if (!heart.isPlaying) heart.Play();
        heart.pitch = Mathf.Clamp(Inventory.pulse / 70f, 0.7f, 2f);
        heart.volume = Mathf.Clamp01((Inventory.pulse - 60f) / 50f + 0.15f);
    }

    void StartBreathing()
    {
        breathing = true;
        marker = 0.2f; markerVel = 0f;
        zone = 0.35f; zoneTarget = 0.35f; nextZoneMove = 0f;
        calm = 0.25f;
        result = "";

        GameObject p = GameObject.Find("Player");
        if (p != null)
        {
            pc = p.GetComponent<PlayerController>();
            if (pc != null)
            {
                savedSpeed = pc.speed;
                pc.speed = 0f;      // во время дыхания стоим на месте
            }
        }
    }

    void StopBreathing()
    {
        breathing = false;
        if (pc != null && savedSpeed > 0f) pc.speed = savedSpeed;
    }

    // Вызывается при смерти
    public void ForceExit()
    {
        if (breathing) StopBreathing();
    }

    void UpdateBreathing()
    {
        float dt = Time.deltaTime;

        // Отметка: пробел тянет вверх, без пробела падает (с инерцией, как поплавок)
        if (Input.GetKey(KeyCode.Space)) markerVel += 1.5f * dt;
        else markerVel -= 1.25f * dt;
        markerVel *= 1f - Mathf.Min(1f, 1.4f * dt);          // плавное торможение, без рывков
        markerVel = Mathf.Clamp(markerVel, -0.75f, 0.75f);
        marker += markerVel * dt;
        if (marker < 0f) { marker = 0f; markerVel = Mathf.Max(0f, markerVel) * 0.2f; }
        if (marker > 1f) { marker = 1f; markerVel = Mathf.Min(0f, markerVel) * 0.2f; }

        // Зелёная зона гуляет: чем выше пульс, тем резче и чаще
        float nerv = Mathf.Clamp01((Inventory.pulse - 70f) / 60f);
        if (Time.time >= nextZoneMove)
        {
            zoneTarget = Random.Range(0f, 1f - ZoneSize);
            nextZoneMove = Time.time + Random.Range(1.4f, 2.8f) * (1.2f - nerv * 0.4f);
        }
        zone = Mathf.SmoothDamp(zone, zoneTarget, ref zoneVel, 1.1f - nerv * 0.4f, 0.35f);   // зона плывёт мягко

        // Шкала спокойствия
        bool inZone = marker >= zone && marker <= zone + ZoneSize;
        calm += (inZone ? 1f / FillTime : -1f / (FillTime * 1.4f)) * dt;
        if (inZone) Inventory.pulse = Mathf.MoveTowards(Inventory.pulse, 60f, 3f * dt);

        if (calm >= 1f)
        {
            Inventory.pulse = 62f;
            panic = -15f;
            calmUntil = Time.time + CalmTime;
            Hud.Say("Сердце успокоилось.", 4f);
            StopBreathing();
        }
        else if (calm <= 0f)
        {
            // Сбился: громкий вдох, Володя слышит
            calm = 0.25f;
            panic = Mathf.Min(60f, panic + 10f);
            Inventory.pulse += 8f;
            if (monster != null) monster.Approach(3f);
            Say("Дыхание сбилось. Он услышал.");
        }
    }

    void Say(string text)
    {
        result = text;
        resultUntil = Time.time + 1.6f;
    }

    string StepsText()
    {
        if (monster == null || !monster.gameObject.activeInHierarchy) return "Тихо…";
        float d = monster.DistanceToPlayer();
        if (d < 4f) return "Шаги совсем рядом!";
        if (d < 10f) return "Шаги близко.";
        if (d < 25f) return "Шаги где-то рядом.";
        return "Тихо…";
    }

    void OnGUI()
    {
        if (!breathing) return;
        GUI.depth = -150;

        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(UITheme.Label());
            bigStyle.fontSize = 40;
            bigStyle.fontStyle = FontStyle.Bold;
            bigStyle.alignment = TextAnchor.MiddleCenter;
            bigStyle.normal.textColor = UITheme.Text;

            midStyle = new GUIStyle(bigStyle);
            midStyle.fontSize = 26;
            midStyle.fontStyle = FontStyle.Normal;

            smallStyle = new GUIStyle(midStyle);
            smallStyle.fontSize = 18;
            smallStyle.normal.textColor = new Color(0.7f, 0.7f, 0.75f);
        }

        // Затемнение экрана
        GUI.color = new Color(0f, 0f, 0f, 0.93f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // Вертикальная шкала дыхания (как в рыбалке) и рядом шкала спокойствия
        float bh = Mathf.Min(420f, Screen.height * 0.5f), bw = 54f;
        Rect bar = new Rect(Screen.width / 2f - bw - 14f, Screen.height * 0.5f - bh / 2f + 30f, bw, bh);
        GUI.color = new Color(0.16f, 0.16f, 0.18f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        bool inZone = marker >= zone && marker <= zone + ZoneSize;
        GUI.color = inZone ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.1f, 0.55f, 0.18f);
        GUI.DrawTexture(new Rect(bar.x + 4f, bar.yMax - bh * (zone + ZoneSize), bw - 8f, bh * ZoneSize), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(bar.x - 6f, bar.yMax - bh * marker - 3f, bw + 12f, 6f), Texture2D.whiteTexture);

        Rect cb = new Rect(Screen.width / 2f + 14f, bar.y, 18f, bh);
        GUI.color = new Color(0.16f, 0.16f, 0.18f);
        GUI.DrawTexture(cb, Texture2D.whiteTexture);
        float c = Mathf.Clamp01(calm);
        GUI.color = Color.Lerp(new Color(0.85f, 0.3f, 0.2f), new Color(0.3f, 0.85f, 0.4f), c);
        GUI.DrawTexture(new Rect(cb.x, cb.yMax - bh * c, cb.width, bh * c), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Тексты
        GUI.Label(new Rect(0, Screen.height * 0.12f, Screen.width, 60), "ДЫХАНИЕ", bigStyle);
        GUI.Label(new Rect(0, Screen.height * 0.12f + 56f, Screen.width, 30), "Держи ПРОБЕЛ, чтобы белая черта была в зелёной зоне.   V — выйти", smallStyle);
        GUI.Label(new Rect(0, bar.yMax + 14f, Screen.width, 30), StepsText(), smallStyle);
        if (Time.time < resultUntil)
            GUI.Label(new Rect(0, bar.yMax + 48f, Screen.width, 40), result, midStyle);
    }
}