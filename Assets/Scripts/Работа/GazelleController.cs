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

        // Мотор едет вместе с машиной; на стоянке тише и ниже (холостой ход)
        AudioSource engine = Sfx.Loop("gazelle_engine", transform, 1f, 45f);

        yield return StartCoroutine(Move(stop, 7f));
        Sfx.PlayAt("gazelle_brake", transform.position, 1f);
        if (engine != null) { engine.volume = 0.55f; engine.pitch = 0.85f; }
        yield return new WaitForSeconds(0.8f);
        Sfx.PlayAt("gazelle_door", transform.position, 1f);
        yield return new WaitForSeconds(1.7f);

        // Паллеты погружены
        int n = ShipZone.Instance != null ? ShipZone.Instance.ClearAll() : 0;
        Inventory.palletsReady = 0;
        if (n > 0) Hud.AddMoney(n * GameConfig.payPerPallet);
        Inventory.palletsShipped += n;
        if (n > 0)
        {
            Sfx.PlayAt("gazelle_load", transform.position, 1f);
            int left = GameConfig.quotaPallets - Inventory.palletsShipped;
            if (left > 0)
                PhoneCall.Boss("boss_quota_left", "Видел, газель забрала. До нормы ещё не дотянул — давай, шевелись.",
                    "Норма: " + Inventory.palletsShipped + " из " + GameConfig.quotaPallets + " паллет. Осталось " + left + ".");
            else
                PhoneCall.Boss("boss_quota_done", "Норму сделал, молодец. Премия будет. Можешь не рвать жилы.",
                    "Норма выполнена (" + Inventory.palletsShipped + "/" + GameConfig.quotaPallets + "). Премия будет. Можешь не рвать жилы.");
        }
        PhoneUI.AddMessage("Газель", "Забрал паллет: " + n + ". Оплата: " + (n * GameConfig.payPerPallet) + " руб. Хорошей смены.");

        yield return new WaitForSeconds(1.0f);
        Sfx.PlayAt("gazelle_door", transform.position, 1f);
        yield return new WaitForSeconds(0.5f);
        Sfx.PlayAt("gazelle_horn", transform.position, 1f);
        if (engine != null) { engine.volume = 1f; engine.pitch = 1f; }
        yield return StartCoroutine(Move(stop + Vector3.right * 60f, 8f));
        if (engine != null) Destroy(engine);

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
