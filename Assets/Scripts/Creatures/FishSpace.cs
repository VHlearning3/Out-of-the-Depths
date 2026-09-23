using System.Collections.Generic;
using UnityEngine;

// The room a fish keeps round its middle, so fish never swim into each other. After everything has moved each frame,
// a fish that is inside another's room steps straight back out by its half of the overlap (the other does the same on
// its own turn), and never through a wall. The soft Separation steering on Fish Wander / Chase Pufferfish still
// spreads packs out; this is the hard floor under it that the slot pull, a chase or a lunge cannot push through.
// Added by Fish Wander and Chase Pufferfish when they wake (they hand over their wall clearance and mask); Radius 0 =
// sized from the fish's own model then. Dead fish are left alone.
[DisallowMultipleComponent]
public class FishSpace : MonoBehaviour
{
    [Tooltip("How much room this fish keeps round its middle, in metres: no two fish get closer than their two rooms together. 0 = from its model (about half its length and width).")]
    [SerializeField] private float radius = 0f;

    private static readonly List<FishSpace> all = new List<FishSpace>();

    private float clearance = 0.3f;
    private LayerMask walls = ~0;
    private FishController controller;

    public float Radius => radius;
    private bool Alive => controller == null || controller.IsAlive;

    // The owner's wall clearance and mask, so stepping out never pushes it into a wall.
    public void Setup(float wallClearance, LayerMask wallMask)
    {
        clearance = wallClearance;
        walls = wallMask;
    }

    private void Awake()
    {
        controller = GetComponent<FishController>();
    }

    private void Start()
    {
        if (radius <= 0f)
            radius = FromModel();
    }

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    private void LateUpdate()
    {
        if (!Alive || radius <= 0f)
            return;
        Vector3 here = transform.position;
        Vector3 push = Vector3.zero;
        foreach (FishSpace other in all)
        {
            if (other == this || !other.Alive || other.radius <= 0f)
                continue;
            Vector3 away = here - other.transform.position;
            float need = radius + other.radius;
            float squared = away.sqrMagnitude;
            if (squared >= need * need)
                continue;
            float distance = Mathf.Sqrt(squared);
            // Exactly on top of each other: part along a direction fixed per pair, so the two go opposite ways.
            Vector3 direction = distance > 0.0001f ? away / distance
                : (GetInstanceID() < other.GetInstanceID() ? Vector3.right : Vector3.left);
            push += direction * ((need - distance) * 0.5f);
        }
        if (push.sqrMagnitude > 0.000001f)
            transform.position += FishSteering.ClampMove(here, push, clearance, walls);
    }

    // About half the model's length and width: measured facing straight ahead, so it does not depend on which way the
    // fish happened to be turned when it woke.
    private float FromModel()
    {
        Quaternion turned = transform.rotation;
        transform.rotation = Quaternion.identity;
        bool any = false;
        Bounds bounds = default;
        foreach (Renderer part in GetComponentsInChildren<Renderer>())
        {
            if (part is ParticleSystemRenderer || part is TrailRenderer || part is LineRenderer)
                continue;
            if (!any)
                bounds = part.bounds;
            else
                bounds.Encapsulate(part.bounds);
            any = true;
        }
        transform.rotation = turned;
        if (!any)
            return 0.35f;
        return Mathf.Clamp((bounds.extents.x + bounds.extents.z) * 0.5f, 0.15f, 1.2f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius > 0f ? radius : 0.35f);
    }
}
