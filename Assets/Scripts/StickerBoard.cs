using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Вешается на зону перед деревянной доской. Для FBS-заказов нажимайте E, чтобы клеить стикеры.
[RequireComponent(typeof(Interactable))]
public class StickerBoard : MonoBehaviour
{
    public static StickerBoard Instance;

    public Material stickerMaterial;   // подставляется построителем склада
    public int maxStickers = 6;
    public float facing = 1f;          // 1 = доска смотрит в +X (левая стена), -1 = в -X (правая стена)

    List<GameObject> stuck = new List<GameObject>();
    Interactable it;

    void Awake()
    {
        Instance = this;
        it = GetComponent<Interactable>();
        if (it.onInteract == null) it.onInteract = new UnityEvent();
        it.onInteract.AddListener(OnInteract);
    }

    void Update()
    {
        MeasurableBox b = MeasurableBox.ActiveFbsBox;
        if (b != null && b.WorkLeft > 0)
            it.prompt = "E — приклеить стикер (осталось " + b.WorkLeft + ")";
        else
            it.prompt = "Доска для стикеров";
    }

    void OnInteract()
    {
        MeasurableBox b = MeasurableBox.ActiveFbsBox;
        if (b == null || b.WorkLeft <= 0)
        {
            Hud.Say("Стикеры пока не нужны.");
            return;
        }
        if (stuck.Count >= maxStickers) return;

        PlaceSticker(stuck.Count);
        b.OnStickerStuck();
    }

    void PlaceSticker(int i)
    {
        int col = i % 3;
        int row = i / 3;

        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(s.GetComponent<Collider>());
        s.name = "Sticker";
        s.transform.SetParent(transform, false);
        // Сетка 3 x 2 на лицевой стороне доски
        s.transform.localPosition = new Vector3(-0.025f * facing, 0.22f - row * 0.44f, -0.45f + col * 0.45f);
        s.transform.localScale = new Vector3(0.02f, 0.18f, 0.28f);
        if (stickerMaterial != null) s.GetComponent<Renderer>().sharedMaterial = stickerMaterial;
        stuck.Add(s);
    }

    public void Clear()
    {
        for (int i = 0; i < stuck.Count; i++)
            if (stuck[i] != null) Destroy(stuck[i]);
        stuck.Clear();
    }
}
