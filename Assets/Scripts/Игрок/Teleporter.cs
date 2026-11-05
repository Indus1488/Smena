using System.Collections;
using UnityEngine;

public class Teleporter : MonoBehaviour
{
    public Transform player;
    public Transform target;

    public void Go()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return ScreenFade.Instance.Fade(1f);

        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.position = target.position;
        player.rotation = target.rotation;
        cc.enabled = true;

        yield return new WaitForSeconds(0.2f);
        yield return ScreenFade.Instance.Fade(0f);
    }
}