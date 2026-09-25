using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Debug page of the pause menu (Esc, or 0 to jump straight here). IMGUI, drawn inside PauseMenu. Everything in it works
// on whatever is in the scene: player stats and cheats, give any item, spawn things, teleport to any sign or checkpoint,
// drive the chase, doors and rubble, time scale, fish colours. The arena builder fills Items and Spawnables.
public class AdminPanel : MonoBehaviour, IPauseMenuPage
{
    [System.Serializable]
    public class Spawnable
    {
        public string label;
        public GameObject prefab;
    }

    [Header("Spawning")]
    [SerializeField] private Spawnable[] spawnables;
    [SerializeField] private Transform spawnOrigin;
    [SerializeField] private float spawnDistance = 3f;

    [Header("Items")]
    [Tooltip("Items the Give buttons hand out. Rebuild Test Arena fills this with every item in Assets/Items.")]
    [SerializeField] private ItemDefinition[] items;

    [Header("Fish colour")]
    [SerializeField] private Color fishTint = Color.white;

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
    private DoubleDoor[] doubleDoors = new DoubleDoor[0];
    private PuzzleStation[] puzzles = new PuzzleStation[0];
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
        RequestedTimeScale = timeScale;
    }

    private Vector3 lastPlayerPosition;
    private float driftPerSecond;

    private void Update()
    {
        // How fast the player object is actually moving, whoever is moving it.
        if (swimmer != null && Time.deltaTime > 0f)
        {
            driftPerSecond = (swimmer.transform.position - lastPlayerPosition).magnitude / Time.deltaTime;
            lastPlayerPosition = swimmer.transform.position;
        }
    }

    private void RefreshSceneLists()
    {
        signs = FindObjectsByType<ProximityLabel>(FindObjectsSortMode.None);
        System.Array.Sort(signs, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        checkpoints = FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(checkpoints, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        doors = FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        doubleDoors = FindObjectsByType<DoubleDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        puzzles = FindObjectsByType<PuzzleStation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        rubble = FindObjectsByType<RubbleFall>(FindObjectsSortMode.None);
        chase = FindFirstObjectByType<ChaseSequence>();
    }

    // ---- the pause menu page -----------------------------------------------------------------------------------

    public string PageTitle => "Admin";
    public int Order => 100;

    // Time scale the World section asks for; the pause menu applies it when the game resumes.
    public float RequestedTimeScale { get; private set; } = 1f;

    public void OnPageShown()
    {
        RefreshSceneLists();
    }

    public void DrawPage()
    {
        EnsureStyles();
        DrawPlayer();
        DrawGive();
        DrawSpawn();
        DrawGoTo();
        DrawChase();
        DrawWorld();
    }

    private void EnsureStyles()
    {
        if (headerStyle != null)
            return;
        headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 };
        headerStyle.normal.textColor = new Color(0.35f, 0.85f, 0.95f);
        noteStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
        noteStyle.normal.textColor = new Color(0.7f, 0.76f, 0.82f);
    }

    // ---- sections -----------------------------------------------------------------------------------------------

    private void DrawPlayer()
    {
        MenuGUI.Heading("Player");
        if (health != null)
            Bar($"Health  {health.CurrentHealth:0} / {health.MaxHealth:0}", health.HealthPercent01, new Color(0.85f, 0.25f, 0.25f));
        if (hunger != null)
            Bar($"Hunger  {hunger.CurrentHunger:0} / {hunger.MaxHunger:0}", hunger.HungerPercent01, new Color(0.9f, 0.65f, 0.2f));

        if (damageManager != null)
            damageManager.GodMode = MenuGUI.SwitchRow("God mode  (no damage from anything, starving included)", damageManager.GodMode);
        if (hunger != null)
            hunger.DrainPaused = MenuGUI.SwitchRow("No hunger drain", hunger.DrainPaused);

        GUILayout.BeginHorizontal();
        if (MenuGUI.Button("Fill health") && health != null) health.Heal(health.MaxHealth);
        if (MenuGUI.Button("Hurt 25") && health != null) health.TakeDamage(25f);
        if (MenuGUI.Button("Kill") && health != null) health.TakeDamage(health.MaxHealth);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (MenuGUI.Button("Fill hunger") && hunger != null) hunger.Eat(hunger.MaxHunger);
        if (MenuGUI.Button("Starve") && hunger != null) { hunger.ResetHunger(); hunger.Starve(); }
        if (MenuGUI.Button("To checkpoint") && Checkpoint.Current != null) Teleport(Checkpoint.Current.transform.position + Vector3.up * 1.6f);
        GUILayout.EndHorizontal();
        if (swimmer != null)
        {
            GUILayout.Label($"Position {swimmer.transform.position:0.00}   moving {driftPerSecond:0.000} m/s", noteStyle);
            GUILayout.Label($"Last swim input raw {swimmer.LastRawMoveInput:0.00} / after deadzone {swimmer.LastMoveInput:0.00}   velocity {swimmer.CurrentVelocity:0.00}", noteStyle);
            swimmer.Frozen = MenuGUI.SwitchRow("Freeze swim movement (debug)", swimmer.Frozen);
            swimmer.ShowMovementDebug = MenuGUI.SwitchRow("Show movement debug line at the bottom of the screen (stays on after closing this panel)", swimmer.ShowMovementDebug);
            PuzzleBoard.ShowAnswer = MenuGUI.SwitchRow("Show the rune puzzle's answer on its board (testing)", PuzzleBoard.ShowAnswer);
            HelloFish.Always = MenuGUI.SwitchRow("Hello fish on every inspect (testing the easter egg)", HelloFish.Always);
        }
        DrawInputDevices();
    }

    // Every device the Input System currently sees and what its sticks report right now, read straight from the
    // hardware (works while the panel has the swim controls switched off). A drifting stick shows up here.
    private void DrawInputDevices()
    {
        var names = new System.Text.StringBuilder();
        foreach (InputDevice device in InputSystem.devices)
        {
            if (names.Length > 0)
                names.Append(", ");
            names.Append(device.displayName);
        }
        GUILayout.Label("Input devices: " + (names.Length > 0 ? names.ToString() : "none"), noteStyle);

        var live = new System.Text.StringBuilder();
        if (Gamepad.current != null)
            live.Append($"gamepad left stick {Gamepad.current.leftStick.ReadValue():0.00}  ");
        if (Joystick.current != null)
            live.Append($"joystick {Joystick.current.stick.ReadValue():0.00}  ");
        if (Keyboard.current != null)
        {
            var held = new System.Text.StringBuilder();
            foreach (KeyControl key in Keyboard.current.allKeys)
                if (key.isPressed)
                    held.Append(key.displayName).Append(' ');
            live.Append(held.Length > 0 ? "keys held: " + held : "no keys held");
        }
        GUILayout.Label(live.ToString(), noteStyle);
    }

    private void DrawGive()
    {
        MenuGUI.Heading("Give");
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
                if (MenuGUI.Button(item.DisplayName))
                    inventory.Add(item, 1);
                if (item.MaxStack >= 3 && MenuGUI.Button("x3", GUILayout.Width(60f)))
                    inventory.Add(item, 3);
            }
            GUILayout.EndHorizontal();
        }
        if (MenuGUI.Button("Clear inventory"))
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
        MenuGUI.Heading("Spawn in front of you");
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
                if (s.prefab != null && MenuGUI.Button(string.IsNullOrEmpty(s.label) ? s.prefab.name : s.label))
                    Spawn(s.prefab);
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawGoTo()
    {
        MenuGUI.Heading("Go to");
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
                // Signs float above head height: stand on the floor 2.5 m in front of the board, looking at it.
                if (MenuGUI.Button(label))
                {
                    Vector3 p = sign.transform.position - sign.transform.forward * 2.5f;
                    Teleport(new Vector3(p.x, 1.6f, p.z));
                }
            }
            GUILayout.EndHorizontal();
        }
        for (int i = 0; i < checkpoints.Length; i += perRow)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(checkpoints.Length, i + perRow); j++)
            {
                Checkpoint plate = checkpoints[j];
                if (plate != null && MenuGUI.Button(plate.name.Replace("Checkpoint_", "Plate: ")))
                    Teleport(plate.transform.position + Vector3.up * 1.6f);
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawChase()
    {
        MenuGUI.Heading("Chase");
        if (chase == null)
        {
            GUILayout.Label("(no Chase Sequence in the scene)", noteStyle);
            return;
        }
        string state = chase.IsRunning ? "running" : chase.IsFinished ? "over (rubble down)" : "waiting";
        GUILayout.Label($"{chase.name}: {state}   danger {chase.Danger01:0.00}", noteStyle);
        GUILayout.BeginHorizontal();
        if (MenuGUI.Button("Go to its start") && chase.StartSpot(out Vector3 spot, out Vector3 vent))
        {
            Teleport(spot);
            if (swimmer != null)
            {
                Vector2 look = swimmer.AnglesToward(vent);
                swimmer.SetLookAngles(look.x, look.y);
            }
        }
        if (MenuGUI.Button("Start")) chase.Begin();
        if (MenuGUI.Button("End (pack leaves)")) chase.End();
        if (MenuGUI.Button("Reset")) chase.ResetChase();
        GUILayout.EndHorizontal();
        if (MenuGUI.Button("Play the ending (credits)"))
        {
            FindFirstObjectByType<PauseMenu>()?.Resume();
            EndCredits.Play();
        }
    }

    private void DrawWorld()
    {
        MenuGUI.Heading("World");
        GUILayout.Label($"Time scale  {timeScale:0.00}x");
        GUILayout.BeginHorizontal();
        timeScale = GUILayout.HorizontalSlider(timeScale, 0f, 3f);
        foreach (float preset in new[] { 0.25f, 0.5f, 1f, 2f })
            if (MenuGUI.Button(preset.ToString("0.##"), GUILayout.Width(64f)))
                timeScale = preset;
        GUILayout.EndHorizontal();
        RequestedTimeScale = timeScale;   // applied by the pause menu when the game resumes

        GUILayout.BeginHorizontal();
        if (MenuGUI.Button($"Unlock + open all doors ({doors.Length + doubleDoors.Length})"))
        {
            foreach (Door door in doors)
                if (door != null) { door.Unlock(); door.Open(); }
            foreach (DoubleDoor door in doubleDoors)
                if (door != null) door.Open();
        }
        if (MenuGUI.Button("Close all doors"))
        {
            foreach (Door door in doors)
                if (door != null) door.Close();
            foreach (DoubleDoor door in doubleDoors)
                if (door != null) door.Close();
        }
        if (MenuGUI.Button($"Solve all puzzles ({puzzles.Length})"))
            foreach (PuzzleStation puzzle in puzzles)
                if (puzzle != null) puzzle.SolveNow();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (MenuGUI.Button($"Drop all rubble ({rubble.Length})"))
            foreach (RubbleFall pile in rubble)
                if (pile != null) pile.Drop();
        if (MenuGUI.Button("Kill all fish"))
            foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                d.TakeDamage(d.MaxHealth);
        GUILayout.EndHorizontal();

        FishColors.Enabled = MenuGUI.SwitchRow("Colour fish by type (testing): blue wanderer, red pufferfish, yellow wall fish, purple chase pack, green dead = food, grey dead = not food", FishColors.Enabled);
        GUILayout.Label("Fish colour");
        fishTint.r = LabeledSlider("R", fishTint.r);
        fishTint.g = LabeledSlider("G", fishTint.g);
        fishTint.b = LabeledSlider("B", fishTint.b);
        GUILayout.BeginHorizontal();
        if (MenuGUI.Button("Apply to all fish")) TintAllFish(fishTint);
        if (MenuGUI.Button("Reset")) { fishTint = Color.white; TintAllFish(fishTint); }
        GUILayout.EndHorizontal();
    }

    // ---- widgets ------------------------------------------------------------------------------------------------


    // A rounded track with a coloured fill.
    private void Bar(string label, float fraction, Color color)
    {
        GUILayout.Label(label);
        Rect rect = GUILayoutUtility.GetRect(12f, 12f, GUILayout.ExpandWidth(true));
        GUI.Box(rect, GUIContent.none, GUI.skin.horizontalSlider);
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * Mathf.Clamp01(fraction), rect.height - 4f), Texture2D.whiteTexture);
        GUI.color = previous;
        GUILayout.Space(12f);
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
