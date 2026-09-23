using UnityEngine;

// A fish knocked back by a hit: shoved away from the player, sliding to a stop through the water and never into a wall,
// and dazed for a moment (Stunned: a pufferfish neither chases nor bites until it clears). Slash Attack calls Push on
// every fish it hits. Added to every fish by Fish Controller.
public class FishKnockback : MonoBehaviour
{
    [Tooltip("How quickly the shove dies away in the water, per second. Higher = a shorter slide.")]
    [SerializeField] private float waterDrag = 4f;
    [Tooltip("Wall clearance while sliding, in metres (the Fish Wander's body radius when it has one).")]
    [SerializeField] private float clearance = 0.3f;

    public bool Stunned => Time.time < stunnedUntil;

    private Vector3 velocity;
    private float stunnedUntil;
    private LayerMask walls = ~0;

    private void Awake()
    {
        FishWander wander = GetComponent<FishWander>();
        if (wander != null)
        {
            clearance = wander.BodyRadius;
            walls = wander.ObstacleMask;
        }
    }

    // A shove (metres per second) and how long it is dazed for.
    public void Push(Vector3 shove, float stunSeconds)
    {
        velocity += shove;
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + stunSeconds);
    }

    private void LateUpdate()
    {
        if (velocity.sqrMagnitude < 0.0004f)
        {
            velocity = Vector3.zero;
            return;
        }
        transform.position += FishSteering.ClampMove(transform.position, velocity * Time.deltaTime, clearance, walls);
        velocity *= Mathf.Exp(-waterDrag * Time.deltaTime);
    }
}
