using System.Collections.Generic;
using UnityEngine;

// Звуки игры. Все звуки — ваши записи: положите файлы в папку Assets/Resources
// с именами из списка ниже (расширение любое: .wav, .mp3, .ogg). Если файла нет, игра просто молчит.
//
//   step_concrete  — шаг по бетону/асфальту       tape_measure — рулетка
//   step_wood      — шаг по ОСБ внутри склада      sticker      — стикер на доску
//   door_open      — ворота склада открываются     scan         — проверка QR
//   door_close     — ворота закрываются            film_wrap    — обмотка плёнкой
//   pickup         — взять предмет                 jack_pump    — качнуть рохлю
//   sweep          — убрать мусор шваброй          money        — деньги пришли
//   phone_msg      — новое сообщение               flashlight   — щелчок фонарика
//   locked         — дверь заперта                 battery      — смена батареи
//   lamp_break     — Володя разбивает лампу        trash_bag    — мешок в мусорку
//   hide           — спрятаться в укрытие          monster_run  — Володя бежит в финале
//   jumpscare      — крик скримера (если нет — играет finale_scream)
//
// Плюс звуки, которые уже подключены раньше: gate_creak, heartbeat, monster_steps,
// finale_scream, gazelle_engine.
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

    // Звук в точке мира (тише издалека)
    public static void PlayAt(string name, Vector3 pos, float volume = 1f)
    {
        AudioClip c = Get(name);
        if (c != null) AudioSource.PlayClipAtPoint(c, pos, volume);
    }
}
