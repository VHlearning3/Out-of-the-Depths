using UnityEngine;

// "Ilkeä Pallokala": chases the player when close and bites on contact.
[RequireComponent(typeof(FishWander))]
public class FishAggression : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectRange = 5f;
    [SerializeField] private float loseRange = 8f;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 3f;
    [SerializeField] private float turnSpeed = 4f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private AudioClip attackSound;
    [SerializeField, Range(0f, 1f)] private float attackVolume = 0.7f;

    public bool IsChasing { get; private set; }
    // Every fish chasing the player right now (the HUD's direction indicators point at them).
    public static readonly System.Collections.Generic.List<FishAggression> Chasing = new System.Collections.Generic.List<FishAggression>();

    private FishWander wander;
    private FishKnockback knockback;
    private DamageManager target;
    private float nextAttackTime;

    private void Awake()
    {
        wander = GetComponent<FishWander>();
        knockback = GetComponent<FishKnockback>();
    }

    private void Start()
    {
        target = FindFirstObjectByType<DamageManager>();
    }

    private void OnDisable()
    {
        SetChasing(false);
    }

    private void Update()
    {
        if (target == null)
            return;

        Vector3 targetPoint = target.transform.position + Vector3.up;
        float distance = Vector3.Distance(transform.position, targetPoint);

        if (!IsChasing && distance <= detectRange)
            SetChasing(true);
        else if (IsChasing && distance > loseRange)
            SetChasing(false);

        if (!IsChasing)
            return;
        if (knockback == null)
            knockback = GetComponent<FishKnockback>();
        if (knockback != null && knockback.Stunned)
            return;   // knocked back by a hit: dazed, no chasing or biting until it clears

        // Steer around walls on the way, but never treat the player being chased as one.
        Vector3 toTarget = (targetPoint - transform.position).normalized;
        toTarget = FishSteering.Avoid(transform.position, toTarget, wander.BodyRadius, wander.LookAhead, wander.ObstacleMask, target.transform);
        Quaternion desired = Quaternion.LookRotation(toTarget, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, turnSpeed * Time.deltaTime);

        if (distance > attackRange)
        {
            Vector3 move = transform.forward * (chaseSpeed * Time.deltaTime);
            transform.position += FishSteering.ClampMove(transform.position, move, wander.BodyRadius, wander.ObstacleMask, target.transform);
        }
        else if (Time.time >= nextAttackTime)
        {
            Bite();
        }
    }

    private void Bite()
    {
        nextAttackTime = Time.time + attackCooldown;
        target.ApplyDamage(damage, transform.position);
        if (attackSound != null)
            SoundVariety.PlayAt(attackSound, transform.position, attackVolume);
    }

    private void SetChasing(bool chasing)
    {
        IsChasing = chasing;
        if (chasing && !Chasing.Contains(this))
            Chasing.Add(this);
        else if (!chasing)
            Chasing.Remove(this);
        if (wander != null && isActiveAndEnabled)
            wander.enabled = !chasing;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
