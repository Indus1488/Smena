using System.Collections.Generic;
using UnityEngine;

// 4 лампы на фасаде полосатого центрального склада (CentralWarehouse).
// Стоят с самого начала, но не светят. В 17:30 загораются, в 18:00 Володя разбивает их (ActController).
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class CentralLamps : MonoBehaviour
{
    public static CentralLamps Instance;
    public static readonly List<Transform> Lamps = new List<Transform>();

    bool on;
    readonly HashSet<Transform> broken = new HashSet<Transform>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Instance = new GameObject("CentralLamps").AddComponent<CentralLamps>();
    }

    void Start()
    {
        Lamps.Clear();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == "FacadeLamp") Lamps.Add(t);
        foreach (Transform t in Lamps) SetLit(t, false);
    }

    void Update()
    {
        if (!on && ActController.Act == 1 && Hud.minutes >= 17.5f * 60f) TurnOn();
    }

    public void TurnOn()
    {
        if (on) return;
        on = true;
        foreach (Transform t in Lamps)
            if (!broken.Contains(t)) SetLit(t, true);
    }

    public void Smash(Transform lamp)
    {
        if (lamp == null || broken.Contains(lamp)) return;
        broken.Add(lamp);
        SetLit(lamp, false);
        Sfx.PlayAt("lamp_break", lamp.position, 1f);
    }

    public void SmashAll()
    {
        foreach (Transform t in Lamps) Smash(t);
    }

    // Свет и свечение плафона
    static void SetLit(Transform lamp, bool lit)
    {
        Light l = lamp.GetComponentInChildren<Light>(true);
        if (l != null) l.enabled = lit;

        Transform h = lamp.Find("Housing");
        Renderer r = h != null ? h.GetComponent<Renderer>() : null;
        if (r == null) return;
        Material m = r.material;      // своя копия материала у каждой лампы
        if (lit)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.5f) * 3f);
        }
        else
        {
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
        }
    }
}
