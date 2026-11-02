using System.Collections;
using UnityEngine;

// Володя.
// - В акте 2 один раз пробегает по стене завода (двигает его ActController).
// - В акте 3 он НЕ гонится постоянно, а живёт волнами:
//     * Дома (в складе слева) он спит. Пока вы спокойны и без света, тревога падает.
//     * Свет фонарика и высокий пульс копят «тревогу». Когда она выше порога, он выходит на охоту.
//     * Охота длится не дольше maxHuntSeconds. Если вы затихли, он теряет интерес и уходит домой.
//     * После ухода он не выходит cooldownSeconds секунд.
// - Вплотную: чёрный экран («смерть»), через несколько секунд вы просыпаетесь у входа.
// Проходит сквозь стены: это сделано специально, чтобы он не застревал.
// Свои звуки положите в Assets/Resources: monster_steps (шаги).
public class MonsterController : MonoBehaviour
{
    public enum Mode { Hidden, Scripted, Hunt }
    enum Phase { Away, Hunting, Retreating }

    // Находит Володю на сцене, даже если он выключен
    public static MonsterController Find()
    {
        MonsterController[] all = Resources.FindObjectsOfTypeAll<MonsterController>();
        for (int i = 0; i < all.Length; i++)
            if (all[i].gameObject.scene.IsValid()) return all[i];
        return null;
    }

    public Mode mode = Mode.Hidden;
    public bool scriptRun;                       // в сценах (скример) модель бежит, а не застывает
    public float baseSpeed = 2.0f;               // м/с, когда он что-то слышит
    public float killDistance = 1.3f;

    // «Характер»: меняйте числа, чтобы сделать его реже или чаще
    public float alertToHunt = 60f;              // с какой тревоги он выходит на охоту (0..100)
    public float lightAlertPerSecond = 7f;       // сколько тревоги даёт включённый фонарик за секунду
    public float heartAlertPerSecond = 0.35f;    // тревога за каждый удар пульса выше 90
    public float alertDecayPerSecond = 5f;       // как быстро он успокаивается
    public float maxHuntSeconds = 45f;           // самая долгая охота
    public float cooldownSeconds = 40f;          // пауза после ухода
    public float idleAlertPerSecond = 1.3f;      // в тишине тревога всё равно растёт: он выходит искать вас примерно раз в минуту
    public float senseEvery = 4f;                // как часто он «чует», где вы, когда не слышит (раз в N секунд, с ошибкой)
    public float senseError = 4f;                // ошибка чутья, м
    public float sightRange = 16f;               // на каком расстоянии он замечает вас глазами (если ничто не загораживает)
    public float chaseSpeed = 4.3f;              // скорость погони, когда видит вас (вы бегаете со скоростью 4)
    public float searchSeconds = 12f;            // сколько он ищет вокруг места, где потерял вас

    Vector3 home;
    Vector3 lastHeard;
    Transform player;
    AudioSource steps;
    AudioClip stepClip;
    float stepTimer;
    Phase phase = Phase.Away;
    float alert = 40f;
    float huntTimer, calmTimer, cooldown, senseTimer, searchTimer, lastSeenTime = -99f;
    bool chasing;
    bool dying;

    void Awake()
    {
        home = transform.position;
        steps = gameObject.AddComponent<AudioSource>();
        steps.spatialBlend = 1f;
        steps.maxDistance = 40f;
        steps.rolloffMode = AudioRolloffMode.Linear;
        stepClip = Resources.Load<AudioClip>("monster_steps");
    }

    bool FindPlayer()
    {
        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }
        return player != null;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    public float DistanceToPlayer()
    {
        if (!FindPlayer()) return 999f;
        return Vector3.Distance(Flat(player.position), Flat(transform.position));
    }

    // Делает шаг к игроку на указанное число метров (за ошибки в дыхании)
    public void Approach(float meters)
    {
        if (!FindPlayer() || mode != Mode.Hunt) return;
        Vector3 dir = Flat(player.position) - Flat(transform.position);
        if (dir.magnitude < 0.1f) return;
        transform.position += dir.normalized * Mathf.Min(meters, dir.magnitude);
        lastHeard = player.position;
        if (phase == Phase.Away)
        {
            phase = Phase.Hunting;      // шум разбудил его
            huntTimer = 0f;
            calmTimer = 0f;
        }
    }

    // Стоит дома (в складе с монстром) и ничего не делает. Так он ждёт с самого начала игры.
    public void ReturnHome()
    {
        if (!GameConfig.horror) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);
        mode = Mode.Hidden;
        transform.position = home;
        transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // лицом к воротам
    }

    public void BeginHunt()
    {
        if (!GameConfig.horror) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);
        mode = Mode.Hunt;
        phase = Phase.Away;
        dying = false;
        transform.position = home;
        alert = 50f;       // при первом же свете он быстро заинтересуется
        cooldown = 0f;
        FindPlayer();
    }

    void Update()
    {
        if (!GameConfig.horror) { if (gameObject.activeSelf) gameObject.SetActive(false); return; }
        if (mode != Mode.Hunt || dying || !FindPlayer()) return;
        float dt = Time.deltaTime;

        // Тревога: свет и пульс копят её, тишина снижает
        float gain = 0f;
        if (Inventory.flashlightOn) gain += lightAlertPerSecond;
        if (Inventory.pulse > 90f) gain += (Inventory.pulse - 90f) * heartAlertPerSecond;
        if (gain > 0f) alert += gain * dt;
        else if (phase == Phase.Away) alert += idleAlertPerSecond * dt;      // сидит у себя и всё равно начинает искать
        else alert -= alertDecayPerSecond * dt;
        alert = Mathf.Clamp(alert, 0f, 100f);
        cooldown -= dt;

        if (phase == Phase.Away)
        {
            if (alert >= alertToHunt && cooldown <= 0f)
            {
                phase = Phase.Hunting;
                huntTimer = 0f;
                calmTimer = 0f;
                lastHeard = player.position;
            }
        }
        else if (phase == Phase.Hunting)
        {
            UpdateHunt(dt);
        }
        else
        {
            UpdateRetreat(dt);
        }
    }

    // Охота. Он идёт туда, где слышал вас (свет, сердце) или где чует. Если видит — бежит.
    // Убежать от него нельзя (он чуть быстрее), можно только спрятаться: E у паллеты или у мусорного контейнера.
    void UpdateHunt(float dt)
    {
        float dist = DistanceToPlayer();
        bool hidden = HidingSpot.Current != null;

        bool hearsLight = Inventory.flashlightOn && dist < 60f;                  // свет видно даже из укрытия
        bool hearsHeart = Inventory.pulse > 95f && dist < (hidden ? 4f : 45f);  // в укрытии сердце слышно только вплотную
        bool sees = !hidden && dist < sightRange && CanSee();
        if (sees) lastSeenTime = Time.time;

        if (hearsLight || hearsHeart || sees)
        {
            lastHeard = player.position;
            searchTimer = 0f;
        }
        else if (!hidden)
        {
            // Не слышит и не видит — но время от времени чует, примерно где вы
            senseTimer -= dt;
            if (senseTimer <= 0f)
            {
                senseTimer = senseEvery;
                Vector2 e = Random.insideUnitCircle * senseError;
                lastHeard = player.position + new Vector3(e.x, 0f, e.y);
            }
        }

        // Видел, как вы прятались (гнался и был рядом) — знает, где вы
        bool sawHide = hidden && chasing && HidingSpot.EnteredAt - lastSeenTime < 1.5f && dist < 6f;
        if (sawHide) lastHeard = player.position;
        chasing = sees || (chasing && Time.time - lastSeenTime < 2f);

        float speed = baseSpeed;
        if (chasing) speed = chaseSpeed;
        else if (hearsLight) speed *= 2f;                                   // свет — приманка
        else if (hearsHeart) speed *= 1f + (Inventory.pulse - 95f) / 60f;   // сердце — маяк
        else speed *= 0.8f;

        float left = MoveFlat(lastHeard, speed, dt);
        MatchHeight(player.position.y - 1f, dt);
        Footsteps(dist, dt);

        // Пришёл на место, а вас нет — ищет вокруг, потом уходит
        if (left < 0.6f && !hearsLight && !hearsHeart && !sees && !sawHide)
        {
            searchTimer += dt;
            Vector2 r = Random.insideUnitCircle * 4f;
            lastHeard += new Vector3(r.x, 0f, r.y) * 0.5f;
        }

        huntTimer += dt;
        if (alert < 10f) calmTimer += dt; else calmTimer = 0f;
        if (huntTimer > maxHuntSeconds || calmTimer > 6f || searchTimer > searchSeconds) { phase = Phase.Retreating; chasing = false; }

        // Поймал: если вы не прячетесь, или выдали себя светом/сердцем, или он видел, как вы прятались
        bool caught = !hidden || Inventory.flashlightOn || Inventory.pulse > GameConfig.hidePulseLimit || sawHide;
        if (dist < killDistance && caught) StartCoroutine(Death());
    }

    // Видит ли он игрока: ничто не загораживает от его головы до камеры
    bool CanSee()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 eye = transform.position + Vector3.up * 2.2f;
        Vector3 target = cam.transform.position;
        RaycastHit hit;
        if (!Physics.Linecast(eye, target, out hit, ~0, QueryTriggerInteraction.Ignore)) return true;
        return hit.collider.transform.root == player.root;
    }

    void UpdateRetreat(float dt)
    {
        float left = MoveFlat(home, baseSpeed * 1.5f, dt);
        MatchHeight(home.y, dt);
        Footsteps(DistanceToPlayer(), dt);

        if (left < 0.5f)
        {
            phase = Phase.Away;
            alert = 15f;
            cooldown = cooldownSeconds;
        }
    }

    // Идёт к точке по земле, возвращает оставшееся расстояние
    float MoveFlat(Vector3 target, float speed, float dt)
    {
        Vector3 toTarget = Flat(target) - Flat(transform.position);
        float left = toTarget.magnitude;
        if (left > 0.4f)
        {
            Vector3 dir = toTarget / left;
            transform.position += dir * speed * dt;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
        return left;
    }

    void MatchHeight(float y, float dt)
    {
        Vector3 p = transform.position;
        p.y = Mathf.MoveTowards(p.y, y, 2f * dt);
        transform.position = p;
    }

    void Footsteps(float dist, float dt)
    {
        if (stepClip == null) return;
        stepTimer -= dt;
        if (stepTimer > 0f) return;
        steps.PlayOneShot(stepClip, Mathf.Clamp01(1.2f - dist / 40f));
        stepTimer = Mathf.Lerp(0.35f, 0.9f, Mathf.Clamp01(dist / 40f));
    }

    IEnumerator Death()
    {
        dying = true;
        HidingSpot.ForceExit();
        if (PulseSystem.Instance != null) PulseSystem.Instance.ForceExit();
        CenterText.Show("ОН ТЕБЯ НАШЁЛ", 4f, true);
        yield return new WaitForSeconds(4f);
        Respawn();
    }

    void Respawn()
    {
        GameObject p = GameObject.Find("Player");
        if (p != null)
        {
            CharacterController cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            p.transform.position = new Vector3(0f, 1.05f, -9f);
            p.transform.rotation = Quaternion.identity;
            if (cc != null) cc.enabled = true;
        }

        // Если игрок в момент смерти качал рохлю — отпускаем паллету и возвращаем управление
        PalletCargo.CancelLifting();
        if (p != null)
        {
            PlayerController pc = p.GetComponent<PlayerController>();
            if (pc != null && !GameMenus.IsBlocking && !PhoneUI.IsOpen) pc.enabled = true;
        }

        Inventory.pulse = 70f;
        transform.position = home;
        phase = Phase.Away;
        alert = 0f;
        cooldown = cooldownSeconds;
        dying = false;
    }
}