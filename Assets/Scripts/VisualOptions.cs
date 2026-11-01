using UnityEngine;

// Настройки графики (сохраняются между запусками). Меняются в игре: Esc → Настройки;
// в редакторе: Smena → Графика.
//   Низкая  — простые фигуры (кубики) и однотонные цвета без текстур.
//   Обычная — простые фигуры с пиксельными (ретро) текстурами.
//   Высокая — 3D-модели, HD-текстуры и тёплая картинка как в Zort.
//   VHS     — отдельный переключатель: картинка в низком разрешении с крупными пикселями (режим PS1).
public static class VisualOptions
{
    public const string QualityKey = "gfxQuality";
    public const string VhsKey = "gfxVhs";

    public static readonly string[] QualityNames = { "Низкая", "Обычная", "Высокая" };

    public static int Quality
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 2), 0, 2); }
        set { PlayerPrefs.SetInt(QualityKey, Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); }
    }

    public static bool Vhs
    {
        get { return PlayerPrefs.GetInt(VhsKey, 0) == 1; }
        set { PlayerPrefs.SetInt(VhsKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public const int VhsHeight = 360;   // высота картинки в режиме VHS, пикселей

    public static bool UseModels { get { return Quality == 2; } }
    public static bool Warm { get { return Quality == 2; } }

    // 0 — без текстур, 1 — ретро, 2 — HD
    public static int TextureMode { get { return Quality; } }
}
