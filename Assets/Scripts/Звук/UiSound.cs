using UnityEngine;

// Звуки интерфейса для кнопок меню и телефона.
//   ui_hover — навёл мышь на кнопку,  ui_click — нажал,  ui_back — «Назад» / «Закрыть» (нет файла — ui_click),
//   ui_slider — тянешь ползунок в настройках.
// В коде вместо GUI.Button(...) пишется UiSound.Button(...) — всё остальное как раньше.
public static class UiSound
{
    static int hoverKey, hoverFrame = -10;

    public static bool Button(Rect r, string text, GUIStyle style)
    {
        return Button(r, new GUIContent(text), style);
    }

    public static bool Button(Rect r, GUIContent content, GUIStyle style)
    {
        Hover(r);
        bool clicked = GUI.Button(r, content, style);
        if (clicked) Click(content != null ? content.text : null);
        return clicked;
    }

    // Отдельно, для кнопок-картинок (главное меню рисует свои)
    public static void Hover(Rect r)
    {
        Event e = Event.current;
        if (e == null || e.type != EventType.Repaint) return;
        if (!r.Contains(e.mousePosition)) return;
        Vector2 p = GUIUtility.GUIToScreenPoint(r.position);
        int key = ((int)p.x * 73856093) ^ ((int)p.y * 19349663) ^ ((int)r.width * 83492791);
        if (key != hoverKey || Time.frameCount - hoverFrame > 2) Sfx.Play("ui_hover", 0.35f);
        hoverKey = key;
        hoverFrame = Time.frameCount;
    }

    public static void Click(string text = null)
    {
        bool back = text != null && (text.StartsWith("Назад") || text.StartsWith("Закрыть") || text.StartsWith("К списку"));
        Sfx.Play(back && Sfx.Get("ui_back") != null ? "ui_back" : "ui_click", 0.6f);
    }

    // Ползунок (громкость, чувствительность, яркость): ui_slider, пока тянешь
    static float lastSlide;
    public static float Slider(Rect r, float value, float min, float max)
    {
        float v = GUI.HorizontalSlider(r, value, min, max);
        if (Mathf.Abs(v - value) > (max - min) * 0.004f && Time.unscaledTime - lastSlide > 0.07f)
        {
            lastSlide = Time.unscaledTime;
            Sfx.Play("ui_slider", 0.5f);
        }
        return v;
    }
}
