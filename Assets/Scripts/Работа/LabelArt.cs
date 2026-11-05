using UnityEngine;

// Картинки для этикеток FBO, рисуются кодом (без файлов):
//  - QR(seed)      — квадрат «QR-кода» 25×25 с тремя метками по углам (у каждой коробки свой);
//  - Barcode(seed) — штрихкод;
//  - Sticker(seed) — вся этикетка для коробки в 3D: шапка, QR, штрихкод, серые строки «текста».
// Номера на 3D-этикетке не читаются (слишком мелко) — их показывает крупный вид проверки (LabelCheck).
public static class LabelArt
{
    public static Texture2D QR(int seed)
    {
        const int N = 25;
        System.Random r = new System.Random(seed);
        Texture2D t = new Texture2D(N + 2, N + 2, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < N + 2; y++)
            for (int x = 0; x < N + 2; x++)
            {
                int u = x - 1, v = y - 1;
                bool black = false;
                if (u >= 0 && v >= 0 && u < N && v < N)
                {
                    int f = Finder(u, v, N);
                    black = f >= 0 ? f == 1 : r.NextDouble() < 0.48;
                }
                t.SetPixel(x, y, black ? Color.black : Color.white);
            }
        t.Apply();
        return t;
    }

    // Метка по углу: 1 — чёрный, 0 — белый, -1 — не метка
    static int Finder(int u, int v, int n)
    {
        int[,] c = { { 0, 0 }, { n - 7, 0 }, { 0, n - 7 } };
        for (int i = 0; i < 3; i++)
        {
            int x = u - c[i, 0], y = v - c[i, 1];
            if (x >= -1 && y >= -1 && x <= 7 && y <= 7)
            {
                if (x < 0 || y < 0 || x > 6 || y > 6) return 0;
                bool ring = x == 0 || y == 0 || x == 6 || y == 6;
                bool core = x >= 2 && x <= 4 && y >= 2 && y <= 4;
                return ring || core ? 1 : 0;
            }
        }
        return -1;
    }

    public static Texture2D Barcode(int seed)
    {
        System.Random r = new System.Random(seed * 31 + 7);
        const int W = 120;
        Texture2D t = new Texture2D(W, 1, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp;
        int x = 0;
        bool black = true;
        while (x < W)
        {
            int w = r.Next(1, 4);
            if (x < 3 || x > W - 4) w = 1;
            for (int i = 0; i < w && x < W; i++, x++) t.SetPixel(x, 0, black ? Color.black : Color.white);
            black = !black;
        }
        t.Apply();
        return t;
    }

    // Этикетка 256×176: чёрная шапка, QR слева, справа строки, внизу штрихкод
    public static Texture2D Sticker(int seed)
    {
        const int W = 256, H = 176;
        Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Clamp;
        Color paper = new Color(0.95f, 0.94f, 0.9f);
        Color[] px = new Color[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = paper;

        // шапка
        Fill(px, W, 0, H - 26, W, 26, new Color(0.08f, 0.08f, 0.09f));
        // QR
        Texture2D q = QR(seed);
        int qs = 104, qx = 12, qy = H - 26 - 12 - qs;
        for (int y = 0; y < qs; y++)
            for (int x = 0; x < qs; x++)
                px[(qy + y) * W + qx + x] = q.GetPixel(x * q.width / qs, y * q.height / qs) == Color.black ? Color.black : paper;
        Object.Destroy(q);
        // строки «текста»
        System.Random r = new System.Random(seed + 99);
        for (int i = 0; i < 6; i++)
        {
            int ly = H - 40 - i * 16;
            int len = 60 + r.Next(60);
            Fill(px, W, 128, ly, len, i == 0 ? 8 : 5, new Color(0.15f, 0.15f, 0.16f, 1f));
        }
        // штрихкод
        Texture2D b = Barcode(seed);
        for (int y = 8; y < 34; y++)
            for (int x = 0; x < 232; x++)
                if (b.GetPixel(x * b.width / 232, 0) == Color.black) px[y * W + 12 + x] = Color.black;
        Object.Destroy(b);
        // лёгкая грязь и потёртость
        for (int i = 0; i < px.Length; i++)
        {
            float n = Mathf.PerlinNoise((i % W) * 0.05f + seed, (i / W) * 0.05f) * 0.08f;
            px[i] = Color.Lerp(px[i], new Color(0.6f, 0.55f, 0.45f), n);
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    // Стикер FBS 160×112: шапка, штрихкод, строки «текста» и маленький квадратный код справа
    public static Texture2D FbsSticker(int seed)
    {
        const int W = 160, H = 112;
        Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Clamp;
        Color paper = new Color(0.97f, 0.96f, 0.93f), ink = new Color(0.08f, 0.08f, 0.09f);
        Color[] px = new Color[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = paper;
        Fill(px, W, 0, H - 18, W, 18, ink);                                   // шапка «FBS»
        System.Random r = new System.Random(seed);
        for (int i = 0; i < 3; i++) Fill(px, W, 8, H - 32 - i * 11, 50 + r.Next(50), i == 0 ? 6 : 4, ink);   // строки
        Texture2D q = QR(seed);                                               // квадратный код справа
        int qs = 40, qx = W - qs - 8, qy = H - 18 - 6 - qs;
        for (int y = 0; y < qs; y++)
            for (int x = 0; x < qs; x++)
                if (q.GetPixel(x * q.width / qs, y * q.height / qs) == Color.black) px[(qy + y) * W + qx + x] = ink;
        Object.Destroy(q);
        Texture2D b = Barcode(seed);                                          // штрихкод внизу
        for (int y = 8; y < 34; y++)
            for (int x = 0; x < W - 16; x++)
                if (b.GetPixel(x * b.width / (W - 16), 0) == Color.black) px[y * W + 8 + x] = ink;
        Object.Destroy(b);
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    static void Fill(Color[] px, int W, int x0, int y0, int w, int h, Color c)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                if (x >= 0 && x < W && y >= 0 && y * W + x < px.Length) px[y * W + x] = c;
    }
}
