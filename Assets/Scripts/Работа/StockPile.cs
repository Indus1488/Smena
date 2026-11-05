using System.Collections.Generic;
using UnityEngine;

// Купленные пустые паллеты лежат стопкой во дворе — это и есть счётчик.
// Нет паллет во дворе = в запасе 0; две паллеты во дворе = в запасе 2.
// Стопка стоит на месте декоративных паллет PalletYard1/PalletYard2 (сами они прячутся).
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class StockPile : MonoBehaviour
{
    const float Step = 0.15f;    // высота одной паллеты в стопке

    Vector3 basePos;
    Quaternion baseRot;
    Transform parent;
    Material wood;
    readonly List<GameObject> shown = new List<GameObject>();
    readonly System.Random rnd = new System.Random(5);
    bool ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("StockPile").AddComponent<StockPile>();
    }

    void Start()
    {
        GameObject p1 = GameObject.Find("PalletYard1");
        if (p1 == null) { enabled = false; return; }
        basePos = p1.transform.position;
        baseRot = p1.transform.rotation;
        parent = p1.transform.parent;
        Transform deck = p1.transform.Find("Deck");
        if (deck != null) wood = deck.GetComponent<Renderer>().sharedMaterial;

        p1.SetActive(false);
        GameObject p2 = GameObject.Find("PalletYard2");
        if (p2 != null) p2.SetActive(false);
        ready = true;
    }

    void Update()
    {
        if (!ready) return;
        int want = Mathf.Clamp(Inventory.palletStock, 0, 12);
        while (shown.Count < want) shown.Add(MakePallet(shown.Count));
        while (shown.Count > want)
        {
            Destroy(shown[shown.Count - 1]);
            shown.RemoveAt(shown.Count - 1);
        }
    }

    GameObject MakePallet(int i)
    {
        GameObject p = new GameObject("StockPallet");
        p.transform.SetParent(parent, true);
        p.transform.position = basePos + Vector3.up * (i * Step);
        p.transform.rotation = baseRot * Quaternion.Euler(0, (float)(rnd.NextDouble() * 10.0 - 5.0), 0);

        Part(p.transform, "Deck", new Vector3(0, 0.13f, 0), new Vector3(1.2f, 0.04f, 0.8f), true);
        for (int k = -1; k <= 1; k++)
            Part(p.transform, "Runner", new Vector3(k * 0.5f, 0.055f, 0), new Vector3(0.1f, 0.11f, 0.8f), false);
        return p;
    }

    void Part(Transform parentT, string name, Vector3 pos, Vector3 size, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collider) Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parentT, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        if (wood != null) go.GetComponent<Renderer>().sharedMaterial = wood;
    }
}
