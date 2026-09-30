using UnityEngine;

// The hand-drawn slash frames (Art/Sprites: Slash_Side_Spritesheet, Slash_Down_Spritesheet, a hand and the dagger from
// the player's own eyes) and how they are shown. Assets/Resources/SlashSprites.asset; the Slash Attack loads it and
// plays them (Sprite Slash Animator) whenever the dagger is the weapon in hand: Side for the left-right sweep, Down for
// the up-down chop. The drawings are cut flat where they run off the screen (the arm at the bottom, the raised fist at
// the top, the blade's ends at the sides), so they are laid out to put those cuts on or past the screen's edges: the
// sweep runs from the left edge (the first frame) to the right edge (the last), the chop stands in one column tall
// enough that the raised arm goes off the top. Swap in new frames here.
[CreateAssetMenu(fileName = "SlashSprites", menuName = "Out of the Depths/Slash Sprites")]
public class SlashSprites : ScriptableObject
{
    [Tooltip("The left-right sweep, in order (one per cell of the sheet).")]
    public Sprite[] side = new Sprite[0];
    [Tooltip("The up-down chop, in order (one per cell of the sheet).")]
    public Sprite[] down = new Sprite[0];
    [Tooltip("Where the blade's tip is in each sweep frame, as shares of the frame (0,0 = its bottom left, 1,1 = its top right). The slash ribbon follows these.")]
    public Vector2[] sideTips = { new Vector2(0f, 0.57f), new Vector2(0f, 0.58f), new Vector2(0f, 0.49f), new Vector2(0f, 0.99f), new Vector2(0.38f, 0.9f), new Vector2(0f, 1f) };
    [Tooltip("Where the blade's tip is in each chop frame, as shares of the frame. The first ones are off the top of the screen: the ribbon comes in from above.")]
    public Vector2[] downTips = { new Vector2(0.33f, 1f), new Vector2(0.39f, 1f), new Vector2(0.32f, 1f), new Vector2(0.64f, 0.99f), new Vector2(0.73f, 0f), new Vector2(0.5f, 0f) };
    [Tooltip("How fast the frames play (on average: the first and last are held longer, the ones between go faster). 20 = the whole slash in 0.3 s, one slash and its recovery.")]
    public float framesPerSecond = 20f;
    [Tooltip("How much longer the first frame (the wind-up) stays than an in-between one.")]
    [Range(1f, 4f)] public float holdFirst = 1.6f;
    [Tooltip("How much longer the last frame (the follow-through) stays than an in-between one.")]
    [Range(1f, 4f)] public float holdLast = 2.2f;
    [Tooltip("Seconds the arm takes to rise into view from below at the start (skipped when slashes chain).")]
    public float slideIn = 0.05f;
    [Tooltip("Seconds the arm takes to sink out of view and fade after the last frame.")]
    public float slideOut = 0.12f;
    [Tooltip("How strong the fading afterimage of the frame before is, for a sense of motion (0 = none).")]
    [Range(0f, 1f)] public float afterimage = 0.35f;
    [Tooltip("How tall the whole side sheet is on screen, as a share of the screen's height (sizes every sweep frame).")]
    [Range(0.3f, 1.8f)] public float sideHeight = 0.95f;
    [Tooltip("The sweep runs from this far in from the left edge (the first frame's left side) to this far in from the right edge (the last frame's right side), as a share of the screen's width. Below 0 pushes the cut ends just off the screen.")]
    [Range(-0.3f, 0.3f)] public float sideMargin = -0.02f;
    [Tooltip("How tall the whole down sheet is on screen, as a share of the screen's height. Over about 1.1 the raised arm's cut top stays off the screen.")]
    [Range(0.3f, 1.8f)] public float downHeight = 1.15f;
    [Tooltip("Where the chop comes down across the screen (0 = left edge, 1 = right edge): the right hand, just right of the dot.")]
    [Range(0f, 1f)] public float downCentreX = 0.58f;
    [Tooltip("Colour over the drawings, so the bright hand sits in the murky water (white = as drawn).")]
    public Color tint = new Color(0.82f, 0.9f, 0.95f);
    [Tooltip("Played only while the weapon in hand is this item (the drawings are of the dagger); any other weapon keeps its own swing.")]
    public string weaponItem = "Item_Dagger";
}
