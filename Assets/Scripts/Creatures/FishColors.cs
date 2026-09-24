using System.Collections.Generic;
using UnityEngine;

// Testing colours: every kind of fish gets its own flat colour so you can tell them apart at a glance (all the fish
// share one placeholder model for now). Wanderers blue, pufferfish (they bite) red, wall fish (they stay put) yellow,
// the chase pack purple; dead fish you can eat green, dead fish that are not food grey. Painted through each fish's
// Renderer Tint (added if missing). The admin page's "Colour fish by type" switch turns it off and puts every fish back
// to the colour it had.
public static class FishColors
{
    public static readonly Color Wanderer = new Color(0.25f, 0.6f, 1f);
    public static readonly Color Pufferfish = new Color(1f, 0.22f, 0.18f);
    public static readonly Color WallFish = new Color(1f, 0.85f, 0.15f);
    public static readonly Color Chase = new Color(0.75f, 0.25f, 1f);
    public static readonly Color Food = new Color(0.3f, 1f, 0.35f);
    public static readonly Color NotFood = new Color(0.45f, 0.45f, 0.48f);

    private static bool enabled = true;
    // The colour each tint had before it was painted, to put back when the switch goes off.
    private static readonly Dictionary<RendererTint, Color> original = new Dictionary<RendererTint, Color>();

    public static bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value)
                return;
            enabled = value;
            if (!enabled)
            {
                foreach (var pair in original)
                    if (pair.Key != null)
                        pair.Key.Tint = pair.Value;
                original.Clear();
            }
            RepaintAll();
        }
    }

    // The colour a live fish of this kind wears.
    public static Color Alive(GameObject fish)
    {
        if (fish.GetComponent<FishAggression>() != null)
            return Pufferfish;
        if (fish.GetComponent<FishWander>() == null)
            return WallFish;
        return Wanderer;
    }

    public static void Paint(GameObject fish, Color color)
    {
        if (!enabled || fish == null)
            return;
        var tint = fish.GetComponent<RendererTint>();
        if (tint == null)
            tint = fish.AddComponent<RendererTint>();
        if (!original.ContainsKey(tint))
            original[tint] = tint.Tint;
        tint.Tint = color;
    }

    // Every fish in the scene, by what it is right now.
    public static void RepaintAll()
    {
        if (!enabled)
            return;
        foreach (FishController fish in Object.FindObjectsByType<FishController>(FindObjectsSortMode.None))
            fish.PaintByKind();
        foreach (ChasePufferfish chaser in Object.FindObjectsByType<ChasePufferfish>(FindObjectsSortMode.None))
            Paint(chaser.gameObject, Chase);
        foreach (EdibleFish food in Object.FindObjectsByType<EdibleFish>(FindObjectsSortMode.None))
            if (food.GetComponent<FishController>() == null)
                Paint(food.gameObject, Food);
    }
}
