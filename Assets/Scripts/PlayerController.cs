using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraTransform;
    public float speed = 4f;
    public float mouseSensitivity = 2f;
    public float gravity = -20f;

    public static bool MoveLocked;   // в укрытии: смотреть можно, ходить нельзя

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { MoveLocked = false; }

    CharacterController cc;
    float xRot;
    float yVel;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Поворот мышкой
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        xRot = Mathf.Clamp(xRot - my, -85f, 85f);
        cameraTransform.localRotation = Quaternion.Euler(xRot, 0, 0);
        transform.Rotate(0, mx, 0);

        if (MoveLocked) return;

        // Ходьба
        Vector3 move = transform.right * Input.GetAxis("Horizontal")
                     + transform.forward * Input.GetAxis("Vertical");

        // Гравитация
        if (cc.isGrounded && yVel < 0) yVel = -2f;
        yVel += gravity * Time.deltaTime;

        cc.Move((move * speed + Vector3.up * yVel) * Time.deltaTime);
    }
}