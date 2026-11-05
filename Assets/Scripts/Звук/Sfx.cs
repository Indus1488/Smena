using System.Collections.Generic;
using UnityEngine;

// Звуки игры. Все звуки — ваши записи: положите файлы в папку Assets/Resources
// с именами из списка ниже (расширение любое: .wav, .mp3, .ogg). Если файла нет, игра просто молчит.
// Где написано «_1…_4» — можно положить несколько вариантов (step_concrete_1, step_concrete_2…),
// игра берёт случайный. Можно и один файл без номера.
// Полный список с пояснениями — в документе «Смена — что озвучить».
//
// ИГРОК
//   step_concrete(_1…_4) — шаг по бетону/асфальту     step_wood(_1…_4) — шаг по ОСБ в складе
//   step_dirt(_1…_4)     — шаг по грунту/щебню двора
//   pickup — взять предмет    hide — спрятаться    flashlight — щелчок фонарика    battery — смена батареи
// РАБОТА
//   tape_out — вытянуть ленту рулетки    tape_in — лента уехала обратно    tape_measure — замер записан
//   scan — проверка QR    sticker — стикер на доску    film_wrap — обмотка плёнкой    sweep — убрать мусор
//   trash_bag — мешок в контейнер    money — деньги
//   jack_pump(_1…_5) — качок рохли    jack_lifted — паллета поднята    jack_drop — паллету опустили
// ГАЗЕЛЬ
//   gazelle_engine — мотор (по кругу, едет с машиной)    gazelle_brake — тормоз, остановилась
//   gazelle_door — дверь/борт    gazelle_load — паллеты грузят    gazelle_horn — гудок, уезжает
// ЛАМПЫ
//   lamp_hum — гудение горящей лампы (по кругу)    lamp_on — включилась    lamp_off — выключилась
//   lamp_flicker — треск при мигании    lamp_break — Володя разбивает лампу
// ТЕЛЕФОН И ИНТЕРФЕЙС
//   phone_ring — звонок (по кругу)   phone_pickup — взял трубку   phone_hangup — положил   phone_msg — SMS
//   phone_open / phone_close — достать и убрать телефон (Tab)
//   ui_hover — навёл на кнопку    ui_click — нажал    ui_back — «Назад»/«Закрыть»    ui_slider — ползунок
// ХОРРОР
//   door_open / door_close / locked — ворота склада    gate_creak — ворота центрального склада
//   panel_break — щиток    hunt_omen — гул перед охотой    radar_ping — пинг радара
//   monster_run / monster_steps — Володя    heartbeat — сердце    jumpscare / finale_scream — скример
//
// Голоса — папка Assets/Resources/Voice (звонки начальника boss_…, фразы селлеров seller_…).
//
// Вызов из кода: Sfx.Play("pickup");
public static class Sfx
{
    static Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        cache = new Dictionary<string, AudioClip>();
        source = null;
    }

    public static AudioClip Get(string name)
    {
        AudioClip c;
        if (!cache.TryGetValue(name, out c))
        {
            c = Resources.Load<AudioClip>(name);
            cache[name] = c;   // запоминаем и отсутствие, чтобы не искать каждый раз
        }
        return c;
    }

    // Звук «в голове» игрока (не зависит от расстояния)
    public static void Play(string name, float volume = 1f)
    {
        AudioClip c = Get(name);
        if (c == null) return;
        if (source == null)
        {
            GameObject go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        source.pitch = Random.Range(0.95f, 1.05f);   // лёгкий разброс, чтобы не звучало одинаково
        source.PlayOneShot(c, volume);
    }

    // Один из нескольких вариантов: name_1 … name_count (если вариантов нет — просто name)
    public static void PlayRandom(string name, int count, float volume = 1f)
    {
        string v = name + "_" + Random.Range(1, count + 1);
        if (Get(v) != null) Play(v, volume); else Play(name, volume);
    }

    // Звук по кругу, привязанный к объекту (мотор газели, гудение). Нет файла — null.
    public static AudioSource Loop(string name, Transform follow, float volume = 1f, float maxDistance = 30f)
    {
        AudioClip c = Get(name);
        if (c == null || follow == null) return null;
        AudioSource a = follow.gameObject.AddComponent<AudioSource>();
        a.clip = c; a.loop = true; a.volume = volume;
        a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 2f; a.maxDistance = maxDistance;
        a.Play();
        return a;
    }

    // Голос (папка Resources/Voice). В точке мира — если pos задан, иначе «в голове».
    public static bool Voice(string id, Vector3? pos = null, float volume = 1f)
    {
        AudioClip c = Get("Voice/" + id);
        if (c == null) return false;
        if (pos.HasValue) AudioSource.PlayClipAtPoint(c, pos.Value, volume);
        else Play("Voice/" + id, volume);
        return true;
    }

    // Звук в точке мира (тише издалека)
    public static void PlayAt(string name, Vector3 pos, float volume = 1f)
    {
        AudioClip c = Get(name);
        if (c != null) AudioSource.PlayClipAtPoint(c, pos, volume);
    }
}
