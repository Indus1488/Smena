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

    // =====================================================================
    // ХОРРОР-ЧАСТЬ
    // true  — как задумано: Володя, пульс и дыхание, укрытия, скример в финале.
    // false — спокойная версия: только работа и темнота, без Володи, пульса и скримера.
    // =====================================================================
    // Переключается в Unity: Smena → Режим игры. Хранится в PlayerPrefs; по умолчанию — хоррор.
    public const string HorrorKey = "gameHorror";
    public static bool horror
    {
        get { return UnityEngine.PlayerPrefs.GetInt(HorrorKey, 1) == 1; }
        set { UnityEngine.PlayerPrefs.SetInt(HorrorKey, value ? 1 : 0); UnityEngine.PlayerPrefs.Save(); }
    }

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
    public static int palletCapacity = 8;

    // Событие «рулоны»: в это время селлеры привозят рулоны вместо коробок (минуты от полуночи).
    // Меряешь один рулон (длину и диаметр), потом FBS: стикеры на доску. Партия — много одинаковых рулонов.
    // На паллете рулон занимает полместа коробки (игроку это не пишется).
    public static float rollsFrom = 17f * 60f, rollsTo = 17f * 60f + 40f;
    // Один селлер привозит много рулонов (от и до); рулоны всегда FBS — на всю партию 2 стикера
    public static int rollsMin = 8, rollsMax = 12;
    public static int rollStickers = 2;
    public static int payPerPallet = 1200;

    // Поворот мышью влево-вправо, пока везёшь паллету на рохле (1 — как обычно, 0.35 — втрое медленнее)
    public static float jackTurnFactor = 0.35f;

    // Норма смены: сколько паллет должна увезти газель до 20:00. Выполнил — премия, нет — штраф (в итогах смены)
    public static int quotaPallets = 1;
    public static int quotaBonus = 1500;
    public static int quotaFine = 1000;

    // ---------- Охоты Володи (хоррор, с 18:00 после щитка) ----------
    // Перед охотой — странное событие (мигает свет, странная коробка, звук), через huntWarning секунд он выходит.
    // Между охотами можно работать. В акте 3 (с 19:30) охоты чаще.
    public static float huntFirstDelay = 50f;                 // после темноты до первого странного события, с
    public static float huntGapMin = 100f, huntGapMax = 140f;  // пауза между охотами в акте 2, с
    public static float huntGapMin3 = 60f, huntGapMax3 = 80f;  // в акте 3
    public static float huntWarning = 8f;                     // от странного события до выхода, с
    public static float huntLength = 40f;                     // сколько длится охота, с

    // Магазин (Tab → Магазин)
    public static int priceRadar = 600;     // радар: только в хоррор-режиме
    public static int priceCoffee = 150;    // кофе: 60 с бегаешь быстрее; в хорроре ещё и пульс ниже

    // Укрытия: с каким пульсом Володя услышит сердце, даже если вы спрятались (и он совсем рядом)
    public static float hidePulseLimit = 110f;

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
