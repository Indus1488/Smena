using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Вешается на зону перед деревянной доской. Для FBS-заказов нажимайте E, чтобы клеить стикеры.
[RequireComponent(typeof(Interactable))]
public class StickerBoard : MonoBehaviour
{
    public static StickerBoard Instance;

    public Material stickerMaterial;   // подставляется построителем склада
    // Сетка мест для стикеров: 6 столбцов x 4 ряда. Стикеры остаются на доске после готовой коробки;
    // когда места кончаются, самый старый стикер срывается и на его место клеится новый.
    public int columns = 6;
    public int rows = 4;
    public float facing = 1f;          // 1 = доска смотрит в +X (левая стена), -1 = в -X (правая стена)

    List<GameObject> stuck = new List<GameObject>();
    List<int> stuckSlot = new List<int>();
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
        PlaceSticker();
        Sfx.Play("sticker", 0.8f);
        b.OnStickerStuck();
    }

    void PlaceSticker()
    {
        int total = columns * rows;
        // Случайное свободное место; если свободных нет — срываем самый старый стикер
        List<int> free = new List<int>();
        for (int k = 0; k < total; k++) if (!stuckSlot.Contains(k)) free.Add(k);
        int slot;
        if (free.Count > 0) slot = free[Random.Range(0, free.Count)];
        else
        {
            slot = stuckSlot[0];
            if (stuck[0] != null) Destroy(stuck[0]);
            stuck.RemoveAt(0);
            stuckSlot.RemoveAt(0);
        }

        int col = slot % columns;
        int row = slot / columns;
        float stepZ = 1.25f / columns, stepY = 0.85f / rows;

        // Плоский стикер с напечатанным штрихкодом (кубик TextureNoStretch перекрашивал в пустую бумагу)
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(s.GetComponent<Collider>());
        s.name = "Sticker";
        s.transform.SetParent(transform, false);
        // Лицо доски — в 3 см от этой точки (доска на правой стене, facing = -1): стикер на 2.2 см, перед доской
        s.transform.localPosition = new Vector3(-0.022f * facing,
            0.425f - stepY * (row + 0.5f) + Random.Range(-0.02f, 0.02f),
            -0.625f + stepZ * (col + 0.5f) + Random.Range(-0.02f, 0.02f));
        s.transform.localRotation = Quaternion.Euler(0f, -90f * facing, Random.Range(-9f, 9f));   // лицом от доски, чуть криво
        s.transform.localScale = new Vector3(stepZ * 0.8f, stepY * 0.72f, 1f);
        Material sm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Texture2D tex = LabelArt.FbsSticker(Random.Range(1, 100000));
        sm.mainTexture = tex; sm.SetTexture("_BaseMap", tex); sm.SetFloat("_Smoothness", 0.2f);
        sm.SetFloat("_Cull", 0f);   // видно с обеих сторон, даже если доска повёрнута иначе
        s.GetComponent<Renderer>().sharedMaterial = sm;
        stuck.Add(s);
        stuckSlot.Add(slot);
    }

    public void Clear()
    {
        for (int i = 0; i < stuck.Count; i++)
            if (stuck[i] != null) Destroy(stuck[i]);
        stuck.Clear();
        stuckSlot.Clear();
    }
}
