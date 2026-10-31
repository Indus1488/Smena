using UnityEngine;

public class Interactor : MonoBehaviour
{
    public float distance = 3f;
    Interactable current;

    void Update()
    {
        current = null;
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, distance))
            current = hit.collider.GetComponentInParent<Interactable>();

        if (current != null && Input.GetKeyDown(KeyCode.E))
            current.Interact();
    }

    void OnGUI()
    {
        if (current == null) return;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 40), current.prompt, style);
    }
}