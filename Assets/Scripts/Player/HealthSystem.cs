using UnityEngine;

[RequireComponent(typeof(HungerSystem))]
public class HealthSystem : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float starvationDamagePerTick = 15f;
    [SerializeField] private float damageTickInterval = 1f;

    private HungerSystem hungerSystem;
    private float tickTimer;
    private bool isDead;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    public event System.Action<float> OnDamaged;
    public event System.Action OnDeath;

    private void Awake()
    {
        hungerSystem = GetComponent<HungerSystem>();
        CurrentHealth = maxHealth;
    }

    private void Update()
    {
        if (hungerSystem.CurrentHunger > 0f)
        {
            tickTimer = 0f;
            return;
        }

        tickTimer += Time.deltaTime;
        if (tickTimer < damageTickInterval)
            return;

        tickTimer -= damageTickInterval;
        TakeDamage(starvationDamagePerTick);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || isDead)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnDamaged?.Invoke(amount);

        if (CurrentHealth <= 0f)
        {
            isDead = true;
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }
}
