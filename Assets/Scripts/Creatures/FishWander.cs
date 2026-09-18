using UnityEngine;

// Drives the fish's root transform, so the visual mesh underneath can be swapped freely. On its own it wanders around
// its spawn point; under a Fish School it keeps its slot in the pack instead. Steers around walls and never moves into them.
public class FishWander : MonoBehaviour
{
    [Header("Wander")]
    [SerializeField] private float wanderRadius = 3f;
    [SerializeField] private float verticalRange = 1f;
    [SerializeField] private float speed = 1.2f;
    [SerializeField] private float turnSpeed = 2.5f;
    [SerializeField] private float targetReachDistance = 0.4f;
    [SerializeField] private float idleBobAmount = 0.08f;

    [Header("Walls")]
    [Tooltip("What counts as a wall. Other fish are always ignored.")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [Tooltip("Roughly half the fish's body: the clearance it keeps from walls.")]
    [SerializeField] private float bodyRadius = 0.3f;
    [SerializeField] private float lookAhead = 1.5f;

    [Header("Other fish")]
    [Tooltip("Fish closer than this push each other apart, so packs don't overlap. 0 = off.")]
    [SerializeField] private float separationDistance = 0.9f;
    [SerializeField] private float separationStrength = 1.5f;

    [Header("In a pack")]
    [Tooltip("Beyond this distance from its slot a fish pulls hard toward it; within it, it mostly just swims along with the pack.")]
    [SerializeField] private float slotPullDistance = 2f;
    [Tooltip("Extra speed (as a fraction of pack speed) a fish uses to catch up with its slot.")]
    [SerializeField] private float catchUp = 0.8f;
    [Tooltip("Small sideways sway inside the pack so it looks alive.")]
    [SerializeField] private float sway = 0.25f;

    public LayerMask ObstacleMask => obstacleMask;
    public float BodyRadius => bodyRadius;
    public float LookAhead => lookAhead;

    private static readonly Collider[] neighbours = new Collider[16];

    private Vector3 home;
    private Vector3 target;
    private float bobOffset;
    private FishSchool school;
    private Vector3 slotOffset;
    private float speedScale = 1f;
    private bool entering;
    private Vector3[] entryPath;
    private int entryIndex;
    private System.Action arrived;

    private void Awake()
    {
        home = transform.position;
        bobOffset = Random.value * 10f;
        PickNewTarget();
    }

    public void ResetHome(Vector3 position)
    {
        home = position;
        PickNewTarget();
    }

    public void JoinSchool(FishSchool pack, Vector3 offset, float speedMultiplier)
    {
        school = pack;
        slotOffset = offset;
        speedScale = speedMultiplier;
    }

    // Swim through the given points in order (e.g. up a shaft, then out through a hole), ignoring walls on the way,
    // then wander around homeAfter. onArrived fires at the last point (used to park a fish that swam back out).
    public void SwimOut(Vector3[] path, Vector3 homeAfter, System.Action onArrived = null)
    {
        entryPath = path;
        entryIndex = 0;
        entering = path != null && path.Length > 0;
        home = homeAfter;
        arrived = onArrived;
        if (entering)
            target = path[0];
    }

    private void Update()
    {
        if (entering)
        {
            Vector3 toPoint = target - transform.position;
            if (toPoint.magnitude < targetReachDistance)
            {
                entryIndex++;
                if (entryIndex < entryPath.Length)
                {
                    target = entryPath[entryIndex];
                    return;
                }
                entering = false;
                PickNewTarget();
                System.Action callback = arrived;
                arrived = null;
                callback?.Invoke();
                if (!isActiveAndEnabled)
                    return;
            }
            else
            {
                Quaternion entryLook = Quaternion.LookRotation(toPoint.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, entryLook, turnSpeed * Time.deltaTime);
                transform.position += transform.forward * (speed * speedScale * Time.deltaTime);
                return;
            }
        }

        float moveSpeed = speed * speedScale;
        Vector3 desired;
        if (school != null)
        {
            // Flocking: swim along with the pack, get pulled toward your slot the further you are from it, keep apart from neighbours.
            Vector3 slot = school.Center + school.Frame * slotOffset
                           + school.Frame * Vector3.right * (Mathf.Sin(Time.time * 1.3f + bobOffset) * sway);
            Vector3 toSlot = slot - transform.position;
            float gap = toSlot.magnitude;
            float pull = Mathf.Clamp01(gap / Mathf.Max(0.01f, slotPullDistance));
            Vector3 slotDirection = gap > 0.01f ? toSlot / gap : Vector3.zero;

            desired = school.Heading * (1f - 0.6f * pull) + slotDirection * (1.3f * pull) + Separation();
            moveSpeed = school.Speed * speedScale * (1f + catchUp * pull);
        }
        else
        {
            if (Vector3.Distance(transform.position, target) < targetReachDistance)
                PickNewTarget();

            Vector3 toTarget = target - transform.position;
            desired = (toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward) + Separation();
        }

        desired = desired.sqrMagnitude > 0.0001f ? desired.normalized : transform.forward;
        desired = FishSteering.Avoid(transform.position, desired, bodyRadius, lookAhead, obstacleMask);

        Quaternion look = Quaternion.LookRotation(desired, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);

        Vector3 bob = Vector3.up * (Mathf.Sin(Time.time * 2f + bobOffset) * idleBobAmount * Time.deltaTime);
        Vector3 move = transform.forward * (moveSpeed * Time.deltaTime) + bob;
        transform.position += FishSteering.ClampMove(transform.position, move, bodyRadius, obstacleMask);
    }

    // A push away from every other fish inside separationDistance, stronger the closer they are.
    private Vector3 Separation()
    {
        if (separationDistance <= 0f)
            return Vector3.zero;

        Vector3 push = Vector3.zero;
        int count = Physics.OverlapSphereNonAlloc(transform.position, separationDistance, neighbours, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var other = neighbours[i].GetComponentInParent<FishWander>();
            if (other == null || other == this)
                continue;

            Vector3 away = transform.position - other.transform.position;
            float distance = away.magnitude;
            away = distance > 0.001f ? away / distance : Random.insideUnitSphere;
            push += away * (1f - Mathf.Clamp01(distance / separationDistance));
        }
        return push * separationStrength;
    }

    private void PickNewTarget()
    {
        Vector2 flat = Random.insideUnitCircle * wanderRadius;
        float y = Random.Range(-verticalRange, verticalRange);
        target = home + new Vector3(flat.x, y, flat.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (school != null)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.6f);
            Gizmos.DrawLine(transform.position, school.Center + school.Frame * slotOffset);
            return;
        }

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, wanderRadius);
    }
}
