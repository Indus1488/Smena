using System.Collections;
using UnityEngine;

// Вешается на ворота центрального склада. В 18:00 ворота медленно поднимаются.
// Свой звук скрипа положите в Assets/Resources под именем gate_creak.
public class CentralGate : MonoBehaviour
{
    public float openHeight = 5.2f;
    public float seconds = 3f;

    public void Open()
    {
        AudioClip creak = Resources.Load<AudioClip>("gate_creak");
        if (creak != null) AudioSource.PlayClipAtPoint(creak, transform.position, 1f);
        StartCoroutine(Slide());
    }

    IEnumerator Slide()
    {
        Vector3 a = transform.position;
        Vector3 b = a + Vector3.up * openHeight;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(a, b, Mathf.Clamp01(t / seconds));
            yield return null;
        }
        transform.position = b;
    }
}
