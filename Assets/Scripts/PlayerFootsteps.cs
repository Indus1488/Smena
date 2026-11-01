using UnityEngine;

// Шаги игрока. Внутри склада (на ОСБ) звучит step_wood, снаружи step_concrete.
// Если файла step_wood нет, везде звучит step_concrete.
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

        bool inside = player.position.y > 1.5f;   // пол склада выше земли (платформа)
        string clip = (inside && Sfx.Get("step_wood") != null) ? "step_wood" : "step_concrete";
        Sfx.Play(clip, 0.5f);
    }
}
