using UnityEngine;
using UnityEngine.Events;

// Single entry point for anything that hurts the player (hazards, enemies). Adds a short invulnerability window and God mode.
public class DamageManager : MonoBehaviour, IDamageable
{
    [SerializeField] private HealthSystem healthSystem;
    [Tooltip("Brief window after a hit where further damage is ignored, so overlapping hazards don't stack.")]
    [SerializeField] private float invulnerabilityDuration = 0.5f;

    public UnityEvent<float> onDamageTaken = new UnityEvent<float>();

    public bool IsInvulnerable => GodMode || Time.time < invulnerableUntil;
    public bool GodMode { get; set; }

    private float invulnerableUntil;

    public void TakeDamage(float amount)
    {
        ApplyDamage(amount);
    }

    public void ApplyDamage(float amount)
    {
        if (amount <= 0f || healthSystem == null || healthSystem.IsDead || IsInvulnerable)
            return;

        invulnerableUntil = Time.time + invulnerabilityDuration;
        healthSystem.TakeDamage(amount);
        onDamageTaken.Invoke(amount);
    }
}
