using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fish swim in through windows / holes. Spawns Fish Prefab out of sight behind one of its Fish Windows, which rise into
// view and swim through into the room, then wander around this object or join a pack. When a fish is eaten or fades away,
// its replacement comes in through a window again after the prefab's Edible Fish → Respawn Time.
// With Player Enters Trigger the room "loads" when the player swims in and unloads when they leave: live fish that are
// already in the room pause right where they are (switched off) and are back the moment the player returns, so only the
// first arrivals and replacements for eaten fish come in through the windows; corpses keep drifting; anything that
// respawns meanwhile waits too.
// Windows: every Fish Window under this object is used automatically (duplicate the FishWindow prefab as children);
// or drag any transforms into Openings. Plain transforms use the fallback path settings below.
public class FishSpawner : MonoBehaviour
{
    public enum StartMode { SceneStart, PlayerEntersTrigger, Manual }

    [Header("What")]
    [SerializeField] private GameObject fishPrefab;
    [Tooltip("How many fish this spawner keeps alive. With Join School set, this is the size of the pack.")]
    [SerializeField, Min(0)] private int count = 4;
    [Tooltip("Optional: fish join this pack once they are out.")]
    [SerializeField] private FishSchool joinSchool;

    [Header("Where")]
    [Tooltip("Leave empty to use every Fish Window under this object. Otherwise: windows or plain transforms (blue arrow = into the room).")]
    [SerializeField] private Transform[] openings;
    [Tooltip("Pick openings at random; off = take turns in list order.")]
    [SerializeField] private bool randomOpening = true;

    [Header("Fallback path (openings without a Fish Window)")]
    [SerializeField] private float startDepth = 1.8f;
    [SerializeField] private float startDrop = 6f;
    [SerializeField] private float exitDistance = 3f;
    [SerializeField] private Vector2 openingScatter = new Vector2(0.8f, 0.4f);
    [SerializeField] private Vector2 exitScatter = new Vector2(45f, 20f);

    [Header("When")]
    [Tooltip("Scene Start: the fish come in as the level loads. Player Enters Trigger: put a trigger collider on this object " +
             "covering the room; the fish come in when the player swims into it. Manual: call Activate() / Deactivate() from " +
             "events, e.g. a Player Area Trigger somewhere else.")]
    [SerializeField] private StartMode startMode = StartMode.SceneStart;
    [Tooltip("When the player leaves the trigger (or Deactivate is called), live fish pause where they are (switched off) until the player returns. Off = they keep swimming about the room meanwhile.")]
    [SerializeField] private bool despawnWhenPlayerLeaves = true;
    [Tooltip("Average seconds between fish swimming in (each gap varies ±50%), so they don't all appear at once.")]
    [SerializeField] private float stagger = 0.8f;

    public bool Activated { get; private set; }
    public bool PlayerInside { get; private set; }

    private readonly List<Transform> activeOpenings = new List<Transform>();
    private readonly List<GameObject> fish = new List<GameObject>();
    private readonly List<GameObject> parked = new List<GameObject>();
    private readonly List<GameObject> resting = new List<GameObject>();   // in the room, paused while the player is away
    private int nextOpening;
    private Coroutine arrivals;

    private void Start()
    {
        CollectOpenings();
        if (startMode == StartMode.PlayerEntersTrigger && GetComponent<Collider>() == null)
            Debug.LogWarning($"{name}: Start Mode is Player Enters Trigger but there is no collider on the spawner to swim into.", this);
        if (startMode == StartMode.SceneStart)
            Activate();
    }

    // The player is here: bring the fish in (the first time this creates them; later it brings parked ones back).
    public void Activate()
    {
        PlayerInside = true;
        if (!Activated)
        {
            Activated = true;
            for (int i = 0; i < count; i++)
                CreateFish();
        }

        // The ones that were already in the room carry on from where they were, at once.
        foreach (GameObject go in resting)
            if (go != null)
                go.SetActive(true);
        resting.Clear();

        if (arrivals != null)
            StopCoroutine(arrivals);
        arrivals = StartCoroutine(BringIn());
    }

    // The player left: live fish swim back out through the nearest window and park out of sight.
    public void Deactivate()
    {
        PlayerInside = false;
        if (!despawnWhenPlayerLeaves)
            return;

        if (arrivals != null)
        {
            StopCoroutine(arrivals);
            arrivals = null;
        }

        foreach (GameObject go in fish)
        {
            if (go == null || !go.activeSelf || parked.Contains(go))
                continue;

            var controller = go.GetComponent<FishController>();
            var wander = go.GetComponent<FishWander>();
            if (controller == null || !controller.IsAlive || wander == null)
                continue;

            // Not in the room yet: it comes in through a window again next time.
            if (wander.IsEntering)
            {
                Park(go);
                continue;
            }

            // In the room: pause it right here, to carry on the moment the player is back.
            go.SetActive(false);
            if (!resting.Contains(go))
                resting.Add(go);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (startMode == StartMode.PlayerEntersTrigger && PlayerBody.Is(other))
            Activate();
    }

    private void OnTriggerExit(Collider other)
    {
        if (startMode == StartMode.PlayerEntersTrigger && PlayerBody.Is(other))
            Deactivate();
    }

    private void CollectOpenings()
    {
        activeOpenings.Clear();
        if (openings != null)
        {
            foreach (Transform opening in openings)
            {
                if (opening != null)
                    activeOpenings.Add(opening);
            }
        }

        if (activeOpenings.Count == 0)
        {
            foreach (FishWindow window in GetComponentsInChildren<FishWindow>())
                activeOpenings.Add(window.transform);
        }

        if (activeOpenings.Count == 0)
            activeOpenings.Add(transform);
    }

    private void CreateFish()
    {
        if (fishPrefab == null)
        {
            Debug.LogWarning($"{name}: Fish Spawner has no Fish Prefab.", this);
            return;
        }

        GameObject go = Instantiate(fishPrefab, transform.position - Vector3.up * 50f, Quaternion.identity, transform);
        go.SetActive(false);
        fish.Add(go);
        parked.Add(go);

        // FishController.Revive runs first (it subscribed earlier); then the revived fish comes back in, or waits if the player is away.
        var edible = go.GetComponent<EdibleFish>();
        if (edible != null)
            edible.onRespawned.AddListener(() => Park(go));
    }

    // Parked fish come in one by one while the player is inside.
    private IEnumerator BringIn()
    {
        while (PlayerInside && parked.Count > 0)
        {
            GameObject go = parked[0];
            parked.RemoveAt(0);
            if (go == null)
                continue;

            go.SetActive(true);
            Emerge(go, PickOpening());
            if (stagger > 0f)
                yield return new WaitForSeconds(stagger * Random.Range(0.5f, 1.5f));
        }
        arrivals = null;
    }

    // A fish that is out of the room: if the player is inside it comes straight back in, otherwise it waits invisibly.
    private void Park(GameObject go)
    {
        if (go == null)
            return;

        if (PlayerInside)
        {
            Emerge(go, PickOpening());
            return;
        }

        go.SetActive(false);
        if (!parked.Contains(go))
            parked.Add(go);
    }

    private void Emerge(GameObject go, Transform opening)
    {
        var window = opening.GetComponent<FishWindow>();
        Vector3 start;
        Vector3[] path = window != null
            ? window.BuildPath(out start)
            : FishWindow.BuildPath(opening, startDepth, startDrop, exitDistance, openingScatter, exitScatter, out start);

        // Facing along the first leg (up for a window, down for a hole in the ceiling).
        Vector3 heading = path.Length > 0 ? path[0] - start : Vector3.up;
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.up;
        heading.Normalize();
        Vector3 upHint = Mathf.Abs(Vector3.Dot(heading, opening.forward)) > 0.9f ? opening.up : -opening.forward;
        go.transform.SetPositionAndRotation(start, Quaternion.LookRotation(heading, upHint));

        var wander = go.GetComponent<FishWander>();
        if (wander == null)
            return;

        // Join first so the pack's per-fish speed variation already applies on the way in.
        if (joinSchool != null)
            joinSchool.Add(wander);
        wander.SwimOut(path, transform.position);
    }

    private Transform PickOpening()
    {
        if (activeOpenings.Count == 0)
            CollectOpenings();

        int index = randomOpening ? Random.Range(0, activeOpenings.Count) : nextOpening++ % activeOpenings.Count;
        return activeOpenings[index];
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        var area = GetComponent<Collider>();
        if (startMode == StartMode.PlayerEntersTrigger && area != null)
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.15f);
            Gizmos.DrawCube(area.bounds.center, area.bounds.size);
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.7f);
            Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
        }

        if (openings == null)
            return;

        // Fish Windows draw their own path; only plain transforms need the fallback drawn here.
        foreach (Transform opening in openings)
        {
            if (opening != null && opening.GetComponent<FishWindow>() == null)
                FishWindow.DrawGizmos(opening, startDepth, startDrop, exitDistance, openingScatter);
        }
    }
}
