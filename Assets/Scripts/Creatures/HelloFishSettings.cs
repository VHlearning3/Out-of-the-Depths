using UnityEngine;

// Everything about the hello fish easter egg that can be tuned: how often it comes, how long it stays, what it says,
// the petting (rub the cursor over it: hearts float up) and the poking (click it: it puffs up and complains), and its
// sounds. The one the game uses is Assets/Resources/HelloFish.asset; with none there it runs on these defaults
// without sounds. Changes on the asset show at once while playing.
[CreateAssetMenu(menuName = "Out of the Depths/Hello Fish", fileName = "HelloFish")]
public class HelloFishSettings : ScriptableObject
{
    [Header("Coming by")]
    [Tooltip("How often an inspect brings it, 0..1 (the admin page's Hello fish switch makes it every time).")]
    [Range(0f, 1f)] public float chance = 0.1f;
    [Tooltip("Seconds it stays beside the item when nobody touches it.")]
    [Min(0.5f)] public float staySeconds = 3.5f;
    [Tooltip("While the cursor is on it, and after every pet or poke, it stays at least this much longer.")]
    [Min(0.2f)] public float stayAfterTouch = 1.6f;
    public string helloText = "Hello!";

    [Header("Petting (rub the cursor over it)")]
    [Tooltip("How far the cursor has to rub over it for each heart, in pixels on a 1080p screen. Back and forth counts.")]
    [Min(20f)] public float rubPerHeart = 220f;
    [Tooltip("What it says when a heart pops, one after another.")]
    public string[] petLines = { "Hehe!", "Aww!", "That tickles!", "More pets!", "I like you!" };
    [Tooltip("The heart picture. Empty = a drawn pink heart in Heart Color.")]
    public Texture2D heartPicture;
    public Color heartColor = new Color(1f, 0.36f, 0.52f);
    [Tooltip("Heart size in pixels on a 1080p screen, smallest and largest.")]
    public Vector2 heartSize = new Vector2(34f, 48f);
    [Tooltip("How far a heart floats up before it fades, in pixels on a 1080p screen, and how long that takes.")]
    public float heartRise = 150f;
    [Min(0.2f)] public float heartSeconds = 1.4f;

    [Header("Poking (click it)")]
    [Tooltip("What it says on each poke, in order; after the last one it keeps saying the last.")]
    public string[] pokeLines = { "Hey!", "Ow! Don't poke me!", "Stop poking!", "Rude! I'm leaving!" };
    [Tooltip("It swims off in a huff after this many pokes. 0 = never.")]
    [Min(0)] public int leaveAfterPokes = 4;
    [Tooltip("How much it puffs up when poked (0.35 = 35% bigger for a moment).")]
    [Range(0f, 1f)] public float pokePuff = 0.35f;
    [Tooltip("The complaints are written in this colour.")]
    public Color complainColor = new Color(0.62f, 0.1f, 0.08f);
    [Tooltip("A click counts as a poke when the button comes back up within this many seconds and the cursor hardly moved; longer or dragged = petting.")]
    [Min(0.05f)] public float pokeSeconds = 0.35f;

    [Header("Sound")]
    [Tooltip("Its hello. Empty = two tiny made-up blips.")]
    public AudioClip helloSound;
    [Tooltip("When a heart pops.")]
    public AudioClip petSound;
    [Tooltip("When it is poked.")]
    public AudioClip pokeSound;
    [Range(0f, 1f)] public float volume = 0.55f;

    private static HelloFishSettings current;

    // The asset in Resources, or the defaults (no sounds) when there is none.
    public static HelloFishSettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<HelloFishSettings>("HelloFish");
                if (current == null)
                {
                    current = CreateInstance<HelloFishSettings>();
                    current.hideFlags = HideFlags.DontSave;
                }
            }
            return current;
        }
    }
}
