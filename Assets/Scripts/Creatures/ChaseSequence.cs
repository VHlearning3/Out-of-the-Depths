using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Runs the GDD chase. It starts with the reveal cutscene and releases the chasers: by default one Chase Swarm, a wall
// of hundreds of pufferfish that pours out of the hole and follows the player at a set speed (Use Swarm; made here
// when there is none), else the Chase Pufferfish under this object one by one. They hunt the player until End() (wire
// it to the rubble's On Dropped) sends them away for good.
// Starting: with only a Start Trigger, the player being in it starts it; with only a Start Door, the door opening
// does; with both, the door opening arms it and the player being in the trigger (then or later) starts it, so the
// reveal always plays where the vent can be seen. "In the trigger" means the middle of the player, not the first
// brush of their body against its edge, so standing in a doorway at its edge does not count. Begin() starts it at once from anywhere (the admin panel).
// The reveal cutscene takes the camera: black bars slide in, the ocean hushes, the lights stutter and something groans
// in the vent while the view turns to face it (from wherever the player was looking); the grate bursts, time slows
// and the camera follows the first one out; then time comes back, the bars go, control returns and SWIM! flashes up.
// Dying resets it: the pack goes back to its hole and comes again shortly after the respawn if the player is still
// near (without the cutscene, which only plays the first time). Danger01 drives the red pulse (Chase Danger UI).
public class ChaseSequence : MonoBehaviour
{
    [Header("Start and end (drag-in shortcuts; the Begin / End events work too)")]
    [Tooltip("The player being in it starts the chase (once the Start Door is open, if there is one): their middle, not the first brush of their body. Put it where the vent can be seen, a little way in from any doorway.")]
    [SerializeField] private PlayerAreaTrigger startTrigger;
    [Tooltip("The door opening starts the chase, or with a Start Trigger arms it (the GDD's final door).")]
    [SerializeField] private Door startDoor;
    [Tooltip("End() once this rubble has come down.")]
    [SerializeField] private RubbleFall endRubble;

    [Header("Pack")]
    [Tooltip("The chasers are one Chase Swarm: a wall of hundreds of pufferfish that pours out of the hole and comes after the player at one set speed (made here as the scene starts when there is none under this object). Off = the separate Chase Pufferfish below, each hunting on its own.")]
    [SerializeField] private bool useSwarm = true;
    [Tooltip("Empty = the Chase Swarm under this object, or a new one here pouring out the way the pufferfish face.")]
    [SerializeField] private ChaseSwarm swarm;
    [Tooltip("During the chase the tablet takes the fragments from anywhere in the inventory: swim up to it and press E, no picking them in the hotbar first.")]
    [SerializeField] private bool easyTablet = true;
    [Tooltip("Empty = every Chase Pufferfish under this object. Used only with Use Swarm off.")]
    [SerializeField] private ChasePufferfish[] pursuers;
    [Tooltip("Pause after the first one comes out before the rest follow (real seconds).")]
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private float releaseInterval = 0.7f;

    [Header("Coming out")]
    [Tooltip("Blown off its hole and dropped to the floor when the chase starts. Any object; it flies along its own forward.")]
    [SerializeField] private Transform grate;
    [SerializeField] private float grateFlyDistance = 2.5f;
    [SerializeField] private float grateFallHeight = 3f;
    [SerializeField] private float grateFlyTime = 0.8f;
    [SerializeField] private AudioClip grateSound;
    [Tooltip("What the camera turns to during the hush. Empty = the grate, else the first pursuer, else this object.")]
    [SerializeField] private Transform lookTarget;

    [Header("Reveal cutscene")]
    [Tooltip("Play the reveal. Off = the grate bursts and the pack comes out with no cutscene.")]
    [SerializeField] private bool playCutscene = true;
    [Tooltip("Play it again when the chase restarts after dying. Off = only the first time; after that the pack just bursts out.")]
    [SerializeField] private bool cutsceneOnRestart = false;
    [Tooltip("The hush before it happens, in real seconds: the ocean falls silent, the lights stutter and something groans in the vent while the view turns to face it.")]
    [SerializeField, Min(0f)] private float hushSeconds = 1.8f;
    [Tooltip("How long the turn to face the vent takes, in real seconds (within the hush; it eases in and out).")]
    [SerializeField, Min(0.1f)] private float turnSeconds = 1.1f;
    [Tooltip("What groans in the vent during the hush (played from the grate).")]
    [SerializeField] private AudioClip hushSound;
    [SerializeField] private bool flickerLights = true;
    [Tooltip("After the burst: time slows and the camera zooms in and follows the first one out, for this many real seconds.")]
    [SerializeField, Min(0f)] private float cutsceneSeconds = 1.4f;
    [Tooltip("How slowly time runs during the burst (1 = normal speed).")]
    [SerializeField, Range(0.05f, 1f)] private float cutsceneTimeScale = 0.3f;
    [Tooltip("Camera field of view at full zoom on the burst. The normal view is whatever the camera has (about 60).")]
    [SerializeField] private float cutsceneZoomFov = 34f;
    [Tooltip("Real seconds the zoom in takes...")]
    [SerializeField] private float zoomInSeconds = 0.35f;
    [Tooltip("...and the zoom back out (time speeds back up over the same span).")]
    [SerializeField] private float zoomOutSeconds = 0.5f;
    [Tooltip("How hard the camera jolts when the grate bursts (0..1).")]
    [SerializeField, Range(0f, 1f)] private float burstShake = 0.7f;
    [Tooltip("Black bars top and bottom while the cutscene has the camera.")]
    [SerializeField] private bool letterbox = true;
    [Tooltip("Height of each bar, as a share of the screen.")]
    [SerializeField, Range(0f, 0.25f)] private float letterboxSize = 0.1f;
    [Tooltip("Flashed up in the middle of the screen as control comes back. Empty = nothing.")]
    [SerializeField] private string runText = "SWIM!";
    [SerializeField] private float runTextSeconds = 1.3f;
    [SerializeField] private Color runTextColor = new Color(1f, 0.55f, 0.45f);
    [Tooltip("Hide the 'Press E to ...' prompt: during the reveal cutscene only, for the whole chase (E still works), or never.")]
    [SerializeField] private PromptHiding hideInteractPrompt = PromptHiding.DuringCutscene;

    public enum PromptHiding { Never, DuringCutscene, WholeChase }

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

    [Header("The ending")]
    [Tooltip("Opening the End Door (the way out at the far end of the trident corridor) ends the game: after Credits Delay the screen fades to black and the credits roll (End Credits), then back to the main menu.")]
    [UnityEngine.Serialization.FormerlySerializedAs("creditsOnTrident")]
    [SerializeField] private bool creditsOnEndDoor = true;
    [Tooltip("The last door. Empty = the door named Door_Exit.")]
    [SerializeField] private Door endDoor;
    [Tooltip("Seconds between the door swinging open and the fade.")]
    [SerializeField] private float creditsDelay = 0.8f;

    [Header("Sealed in")]
    [Tooltip("When the chase starts the Start Door slams shut behind the player and stays locked until Unseal When is filled, so there is no way back out before the fragments are in. Dying opens it again.")]
    [SerializeField] private bool sealStartDoor = true;
    [Tooltip("The socket that opens it again (the stone tablet the fragments go into). Empty = the Item Socket on the object named RuneTablet; with none at all it opens when the chase ends.")]
    [SerializeField] private ItemSocket unsealWhen;
    [Tooltip("What trying the sealed door says.")]
    [SerializeField] private string sealedHint = "It slammed shut behind you. Put the stone fragments in the tablet by the far door to open it.";

    [Header("The end (the trident and the rubble)")]
    [Tooltip("Once the player is this close to the pickup that drops End Rubble (the trident), the pack slows down...")]
    [SerializeField] private float endSlowRadius = 12f;
    [Tooltip("...to this share of its speed by the time the player is at it.")]
    [SerializeField, Range(0f, 1f)] private float endSlowSpeed = 0.3f;
    [Tooltip("And once the player is past the rubble line, the pack will not cross it: it stays this far short of the rubble's middle (metres), so it is still behind the rubble when it falls.")]
    [SerializeField] private float holdBehindRubble = 2f;

    [Header("Events")]
    public UnityEvent onStarted = new UnityEvent();
    public UnityEvent onEnded = new UnityEvent();
    public UnityEvent onReset = new UnityEvent();

    // The chase running right now, for the HUD.
    public static ChaseSequence Active { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    // The start door is open and the chase starts as soon as the player is in the start trigger.
    public bool IsArmed { get; private set; }
    public bool InCutscene => cutsceneActive;
    public RubbleFall EndRubble => endRubble;
    public float Danger01 { get; private set; }
    // Metres from the player to the nearest hunter (the swarm's front), infinity when none is hunting.
    public float NearestDistance { get; private set; } = float.PositiveInfinity;
    // The hunting pursuer closest to the player right now (the HUD marker points at it), or null.
    public Transform NearestPursuer { get; private set; }

    private DamageManager player;
    private DeathManager death;
    private PlayerTrail trail;
    private Coroutine releasing;
    private bool restartPending;   // died mid-chase: the trigger waits while the restart comes
    private Coroutine restartRoutine;
    private bool cutscenePlayed;
    private Vector3 grateStartPosition;
    private Quaternion grateStartRotation;
    private Coroutine revealRoutine;
    private bool cutsceneActive;
    private bool playerLocked;
    private SwimController cutsceneSwimmer;
    private Camera cutsceneCamera;
    private float baseTimeScale = 1f;
    private float baseFixedDelta = 0.02f;
    private float baseFov = 60f;
    private bool wasFrozen;
    private bool wasLookLocked;
    private float bars;            // 0..1, the letterbox sliding in and out
    private float runTextAt = -10f;
    private GUIStyle runTextStyle;
    private AudioSource tension;
    private float nextGrowl;
    private SwimController fearSwimmer;
    private Camera fearCamera;
    private float restingFov = -1f;
    private OceanAmbience cutsceneAmbience;
    private PlayerInteractor promptOwner;   // whose prompt we hid, so we can show it again

    private void Awake()
    {
        if (GetComponent<ChaseGuide>() == null)
            gameObject.AddComponent<ChaseGuide>();   // the on-screen steps and marker: what to do next
        if (GetComponent<ChaseProximityBar>() == null)
            gameObject.AddComponent<ChaseProximityBar>();   // the bar at the top: how close they are
        if (pursuers == null || pursuers.Length == 0)
            pursuers = GetComponentsInChildren<ChasePufferfish>(true);
        if (useSwarm)
            MakeSwarm();
        else
            swarm = null;
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
        if (startDoor != null)
            startDoor.onOpened.AddListener(OnDoorOpened);
        if (endRubble != null)
            endRubble.onDropped.AddListener(End);
        if (endDoor == null)
            foreach (Door door in FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (door.name == "Door_Exit")
                {
                    endDoor = door;
                    break;
                }
        if (creditsOnEndDoor && endDoor != null)
            endDoor.onOpened.AddListener(RollCredits);
    }

    private void OnDisable()
    {
        EndCutsceneNow();
        StopTension(true);
        HidePrompt(false);
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
        if (startDoor != null)
            startDoor.onOpened.RemoveListener(OnDoorOpened);
        if (endRubble != null)
            endRubble.onDropped.RemoveListener(End);
        if (endDoor != null)
            endDoor.onOpened.RemoveListener(RollCredits);
        if (Active == this)
            Active = null;
    }

    // The swarm: the one under this object, else a new one here, pouring out the way the pufferfish (or the grate)
    // face, biting with their sound and rushing with the Near Sound.
    private void MakeSwarm()
    {
        if (swarm == null)
            swarm = GetComponentInChildren<ChaseSwarm>(true);
        if (swarm != null)
            return;
        var go = new GameObject("ChaseSwarm");
        go.transform.SetParent(transform, false);
        go.transform.position = transform.position;
        swarm = go.AddComponent<ChaseSwarm>();
        ChasePufferfish first = pursuers != null && pursuers.Length > 0 ? pursuers[0] : null;
        Vector3 facing = first != null ? first.transform.forward : grate != null ? grate.forward : transform.forward;
        swarm.Setup(facing, first != null ? first.BiteSound : null, nearSound);
    }

    // ---- starting -------------------------------------------------------------------------------------------------

    private void OnDoorOpened()
    {
        if (IsRunning || IsFinished)
            return;
        if (startTrigger == null)
        {
            Begin();
            return;
        }
        IsArmed = true;   // Update starts it once the player is in the trigger (maybe already there)
    }

    // Is the player inside the start trigger right now?
    private bool PlayerInTrigger()
    {
        if (startTrigger == null || player == null)
            return false;
        Vector3 p = player.transform.position;
        foreach (Collider area in startTrigger.GetComponents<Collider>())
            if (area.enabled && (area.ClosestPoint(p) - p).sqrMagnitude < 0.0001f)
                return true;
        return false;
    }

    // Where a tester starts from (Admin, Chase, Go to its start): the middle of the start trigger at eye height, and
    // what to look at (the vent). False when there is no start trigger.
    public bool StartSpot(out Vector3 position, out Vector3 lookAt)
    {
        lookAt = grate != null ? grate.position : transform.position;
        Collider area = startTrigger != null ? startTrigger.GetComponent<Collider>() : null;
        if (area == null)
        {
            position = default;
            return false;
        }
        Bounds bounds = area.bounds;
        position = new Vector3(bounds.center.x, bounds.min.y + 1.6f, bounds.center.z);
        return true;
    }

    // The last door is open: the ending (End Credits).
    private void RollCredits()
    {
        if (!EndCredits.Playing)
            StartCoroutine(CreditsLater());
    }

    private IEnumerator CreditsLater()
    {
        yield return new WaitForSecondsRealtime(creditsDelay);
        EndCredits.Play();
    }

    // Start the chase now, from anywhere.
    [ContextMenu("Begin chase")]
    public void Begin()
    {
        if (IsRunning || IsFinished)
            return;
        if (player == null)
            player = FindFirstObjectByType<DamageManager>();
        if (player == null)
        {
            Debug.LogWarning($"{name}: no player (Damage Manager) in the scene, chase not started.", this);
            return;
        }
        IsArmed = false;
        trail = player.GetComponent<PlayerTrail>();
        if (trail == null)
            trail = player.gameObject.AddComponent<PlayerTrail>();
        trail.Clear();

        IsRunning = true;
        Active = this;
        Debug.Log(swarm != null ? $"{name}: chase started, the swarm is coming." : $"{name}: chase started, releasing {pursuers.Length} pursuers.", this);
        fearSwimmer = player.GetComponentInParent<SwimController>();
        fearCamera = fearSwimmer != null ? fearSwimmer.GetComponentInChildren<Camera>() : null;
        if (fearCamera == null)
            fearCamera = Camera.main;
        if (restingFov < 0f)
            restingFov = fearCamera != null ? fearCamera.fieldOfView : -1f;
        nextGrowl = Time.time + 2f;
        StartTension();
        if (hideInteractPrompt == PromptHiding.WholeChase)
            HidePrompt(true);

        bool cutscene = playCutscene && (!cutscenePlayed || cutsceneOnRestart) && fearSwimmer != null;
        if (cutscene)
        {
            cutscenePlayed = true;
            revealRoutine = StartCoroutine(RevealCutscene());   // the hush and the turn, then Burst(), then the slow motion
        }
        else
        {
            Burst(true);
        }
        Seal();
        if (easyTablet)
        {
            ItemSocket tablet = unsealWhen;
            if (tablet == null)
                foreach (ItemSocket socket in FindObjectsByType<ItemSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (socket.name == "RuneTablet")
                    {
                        tablet = socket;
                        break;
                    }
            if (tablet != null)
                tablet.RequireHeld = false;   // no hotbar fiddling with the swarm behind you
        }
        onStarted.Invoke();
    }

    private bool sealedIn;
    private ItemSocket sealSocket;

    // The start door shuts and locks behind the player until the tablet is filled (Sealed In).
    private void Seal()
    {
        if (!sealStartDoor || startDoor == null || sealedIn)
            return;
        if (unsealWhen == null)
            foreach (ItemSocket socket in FindObjectsByType<ItemSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (socket.name == "RuneTablet")
                {
                    unsealWhen = socket;
                    break;
                }
        if (unsealWhen != null && unsealWhen.IsFilled)
            return;   // the fragments are in already (a restart): nothing to keep the player in for
        sealedIn = true;
        startDoor.Close();
        startDoor.Lock(sealedHint);
        sealSocket = unsealWhen;
        if (sealSocket != null)
            sealSocket.onFilled.AddListener(Unseal);
    }

    private void Unseal()
    {
        if (!sealedIn)
            return;
        sealedIn = false;
        if (startDoor != null)
            startDoor.Unlock();
        if (sealSocket != null)
            sealSocket.onFilled.RemoveListener(Unseal);
        sealSocket = null;
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
        if (swarm != null)
            swarm.Dismiss();
        NearestPursuer = null;
        StopTension(false);
        HidePrompt(false);
        Unseal();
        Play(endSound);
        onEnded.Invoke();
    }

    // Back to the start: the pack vanishes into its hole and the chase can start again.
    [ContextMenu("Reset chase")]
    public void ResetChase()
    {
        EndCutsceneNow();
        StopReleasing();
        IsRunning = false;
        IsFinished = false;
        if (sealedIn)
        {
            // Starting over (the player died): the door opens again as it was when the chase began.
            Unseal();
            if (startDoor != null)
                startDoor.Open();
        }
        IsArmed = startDoor != null && startDoor.IsOpen && startTrigger != null;
        Danger01 = 0f;
        NearestPursuer = null;
        StopTension(true);
        HidePrompt(false);
        if (Active == this)
            Active = null;
        foreach (ChasePufferfish fish in pursuers)
            if (fish != null)
                fish.Sleep();
        if (swarm != null)
            swarm.Sleep();
        if (grate != null)
            grate.SetPositionAndRotation(grateStartPosition, grateStartRotation);
        onReset.Invoke();
    }

    // The first one comes out at once, so the cutscene has something to follow; the rest after Start Delay (real
    // seconds, so the slow motion doesn't stretch it).
    private IEnumerator ReleaseAll(bool firstNow)
    {
        if (swarm != null)
        {
            swarm.Release(player, trail);   // the whole wall at once; it pours out by itself
            releasing = null;
            yield break;
        }
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
        if (restartPending && restartRoutine == null)
            restartRoutine = StartCoroutine(RestartLater());
    }

    // Breathing room after the respawn, then the pack comes again if the player is still near.
    private IEnumerator RestartLater()
    {
        yield return new WaitForSeconds(restartDelay);
        restartPending = false;
        restartRoutine = null;
        if (player != null && Vector3.Distance(player.transform.position, transform.position) <= restartDistance)
            Begin();
    }

    // ---- while it runs --------------------------------------------------------------------------------------------

    // Near the end the pack slows as the player closes on the trident, and once the player is over the rubble line it
    // stays behind it (Chase Pufferfish Hold Behind), so taking the trident brings the rubble down between them.
    private void HoldAtTheEnd()
    {
        PickupItem trident = endRubble != null ? endRubble.DropOnPickup : null;
        if (trident == null || endRubble.Dropped || player == null)
        {
            ReleaseHold();
            return;
        }
        Vector3 playerAt = player.transform.position;
        float toTrident = Vector3.Distance(playerAt, trident.transform.position);
        float limit = Mathf.Lerp(endSlowSpeed, 1f, Mathf.InverseLerp(endSlowRadius * 0.3f, endSlowRadius, toTrident));

        Vector3 line = endRubble.Blocker != null ? endRubble.Blocker.transform.position : endRubble.transform.position;   // (its bounds are empty until it drops)
        Vector3 away = trident.transform.position - line;
        away.y = 0f;
        bool past = away.sqrMagnitude > 0.01f && Vector3.Dot(playerAt - line, away) > 0f;
        if (swarm != null)
        {
            swarm.SpeedLimit = limit;
            if (past)
                swarm.HoldBehind(line, away, holdBehindRubble);
            else
                swarm.StopHolding();
        }
        foreach (ChasePufferfish fish in pursuers)
        {
            if (fish == null)
                continue;
            fish.SpeedLimit = limit;
            if (past)
                fish.HoldBehind(line, away, holdBehindRubble);
            else
                fish.StopHolding();
        }
    }

    private void ReleaseHold()
    {
        if (swarm != null)
        {
            swarm.SpeedLimit = 1f;
            swarm.StopHolding();
        }
        if (pursuers == null)
            return;
        foreach (ChasePufferfish fish in pursuers)
            if (fish != null)
            {
                fish.SpeedLimit = 1f;
                fish.StopHolding();
            }
    }

    private void Update()
    {
        // The start trigger (after the start door, if there is one): once the player's middle is inside it.
        if (startTrigger != null && (startDoor == null || IsArmed) && !IsRunning && !IsFinished && !restartPending && PlayerInTrigger())
            Begin();

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

        HoldAtTheEnd();

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
        if (swarm != null && swarm.IsHunting && swarm.DistanceToPlayer < nearest)
        {
            nearest = swarm.DistanceToPlayer;
            nearestFish = swarm.transform;
        }
        NearestPursuer = nearestFish;
        NearestDistance = nearest;
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

        if (fearSwimmer != null && shakeAtMaxDanger > 0f && !cutsceneActive)
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

    // ---- the reveal cutscene --------------------------------------------------------------------------------------

    // Real seconds this frame, but none while the pause menu is up, so the cutscene waits for it.
    private static float CutsceneDelta => PauseMenu.IsOpen ? 0f : Time.unscaledDeltaTime;

    // Runs on real time throughout, and holds still while the game is paused.
    //   1. The hush (Hush Seconds): bars in, the ocean falls silent, the lights stutter, a groan from the vent, and the
    //      view turns to face it over Turn Seconds, eased, from wherever the player was looking, then creeps in.
    //   2. The burst: the grate goes with a jolt, the first one comes out, time slows and the camera zooms in and
    //      follows it (Cutscene Seconds).
    //   3. Control comes back at once; time speeds back up and the view widens over Zoom Out Seconds, the bars slide
    //      away, and SWIM! flashes up.
    private IEnumerator RevealCutscene()
    {
        cutsceneSwimmer = fearSwimmer;
        cutsceneCamera = fearCamera;
        cutsceneAmbience = FindFirstObjectByType<OceanAmbience>();
        Transform vent = grate != null ? grate : (lookTarget != null ? lookTarget : CutsceneTarget());
        Transform focus = lookTarget != null ? lookTarget : vent;

        cutsceneActive = true;
        // Started from the pause menu (the admin page): wait until it closes, or the frozen time would be kept as the
        // game's time and put back at the end, stopping everything.
        while (PauseMenu.IsOpen)
            yield return null;
        baseTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        baseFixedDelta = Time.timeScale > 0f ? Time.fixedDeltaTime : 0.02f;
        baseFov = cutsceneCamera != null ? cutsceneCamera.fieldOfView : 60f;
        if (hideInteractPrompt != PromptHiding.Never)
            HidePrompt(true);
        wasFrozen = cutsceneSwimmer.Frozen;
        wasLookLocked = cutsceneSwimmer.LookLocked;
        cutsceneSwimmer.Frozen = true;
        cutsceneSwimmer.LookLocked = true;
        playerLocked = true;

        // 1. The hush and the turn.
        if (cutsceneAmbience != null)
            cutsceneAmbience.Hush = 1f;
        float hush = Mathf.Max(hushSeconds, turnSeconds);
        if (flickerLights)
        {
            var lighting = FindFirstObjectByType<UnderwaterLighting>();
            if (lighting != null)
                lighting.FlickerFor(hush + 0.5f);
        }
        if (hushSound != null && vent != null)
            SlicedOneShot.Play(hushSound, vent.position, volume, Random.Range(0.75f, 0.9f), 0f, 0.2f, 1f, 45f);

        Vector2 from = cutsceneSwimmer.LookAngles;
        for (float t = 0f; t < hush; t += CutsceneDelta)
        {
            bars = Mathf.Clamp01(bars + CutsceneDelta / 0.4f);
            if (focus != null)
            {
                Vector2 to = cutsceneSwimmer.AnglesToward(focus.position);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / turnSeconds));
                cutsceneSwimmer.SetLookAngles(Mathf.LerpAngle(from.x, to.x, k), Mathf.Lerp(from.y, to.y, k));
            }
            if (cutsceneCamera != null)
                cutsceneCamera.fieldOfView = Mathf.Lerp(baseFov, baseFov - 6f, Ease.InOutSine(t / hush));   // a slow creep in
            cutsceneSwimmer.AddShake(0.12f * t / hush);   // a low rumble building
            yield return null;
        }

        // 2. The burst, and the camera follows the first one out in slow motion.
        Burst(true);
        cutsceneSwimmer.AddShake(burstShake);
        SetTimeScale(cutsceneTimeScale);
        Transform first = swarm != null ? swarm.transform : pursuers.Length > 0 && pursuers[0] != null ? pursuers[0].transform : focus;
        float creptFov = cutsceneCamera != null ? cutsceneCamera.fieldOfView : baseFov;
        for (float t = 0f; t < cutsceneSeconds; t += CutsceneDelta)
        {
            bars = Mathf.Clamp01(bars + CutsceneDelta / 0.4f);
            if (first != null)
            {
                Vector2 now = cutsceneSwimmer.LookAngles;
                Vector2 to = cutsceneSwimmer.AnglesToward(first.position);
                float blend = 1f - Mathf.Exp(-7f * CutsceneDelta);
                cutsceneSwimmer.SetLookAngles(Mathf.LerpAngle(now.x, to.x, blend), Mathf.Lerp(now.y, to.y, blend));
            }
            if (cutsceneCamera != null)
                cutsceneCamera.fieldOfView = Mathf.Lerp(creptFov, cutsceneZoomFov, Ease.OutCubic(zoomInSeconds > 0f ? t / zoomInSeconds : 1f));
            yield return null;
        }

        // 3. Control back; time, the view and the bars ease back.
        RestorePlayer();
        runTextAt = Time.unscaledTime;
        float zoomedFov = cutsceneCamera != null ? cutsceneCamera.fieldOfView : baseFov;
        float span = Mathf.Max(0.05f, zoomOutSeconds);
        for (float t = 0f; t < span; t += CutsceneDelta)
        {
            float k = Ease.InOutSine(t / span);
            if (!PauseMenu.IsOpen)
                SetTimeScale(Mathf.Lerp(cutsceneTimeScale, 1f, k));
            if (cutsceneCamera != null)
                cutsceneCamera.fieldOfView = Mathf.Lerp(zoomedFov, baseFov, k);
            bars = 1f - k;
            yield return null;
        }
        RestoreTime();
        if (cutsceneCamera != null)
            cutsceneCamera.fieldOfView = baseFov;
        bars = 0f;
        cutsceneActive = false;
        revealRoutine = null;
    }

    private Transform CutsceneTarget()
    {
        if (lookTarget != null)
            return lookTarget;
        if (grate != null)
            return grate;
        if (pursuers != null && pursuers.Length > 0 && pursuers[0] != null)
            return pursuers[0].transform;
        return transform;
    }

    // A share of the time scale the game had before the cutscene (1 = back to it).
    private void SetTimeScale(float share)
    {
        Time.timeScale = baseTimeScale * share;
        Time.fixedDeltaTime = baseFixedDelta * share;
    }

    private void RestorePlayer()
    {
        if (!playerLocked)
            return;
        playerLocked = false;
        if (cutsceneSwimmer != null)
        {
            cutsceneSwimmer.Frozen = wasFrozen;
            cutsceneSwimmer.LookLocked = wasLookLocked;
        }
        if (cutsceneAmbience != null)
            cutsceneAmbience.Hush = 0f;
        if (hideInteractPrompt == PromptHiding.DuringCutscene)
            HidePrompt(false);
    }

    private void RestoreTime()
    {
        Time.timeScale = baseTimeScale;
        Time.fixedDeltaTime = baseFixedDelta;
    }

    // The 'Press E to ...' prompt, hidden for the cutscene or the whole chase. Interaction itself keeps working.
    private void HidePrompt(bool hidden)
    {
        if (hidden)
        {
            if (promptOwner == null && player != null)
                promptOwner = player.GetComponentInParent<PlayerInteractor>();
            if (promptOwner != null)
                promptOwner.PromptHidden = true;
            return;
        }
        if (promptOwner != null)
            promptOwner.PromptHidden = false;
        promptOwner = null;
    }

    // Cut the cutscene short (reset, disabled, the chase ended): everything back to normal at once.
    private void EndCutsceneNow()
    {
        if (!cutsceneActive)
            return;
        if (revealRoutine != null)
            StopCoroutine(revealRoutine);
        revealRoutine = null;
        RestorePlayer();
        RestoreTime();
        if (cutsceneCamera != null)
            cutsceneCamera.fieldOfView = baseFov;
        bars = 0f;
        cutsceneActive = false;
    }

    // The letterbox bars and the SWIM! flash, over the HUD.
    private void OnGUI()
    {
        bool showText = !string.IsNullOrEmpty(runText) && Time.unscaledTime - runTextAt < runTextSeconds;
        if (Event.current.type != EventType.Repaint || (bars <= 0f && !showText) || PauseMenu.IsOpen)
            return;
        GUI.depth = 10;
        Color keep = GUI.color;
        if (letterbox && bars > 0f)
        {
            float height = Screen.height * letterboxSize * Ease.OutCubic(bars);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - height, Screen.width, height), Texture2D.whiteTexture);
        }
        if (showText)
        {
            float t = (Time.unscaledTime - runTextAt) / runTextSeconds;
            if (runTextStyle == null)
            {
                runTextStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, font = GameFont.Font };
                runTextStyle.normal.textColor = Color.white;
            }
            runTextStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.075f * (1f + 0.25f * (1f - Ease.OutCubic(Mathf.Clamp01(t * 4f)))));
            float alpha = Mathf.Clamp01(t * 8f) * (1f - Mathf.Clamp01((t - 0.6f) / 0.4f));
            var rect = new Rect(0f, Screen.height * 0.3f, Screen.width, Screen.height * 0.2f);
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.6f);
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), runText, runTextStyle);
            GUI.color = new Color(runTextColor.r, runTextColor.g, runTextColor.b, alpha);
            GUI.Label(rect, runText, runTextStyle);
        }
        GUI.color = keep;
    }

    private void PlayAt(AudioClip clip, Vector3 position)
    {
        if (clip != null)
            SoundVariety.PlayAt(clip, position, volume);
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
            SoundVariety.PlayAt(clip, player.transform.position, volume);
    }
}
