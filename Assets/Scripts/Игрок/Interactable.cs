using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    public string prompt = "E — открыть";
    public UnityEvent onInteract;

    public void Interact()
    {
        onInteract.Invoke();
    }
}