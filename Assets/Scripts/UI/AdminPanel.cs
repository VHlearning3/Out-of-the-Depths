using UnityEngine;
using UnityEngine.InputSystem;

// Debug panel (IMGUI, so it needs no canvas or EventSystem). Toggle with the key below.
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
    [SerializeField] private Behaviour[] pauseWhileOpen;

    [Header("Spawning")]
    [SerializeField] private Spawnable[] spawnables;
    [SerializeField] private Transform spawnOrigin;
    [SerializeField] private float spawnDistance = 3f;

    [Header("Fish colour")]
    [SerializeField] private Color fishTint = Color.white;

    private bool open;
    private Rect windowRect = new Rect(20f, 20f, 320f, 600f);
    private Vector2 scroll;
    private HungerSystem hunger;
    private HealthSystem health;
    private DamageManager damageManager;
    private float timeScale = 1f;

    private void Start()
    {
        hunger = FindFirstObjectByType<HungerSystem>();
        health = FindFirstObjectByType<HealthSystem>();
        damageManager = FindFirstObjectByType<DamageManager>();
        if (spawnOrigin == null && Camera.main != null)
            spawnOrigin = Camera.main.transform;
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
    }

    private void OnGUI()
    {
        if (!open)
            return;

        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, $"Admin  ({toggleKey} to close)");
    }

    private void DrawWindow(int id)
    {
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label("Spawn in front of you");
        foreach (var s in spawnables)
        {
            if (s.prefab != null && GUILayout.Button(string.IsNullOrEmpty(s.label) ? s.prefab.name : s.label))
                Spawn(s.prefab);
        }

        GUILayout.Space(10f);
        GUILayout.Label("Player");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill hunger") && hunger != null) hunger.Eat(hunger.MaxHunger);
        if (GUILayout.Button("Fill health") && health != null) health.Heal(health.MaxHealth);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Starve") && hunger != null) hunger.ResetHunger(); // reset first so the drain event can re-fire
        if (GUILayout.Button("Kill") && health != null) health.TakeDamage(health.MaxHealth);
        GUILayout.EndHorizontal();
        if (damageManager != null)
            damageManager.GodMode = GUILayout.Toggle(damageManager.GodMode, "God mode");

        GUILayout.Space(10f);
        GUILayout.Label($"Time scale: {timeScale:0.0}x");
        timeScale = GUILayout.HorizontalSlider(timeScale, 0f, 3f);
        Time.timeScale = timeScale;

        GUILayout.Space(10f);
        GUILayout.Label("Fish colour");
        fishTint.r = LabeledSlider("R", fishTint.r);
        fishTint.g = LabeledSlider("G", fishTint.g);
        fishTint.b = LabeledSlider("B", fishTint.b);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply to all fish")) TintAllFish(fishTint);
        if (GUILayout.Button("Reset")) { fishTint = Color.white; TintAllFish(fishTint); }
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Kill all fish"))
        {
            foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                d.TakeDamage(d.MaxHealth);
        }

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    private float LabeledSlider(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(16f));
        value = GUILayout.HorizontalSlider(value, 0f, 1f);
        GUILayout.EndHorizontal();
        return value;
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
