using UnityEngine;

// Пульс и дыхание (акт 3).
// Пульс растёт, когда Володя близко, и падает, когда вы спокойны.
// V — «Дыхание»: экран темнеет, появляется шкала. Удерживайте ПРОБЕЛ (вдох) и отпустите в зелёной зоне.
//   Передержали: громкий всхрап, Володя подходит на 4 м. Недодержали: пульс скачет, Володя подходит на 2 м.
//   Нужно набрать GameConfig.breathsNeeded ровных вдохов, чтобы успокоиться.
// Свой звук сердца положите в Assets/Resources под именем heartbeat.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class PulseSystem : MonoBehaviour
{
    public static PulseSystem Instance;

    const float GreenMin = 0.60f;
    const float GreenMax = 0.78f;

    bool breathing;
    float fill;
    bool holding;
    int good;
    string result = "";
    float resultUntil;
    float panic;
    float savedSpeed;
    PlayerController pc;
    MonsterController monster;
    float nextFind;
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

        float target = 70f + fear + panic;
        if (Inventory.flashlightOn) target += 4f;
        target = Mathf.Max(55f, target);
        Inventory.pulse = Mathf.MoveTowards(Inventory.pulse, target, 10f * Time.deltaTime);
        panic = Mathf.MoveTowards(panic, 0f, 1.5f * Time.deltaTime);

        UpdateHeartAudio();

        if (Input.GetKeyDown(KeyCode.V))
        {
            if (breathing) StopBreathing(); else StartBreathing();
        }
        if (breathing) UpdateBreathing();
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
        fill = 0f;
        holding = false;
        good = 0;
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
        holding = false;
        if (pc != null && savedSpeed > 0f) pc.speed = savedSpeed;
    }

    // Вызывается при смерти
    public void ForceExit()
    {
        if (breathing) StopBreathing();
    }

    void UpdateBreathing()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            holding = true;
            fill += 0.55f * Time.deltaTime;
            if (fill >= 1f) Evaluate();
        }
        else if (holding)
        {
            Evaluate();
        }
        else
        {
            fill = Mathf.MoveTowards(fill, 0f, 2f * Time.deltaTime);
        }
    }

    void Say(string text)
    {
        result = text;
        resultUntil = Time.time + 1.6f;
    }

    void Evaluate()
    {
        holding = false;
        float f = fill;
        fill = 0f;
        if (f < 0.05f) return;   // случайное касание

        if (f >= GreenMin && f <= GreenMax)
        {
            good++;
            panic = Mathf.Max(-30f, panic - 5f);
            Inventory.pulse = Mathf.Max(55f, Inventory.pulse - 5f);
            Say("Ровный вдох: " + good + "/" + GameConfig.breathsNeeded);

            if (good >= GameConfig.breathsNeeded)
            {
                Inventory.pulse = 62f;
                panic = 0f;
                Hud.Say("Вы успокоились.", 4f);
                StopBreathing();
            }
        }
        else if (f > GreenMax)
        {
            panic = Mathf.Min(60f, panic + 8f);
            Inventory.pulse += 6f;
            if (monster != null) monster.Approach(4f);
            Say("Слишком долго: громкий всхрап. Он услышал.");
        }
        else
        {
            panic = Mathf.Min(60f, panic + 8f);
            Inventory.pulse += 8f;
            if (monster != null) monster.Approach(2f);
            Say("Не хватило воздуха. Пульс скачет.");
        }
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
            bigStyle = new GUIStyle(GUI.skin.label);
            bigStyle.fontSize = 40;
            bigStyle.fontStyle = FontStyle.Bold;
            bigStyle.alignment = TextAnchor.MiddleCenter;
            bigStyle.normal.textColor = Color.white;

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

        // Шкала
        float w = 520f, h = 36f;
        Rect bar = new Rect((Screen.width - w) / 2f, Screen.height * 0.55f, w, h);
        GUI.color = new Color(0.2f, 0.2f, 0.22f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = new Color(0.1f, 0.6f, 0.15f);
        GUI.DrawTexture(new Rect(bar.x + w * GreenMin, bar.y, w * (GreenMax - GreenMin), h), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(bar.x + w * fill - 3f, bar.y - 8f, 6f, h + 16f), Texture2D.whiteTexture);

        // Тексты
        GUI.Label(new Rect(0, Screen.height * 0.30f, Screen.width, 60), "ДЫХАНИЕ  " + good + "/" + GameConfig.breathsNeeded, bigStyle);
        GUI.Label(new Rect(0, Screen.height * 0.40f, Screen.width, 30), "Удерживайте ПРОБЕЛ (вдох), отпустите в зелёной зоне.   V — выйти", smallStyle);
        if (Time.time < resultUntil)
            GUI.Label(new Rect(0, Screen.height * 0.67f, Screen.width, 40), result, midStyle);
    }
}