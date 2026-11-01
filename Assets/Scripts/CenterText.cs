using UnityEngine;

// Крупная надпись по центру экрана, при желании на чёрном фоне.
// Используется для смерти («ВЫ ПОГИБЛИ») и для финала («СМЕНА ОКОНЧЕНА»).
// Вызов: CenterText.Show("текст", секунды, true);
public class CenterText : MonoBehaviour
{
    static CenterText instance;

    string text = "";
    float until;
    bool black;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        instance = new GameObject("CenterText").AddComponent<CenterText>();
    }

    public static void Show(string text, float seconds, bool blackBackground)
    {
        if (instance == null) return;
        instance.text = text;
        instance.until = Time.time + seconds;
        instance.black = blackBackground;
    }

    void OnGUI()
    {
        if (Time.time >= until) return;
        GUI.depth = -200;   // поверх всего

        if (black)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 56;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
        }
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), text, style);
    }
}
