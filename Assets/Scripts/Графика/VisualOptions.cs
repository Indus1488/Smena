using UnityEngine;

// Настройки графики (сохраняются между запусками). Меняются в игре: Esc → Настройки;
// в редакторе: Smena → Графика.
//   Низкая  — простые фигуры (кубики) и однотонные цвета без текстур.
//   Обычная — простые фигуры с пиксельными (ретро) текстурами.
//   Высокая — 3D-модели, HD-текстуры и тёплая картинка как в Zort.
//   VHS     — отдельный переключатель: картинка в низком разрешении с крупными пикселями + эффекты кассеты.
//   Плюс отдельные галочки: эффекты кассеты, объёмный свет, тёмные углы, AO, размытие в движении.
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
        set { Set(VhsKey, value); }
    }

    // Отдельные параметры (все меняются в Esc → Настройки). Ключ PlayerPrefs, значение по умолчанию.
    public static bool VhsTape      { get { return Get("gfxVhsTape", true); }   set { Set("gfxVhsTape", value); } }      // полосы, шум, трекинг, надпись PLAY
    public static bool VolumeLight  { get { return Get("gfxVolLight", true); }  set { Set("gfxVolLight", value); } }     // объёмный свет от ламп (Высокая)
    public static bool DarkCorners  { get { return Get("gfxDark", true); }      set { Set("gfxDark", value); } }         // тёмные углы (Высокая)
    public static bool AO           { get { return Get("gfxAO", true); }        set { Set("gfxAO", value); } }           // затенение в углах и щелях
    public static bool MotionBlur   { get { return Get("gfxBlur", true); }      set { Set("gfxBlur", value); } }         // размытие в движении

    static bool Get(string k, bool def) { return PlayerPrefs.GetInt(k, def ? 1 : 0) == 1; }
    static void Set(string k, bool v)
    {
        if (PlayerPrefs.GetInt(k, -1) == (v ? 1 : 0)) return;   // не сохраняем лишний раз (меню присваивает каждый кадр)
        PlayerPrefs.SetInt(k, v ? 1 : 0); PlayerPrefs.Save();
    }

    // Меняется при любой смене графики (по нему скрипты понимают, что надо перестроиться)
    public static int Signature
    {
        get
        {
            return Quality | (Vhs ? 4 : 0) | (VhsTape ? 8 : 0) | (VolumeLight ? 16 : 0) | (DarkCorners ? 32 : 0) | (AO ? 64 : 0) | (MotionBlur ? 128 : 0);
        }
    }

    public const int VhsHeight = 360;   // высота картинки в режиме VHS, пикселей

    public static bool UseModels { get { return Quality == 2; } }
    public static bool Warm { get { return Quality == 2; } }

    // 0 — без текстур, 1 — ретро, 2 — HD
    public static int TextureMode { get { return Quality; } }
}
