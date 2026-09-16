using UnityEngine;

// Trigger volume that hurts whatever touches it (spikes, poison, etc.).
[RequireComponent(typeof(Collider))]
public class HazardDamage : MonoBehaviour
{
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float damageInterval = 1f;
    [Tooltip("If on, only damages once when something enters, not continuously while it stays.")]
    [SerializeField] private bool damageOnEnterOnly = false;

    private float nextDamageTime;

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!damageOnEnterOnly)
            TryDamage(other);
    }

    private void TryDamage(Collider other)
    {
        if (Time.time < nextDamageTime)
            return;

        var damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return;

        nextDamageTime = Time.time + damageInterval;
        damageable.TakeDamage(damageAmount);
    }
}
