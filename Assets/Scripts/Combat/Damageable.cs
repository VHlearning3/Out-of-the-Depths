using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Health for enemies and props: hit flash, hit/death sounds, onDeath event, optional destroy.
[RequireComponent(typeof(Collider))]
public class Damageable : MonoBehaviour, IDamageable
{
    [System.Serializable]
    public class HealthChangedEvent : UnityEvent<float, float> { }

    [Header("Health")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float destroyDelay = 0f;

    [Header("Feedback")]
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private float hitFlashDuration = 0.08f;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.7f;

    [Header("Events")]
    public HealthChangedEvent onHealthChanged = new HealthChangedEvent();
    public UnityEvent onDamaged = new UnityEvent();
    public UnityEvent onDeath = new UnityEvent();

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine flashRoutine;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        onHealthChanged.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        onHealthChanged.Invoke(CurrentHealth, maxHealth);
        onDamaged.Invoke();

        if (hitFlashDuration > 0f && renderers.Length > 0)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(HitFlash());
        }

        if (!IsDead)
        {
            if (hitSound != null)
                SoundVariety.PlayAt(hitSound, transform.position, soundVolume);
            return;
        }

        if (deathSound != null)
            SoundVariety.PlayAt(deathSound, transform.position, soundVolume);

        onDeath.Invoke();
        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    private IEnumerator HitFlash()
    {
        SetTint(hitFlashColor, true);
        yield return new WaitForSeconds(hitFlashDuration);
        SetTint(Color.white, false);
        flashRoutine = null;
    }

    private void SetTint(Color color, bool apply)
    {
        if (!apply)
        {
            // Hand the renderers back to whatever normally colours them.
            var tint = GetComponent<RendererTint>();
            if (tint != null)
                tint.Apply();
            else
                foreach (var r in renderers)
                    r.SetPropertyBlock(null);
            return;
        }

        propertyBlock.SetColor(BaseColorId, color);
        foreach (var r in renderers)
            r.SetPropertyBlock(propertyBlock);
    }
}
