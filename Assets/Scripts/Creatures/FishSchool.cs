using UnityEngine;

// A pack of fish that swim the same route. This object's position is the pack's home: a virtual centre wanders around it
// and every Fish Wander under this object follows the centre from its own slot. Drop fish prefabs in as children,
// or set Fish Prefab + Spawn Count to spawn them at start. Dead fish drop out of the pack and rejoin when they respawn.
public class FishSchool : MonoBehaviour
{
    [Header("Members")]
    [Tooltip("Optional: spawned as children at start, on top of any fish already under this object.")]
    [SerializeField] private GameObject fishPrefab;
    [SerializeField, Min(0)] private int spawnCount = 0;
    [Tooltip("How wide the pack is. Slots are spread this far sideways, less up-down, and further along the swim direction, in the pack's own frame.")]
    [SerializeField] private float spread = 2.5f;
    [Tooltip("Random per-fish speed multiplier (min, max), so the pack doesn't swim in lockstep.")]
    [SerializeField] private Vector2 speedVariation = new Vector2(0.9f, 1.15f);

    [Header("Route")]
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private float verticalRange = 1.5f;
    [SerializeField] private float speed = 1.2f;
    [SerializeField] private float turnSpeed = 1.5f;
    [SerializeField] private float targetReachDistance = 1f;

    [Header("Walls")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [Tooltip("Clearance the pack centre keeps from walls. Keep it around Spread so the whole pack fits.")]
    [SerializeField] private float clearance = 1.5f;
    [SerializeField] private float lookAhead = 4f;

    public Vector3 Center { get; private set; }
    public float Speed => speed;
    public Vector3 Heading => heading;
    // The pack's frame: slots live in it, so the formation turns with the pack instead of staying fixed in the world.
    public Quaternion Frame => Quaternion.LookRotation(heading, Vector3.up);

    private Vector3 heading;
    private Vector3 target;

    private void Awake()
    {
        Center = transform.position;
        heading = transform.forward;
        PickNewTarget();

        for (int i = 0; i < spawnCount && fishPrefab != null; i++)
            Instantiate(fishPrefab, Center + SlotOffset(), Quaternion.LookRotation(heading, Vector3.up), transform);

        foreach (FishWander fish in GetComponentsInChildren<FishWander>())
            Add(fish);
    }

    // Give a fish a slot in the pack (also used by Fish Spawner for fish that swim in from windows).
    public void Add(FishWander fish)
    {
        fish.JoinSchool(this, SlotOffset(), Random.Range(speedVariation.x, speedVariation.y));
    }

    private void Update()
    {
        if (Vector3.Distance(Center, target) < targetReachDistance)
            PickNewTarget();

        Vector3 desired = (target - Center).normalized;
        desired = FishSteering.Avoid(Center, desired, clearance, lookAhead, obstacleMask);
        heading = Vector3.Slerp(heading, desired, turnSpeed * Time.deltaTime).normalized;

        Vector3 move = heading * (speed * Time.deltaTime);
        Center += FishSteering.ClampMove(Center, move, clearance, obstacleMask);
    }

    // A slot in the pack's local frame (x sideways, y up, z along the swim direction): real schools are wide and long, not tall.
    private Vector3 SlotOffset()
    {
        return new Vector3(
            Random.Range(-1f, 1f) * spread,
            Random.Range(-0.35f, 0.35f) * spread,
            Random.Range(-1.4f, 1.4f) * spread);
    }

    private void PickNewTarget()
    {
        Vector2 flat = Random.insideUnitCircle * wanderRadius;
        float y = Random.Range(-verticalRange, verticalRange);
        target = transform.position + new Vector3(flat.x, y, flat.y);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 centre = Application.isPlaying ? Center : transform.position;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, wanderRadius);
        Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.6f);
        Gizmos.DrawWireSphere(centre, spread);
    }
}
