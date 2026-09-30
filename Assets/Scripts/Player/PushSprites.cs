using UnityEngine;

// The hand-drawn push (hands on a box, from the player's own eyes) and how it is shown: Assets/Resources/PushSprites.asset.
// Box Push loads it and plays it over the view (Sprite Push Animator) while the player is pushing a box: the frames in
// order, looping, sliding up into view as a push starts and back down as it ends. Empty for now, so nothing shows:
// drop the frames in (e.g. the slices of Art/Sprites/Push_Spritesheet, in order) and it plays, no code needed.
[CreateAssetMenu(fileName = "PushSprites", menuName = "Out of the Depths/Push Sprites")]
public class PushSprites : ScriptableObject
{
    [Tooltip("The push, in order. Empty = no push drawing (nothing shows).")]
    public Sprite[] frames = new Sprite[0];
    [Tooltip("How fast the frames play.")]
    public float framesPerSecond = 10f;
    [Tooltip("Start over after the last frame while the push goes on. Off = stay on the last frame.")]
    public bool loop = true;
    [Tooltip("How tall the tallest frame is on screen, as a share of the screen's height (every frame is sized the same way).")]
    [Range(0.2f, 1.5f)] public float screenHeight = 0.8f;
    [Tooltip("Where the middle of the drawing sits across the screen (0 = left edge, 1 = right edge). Frames stand on the bottom edge.")]
    [Range(0f, 1f)] public float centreX = 0.5f;
    [Tooltip("Colour over the drawings, so they sit in the murky water (white = as drawn).")]
    public Color tint = new Color(0.82f, 0.9f, 0.95f);
    [Tooltip("Seconds to slide into view as a push starts, and out again as it ends.")]
    public float fadeSeconds = 0.15f;
    [Tooltip("Hide the 3D hands and weapon while the push drawing shows (they would be in front of it).")]
    public bool hideHands = true;
}
