using UnityEngine;

// Оформление интерфейса (всё, кроме главного меню): шрифт, тёмные панели, кнопки с янтарной рамкой.
// Шрифт — Assets/Resources/UI/ui_font.ttf (обычный) и ui_font_bold.ttf (жирный, для кнопок).
// Чтобы сменить шрифт, положите туда другой .ttf с теми же именами (нужна кириллица).
// Цвета — константы ниже.
public static class UITheme
{
    public static readonly Color Accent = new Color(1f, 0.72f, 0.32f);        // янтарный: выделение, рамки
    public static readonly Color Text = new Color(0.95f, 0.92f, 0.86f);       // основной текст (тёплый белый)
    public static readonly Color Dim = new Color(0.72f, 0.69f, 0.64f);        // второстепенный текст
    static readonly Color PanelFill = new Color(0.055f, 0.055f, 0.065f, 0.88f);

    static Font regular, bold;
    static Texture2D panelTex, btnTex, btnHoverTex, btnActiveTex;

    public static Font Font
    {
        get { if (regular == null) regular = Resources.Load<Font>("UI/ui_font"); return regular; }
    }

    public static Font Bold
    {
        get { if (bold == null) bold = Resources.Load<Font>("UI/ui_font_bold"); return bold != null ? bold : Font; }
    }

    public static GUIStyle Label()
    {
        GUIStyle s = new GUIStyle(GUI.skin.label);
        if (Font != null) s.font = Font;
        s.normal.textColor = Text;
        return s;
    }

    public static GUIStyle Button()
    {
        Init();
        GUIStyle s = new GUIStyle(GUI.skin.button);
        if (Bold != null) s.font = Bold;
        s.border = new RectOffset(4, 4, 4, 4);
        s.normal.background = btnTex;
        s.hover.background = btnHoverTex;
        s.focused.background = btnTex;
        s.active.background = btnActiveTex;
        s.onNormal.background = btnHoverTex;
        s.normal.textColor = Text;
        s.focused.textColor = Text;
        s.hover.textColor = Accent;
        s.active.textColor = Color.white;
        s.onNormal.textColor = Accent;
        return s;
    }

    public static GUIStyle Panel()
    {
        Init();
        GUIStyle s = new GUIStyle(GUI.skin.box);
        if (Font != null) s.font = Font;
        s.border = new RectOffset(4, 4, 4, 4);
        s.normal.background = panelTex;
        s.normal.textColor = Text;
        return s;
    }

    // Тёмная панель с тонкой рамкой и янтарной полосой слева — для подложек под текст
    public static void DrawPanel(Rect r)
    {
        Init();
        GUI.DrawTexture(r, panelTex);
        Color c = GUI.color;
        GUI.color = Accent;
        GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
        GUI.color = c;
    }

    static void Init()
    {
        if (panelTex != null) return;
        panelTex = Make(PanelFill, new Color(1f, 1f, 1f, 0.10f));
        btnTex = Make(new Color(0.09f, 0.09f, 0.10f, 0.94f), new Color(1f, 1f, 1f, 0.14f));
        btnHoverTex = Make(new Color(0.16f, 0.12f, 0.07f, 0.96f), Accent);
        btnActiveTex = Make(new Color(0.45f, 0.30f, 0.10f, 0.98f), Accent);
    }

    static Texture2D Make(Color fill, Color edge)
    {
        const int n = 12;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.hideFlags = HideFlags.DontSave;
        t.filterMode = FilterMode.Point;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool border = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                t.SetPixel(x, y, border ? edge : fill);
            }
        t.Apply();
        return t;
    }
}
