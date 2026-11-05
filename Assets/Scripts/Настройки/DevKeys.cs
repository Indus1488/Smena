using UnityEngine;

// Тестовые клавиши (работают только в режиме разработки: GameConfig.debugKeys):
//   F2 — +1000 руб.
//   F3 — +8 готовых коробок (как раз на паллету)
//   F4 — перемотать время на 30 минут вперёд
//   F5 — проверка рук: модель встаёт целиком перед камерой, в Console пишется отчёт [Hands]
// (F9 / F10 / F11 — акты, как и раньше.)
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class DevKeys : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("DevKeys").AddComponent<DevKeys>();
    }

    void Update()
    {
        if (!GameConfig.debugKeys || GameMenus.IsBlocking) return;

        if (Input.GetKeyDown(KeyCode.F2))
        {
            Hud.AddMoney(1000);
            Hud.Say("Тест: +1000 руб.", 2f);
        }
        if (Input.GetKeyDown(KeyCode.F3))
        {
            Inventory.boxesReady += 8;
            Hud.Say("Тест: +8 готовых коробок (всего " + Inventory.boxesReady + ")", 2f);
        }
        if (Input.GetKeyDown(KeyCode.F4))
        {
            Hud.minutes += 30f;
            Hud.Say("Тест: +30 минут, сейчас " + Hud.TimeText(), 2f);
        }
        if (Input.GetKeyDown(KeyCode.F5))
        {
            HandsView.Showcase = !HandsView.Showcase;
            Hud.Say(HandsView.Showcase ? "Тест рук: модель перед камерой (F5 — обратно). Отчёт в Console." : "Тест рук выключен.", 4f);
            if (HandsView.Instance != null) HandsView.Instance.Report();
            else Debug.Log("[Hands] HandsView не создан или выключен (см. строки [Hands] выше)");
        }
    }
}
