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
    [Tooltip("The camera is drawn toward this for Look Pull Seconds when the chase starts; the mouse can still fight it. Empty = the grate, else this object.")]
    [SerializeField] private Transform lookTarget;
    [SerializeField] private float lookPullSeconds = 1.6f;

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

    [Header("Events")]
    public UnityEvent onStarted = new UnityEvent();
    public UnityEvent onEnded = new UnityEvent();
    public UnityEvent onReset = new UnityEvent();

    // The chase running right now, for the HUD.
    public static ChaseSequence Active { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    public float Danger01 { get; private set; }

    private DamageManager player;
    private DeathManager death;
    private PlayerTrail trail;
    private Coroutine releasing;
    private bool restartPending;
    private Vector3 grateStartPosition;
    private Quaternion grateStartRotation;

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

    private void OnDestroy()
    {
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

        trail = player.GetComponent<PlayerTrail>();
        if (trail == null)
            trail = player.gameObject.AddComponent<PlayerTrail>();
        trail.Clear();

        IsRunning = true;
        Active = this;
        Debug.Log($"{name}: chase started, releasing {pursuers.Length} pursuers.", this);
        Play(startSound);
        StartCoroutine(BlowGrate());
        var swimmer = player.GetComponentInParent<SwimController>();
        Transform look = lookTarget != null ? lookTarget : grate != null ? grate : transform;
        if (swimmer != null && lookPullSeconds > 0f)
            swimmer.PullLookToward(look.position, lookPullSeconds);
        releasing = StartCoroutine(ReleaseAll());
        onStarted.Invoke();
    }

    // Over for good: the pack turns away and fades.
    [ContextMenu("End chase")]
    public void End()
    {
        if (!IsRunning)
            return;
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
        Play(endSound);
        onEnded.Invoke();
    }

    // Back to the start: the pack vanishes into its hole and Begin() can run again.
    [ContextMenu("Reset chase")]
    public void ResetChase()
    {
        StopReleasing();
        IsRunning = false;
        IsFinished = false;
        Danger01 = 0f;
        if (Active == this)
            Active = null;
        foreach (ChasePufferfish fish in pursuers)
            if (fish != null)
                fish.Sleep();
        if (grate != null)
            grate.SetPositionAndRotation(grateStartPosition, grateStartRotation);
        onReset.Invoke();
    }

    private IEnumerator ReleaseAll()
    {
        yield return new WaitForSeconds(startDelay);
        foreach (ChasePufferfish fish in pursuers)
        {
            if (fish != null)
                fish.Release(player, trail);
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
        if (!IsRunning)
            return;

        float nearest = float.PositiveInfinity;
        foreach (ChasePufferfish fish in pursuers)
            if (fish != null && fish.isActiveAndEnabled && fish.IsHunting)
                nearest = Mathf.Min(nearest, fish.DistanceToPlayer);
        Danger01 = float.IsPositiveInfinity(nearest) ? 0f : 1f - Mathf.InverseLerp(dangerNear, dangerFar, nearest);
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
