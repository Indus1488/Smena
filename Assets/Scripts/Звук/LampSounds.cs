using System.Collections.Generic;
using UnityEngine;

// Звуки ламп (всех: в складах, на фасаде центрального склада, на заводе):
//   lamp_hum     — гудение, пока лампа горит (по кругу, слышно в паре метров; тише, когда лампа тускнеет)
//   lamp_on      — щелчок/разгорание, когда лампа включилась
//   lamp_off     — щелчок, когда выключилась
//   lamp_flicker — треск при мигании (мигание = лампа быстро гаснет и загорается)
//   lamp_break   — разбилась (это уже было, звучит из CentralLamps)
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class LampSounds : MonoBehaviour
{
    class Lamp
    {
        public Light light; public float baseI; public bool lit; public float lastToggle = -99f, lastClick = -99f;
        public AudioSource hum;
    }

    readonly List<Lamp> lamps = new List<Lamp>();
    float nextScan;
    AudioClip humClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("LampSounds").AddComponent<LampSounds>();
    }

    void Start()
    {
        humClip = Sfx.Get("lamp_hum");
    }

    static bool IsLamp(Light l)
    {
        if (l.type != LightType.Point) return false;
        for (Transform t = l.transform; t != null; t = t.parent)
        {
            string n = t.name;
            if (n == "ElectricPanel" || n == "Player" || n == "Main Camera") return false;
            if (n == "InteriorLights" || n == "FacadeLamp" || n == "FactoryLamp" || n == "Lamp") return true;
        }
        return false;
    }

    void Scan()
    {
        HashSet<Light> known = new HashSet<Light>();
        foreach (Lamp x in lamps) known.Add(x.light);
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (known.Contains(l) || !IsLamp(l)) continue;
            Lamp x = new Lamp { light = l, baseI = Mathf.Max(0.01f, l.intensity), lit = On(l, Mathf.Max(0.01f, l.intensity)) };
            if (humClip != null)
            {
                x.hum = l.gameObject.AddComponent<AudioSource>();
                x.hum.clip = humClip;
                x.hum.loop = true;
                x.hum.spatialBlend = 1f;
                x.hum.rolloffMode = AudioRolloffMode.Linear;
                x.hum.minDistance = 1f;
                x.hum.maxDistance = 9f;
                x.hum.volume = 0f;
                x.hum.time = Random.Range(0f, humClip.length);
                x.hum.Play();
            }
            lamps.Add(x);
        }
    }

    static bool On(Light l, float baseI) { return l != null && l.enabled && l.gameObject.activeInHierarchy && l.intensity > baseI * 0.35f; }

    void Update()
    {
        if (Time.time >= nextScan) { nextScan = Time.time + 3f; Scan(); }
        for (int i = lamps.Count - 1; i >= 0; i--)
        {
            Lamp x = lamps[i];
            if (x.light == null) { lamps.RemoveAt(i); continue; }
            bool now = On(x.light, x.baseI);
            if (x.hum != null)
            {
                float k = now ? Mathf.Clamp01(x.light.intensity / x.baseI) : 0f;
                x.hum.volume = Mathf.MoveTowards(x.hum.volume, 0.25f * k, Time.deltaTime * 2f);
            }
            if (now == x.lit) continue;
            bool quick = Time.time - x.lastToggle < 1.5f;
            x.lit = now;
            x.lastToggle = Time.time;
            if (Time.time - x.lastClick < 0.12f) continue;
            x.lastClick = Time.time;
            string snd = quick ? "lamp_flicker" : (now ? "lamp_on" : "lamp_off");
            if (Sfx.Get(snd) == null && quick) snd = now ? "lamp_on" : "lamp_off";
            Sfx.PlayAt(snd, x.light.transform.position, 0.8f);
        }
    }
}
