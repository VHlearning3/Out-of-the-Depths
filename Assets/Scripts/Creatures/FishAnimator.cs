using UnityEngine;

// Plays a fish model's own animations (an Animator on the model under Visual). Two sets, made by Fish Model Tools:
//  - the friendly fish (Art/Models/fish, NormalFish.controller): Swim, Scatter, Hit, Dying, DeadIdle;
//  - the enemy pufferfish (Art/Models/bad fish, BadFish.controller): Swim, Attack, Hit, Dying, DeadIdle.
// Swim the whole time, quicker or slower with how fast the fish really moves (Cruise Speed = played as made); Scatter,
// the startled burst, the moment it starts to flee (Fish Wander: a slash near it); Attack when it bites (Fish
// Aggression) or lunges (Chase Pufferfish); Hit when a slash hurts it; Dying (the hit, then it rolls belly-up) when it is
// killed, then Dead Idle, belly-up and bobbing, while it drifts up; Swim again when it respawns. A controller without
// one of these (no Scatter on the pufferfish, no Attack on the friendly fish) simply never gets asked for it. Start
// Dead: dead from the start (the dead fish lying about to eat). Each fish starts at its own point of the swim, so a
// school never flaps in step. While this drives a death animation, Fish Controller leaves the belly-up roll to it (it
// would roll the fish twice). Put on the prefabs, with the model, by Tools > Out of the Depths > Use Normal Fish Model
// On Fish Prefabs / Use Bad Fish Model On Enemy Prefabs.
[DisallowMultipleComponent]
public class FishAnimator : MonoBehaviour
{
    [Tooltip("The model's Animator. Empty = the first one under this object.")]
    [SerializeField] private Animator animator;
    [Tooltip("Dead from the start: Dead Idle (belly-up, bobbing) and nothing else. For the dead fish lying about to eat.")]
    [SerializeField] private bool startDead;
    [Tooltip("Metres per second at which the swim plays at its own speed; moving faster or slower speeds it up or slows it down (a fleeing fish beats its tail hard).")]
    [SerializeField, Min(0.05f)] private float cruiseSpeed = 1.2f;
    [Tooltip("The slowest and fastest the swim is played (1 = as made). Even a fish hanging still keeps waving gently.")]
    [SerializeField] private Vector2 swimSpeedRange = new Vector2(0.7f, 2.2f);

    private static readonly int SwimState = Animator.StringToHash("Swim");
    private static readonly int DeadIdleState = Animator.StringToHash("DeadIdle");
    private static readonly int HitId = Animator.StringToHash("Hit");
    private static readonly int ScatterId = Animator.StringToHash("Scatter");
    private static readonly int AttackId = Animator.StringToHash("Attack");
    private static readonly int DieId = Animator.StringToHash("Die");
    private static readonly int SwimSpeedId = Animator.StringToHash("SwimSpeed");

    private Damageable damageable;
    private EdibleFish edible;
    private FishWander wander;
    private FishAggression aggression;
    private ChasePufferfish chaser;
    private bool dead;
    private bool wasFleeing;
    private Vector3 lastPosition;
    private float smoothedSpeed;
    private bool lookedUp;
    private bool hasHit, hasScatter, hasAttack, hasDie, hasSwimSpeed;

    // The model rolls itself belly-up when it dies (its Dying animation).
    public bool PlaysDeath => animator != null && animator.runtimeAnimatorController != null;
    public Animator Animator => animator;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        damageable = GetComponent<Damageable>();
        edible = GetComponent<EdibleFish>();
        wander = GetComponent<FishWander>();
        aggression = GetComponent<FishAggression>();
        chaser = GetComponent<ChasePufferfish>();
        if (animator != null)
        {
            animator.applyRootMotion = false;   // the scripts move the fish; the animation only bends it
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // nobody sees it: no bending
        }
    }

    private void OnEnable()
    {
        if (damageable != null)
        {
            damageable.onDamaged.AddListener(OnDamaged);
            damageable.onDeath.AddListener(OnDeath);
        }
        if (edible != null)
            edible.onRespawned.AddListener(OnRespawned);
        if (aggression != null)
            aggression.Bit += Attack;
        if (chaser != null)
            chaser.Lunged += Attack;
        lastPosition = transform.position;
    }

    private void OnDisable()
    {
        if (damageable != null)
        {
            damageable.onDamaged.RemoveListener(OnDamaged);
            damageable.onDeath.RemoveListener(OnDeath);
        }
        if (edible != null)
            edible.onRespawned.RemoveListener(OnRespawned);
        if (aggression != null)
            aggression.Bit -= Attack;
        if (chaser != null)
            chaser.Lunged -= Attack;
    }

    private void Start()
    {
        if (!PlaysDeath)
            return;
        dead = startDead;
        animator.Play(startDead ? DeadIdleState : SwimState, 0, Random.value);
    }

    // Which of the triggers this fish's controller has (looked up once).
    private void LookUp()
    {
        if (lookedUp || !PlaysDeath)
            return;
        lookedUp = true;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            hasHit |= p.nameHash == HitId;
            hasScatter |= p.nameHash == ScatterId;
            hasAttack |= p.nameHash == AttackId;
            hasDie |= p.nameHash == DieId;
            hasSwimSpeed |= p.nameHash == SwimSpeedId;
        }
    }

    private void Trigger(int id, bool has)
    {
        if (has)
            animator.SetTrigger(id);
    }

    private void Clear(int id, bool has)
    {
        if (has)
            animator.ResetTrigger(id);
    }

    // A bite or a lunge: the attack, then back to swimming.
    public void Attack()
    {
        if (!PlaysDeath || dead || !animator.isActiveAndEnabled)
            return;
        LookUp();
        Trigger(AttackId, hasAttack);
    }

    private void OnDamaged()
    {
        if (!PlaysDeath || dead || (damageable != null && damageable.IsDead))
            return;   // the killing blow plays Dying instead
        LookUp();
        Clear(AttackId, hasAttack);
        Trigger(HitId, hasHit);
    }

    private void OnDeath()
    {
        if (!PlaysDeath || dead)
            return;
        dead = true;
        LookUp();
        Clear(HitId, hasHit);
        Clear(ScatterId, hasScatter);
        Clear(AttackId, hasAttack);
        Trigger(DieId, hasDie);
    }

    private void OnRespawned()
    {
        if (!PlaysDeath)
            return;
        LookUp();
        Clear(HitId, hasHit);
        Clear(ScatterId, hasScatter);
        Clear(AttackId, hasAttack);
        Clear(DieId, hasDie);
        dead = startDead;
        wasFleeing = false;
        animator.Play(startDead ? DeadIdleState : SwimState, 0, Random.value);
    }

    private void Update()
    {
        if (!PlaysDeath)
            return;
        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            float speed = (transform.position - lastPosition).magnitude / dt;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-4f * dt));
        }
        lastPosition = transform.position;
        if (dead || !animator.isActiveAndEnabled)
            return;
        LookUp();

        float low = Mathf.Min(swimSpeedRange.x, swimSpeedRange.y), high = Mathf.Max(swimSpeedRange.x, swimSpeedRange.y);
        if (hasSwimSpeed)
            animator.SetFloat(SwimSpeedId, Mathf.Clamp(smoothedSpeed / cruiseSpeed, low, high));

        bool fleeing = wander != null && wander.enabled && wander.Fleeing;
        if (fleeing && !wasFleeing)
            Trigger(ScatterId, hasScatter);   // startled: the burst, then back to swimming (fast, it is fleeing)
        wasFleeing = fleeing;
    }
}
