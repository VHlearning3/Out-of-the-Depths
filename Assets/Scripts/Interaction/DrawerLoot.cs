using System.Collections.Generic;
using UnityEngine;

// Hides things in a chest's drawers when the level starts: Item in one drawer and each of the Extras in another, a
// different shuffle every run (or in drawer order), so you have to open them to find what you need. Each sits inside
// its drawer, rides out with it and is hidden while it is shut (Animated Drawer's Stash). The ship's spawn room hides
// the first-room key in the dresser this way, with a pearl in another drawer.
public class DrawerLoot : MonoBehaviour
{
    [Tooltip("What is hidden (the key pickup).")]
    [SerializeField] private Transform item;
    [Tooltip("More things, each in a drawer of its own (a pearl). Any past the number of drawers are left where they are.")]
    [SerializeField] private Transform[] extras;
    [Tooltip("The drawers they can be in. Empty = every Animated Drawer under this object.")]
    [SerializeField] private AnimatedDrawer[] drawers;
    [Tooltip("Random drawers each run. Off = the item in the first drawer, the extras in the next ones.")]
    [SerializeField] private bool randomDrawer = true;

    public AnimatedDrawer Chosen { get; private set; }

    private void Awake()
    {
        AnimatedDrawer[] pool = drawers != null && drawers.Length > 0 ? drawers : GetComponentsInChildren<AnimatedDrawer>(true);
        var free = new List<AnimatedDrawer>(System.Array.FindAll(pool, d => d != null));
        if (free.Count == 0)
            return;
        if (item != null)
            Chosen = Put(item, free);
        if (extras != null)
            foreach (Transform extra in extras)
                if (extra != null && free.Count > 0)
                    Put(extra, free);
    }

    private AnimatedDrawer Put(Transform thing, List<AnimatedDrawer> free)
    {
        int index = randomDrawer ? Random.Range(0, free.Count) : 0;
        AnimatedDrawer drawer = free[index];
        free.RemoveAt(index);
        drawer.Stash(thing);
        return drawer;
    }
}
