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

    public LayerMask ObstacleMask => obstacleMask;
    public float BodyRadius => bodyRadius;
    public float LookAhead => lookAhead;

    private Vector3 home;
    private Vector3 target;
    private float bobOffset;
    private FishSchool school;
    private Vector3 slotOffset;
    private float speedScale = 1f;

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

    private void Update()
    {
        float moveSpeed = speed * speedScale;
        if (school != null)
        {
            target = school.Center + slotOffset;
            // Catch up when far from the slot, cruise at the pack's pace once in it.
            float gap = Vector3.Distance(transform.position, target);
            moveSpeed = Mathf.Lerp(school.Speed * speedScale, moveSpeed * 1.5f, Mathf.Clamp01((gap - targetReachDistance) / 2f));
        }
        else if (Vector3.Distance(transform.position, target) < targetReachDistance)
        {
            PickNewTarget();
        }

        Vector3 toTarget = target - transform.position;
        Vector3 desired = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
        desired = FishSteering.Avoid(transform.position, desired, bodyRadius, lookAhead, obstacleMask);

        Quaternion look = Quaternion.LookRotation(desired, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);

        Vector3 bob = Vector3.up * (Mathf.Sin(Time.time * 2f + bobOffset) * idleBobAmount * Time.deltaTime);
        Vector3 move = transform.forward * (moveSpeed * Time.deltaTime) + bob;
        transform.position += FishSteering.ClampMove(transform.position, move, bodyRadius, obstacleMask);
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
            Gizmos.DrawLine(transform.position, school.Center + slotOffset);
            return;
        }

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, wanderRadius);
    }
}
