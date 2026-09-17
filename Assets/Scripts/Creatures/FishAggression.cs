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

    private FishWander wander;
    private DamageManager target;
    private float nextAttackTime;

    private void Awake()
    {
        wander = GetComponent<FishWander>();
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

        Vector3 toTarget = (targetPoint - transform.position).normalized;
        Quaternion desired = Quaternion.LookRotation(toTarget, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, turnSpeed * Time.deltaTime);

        if (distance > attackRange)
            transform.position += transform.forward * (chaseSpeed * Time.deltaTime);
        else if (Time.time >= nextAttackTime)
            Bite();
    }

    private void Bite()
    {
        nextAttackTime = Time.time + attackCooldown;
        target.ApplyDamage(damage);
        if (attackSound != null)
            AudioSource.PlayClipAtPoint(attackSound, transform.position, attackVolume);
    }

    private void SetChasing(bool chasing)
    {
        IsChasing = chasing;
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
