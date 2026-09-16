using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SwimController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform cameraPivot;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Swim Movement")]
    [SerializeField] private float swimSpeed = 3.5f;
    [SerializeField] private float sprintMultiplier = 1.8f;
    [SerializeField] private float acceleration = 4f;
    [SerializeField] private float drag = 3f;

    [Header("Feel")]
    [SerializeField] private float strafeRollAngle = 4f;
    [SerializeField] private float rollSmoothTime = 0.3f;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;

    private float pitch;
    private float yaw;
    private Vector3 currentVelocity;
    private float currentRoll;
    private float rollVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        var playerMap = inputActions.FindActionMap("Player");
        moveAction = playerMap.FindAction("Move");
        lookAction = playerMap.FindAction("Look");
        sprintAction = playerMap.FindAction("Sprint");
    }

    private void OnEnable()
    {
        inputActions.FindActionMap("Player").Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        inputActions.FindActionMap("Player").Disable();
    }

    private void Update()
    {
        HandleLook();
        HandleSwim();
    }

    private void HandleLook()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();
        yaw += look.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, currentRoll);
    }

    private void HandleSwim()
    {
        Vector2 move = moveAction.ReadValue<Vector2>();
        float speedMultiplier = sprintAction.IsPressed() ? sprintMultiplier : 1f;

        // Forward/strafe follow the camera's full pitch, so looking up or down
        // while swimming forward is the only way to change depth - no separate
        // up/down keys needed.
        Vector3 wishDirection = cameraPivot.forward * move.y + transform.right * move.x;
        if (wishDirection.sqrMagnitude > 1f)
            wishDirection.Normalize();

        bool noInput = wishDirection.sqrMagnitude < 0.01f;
        Vector3 targetVelocity = noInput ? Vector3.zero : wishDirection * (swimSpeed * speedMultiplier);
        float rate = noInput ? drag : acceleration;
        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Time.deltaTime);

        controller.Move(currentVelocity * Time.deltaTime);

        float targetRoll = -move.x * strafeRollAngle;
        currentRoll = Mathf.SmoothDamp(currentRoll, targetRoll, ref rollVelocity, rollSmoothTime);
    }
}
