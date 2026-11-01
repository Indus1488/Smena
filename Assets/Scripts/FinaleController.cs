using System.Collections;
using UnityEngine;

// Финал. В 20:00 (или F11 для теста) смена кончается: интерфейс пропадает, телефон закрывается.
// Идите к остановке (объект BusStop возле завода, где была лампа). Как только подойдёте,
// управление отбирается, камера сама разворачивается назад, и из темноты на вас выбегает Володя.
// Дальше чёрный экран «СМЕНА ОКОНЧЕНА».
// Свой крик положите в Assets/Resources под именем finale_scream.
public class FinaleController : MonoBehaviour
{
    public static bool Locked;

    bool started, triggered;
    Transform stop;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Locked = false;
        new GameObject("FinaleController").AddComponent<FinaleController>();
    }

    void Update()
    {
        bool debug = GameConfig.debugKeys && Input.GetKeyDown(KeyCode.F11);
        if (!started && (Hud.minutes >= 20f * 60f || debug)) StartFinale();

        if (started && !triggered)
        {
            if (stop == null)
            {
                GameObject s = GameObject.Find("BusStop");
                if (s != null) stop = s.transform;
            }
            GameObject p = GameObject.Find("Player");
            if (stop != null && p != null)
            {
                Vector3 a = p.transform.position; a.y = 0f;
                Vector3 b = stop.position; b.y = 0f;
                if (Vector3.Distance(a, b) < 3f)
                {
                    triggered = true;
                    StartCoroutine(Scare());
                }
            }
        }
    }

    void StartFinale()
    {
        started = true;
        ActController.Act = 4;
        Hud.hidden = true;
        PhoneUI.Locked = true;

        if (BoxSpawner.Instance != null) BoxSpawner.Instance.Stop();

        MonsterController m = MonsterController.Find();
        if (m != null)
        {
            m.mode = MonsterController.Mode.Hidden;
            m.gameObject.SetActive(false);
        }
    }

    IEnumerator Scare()
    {
        Locked = true;
        GameObject pgo = GameObject.Find("Player");
        Camera cam = Camera.main;

        // Игра забирает управление
        PlayerController pc = pgo.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;
        Interactor it = cam != null ? cam.GetComponent<Interactor>() : null;
        if (it != null) it.enabled = false;
        Inventory.hasFlashlight = false;

        // Камера сама разворачивается назад
        Quaternion y0 = pgo.transform.rotation;
        Quaternion y1 = y0 * Quaternion.Euler(0f, 180f, 0f);
        Quaternion c0 = cam.transform.localRotation;
        float t = 0f;
        while (t < 2.5f)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 2.5f);
            pgo.transform.rotation = Quaternion.Slerp(y0, y1, k);
            cam.transform.localRotation = Quaternion.Slerp(c0, Quaternion.identity, k);
            yield return null;
        }
        yield return new WaitForSeconds(1.2f);

        // Володя выбегает из темноты прямо на камеру
        MonsterController m = MonsterController.Find();
        if (m != null)
        {
            m.gameObject.SetActive(true);
            m.mode = MonsterController.Mode.Scripted;
            Transform mt = m.transform;

            Vector3 fwd = pgo.transform.forward;
            fwd.y = 0f;
            fwd.Normalize();
            Vector3 start = pgo.transform.position + fwd * 35f;
            start.y = pgo.transform.position.y - 1f;
            mt.position = start;

            // Лампа у лица, чтобы в последний момент его было видно
            GameObject lg = new GameObject("ScareLight");
            lg.transform.SetParent(mt, false);
            lg.transform.localPosition = new Vector3(0f, 1.9f, 0.9f);
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 8f;
            l.intensity = 3f;
            l.color = new Color(1f, 0.9f, 0.8f);

            AudioClip scream = Resources.Load<AudioClip>("finale_scream");
            if (scream != null) AudioSource.PlayClipAtPoint(scream, pgo.transform.position, 1f);

            while (true)
            {
                Vector3 target = cam.transform.position;
                Vector3 dir = target - mt.position;
                dir.y = 0f;
                if (dir.magnitude < 1.2f) break;
                mt.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                mt.position += dir.normalized * 16f * Time.deltaTime;
                yield return null;
            }
        }

        CenterText.Show("СМЕНА ОКОНЧЕНА", 100000f, true);
    }
}
