using UnityEngine;
using UnityEngine.InputSystem;

// First-person swimming: mouse look, WASD swim (pitch changes depth), sprint, and the camera bank/sway/bob feel.
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
    [SerializeField] private float drag = 1.5f;

    [Header("Feel")]
    [SerializeField] private float strafeRollAngle = 4f;
    [SerializeField] private float turnRollAngle = 5f;
    [SerializeField] private float rollSmoothTime = 0.3f;
    [SerializeField] private float swayAmplitude = 1.5f;
    [SerializeField] private float swayFrequency = 0.25f;
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float bobFrequency = 0.35f;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;

    private float pitch;
    private float yaw;
    private Vector3 currentVelocity;
    private float currentRoll;
    private float rollVelocity;
    private float turnRate;
    private Vector3 cameraPivotRestLocalPosition;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cameraPivotRestLocalPosition = cameraPivot.localPosition;

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
        ApplyCameraFeel();
    }

    private void HandleLook()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();
        float yawDelta = look.x * mouseSensitivity;
        yaw += yawDelta;
        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, minPitch, maxPitch);
        turnRate = Time.deltaTime > 0f ? yawDelta / Time.deltaTime : 0f;

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
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

        // Exponential (framerate-independent) easing instead of a linear MoveTowards,
        // so speeding up and coasting to a stop both feel like gliding through water
        // rather than snapping onto a fixed rate.
        float rate = noInput ? drag : acceleration;
        float smoothing = 1f - Mathf.Exp(-rate * Time.deltaTime);
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, smoothing);

        controller.Move(currentVelocity * Time.deltaTime);

        float turnBank = Mathf.Clamp(turnRate, -60f, 60f) / 60f * turnRollAngle;
        float targetRoll = -move.x * strafeRollAngle - turnBank;
        currentRoll = Mathf.SmoothDamp(currentRoll, targetRoll, ref rollVelocity, rollSmoothTime);
    }

    private void ApplyCameraFeel()
    {
        float sway = Mathf.Sin(Time.time * swayFrequency * Mathf.PI * 2f) * swayAmplitude;
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, currentRoll + sway);

        float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        cameraPivot.localPosition = cameraPivotRestLocalPosition + Vector3.up * bob;
    }
}
