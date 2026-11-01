// «СМЕНА» — меню разработчика: графика. Smena → Графика → Низкая / Обычная / Высокая и переключатель VHS.
// Работает и до запуска игры, и прямо во время Play. То же самое есть в игре: Esc → Настройки.
using UnityEditor;

public static class DevGraphicsMenu
{
    const string Root = "Smena/Графика/";

    [MenuItem(Root + "Низкая (кубики, без текстур)", false, 100)] static void Q0() { VisualOptions.Quality = 0; }
    [MenuItem(Root + "Низкая (кубики, без текстур)", true)] static bool QV0() { Menu.SetChecked(Root + "Низкая (кубики, без текстур)", VisualOptions.Quality == 0); return true; }

    [MenuItem(Root + "Обычная (кубики, ретро-текстуры)", false, 101)] static void Q1() { VisualOptions.Quality = 1; }
    [MenuItem(Root + "Обычная (кубики, ретро-текстуры)", true)] static bool QV1() { Menu.SetChecked(Root + "Обычная (кубики, ретро-текстуры)", VisualOptions.Quality == 1); return true; }

    [MenuItem(Root + "Высокая (3D-модели, как Zort)", false, 102)] static void Q2() { VisualOptions.Quality = 2; }
    [MenuItem(Root + "Высокая (3D-модели, как Zort)", true)] static bool QV2() { Menu.SetChecked(Root + "Высокая (3D-модели, как Zort)", VisualOptions.Quality == 2); return true; }

    [MenuItem(Root + "VHS (пиксели PS1)", false, 120)] static void Vhs() { VisualOptions.Vhs = !VisualOptions.Vhs; }
    [MenuItem(Root + "VHS (пиксели PS1)", true)] static bool VhsV() { Menu.SetChecked(Root + "VHS (пиксели PS1)", VisualOptions.Vhs); return true; }
}
