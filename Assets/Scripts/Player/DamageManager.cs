using UnityEngine;
using UnityEngine.Events;

public class DamageManager : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;

    public UnityEvent<float> onDamageTaken = new UnityEvent<float>();

    public void ApplyDamage(float amount)
    {
        if (amount <= 0f || healthSystem == null || healthSystem.IsDead)
            return;

        healthSystem.TakeDamage(amount);
        onDamageTaken.Invoke(amount);
    }
}
