using UnityEngine;
using UnityEngine.Events;

// Trigger plate that becomes the active respawn point when the player swims through it, with a chime and glow.
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Header("Respawn")]
    [Tooltip("Where the player reappears. Leave empty to use this object.")]
    [SerializeField] private Transform respawnPoint;
    [Tooltip("Treat this as already active at start (no sound). Use for the initial spawn.")]
    [SerializeField] private bool startsActivated = false;

    [Header("Feedback")]
    [SerializeField] private AudioClip activateSound;
    [SerializeField, Range(0f, 1f)] private float activateVolume = 0.7f;
    [SerializeField] private Color activeEmission = new Color(0.3f, 1.5f, 1.5f);
    [SerializeField] private Color inactiveEmission = new Color(0.05f, 0.25f, 0.25f);

    [Header("Events")]
    public UnityEvent onActivated = new UnityEvent();

    public static Checkpoint Current { get; private set; }
    public bool IsActive => Current == this;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private Renderer[] renderers;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();

        if (startsActivated && Current == null)
            Current = this;

        ApplyEmission();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsActive)
            return;

        var death = other.GetComponentInParent<DeathManager>();
        if (death == null)
            return;

        Activate(death);
    }

    private void Activate(DeathManager death)
    {
        Checkpoint previous = Current;
        Current = this;
        previous?.ApplyEmission();

        death.SetRespawnPoint(respawnPoint != null ? respawnPoint : transform);

        if (activateSound != null)
            AudioSource.PlayClipAtPoint(activateSound, transform.position, activateVolume);

        ApplyEmission();
        onActivated.Invoke();
    }

    private void ApplyEmission()
    {
        block.SetColor(EmissionId, IsActive ? activeEmission : inactiveEmission);
        foreach (var r in renderers)
            r.SetPropertyBlock(block);
    }
}
