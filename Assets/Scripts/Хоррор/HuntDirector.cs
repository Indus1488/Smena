using System.Collections;
using UnityEngine;

// «Охоты» (так их зовём только мы, в игре слова нет).
// С наступления темноты Володя выходит не когда захочет, а по расписанию:
//   пауза (работай) → странное событие (мигает свет, гул, странная коробка у входа) → через несколько секунд он выходит →
//   охота ~40 с (прячься и дыши ровно) → уходит домой → снова пауза.
// В акте 3 паузы короче. Свет фонарика в паузе злит его — следующая охота наступает раньше.
// Первая охота объясняется SMS с незнакомого номера.
// Только хоррор-режим. Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class HuntDirector : MonoBehaviour
{
    public static HuntDirector Instance;
    public static bool Warning { get; private set; }   // идёт предупреждение (странное событие уже было, он вот-вот выйдет)

    float timer = -1f;
    bool wasHunting, firstDone;
    GameObject strangeBox;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Warning = false;
        Instance = new GameObject("HuntDirector").AddComponent<HuntDirector>();
    }

    void Update()
    {
        if (!GameConfig.horror || !ActController.NightStarted || FinaleController.Locked) return;
        MonsterController m = MonsterController.Find();
        if (m == null || m.mode != MonsterController.Mode.Hunt) return;

        if (timer < 0f) timer = GameConfig.huntFirstDelay;

        // Охота закончилась — новая пауза
        if (wasHunting && !m.IsHunting)
        {
            timer = ActController.Act >= 3 ? Random.Range(GameConfig.huntGapMin3, GameConfig.huntGapMax3)
                                          : Random.Range(GameConfig.huntGapMin, GameConfig.huntGapMax);
            if (strangeBox != null) Destroy(strangeBox, 5f);
        }
        wasHunting = m.IsHunting;
        if (!m.IsAway || Warning) return;

        // Свет фонарика в паузе злит его: время до охоты тает быстрее
        float speed = 1f + (Inventory.flashlightOn ? 1.5f : 0f) + (Inventory.pulse > 100f ? 0.5f : 0f);
        timer -= Time.deltaTime * speed;
        if (timer <= 0f) StartCoroutine(Omen(m));
    }

    IEnumerator Omen(MonsterController m)
    {
        Warning = true;
        Sfx.Play("hunt_omen", 1f);
        Inventory.pulse = Mathf.Min(150f, Inventory.pulse + 12f);
        if (!firstDone)
        {
            firstDone = true;
            PhoneUI.AddMessage("+7 9•• ••• •• ••", "Мигнул свет — прячься. За паллету или в контейнер. Дыши ровно (V). Он уйдёт.");
        }
        // Что-то странное: то свет мигает, то у входа появляется коробка, которой не было
        if (Random.value < 0.5f) yield return StartCoroutine(Flicker(1.6f));
        else { SpawnStrangeBox(); yield return StartCoroutine(Flicker(0.5f)); }

        yield return new WaitForSeconds(Mathf.Max(0f, GameConfig.huntWarning - 1.6f));
        Warning = false;
        if (m != null && m.IsAway) m.StartHuntNow();
    }

    // Все лампы рядом с игроком и фонарик мигают
    IEnumerator Flicker(float seconds)
    {
        Camera cam = Camera.main;
        Vector3 p = cam != null ? cam.transform.position : Vector3.zero;
        var lights = new System.Collections.Generic.List<Light>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type != LightType.Directional && l.enabled && (l.transform.position - p).sqrMagnitude < 30f * 30f) lights.Add(l);
        float[] base0 = new float[lights.Count];
        for (int i = 0; i < lights.Count; i++) base0[i] = lights[i].intensity;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            bool off = Random.value < 0.45f;
            for (int i = 0; i < lights.Count; i++) if (lights[i] != null) lights[i].intensity = off ? base0[i] * 0.05f : base0[i];
            yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
            t += 0.08f;
        }
        for (int i = 0; i < lights.Count; i++) if (lights[i] != null) lights[i].intensity = base0[i];
    }

    // Мокрая тёмная коробка у входа на склад — никто её не привозил
    void SpawnStrangeBox()
    {
        GameObject door = GameObject.Find("DoorTrigger_Inside");
        if (door == null) return;
        if (strangeBox != null) Destroy(strangeBox);
        strangeBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strangeBox.name = "StrangeBox";
        Vector3 pos = door.transform.position + door.transform.right * Random.Range(-1.2f, 1.2f);
        RaycastHit hit;
        float y = pos.y;
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out hit, 5f, ~(1 << 2), QueryTriggerInteraction.Ignore)) y = hit.point.y;
        strangeBox.transform.position = new Vector3(pos.x, y + 0.25f, pos.z);
        strangeBox.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        strangeBox.transform.localScale = new Vector3(0.55f, 0.5f, 0.45f);
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.18f, 0.12f, 0.09f));
        m.SetFloat("_Smoothness", 0.7f);   // мокрая
        strangeBox.GetComponent<Renderer>().sharedMaterial = m;
    }
}
