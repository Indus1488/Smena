using UnityEngine;

// Отложенный штраф: через несколько секунд начальник пишет SMS и списывает деньги.
// Нужен, чтобы ошибка (например, пропущенный пересорт) всплывала не сразу.
public class Penalty : MonoBehaviour
{
    int amount;
    string text, voiceId, spoken;

    // sms — текст в сообщениях (с подробностями); voiceId + spoken — звонок начальника (если заданы)
    public static void Later(float seconds, int rub, string sms, string voiceId = null, string spoken = null)
    {
        Penalty p = new GameObject("Penalty").AddComponent<Penalty>();
        p.amount = rub;
        p.text = sms;
        p.voiceId = voiceId;
        p.spoken = spoken;
        p.Invoke("Fire", seconds);
    }

    void Fire()
    {
        Hud.AddMoney(-amount);
        if (voiceId != null) PhoneCall.Boss(voiceId, spoken ?? text, text);
        else PhoneUI.AddMessage("Начальник", text);
        Destroy(gameObject);
    }
}
