using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Target yang dikejar NPC. Digerakkan pemain dengan WASD / panah, relatif terhadap arah kamera.
/// Shift = lari, supaya target bisa kabur dan proses repathing NPC terlihat jelas.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerTargetMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.8f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private float gravity = -20f;

    [Tooltip("Kosongkan untuk memakai Camera.main.")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        Vector2 input = ReadMoveInput();
        Vector3 move = ToWorldDirection(input);

        float speed = moveSpeed;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.leftShiftKey.isPressed)
            speed *= sprintMultiplier;

        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private static Vector2 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        float x = 0f, y = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    private Vector3 ToWorldDirection(Vector2 input)
    {
        if (cameraTransform == null)
            return new Vector3(input.x, 0f, input.y);

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = cameraTransform.right;
        right.y = 0f;
        right.Normalize();

        return forward * input.y + right * input.x;
    }
}
