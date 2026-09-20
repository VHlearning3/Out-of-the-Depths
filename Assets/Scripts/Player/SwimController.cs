using UnityEngine;
using UnityEngine.InputSystem;

// First-person swimming: mouse look, WASD swim (forward follows where you look, so pitching changes depth),
// Space / Ctrl for straight up / down, sprint, and the camera bank/sway/bob feel. Movement carries momentum:
// slow to get going, long glide when you let go - tune Acceleration and Drag for heavier or lighter water.
// AddImpulse() shoves the player (a bite), AddShake() rattles the camera (a bite, falling rubble).
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
    [Tooltip("Top speed in metres per second.")]
    [SerializeField] private float swimSpeed = 2.2f;
    [SerializeField] private float sprintMultiplier = 1.6f;
    [Tooltip("Speed straight up (Space) and down (Ctrl), in metres per second.")]
    [SerializeField] private float verticalSpeed = 1.6f;
    [Tooltip("How quickly you reach top speed. Lower = heavier, more swimming-like. About 3 / this many seconds to get up to speed.")]
    [SerializeField] private float acceleration = 1.6f;
    [Tooltip("How quickly you glide to a stop with no input. Lower = longer glide.")]
    [SerializeField] private float drag = 0.7f;
    [Tooltip("Stick input smaller than this counts as no input. Stops a drifting gamepad/joystick from swimming you around on its own.")]
    [SerializeField, Range(0f, 0.5f)] private float inputDeadzone = 0.15f;
    [Tooltip("Off = only the keyboard can swim; anything a controller, joystick or other device reports is ignored.")]
    [SerializeField] private bool allowControllerInput = false;
    [Tooltip("Show a small on-screen line with the live swim input, velocity and actual movement, to track down drift.")]
    [SerializeField] private bool showMovementDebug = false;
    [Tooltip("Below this speed (m/s) with no input you simply stop, instead of creeping forever on a glide that never quite ends.")]
    [SerializeField] private float stopSpeed = 0.03f;

    [Header("Feel")]
    [SerializeField] private float strafeRollAngle = 4f;
    [SerializeField] private float turnRollAngle = 5f;
    [SerializeField] private float rollSmoothTime = 0.3f;
    [SerializeField] private float swayAmplitude = 1.5f;
    [SerializeField] private float swayFrequency = 0.25f;
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float bobFrequency = 0.35f;
    [Tooltip("How quickly a camera shake (a bite, falling rubble) dies away, per second.")]
    [SerializeField] private float shakeDecay = 2.5f;
    [Tooltip("How hard a look pull (the chase reveal) drags the view toward its target, per second. The mouse can still fight it.")]
    [SerializeField] private float lookPullRate = 5f;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction upAction;
    private InputAction downAction;

    private float pitch;
    private float yaw;
    private Vector3 currentVelocity;
    private float currentRoll;
    private float rollVelocity;
    private float turnRate;
    private float shake;
    private Vector3 lookPullTarget;
    private float lookPullUntil = -1f;
    private float lookPullActiveRate;
    private Vector3 cameraPivotRestLocalPosition;

    // Current speed as a fraction of top speed, for anything that wants to react to how fast you swim.
    public float Speed01 => swimSpeed > 0f ? Mathf.Clamp01(currentVelocity.magnitude / swimSpeed) : 0f;

    // Debug readouts for the admin panel: what the swim code itself is doing, so any other motion stands out.
    public Vector2 LastMoveInput { get; private set; }
    public Vector2 LastRawMoveInput { get; private set; }
    public Vector3 CurrentVelocity => currentVelocity;
    public bool MovedThisFrame { get; private set; }
    // Hold the player still no matter what (admin panel). If they still move, something else is pushing them.
    public bool Frozen { get; set; }
    // Ignore the mouse (a cutscene). A look pull still steers the view.
    public bool LookLocked { get; set; }
    // Extra camera sway and bob: 0 = normal, 1 = twice as heavy and quicker (PlayerPanic).
    public float PanicSway { get; set; }
    // Scales the swim speed: 1 = normal (PlayerPanic's adrenaline, later maybe a current or a heavy item).
    public float SpeedMultiplier { get; set; } = 1f;

    private float swayPhase;
    private float bobPhase;
    public bool ShowMovementDebug { get => showMovementDebug; set => showMovementDebug = value; }
    public string LastInputDevice { get; private set; } = "none";

    private Vector3 lastDebugPosition;
    private float lastDebugSpeed;

    // A shove from outside (a bite): joins the swim velocity and bleeds off through Drag / Acceleration like any motion.
    public void AddImpulse(Vector3 velocity)
    {
        currentVelocity += velocity;
    }

    // Camera shake, roughly 0..1; the strongest pending shake wins and dies away at Shake Decay per second.
    public void AddShake(float strength)
    {
        shake = Mathf.Max(shake, strength);
    }

    // Draws the view toward a point for a while (the chase reveal). The mouse still works; it just has to fight the pull.
    // Runs on real time, so it keeps working while time is slowed.
    // rate: how hard it pulls per second; 0 = the Look Pull Rate set in the Inspector. Low (1-2) = a slow, dreadful turn.
    public void PullLookToward(Vector3 worldPoint, float seconds, float rate = 0f)
    {
        lookPullTarget = worldPoint;
        lookPullUntil = Time.unscaledTime + seconds;
        lookPullActiveRate = rate > 0f ? rate : lookPullRate;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cameraPivotRestLocalPosition = cameraPivot.localPosition;

        var playerMap = inputActions.FindActionMap("Player");
        moveAction = playerMap.FindAction("Move");
        lookAction = playerMap.FindAction("Look");
        sprintAction = playerMap.FindAction("Sprint");
        upAction = playerMap.FindAction("Jump");
        downAction = playerMap.FindAction("Crouch");
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
        Vector2 look = LookLocked ? Vector2.zero : lookAction.ReadValue<Vector2>();
        float yawDelta = look.x * mouseSensitivity;
        yaw += yawDelta;
        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, minPitch, maxPitch);
        turnRate = Time.deltaTime > 0f ? yawDelta / Time.deltaTime : 0f;

        if (Time.unscaledTime < lookPullUntil)
        {
            Vector3 toTarget = lookPullTarget - cameraPivot.position;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                float targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                float targetPitch = -Mathf.Atan2(toTarget.y, new Vector2(toTarget.x, toTarget.z).magnitude) * Mathf.Rad2Deg;
                float blend = 1f - Mathf.Exp(-(lookPullActiveRate > 0f ? lookPullActiveRate : lookPullRate) * Time.unscaledDeltaTime);
                yaw = Mathf.LerpAngle(yaw, targetYaw, blend);
                pitch = Mathf.Lerp(pitch, Mathf.Clamp(targetPitch, minPitch, maxPitch), blend);
            }
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void HandleSwim()
    {
        Vector2 move = moveAction.ReadValue<Vector2>();
        LastRawMoveInput = move;
        InputDevice device = moveAction.activeControl != null ? moveAction.activeControl.device : null;
        LastInputDevice = device != null ? device.displayName : "none";
        if (move.sqrMagnitude < inputDeadzone * inputDeadzone)
            move = Vector2.zero;
        // Only the keyboard may swim unless controllers are explicitly allowed: a phantom or drifting device can't move us.
        if (!allowControllerInput && device != null && !(device is Keyboard))
            move = Vector2.zero;
        LastMoveInput = move;
        float speedMultiplier = (sprintAction.IsPressed() ? sprintMultiplier : 1f) * Mathf.Max(0f, SpeedMultiplier);

        // Forward/strafe follow the camera's full pitch, so looking up or down while swimming forward changes depth.
        Vector3 wishVelocity = (cameraPivot.forward * move.y + transform.right * move.x) * (swimSpeed * speedMultiplier);

        // Space / Ctrl: straight up / down in world space, on top of whatever WASD is doing.
        float vertical = (IsKeyboardPressed(upAction) ? 1f : 0f) - (IsKeyboardPressed(downAction) ? 1f : 0f);
        wishVelocity += Vector3.up * (vertical * verticalSpeed * speedMultiplier);

        // Diagonals never exceed the top speed.
        float maxSpeed = Mathf.Max(swimSpeed, verticalSpeed) * speedMultiplier;
        if (wishVelocity.magnitude > maxSpeed)
            wishVelocity = wishVelocity.normalized * maxSpeed;

        bool noInput = wishVelocity.sqrMagnitude < 0.0001f;

        // Exponential (framerate-independent) easing: speeding up, changing direction and coasting to a stop all
        // carry momentum, so a turn swings wide instead of snapping like footsteps would.
        float rate = noInput ? drag : acceleration;
        float smoothing = 1f - Mathf.Exp(-rate * Time.deltaTime);
        currentVelocity = Vector3.Lerp(currentVelocity, wishVelocity, smoothing);

        // The exponential glide never reaches exactly zero; once it is slower than a crawl, stop for real.
        if (Frozen || (noInput && currentVelocity.sqrMagnitude < stopSpeed * stopSpeed))
            currentVelocity = Vector3.zero;

        MovedThisFrame = currentVelocity.sqrMagnitude > 0f;
        if (MovedThisFrame)
            controller.Move(currentVelocity * Time.deltaTime);

        float turnBank = Mathf.Clamp(turnRate, -60f, 60f) / 60f * turnRollAngle;
        float targetRoll = -move.x * strafeRollAngle - turnBank;
        currentRoll = Mathf.SmoothDamp(currentRoll, targetRoll, ref rollVelocity, rollSmoothTime);

        if (showMovementDebug && Time.deltaTime > 0f)
        {
            lastDebugSpeed = (transform.position - lastDebugPosition).magnitude / Time.deltaTime;
            lastDebugPosition = transform.position;
        }
    }

    private bool IsKeyboardPressed(InputAction action)
    {
        if (action == null || !action.IsPressed())
            return false;
        return allowControllerInput || action.activeControl == null || action.activeControl.device is Keyboard;
    }

    private void OnGUI()
    {
        if (!showMovementDebug)
            return;

        string line = $"swim  raw {LastRawMoveInput:0.00} ({LastInputDevice})  used {LastMoveInput:0.00}  vel {currentVelocity:0.00}  Move() {(MovedThisFrame ? "yes" : "no")}  actual {lastDebugSpeed:0.000} m/s";
        GUI.Label(new Rect(10f, Screen.height - 28f, Screen.width - 20f, 22f), line);
    }

    private void ApplyCameraFeel()
    {
        shake = Mathf.MoveTowards(shake, 0f, shakeDecay * Time.deltaTime);
        float jolt = shake * shake;   // eases out instead of stopping dead

        // Phases accumulate so a change of speed (panic) never makes the motion jump.
        float panic = Mathf.Max(0f, PanicSway);
        swayPhase += Time.deltaTime * swayFrequency * (1f + 0.6f * panic);
        bobPhase += Time.deltaTime * bobFrequency * (1f + 0.8f * panic);
        float sway = Mathf.Sin(swayPhase * Mathf.PI * 2f) * swayAmplitude * (1f + panic);
        float shakePitch = (Random.value - 0.5f) * 4f * jolt;
        float shakeRoll = (Random.value - 0.5f) * 6f * jolt;
        cameraPivot.localRotation = Quaternion.Euler(pitch + shakePitch, 0f, currentRoll + sway + shakeRoll);

        float bob = Mathf.Sin(bobPhase * Mathf.PI * 2f) * bobAmplitude * (1f + panic);
        cameraPivot.localPosition = cameraPivotRestLocalPosition + Vector3.up * bob + Random.insideUnitSphere * (0.08f * jolt);
    }
}
