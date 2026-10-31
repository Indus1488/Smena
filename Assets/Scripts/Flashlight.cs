using UnityEngine;

// Фонарик. Появляется в акте 2.
// Q — включить и выключить, T — сменить батарею на запасную.
// Батарея садится, пока фонарик включён. Ниже 20% свет мерцает.
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class Flashlight : MonoBehaviour
{
    const float FullIntensity = 12f;

    Light lamp;
    bool on;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("Flashlight").AddComponent<Flashlight>();
    }

    void Start()
    {
        // Луч крепится к камере игрока
        GameObject beam = new GameObject("FlashlightBeam");
        Camera cam = Camera.main;
        if (cam != null) beam.transform.SetParent(cam.transform, false);
        beam.transform.localPosition = new Vector3(0.15f, -0.1f, 0.2f);

        lamp = beam.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.range = 22f;
        lamp.spotAngle = 60f;
        lamp.innerSpotAngle = 25f;
        lamp.intensity = FullIntensity;
        lamp.color = new Color(1f, 0.95f, 0.8f);
        lamp.shadows = LightShadows.Soft;
        lamp.enabled = false;
    }

    void Update()
    {
        if (lamp == null) return;

        if (!Inventory.hasFlashlight)
        {
            lamp.enabled = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (Inventory.flashlightBattery <= 0f)
                Hud.Say("Батарея села. Нужен запас (T — сменить батарею).", 3f);
            else
                on = !on;
        }

        if (Input.GetKeyDown(KeyCode.T)) ReplaceBattery();

        if (GameConfig.debugKeys && Input.GetKeyDown(KeyCode.Y))
        {
            Inventory.spareBatteries++;
            Hud.Say("Тест: +1 запасная батарея", 2f);
        }

        if (on)
        {
            Inventory.flashlightBattery -= GameConfig.flashlightDrainPerSecond * Time.deltaTime;
            if (Inventory.flashlightBattery <= 0f)
            {
                Inventory.flashlightBattery = 0f;
                on = false;
                Hud.Say("Фонарик погас: батарея села.", 4f);
            }
        }

        lamp.enabled = on;
        if (on)
        {
            float b = Inventory.flashlightBattery;
            float k = 1f;
            if (b < 20f)
            {
                // Мерцание: чем меньше заряд, тем чаще и сильнее провалы света
                k = Mathf.PerlinNoise(Time.time * 12f, 0.3f) > 0.35f ? 1f : 0.15f;
                k *= Mathf.Lerp(0.5f, 1f, b / 20f);
            }
            lamp.intensity = FullIntensity * k;
        }
    }

    void ReplaceBattery()
    {
        if (Inventory.spareBatteries <= 0)
        {
            Hud.Say("Запасных батарей нет.", 2f);
            return;
        }
        Inventory.spareBatteries--;
        Inventory.flashlightBattery = 100f;
        Hud.Say("Батарея заменена.", 2f);
    }
}
