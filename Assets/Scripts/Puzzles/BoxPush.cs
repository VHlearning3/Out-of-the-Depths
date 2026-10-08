using UnityEngine;
using UnityEngine.InputSystem;

// On the player: swimming into a loose object pushes it.
//  - A Pushable Box (the box puzzle's boxes) is found just ahead in the way you swim, before you bump into it, and
//    moves at your own pace along its side nearest your heading: swim at it and it goes, stop and it stops, with no
//    stop-start bumping (the old push only fired on contact and shoved it off at a fixed speed, so it juddered away
//    from you and you had to catch it up again). While a box moves, Pushing is true, and the hand-drawn push plays over
//    the view (Sprite Push Animator, added at start from Resources/PushSprites; it shows nothing until frames are put in).
//  - Grab: hold the right mouse button while looking at a box (within Grab Reach) and it follows where you look, kept
//    at the distance you grabbed it from: turn and it swings round, swim backwards and you pull it. Let go to drop it.
//  - Any other non-kinematic rigidbody is nudged up to a speed that depends on its mass (Push Strength / mass, never
//    over Max Push Speed), not shoved harder every frame of contact (a default 1 kg cube used to shoot off at the
//    lightest bump), and gets some water drag, so under water it slows down and stops instead of drifting forever.
[RequireComponent(typeof(CharacterController))]
public class BoxPush : MonoBehaviour
{
    [Tooltip("Pushable boxes: how far ahead of the player (m) a box already counts as being pushed.")]
    public float reachAhead = 0.35f;
    [Tooltip("How hard the player pushes other loose objects (kg·m/s): one of this mass or less moves at Max Push Speed, a heavier one slower.")]
    public float pushStrength = 15f;
    [Tooltip("The fastest a push moves another loose object (m/s).")]
    public float maxPushSpeed = 2.5f;
    [Tooltip("Drag a pushed object gets at least, so it slows down in the water (0 = leave its own).")]
    public float waterDrag = 1.5f;
    [Tooltip("Grab: how far from the camera (m) a box can be taken hold of with the right mouse button.")]
    public float grabReach = 3.5f;
    [Tooltip("Grab: the nearest a held box is kept to the player (m, middle to middle), so it never ends up on top of them.")]
    public float holdMin = 1.3f;
    [Tooltip("Grab: let go by itself when the box falls this far (m) further behind than where it was held (stuck on a wall while you swim off).")]
    public float holdSnap = 2.5f;

    private CharacterController controller;
    private SwimController swim;
    private readonly RaycastHit[] ahead = new RaycastHit[8];
    private float pushedAt = -10f;
    private PushableBox held;
    private float holdDistance;
    private Camera view;
    private PlayerInteractor interactor;

    // A box moved under the player's push just now.
    public bool Pushing => Time.time - pushedAt < 0.15f;
    // The box held with the right mouse button, or null.
    public PushableBox Held => held;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        swim = GetComponent<SwimController>();
        PushSprites drawn = Resources.Load<PushSprites>("PushSprites");
        if (drawn != null && GetComponent<SpritePushAnimator>() == null)
            gameObject.AddComponent<SpritePushAnimator>().Setup(drawn, this);
    }

    // The player's flat swim velocity: what they are trying to do, even while something blocks them.
    private Vector3 FlatVelocity()
    {
        Vector3 v = swim != null ? swim.CurrentVelocity : controller.velocity;
        return new Vector3(v.x, 0f, v.z);
    }

    // Level with the box, not above it (or below it): the player's feet under its top and head over its bottom, with a
    // margin. Swimming over a box, or resting on it, must not push it along under you.
    private bool Beside(Collider box)
    {
        const float margin = 0.2f;
        Bounds b = box.bounds;
        float middle = transform.TransformPoint(controller.center).y;
        float half = controller.height * 0.5f * Mathf.Abs(transform.lossyScale.y);
        return middle - half < b.max.y - margin && middle + half > b.min.y + margin;
    }

    // Right mouse down on a box: take hold of it; up (or the box placed, or stuck far behind): let go.
    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (held != null && (mouse == null || !mouse.rightButton.isPressed || held.Placed || !isActiveAndEnabled))
            held = null;
        if (held != null || mouse == null || !mouse.rightButton.wasPressedThisFrame || Time.timeScale <= 0f)
            return;
        if (interactor == null)
            interactor = GetComponent<PlayerInteractor>();
        if (interactor != null && interactor.Busy)
            return;
        Camera eye = View();
        if (eye == null)
            return;
        Ray ray = eye.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, grabReach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return;
        PushableBox box = hit.collider.GetComponentInParent<PushableBox>();
        if (box == null || box.Placed)
            return;
        held = box;
        Vector3 offset = box.transform.position - transform.position;
        offset.y = 0f;
        holdDistance = Mathf.Max(holdMin, offset.magnitude);
    }

    private Camera View()
    {
        if (view == null || !view.isActiveAndEnabled)
        {
            view = GetComponentInChildren<Camera>();
            if (view == null)
                view = Camera.main;
        }
        return view;
    }

    // Held: the point straight ahead where you look, Hold Distance away across the floor, at the box's own height.
    private void Hold()
    {
        Camera eye = View();
        Vector3 look = eye != null ? eye.transform.forward : transform.forward;
        look.y = 0f;
        if (look.sqrMagnitude < 1e-4f)
            look = transform.forward;
        Vector3 target = transform.position + look.normalized * holdDistance;
        Vector3 at = held.transform.position;
        target.y = at.y;
        Vector3 apart = at - transform.position;
        apart.y = 0f;
        if (apart.magnitude > holdDistance + holdSnap)
        {
            held = null;   // caught on something: let go rather than drag it through the wall
            return;
        }
        if (held.HoldToward(target))
            pushedAt = Time.time;
    }

    private void FixedUpdate()
    {
        if (held != null)
        {
            Hold();
            return;   // holding it: it goes where you look, not where you swim
        }
        Vector3 flat = FlatVelocity();
        if (flat.sqrMagnitude < 0.04f)
            return;
        Vector3 heading = flat.normalized;
        Vector3 centre = transform.TransformPoint(controller.center);
        Vector3 up = transform.up * Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        int count = Physics.CapsuleCastNonAlloc(centre - up, centre + up, controller.radius * 0.95f, heading, ahead,
            reachAhead + flat.magnitude * Time.fixedDeltaTime, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            PushableBox box = ahead[i].collider != null ? ahead[i].collider.GetComponentInParent<PushableBox>() : null;
            if (box != null && Beside(ahead[i].collider) && box.PushWith(flat))
                pushedAt = Time.time;
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rigidbody = hit.collider.attachedRigidbody;

        PushableBox box = rigidbody != null ? rigidbody.GetComponent<PushableBox>() : null;
        if (box != null)
        {
            if (box == held)
                return;   // the one in hand moves where you look
            if (Beside(hit.collider) && box.PushWith(FlatVelocity()))   // touching it: in case the look ahead missed it
                pushedAt = Time.time;
            return;
        }

        if (rigidbody == null || rigidbody.isKinematic || hit.moveDirection.y < -0.3f)
            return;   // nothing loose, or it is under the player (standing on it is not pushing it)

        Vector3 direction = hit.moveDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;
        direction.Normalize();

        // Speed it up along the push, only up to what this push can reach: never adds up frame after frame.
        float target = Mathf.Min(maxPushSpeed, pushStrength / Mathf.Max(0.01f, rigidbody.mass));
        float along = Vector3.Dot(rigidbody.linearVelocity, direction);
        if (along < target)
            rigidbody.AddForce(direction * (target - along), ForceMode.VelocityChange);
        if (waterDrag > 0f)
        {
            rigidbody.linearDamping = Mathf.Max(rigidbody.linearDamping, waterDrag);
            rigidbody.angularDamping = Mathf.Max(rigidbody.angularDamping, waterDrag);
        }
    }
}
