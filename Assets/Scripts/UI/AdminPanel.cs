using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Debug panel (IMGUI, so it needs no canvas or EventSystem). Press the toggle key (0) to open it. Everything in it works
// on whatever is in the scene: player stats and cheats, give any item, spawn things, teleport to any sign or checkpoint,
// drive the chase, doors and rubble, time scale, fish colours. The arena builder fills Items and Spawnables.
public class AdminPanel : MonoBehaviour
{
    [System.Serializable]
    public class Spawnable
    {
        public string label;
        public GameObject prefab;
    }

    [Header("Panel")]
    [SerializeField] private Key toggleKey = Key.Digit0;
    [Tooltip("Turned off while the panel is open (movement, interaction), so the mouse is free.")]
    [SerializeField] private Behaviour[] pauseWhileOpen;
    [Tooltip("Extra size on top of the automatic scaling (the panel grows with the screen height).")]
    [SerializeField] private float scale = 1f;

    [Header("Spawning")]
    [SerializeField] private Spawnable[] spawnables;
    [SerializeField] private Transform spawnOrigin;
    [SerializeField] private float spawnDistance = 3f;

    [Header("Items")]
    [Tooltip("Items the Give buttons hand out. Rebuild Test Arena fills this with every item in Assets/Items.")]
    [SerializeField] private ItemDefinition[] items;

    [Header("Fish colour")]
    [SerializeField] private Color fishTint = Color.white;

    private bool open;
    private Rect windowRect = new Rect(20f, 20f, 420f, 100f);
    private Vector2 scroll;
    private float timeScale = 1f;

    private HungerSystem hunger;
    private HealthSystem health;
    private DamageManager damageManager;
    private PlayerInventory inventory;
    private SwimController swimmer;
    private DeathManager death;

    // Scene things, refreshed every time the panel opens.
    private ProximityLabel[] signs = new ProximityLabel[0];
    private Checkpoint[] checkpoints = new Checkpoint[0];
    private Door[] doors = new Door[0];
    private RubbleFall[] rubble = new RubbleFall[0];
    private ChaseSequence chase;

    private GUIStyle headerStyle;
    private GUIStyle noteStyle;

    private void Start()
    {
        hunger = FindFirstObjectByType<HungerSystem>();
        health = FindFirstObjectByType<HealthSystem>();
        damageManager = FindFirstObjectByType<DamageManager>();
        inventory = FindFirstObjectByType<PlayerInventory>();
        swimmer = FindFirstObjectByType<SwimController>();
        death = FindFirstObjectByType<DeathManager>();
        if (spawnOrigin == null && Camera.main != null)
            spawnOrigin = Camera.main.transform;
        timeScale = Time.timeScale;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            SetOpen(!open);
    }

    private void SetOpen(bool value)
    {
        open = value;
        foreach (var b in pauseWhileOpen)
        {
            if (b != null)
                b.enabled = !open;
        }

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (open)
            RefreshSceneLists();
    }

    private void RefreshSceneLists()
    {
        signs = FindObjectsByType<ProximityLabel>(FindObjectsSortMode.None);
        System.Array.Sort(signs, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        checkpoints = FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(checkpoints, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        doors = FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        rubble = FindObjectsByType<RubbleFall>(FindObjectsSortMode.None);
        chase = FindFirstObjectByType<ChaseSequence>();
    }

    private void OnGUI()
    {
        if (!open)
            return;

        // Grow with the screen so it stays readable on big monitors.
        float s = Mathf.Max(1f, Screen.height / 800f) * Mathf.Max(0.5f, scale);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        EnsureStyles();
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, $"Admin  ({toggleKey} to close)");
        GUI.matrix = Matrix4x4.identity;
    }

    private void EnsureStyles()
    {
        if (headerStyle != null)
            return;
        headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
        noteStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true };
        noteStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
    }

    private void DrawWindow(int id)
    {
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(400f), GUILayout.Height(Mathf.Min(620f, Screen.height / Mathf.Max(1f, Screen.height / 800f) - 80f)));
        DrawPlayer();
        DrawGive();
        DrawSpawn();
        DrawGoTo();
        DrawChase();
        DrawWorld();
        GUILayout.EndScrollView();

        if (GUILayout.Button("Close"))
            SetOpen(false);
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    // ---- sections -----------------------------------------------------------------------------------------------

    private void DrawPlayer()
    {
        Header("Player");
        if (health != null)
            Bar($"Health  {health.CurrentHealth:0} / {health.MaxHealth:0}", health.HealthPercent01, new Color(0.85f, 0.25f, 0.25f));
        if (hunger != null)
            Bar($"Hunger  {hunger.CurrentHunger:0} / {hunger.MaxHunger:0}", hunger.HungerPercent01, new Color(0.9f, 0.65f, 0.2f));

        if (damageManager != null)
            damageManager.GodMode = GUILayout.Toggle(damageManager.GodMode, " God mode  (no damage from anything, starving included)");
        if (hunger != null)
            hunger.DrainPaused = GUILayout.Toggle(hunger.DrainPaused, " No hunger drain");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill health") && health != null) health.Heal(health.MaxHealth);
        if (GUILayout.Button("Hurt 25") && health != null) health.TakeDamage(25f);
        if (GUILayout.Button("Kill") && health != null) health.TakeDamage(health.MaxHealth);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill hunger") && hunger != null) hunger.Eat(hunger.MaxHunger);
        if (GUILayout.Button("Starve") && hunger != null) { hunger.ResetHunger(); hunger.Starve(); }
        if (GUILayout.Button("To checkpoint") && Checkpoint.Current != null) Teleport(Checkpoint.Current.transform.position + Vector3.up * 1.6f);
        GUILayout.EndHorizontal();
        if (swimmer != null)
            GUILayout.Label($"Position {swimmer.transform.position:0.0}", noteStyle);
    }

    private void DrawGive()
    {
        Header("Give");
        if (inventory == null)
        {
            GUILayout.Label("(no Player Inventory in the scene)", noteStyle);
            return;
        }
        if (items == null || items.Length == 0)
        {
            GUILayout.Label("(no items wired: Tools → Out of the Depths → Rebuild Test Arena, or set Items on this component)", noteStyle);
            return;
        }

        int perRow = 2;
        for (int i = 0; i < items.Length; i += perRow)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(items.Length, i + perRow); j++)
            {
                ItemDefinition item = items[j];
                if (item == null)
                    continue;
                if (GUILayout.Button(item.DisplayName))
                    inventory.Add(item, 1);
                if (item.MaxStack >= 3 && GUILayout.Button("x3", GUILayout.Width(34f)))
                    inventory.Add(item, 3);
            }
            GUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Clear inventory"))
        {
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                PlayerInventory.Slot slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                    inventory.Remove(slot.item, slot.count);
            }
        }
    }

    private void DrawSpawn()
    {
        Header("Spawn in front of you");
        if (spawnables == null || spawnables.Length == 0)
        {
            GUILayout.Label("(nothing in Spawnables)", noteStyle);
            return;
        }
        int perRow = 3;
        for (int i = 0; i < spawnables.Length; i += perRow)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(spawnables.Length, i + perRow); j++)
            {
                Spawnable s = spawnables[j];
                if (s.prefab != null && GUILayout.Button(string.IsNullOrEmpty(s.label) ? s.prefab.name : s.label))
                    Spawn(s.prefab);
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawGoTo()
    {
        Header("Go to");
        if (swimmer == null)
        {
            GUILayout.Label("(no player in the scene)", noteStyle);
            return;
        }
        if (signs.Length == 0 && checkpoints.Length == 0)
        {
            GUILayout.Label("(no signs or checkpoints in the scene)", noteStyle);
            return;
        }

        int perRow = 3;
        for (int i = 0; i < signs.Length; i += perRow)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(signs.Length, i + perRow); j++)
            {
                ProximityLabel sign = signs[j];
                if (sign == null)
                    continue;
                string label = sign.name.StartsWith("Sign_") ? sign.name.Substring(5) : sign.name;
                // Signs face the spawn pad: stand 2 m in front of the board, looking at it.
                if (GUILayout.Button(label))
                    Teleport(sign.transform.position - sign.transform.forward * 2f + Vector3.up * 1.6f);
            }
            GUILayout.EndHorizontal();
        }
        for (int i = 0; i < checkpoints.Length; i += perRow)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(checkpoints.Length, i + perRow); j++)
            {
                Checkpoint plate = checkpoints[j];
                if (plate != null && GUILayout.Button(plate.name.Replace("Checkpoint_", "Plate: ")))
                    Teleport(plate.transform.position + Vector3.up * 1.6f);
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawChase()
    {
        Header("Chase");
        if (chase == null)
        {
            GUILayout.Label("(no Chase Sequence in the scene)", noteStyle);
            return;
        }
        string state = chase.IsRunning ? "running" : chase.IsFinished ? "over (rubble down)" : "waiting";
        GUILayout.Label($"{chase.name}: {state}   danger {chase.Danger01:0.00}", noteStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Start")) chase.Begin();
        if (GUILayout.Button("End (pack leaves)")) chase.End();
        if (GUILayout.Button("Reset")) chase.ResetChase();
        GUILayout.EndHorizontal();
    }

    private void DrawWorld()
    {
        Header("World");
        GUILayout.Label($"Time scale  {timeScale:0.00}x");
        GUILayout.BeginHorizontal();
        timeScale = GUILayout.HorizontalSlider(timeScale, 0f, 3f);
        foreach (float preset in new[] { 0.25f, 0.5f, 1f, 2f })
            if (GUILayout.Button(preset.ToString("0.##"), GUILayout.Width(40f)))
                timeScale = preset;
        GUILayout.EndHorizontal();
        Time.timeScale = timeScale;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"Unlock + open all doors ({doors.Length})"))
            foreach (Door door in doors)
                if (door != null) { door.Unlock(); door.Open(); }
        if (GUILayout.Button("Close all doors"))
            foreach (Door door in doors)
                if (door != null) door.Close();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"Drop all rubble ({rubble.Length})"))
            foreach (RubbleFall pile in rubble)
                if (pile != null) pile.Drop();
        if (GUILayout.Button("Kill all fish"))
            foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                d.TakeDamage(d.MaxHealth);
        GUILayout.EndHorizontal();

        GUILayout.Label("Fish colour");
        fishTint.r = LabeledSlider("R", fishTint.r);
        fishTint.g = LabeledSlider("G", fishTint.g);
        fishTint.b = LabeledSlider("B", fishTint.b);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply to all fish")) TintAllFish(fishTint);
        if (GUILayout.Button("Reset")) { fishTint = Color.white; TintAllFish(fishTint); }
        GUILayout.EndHorizontal();
    }

    // ---- widgets ------------------------------------------------------------------------------------------------

    private void Header(string text)
    {
        GUILayout.Space(8f);
        GUILayout.Label(text, headerStyle);
    }

    private void Bar(string label, float fraction, Color color)
    {
        GUILayout.Label(label);
        Rect rect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true));
        GUI.Box(rect, GUIContent.none);
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, (rect.width - 2f) * Mathf.Clamp01(fraction), rect.height - 2f), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private float LabeledSlider(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(16f));
        value = GUILayout.HorizontalSlider(value, 0f, 1f);
        GUILayout.EndHorizontal();
        return value;
    }

    // ---- actions ------------------------------------------------------------------------------------------------

    private void Teleport(Vector3 position)
    {
        if (swimmer == null)
            return;
        var controller = swimmer.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;
        swimmer.transform.position = position;
        if (controller != null)
            controller.enabled = true;
    }

    private void Spawn(GameObject prefab)
    {
        Transform origin = spawnOrigin != null ? spawnOrigin : transform;
        Vector3 position = origin.position + origin.forward * spawnDistance;
        Quaternion rotation = Quaternion.LookRotation(-origin.forward, Vector3.up);
        var instance = Instantiate(prefab, position, rotation);

        if (instance.GetComponent<FishController>() != null)
        {
            var tint = instance.GetComponent<RendererTint>();
            if (tint == null)
                tint = instance.AddComponent<RendererTint>();
            tint.Tint = fishTint;
        }
    }

    private void TintAllFish(Color color)
    {
        foreach (var fish in FindObjectsByType<FishController>(FindObjectsSortMode.None))
        {
            var tint = fish.GetComponent<RendererTint>();
            if (tint == null)
                tint = fish.gameObject.AddComponent<RendererTint>();
            tint.Tint = color;
        }
    }
}
