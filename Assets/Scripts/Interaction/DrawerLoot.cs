using UnityEngine;

// Hides an item in one of a chest's drawers when the level starts: a different drawer every run (or always the first
// one), so you have to open them to find it. The item sits inside that drawer, rides out with it and is hidden while it
// is shut (Animated Drawer's Stash). The ship's spawn room hides the first-room key in the dresser this way.
public class DrawerLoot : MonoBehaviour
{
    [Tooltip("What is hidden (the key pickup).")]
    [SerializeField] private Transform item;
    [Tooltip("The drawers it can be in. Empty = every Animated Drawer under this object.")]
    [SerializeField] private AnimatedDrawer[] drawers;
    [Tooltip("A random drawer each run. Off = always the first one.")]
    [SerializeField] private bool randomDrawer = true;

    public AnimatedDrawer Chosen { get; private set; }

    private void Awake()
    {
        if (item == null)
            return;
        AnimatedDrawer[] pool = drawers != null && drawers.Length > 0 ? drawers : GetComponentsInChildren<AnimatedDrawer>(true);
        pool = System.Array.FindAll(pool, d => d != null);
        if (pool.Length == 0)
            return;
        Chosen = pool[randomDrawer ? Random.Range(0, pool.Length) : 0];
        Chosen.Stash(item);
    }
}
