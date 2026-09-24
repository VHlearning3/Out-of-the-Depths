using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// The chase-sequence pufferfish. Unlike the normal one it never loses you: it follows your exact route (Player Trail
// breadcrumbs) through doors and around corners, pours on speed when it falls behind and eases off when it is right on
// you, then puffs up, lunges and bites with a shove. Hits To Kill bites from full health kill the player. The dagger
// does nothing to it. Asleep until a Chase Sequence calls Release(); Dismiss() sends it away, Sleep() puts it back.
[RequireComponent(typeof(Collider))]
public class ChasePufferfish : MonoBehaviour
{
    [Header("Swimming")]
    [SerializeField] private float speed = 2.4f;
    [Tooltip("Speed multiplier when far behind, so you can never simply leave it behind.")]
    [SerializeField] private float catchUpBoost = 1.5f;
    [Tooltip("Distance at which the catch-up boost is at full strength.")]
    [SerializeField] private float farDistance = 16f;
    [Tooltip("Speed multiplier when right behind you, so every bite is something you can react to.")]
    [SerializeField, Range(0.2f, 1.5f)] private float closeSpeedFactor = 0.7f;
    [SerializeField] private float closeDistance = 3f;
    [SerializeField] private float turnSpeed = 5f;

    [Header("Walls")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float bodyRadius = 0.45f;
    [SerializeField] private float lookAhead = 1.6f;
    [Tooltip("Keeps the pack from stacking on one spot.")]
    [SerializeField] private float separation = 1.4f;

    [Header("Bite")]
    [Tooltip("Bites from full health it takes to kill the player; the damage per bite comes from this.")]
    [SerializeField, Min(1)] private int hitsToKill = 3;
    [SerializeField] private float lungeRange = 2.4f;
    [SerializeField] private float lungeSpeed = 5.5f;
    [SerializeField] private float lungeDuration = 0.35f;
    [SerializeField] private float biteRange = 1.3f;
    [Tooltip("Shove given to the player on a bite, metres per second.")]
    [SerializeField] private float knockback = 5f;
    [Tooltip("How long it hangs back after a lunge, hit or miss, before hunting again.")]
    [SerializeField] private float recoverTime = 1.8f;
    [Tooltip("Puffs up to this size while lunging.")]
    [SerializeField] private float puffScale = 1.35f;
    [SerializeField] private AudioClip biteSound;
    [SerializeField] private AudioClip lungeSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    [Header("Leaving")]
    [Tooltip("When dismissed it turns away and fades out over this long.")]
    [SerializeField] private float fleeTime = 1.6f;

    [Header("Coming out")]
    [Tooltip("How far it swims straight out of its hole (along its start facing) before the hunt starts.")]
    [SerializeField] private float emergeDistance = 4.5f;
    [SerializeField] private float emergeSpeed = 2.2f;
    [Tooltip("Gives up on the way out and hunts anyway after this long.")]
    [SerializeField] private float emergeTimeout = 3f;

    [Header("Events")]
    public UnityEvent onBite = new UnityEvent();

    public bool IsHunting => state == State.Emerging || state == State.Hunting || state == State.Lunging || state == State.Recovering;
    public float DistanceToPlayer => target != null ? Vector3.Distance(transform.position, TargetPoint) : float.PositiveInfinity;

    private enum State { Dormant, Emerging, Hunting, Lunging, Recovering, Fleeing }

    private const int TrailChecksPerFrame = 6;
    private static readonly List<ChasePufferfish> active = new List<ChasePufferfish>();
    private static readonly RaycastHit[] hits = new RaycastHit[8];
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private State state = State.Dormant;
    private DamageManager target;
    private HealthSystem health;
    private SwimController swimmer;
    private PlayerTrail trail;
    private int trailIndex;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Transform visual;
    private Vector3 visualScale = Vector3.one;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private float speedScale = 1f;
    private float stateUntil;
    private Vector3 lungeDirection;
    private bool bitThisLunge;
    private float puff = 1f;
    private float fade = 1f;
    private Vector3 emergeTarget;
    private float emergeUntil;

    private Vector3 TargetPoint => target.transform.position + Vector3.up * 0.4f;

    private void Awake()
    {
        FishColors.Paint(gameObject, FishColors.Chase);   // testing colour: the chase pack
        spawnPosition = transform.position;
        // Hard spacing from the rest of the pack (Fish Space), on top of the soft Separation.
        FishSpace space = GetComponent<FishSpace>() != null ? GetComponent<FishSpace>() : gameObject.AddComponent<FishSpace>();
        space.Setup(bodyRadius, obstacleMask);
        spawnRotation = transform.rotation;
        visual = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (visual != null)
            visualScale = visual.localScale;
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        speedScale = Random.Range(0.9f, 1.1f);   // no two swim exactly alike, so the pack never moves as one blob
    }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    // Wake up in the start pose and go after the player (called by Chase Sequence).
    public void Release(DamageManager player, PlayerTrail playerTrail)
    {
        target = player;
        health = player != null ? player.GetComponentInParent<HealthSystem>() : null;
        swimmer = player != null ? player.GetComponentInParent<SwimController>() : null;
        trail = playerTrail;

        gameObject.SetActive(true);   // Awake runs here the first time and captures the start pose
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        foreach (Collider around in Physics.OverlapSphere(spawnPosition, bodyRadius * 0.5f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (around.transform.IsChildOf(transform))
                continue;
            Debug.LogWarning($"{name}: its start point is inside '{around.name}', so it may never get out. Move it into open water.", this);
            break;
        }
        trailIndex = trail != null ? trail.Oldest : 0;
        puff = 1f;
        fade = 1f;
        ApplyLook();
        emergeTarget = spawnPosition + spawnRotation * Vector3.forward * emergeDistance;
        emergeUntil = Time.time + emergeTimeout;
        state = target != null ? State.Emerging : State.Dormant;
    }

    // Turn away and fade out: the rubble has come down.
    // Set by the Chase Sequence near the end: how fast it may go (1 = normal), and a line it will not cross (the rubble,
    // so it is still on the far side when the rubble comes down). One already over the line swims back behind it.
    public float SpeedLimit { get; set; } = 1f;
    private bool holding;
    private Vector3 holdPoint;
    private Vector3 holdAway;   // the side it must stay out of
    private float holdMargin;

    public void HoldBehind(Vector3 point, Vector3 forbidden, float margin)
    {
        holding = true;
        holdPoint = point;
        holdAway = forbidden.normalized;
        holdMargin = margin;
    }

    public void StopHolding() => holding = false;

    public void Dismiss()
    {
        if (state == State.Dormant || state == State.Fleeing)
            return;
        state = State.Fleeing;
        stateUntil = Time.time + fleeTime;
    }

    // Straight back into its hole, invisible, ready for the next Release().
    public void Sleep()
    {
        state = State.Dormant;
        if (renderers != null)
        {
            puff = 1f;
            fade = 1f;
            ApplyLook();
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (target == null)
        {
            if (state != State.Dormant)
                Sleep();
            return;
        }

        switch (state)
        {
            case State.Emerging: Emerge(); break;
            case State.Hunting: Hunt(); break;
            case State.Lunging: Lunge(); break;
            case State.Recovering: Recover(); break;
            case State.Fleeing: Flee(); break;
        }

        float targetPuff = state == State.Lunging ? puffScale : state == State.Emerging ? 1f + 0.1f * Mathf.Sin(Time.time * 9f) : 1f;
        puff = Mathf.Lerp(puff, targetPuff, 1f - Mathf.Exp(-12f * Time.deltaTime));
        ApplyLook();
    }

    // Out of the hole into open water first, breathing hard, then the hunt starts.
    private void Emerge()
    {
        Vector3 to = emergeTarget - transform.position;
        if (to.magnitude < 0.6f || Time.time >= emergeUntil)
        {
            state = State.Hunting;
            return;
        }
        Vector3 direction = FishSteering.Avoid(transform.position, to.normalized, bodyRadius, lookAhead, obstacleMask, target.transform);
        Face(direction, turnSpeed);
        Move(transform.forward * (emergeSpeed * Time.deltaTime));
    }

    private void Hunt()
    {
        Vector3 playerPoint = TargetPoint;
        float distance = Vector3.Distance(transform.position, playerPoint);
        bool seesPlayer = Visible(transform.position, playerPoint);

        Vector3 goal;
        if (seesPlayer)
        {
            goal = playerPoint;
            if (trail != null)
                trailIndex = trail.Newest;
            if (distance <= lungeRange)
            {
                StartLunge(playerPoint);
                return;
            }
        }
        else if (trail != null && trail.Count > 0)
        {
            // Out of sight: head for the newest breadcrumb we can see, otherwise keep going for the current one.
            for (int i = 0; i < TrailChecksPerFrame; i++)
            {
                int index = trail.Newest - i;
                if (index <= trailIndex)
                    break;
                if (Visible(transform.position, trail.Get(index)))
                {
                    trailIndex = index;
                    break;
                }
            }
            if (trailIndex < trail.Oldest)
                trailIndex = trail.Oldest;
            goal = trail.Get(trailIndex);
            if (trailIndex < trail.Newest && (goal - transform.position).sqrMagnitude < 1f)
                goal = trail.Get(++trailIndex);
        }
        else
        {
            goal = playerPoint;
        }

        // Rubber band: pour it on when far behind, ease off when right on top of the player.
        float band = Mathf.InverseLerp(closeDistance, farDistance, distance);
        Swim(goal, speed * speedScale * Mathf.Lerp(closeSpeedFactor, catchUpBoost, band));
    }

    private void Swim(Vector3 goal, float currentSpeed)
    {
        Vector3 desired = goal - transform.position;
        if (desired.sqrMagnitude < 0.0001f)
            return;
        desired = desired.normalized + Separation();
        if (desired.sqrMagnitude < 0.0001f)
            return;
        desired = FishSteering.Avoid(transform.position, desired.normalized, bodyRadius, lookAhead, obstacleMask, target.transform);
        Face(desired, turnSpeed);
        Move(transform.forward * (currentSpeed * Time.deltaTime));
    }

    private void Move(Vector3 move)
    {
        move *= Mathf.Clamp01(SpeedLimit);
        if (holding)
        {
            // How far over the line it would be (positive = over): never further than Margin short of it, and one that
            // is over already drifts back behind it.
            float over = Vector3.Dot(transform.position + move - holdPoint, holdAway) + holdMargin;
            if (over > 0f)
            {
                float into = Mathf.Max(0f, Vector3.Dot(move, holdAway));
                move -= holdAway * Mathf.Min(into, over);
                float stillOver = Vector3.Dot(transform.position + move - holdPoint, holdAway) + holdMargin;
                if (stillOver > 0f)
                    move -= holdAway * Mathf.Min(stillOver, speed * Time.deltaTime);
            }
        }
        transform.position += FishSteering.ClampMove(transform.position, move, bodyRadius, obstacleMask, target != null ? target.transform : null);
    }

    private void Face(Vector3 direction, float rate)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), rate * Time.deltaTime);
    }

    private Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        foreach (ChasePufferfish other in active)
        {
            if (other == this || !other.IsHunting)
                continue;
            Vector3 away = transform.position - other.transform.position;
            float d = away.magnitude;
            if (d < 0.001f || d > separation)
                continue;
            push += away / d * (1f - d / separation);
        }
        return push;
    }

    private void StartLunge(Vector3 playerPoint)
    {
        state = State.Lunging;
        stateUntil = Time.time + lungeDuration;
        bitThisLunge = false;
        lungeDirection = (playerPoint - transform.position).normalized;
        Play(lungeSound);
    }

    private void Lunge()
    {
        Face(lungeDirection, turnSpeed * 3f);
        Move(lungeDirection * (lungeSpeed * Time.deltaTime));
        if (!bitThisLunge && DistanceToPlayer <= biteRange)
            Bite();
        if (Time.time >= stateUntil)
        {
            state = State.Recovering;
            stateUntil = Time.time + recoverTime;
        }
    }

    // Hang back for a moment so the player can get away, still facing them.
    private void Recover()
    {
        Vector3 toPlayer = TargetPoint - transform.position;
        Face(toPlayer, turnSpeed);
        if (toPlayer.magnitude < closeDistance)
            Move(-toPlayer.normalized * (speed * 0.4f * Time.deltaTime));
        if (Time.time >= stateUntil)
            state = State.Hunting;
    }

    private void Flee()
    {
        Vector3 away = transform.position - TargetPoint;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = -transform.forward;
        Vector3 direction = FishSteering.Avoid(transform.position, away.normalized, bodyRadius, lookAhead, obstacleMask, target.transform);
        Face(direction, turnSpeed);
        Move(transform.forward * (speed * Time.deltaTime));
        fade = Mathf.Clamp01((stateUntil - Time.time) / Mathf.Max(0.01f, fleeTime));
        if (Time.time >= stateUntil)
            Sleep();
    }

    private void Bite()
    {
        bitThisLunge = true;
        float damage = health != null ? Mathf.Ceil(health.MaxHealth / hitsToKill + 0.01f) : 34f;
        target.ApplyDamage(damage);
        if (swimmer != null)
        {
            swimmer.AddImpulse(lungeDirection * knockback + Vector3.up * 0.5f);
            swimmer.AddShake(0.8f);
        }
        Play(biteSound);
        onBite.Invoke();
    }

    // Clear line from here to there? The player and other fish never count as blocking.
    private bool Visible(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f)
            return true;

        int count = Physics.RaycastNonAlloc(from, delta / distance, hits, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hitTransform = hits[i].collider.transform;
            if (hitTransform.IsChildOf(transform) || (target != null && hitTransform.IsChildOf(target.transform)))
                continue;
            if (hits[i].collider.GetComponentInParent<FishController>() != null || hits[i].collider.GetComponentInParent<ChasePufferfish>() != null)
                continue;
            return false;
        }
        return true;
    }

    // Puff size and fade, applied to the Visual and its renderers (alpha where the material allows it, plus a shrink).
    private void ApplyLook()
    {
        if (visual != null)
            visual.localScale = visualScale * (puff * fade);
        if (renderers == null)
            return;

        foreach (Renderer r in renderers)
        {
            r.GetPropertyBlock(block);
            Color color = block.HasColor(BaseColorId) ? block.GetColor(BaseColorId)
                : r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId) ? r.sharedMaterial.GetColor(BaseColorId) : Color.white;
            color.a = fade;
            block.SetColor(BaseColorId, color);
            r.SetPropertyBlock(block);
        }
    }

    private void Play(AudioClip clip)
    {
        if (clip != null)
            SoundVariety.PlayAt(clip, transform.position, volume);
    }

    private void OnDrawGizmos()
    {
        // Where it waits and which way it comes out (its forward) before hunting.
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, bodyRadius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * emergeDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, lungeRange);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, biteRange);
    }
}
