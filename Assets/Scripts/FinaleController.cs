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

        HidingSpot.ForceExit();
        if (GameConfig.horror) yield return StartCoroutine(MonsterScare(pgo, cam));
        else yield return new WaitForSeconds(1.5f);

        CenterText.Show("СМЕНА ОКОНЧЕНА", 4f, true);
        yield return new WaitForSeconds(4f);

        // Итоги смены
        string verdict = Inventory.sellerAnger == 0 ? "Селлеры тебя не запомнили."
            : (Inventory.sellerAnger < 4 ? "Кое-кто тебя запомнил." : "Тебя запомнили многие.");
        bool quotaOk = Inventory.palletsShipped >= GameConfig.quotaPallets;
        int quotaMoney = quotaOk ? GameConfig.quotaBonus : -GameConfig.quotaFine;
        Hud.AddMoney(quotaMoney);
        string quota = "Норма: " + Inventory.palletsShipped + "/" + GameConfig.quotaPallets + " паллет — "
            + (quotaOk ? "выполнена, премия " + GameConfig.quotaBonus + " руб." : "не выполнена, штраф " + GameConfig.quotaFine + " руб.");
        CenterText.Show("СМЕНА ОКОНЧЕНА\n\nЗаработано: " + Hud.totalEarned + " руб.\n" + quota + "\nНа руках: " + Hud.money + " руб.\nНедовольных селлеров: "
            + Inventory.sellerAnger + "\n" + verdict + "\n\nEsc — выход", 100000f, true);
    }

    // Скример: камера оборачивается, Володя бежит из темноты, бросается в лицо (помехи, тряска, крик), чернота
    IEnumerator MonsterScare(GameObject pgo, Camera cam)
    {
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

        MonsterController m = MonsterController.Find();
        if (m == null) yield break;
        m.gameObject.SetActive(true);
        m.mode = MonsterController.Mode.Scripted;
        m.scriptRun = true;                    // в модели — бег, а не застывшая поза
        Transform mt = m.transform;

        Vector3 fwd = pgo.transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 start = pgo.transform.position + fwd * 10f;
        start.y = pgo.transform.position.y - 1f;
        mt.position = start;

        // Тусклый свет у лица, чтобы его было видно в последний момент
        GameObject lg = new GameObject("ScareLight");
        lg.transform.SetParent(mt, false);
        lg.transform.localPosition = new Vector3(0.3f, 2.3f, 0.6f);
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 3.5f;
        l.intensity = 0.7f;
        l.color = new Color(1f, 0.9f, 0.8f);

        Sfx.PlayAt("monster_run", mt.position, 1f);

        // Бежит
        while (true)
        {
            Vector3 dir = cam.transform.position - mt.position; dir.y = 0f;
            if (dir.magnitude < 1.6f) break;
            mt.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            mt.position += dir.normalized * 7f * Time.deltaTime;
            yield return null;
        }

        // Бросок в лицо: голова прямо перед камерой
        m.scriptRun = false;
        VolodyaModel vm = m.GetComponent<VolodyaModel>();
        Transform head = vm != null ? vm.Head : null;
        Vector3 camF = cam.transform.forward; camF.y = 0f; camF.Normalize();
        mt.rotation = Quaternion.LookRotation(-camF, Vector3.up);
        if (head != null) mt.position += (cam.transform.position + cam.transform.forward * 0.45f) - head.position;

        AudioClip scream = Resources.Load<AudioClip>("jumpscare");
        if (scream == null) scream = Resources.Load<AudioClip>("finale_scream");
        if (scream != null) AudioSource.PlayClipAtPoint(scream, cam.transform.position, 1f);

        Jumpscare.Play(1.1f);
        float fov0 = cam.fieldOfView;
        Vector3 camPos0 = cam.transform.localPosition;
        t = 0f;
        while (t < 1.1f)
        {
            t += Time.deltaTime;
            cam.transform.localPosition = camPos0 + Random.insideUnitSphere * 0.07f;
            cam.fieldOfView = Mathf.Lerp(fov0, fov0 * 0.7f, Mathf.Clamp01(t / 0.25f));
            yield return null;
        }
        cam.transform.localPosition = camPos0;
        cam.fieldOfView = fov0;

        // Чернота и тишина
        Jumpscare.Black(true);
        AudioListener.volume = 0f;
        m.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        AudioListener.volume = PlayerPrefs.GetFloat("volume", 1f);
        Jumpscare.Black(false);
    }
}
