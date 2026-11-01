// Все настройки игры в одном месте.
public static class GameConfig
{
    // =====================================================================
    // ГЛАВНЫЙ ПЕРЕКЛЮЧАТЕЛЬ ПЕРЕД СБОРКОЙ ИГРЫ
    // false — режим разработки: быстрое время, тестовые клавиши, переключатели ниже работают.
    // true  — итоговая версия: время вдвое медленнее, тестовые клавиши выключены,
    //         пролог и главное меню включены, игра начинается с акта 1.
    // =====================================================================
    public static bool release = false;

    // ---------- Режим разработки (работают только при release = false) ----------
    const bool devFastTime = true;     // быстрое время (удобно тестировать акт 2)
    const bool devSkipPrologue = false;   // пропустить уборку мусора у входа
    const bool devDebugKeys = true;    // тестовые клавиши: B, P, Y, F9, F10, F11
    const bool devStartInAct2 = false;    // начинать сразу с акта 2
    const bool devShowMainMenu = true;    // показывать главное меню при запуске

    public static bool fastTestTime { get { return !release && devFastTime; } }
    public static bool skipPrologue { get { return !release && devSkipPrologue; } }
    public static bool debugKeys { get { return !release && devDebugKeys; } }
    public static bool startInAct2 { get { return !release && devStartInAct2; } }
    public static bool showMainMenu { get { return release || devShowMainMenu; } }

    // ---------- Баланс (действует всегда) ----------

    // Сколько процентов заряда фонарик тратит за секунду (0.8 = примерно 2 минуты работы)
    public static float flashlightDrainPerSecond = 0.8f;

    // Сколько ровных вдохов нужно в мини-игре дыхания (по сюжету 10-15)
    public static int breathsNeeded = 10;

    // Цены в магазине и вызов газели (в рублях)
    public static int pricePallet = 300;
    public static int priceBattery = 250;
    public static int priceGazelle = 800;

    // Сколько коробок помещается на паллету и сколько платят за каждую паллету, которую увезла газель
    public static int palletCapacity = 6;
    public static int payPerPallet = 600;

    // Мусор: сколько кусков влезает в мешок, сколько появляется после готовой коробки (1..N), максимум на полу
    public static int trashBagSize = 8;
    public static int trashPerBoxMax = 2;
    public static int trashMaxOnFloor = 20;

    // Подсказка «Задача: ...» вверху экрана (что делать дальше). Можно выключить, когда игра станет привычной.
    public static bool showObjectives = true;

    // ---------- Графика ----------
    // Ретро-вид в стиле PS1: низкое разрешение с чёткими пикселями.
    // ps1Height — высота картинки в пикселях (меньше = грубее, например 200; больше = мягче, например 360).
    public static bool ps1Look = true;
    public static int ps1Height = 360;
}
