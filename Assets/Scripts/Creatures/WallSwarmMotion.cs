using UnityEngine;

// One fish of a wall of fish (the swarms that block a doorway, a window or the basement hatch until you hack through
// them): it swims with the swarm instead of hanging still. Sway: back and forth across the opening along Across, Sway
// metres either side of where it was placed, turning round at each end (the builder gives neighbouring rows opposite
// phases, so the swarm churns but the gap never opens). Orbit: round and round a centre (over the hatch). A gentle bob
// on top, and it always faces the way it swims. The swarm never overlaps: every frame each fish checks the hitboxes of
// the swarm fish round it (their colliders, as they are turned now) and, where two overlap, both are nudged apart just
// enough (Keep Apart more), settling back into their place in the swarm once clear. Stops for good once the fish is
// killed (it floats up like any dead fish). Added and set by the ship builder.
public class WallSwarmMotion : MonoBehaviour
{
    public enum Mode { Sway, Orbit }

    public Mode mode = Mode.Sway;
    [Tooltip("Sway: the direction across the opening (flat).")]
    public Vector3 across = Vector3.right;
    [Tooltip("Sway: metres either side of where it was placed.")]
    public float sway = 0.5f;
    [Tooltip("Orbit: the point it circles round (same height as the fish is kept).")]
    public Vector3 orbitCentre;
    [Tooltip("Orbit: round the other way.")]
    public bool clockwise;
    [Tooltip("Swim speed: for Sway, swings per second (radians); for Orbit, metres per second along the circle.")]
    public float speed = 1.6f;
    [Tooltip("Where in the swing it starts (radians), so the fish don't all move as one.")]
    public float phase;
    [Tooltip("Up and down bob, metres.")]
    public float bob = 0.06f;
    [Tooltip("Extra gap, metres, kept between two swarm fish once their hitboxes touch.")]
    public float keepApart = 0.04f;

    private static readonly Collider[] near = new Collider[24];
    private Collider body;
    private Vector3 nudge;   // how far it has been pushed off its place by its neighbours

    private Vector3 anchor;
    private float radius;
    private float angle;
    private FishController fish;

    private void Start()
    {
        anchor = transform.position;
        fish = GetComponent<FishController>();
        body = GetComponent<Collider>();
        across.y = 0f;
        across = across.sqrMagnitude > 0.0001f ? across.normalized : Vector3.right;
        Vector3 out_ = anchor - orbitCentre;
        out_.y = 0f;
        radius = Mathf.Max(0.3f, out_.magnitude);
        angle = Mathf.Atan2(out_.z, out_.x);
    }

    private void Update()
    {
        if (fish != null && !fish.IsAlive)
        {
            enabled = false;   // killed: the fish's own death float takes over
            return;
        }

        float t = Time.time;
        float lift = Mathf.Sin(t * speed * 1.7f + phase * 2.3f) * bob;
        Vector3 heading;
        Vector3 position;
        if (mode == Mode.Orbit)
        {
            float direction = clockwise ? -1f : 1f;
            angle += direction * speed / radius * Time.deltaTime;
            position = new Vector3(orbitCentre.x + Mathf.Cos(angle) * radius, anchor.y + lift, orbitCentre.z + Mathf.Sin(angle) * radius);
            heading = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * direction;
        }
        else
        {
            float s = Mathf.Sin(t * speed + phase);
            position = anchor + across * (s * sway) + Vector3.up * lift;
            heading = across * (Mathf.Cos(t * speed + phase) >= 0f ? 1f : -1f);
        }
        Quaternion facing = Quaternion.LookRotation(heading, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, facing, 1f - Mathf.Exp(-7f * Time.deltaTime));
        transform.position = position + nudge;
        KeepClear(position);
    }

    // Out of any swarm fish it overlaps (each of the two takes half the push), and slowly back to its place when clear.
    private void KeepClear(Vector3 place)
    {
        if (body == null || !body.enabled)
            return;
        Vector3 at = transform.position;
        Quaternion turn = transform.rotation;
        int count = Physics.OverlapSphereNonAlloc(at, body.bounds.extents.magnitude * 2f, near, ~0, QueryTriggerInteraction.Ignore);
        Vector3 push = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider other = near[i];
            if (other == null || other == body || other.isTrigger)
                continue;
            WallSwarmMotion mate = other.GetComponent<WallSwarmMotion>();
            if (mate == null || !mate.enabled)
                continue;   // only the swarm: walls and the player are not its business
            if (Physics.ComputePenetration(body, at, turn, other, other.transform.position, other.transform.rotation, out Vector3 away, out float depth))
                push += away * (depth * 0.5f + keepApart);
        }
        if (push != Vector3.zero)
        {
            nudge = Vector3.ClampMagnitude(nudge + push, 0.8f);
            transform.position = place + nudge;
        }
        else
            nudge = Vector3.MoveTowards(nudge, Vector3.zero, 0.25f * Time.deltaTime);
    }
}
