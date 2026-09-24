using UnityEngine;

// Drives the fish's root transform, so the visual mesh underneath can be swapped freely. On its own it wanders around
// its spawn point; under a Fish School it keeps its slot in the pack instead. Steers around walls and never moves into them.
// Attacked nearby (a slash close to it, or a fish near it hit: Slash Attack calls ScareAround), it darts away from the
// player for a moment, fast and turning sharply, then a pack fish drifts back to its pack and a lone one settles where
// it fled to. A pufferfish chasing the player has this switched off while it chases, so it keeps coming.
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

    [Header("Fleeing")]
    [Tooltip("Dart away from the player when attacked nearby.")]
    [SerializeField] private bool fleesWhenAttacked = true;
    [Tooltip("How much faster than its normal speed it flees.")]
    [SerializeField] private float fleeSpeedMultiplier = 1.6f;   // just under the player's swim speed: a chase you can win
    [Tooltip("Roughly how long it flees, in seconds.")]
    [SerializeField] private float fleeSeconds = 1.8f;

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
    public bool IsEntering => entering;

    private static readonly Collider[] neighbours = new Collider[16];
    private static readonly System.Collections.Generic.List<FishWander> awake = new System.Collections.Generic.List<FishWander>();
    private float fleeUntil = -1f;
    private float fleeStarted;
    private Vector3 fleeFrom;

    public bool Fleeing => Time.time < fleeUntil;

    // Every fish swimming about within radius of centre flees from threat (the player).
    public static void ScareAround(Vector3 centre, float radius, Vector3 threat)
    {
        float squared = radius * radius;
        foreach (FishWander fish in awake)
            if (fish != null && fish.fleesWhenAttacked && (fish.transform.position - centre).sqrMagnitude <= squared)
                fish.Scare(threat);
    }

    public void Scare(Vector3 threat)
    {
        if (!fleesWhenAttacked || entering)
            return;
        if (!Fleeing)
            fleeStarted = Time.time;
        fleeFrom = threat;
        fleeUntil = Time.time + fleeSeconds * Random.Range(0.8f, 1.2f);
    }

    private void OnEnable() => awake.Add(this);

    private void OnDisable()
    {
        awake.Remove(this);
        fleeUntil = -1f;
    }

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
        // Hard spacing from other fish (Fish Space), on top of the soft Separation below.
        FishSpace space = GetComponent<FishSpace>() != null ? GetComponent<FishSpace>() : gameObject.AddComponent<FishSpace>();
        space.Setup(bodyRadius, obstacleMask);
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
        float turn = turnSpeed;
        Vector3 desired;
        bool wasFleeing = fleeUntil > 0f;
        if (Fleeing)
        {
            // Away from the player, mostly sideways (not straight up or down), fast at first and easing off.
            Vector3 away = transform.position - fleeFrom;
            away.y *= 0.3f;
            if (away.sqrMagnitude < 0.01f)
                away = transform.forward;
            float left = Mathf.Clamp01((fleeUntil - Time.time) / Mathf.Max(0.01f, fleeUntil - fleeStarted));
            desired = away.normalized + Separation();
            moveSpeed = speed * speedScale * Mathf.Lerp(1.2f, fleeSpeedMultiplier, left);
            turn = turnSpeed * 3f;
        }
        else if (wasFleeing)
        {
            // Done fleeing: a lone fish makes its new home where it ended up; a pack fish heads back to its slot.
            fleeUntil = -1f;
            if (school == null)
                ResetHome(transform.position);
            desired = transform.forward;
        }
        else if (school != null)
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
        if (desired.sqrMagnitude < 0.0001f)
            desired = transform.forward;

        Quaternion look = Quaternion.LookRotation(desired, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turn * Time.deltaTime);

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
