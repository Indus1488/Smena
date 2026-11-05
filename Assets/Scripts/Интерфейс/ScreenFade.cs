using System.Collections;
using UnityEngine;

public class ScreenFade : MonoBehaviour
{
    public static ScreenFade Instance;
    float alpha;
    Texture2D tex;

    void Awake()
    {
        Instance = this;
        tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.black);
        tex.Apply();
    }

    public IEnumerator Fade(float to, float time = 0.4f)
    {
        float from = alpha, t = 0;
        while (t < time)
        {
            t += Time.deltaTime;
            alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        alpha = to;
    }

    void OnGUI()
    {
        if (alpha <= 0f) return;
        GUI.depth = -100;
        GUI.color = new Color(1, 1, 1, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), tex);
    }
}