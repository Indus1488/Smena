using UnityEngine;

// Что у игрока с собой и как к нему относятся селлеры.
public static class Inventory
{
    public static bool hasTape;
    public static int sellerAnger;   // сколько селлеров «запомнили» вас. Пригодится в акте 3.
    public static int boxesReady;    // готовые коробки, которые нужно собрать на паллету

    public static bool hasMop;
    public static int trashTotal;    // сколько кусков мусора было у входа
    public static int trashLeft;     // сколько осталось убрать

    public static bool hasJack;      // рохля в руках
    public static bool hasKey;       // ключ от самого левого склада

    public static bool hasFlashlight;        // фонарик появляется в акте 2
    public static float flashlightBattery = 100f;   // заряд в процентах
    public static int spareBatteries;        // запасные батареи (аккумуляторы)
    public static int palletsReady;  // паллет в зоне отгрузки, ждут газель

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        hasTape = false;
        sellerAnger = 0;
        boxesReady = 0;
        hasMop = false;
        trashTotal = 0;
        trashLeft = 0;
        hasJack = false;
        hasKey = false;
        hasFlashlight = false;
        flashlightBattery = 100f;
        spareBatteries = 0;
        palletsReady = 0;
    }
}
