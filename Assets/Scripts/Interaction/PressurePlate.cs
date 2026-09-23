using UnityEngine;
using UnityEngine.Events;

// A plate on the floor pressed down by a Pushable Box resting on it (its middle over the plate), or by the player
// with Player Presses It on. It sinks a little and glows while pressed. On Pressed / On Released fire as that changes;
// On All Pressed fires once, the first time this plate and every plate in Also Needs are pressed together (the box
// room's two plates open the closet). The plate's footprint is its own size on the floor (x and z of its scale, or
// Size when set).
public class PressurePlate : MonoBehaviour
{
    [Tooltip("Pressed by the player swimming onto it too, not only by a box.")]
    [SerializeField] private bool playerPressesIt = false;
    [Tooltip("Footprint on the floor, in metres (x, z). 0 = the object's own scale.")]
    [SerializeField] private Vector2 size = Vector2.zero;
    [Tooltip("How far into the plate from its edge a box's middle may be and still count, in metres (negative = must be further in).")]
    [SerializeField] private float leeway = 0.2f;
    [Tooltip("The other plates that must be pressed at the same time for On All Pressed.")]
    [SerializeField] private PressurePlate[] alsoNeeds = new PressurePlate[0];
    [Tooltip("How far it sinks while pressed, in metres.")]
    [SerializeField] private float sink = 0.04f;
    [SerializeField] private Color pressedColor = new Color(0.4f, 1f, 0.5f);
    [SerializeField] private AudioClip pressSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

    [Header("Events")]
    public UnityEvent onPressed = new UnityEvent();
    public UnityEvent onReleased = new UnityEvent();
    public UnityEvent onAllPressed = new UnityEvent();

    public bool IsPressed { get; private set; }

    private Vector3 restPosition;
    private Renderer look;
    private Color restColor;
    private MaterialPropertyBlock block;
    private Transform player;
    private bool allFired;

    private void Awake()
    {
        restPosition = transform.localPosition;
        look = GetComponent<Renderer>();
        if (look != null)
        {
            block = new MaterialPropertyBlock();
            restColor = look.sharedMaterial != null && look.sharedMaterial.HasProperty("_BaseColor") ? look.sharedMaterial.GetColor("_BaseColor") : Color.white;
            RendererTint tint = GetComponent<RendererTint>();
            if (tint != null)
                restColor = tint.Tint;
        }
    }

    private void Update()
    {
        bool pressed = Weighed();
        if (pressed != IsPressed)
        {
            IsPressed = pressed;
            transform.localPosition = restPosition + Vector3.down * (pressed ? sink : 0f);
            Paint(pressed ? pressedColor : restColor);
            if (pressSound != null)
                AudioSource.PlayClipAtPoint(pressSound, transform.position, volume * (pressed ? 1f : 0.5f));
            (pressed ? onPressed : onReleased).Invoke();
            if (pressed)
                CheckAll();
        }
    }

    // Is a box (or the player, if allowed) resting over the plate?
    private bool Weighed()
    {
        Vector3 centre = transform.position;
        Vector2 half = Footprint() * 0.5f + Vector2.one * leeway;
        foreach (PushableBox box in PushableBox.All)
            if (box != null && box.Over(centre, half, 1.5f))
                return true;
        if (!playerPressesIt)
            return false;
        if (player == null)
        {
            SwimController swimmer = FindFirstObjectByType<SwimController>();
            player = swimmer != null ? swimmer.transform : null;
        }
        if (player == null)
            return false;
        Vector3 at = player.position;
        return Mathf.Abs(at.x - centre.x) <= half.x && Mathf.Abs(at.z - centre.z) <= half.y && at.y - centre.y <= 2f && at.y >= centre.y - 0.5f;
    }

    private Vector2 Footprint()
    {
        if (size.x > 0f && size.y > 0f)
            return size;
        Vector3 scale = transform.lossyScale;
        return new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
    }

    // This one and all it needs are down: fire On All Pressed once, here and on the others (so wiring any one works).
    private void CheckAll()
    {
        if (allFired)
            return;
        foreach (PressurePlate other in alsoNeeds)
            if (other != null && !other.IsPressed)
                return;
        allFired = true;
        onAllPressed.Invoke();
        foreach (PressurePlate other in alsoNeeds)
            if (other != null && !other.allFired)
            {
                other.allFired = true;
                other.onAllPressed.Invoke();
            }
    }

    private void Paint(Color colour)
    {
        if (look == null)
            return;
        look.GetPropertyBlock(block);
        block.SetColor("_BaseColor", colour);
        block.SetColor("_Color", colour);
        look.SetPropertyBlock(block);
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 foot = Footprint() + Vector2.one * leeway * 2f;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.75f, new Vector3(foot.x, 1.5f, foot.y));
    }
}
