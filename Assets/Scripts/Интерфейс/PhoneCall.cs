using System.Collections.Generic;
using UnityEngine;

// Звонки. Начальник не пишет SMS, а звонит.
//  - Звонит телефон (звук phone_ring), вверху справа «Входящий вызов · Начальник». T — ответить.
//  - Разговор: голос из Assets/Resources/Voice/<id> (.wav/.mp3/.ogg) с фильтром «как по телефону», внизу субтитры.
//    T ещё раз — положить трубку. Файла с голосом нет — звонок всё равно идёт, только субтитрами.
//  - Не ответил за 12 секунд — «пропущенный вызов», текст приходит в Сообщения.
//  - После разговора текст тоже остаётся в Сообщениях (без уведомления), чтобы перечитать.
//  - Во время охоты, в меню и в финале телефон не звонит: звонок ждёт.
// Из других скриптов: PhoneCall.Boss("boss_rolls", "Что говорит", "Что прийдёт SMS, если не ответил (необязательно)");
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class PhoneCall : MonoBehaviour
{
    public static PhoneCall Instance;
    public static bool Ringing { get { return Instance != null && Instance.state == State.Ringing; } }
    public static bool Talking { get { return Instance != null && Instance.state == State.Talking; } }

    public static KeyCode answerKey = KeyCode.T;
    const float RingSeconds = 12f;

    class Call { public string from, voice, spoken, sms; }

    enum State { Idle, Ringing, Talking }
    State state;
    readonly Queue<Call> queue = new Queue<Call>();
    Call cur;
    float stateTime, talkLength, nextRing;
    string[] lines;
    float[] lineStart;
    AudioSource voice, ring;
    GUIStyle subS, boxS, smallS;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Instance = new GameObject("PhoneCall").AddComponent<PhoneCall>();
    }

    public static void Boss(string voiceId, string spoken, string smsIfMissed = null)
    {
        Make("Начальник", voiceId, spoken, smsIfMissed);
    }

    public static void Make(string from, string voiceId, string spoken, string smsIfMissed = null)
    {
        if (Instance == null) { PhoneUI.AddMessage(from, smsIfMissed ?? spoken); return; }
        Instance.queue.Enqueue(new Call { from = from, voice = voiceId, spoken = spoken, sms = smsIfMissed ?? spoken });
    }

    void Awake()
    {
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        // «Телефонный» звук: срезаем низы и верхи, чуть грязи
        gameObject.AddComponent<AudioHighPassFilter>().cutoffFrequency = 350f;
        gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3400f;
        gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.12f;

        GameObject r = new GameObject("PhoneRing");
        r.transform.SetParent(transform, false);
        ring = r.AddComponent<AudioSource>();
        ring.playOnAwake = false;
        ring.loop = true;
        ring.spatialBlend = 0f;
        ring.volume = 0.7f;
    }

    bool CanRing()
    {
        if (GameMenus.IsBlocking || FinaleController.Locked || PhoneUI.Locked) return false;
        if (TapeMeasureScreen.IsOpen) return false;
        MonsterController m = MonsterController.Find();
        if (m != null && m.IsHunting) return false;
        if (HuntDirector.Warning) return false;
        return true;
    }

    void Update()
    {
        switch (state)
        {
            case State.Idle:
                if (queue.Count > 0 && Time.time >= nextRing && CanRing()) StartRing(queue.Dequeue());
                break;

            case State.Ringing:
                stateTime += Time.deltaTime;
                if (FinaleController.Locked) { Missed(); break; }
                if (Input.GetKeyDown(answerKey) && !GameMenus.IsBlocking) { Answer(); break; }
                if (stateTime > RingSeconds) Missed();
                break;

            case State.Talking:
                stateTime += Time.deltaTime;
                if (Input.GetKeyDown(answerKey) && !GameMenus.IsBlocking && stateTime > 0.3f) { HangUp(); break; }
                if (stateTime > talkLength || FinaleController.Locked) HangUp();
                break;
        }
    }

    void StartRing(Call c)
    {
        cur = c;
        state = State.Ringing;
        stateTime = 0f;
        AudioClip rc = Sfx.Get("phone_ring");
        if (rc != null) { ring.clip = rc; ring.Play(); }
        else Sfx.Play("phone_msg", 0.8f);
    }

    void Answer()
    {
        ring.Stop();
        Sfx.Play("phone_pickup", 0.8f);
        state = State.Talking;
        stateTime = 0f;

        AudioClip clip = Resources.Load<AudioClip>("Voice/" + cur.voice);
        lines = SplitLines(cur.spoken);
        lineStart = new float[lines.Length + 1];
        // Без записи — время по длине текста (~14 букв в секунду); с записью — по длине записи
        float total = clip != null ? clip.length + 0.4f : Mathf.Max(2.5f, cur.spoken.Length / 14f + 1f);
        int chars = 0;
        foreach (string l in lines) chars += l.Length;
        float t = 0.25f;
        for (int i = 0; i < lines.Length; i++)
        {
            lineStart[i] = t;
            t += (total - 0.25f) * lines[i].Length / Mathf.Max(1, chars);
        }
        lineStart[lines.Length] = total;
        talkLength = total;
        if (clip != null) { voice.clip = clip; voice.PlayDelayed(0.25f); }
    }

    void HangUp()
    {
        voice.Stop();
        Sfx.Play("phone_hangup", 0.8f);
        PhoneUI.AddMessage(cur.from, "Звонок: " + cur.sms, true);
        End();
    }

    void Missed()
    {
        ring.Stop();
        PhoneUI.AddMessage(cur.from, "Пропущенный вызов. " + cur.sms);
        Hud.Say("Пропущенный вызов: " + cur.from + " · Tab — телефон", 4f);
        End();
    }

    void End()
    {
        state = State.Idle;
        cur = null;
        nextRing = Time.time + 2.5f;    // следующий звонок не сразу
    }

    // Субтитры по предложениям
    static string[] SplitLines(string text)
    {
        List<string> res = new List<string>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            bool end = (ch == '.' || ch == '!' || ch == '?' || ch == '…') && (i + 1 >= text.Length || text[i + 1] == ' ');
            if (!end) continue;
            string s = text.Substring(start, i + 1 - start).Trim();
            if (s.Length > 0) res.Add(s);
            start = i + 1;
        }
        string rest = text.Substring(start).Trim();
        if (rest.Length > 0) res.Add(rest);
        // короткие склеиваем с соседями, чтобы не мелькали
        for (int i = res.Count - 1; i > 0; i--)
            if (res[i].Length < 18 || res[i - 1].Length < 18) { if (res[i - 1].Length + res[i].Length < 110) { res[i - 1] += " " + res[i]; res.RemoveAt(i); } }
        return res.ToArray();
    }

    void OnGUI()
    {
        if (state == State.Idle || cur == null) return;
        if (GameMenus.IsBlocking) return;
        if (subS == null)
        {
            subS = new GUIStyle(UITheme.Label()) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            boxS = new GUIStyle(UITheme.Label()) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
            smallS = new GUIStyle(UITheme.Label()) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        }
        float sw = Screen.width, sh = Screen.height;
        Rect panel = new Rect(sw - 400f, 60f, 380f, 74f);
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (state == State.Ringing)
        {
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 9f);
            GUI.color = new Color(1f, 0.82f, 0.45f, pulse);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 6f, 360f, 32f), "Входящий вызов · " + cur.from, boxS);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 16f, panel.y + 40f, 360f, 26f), answerKey + " — ответить", smallS);
            return;
        }

        int sec = Mathf.FloorToInt(stateTime);
        GUI.Label(new Rect(panel.x + 16f, panel.y + 6f, 360f, 32f), cur.from + " · " + (sec / 60) + ":" + (sec % 60).ToString("00"), boxS);
        GUI.Label(new Rect(panel.x + 16f, panel.y + 40f, 360f, 26f), answerKey + " — положить трубку", smallS);

        // Субтитры
        int k = 0;
        while (k + 1 < lines.Length && stateTime >= lineStart[k + 1]) k++;
        if (stateTime < lineStart[0]) return;
        string s = cur.from + ": " + lines[k];
        Rect r = new Rect(sw * 0.15f, sh - 170f, sw * 0.7f, 70f);
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), s, subS);
        GUI.color = new Color(1f, 0.93f, 0.8f);
        GUI.Label(r, s, subS);
        GUI.color = Color.white;
    }
}
