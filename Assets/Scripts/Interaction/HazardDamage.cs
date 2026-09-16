using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HazardDamage : MonoBehaviour
{
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float damageInterval = 1f;

    private float nextDamageTime;

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider other)
    {
        if (Time.time < nextDamageTime)
            return;

        var damageManager = other.GetComponentInParent<DamageManager>();
        if (damageManager == null)
            return;

        nextDamageTime = Time.time + damageInterval;
        damageManager.ApplyDamage(damageAmount);
    }
}
