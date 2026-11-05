using UnityEngine;

// Скример в духе FNAF: помехи на весь экран, тряска камеры и резкое приближение,
// потом мгновенная чернота. Вызывается из FinaleController.
public class Jumpscare : MonoBehaviour
{
    static Jumpscare inst;
    Texture2D noise;
    float until, blackFrom;
    Color[] px;

    public static void Play(float seconds)
    {
        if (inst == null) inst = new GameObject("Jumpscare").AddComponent<Jumpscare>();
        inst.until = Time.time + seconds;
        inst.blackFrom = Time.time + seconds;
    }

    public static void Black(bool on)
    {
        if (inst == null) inst = new GameObject("Jumpscare").AddComponent<Jumpscare>();
        inst.blackFrom = on ? Time.time : float.MaxValue;
        if (!on) inst.until = 0f;
    }

    void Awake()
    {
        noise = new Texture2D(96, 54, TextureFormat.RGBA32, false);
        noise.filterMode = FilterMode.Point;
        px = new Color[96 * 54];
        blackFrom = float.MaxValue;
    }

    void OnGUI()
    {
        GUI.depth = -1000;
        Rect full = new Rect(0, 0, Screen.width, Screen.height);
        if (Time.time >= blackFrom)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = Color.white;
            return;
        }
        if (Time.time > until) return;

        // Помехи: полупрозрачный «снег» и красная вспышка
        float k = Mathf.PingPong(Time.time * 18f, 1f);
        for (int i = 0; i < px.Length; i++)
        {
            float v = Random.value;
            px[i] = new Color(v, v * 0.9f, v * 0.9f, 0.18f + 0.22f * k);
        }
        noise.SetPixels(px);
        noise.Apply(false);
        GUI.DrawTexture(full, noise);
        if (Random.value < 0.25f)
        {
            GUI.color = new Color(0.6f, 0f, 0f, 0.35f);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
