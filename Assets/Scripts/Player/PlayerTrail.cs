using UnityEngine;

// Breadcrumbs: the player's recent positions, so a pursuer can follow the exact route through doors and around corners
// without a navmesh. Chase Sequence adds this to the player by itself. Indices stay valid as old points fall off the front.
public class PlayerTrail : MonoBehaviour
{
    [Tooltip("A new point is recorded every this many metres.")]
    [SerializeField] private float spacing = 0.5f;
    [Tooltip("How many points are kept; the oldest are dropped.")]
    [SerializeField] private int capacity = 400;

    private Vector3[] points;
    private int start;      // ring slot of the oldest point
    private int count;
    private int dropped;    // points that have fallen off the front, so indices never shift

    public int Count => count;
    public int Oldest => dropped;
    public int Newest => dropped + count - 1;

    private void Awake()
    {
        points = new Vector3[Mathf.Max(8, capacity)];
    }

    private void OnEnable()
    {
        Clear();
    }

    private void Update()
    {
        if (count == 0 || (transform.position - Get(Newest)).sqrMagnitude >= spacing * spacing)
            Record(transform.position);
    }

    public Vector3 Get(int index)
    {
        if (count == 0)
            return transform.position;
        index = Mathf.Clamp(index, Oldest, Newest);
        return points[(start + (index - dropped)) % points.Length];
    }

    // Forget the route so far (a respawn, a chase starting): the trail begins again where the player is now.
    public void Clear()
    {
        count = 0;
        start = 0;
        dropped = 0;
        Record(transform.position);
    }

    private void Record(Vector3 position)
    {
        if (count < points.Length)
        {
            points[(start + count) % points.Length] = position;
            count++;
            return;
        }
        points[start] = position;
        start = (start + 1) % points.Length;
        dropped++;
    }
}
