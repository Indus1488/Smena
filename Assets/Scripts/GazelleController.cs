using System.Collections;
using UnityEngine;

// Газель. Вызывается из телефона (приложение «Газель», 1000 руб.).
// Подъезжает к жёлтой зоне, забирает все паллеты, уезжает.
// Свой звук мотора положите в Assets/Resources под именем gazelle_engine.
public class GazelleController : MonoBehaviour
{
    public static bool Busy;

    // Находит газель на сцене, даже если она выключена
    public static GazelleController Find()
    {
        GazelleController[] all = Resources.FindObjectsOfTypeAll<GazelleController>();
        for (int i = 0; i < all.Length; i++)
            if (all[i].gameObject.scene.IsValid()) return all[i];
        return null;
    }

    Vector3 home;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Busy = false;
    }

    void Awake()
    {
        home = transform.position;
    }

    public void Call()
    {
        if (Busy) return;
        gameObject.SetActive(true);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        Busy = true;

        Vector3 stop = home + Vector3.right * 32f;
        if (ShipZone.Instance != null)
        {
            stop = ShipZone.Instance.transform.position + new Vector3(-6.5f, 0f, 0f);
            stop.y = 0f;
        }

        transform.position = home;
        transform.rotation = Quaternion.LookRotation(Vector3.right);

        AudioClip engine = Resources.Load<AudioClip>("gazelle_engine");
        if (engine != null) AudioSource.PlayClipAtPoint(engine, stop, 1f);

        yield return StartCoroutine(Move(stop, 7f));
        yield return new WaitForSeconds(2.5f);

        // Паллеты погружены
        int n = ShipZone.Instance != null ? ShipZone.Instance.ClearAll() : 0;
        Inventory.palletsReady = 0;
        PhoneUI.AddMessage("Газель", "Забрал паллет: " + n + ". Хорошей смены.");

        yield return new WaitForSeconds(1.5f);
        yield return StartCoroutine(Move(stop + Vector3.right * 60f, 8f));

        gameObject.SetActive(false);
        Busy = false;
    }

    IEnumerator Move(Vector3 target, float speed)
    {
        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }
    }
}
