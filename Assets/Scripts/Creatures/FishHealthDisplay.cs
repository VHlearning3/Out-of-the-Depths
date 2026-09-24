using System.Collections.Generic;
using UnityEngine;

// How hurt a fish is, at a glance. While the crosshair is on it, the Fish Health Bar at the top of the screen shows
// its name and health, boss-bar style (see there). Every hit also pops up the damage it did over the fish itself,
// rising and fading, so even a fish killed in one slash shows the hit landed. Added to every fish by Fish Controller;
// reads its Damageable.
[RequireComponent(typeof(Damageable))]
public class FishHealthDisplay : MonoBehaviour
{
    [Tooltip("The name on the health bar. Empty = from the object's name (Fish_Wanderer -> Fish Wanderer).")]
    [SerializeField] private string displayName = "";
    [Tooltip("How far above the fish's middle the damage numbers start, in metres.")]
    [SerializeField] private float height = 0.45f;
    [Tooltip("Pop up the damage each hit did.")]
    [SerializeField] private bool damageNumbers = true;
    [SerializeField] private Color numberColor = new Color(1f, 0.85f, 0.5f);
    [Tooltip("Damage numbers are hidden beyond this distance from the camera, in metres.")]
    [SerializeField] private float maxDistance = 22f;

    private struct Popup
    {
        public float amount;
        public float at;
        public float drift;
    }

    private static GUIStyle numberStyle;

    private Damageable health;
    private FishController fish;
    private float lastHealth;
    private readonly List<Popup> popups = new List<Popup>();
    private string cleanName;

    public Damageable Health => health;
    public bool Alive => !health.IsDead && (fish == null || fish.IsAlive);
    // When it was last hit (Time.time), for the bar's draining chunk.
    public float LastHitAt { get; private set; } = -100f;

    public string Name
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName))
                return displayName;
            if (cleanName == null)
            {
                string raw = gameObject.name.Replace("(Clone)", "").Replace("_Placeholder", "").Replace('_', ' ');
                raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\s*\d+$", "");       // Fish_Wanderer 3 -> Fish Wanderer
                raw = System.Text.RegularExpressions.Regex.Replace(raw, @"(?<=[a-z])(?=[A-Z])", " ");   // ChasePufferfish -> Chase Pufferfish
                cleanName = System.Text.RegularExpressions.Regex.Replace(raw, @"\s+", " ").Trim();
            }
            return cleanName;
        }
    }

    private void Awake()
    {
        health = GetComponent<Damageable>();
        fish = GetComponent<FishController>();
        FishHealthBar.Ensure();
    }

    private void OnEnable()
    {
        lastHealth = health.MaxHealth;
        health.onHealthChanged.AddListener(OnHealthChanged);
    }

    private void OnDisable()
    {
        health.onHealthChanged.RemoveListener(OnHealthChanged);
    }

    private void OnHealthChanged(float current, float max)
    {
        if (current < lastHealth)
        {
            LastHitAt = Time.time;
            if (damageNumbers)
                popups.Add(new Popup { amount = lastHealth - current, at = Time.time, drift = Random.Range(-0.4f, 0.4f) });
        }
        else if (current >= max)
        {
            popups.Clear();   // back to full (it respawned)
        }
        lastHealth = current;
    }

    // The damage numbers, over the fish.
    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || popups.Count == 0 || PauseMenu.IsOpen)
            return;
        Camera view = Camera.main;
        if (view == null)
            return;
        Vector3 screen = view.WorldToScreenPoint(transform.position + Vector3.up * height);
        if (screen.z <= 0.1f || screen.z > maxDistance)
        {
            popups.Clear();
            return;
        }
        if (numberStyle == null)
        {
            numberStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            numberStyle.normal.textColor = Color.white;
            numberStyle.font = GameFont.Font;
        }
        float scale = Screen.height / 1080f * UIScale.Hud * Mathf.Clamp(6f / screen.z, 0.6f, 1.4f);   // a touch bigger up close
        var at = new Vector2(screen.x, Screen.height - screen.y);
        Color keep = GUI.color;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            float t = (Time.time - popups[i].at) / 0.9f;
            if (t >= 1f)
            {
                popups.RemoveAt(i);
                continue;
            }
            float rise = (1f - (1f - t) * (1f - t)) * 46f * scale;
            float alpha = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
            numberStyle.fontSize = Mathf.RoundToInt(22f * scale * (1f + 0.25f * (1f - Mathf.Clamp01(t * 5f))));
            var rect = new Rect(at.x - 60f + popups[i].drift * 30f * scale, at.y - rise - 24f * scale, 120f, 30f * scale);
            string text = "-" + Mathf.RoundToInt(popups[i].amount);
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.6f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, numberStyle);
            GUI.color = new Color(numberColor.r, numberColor.g, numberColor.b, alpha);
            GUI.Label(rect, text, numberStyle);
        }
        GUI.color = keep;
    }
}
