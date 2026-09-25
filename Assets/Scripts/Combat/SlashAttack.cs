using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Left-click melee, as the GDD storyboard draws it (POV: slashing with dagger): the blade sweeps a wide arc in front
// of the camera and cuts through everything along it, so one slash takes several fish of a school at once. Clicks
// alternate between the two slashes: left-right (a horizontal sweep from right to left) and up-down (a vertical chop
// from top to bottom); a pause longer than Combo Reset starts again with left-right. Each swing traces its arc over
// Swing Seconds, damaging whatever the blade passes through (each target once per swing); a click during a swing or
// its recovery is remembered and throws the next one as soon as it can, so slashes chain. Hands: Hand Animator's
// PlaySlash (and PlaySlash(direction) when it implements IDirectionalHandAnimator) is where the art goes.
public class SlashAttack : MonoBehaviour
{
    public enum Direction { LeftRight, UpDown }

    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private MonoBehaviour handAnimator;

    [Header("Weapon")]
    [Tooltip("Slashing needs a Weapon item (dagger, trident) in the inventory. Off = always armed, for testing.")]
    [SerializeField] private bool requireWeapon = true;
    [Tooltip("Shown only while a weapon is owned, e.g. the dagger placeholder under Hands. Empty = the hand animator's object.")]
    [SerializeField] private GameObject weaponVisual;

    [Header("Slash")]
    [SerializeField] private bool canAttack = true;
    [Tooltip("Damage to what one slash hits. 20: a wandering fish (40 HP) takes two, a pufferfish (60 HP) three.")]
    [SerializeField] private float slashDamage = 20f;
    [Tooltip("How far the blade reaches from the eyes, in metres.")]
    [SerializeField] private float reach = 2.6f;
    [Tooltip("How thick the blade's path is, in metres: bigger = easier to hit.")]
    [SerializeField] private float bladeRadius = 0.3f;
    [Tooltip("How wide the left-right sweep is, in degrees (it runs from right to left through the middle of the view).")]
    [SerializeField] private float sweepArc = 120f;
    [Tooltip("How tall the up-down chop is, in degrees (top to bottom through the middle of the view).")]
    [SerializeField] private float chopArc = 100f;
    [Tooltip("Seconds the blade takes to cross its arc.")]
    [SerializeField] private float swingSeconds = 0.14f;
    [Tooltip("Seconds after a swing before the next one can start.")]
    [SerializeField] private float recoverSeconds = 0.16f;
    [Tooltip("A click this long before the next slash is allowed still counts: it throws the slash the moment it can.")]
    [SerializeField] private float inputBuffer = 0.3f;
    [Tooltip("After this long without slashing, the next one is left-right again.")]
    [SerializeField] private float comboReset = 1f;
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("Fish within this distance of the swing dart away when you slash (metres).")]
    [SerializeField] private float scareRadius = 2.5f;
    [Tooltip("How hard a hit shoves a fish away from you (metres per second at first) and how long it is dazed (a pufferfish cannot chase or bite meanwhile).")]
    [SerializeField] private float knockback = 6f;
    [SerializeField] private float knockbackStun = 0.7f;
    [Tooltip("When a slash hits, every fish within this distance of what it hit darts away (metres).")]
    [SerializeField] private float hitScareRadius = 4.5f;

    [Header("Feedback")]
    [SerializeField] private AudioClip swingSound;
    [SerializeField, Range(0f, 1f)] private float swingVolume = 0.3f;
    [SerializeField, Range(0f, 0.5f)] private float pitchVariation = 0.12f;
    [Tooltip("Camera jolt when a slash connects (0..1), a bit more for each extra thing it cuts.")]
    [SerializeField, Range(0f, 1f)] private float hitShake = 0.18f;
    [Tooltip("Until the hand art shows the slashes: a see-through ribbon over the area each slash sweeps (Slash Trail, added at start), warm when it cuts something.")]
    [SerializeField] private bool placeholderTrail = true;

    [Header("Hit Feedback")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.6f;
    [SerializeField] private HitMarker hitMarker;

    [Header("Events")]
    public UnityEvent onSlashStarted = new UnityEvent();
    public UnityEvent onSlashHit = new UnityEvent();

    public bool HasWeapon => !requireWeapon || (inventory != null && inventory.HasCategory(ItemDefinition.Category.Weapon));
    public bool CanAttack => canAttack;
    public bool Swinging => swingT >= 0f;
    // The slash being thrown (or last thrown): what the hand art should show.
    public Direction LastDirection { get; private set; }
    // For the placeholder trail (or anything that draws the swing): where the blade is along its arc, in degrees.
    public Transform Origin => attackOrigin;
    public float Reach => reach;
    public float BladeRadius => bladeRadius;
    public float BladeAngle => bladeAngle;
    public float ArcStartAngle => -Arc() * 0.5f;
    public int HitsThisSwing => hitThisSwing.Count;

    private InputAction attackAction;
    private IHandAnimator hands;
    private PlayerInventory inventory;
    private SwimController swimmer;
    private AudioSource audioSource;
    private readonly RaycastHit[] hitResults = new RaycastHit[32];
    private readonly Collider[] overlapResults = new Collider[32];
    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

    private float swingT = -1f;          // 0..1 through the current swing, -1 = none
    private float readyAt;               // when the next slash may start
    private float queuedAt = -10f;       // when a click last came in that has not been used yet
    private float lastSlashAt = -10f;
    private Direction next = Direction.LeftRight;
    private float bladeAngle;            // where the blade was last frame, in degrees along the arc
    private bool hitSomething;

    private void Awake()
    {
        attackAction = inputActions.FindActionMap("Player").FindAction("Attack");
        hands = handAnimator as IHandAnimator;
        inventory = GetComponentInParent<PlayerInventory>();
        swimmer = GetComponentInParent<SwimController>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (placeholderTrail && GetComponent<SlashTrail>() == null)
            gameObject.AddComponent<SlashTrail>();
        // The weapon's real model in the hand (dagger, trident) in place of the placeholder stick.
        if (handAnimator != null && handAnimator.GetComponent<HeldWeapon>() == null)
            handAnimator.gameObject.AddComponent<HeldWeapon>().Setup(inventory, weaponVisual);

        if (handAnimator != null && hands == null)
            Debug.LogError($"{name}: Hand Animator must implement IHandAnimator.", this);
        if (requireWeapon && inventory == null)
            Debug.LogWarning($"{name}: no Player Inventory on the player, so no weapon can be owned and slashing stays locked.", this);
    }

    private void OnEnable()
    {
        attackAction.performed += OnAttackPerformed;
        if (inventory != null)
            inventory.onChanged.AddListener(RefreshWeapon);
        RefreshWeapon();
    }

    private void OnDisable()
    {
        attackAction.performed -= OnAttackPerformed;
        if (inventory != null)
            inventory.onChanged.RemoveListener(RefreshWeapon);
        swingT = -1f;
    }

    public void SetCanAttack(bool value)
    {
        canAttack = value;
        if (!value)
            queuedAt = -10f;
    }

    private void RefreshWeapon()
    {
        GameObject visual = weaponVisual != null ? weaponVisual : (handAnimator != null ? handAnimator.gameObject : null);
        if (visual != null)
            visual.SetActive(HasWeapon);
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        if (canAttack && HasWeapon)
            queuedAt = Time.time;
    }

    private void Update()
    {
        if (Swinging)
            Swing();
        if (!Swinging && Time.time >= readyAt && Time.time - queuedAt <= inputBuffer && canAttack && HasWeapon)
        {
            queuedAt = -10f;
            StartSlash();
        }
    }

    private void StartSlash()
    {
        if (Time.time - lastSlashAt > comboReset)
            next = Direction.LeftRight;
        LastDirection = next;
        next = next == Direction.LeftRight ? Direction.UpDown : Direction.LeftRight;
        lastSlashAt = Time.time;

        swingT = 0f;
        bladeAngle = -Arc() * 0.5f;
        hitSomething = false;
        hitThisSwing.Clear();

        if (hands is IDirectionalHandAnimator directional)
            directional.PlaySlash(LastDirection);
        else
            hands?.PlaySlash();
        onSlashStarted.Invoke();

        if (swingSound != null)
        {
            audioSource.pitch = Random.Range(1f - pitchVariation, 1f + pitchVariation) * (LastDirection == Direction.UpDown ? 0.92f : 1f);
            audioSource.PlayOneShot(swingSound, swingVolume);
        }
        Cut(bladeAngle, bladeAngle);   // point blank at the very start
        if (scareRadius > 0f)
            FishWander.ScareAround(attackOrigin.position + attackOrigin.forward * (reach * 0.5f), scareRadius, attackOrigin.position);
    }

    private float Arc() => LastDirection == Direction.LeftRight ? sweepArc : chopArc;

    // One frame of the swing: the blade moves on along its arc (fast at first, easing at the end) and cuts along the
    // stretch it crossed.
    private void Swing()
    {
        swingT = swingSeconds > 0f ? Mathf.Min(1f, swingT + Time.deltaTime / swingSeconds) : 1f;
        float angle = Mathf.Lerp(-Arc() * 0.5f, Arc() * 0.5f, 1f - (1f - swingT) * (1f - swingT));
        Cut(bladeAngle, angle);
        bladeAngle = angle;
        if (swingT >= 1f)
        {
            swingT = -1f;
            readyAt = Time.time + recoverSeconds;
            if (hitSomething)
            {
                hitMarker?.Show();
                onSlashHit.Invoke();
            }
        }
    }

    // The blade between two points of its arc, a few degrees at a time: along each line it hits the first living thing
    // only, and nothing behind a wall, so it never cuts through one fish into the next. Fish side by side across the
    // sweep are each on a line of their own, so one slash can still take several.
    private void Cut(float fromAngle, float toAngle)
    {
        int before = hitThisSwing.Count;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(toAngle - fromAngle) / 6f));
        for (int i = 0; i <= steps; i++)
        {
            Vector3 direction = BladeDirection(Mathf.Lerp(fromAngle, toAngle, (float)i / steps));
            IDamageable first = FirstInLine(attackOrigin.position, direction);
            if (first != null && hitThisSwing.Add(first))
            {
                first.TakeDamage(slashDamage);
                if (first is Component struck)
                {
                    FishKnockback shoved = struck.GetComponentInParent<FishKnockback>();
                    if (shoved != null && knockback > 0f)
                    {
                        Vector3 away = struck.transform.position - attackOrigin.position;
                        away = (away.sqrMagnitude > 0.0001f ? away.normalized : attackOrigin.forward) + direction * 0.5f;
                        shoved.Push(away.normalized * knockback, knockbackStun);
                    }
                    if (hitScareRadius > 0f)
                        FishWander.ScareAround(struck.transform.position, hitScareRadius, attackOrigin.position);   // the rest scatter
                }
            }
        }
        int cut = hitThisSwing.Count - before;
        if (cut <= 0)
            return;
        if (hitSound != null)
        {
            audioSource.pitch = Random.Range(1f - pitchVariation, 1f + pitchVariation);
            audioSource.PlayOneShot(hitSound, hitVolume);
        }
        if (swimmer != null && hitShake > 0f)
            swimmer.AddShake(Mathf.Min(1f, hitShake * (1f + 0.25f * (hitThisSwing.Count - 1))));
        hitSomething = true;
    }

    // The blade's direction at a point of its arc: left-right turns about the view's up axis from the right side to
    // the left, up-down about its right axis from the top down.
    private Vector3 BladeDirection(float angle) => BladeDirectionAt(angle, LastDirection);

    public Vector3 BladeDirectionAt(float angle, Direction direction)
    {
        Transform eye = attackOrigin;
        Quaternion turn = direction == Direction.LeftRight
            ? Quaternion.AngleAxis(-angle, eye.up)
            : Quaternion.AngleAxis(angle, eye.right);
        return turn * eye.forward;
    }

    // The one thing a line of the swing hits: the nearest living target along it, unless something solid (a wall)
    // comes first. Dead fish do not shield the living; the player's own colliders never count.
    private IDamageable FirstInLine(Vector3 origin, Vector3 direction)
    {
        IDamageable best = null;
        float bestDistance = float.MaxValue;
        float wall = float.MaxValue;

        // Point blank: a sphere cast ignores what it starts inside, so what is right in front of the face is checked
        // apart (targets only: a wall there is found by the cast below).
        int overlaps = Physics.OverlapSphereNonAlloc(origin + direction * bladeRadius, bladeRadius, overlapResults, hitMask, QueryTriggerInteraction.Collide);
        for (int k = 0; k < overlaps; k++)
        {
            IDamageable target = Target(overlapResults[k]);
            float distance = Vector3.Distance(origin, overlapResults[k].bounds.ClosestPoint(origin));
            if (target != null && distance < bestDistance)
            {
                best = target;
                bestDistance = distance;
            }
        }

        int casts = Physics.SphereCastNonAlloc(origin, bladeRadius, direction, hitResults, reach, hitMask, QueryTriggerInteraction.Collide);
        for (int k = 0; k < casts; k++)
        {
            RaycastHit hit = hitResults[k];
            if (hit.distance <= 0f && hit.point == Vector3.zero)
                continue;   // it started inside this one: the overlap above has it
            IDamageable target = Target(hit.collider);
            if (target != null)
            {
                if (hit.distance < bestDistance)
                {
                    best = target;
                    bestDistance = hit.distance;
                }
            }
            else if (!hit.collider.isTrigger && !IsOwn(hit.collider) && hit.distance < wall)
            {
                wall = hit.distance;
            }
        }
        return best != null && bestDistance <= wall ? best : null;
    }

    // Something that can be hurt (and is still alive), or null.
    private IDamageable Target(Collider hit)
    {
        if (IsOwn(hit))
            return null;
        var damageable = hit.GetComponentInParent<IDamageable>();
        if (damageable is Damageable health && health.IsDead)
            return null;
        return damageable;
    }

    // Our own colliders. transform.root is not safe here: the player shares a parent object with the level, so it
    // would exclude everything in the scene.
    private bool IsOwn(Collider hit) => hit.transform == transform || hit.transform.IsChildOf(transform);

    // Scene view: the two arcs, from the attack origin.
    private void OnDrawGizmosSelected()
    {
        if (attackOrigin == null)
            return;
        Direction keep = LastDirection;
        Color[] colours = { new Color(1f, 0.6f, 0.2f, 0.8f), new Color(0.3f, 0.8f, 1f, 0.8f) };
        foreach (Direction direction in new[] { Direction.LeftRight, Direction.UpDown })
        {
            LastDirection = direction;
            Gizmos.color = colours[(int)direction];
            float arc = Arc();
            Vector3 previous = attackOrigin.position + BladeDirection(-arc * 0.5f) * reach;
            for (int i = 1; i <= 16; i++)
            {
                Vector3 point = attackOrigin.position + BladeDirection(Mathf.Lerp(-arc * 0.5f, arc * 0.5f, i / 16f)) * reach;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
        LastDirection = keep;
    }
}
