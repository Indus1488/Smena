using UnityEngine;

// Шаги игрока: в складе (ОСБ) step_wood, на грунте двора step_dirt, на бетоне и асфальте step_concrete.
// У каждого можно до 4 вариантов (_1…_4). Нет нужного файла — звучит step_concrete.
// Создаётся сам при запуске игры и ищет объект Player.
public class PlayerFootsteps : MonoBehaviour
{
    Transform player;
    CharacterController cc;
    Vector3 lastPos;
    float walked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("PlayerFootsteps").AddComponent<PlayerFootsteps>();
    }

    void Update()
    {
        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            player = p.transform;
            cc = p.GetComponent<CharacterController>();
            lastPos = player.position;
        }

        Vector3 d = player.position - lastPos;
        d.y = 0f;
        lastPos = player.position;
        if (cc != null && !cc.isGrounded) return;

        walked += d.magnitude;
        if (walked < 1.6f) return;   // один шаг примерно на 1.6 м
        walked = 0f;

        Sfx.PlayRandom(Surface(), 4, 0.5f);
    }

    // По чему идём: ОСБ в складе, грунт/щебень двора, бетон/асфальт. Нет нужного файла — бетон.
    string Surface()
    {
        string s = "step_concrete";
        RaycastHit hit;
        if (Physics.Raycast(player.position + Vector3.up * 0.3f, Vector3.down, out hit, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore))
        {
            string n = hit.collider.name;
            if (n == "Floor" || n.StartsWith("Step")) s = "step_wood";
            else if (n == "Dirt") s = "step_dirt";
        }
        else if (player.position.y > 1.5f) s = "step_wood";   // пол склада выше земли (платформа)
        if (Sfx.Get(s) == null && Sfx.Get(s + "_1") == null) s = "step_concrete";
        return s;
    }
}
