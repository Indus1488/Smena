using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Яркость из настроек (Esc → Настройки → Яркость). Работает при любой графике: своя постобработка с экспозицией.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class Brightness : MonoBehaviour
{
    public const string Key = "brightness";      // -1 (темнее) .. 0 (как задумано) .. 1 (светлее)
    public static float Value
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(Key, 0f), -1f, 1f); }
        set { PlayerPrefs.SetFloat(Key, Mathf.Clamp(value, -1f, 1f)); }
    }

    // Нужна ли камере постобработка ради яркости (WarmLook включает её и для «тёплой» графики)
    public static bool NeedsPost { get { return Mathf.Abs(Value) > 0.01f; } }

    Volume volume;
    ColorAdjustments ca;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("Brightness").AddComponent<Brightness>();
    }

    void Start()
    {
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();
        ca = p.Add<ColorAdjustments>(true);
        volume.sharedProfile = p;
    }

    void Update()
    {
        if (ca == null) return;
        ca.postExposure.Override(Value * 1.5f);

        Camera cam = Camera.main;
        if (cam == null) return;
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data != null && NeedsPost && !data.renderPostProcessing) data.renderPostProcessing = true;
    }
}
