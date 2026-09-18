using UnityEngine;

// Wall avoidance for anything that swims by moving its transform: steer the wanted direction along walls, and never
// take the last step into one. Other fish are never treated as walls; pass 'ignore' to also skip e.g. the player being chased.
public static class FishSteering
{
    private static readonly RaycastHit[] hits = new RaycastHit[8];

    // Returns the direction to swim in: unchanged when the way ahead is clear, otherwise sliding along the wall and pushed
    // off it harder the closer it is.
    public static Vector3 Avoid(Vector3 position, Vector3 desired, float radius, float lookAhead, LayerMask mask, Transform ignore = null)
    {
        if (!Nearest(position, desired, radius, lookAhead, mask, ignore, out RaycastHit hit))
            return desired;

        float closeness = 1f - Mathf.Clamp01(hit.distance / lookAhead);
        Vector3 slide = Vector3.ProjectOnPlane(desired, hit.normal);
        if (slide.sqrMagnitude < 0.001f)
            slide = Vector3.Cross(hit.normal, Vector3.up);
        if (slide.sqrMagnitude < 0.001f)
            slide = Vector3.Cross(hit.normal, Vector3.right);   // head-on into a floor or ceiling

        Vector3 steered = slide.normalized + hit.normal * Mathf.Max(closeness, 0.05f);
        return steered.sqrMagnitude > 0.0001f ? steered.normalized : hit.normal;
    }

    // Shortens a move so it stops just short of the first wall in its way.
    public static Vector3 ClampMove(Vector3 position, Vector3 move, float radius, LayerMask mask, Transform ignore = null)
    {
        float distance = move.magnitude;
        if (distance < 0.0001f)
            return move;

        Vector3 direction = move / distance;
        if (!Nearest(position, direction, radius, distance + radius, mask, ignore, out RaycastHit hit))
            return move;

        float allowed = Mathf.Max(0f, hit.distance - radius * 0.5f);
        return direction * Mathf.Min(distance, allowed);
    }

    private static bool Nearest(Vector3 origin, Vector3 direction, float radius, float maxDistance, LayerMask mask, Transform ignore, out RaycastHit nearest)
    {
        nearest = default;
        float best = float.MaxValue;
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, maxDistance, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.distance <= 0f && hit.point == Vector3.zero)
                continue;   // the sweep started inside this collider: it is around us, not ahead of us
            if (hit.distance >= best)
                continue;
            if (hit.collider.GetComponentInParent<FishController>() != null)
                continue;
            if (ignore != null && hit.collider.transform.IsChildOf(ignore))
                continue;

            best = hit.distance;
            nearest = hit;
        }
        return best < float.MaxValue;
    }
}
