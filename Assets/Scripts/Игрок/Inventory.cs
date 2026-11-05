using UnityEngine;

// Что у игрока с собой и как к нему относятся селлеры.
public static class Inventory
{
    public static bool hasTape;
    public static int sellerAnger;   // сколько селлеров «запомнили» вас. Пригодится в акте 3.
    public static int boxesReady;    // готовые коробки, которые нужно собрать на паллету
    public static int rollsReady;    // готовые рулоны (событие в 17:00); на паллете рулон занимает полместа коробки

    public static bool hasMop;
    public static int trashTotal;    // сколько кусков мусора было у входа
    public static int trashLeft;     // сколько осталось убрать

    public static bool hasJack;      // рохля в руках
    public static bool hasKey;       // ключ от самого левого склада

    public static bool hasFlashlight;        // фонарик появляется в акте 2
    public static bool hasRadar;             // радар из магазина (хоррор): показывает Володю рядом
    public static float coffeeUntil;         // до какого времени действует кофе (Time.time)
    public static bool Coffee { get { return UnityEngine.Time.time < coffeeUntil; } }
    public static float flashlightBattery = 100f;   // заряд в процентах
    public static int spareBatteries;        // запасные батареи (аккумуляторы)
    public static bool flashlightOn;         // фонарик сейчас светит (Володя это «видит»)
    public static float pulse = 70f;         // пульс игрока
    public static int palletsReady;  // паллет в зоне отгрузки, ждут газель
    public static int palletStock;   // купленные паллеты, которых ещё нет на месте сборки (лежат стопкой во дворе)
    public static int trashBag;      // сколько мусора в мешке
    public static int palletsShipped;   // сколько паллет увезла газель (для нормы смены)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        hasTape = false;
        sellerAnger = 0;
        boxesReady = 0;
        rollsReady = 0;
        hasMop = false;
        trashTotal = 0;
        trashLeft = 0;
        hasJack = false;
        hasKey = false;
        hasFlashlight = false;
        hasRadar = false;
        coffeeUntil = 0f;
        flashlightBattery = 100f;
        spareBatteries = 0;
        flashlightOn = false;
        pulse = 70f;
        palletsReady = 0;
        palletStock = 0;
        trashBag = 0;
        palletsShipped = 0;
    }
}