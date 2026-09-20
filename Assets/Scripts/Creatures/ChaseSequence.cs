using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Runs the GDD chase. Begin() (wire it to a Player Area Trigger or the final door's On Opened) releases the Chase
// Pufferfish under this object one by one; they hunt the player until End() (wire it to the rubble's On Dropped) sends
// them away for good. Dying resets it: the pack goes back to its hole and comes again shortly after the respawn if the
// player is still near; otherwise the next Begin() starts it over. Danger01 drives the red pulse (Chase Danger UI).
public class ChaseSequence : MonoBehaviour
{
    [Header("Start and end (drag-in shortcuts; the Begin / End events work too)")]
    [Tooltip("Begin() when the player enters this trigger.")]
    [SerializeField] private PlayerAreaTrigger startTrigger;
    [Tooltip("Begin() when this door opens (the GDD's final door).")]
    [SerializeField] private Door startDoor;
    [Tooltip("End() once this rubble has come down.")]
    [SerializeField] private RubbleFall endRubble;

    [Header("Start condition")]
    [Tooltip("When the trigger / door fires, wait until the player is actually looking down the corridor at the target with nothing in the way before starting, so the reveal is never wasted on a wall. Off = start the moment the trigger fires.")]
    [SerializeField] private bool startWhenSeen = true;
    [Tooltip("The vent (the grate, else the target) counts as seen when it is within this many degrees of the middle of the view...")]
    [SerializeField, Range(5f, 90f)] private float seenAngle = 25f;
    [Tooltip("...and (with this on) straight lines from the camera to it and to points around it hit nothing but the player, the grate and the pack - so a wall edge half-covering the corridor still counts as blocked.")]
    [SerializeField] private bool seenNeedsClearLine = true;
    [Tooltip("How far to the sides (metres) of the vent the extra sight lines go. Keep it inside the opening.")]
    [SerializeField] private float seenSpread = 1f;
    [Tooltip("How far above and below the vent the extra sight lines go.")]
    [SerializeField] private float seenSpreadVertical = 0.6f;
    [Tooltip("Safety net: start anyway once the player is this close to the target, seen or not. 0 = never.")]
    [SerializeField] private float forceStartDistance = 4f;
    [Tooltip("Safety net: start anyway this many seconds after the trigger fired. 0 = never.")]
    [SerializeField] private float forceStartAfter = 0f;

    [Header("Pack")]
    [Tooltip("Empty = every Chase Pufferfish under this object.")]
    [SerializeField] private ChasePufferfish[] pursuers;
    [Tooltip("Pause after Begin() before the first one comes out.")]
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private float releaseInterval = 0.7f;

    [Header("Coming out")]
    [Tooltip("Blown off its hole and dropped to the floor when the chase starts. Any object; it flies along its own forward.")]
    [SerializeField] private Transform grate;
    [SerializeField] private float grateFlyDistance = 2.5f;
    [SerializeField] private float grateFallHeight = 3f;
    [SerializeField] private float grateFlyTime = 0.8f;
    [SerializeField] private AudioClip grateSound;
    [Tooltip("What the reveal zooms in on and the camera is drawn toward. Empty = the first pursuer, else the grate, else this object.")]
    [SerializeField] private Transform lookTarget;
    [Tooltip("After the cutscene (or from the start, with no cutscene) the camera keeps being drawn toward the target for this long; the mouse can still fight it.")]
    [SerializeField] private float lookPullSeconds = 1.6f;

    [Header("Reveal cutscene")]
    [Tooltip("The hush before it happens: the ocean falls silent, the lights stutter, something groans in the vent and the view is drawn slowly toward it - for this many real seconds. Then the grate bursts. 0 = no hush.")]
    [SerializeField] private float hushSeconds = 1.6f;
    [Tooltip("What groans in the vent during the hush (played from the grate).")]
    [SerializeField] private AudioClip hushSound;
    [SerializeField] private bool flickerLights = true;
    [Tooltip("How hard the view is drawn toward the vent during the hush. Low = a slow, dreadful turn.")]
    [SerializeField] private float hushLookRate = 1.5f;
    [Tooltip("After the burst the camera zooms in on the target while time slows, for this many real seconds, then eases back. 0 = no slow-motion zoom.")]
    [SerializeField] private float cutsceneSeconds = 1.5f;
    [Tooltip("How slowly time runs during the cutscene (1 = normal speed).")]
    [SerializeField, Range(0.05f, 1f)] private float cutsceneTimeScale = 0.25f;
    [Tooltip("Camera field of view at full zoom. The normal view is whatever the camera has (about 60).")]
    [SerializeField] private float cutsceneZoomFov = 30f;
    [Tooltip("Real seconds the zoom in takes at the start of the cutscene...")]
    [SerializeField] private float zoomInSeconds = 0.5f;
    [Tooltip("...and the zoom back out at the end.")]
    [SerializeField] private float zoomOutSeconds = 0.4f;
    [Tooltip("The player can't swim or look around during the cutscene.")]
    [SerializeField] private bool lockPlayer = true;

    [Header("After dying")]
    [Tooltip("Come again after a respawn without needing the trigger, if the player respawned within Restart Distance of this object.")]
    [SerializeField] private bool restartAfterRespawn = true;
    [SerializeField] private float restartDistance = 12f;
    [Tooltip("Breathing room after the respawn before the pack comes again.")]
    [SerializeField] private float restartDelay = 2.5f;

    [Header("Feedback")]
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip endSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;
    [Tooltip("Danger (0..1, the red pulse) is full when the nearest pursuer is this close...")]
    [SerializeField] private float dangerNear = 2.5f;
    [Tooltip("...and zero when the nearest one is this far.")]
    [SerializeField] private float dangerFar = 16f;

    [Header("Fear")]
    [Tooltip("Looping drone that runs for the whole chase, louder as the pack closes in. Fades out when it ends.")]
    [SerializeField] private AudioClip tensionLoop;
    [SerializeField, Range(0f, 1f)] private float tensionVolume = 0.5f;
    [Tooltip("Heard from the nearest hunter's own position whenever it is within Near Distance, every Near Cooldown seconds or so. Default: a slice of a hydrophone recording of something big moving past. A long clip plays a random Near Slice Seconds window.")]
    [SerializeField] private AudioClip nearSound;
    [SerializeField] private float nearDistance = 6f;
    [SerializeField] private Vector2 nearCooldown = new Vector2(3f, 6f);
    [SerializeField] private float nearSliceSeconds = 2.5f;
    [Tooltip("Random pitch range for the near sound. Low = big and slow.")]
    [SerializeField] private Vector2 nearPitch = new Vector2(0.7f, 0.9f);
    [Tooltip("Camera tremble at full danger (0 = none). A bite still jolts on top of it.")]
    [SerializeField, Range(0f, 1f)] private float shakeAtMaxDanger = 0.35f;
    [Tooltip("Extra degrees of field of view at full danger: the view widens as panic sets in. 0 = none.")]
    [SerializeField] private float fovBoost = 8f;

    [Header("Events")]
    public UnityEvent onStarted = new UnityEvent();
    public UnityEvent onEnded = new UnityEvent();
    public UnityEvent onReset = new UnityEvent();

    // The chase running right now, for the HUD.
    public static ChaseSequence Active { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    // Triggered, waiting for the player to look down the corridor.
    public bool IsArmed { get; private set; }
    public float Danger01 { get; private set; }
    // The hunting pursuer closest to the player right now (the HUD marker points at it), or null.
    public Transform NearestPursuer { get; private set; }

    private DamageManager player;
    private DeathManager death;
    private PlayerTrail trail;
    private Coroutine releasing;
    private bool restartPending;
    private Vector3 grateStartPosition;
    private Quaternion grateStartRotation;
    private Coroutine revealRoutine;
    private bool cutsceneActive;
    private SwimController cutsceneSwimmer;
    private Camera cutsceneCamera;
    private float baseTimeScale = 1f;
    private float baseFixedDelta = 0.02f;
    private float baseFov = 60f;
    private bool wasFrozen;
    private bool wasLookLocked;
    private float armedAt;
    private AudioSource tension;
    private float nextGrowl;
    private SwimController fearSwimmer;
    private Camera fearCamera;
    private float restingFov = -1f;
    private OceanAmbience cutsceneAmbience;

    private void Awake()
    {
        if (pursuers == null || pursuers.Length == 0)
            pursuers = GetComponentsInChildren<ChasePufferfish>(true);
        if (grate != null)
        {
            grateStartPosition = grate.position;
            grateStartRotation = grate.rotation;
        }
    }

    private void Start()
    {
        player = FindFirstObjectByType<DamageManager>();
        death = player != null ? player.GetComponentInParent<DeathManager>() : null;
        if (death != null)
        {
            death.onDied.AddListener(OnPlayerDied);
            death.onRespawned.AddListener(OnPlayerRespawned);
        }
        if (startTrigger != null)
            startTrigger.onPlayerEnter.AddListener(Begin);
        if (startDoor != null)
            startDoor.onOpened.AddListener(Begin);
        if (endRubble != null)
            endRubble.onDropped.AddListener(End);
    }

    private void OnDisable()
    {
        EndCutsceneNow();
        StopTension(true);
        if (fearCamera != null && restingFov > 0f)
            fearCamera.fieldOfView = restingFov;
    }

    private void OnDestroy()
    {
        EndCutsceneNow();
        if (death != null)
        {
            death.onDied.RemoveListener(OnPlayerDied);
            death.onRespawned.RemoveListener(OnPlayerRespawned);
        }
        if (startTrigger != null)
            startTrigger.onPlayerEnter.RemoveListener(Begin);
        if (startDoor != null)
            startDoor.onOpened.RemoveListener(Begin);
        if (endRubble != null)
            endRubble.onDropped.RemoveListener(End);
        if (Active == this)
            Active = null;
    }

    // Start the chase - or, with Start When Seen, arm it and start once the player is looking down the corridor.
    [ContextMenu("Begin chase")]
    public void Begin()
    {
        if (IsRunning || IsFinished || IsArmed)
            return;
        if (player == null)
            player = FindFirstObjectByType<DamageManager>();
        if (player == null)
        {
            Debug.LogWarning($"{name}: no player (Damage Manager) in the scene, chase not started.", this);
            return;
        }

        if (startWhenSeen && !CanSeeTarget())
        {
            IsArmed = true;
            armedAt = Time.unscaledTime;
            Debug.Log($"{name}: chase armed, waiting for the player to look down the corridor.", this);
            return;
        }
        StartNow();
    }

    private void StartNow()
    {
        IsArmed = false;
        trail = player.GetComponent<PlayerTrail>();
        if (trail == null)
            trail = player.gameObject.AddComponent<PlayerTrail>();
        trail.Clear();

        IsRunning = true;
        Active = this;
        Debug.Log($"{name}: chase started, releasing {pursuers.Length} pursuers.", this);
        fearSwimmer = player.GetComponentInParent<SwimController>();
        fearCamera = fearSwimmer != null ? fearSwimmer.GetComponentInChildren<Camera>() : null;
        if (fearCamera == null)
            fearCamera = Camera.main;
        restingFov = fearCamera != null ? fearCamera.fieldOfView : -1f;
        nextGrowl = Time.time + 2f;
        StartTension();
        if (hushSeconds > 0f || cutsceneSeconds > 0f)
        {
            revealRoutine = StartCoroutine(RevealCutscene());   // the hush, then Burst(), then the slow-motion zoom
        }
        else
        {
            Burst(false);
            var swimmer = player.GetComponentInParent<SwimController>();
            Transform look = CutsceneTarget();
            if (swimmer != null && look != null && lookPullSeconds > 0f)
                swimmer.PullLookToward(look.position, lookPullSeconds);
        }
        onStarted.Invoke();
    }

    // The moment it all kicks off: the start sound, the grate flying, the pack released (the first one at once if firstNow).
    private void Burst(bool firstNow)
    {
        Play(startSound);
        StartCoroutine(BlowGrate());
        releasing = StartCoroutine(ReleaseAll(firstNow));
    }

    // Over for good: the pack turns away and fades.
    [ContextMenu("End chase")]
    public void End()
    {
        if (!IsRunning)
            return;
        EndCutsceneNow();
        StopReleasing();
        IsRunning = false;
        IsFinished = true;
        Debug.Log($"{name}: chase over, the pack leaves.", this);
        Danger01 = 0f;
        if (Active == this)
            Active = null;
        foreach (ChasePufferfish fish in pursuers)
            if (fish != null)
                fish.Dismiss();
        NearestPursuer = null;
        StopTension(false);
        Play(endSound);
        onEnded.Invoke();
    }

    // Back to the start: the pack vanishes into its hole and Begin() can run again.
    [ContextMenu("Reset chase")]
    public void ResetChase()
    {
        EndCutsceneNow();
        StopReleasing();
        IsRunning = false;
        IsFinished = false;
        IsArmed = false;
        Danger01 = 0f;
        NearestPursuer = null;
        StopTension(true);
        if (Active == this)
            Active = null;
        foreach (ChasePufferfish fish in pursuers)
            if (fish != null)
                fish.Sleep();
        if (grate != null)
            grate.SetPositionAndRotation(grateStartPosition, grateStartRotation);
        onReset.Invoke();
    }

    // With the reveal cutscene the first one comes out at once, so the zoom has something to look at; the rest follow
    // after Start Delay (real seconds, so the slow motion doesn't stretch it).
    private IEnumerator ReleaseAll(bool firstNow)
    {
        int next = 0;
        if (firstNow && pursuers.Length > 0)
        {
            if (pursuers[0] != null)
                pursuers[0].Release(player, trail);
            next = 1;
        }
        yield return new WaitForSecondsRealtime(startDelay);
        for (; next < pursuers.Length; next++)
        {
            if (pursuers[next] != null)
                pursuers[next].Release(player, trail);
            yield return new WaitForSeconds(releaseInterval);
        }
        releasing = null;
    }

    private void StopReleasing()
    {
        if (releasing != null)
            StopCoroutine(releasing);
        releasing = null;
    }

    private void OnPlayerDied()
    {
        if (!IsRunning)
            return;
        ResetChase();
        restartPending = restartAfterRespawn;
    }

    private void OnPlayerRespawned()
    {
        if (!restartPending)
            return;
        restartPending = false;
        StartCoroutine(RestartLater());
    }

    private IEnumerator RestartLater()
    {
        yield return new WaitForSeconds(restartDelay);
        if (player != null && Vector3.Distance(player.transform.position, transform.position) <= restartDistance)
            Begin();
    }

    private void Update()
    {
        if (IsArmed && !IsRunning && !IsFinished && (CanSeeTarget() || ForceStartDue()))
            StartNow();

        if (!IsRunning)
        {
            // After the chase the view eases back to its normal width.
            if (fearCamera != null && restingFov > 0f && !cutsceneActive)
            {
                fearCamera.fieldOfView = Mathf.Lerp(fearCamera.fieldOfView, restingFov, 1f - Mathf.Exp(-3f * Time.deltaTime));
                if (Mathf.Abs(fearCamera.fieldOfView - restingFov) < 0.05f)
                {
                    fearCamera.fieldOfView = restingFov;
                    restingFov = -1f;
                }
            }
            return;
        }

        float nearest = float.PositiveInfinity;
        Transform nearestFish = null;
        foreach (ChasePufferfish fish in pursuers)
        {
            if (fish == null || !fish.isActiveAndEnabled || !fish.IsHunting)
                continue;
            float distance = fish.DistanceToPlayer;
            if (distance < nearest)
            {
                nearest = distance;
                nearestFish = fish.transform;
            }
        }
        NearestPursuer = nearestFish;
        Danger01 = float.IsPositiveInfinity(nearest) ? 0f : 1f - Mathf.InverseLerp(dangerNear, dangerFar, nearest);
        UpdateFear();
    }

    // The drone swells, the nearest hunter growls when it's close, the camera trembles and the view widens with danger.
    private void UpdateFear()
    {
        if (tension != null && tension.isPlaying)
            tension.volume = Mathf.MoveTowards(tension.volume, (0.35f + 0.65f * Danger01) * tensionVolume, Time.unscaledDeltaTime * 0.5f);

        if (nearSound != null && NearestPursuer != null && Time.time >= nextGrowl &&
            Vector3.Distance(NearestPursuer.position, player.transform.position) <= nearDistance)
        {
            SlicedOneShot.Play(nearSound, NearestPursuer.position, volume, Random.Range(nearPitch.x, nearPitch.y), nearSliceSeconds, 0.3f, 1f, 30f);
            nextGrowl = Time.time + Random.Range(nearCooldown.x, nearCooldown.y);
        }

        if (fearSwimmer != null && shakeAtMaxDanger > 0f)
            fearSwimmer.AddShake(shakeAtMaxDanger * Danger01 * Danger01);

        if (fearCamera != null && restingFov > 0f && !cutsceneActive && fovBoost != 0f)
            fearCamera.fieldOfView = Mathf.Lerp(fearCamera.fieldOfView, restingFov + fovBoost * Danger01, 1f - Mathf.Exp(-2f * Time.deltaTime));
    }

    private void StartTension()
    {
        if (tensionLoop == null)
            return;
        if (tension == null)
        {
            tension = gameObject.AddComponent<AudioSource>();
            tension.loop = true;
            tension.spatialBlend = 0f;
            tension.playOnAwake = false;
        }
        tension.clip = tensionLoop;
        tension.volume = 0f;
        tension.Play();
    }

    private void StopTension(bool instant)
    {
        if (tension == null || !tension.isPlaying)
            return;
        if (instant || !isActiveAndEnabled)
            tension.Stop();
        else
            StartCoroutine(FadeOutTension());
    }

    private IEnumerator FadeOutTension()
    {
        while (tension != null && tension.volume > 0.01f && !IsRunning)
        {
            tension.volume = Mathf.MoveTowards(tension.volume, 0f, Time.unscaledDeltaTime * 0.6f);
            yield return null;
        }
        if (tension != null && !IsRunning)
            tension.Stop();
    }

    // Is the player looking straight at the vent (the grate, else the target) with a clear view of all of it?
    private bool CanSeeTarget()
    {
        if (player == null)
            return false;
        Transform vent = grate != null ? grate : CutsceneTarget();
        if (vent == null)
            return false;
        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return true;   // nothing to judge with; don't hold the chase hostage

        Vector3 eye = cam.transform.position;
        Vector3 toVent = vent.position - eye;
        if (toVent.sqrMagnitude < 0.01f)
            return true;
        if (Vector3.Angle(cam.transform.forward, toVent) > seenAngle)
            return false;
        if (!seenNeedsClearLine)
            return true;

        // Not one sight line but a spread of them, to the middle of the opening and around it, so a wall edge that
        // still half-covers the corridor counts as blocked.
        Vector3 side = Vector3.Cross(Vector3.up, toVent).normalized;
        if (side.sqrMagnitude < 0.5f)
            side = cam.transform.right;
        Vector3 spread = side * seenSpread;
        Vector3 lift = Vector3.up * seenSpreadVertical;
        return LineIsClear(eye, vent.position)
            && LineIsClear(eye, vent.position + spread)
            && LineIsClear(eye, vent.position - spread)
            && LineIsClear(eye, vent.position + lift)
            && LineIsClear(eye, vent.position - lift);
    }

    private bool LineIsClear(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.05f)
            return true;
        foreach (RaycastHit hit in Physics.RaycastAll(from, delta / length, Mathf.Max(0f, length - 0.3f), ~0, QueryTriggerInteraction.Ignore))
        {
            Transform t = hit.collider.transform;
            if (PlayerBody.Is(hit.collider) || t.IsChildOf(transform) || (grate != null && t.IsChildOf(grate)) || (lookTarget != null && t.IsChildOf(lookTarget)))
                continue;
            return false;   // something solid in the way
        }
        return true;
    }

    private bool ForceStartDue()
    {
        if (forceStartAfter > 0f && Time.unscaledTime - armedAt >= forceStartAfter)
            return true;
        Transform target = CutsceneTarget();
        return forceStartDistance > 0f && player != null && target != null &&
               Vector3.Distance(player.transform.position, target.position) <= forceStartDistance;
    }

    // The grate flies out of the hole, tips over and drops to the floor.
    private IEnumerator BlowGrate()
    {
        if (grate == null)
            yield break;
        Vector3 from = grateStartPosition;
        Quaternion fromRotation = grateStartRotation;
        Vector3 forward = fromRotation * Vector3.forward;
        Vector3 axis = Vector3.Cross(forward, Vector3.up);
        if (axis.sqrMagnitude < 0.001f)
            axis = Vector3.right;
        PlayAt(grateSound, from);

        for (float t = 0f; t < grateFlyTime; t += Time.deltaTime)
        {
            float k = t / grateFlyTime;
            Vector3 position = from + forward * (grateFlyDistance * Ease.OutQuad(k)) + Vector3.down * (grateFallHeight * Ease.InQuad(k));
            grate.SetPositionAndRotation(position, fromRotation * Quaternion.AngleAxis(-140f * Ease.OutQuad(k), axis));
            yield return null;
        }
        grate.SetPositionAndRotation(from + forward * grateFlyDistance + Vector3.down * grateFallHeight, fromRotation * Quaternion.AngleAxis(-140f, axis));
        PlayAt(grateSound, grate.position);
    }

    // The reveal: time slows and the camera zooms in on the target (the first pursuer coming out of its hole); then
    // time and the view come back and the player has control again. Runs on real time throughout.
    private IEnumerator RevealCutscene()
    {
        cutsceneSwimmer = player.GetComponentInParent<SwimController>();
        cutsceneCamera = cutsceneSwimmer != null ? cutsceneSwimmer.GetComponentInChildren<Camera>() : null;
        if (cutsceneCamera == null)
            cutsceneCamera = Camera.main;
        Transform target = CutsceneTarget();
        cutsceneAmbience = FindFirstObjectByType<OceanAmbience>();

        cutsceneActive = true;
        baseTimeScale = Time.timeScale;
        baseFixedDelta = Time.fixedDeltaTime;
        baseFov = cutsceneCamera != null ? cutsceneCamera.fieldOfView : 60f;
        if (cutsceneSwimmer != null)
        {
            wasFrozen = cutsceneSwimmer.Frozen;
            wasLookLocked = cutsceneSwimmer.LookLocked;
            if (lockPlayer)
            {
                cutsceneSwimmer.Frozen = true;
                cutsceneSwimmer.LookLocked = true;
            }
        }

        // 1. The hush. Nothing has happened yet - that is the point: the ocean goes quiet, the light stutters, something
        //    groans in the vent, and the view is drawn slowly toward it.
        if (hushSeconds > 0f)
        {
            if (cutsceneAmbience != null)
                cutsceneAmbience.Hush = 1f;
            if (flickerLights)
            {
                var lighting = FindFirstObjectByType<UnderwaterLighting>();
                if (lighting != null)
                    lighting.FlickerFor(hushSeconds + 0.6f);
            }
            Transform vent = grate != null ? grate : target;
            if (hushSound != null && vent != null)
                SlicedOneShot.Play(hushSound, vent.position, volume, Random.Range(0.75f, 0.9f), 0f, 0.2f, 1f, 45f);

            for (float t = 0f; t < hushSeconds; t += Time.unscaledDeltaTime)
            {
                if (target != null && cutsceneSwimmer != null)
                    cutsceneSwimmer.PullLookToward(target.position, 0.3f, hushLookRate);
                yield return null;
            }
        }

        // 2. The burst: the grate goes, the first one comes out, and time slows while the camera zooms in on it.
        Burst(true);
        Time.timeScale = cutsceneTimeScale;
        Time.fixedDeltaTime = baseFixedDelta * cutsceneTimeScale;

        for (float t = 0f; t < cutsceneSeconds; t += Time.unscaledDeltaTime)
        {
            if (target != null && cutsceneSwimmer != null)
                cutsceneSwimmer.PullLookToward(target.position, 0.3f);
            if (cutsceneCamera != null)
                cutsceneCamera.fieldOfView = Mathf.Lerp(baseFov, cutsceneZoomFov, Ease.OutCubic(zoomInSeconds > 0f ? t / zoomInSeconds : 1f));
            yield return null;
        }

        // 3. Time and control come back first; the view eases out over Zoom Out Seconds.
        RestoreTimeAndPlayer();
        if (target != null && cutsceneSwimmer != null && lookPullSeconds > 0f)
            cutsceneSwimmer.PullLookToward(target.position, lookPullSeconds);
        for (float t = 0f; t < zoomOutSeconds; t += Time.unscaledDeltaTime)
        {
            if (cutsceneCamera != null)
                cutsceneCamera.fieldOfView = Mathf.Lerp(cutsceneZoomFov, baseFov, Ease.InOutSine(t / zoomOutSeconds));
            yield return null;
        }
        if (cutsceneCamera != null)
            cutsceneCamera.fieldOfView = baseFov;
        cutsceneActive = false;
        revealRoutine = null;
    }

    private Transform CutsceneTarget()
    {
        if (lookTarget != null)
            return lookTarget;
        if (pursuers != null && pursuers.Length > 0 && pursuers[0] != null)
            return pursuers[0].transform;
        return grate != null ? grate : transform;
    }

    private void RestoreTimeAndPlayer()
    {
        if (!cutsceneActive)
            return;
        Time.timeScale = baseTimeScale;
        Time.fixedDeltaTime = baseFixedDelta;
        if (cutsceneSwimmer != null)
        {
            cutsceneSwimmer.Frozen = wasFrozen;
            cutsceneSwimmer.LookLocked = wasLookLocked;
        }
        if (cutsceneAmbience != null)
            cutsceneAmbience.Hush = 0f;
    }

    // Cut the cutscene short (reset, disabled): everything back to normal at once.
    private void EndCutsceneNow()
    {
        if (!cutsceneActive)
            return;
        if (revealRoutine != null)
            StopCoroutine(revealRoutine);
        revealRoutine = null;
        RestoreTimeAndPlayer();
        if (cutsceneCamera != null)
            cutsceneCamera.fieldOfView = baseFov;
        cutsceneActive = false;
    }

    private void PlayAt(AudioClip clip, Vector3 position)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    // Scene view: the pack's start points and what starts / ends the chase.
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);
        foreach (ChasePufferfish fish in GetComponentsInChildren<ChasePufferfish>(true))
            Gizmos.DrawLine(transform.position, fish.transform.position);

        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.9f);
        if (startTrigger != null)
            Gizmos.DrawLine(transform.position, startTrigger.transform.position);
        if (startDoor != null)
            Gizmos.DrawLine(transform.position, startDoor.transform.position);
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        if (endRubble != null)
            Gizmos.DrawLine(transform.position, endRubble.transform.position);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, restartDistance);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && player != null)
            AudioSource.PlayClipAtPoint(clip, player.transform.position, volume);
    }
}
