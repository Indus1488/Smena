using UnityEngine;

// Мелкие события по времени смены, чтобы первый час не был пустым.
// Начало смены — начальник звонит и коротко объясняет, что делать.
// 17:00 — начальник звонит и спрашивает, как дела с нормой (текст зависит от того, сколько уже отправлено).
// Создаётся сам при запуске игры, на сцену ничего перетаскивать не нужно.
public class MicroEvents : MonoBehaviour
{
    bool bossDone, helloDone;
    float helloAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("MicroEvents").AddComponent<MicroEvents>();
    }

    void Update()
    {
        if (ActController.Act != 1) return;

        // Начало смены: через несколько секунд после меню звонит начальник
        if (!helloDone && !GameMenus.IsBlocking)
        {
            if (helloAt < 0f) helloAt = Time.time + 4f;
            if (Time.time >= helloAt)
            {
                helloDone = true;
                PhoneCall.Boss("boss_hello",
                    "Алло. Смена до восьми, ты сегодня один. Сначала прибери склад: швабра у левой стены, мешок — в контейнер под эстакадой. Потом пойдут селлеры. Всё остальное я тебе в сообщениях расписал.",
                    "Смена до восьми. Сначала прибери склад: швабра у левой стены, мешок — в контейнер под эстакадой. Подробности — в сообщениях выше.");
            }
        }

        if (!bossDone && Hud.minutes >= 17f * 60f)
        {
            bossDone = true;
            int sent = Inventory.palletsShipped, need = GameConfig.quotaPallets;
            if (sent >= need)
                PhoneCall.Boss("boss_check_done", "Норму уже сделал? Красавчик. Досиди до восьми и свободен.");
            else if (sent > 0 || Inventory.palletsReady > 0)
                PhoneCall.Boss("boss_check_going", "Ну как идёт? Помнишь про норму до восьми? Не тяни с газелью.",
                               "Как идёт? Помню, норма " + Pallets(need) + " до восьми. Не тяни с газелью.");
            else
                PhoneCall.Boss("boss_check_nothing", "Ты там вообще работаешь? Час прошёл, ни одной паллеты. Норму не сделаешь — штраф.",
                               "Ты там вообще работаешь? Час прошёл, ни одной паллеты. Норма " + Pallets(need) + " до восьми, не сделаешь — штраф.");
        }
    }

    static string Pallets(int n)
    {
        int m10 = n % 10, m100 = n % 100;
        string w = (m10 == 1 && m100 != 11) ? "паллета" : (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) ? "паллеты" : "паллет";
        return n + " " + w;
    }
}
