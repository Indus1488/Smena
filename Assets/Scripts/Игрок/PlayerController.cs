using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraTransform;
    public float speed = 4f;
    public float sprintMultiplier = 1.7f;   // Shift — бег: во столько раз быстрее шага
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
        if (PalletCargo.Carried != null) mx *= GameConfig.jackTurnFactor;   // с паллетой на рохле разворачиваешься тяжело
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        xRot = Mathf.Clamp(xRot - my, -85f, 85f);
        cameraTransform.localRotation = Quaternion.Euler(xRot, 0, 0);
        transform.Rotate(0, mx, 0);

        if (MoveLocked) return;

        // Ходьба: по диагонали (W + A/D) не быстрее, чем прямо
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        input = Vector3.ClampMagnitude(input, 1f);
        Vector3 move = transform.right * input.x + transform.forward * input.z;

        // Бег на Shift
        float s = speed;
        if (Inventory.Coffee) s *= 1.2f;   // кофе из магазина
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) s *= sprintMultiplier;

        // Гравитация
        if (cc.isGrounded && yVel < 0) yVel = -2f;
        yVel += gravity * Time.deltaTime;

        cc.Move((move * s + Vector3.up * yVel) * Time.deltaTime);
    }
}