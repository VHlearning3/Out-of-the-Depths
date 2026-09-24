using System.Collections.Generic;
using UnityEngine;

// A window fish come in through. Drop the prefab on a wall (blue arrow pointing into the room), make it a child of a
// Fish Spawner - or drag it into the spawner's Openings - and duplicate it for more windows. On box walls it cuts its own
// hole (at Play, or bake it with the inspector button); modelled hulls need the hole in the model.
// Holds that window's entry path: the fish appears behind and below (out of sight), rises to the opening, swims through and fans out.
// It also works as a hole in a ceiling: point the blue arrow down into the room and the fish start above instead,
// out of sight over the roof, and come down through it (the middle room's roof holes).
// With a model on it that has colliders (the porthole: its frame and its broken glass), fish only come through where a
// fish fits past them - the hole broken in the glass - lining up square to the window before they cross it. The spots
// are worked out once, the first time a fish needs one; without such colliders it is anywhere in Opening Scatter.
public class FishWindow : MonoBehaviour
{
    [Header("Hole")]
    [Tooltip("Size of the opening cut through a box wall behind this window (match the rim art).")]
    [SerializeField] private Vector2 holeSize = new Vector2(2.6f, 1.6f);
    [Tooltip("Cut the hole automatically when the game starts. Use the button below (or Tools > Out of the Depths > Cut Holes) to bake it into the scene instead.")]
    [SerializeField] private bool cutHoleAtStart = true;
    [Tooltip("After cutting, line the hole through the wall with a frame of this thickness so the cut edges look finished. 0 = none.")]
    [SerializeField] private float sleeveThickness = 0.08f;
    [SerializeField] private Color sleeveColor = new Color(0.22f, 0.2f, 0.18f);
    [Tooltip("For a round frame (the porthole): how far its outer edge is from the middle, in metres. The corners of the square hole that stick out past it are filled in (with the sleeve), so the hole can be as big as the frame's opening. 0 = none.")]
    [SerializeField] private float frameEdge = 0f;

    [Header("Entry path")]
    [Tooltip("How far behind the opening the fish starts (past the hull's outer face).")]
    [SerializeField] private float startDepth = 1.8f;
    [Tooltip("How far below the opening the fish starts, so it is out of every sightline until it rises into view.")]
    [SerializeField] private float startDrop = 6f;
    [Tooltip("How far into the room the fish swims before it starts wandering.")]
    [SerializeField] private float exitDistance = 3f;
    [Tooltip("Random sideways / up-down offset inside the opening per fish. Keep it inside the hole.")]
    [SerializeField] private Vector2 openingScatter = new Vector2(0.8f, 0.4f);
    [Tooltip("Random yaw / pitch (degrees) of each fish's exit line into the room.")]
    [SerializeField] private Vector2 exitScatter = new Vector2(45f, 20f);
    [Tooltip("How wide a fish is, in metres: fish only cross where one this wide clears the window's own colliders (the glass and frame of the porthole). Colliders named Player Blocker are not counted.")]
    [SerializeField] private float fishWidth = 0.5f;

    public const string PlayerBlockerName = "PlayerBlocker";
    private List<Vector2> passage;   // spots in the window's plane (local x, y) where a fish fits through

    private void Awake()
    {
        if (Application.isPlaying && cutHoleAtStart)
            CutHole(false);
    }

    // Replaces the box wall behind the window with pieces around the hole and lines the hole. Returns true if a hole was cut.
    public bool CutHole(bool undoable)
    {
        Physics.SyncTransforms();
        bool cut = WallCutter.Cut(transform, holeSize, undoable, out string message, out float thickness);
        if (cut)
        {
            BuildSleeve(thickness, undoable);
            Debug.Log($"{name}: {message}", this);
        }
        else if (!string.IsNullOrEmpty(message))
            Debug.LogWarning($"{name}: {message}", this);
        else if (undoable)
            Debug.Log($"{name}: no wall within reach behind the window (already cut, or nothing there).", this);
        return cut;
    }

    // Four thin boxes lining the inside edges of the hole through the whole wall depth, so the cut reads as a framed opening.
    private void BuildSleeve(float thickness, bool undoable)
    {
        if (sleeveThickness <= 0f || transform.Find("Sleeve") != null)
            return;

        var sleeve = new GameObject("Sleeve");
        sleeve.transform.SetParent(transform, false);
        float t = sleeveThickness;
        float depth = thickness + 0.04f;
        float z = -thickness * 0.5f;
        SleevePiece(sleeve, "Top", new Vector3(0f, holeSize.y * 0.5f - t * 0.5f, z), new Vector3(holeSize.x, t, depth));
        SleevePiece(sleeve, "Bottom", new Vector3(0f, -holeSize.y * 0.5f + t * 0.5f, z), new Vector3(holeSize.x, t, depth));
        SleevePiece(sleeve, "Left", new Vector3(-holeSize.x * 0.5f + t * 0.5f, 0f, z), new Vector3(t, holeSize.y - 2f * t, depth));
        SleevePiece(sleeve, "Right", new Vector3(holeSize.x * 0.5f - t * 0.5f, 0f, z), new Vector3(t, holeSize.y - 2f * t, depth));

        // A round frame covers the middle of each side but not the corners: fill each corner from just under the
        // frame's edge out past the corner, a block turned to face the middle.
        if (frameEdge > 0f)
        {
            Vector2 half = holeSize * 0.5f;
            float corner = half.magnitude;
            float inner = frameEdge - 0.08f;
            if (corner > inner)
            {
                float angle = Mathf.Atan2(half.y, half.x) * Mathf.Rad2Deg;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 d = new Vector2(i == 0 || i == 3 ? 1f : -1f, i < 2 ? 1f : -1f);
                    float turn = Mathf.Atan2(d.y * half.y, d.x * half.x) * Mathf.Rad2Deg;
                    Vector2 dir = new Vector2(Mathf.Cos(turn * Mathf.Deg2Rad), Mathf.Sin(turn * Mathf.Deg2Rad));
                    float outer = corner + 0.05f;
                    float mid = (inner + outer) * 0.5f;
                    float across = 2f * (corner - inner) + 0.1f;
                    SleevePiece(sleeve, "Corner", new Vector3(dir.x * mid, dir.y * mid, z), new Vector3(outer - inner, across, depth));
                    sleeve.transform.GetChild(sleeve.transform.childCount - 1).localRotation = Quaternion.Euler(0f, 0f, turn);
                }
            }
        }

#if UNITY_EDITOR
        if (undoable)
            UnityEditor.Undo.RegisterCreatedObjectUndo(sleeve, "Cut hole in wall");
#endif
    }

    private void SleevePiece(GameObject parent, string label, Vector3 localPosition, Vector3 size)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = label;
        Destroy(piece.GetComponent<Collider>(), Application.isPlaying);
        piece.transform.SetParent(parent.transform, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = size;
        piece.AddComponent<RendererTint>().Tint = sleeveColor;
    }

    private static void Destroy(Object target, bool playing)
    {
        if (playing)
            Object.Destroy(target);
        else
            Object.DestroyImmediate(target);
    }

    public Vector3[] BuildPath(out Vector3 start)
    {
        if (!PassagePoint(out Vector3 crossing))
            return BuildPath(transform, startDepth, startDrop, exitDistance, openingScatter, exitScatter, out start);

        // Straight through the gap, square to the window: up behind it, line up, cross, then fan out into the room.
        Vector3 behind = crossing - transform.forward * startDepth;
        start = behind + OutOfSight(transform) * (startDrop + Random.Range(0f, 1f));
        Vector3 lineUp = crossing - transform.forward * (startDepth * 0.5f);
        Vector3 through = crossing + transform.forward * 0.8f;
        Quaternion fan = Quaternion.AngleAxis(Random.Range(-exitScatter.x, exitScatter.x), transform.up)
                       * Quaternion.AngleAxis(Random.Range(-exitScatter.y, exitScatter.y), transform.right);
        Vector3 exit = through + fan * transform.forward * (exitDistance * Random.Range(0.7f, 1.3f));
        return new[] { behind, lineUp, through, exit };
    }

    public Vector3[] BuildLeavePath()
    {
        if (!PassagePoint(out Vector3 crossing))
            return BuildLeavePath(transform, startDepth, startDrop, openingScatter);
        Vector3 behind = crossing - transform.forward * startDepth;
        return new[] { crossing + transform.forward * 1.5f, crossing, behind, behind + OutOfSight(transform) * startDrop };
    }

    // A random spot where a fish fits through the window's own model, in world space. False when the window has no
    // colliders of its own (or no gap in them), so the plain Opening Scatter is used.
    private bool PassagePoint(out Vector3 point)
    {
        point = transform.position;
        if (passage == null)
            passage = FindPassage();
        if (passage.Count == 0)
            return false;
        Vector2 p = passage[Random.Range(0, passage.Count)];
        point = transform.TransformPoint(new Vector3(p.x, p.y, 0f));
        return true;
    }

    // Every spot on a 10 cm grid over the opening where a fish (Fish Width across) crosses without touching the
    // window's colliders: rays square through the window at its middle and all round its edge.
    private List<Vector2> FindPassage()
    {
        var found = new List<Vector2>();
        var solid = new List<Collider>();
        foreach (Collider c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger && c.enabled && c.name != PlayerBlockerName)
                solid.Add(c);
        if (solid.Count == 0)
            return found;

        float radius = fishWidth * 0.5f;
        Vector2 half = holeSize * 0.5f - Vector2.one * radius;
        var ring = new List<Vector2> { Vector2.zero };
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            ring.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }
        const float step = 0.1f;
        for (float x = -half.x; x <= half.x + 0.001f; x += step)
            for (float y = -half.y; y <= half.y + 0.001f; y += step)
            {
                bool clear = true;
                foreach (Vector2 offset in ring)
                {
                    Vector3 from = transform.TransformPoint(new Vector3(x + offset.x, y + offset.y, -2f));
                    var ray = new Ray(from, transform.forward);
                    foreach (Collider c in solid)
                        if (c.Raycast(ray, out _, 4f))
                        {
                            clear = false;
                            break;
                        }
                    if (!clear)
                        break;
                }
                if (clear)
                    found.Add(new Vector2(x, y));
            }
        if (found.Count == 0)
            Debug.LogWarning($"{name}: no gap a fish {fishWidth} m wide fits through in this window's model; fish use the whole opening.", this);
        return found;
    }

    // The way back out: in front of the opening, through it to behind the wall, then down out of sight.
    public static Vector3[] BuildLeavePath(Transform opening, float startDepth, float startDrop, Vector2 openingScatter)
    {
        Vector3 side = opening.right * Random.Range(-openingScatter.x, openingScatter.x);
        Vector3 lift = opening.up * Random.Range(-openingScatter.y, openingScatter.y);
        Vector3 behind = opening.position - opening.forward * startDepth;
        return new[]
        {
            opening.position + opening.forward * 1.5f + side * 0.5f + lift,
            behind + side + lift,
            behind + side + OutOfSight(opening) * startDrop,
        };
    }

    // The randomised entry for one fish: start point (hidden), then rise → through the opening → fanned-out exit.
    public static Vector3[] BuildPath(Transform opening, float startDepth, float startDrop, float exitDistance,
        Vector2 openingScatter, Vector2 exitScatter, out Vector3 start)
    {
        Vector3 side = opening.right * Random.Range(-openingScatter.x, openingScatter.x);
        Vector3 lift = opening.up * Random.Range(-openingScatter.y, openingScatter.y);
        Vector3 behind = opening.position - opening.forward * startDepth;

        start = behind + side + OutOfSight(opening) * (startDrop + Random.Range(0f, 1f));
        Vector3 rise = behind + side + lift;
        Vector3 through = opening.position + opening.forward * 0.8f + side * 0.5f + lift;
        Quaternion fan = Quaternion.AngleAxis(Random.Range(-exitScatter.x, exitScatter.x), opening.up)
                       * Quaternion.AngleAxis(Random.Range(-exitScatter.y, exitScatter.y), opening.right);
        Vector3 exit = through + fan * opening.forward * (exitDistance * Random.Range(0.7f, 1.3f));

        return new[] { rise, through, exit };
    }

    // Which way from behind the opening the fish waits: down for a window in a wall, up for a hole in a ceiling (its
    // arrow pointing down into the room).
    public static Vector3 OutOfSight(Transform opening) => opening.forward.y < -0.7f ? Vector3.up : Vector3.down;

    private void OnDrawGizmos()
    {
        DrawGizmos(transform, startDepth, startDrop, exitDistance, openingScatter);

        // The hole that gets cut.
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, 0f, -0.5f), new Vector3(holeSize.x, holeSize.y, 1f));
        Gizmos.matrix = Matrix4x4.identity;
    }

    public static void DrawGizmos(Transform opening, float startDepth, float startDrop, float exitDistance, Vector2 openingScatter)
    {
        Vector3 behind = opening.position - opening.forward * startDepth;
        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(opening.position, opening.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(openingScatter.x * 2f, openingScatter.y * 2f, 0.1f));
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawLine(behind + OutOfSight(opening) * startDrop, behind);
        Gizmos.DrawLine(behind, opening.position + opening.forward * exitDistance);
        Gizmos.DrawSphere(behind + OutOfSight(opening) * startDrop, 0.15f);
    }
}
