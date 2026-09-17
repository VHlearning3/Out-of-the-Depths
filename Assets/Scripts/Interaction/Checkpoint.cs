using UnityEngine;
using UnityEngine.Events;

// Respawn plate: press E on it to make it the active respawn point (chime + glow). The active plate stays lit and
// can't be re-activated until another one takes over.
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour, IInteractable
{
    [Header("Respawn")]
    [Tooltip("Where the player reappears. Leave empty to use this object.")]
    [SerializeField] private Transform respawnPoint;
    [Tooltip("Treat this as already active at start (no sound). Use for the initial spawn.")]
    [SerializeField] private bool startsActivated = false;
    [SerializeField] private string prompt = "activate checkpoint";

    [Header("Feedback")]
    [SerializeField] private AudioClip activateSound;
    [SerializeField, Range(0f, 1f)] private float activateVolume = 0.7f;
    [SerializeField] private Color activeEmission = new Color(0.3f, 1.5f, 1.5f);
    [SerializeField] private Color inactiveEmission = new Color(0.05f, 0.25f, 0.25f);

    [Header("Events")]
    public UnityEvent onActivated = new UnityEvent();

    public static Checkpoint Current { get; private set; }
    public bool IsActive => Current == this;
    public string Prompt => prompt;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private Renderer[] renderers;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();

        if (startsActivated && Current == null)
            Current = this;

        ApplyState();
    }

    public void Interact(GameObject interactor)
    {
        if (IsActive)
            return;

        var death = interactor.GetComponentInParent<DeathManager>();
        if (death == null)
            return;

        Activate(death);
    }

    private void Activate(DeathManager death)
    {
        Checkpoint previous = Current;
        Current = this;
        if (previous != null)
            previous.ApplyState();

        death.SetRespawnPoint(respawnPoint != null ? respawnPoint : transform);

        if (activateSound != null)
            AudioSource.PlayClipAtPoint(activateSound, transform.position, activateVolume);

        ApplyState();
        onActivated.Invoke();
    }

    // The active plate is disabled so the E prompt only shows on plates you can still activate.
    private void ApplyState()
    {
        enabled = !IsActive;
        block.SetColor(EmissionId, IsActive ? activeEmission : inactiveEmission);
        foreach (var r in renderers)
            r.SetPropertyBlock(block);
    }
}
