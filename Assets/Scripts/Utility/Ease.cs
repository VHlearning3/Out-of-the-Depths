using UnityEngine;

// Easing curves for hand-rolled animations (doors, placing pieces, fades, falling rubble). t goes 0..1, result goes 0..1.
public static class Ease
{
    // Slow start, quick middle, soft end: doors, hatches, things that move.
    public static float InOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    // Fast start, soft end: things that come to rest.
    public static float OutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    // Like OutCubic but overshoots a little before settling: landings, pops.
    public static float OutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // Gentle both ends: fades.
    public static float InOutSine(float t)
    {
        t = Mathf.Clamp01(t);
        return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
    }

    // Starts from rest and speeds up: falling things.
    public static float InQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }

    // Starts fast and slows down: a thrown thing losing speed.
    public static float OutQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }
}
