using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The chase as one thing: a wall of hundreds of pufferfish that comes after the player at one set speed. It pours out
// of its hole (the fish fly out one after another and take their places), then swims along the player's own route
// (Player Trail breadcrumbs through doors and round corners; straight at the player while it can see them) at Speed,
// always the same, so keep swimming and it cannot catch you; stop to fiddle with something and it closes in. It fills
// whatever corridor it is in: every frame it feels for the walls, floor and ceiling round it and spreads its fish
// across that, a rounded wall front to back Depth metres deep, the front row puffing up as it nears you. Caught (you
// are in its front), a bite takes 1 / Hits To Kill of your health, shoves you on ahead and it holds still for Recover
// Seconds so you can get away. Dismiss() scatters it (the rubble came down); Sleep() puts it back in its hole.
// It is one object: no fish has a collider or a brain; the fish are generated low-poly pufferfish (spikes, big eyes,
// a pout) drawn with GPU instancing, a handful of draw calls for all of them. Chase Sequence makes one for itself when
// it has none (Use Swarm), or place one under it.
public class ChaseSwarm : MonoBehaviour
{
    [Header("Moving")]
    [Tooltip("Metres per second, all the way: it never speeds up to catch you and never slows (2.1, just under the player's 2.2: keep swimming and you stay ahead, stop to grab or place something and it closes in; a dash is much faster). Keep moving and it cannot catch you.")]
    [SerializeField] private float speed = 2.1f;
    [Tooltip("How far it pours straight out of its hole (along its start facing) before it goes after the player.")]
    [SerializeField] private float emergeDistance = 3f;
    [Tooltip("Keeps the chase close: while it is more than this far behind (metres) AND you cannot see it, it hurries (Hurry times Speed) until it is within it again. 0 = never: strictly the set speed.")]
    [SerializeField] private float keepWithin = 20f;
    [SerializeField] private float hurry = 2.5f;
    [Tooltip("The start: it bursts out and comes at this many times Speed until it first gets within Opening Until Within metres of the player, so the chase is on at once instead of a long swim up from its hole.")]
    [SerializeField] private float openingBoost = 2.2f;
    [SerializeField] private float openingUntilWithin = 9f;
    [Tooltip("How wide its front is for getting through gaps (metres, a radius): it only goes straight at the player when a body this wide fits the whole way, else it follows the player's route, so it never jams on a door frame.")]
    [SerializeField] private float bodyRadius = 0.35f;
    [Tooltip("How quickly the wall turns to face the way it is going.")]
    [SerializeField] private float turnRate = 2.5f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("The wall")]
    [SerializeField, Range(20, 1000)] private int count = 420;
    [Tooltip("Size of one pufferfish, metres across, smallest and largest.")]
    [SerializeField] private Vector2 fishSize = new Vector2(0.35f, 0.6f);
    [Tooltip("How deep the wall is, front to back, metres.")]
    [SerializeField] private float depth = 3f;
    [Tooltip("It fills the corridor it is in, up to this far out from its middle (metres), leaving Wall Gap to the walls.")]
    [SerializeField] private float maxRadius = 4f;
    [SerializeField] private float wallGap = 0.25f;
    [Tooltip("Seconds it takes to pour out of its hole at the start.")]
    [SerializeField] private float pourSeconds = 1f;
    [Tooltip("The fish come in these colours, a share each.")]
    [SerializeField] private Color[] colors = { new Color(0.95f, 0.76f, 0.29f), new Color(0.91f, 0.59f, 0.23f), new Color(0.85f, 0.82f, 0.48f) };
    [Tooltip("A faint glow of their own, so the wall reads in the dark water.")]
    [SerializeField, Range(0f, 0.5f)] private float glow = 0.2f;
    [Tooltip("When dismissed the fish scatter and shrink away over this long.")]
    [SerializeField] private float leaveSeconds = 1.6f;

    [Header("Catching you")]
    [Tooltip("Catches from full health it takes to kill the player; the damage per catch comes from this.")]
    [SerializeField, Min(1)] private int hitsToKill = 3;
    [Tooltip("Shove on a catch, metres per second, the way the wall is going (on ahead of it).")]
    [SerializeField] private float knockback = 6f;
    [Tooltip("After a catch it holds still this long so you can get away.")]
    [SerializeField] private float recoverSeconds = 1.4f;
    [Tooltip("How far in front of its front row you count as caught (metres).")]
    [SerializeField] private float reach = 0.5f;
    [SerializeField] private AudioClip biteSound;
    [Tooltip("Loops from the wall while it hunts (a rush of water), louder as it nears you, pitched down.")]
    [SerializeField] private AudioClip rushLoop;
    [SerializeField, Range(0.3f, 1.5f)] private float rushPitch = 0.75f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    // The one hunting right now (the HUD's direction indicators point at it), or null.
    public static ChaseSwarm Active { get; private set; }
    public bool IsHunting => state == State.Hunting;
    public float DistanceToPlayer { get; private set; } = float.PositiveInfinity;
    // Set by the Chase Sequence near the end: how fast it may go (1 = Speed), and a line it will not cross.
    public float SpeedLimit { get; set; } = 1f;

    private enum State { Asleep, Hunting, Leaving }

    private State state = State.Asleep;
    private DamageManager target;
    private HealthSystem health;
    private SwimController swimmer;
    private PlayerTrail trail;
    private int trailIndex;

    private Vector3 home;               // where it waits (its hole)
    private Vector3 startFacing = Vector3.forward;
    private Vector3 head;               // the middle of its front row, on the route
    private Vector3 forward = Vector3.forward;
    private Vector3 right = Vector3.right, up = Vector3.up;
    private bool emerging;
    private Vector3 emergeTarget;
    private float rx = 0.6f, ry = 0.6f, ox, oy;   // the wall's half-size and offset across the corridor
    private float releasedAt;
    private float pausedUntil;
    private float caughtAt = -10f;
    private float leavingAt;
    private bool holding;
    private Vector3 holdPoint, holdAway;
    private float holdMargin;
    private bool placed;
    private bool opening;              // the start rush (Opening Boost), until it is first close
    private float stuckFor;            // seconds it has hardly moved while it should have
    private float routeOnlyUntil;      // after being stuck: follow the route, not the straight line, until then
    private AudioSource rush;

    // The fish: where each sits in the wall (u, v across it, -1..1; w back from the front, 0..1), how big, its own
    // wobble, colour, when it pours out, and which way it scatters.
    private float[] fu, fv, fw, fsize, fphase, fappear;
    private int[] fvariant;
    private Vector3[] fscatter;
    private Matrix4x4[][] bodyMatrices;
    private int[] bodyCounts;
    private Matrix4x4[] allMatrices;

    private static Mesh bodyMesh, whiteMesh, pupilMesh;
    private Material[] bodyMaterials;
    private Material whiteMaterial, pupilMaterial;
    private readonly RaycastHit[] hits = new RaycastHit[12];

    // The middle of the player's body (the player object's origin is at its feet, so aiming there dragged the wall down
    // into the floor), and a breadcrumb lifted the same way.
    private Vector3 PlayerPoint => target.transform.position + Vector3.up * bodyLift;
    private Vector3 Crumb(int index) => trail.Get(index) + Vector3.up * bodyLift;
    private float bodyLift = 1f;   // from the player's origin up to the middle of its Character Controller

    private void Awake()
    {
        Place();
    }

    private void Place()
    {
        if (placed)
            return;
        placed = true;
        home = transform.position;
        if (startFacing == Vector3.forward)
            startFacing = transform.forward;
    }

    // For a swarm made in code (the Chase Sequence): which way it pours out, and the sounds, when none are set.
    public void Setup(Vector3 facing, AudioClip bite, AudioClip rushSound)
    {
        if (facing.sqrMagnitude > 0.01f)
            startFacing = facing.normalized;
        if (biteSound == null)
            biteSound = bite;
        if (rushLoop == null)
            rushLoop = rushSound;
    }

    // Out of the hole and after the player.
    public void Release(DamageManager player, PlayerTrail playerTrail)
    {
        Place();
        target = player;
        health = player != null ? player.GetComponentInParent<HealthSystem>() : null;
        swimmer = player != null ? player.GetComponentInParent<SwimController>() : null;
        trail = playerTrail;
        CharacterController body = player != null ? player.GetComponentInParent<CharacterController>() : null;
        bodyLift = body != null ? body.transform.TransformPoint(body.center).y - player.transform.position.y : 1f;
        trailIndex = trail != null ? trail.Oldest : 0;
        head = home;
        forward = startFacing;
        emerging = emergeDistance > 0.05f;
        emergeTarget = home + startFacing * emergeDistance;
        rx = ry = 0.6f;
        ox = oy = 0f;
        releasedAt = Time.time;
        pausedUntil = 0f;
        opening = openingBoost > 1f;
        stuckFor = 0f;
        routeOnlyUntil = 0f;
        caughtAt = -10f;
        holding = false;
        SpeedLimit = 1f;
        BuildFish();
        state = target != null ? State.Hunting : State.Asleep;
        if (state == State.Hunting)
        {
            Active = this;
            StartRush();
        }
    }

    // The rubble came down: the fish scatter and shrink away.
    public void Dismiss()
    {
        if (state != State.Hunting)
            return;
        state = State.Leaving;
        leavingAt = Time.time;
        if (Active == this)
            Active = null;
    }

    // Back in its hole, invisible, ready for the next Release().
    public void Sleep()
    {
        state = State.Asleep;
        if (Active == this)
            Active = null;
        if (rush != null)
            rush.Stop();
        DistanceToPlayer = float.PositiveInfinity;
        transform.position = home;
    }

    public void HoldBehind(Vector3 point, Vector3 forbidden, float margin)
    {
        holding = true;
        holdPoint = point;
        holdAway = forbidden.normalized;
        holdMargin = margin;
    }

    public void StopHolding() => holding = false;

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    private void Update()
    {
        if (state == State.Asleep)
            return;
        if (target == null)
        {
            Sleep();
            return;
        }
        float dt = Time.deltaTime;
        if (state == State.Hunting)
        {
            DistanceToPlayer = Vector3.Distance(head, PlayerPoint);
            Advance(dt);
            Fit(dt);
            CheckCatch();
        }
        else if (Time.time - leavingAt >= leaveSeconds)
        {
            Sleep();
            return;
        }
        UpdateRush();
        transform.position = head;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    // ---- Moving ---------------------------------------------------------------------------------------------------

    private void Advance(float dt)
    {
        if (Time.time < pausedUntil)
            return;
        Vector3 playerPoint = PlayerPoint;
        float step = speed * Mathf.Clamp01(SpeedLimit) * dt;
        if (opening && DistanceToPlayer <= openingUntilWithin)
            opening = false;
        if (opening)
            step *= openingBoost;
        else if (keepWithin > 0f && DistanceToPlayer > keepWithin && !Seen())
            step *= hurry;
        bool routeOnly = Time.time < routeOnlyUntil;

        Vector3 goal;
        if (emerging)
        {
            goal = emergeTarget;
            if ((goal - head).sqrMagnitude < 0.04f)
                emerging = false;
        }
        else if (!routeOnly && Visible(head, playerPoint))
        {
            goal = playerPoint;   // straight at the player while it can see them (and fits the whole way)
            if (trail != null)
                trailIndex = trail.Newest;
        }
        else if (trail != null && trail.Count > 0)
        {
            // Round a corner: the newest breadcrumb it can see, else keep going for the current one.
            for (int i = 0; i < 6 && !routeOnly; i++)
            {
                int index = trail.Newest - i;
                if (index <= trailIndex)
                    break;
                if (Visible(head, Crumb(index)))
                {
                    trailIndex = index;
                    break;
                }
            }
            trailIndex = Mathf.Max(trailIndex, trail.Oldest);
            goal = Crumb(trailIndex);
            if (trailIndex < trail.Newest && (goal - head).sqrMagnitude < 0.25f)
                goal = Crumb(++trailIndex);
        }
        else
        {
            goal = playerPoint;
        }

        Vector3 to = goal - head;
        float distance = to.magnitude;
        if (distance < 1e-4f)
            return;
        Vector3 move = to / distance * Mathf.Min(step, distance);
        if (holding)
        {
            // Never over the line (the rubble): it stops Margin short of it.
            float over = Vector3.Dot(head + move - holdPoint, holdAway) + holdMargin;
            if (over > 0f)
                move -= holdAway * over;
        }
        // Its front never goes through a wall: it slides along one (a floor it grazes, a door frame it clips) instead of
        // stopping dead; out of the hole it is free, the grate is flying.
        if (!emerging)
            move = SlideMove(head, move);
        head += move;

        // Jammed (against a frame, in a corner) while it should be moving: drop back onto the player's own route from
        // the breadcrumb nearest to it and follow that, one crumb after the next, for a couple of seconds.
        bool held = holding && Vector3.Dot(head - holdPoint, holdAway) + holdMargin > -0.2f;
        if (!emerging && !held && Mathf.Min(step, distance) > 1e-4f && move.magnitude < Mathf.Min(step, distance) * 0.3f)
            stuckFor += dt;
        else
            stuckFor = Mathf.Max(0f, stuckFor - dt * 2f);
        if (stuckFor > 0.4f && trail != null && trail.Count > 0 && routeOnly && !holding && !Seen()
            && DistanceToPlayer > 6f && (Crumb(Mathf.Min(trailIndex + 1, trail.Newest)) - PlayerPoint).magnitude > 6f)   // never hops up to the player: it has to swim the last bit, in the open
        {
            // Still jammed while already on the route, and the player is not looking: step it along the route (the
            // player swam there, so there is room) until it is free again. Nobody sees the hop.
            stuckFor = 0.3f;
            trailIndex = Mathf.Min(trailIndex + 1, trail.Newest);
            head = Crumb(trailIndex);
            routeOnlyUntil = Time.time + 2.5f;
        }
        else if (stuckFor > 0.4f && trail != null && trail.Count > 0)
        {
            stuckFor = 0f;
            routeOnlyUntil = Time.time + 2.5f;
            int nearest = trail.Oldest;
            float best = float.MaxValue;
            for (int i = trail.Oldest; i <= trail.Newest; i++)
            {
                float d = (Crumb(i) - head).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }
            trailIndex = nearest;
        }

        if (distance > 0.05f)
        {
            Vector3 want = to / distance;
            want.y = Mathf.Clamp(want.y, -0.45f, 0.45f);   // a wall, not a column: it never tips over
            forward = Vector3.Slerp(forward, want.normalized, 1f - Mathf.Exp(-turnRate * dt)).normalized;
        }
    }

    // Spread across the corridor: feel for the walls, floor and ceiling and fill the space between them (always with
    // its front's middle, the route, inside it).
    private void Fit(float dt)
    {
        right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 1e-4f)
            right = Vector3.right;
        right.Normalize();
        up = Vector3.Cross(forward, right).normalized;
        float far = maxRadius * 2f;
        float left = Probe(-right, far), rightSide = Probe(right, far), down = Probe(-up, far), upSide = Probe(up, far);
        float wantRx = Mathf.Clamp((left + rightSide) * 0.5f - wallGap, 0.5f, maxRadius);
        float wantRy = Mathf.Clamp((down + upSide) * 0.5f - wallGap, 0.5f, maxRadius);
        float wantOx = Mathf.Clamp((rightSide - left) * 0.5f, -(wantRx - 0.3f), wantRx - 0.3f);
        float wantOy = Mathf.Clamp((upSide - down) * 0.5f, -(wantRy - 0.3f), wantRy - 0.3f);
        float k = 1f - Mathf.Exp(-4f * dt);
        rx = Mathf.Lerp(rx, wantRx, k);
        ry = Mathf.Lerp(ry, wantRy, k);
        ox = Mathf.Lerp(ox, wantOx, k);
        oy = Mathf.Lerp(oy, wantOy, k);
    }

    // How far to the nearest solid thing that way (not the player, not fish), up to `far`.
    private float Probe(Vector3 direction, float far)
    {
        int n = Physics.RaycastNonAlloc(head, direction, hits, far, obstacleMask, QueryTriggerInteraction.Ignore);
        float best = far;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].distance >= best || Ignored(hits[i].collider))
                continue;
            best = hits[i].distance;
        }
        return best;
    }

    private bool Ignored(Collider collider) =>
        PlayerBody.Is(collider) || (target != null && collider.transform.IsChildOf(target.transform)) ||
        collider.GetComponentInParent<FishController>() != null || collider.GetComponentInParent<ChasePufferfish>() != null;

    // Move, but never into anything solid: up to what is in the way, then the rest of the move along its surface (a
    // floor it grazes, a door frame it clips), twice over for corners.
    private Vector3 SlideMove(Vector3 from, Vector3 move)
    {
        const float radius = 0.3f, skin = 0.05f;
        Vector3 done = Vector3.zero;
        for (int pass = 0; pass < 3; pass++)
        {
            float length = move.magnitude;
            if (length < 1e-5f)
                break;
            Vector3 direction = move / length;
            int n = Physics.SphereCastNonAlloc(from + done, radius, direction, hits, length + skin, obstacleMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance <= 0f && hit.point == Vector3.zero)
                    continue;   // started inside it: it is round us, not ahead
                if (hit.distance >= nearest || Ignored(hit.collider))
                    continue;
                nearest = hit.distance;
                normal = hit.normal;
            }
            if (nearest == float.MaxValue)
            {
                done += move;
                break;
            }
            float allowed = Mathf.Max(0f, nearest - skin);
            done += direction * Mathf.Min(length, allowed);
            move = Vector3.ProjectOnPlane(direction * Mathf.Max(0f, length - allowed), normal);
        }
        return done;
    }

    // A clear line from here to there? The player and fish never count as blocking.
    private bool Visible(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f)
            return true;
        // A sweep as wide as its front, not a thin line: a line can slip past a door frame the body cannot.
        int n = Physics.SphereCastNonAlloc(from, bodyRadius, delta / distance, hits, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (!Ignored(hits[i].collider) && !(hits[i].distance <= 0f && hits[i].point == Vector3.zero))   // (not what it is already touching)
                return false;
        return true;
    }

    // Roughly on screen (for Keep Within: it only hurries where you cannot see it).
    private bool Seen()
    {
        Camera eye = Camera.main;
        if (eye == null)
            return false;
        Vector3 view = eye.WorldToViewportPoint(head);
        return view.z > 0f && view.z < 40f && view.x > -0.1f && view.x < 1.1f && view.y > -0.1f && view.y < 1.1f;
    }

    // ---- Catching you ---------------------------------------------------------------------------------------------

    private void CheckCatch()
    {
        if (Time.time < pausedUntil || emerging)
            return;
        Vector3 rel = PlayerPoint - head;
        float along = Vector3.Dot(rel, forward);
        if (along > reach || along < -depth)
            return;
        float x = (Vector3.Dot(rel, right) - ox) / (rx + 0.5f);
        float y = (Vector3.Dot(rel, up) - oy) / (ry + 0.5f);
        if (x * x * x * x + y * y * y * y > 1f)
            return;   // beside it (in a room wider than it), not in it
        Catch();
    }

    private void Catch()
    {
        float damage = health != null ? Mathf.Ceil(health.MaxHealth / hitsToKill + 0.01f) : 34f;
        target.ApplyDamage(damage, head);
        if (swimmer != null)
        {
            swimmer.AddImpulse(forward * knockback + Vector3.up * 0.6f);
            swimmer.AddShake(0.9f);
        }
        if (biteSound != null)
            SoundVariety.PlayAt(biteSound, PlayerPoint, volume);
        pausedUntil = Time.time + recoverSeconds;
        caughtAt = Time.time;
    }

    private void StartRush()
    {
        if (rushLoop == null)
            return;
        if (rush == null)
        {
            rush = gameObject.AddComponent<AudioSource>();
            rush.loop = true;
            rush.playOnAwake = false;
            rush.spatialBlend = 1f;
            rush.rolloffMode = AudioRolloffMode.Linear;
            rush.minDistance = 2f;
            rush.maxDistance = 28f;
            rush.dopplerLevel = 0f;
        }
        rush.clip = rushLoop;
        rush.pitch = rushPitch;
        rush.volume = 0f;
        rush.time = Random.Range(0f, rushLoop.length * 0.5f);
        rush.Play();
    }

    private void UpdateRush()
    {
        if (rush == null || !rush.isPlaying)
            return;
        float near = Mathf.Clamp01(1f - (DistanceToPlayer - 2f) / 16f);
        float want = state == State.Leaving ? 0f : volume * (0.35f + 0.65f * near);
        rush.volume = Mathf.MoveTowards(rush.volume, want, Time.deltaTime * 0.8f);
    }

    // ---- The fish ---------------------------------------------------------------------------------------------------

    private void BuildFish()
    {
        if (fu != null && fu.Length == count)
            return;
        var random = new System.Random(1234);
        float Next() => (float)random.NextDouble();
        int n = Mathf.Max(1, count);
        var order = new List<(float w, int i)>(n);
        fu = new float[n]; fv = new float[n]; fw = new float[n]; fsize = new float[n]; fphase = new float[n]; fappear = new float[n];
        fvariant = new int[n];
        fscatter = new Vector3[n];
        int variants = Mathf.Max(1, colors != null ? colors.Length : 1);
        for (int i = 0; i < n; i++)
        {
            float u, v;
            do
            {
                u = Next() * 2f - 1f;
                v = Next() * 2f - 1f;
            } while (u * u * u * u + v * v * v * v > 1f);   // a rounded square: fills corridor corners
            fu[i] = u;
            fv[i] = v;
            fw[i] = Mathf.Pow(Next(), 1.6f);                   // most of them near the front
            fsize[i] = Mathf.Lerp(fishSize.x, fishSize.y, Next());
            fphase[i] = Next() * Mathf.PI * 2f;
            fvariant[i] = Mathf.Min(variants - 1, (int)(Next() * variants));
            fscatter[i] = new Vector3(Next() * 2f - 1f, Next() * 2f - 1f, Next() * 2f - 1f);
            order.Add((fw[i], i));
        }
        // They pour out front row first.
        order.Sort((a, b) => a.w.CompareTo(b.w));
        for (int rank = 0; rank < n; rank++)
            fappear[order[rank].i] = pourSeconds * Mathf.Pow(rank / (float)n, 0.8f);

        bodyMatrices = new Matrix4x4[variants][];
        bodyCounts = new int[variants];
        for (int v = 0; v < variants; v++)
            bodyMatrices[v] = new Matrix4x4[n];
        allMatrices = new Matrix4x4[n];
    }

    private void LateUpdate()
    {
        if (state == State.Asleep || fu == null)
            return;
        EnsureMeshes();
        EnsureMaterials();

        float now = Time.time;
        float since = now - releasedAt;
        float leave = state == State.Leaving ? Mathf.Clamp01((now - leavingAt) / Mathf.Max(0.05f, leaveSeconds)) : 0f;
        float near = Mathf.Clamp01(1f - (DistanceToPlayer - 1.5f) / 5f);
        float caught = Mathf.Clamp01(1f - (now - caughtAt) / 0.6f);
        Vector3 centre = head + right * ox + up * oy;
        Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
        for (int v = 0; v < bodyCounts.Length; v++)
            bodyCounts[v] = 0;
        int all = 0;

        for (int i = 0; i < fu.Length; i++)
        {
            float age = since - fappear[i];
            if (age <= 0f)
                continue;
            float grow = Ease.OutCubic(Mathf.Clamp01(age / 0.6f));
            float p = fphase[i];
            Vector3 slot = centre
                + right * (fu[i] * rx + Mathf.Sin(now * 1.3f + p) * 0.12f)
                + up * (fv[i] * ry + Mathf.Sin(now * 1.7f + p * 1.3f) * 0.1f)
                - forward * (fw[i] * depth + Mathf.Sin(now * 2.1f + p) * 0.15f);
            Vector3 position = grow < 1f ? Vector3.LerpUnclamped(home, slot, grow) : slot;
            if (leave > 0f)
                position += fscatter[i] * (leave * leave * 5f) - forward * (leave * 3f);
            float front = 1f - fw[i];
            float scale = fsize[i] * grow * (1f + 0.06f * Mathf.Sin(now * 5f + p)) * (1f + 0.22f * front * near + 0.3f * front * caught) * (1f - leave);
            if (scale <= 0.001f)
                continue;
            Quaternion wobble = Quaternion.Euler(Mathf.Sin(now * 2f + p) * 8f, Mathf.Sin(now * 3f + p * 1.7f) * 16f, Mathf.Sin(now * 1.5f + p) * 10f);
            Matrix4x4 matrix = Matrix4x4.TRS(position, facing * wobble, new Vector3(scale, scale, scale));
            int variant = fvariant[i];
            bodyMatrices[variant][bodyCounts[variant]++] = matrix;
            allMatrices[all++] = matrix;
        }
        if (all == 0)
            return;

        var bounds = new Bounds(centre, Vector3.one * (maxRadius * 2f + depth * 2f + 8f));
        bounds.Encapsulate(home);
        for (int v = 0; v < bodyCounts.Length; v++)
            if (bodyCounts[v] > 0)
                Graphics.RenderMeshInstanced(Params(bodyMaterials[v], bounds), bodyMesh, 0, bodyMatrices[v], bodyCounts[v]);
        Graphics.RenderMeshInstanced(Params(whiteMaterial, bounds), whiteMesh, 0, allMatrices, all);
        Graphics.RenderMeshInstanced(Params(pupilMaterial, bounds), pupilMesh, 0, allMatrices, all);
    }

    private RenderParams Params(Material material, Bounds bounds) => new RenderParams(material)
    {
        worldBounds = bounds,
        shadowCastingMode = ShadowCastingMode.Off,
        receiveShadows = false,
        layer = gameObject.layer,
    };

    private void EnsureMaterials()
    {
        int variants = bodyCounts.Length;
        if (bodyMaterials != null && bodyMaterials.Length == variants && whiteMaterial != null)
            return;
        bodyMaterials = new Material[variants];
        for (int v = 0; v < variants; v++)
        {
            Color c = colors != null && colors.Length > v ? colors[v] : new Color(0.95f, 0.76f, 0.29f);
            bodyMaterials[v] = MakeMaterial("Swarm Pufferfish " + (v + 1), c, 0.35f, glow);
        }
        whiteMaterial = MakeMaterial("Swarm Eyes", new Color(0.95f, 0.97f, 0.94f), 0.6f, glow * 0.5f);
        pupilMaterial = MakeMaterial("Swarm Pupils", new Color(0.04f, 0.05f, 0.06f), 0.8f, 0f);
    }

    private static Material MakeMaterial(string name, Color color, float smoothness, float emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        var material = new Material(shader) { name = name + " (runtime)", enableInstancing = true, hideFlags = HideFlags.DontSave };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (emission > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        return material;
    }

    private void OnDestroy()
    {
        if (bodyMaterials != null)
            foreach (Material material in bodyMaterials)
                if (material != null)
                    Destroy(material);
        if (whiteMaterial != null)
            Destroy(whiteMaterial);
        if (pupilMaterial != null)
            Destroy(pupilMaterial);
        if (Active == this)
            Active = null;
    }

    // ---- The pufferfish mesh (one fish, a metre across, nose along +Z) ----------------------------------------------

    private static void EnsureMeshes()
    {
        if (bodyMesh != null && whiteMesh != null && pupilMesh != null)
            return;
        var body = new List<Vector3>();
        var radii = new Vector3(0.46f, 0.42f, 0.5f);
        Ellipsoid(body, radii, Vector3.zero, 7, 12);

        // Spikes: small pyramids spread evenly over the body, but not on the face or the tail.
        const int spikes = 34;
        float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (int k = 0; k < spikes; k++)
        {
            float y = 1f - (k + 0.5f) / spikes * 2f, r = Mathf.Sqrt(1f - y * y), a = golden * k;
            var dir = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            if (dir.z > 0.55f && Mathf.Abs(dir.x) < 0.65f && dir.y > -0.45f)
                continue;   // the face
            if (dir.z < -0.85f)
                continue;   // the tail
            var p = Vector3.Scale(dir, radii);
            Vector3 outward = new Vector3(dir.x / radii.x, dir.y / radii.y, dir.z / radii.z).normalized;
            Vector3 side = Vector3.Cross(outward, Mathf.Abs(outward.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 side2 = Vector3.Cross(outward, side);
            const float spread = 0.055f, length = 0.2f;
            Vector3 b0 = p + side * spread - outward * 0.02f;
            Vector3 b1 = p - side * (spread * 0.5f) + side2 * (spread * 0.87f) - outward * 0.02f;
            Vector3 b2 = p - side * (spread * 0.5f) - side2 * (spread * 0.87f) - outward * 0.02f;
            Vector3 tip = p + outward * length;
            SpikeFace(body, b0, b1, tip, p, outward);
            SpikeFace(body, b1, b2, tip, p, outward);
            SpikeFace(body, b2, b0, tip, p, outward);
        }

        // The tail fin and two side fins: thin, so both faces.
        Vector3 root = new Vector3(0f, 0f, -0.46f), finUp = new Vector3(0f, 0.24f, -0.78f), finDown = new Vector3(0f, -0.24f, -0.78f), mid = new Vector3(0f, 0f, -0.68f);
        TwoSided(body, root, finUp, mid);
        TwoSided(body, root, mid, finDown);
        foreach (float s in new[] { -1f, 1f })
            TwoSided(body, new Vector3(s * 0.42f, -0.05f, 0.1f), new Vector3(s * 0.62f, -0.02f, -0.05f), new Vector3(s * 0.42f, -0.12f, -0.08f));

        var whites = new List<Vector3>();
        var pupils = new List<Vector3>();
        foreach (float s in new[] { -1f, 1f })
        {
            Ellipsoid(whites, new Vector3(0.13f, 0.13f, 0.1f), new Vector3(s * 0.24f, 0.14f, 0.38f), 5, 8);
            Ellipsoid(pupils, new Vector3(0.07f, 0.075f, 0.05f), new Vector3(s * 0.265f, 0.15f, 0.46f), 4, 8);
        }
        Ellipsoid(pupils, new Vector3(0.06f, 0.045f, 0.04f), new Vector3(0f, -0.1f, 0.49f), 4, 8);   // the pout

        bodyMesh = ToMesh("Swarm Pufferfish", body);
        whiteMesh = ToMesh("Swarm Pufferfish Eyes", whites);
        pupilMesh = ToMesh("Swarm Pufferfish Pupils", pupils);
    }

    // A low-poly ellipsoid, every triangle facing out from its middle.
    private static void Ellipsoid(List<Vector3> into, Vector3 radii, Vector3 centre, int lat, int lon)
    {
        Vector3 At(int i, int j)
        {
            float theta = Mathf.PI * i / lat, phi = 2f * Mathf.PI * j / lon;
            return centre + new Vector3(radii.x * Mathf.Sin(theta) * Mathf.Cos(phi), radii.y * Mathf.Cos(theta), radii.z * Mathf.Sin(theta) * Mathf.Sin(phi));
        }
        for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                Vector3 a = At(i, j), b = At(i + 1, j), c = At(i + 1, j + 1), d = At(i, j + 1);
                if (i < lat - 1)
                    Outward(into, a, c, b, centre);
                if (i > 0)
                    Outward(into, a, d, c, centre);
            }
    }

    // A spike's side: facing away from the spike's own axis.
    private static void SpikeFace(List<Vector3> into, Vector3 a, Vector3 b, Vector3 c, Vector3 basePoint, Vector3 axis)
    {
        Vector3 centroid = (a + b + c) / 3f;
        Vector3 onAxis = basePoint + axis * Vector3.Dot(centroid - basePoint, axis);
        Outward(into, a, b, c, onAxis);
    }

    // Add a triangle wound so it faces away from `inside` (Unity's front face: cross(b - a, c - a) points at you).
    private static void Outward(List<Vector3> into, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f)
            (b, c) = (c, b);
        into.Add(a);
        into.Add(b);
        into.Add(c);
    }

    private static void TwoSided(List<Vector3> into, Vector3 a, Vector3 b, Vector3 c)
    {
        into.Add(a); into.Add(b); into.Add(c);
        into.Add(a); into.Add(c); into.Add(b);
    }

    // Unwelded, so every face is flat-shaded (the low-poly look).
    private static Mesh ToMesh(string name, List<Vector3> triangles)
    {
        var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
        mesh.SetVertices(triangles);
        var indices = new int[triangles.Count];
        for (int i = 0; i < indices.Length; i++)
            indices[i] = i;
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDrawGizmos()
    {
        // Where it waits and which way it pours out.
        Vector3 at = Application.isPlaying ? home : transform.position;
        Vector3 facing = Application.isPlaying ? startFacing : transform.forward;
        Gizmos.color = new Color(1f, 0.75f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(at, 0.4f);
        Gizmos.DrawLine(at, at + facing * emergeDistance);
    }
}
