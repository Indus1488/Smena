// Переключатели для тестирования. Меняйте здесь, а не по всему проекту.
public static class GameConfig
{
    // true: время идёт быстро (удобно тестировать акт 2).
    // В ИТОГОВОЙ ВЕРСИИ поставьте false: время пойдёт в 2 раза медленнее.
    public static bool fastTestTime = true;

    // true: пропустить уборку мусора у входа и сразу начать приём коробок
    public static bool skipPrologue = false;

    // true: работают тестовые клавиши (B: новая коробка, P: +4 готовые коробки)
    // В итоговой версии поставьте false.
    public static bool debugKeys = true;

    // true: игра сразу начинается с акта 2 (тьма, фонарик). Для тестов.
    public static bool startInAct2 = false;

    // Сколько процентов заряда фонарик тратит за секунду (0.4 = примерно 4 минуты работы)
    public static float flashlightDrainPerSecond = 0.4f;
}
