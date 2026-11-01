using UnityEngine;

// Ретро-вид в духе PS1: игра рисуется в низком разрешении с чёткими пикселями и растягивается на весь экран.
// Интерфейс (часы, телефон, надписи) остаётся чётким поверх картинки.
// Отключить или поменять разрешение можно в GameConfig (ps1Look, ps1Height).
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class PS1Look : MonoBehaviour
{
    RenderTexture rt;
    Camera cam;
    int lastW, lastH;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!GameConfig.ps1Look) return;
        new GameObject("PS1Look").AddComponent<PS1Look>();
    }

    void Start()
    {
        QualitySettings.antiAliasing = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        Rebuild();
    }

    void Rebuild()
    {
        cam = Camera.main;
        if (cam == null) return;

        if (rt != null)
        {
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
        }

        int h = Mathf.Max(120, GameConfig.ps1Height);
        int w = Mathf.RoundToInt(h * (float)Screen.width / Mathf.Max(1, Screen.height));
        rt = new RenderTexture(w, h, 24);
        rt.filterMode = FilterMode.Point;
        rt.useMipMap = false;
        rt.antiAliasing = 1;
        cam.targetTexture = rt;

        lastW = Screen.width;
        lastH = Screen.height;
    }

    void Update()
    {
        if (cam == null || rt == null || Screen.width != lastW || Screen.height != lastH) Rebuild();
    }

    void OnGUI()
    {
        if (rt == null) return;
        if (Event.current.type != EventType.Repaint) return;
        GUI.depth = 1000;   // позади всего интерфейса
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rt, ScaleMode.StretchToFill, false);
    }
}
