using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Checkpoints are save points. Activating one saves the level as it is at that moment: what you carry (every slot,
// the collectibles, which slot is selected), where you respawn, which pickups you have taken (and which have appeared,
// the chest's), every door (open or shut, locked or not), every socket (how many pieces are in), every puzzle solved,
// the bone key tied, the chase over, the rubble down, the nests broken, the food eaten. Dying then does not just
// teleport you back: the level is loaded again and all of that is put back exactly as it was saved, so nothing you
// did after the checkpoint (a door that shut and locked behind you, a key used up, a fight half done) can leave you
// stuck. Without a save the level simply starts again. A short fade from black hides the reload.
// Death Manager calls Reload(); Checkpoint calls Save(). Everything is found by its place in the hierarchy (pickups by
// where they were when the level loaded, before the drawers shuffle the key about), so the scenes need nothing extra.
// The save lives for this play session only; going to the main menu and back starts fresh.
public static class CheckpointSave
{
    private class Snapshot
    {
        public string scene;
        public Vector3 position;
        public Quaternion rotation;
        public string checkpoint;
        public readonly List<KeyValuePair<ItemDefinition, int>> slots = new List<KeyValuePair<ItemDefinition, int>>();
        public readonly Dictionary<ItemDefinition, int> collectibles = new Dictionary<ItemDefinition, int>();
        public int selected;
        public readonly HashSet<string> taken = new HashSet<string>();
        public readonly HashSet<string> shown = new HashSet<string>();
        public readonly Dictionary<string, (bool open, bool locked, bool behind, float direction)> doors = new Dictionary<string, (bool, bool, bool, float)>();
        public readonly Dictionary<string, (bool open, bool locked, float direction)> doubleDoors = new Dictionary<string, (bool, bool, float)>();
        public readonly Dictionary<string, int> sockets = new Dictionary<string, int>();
        public readonly HashSet<string> solved = new HashSet<string>();
        public readonly HashSet<string> tied = new HashSet<string>();
        public readonly HashSet<string> chasesOver = new HashSet<string>();
        public readonly HashSet<string> rubbleDown = new HashSet<string>();
        public readonly HashSet<string> nestsBroken = new HashSet<string>();
        public readonly HashSet<string> eaten = new HashSet<string>();
        public readonly HashSet<string> wallFishGone = new HashSet<string>();
    }

    private static Snapshot saved;
    private static bool restoring;
    private static bool reloading;   // the level is being loaded again after a death
    // Things seen once per session that should not replay after a reload (the chase's reveal cutscene).
    private static readonly HashSet<string> remembered = new HashSet<string>();

    public static bool HasSave => saved != null && saved.scene == SceneManager.GetActiveScene().path;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        saved = null;
        restoring = false;
        remembered.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void Remember(string what) => remembered.Add(what);
    public static bool Remembers(string what) => remembered.Contains(what);

    // ---- Saving ---------------------------------------------------------------------------------------------------

    // A checkpoint was activated: save a moment later, so whatever set it off (a puzzle that also opens a door) has
    // finished doing so.
    public static void Save()
    {
        Runner.Get().StartCoroutine(SaveSoon());
    }

    private static IEnumerator SaveSoon()
    {
        yield return null;
        yield return null;
        saved = Capture();
    }

    private static Snapshot Capture()
    {
        var s = new Snapshot { scene = SceneManager.GetActiveScene().path };

        Checkpoint current = Checkpoint.Current;
        Transform respawn = current != null ? current.RespawnTransform : null;
        var death = Object.FindFirstObjectByType<DeathManager>();
        s.position = respawn != null ? respawn.position : death != null ? death.transform.position : Vector3.zero;
        s.rotation = respawn != null ? respawn.rotation : death != null ? death.transform.rotation : Quaternion.identity;
        s.checkpoint = current != null ? Key(current) : null;

        var inventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (inventory != null)
        {
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                PlayerInventory.Slot slot = inventory.GetSlot(i);
                s.slots.Add(new KeyValuePair<ItemDefinition, int>(slot.IsEmpty ? null : slot.item, slot.IsEmpty ? 0 : slot.count));
            }
            foreach (KeyValuePair<ItemDefinition, int> pair in inventory.Collectibles)
                s.collectibles[pair.Key] = pair.Value;
            s.selected = inventory.SelectedIndex;
        }

        foreach (PickupItem pickup in All<PickupItem>())
        {
            string key = PickupKey(pickup);
            if (pickup.Collected)
                s.taken.Add(key);
            else if (pickup.gameObject.activeSelf)
                s.shown.Add(key);
        }
        foreach (Door door in All<Door>())
            s.doors[Key(door)] = (door.IsOpen, door.IsLocked, door.LockedBehind, door.SwingDirection);
        foreach (DoubleDoor door in All<DoubleDoor>())
            s.doubleDoors[Key(door)] = (door.IsOpen, door.IsLocked, door.Direction);
        foreach (ItemSocket socket in All<ItemSocket>())
            if (socket.Placed > 0)
                s.sockets[Key(socket)] = socket.Placed;
        foreach (PuzzleStation station in All<PuzzleStation>())
            if (station.IsSolved)
                s.solved.Add(Key(station));
        foreach (BoneKeyTying knot in All<BoneKeyTying>())
            if (knot.IsTied)
                s.tied.Add(Key(knot));
        foreach (ChaseSequence chase in All<ChaseSequence>())
            if (chase.IsFinished)
                s.chasesOver.Add(Key(chase));
        foreach (RubbleFall rubble in All<RubbleFall>())
            if (rubble.Dropped)
                s.rubbleDown.Add(Key(rubble));
        foreach (PufferNest nest in All<PufferNest>())
            if (nest.Destroyed)
                s.nestsBroken.Add(Key(nest));
        foreach (EdibleFish food in All<EdibleFish>())
            if (food.GetComponent<FishController>() == null && (food.Consumed || !food.gameObject.activeSelf))
                s.eaten.Add(Key(food));
        // The fish walls (fish that never swim about): the ones cut down stay down.
        foreach (FishController fish in All<FishController>())
            if (fish.GetComponent<FishWander>() == null && (!fish.IsAlive || !fish.gameObject.activeSelf))
                s.wallFishGone.Add(Key(fish));
        return s;
    }

    // ---- Dying ----------------------------------------------------------------------------------------------------

    // The player died and the death screen has run: load the level again and put the save back (or start the level
    // again when there is none). False when the level cannot be reloaded (not in the build list): respawn the old way.
    public static bool Reload()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex < 0)
            return false;
        restoring = HasSave;
        reloading = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene.buildIndex);
        return true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;
        bool again = restoring, afterDeath = reloading;
        restoring = reloading = false;
        if (!again)
        {
            // A fresh start of the level: nothing carried over (after a death with no save yet, it simply starts again;
            // from the menu, what was seen is forgotten too).
            saved = null;
            if (!afterDeath)
                remembered.Clear();
            else
                Runner.Get().Fade();
            return;
        }
        Runner.Get().StartCoroutine(RestoreSoon());
    }

    // After every object in the reloaded level has woken and started (the drawers have hidden the key, the nests have
    // built themselves), put the save back.
    private static IEnumerator RestoreSoon()
    {
        Runner.Get().Fade();
        yield return null;
        if (saved != null)
            Apply(saved);
    }

    private static void Apply(Snapshot s)
    {
        var inventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (inventory != null)
            inventory.RestoreState(s.slots, s.collectibles, s.selected);

        foreach (PickupItem pickup in All<PickupItem>())
        {
            string key = PickupKey(pickup);
            if (s.taken.Contains(key))
                Object.Destroy(pickup.gameObject);   // in your pocket (or used): gone from the world
            else if (s.shown.Contains(key) && !pickup.gameObject.activeSelf && pickup.GetComponentInParent<AnimatedDrawer>(true) == null)
                pickup.gameObject.SetActive(true);   // it had appeared (the chest's) and is still there to take
        }
        foreach (Door door in All<Door>())
            if (s.doors.TryGetValue(Key(door), out var state))
                door.RestoreState(state.open, state.locked, state.behind, state.direction);
        foreach (DoubleDoor door in All<DoubleDoor>())
            if (s.doubleDoors.TryGetValue(Key(door), out var state))
                door.RestoreState(state.open, state.locked, state.direction);
        foreach (ItemSocket socket in All<ItemSocket>())
            if (s.sockets.TryGetValue(Key(socket), out int placed))
                socket.RestorePlaced(placed);
        foreach (PuzzleStation station in All<PuzzleStation>())
            if (s.solved.Contains(Key(station)))
                station.RestoreSolved();
        foreach (BoneKeyTying knot in All<BoneKeyTying>())
            if (s.tied.Contains(Key(knot)))
                knot.RestoreTied();
        // A knot whose fragments are in but not tied yet: it opens again with E (the socket that switches it on did not fire).
        foreach (BoneKeyTying knot in All<BoneKeyTying>())
        {
            if (knot.IsTied)
                continue;
            ItemSocket socket = knot.GetComponentInParent<ItemSocket>(true);
            if (socket != null && socket.IsFilled)
                knot.enabled = true;
        }
        foreach (RubbleFall rubble in All<RubbleFall>())
            if (s.rubbleDown.Contains(Key(rubble)))
                rubble.RestoreDropped();
        foreach (ChaseSequence chase in All<ChaseSequence>())
            if (s.chasesOver.Contains(Key(chase)))
                chase.RestoreFinished();
        foreach (PufferNest nest in All<PufferNest>())
            if (s.nestsBroken.Contains(Key(nest)))
                nest.RestoreBroken();
        foreach (EdibleFish food in All<EdibleFish>())
            if (s.eaten.Contains(Key(food)))
                food.gameObject.SetActive(false);
        foreach (FishController fish in All<FishController>())
            if (s.wallFishGone.Contains(Key(fish)))
                fish.gameObject.SetActive(false);

        // Back at the checkpoint, facing the way you did when you saved.
        var death = Object.FindFirstObjectByType<DeathManager>();
        if (!string.IsNullOrEmpty(s.checkpoint))
            foreach (Checkpoint checkpoint in All<Checkpoint>())
                if (Key(checkpoint) == s.checkpoint)
                {
                    checkpoint.RestoreActive(s.position, s.rotation, death);
                    break;
                }
        if (death != null)
        {
            var body = death.GetComponent<CharacterController>();
            if (body != null)
                body.enabled = false;
            death.transform.SetPositionAndRotation(s.position, s.rotation);
            if (body != null)
                body.enabled = true;
            var swimmer = death.GetComponent<SwimController>();
            if (swimmer != null)
                swimmer.SetLookAngles(s.rotation.eulerAngles.y, 0f);
        }
    }

    // ---- Helpers --------------------------------------------------------------------------------------------------

    // A pickup's key: where it was when the level loaded; one that has never been switched on (the chest's, waiting to
    // appear) has not moved, so where it is now.
    private static string PickupKey(PickupItem pickup) => string.IsNullOrEmpty(pickup.SaveKey) ? PathOf(pickup.transform) : pickup.SaveKey;

    private static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    // Where something sits in the level: the names and places of it and every parent, and which of its kind it is.
    public static string Key(Component component)
    {
        if (component == null)
            return string.Empty;
        return PathOf(component.transform) + ":" + component.GetType().Name;
    }

    public static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent)
            parts.Add(t.name + "#" + t.GetSiblingIndex());
        parts.Reverse();
        return string.Join("/", parts);
    }

    // Runs the coroutines and draws the fade from black after a reload; lives across scene loads.
    private class Runner : MonoBehaviour
    {
        private static Runner instance;
        private float fadeAt = -10f;
        private Texture2D black;

        public static Runner Get()
        {
            if (instance == null)
            {
                instance = new GameObject("CheckpointSave").AddComponent<Runner>();
                DontDestroyOnLoad(instance.gameObject);
            }
            return instance;
        }

        public void Fade() => fadeAt = Time.unscaledTime;

        private void OnGUI()
        {
            float t = Time.unscaledTime - fadeAt;
            if (t > 1.2f || Event.current.type != EventType.Repaint)
                return;
            if (black == null)
            {
                black = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                black.SetPixel(0, 0, Color.white);
                black.Apply();
            }
            GUI.depth = -900;
            Color keep = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01((t - 0.35f) / 0.85f));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), black);
            GUI.color = keep;
        }
    }
}
