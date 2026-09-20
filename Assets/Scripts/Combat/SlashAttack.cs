using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Left-click melee: swings the hands, plays sounds, and damages IDamageable targets in front of the camera.
public class SlashAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private MonoBehaviour handAnimator;

    [Header("Weapon")]
    [Tooltip("Slashing needs a Weapon item (dagger, trident) in the inventory. Off = always armed, for testing.")]
    [SerializeField] private bool requireWeapon = true;
    [Tooltip("Shown only while a weapon is owned, e.g. the dagger placeholder under Hands. Empty = the hand animator's object.")]
    [SerializeField] private GameObject weaponVisual;

    [Header("Attack")]
    [SerializeField] private bool canAttack = true;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private float hitDelay = 0.1f;
    [SerializeField] private float range = 2f;
    [SerializeField] private float hitRadius = 0.6f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Feedback")]
    [SerializeField] private AudioClip swingSound;
    [SerializeField, Range(0f, 1f)] private float swingVolume = 0.3f;
    [SerializeField, Range(0f, 0.5f)] private float pitchVariation = 0.12f;

    [Header("Hit Feedback")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.6f;
    [SerializeField] private HitMarker hitMarker;

    [Header("Events")]
    public UnityEvent onSlashStarted = new UnityEvent();
    public UnityEvent onSlashHit = new UnityEvent();

    public bool HasWeapon => !requireWeapon || (inventory != null && inventory.HasCategory(ItemDefinition.Category.Weapon));
    public bool CanAttack => canAttack;

    private InputAction attackAction;
    private IHandAnimator hands;
    private PlayerInventory inventory;
    private AudioSource audioSource;
    private float nextAttackTime;
    private readonly RaycastHit[] hitResults = new RaycastHit[16];
    private readonly Collider[] overlapResults = new Collider[16];
    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

    private void Awake()
    {
        attackAction = inputActions.FindActionMap("Player").FindAction("Attack");
        hands = handAnimator as IHandAnimator;
        inventory = GetComponentInParent<PlayerInventory>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (handAnimator != null && hands == null)
            Debug.LogError($"{name}: Hand Animator must implement IHandAnimator.", this);
        if (requireWeapon && inventory == null)
            Debug.LogWarning($"{name}: no Player Inventory on the player, so no weapon can be owned and slashing stays locked.", this);
    }

    private void OnEnable()
    {
        attackAction.performed += OnAttackPerformed;
        if (inventory != null)
            inventory.onChanged.AddListener(RefreshWeapon);
        RefreshWeapon();
    }

    private void OnDisable()
    {
        attackAction.performed -= OnAttackPerformed;
        if (inventory != null)
            inventory.onChanged.RemoveListener(RefreshWeapon);
    }

    public void SetCanAttack(bool value)
    {
        canAttack = value;
    }

    private void RefreshWeapon()
    {
        GameObject visual = weaponVisual != null ? weaponVisual : (handAnimator != null ? handAnimator.gameObject : null);
        if (visual != null)
            visual.SetActive(HasWeapon);
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        if (!canAttack || !HasWeapon || Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        hands?.PlaySlash();
        onSlashStarted.Invoke();

        if (swingSound != null)
        {
            audioSource.pitch = Random.Range(1f - pitchVariation, 1f + pitchVariation);
            audioSource.PlayOneShot(swingSound, swingVolume);
        }

        StartCoroutine(ApplyHitAfterDelay());
    }

    // hitDelay lines the damage up with the animation's impact frame instead of the button press.
    private IEnumerator ApplyHitAfterDelay()
    {
        yield return new WaitForSeconds(hitDelay);
        ApplyHit();
    }

    private void ApplyHit()
    {
        hitThisSwing.Clear();

        Vector3 origin = attackOrigin.position;
        Vector3 direction = attackOrigin.forward;

        // A sphere cast ignores anything already overlapping its start, so also check
        // point-blank targets with an overlap right in front of the camera.
        // Triggers included: moving fish use trigger colliders so they can't shove the player.
        int overlapCount = Physics.OverlapSphereNonAlloc(origin + direction * hitRadius, hitRadius, overlapResults, hitMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlapCount; i++)
            TryDamage(overlapResults[i]);

        int castCount = Physics.SphereCastNonAlloc(origin, hitRadius, direction, hitResults, range, hitMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < castCount; i++)
            TryDamage(hitResults[i].collider);

        if (hitThisSwing.Count == 0)
            return;

        if (hitSound != null)
        {
            audioSource.pitch = Random.Range(1f - pitchVariation, 1f + pitchVariation);
            audioSource.PlayOneShot(hitSound, hitVolume);
        }

        hitMarker?.Show();
        onSlashHit.Invoke();
    }

    private void TryDamage(Collider hit)
    {
        // Ignore our own colliders. transform.root is not safe here: the player shares
        // a parent object with the level, so it would exclude everything in the scene.
        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return;

        var damageable = hit.GetComponentInParent<IDamageable>();
        if (damageable == null || !hitThisSwing.Add(damageable))
            return;

        damageable.TakeDamage(damage);
    }
}
