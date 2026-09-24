using System.Collections.Generic;
using UnityEngine;

// Tells the player what to do while the chase runs, one step at a time: a banner under the top of the screen ("Grab
// the 2 stone fragments  1/2", "Put them in the stone tablet", "The door is open - swim through!", "Grab the
// trident") and a marker on the thing to go for, with how far it is; when it is off screen or behind, an arrow at the
// edge of the screen points the way. Gold, so it never reads as the red danger marker.
// Finds its steps by itself from the chase's own wiring, so a scene needs nothing extra: the stone tablet (the Item
// Socket on the object named Tablet Name), the fragments for it (the pickups of the item it wants nearest to it), the
// door it opens (its On Filled), and the trident (the pickup that drops the chase's End Rubble). Chase Sequence adds
// one to itself. Anything it cannot find is simply skipped.
[RequireComponent(typeof(ChaseSequence))]
public class ChaseGuide : MonoBehaviour
{
    [Tooltip("The object with the Item Socket the fragments go into.")]
    [SerializeField] private string tabletName = "RuneTablet";
    [SerializeField] private Color color = new Color(1f, 0.82f, 0.35f);
    [Tooltip("The door step ends once the player is this close to the door (then the trident is the goal).")]
    [SerializeField] private float throughDoorDistance = 2.5f;

    private ChaseSequence chase;
    private bool found;
    private ItemSocket tablet;
    private readonly List<PickupItem> fragments = new List<PickupItem>();
    private Component door;       // Door or DoubleDoor
    private PickupItem trident;
    private Transform player;
    private PlayerInventory inventory;
    private bool pastDoor;

    private string step;
    private Vector3 target;
    private bool hasTarget;
    private float stepChangedAt;

    private Texture2D white;
    private Texture2D arrow;
    private Texture2D diamond;
    private GUIStyle bannerStyle;
    private GUIStyle distanceStyle;

    private void Awake()
    {
        chase = GetComponent<ChaseSequence>();
    }

    private void OnDestroy()
    {
        foreach (Texture2D tex in new[] { white, arrow, diamond })
            if (tex != null)
                Destroy(tex);
    }

    // The chase's pieces, looked up once (the first time the chase runs).
    private void Find()
    {
        found = true;
        var swimmer = FindFirstObjectByType<SwimController>();
        player = swimmer != null ? swimmer.transform : null;
        inventory = FindFirstObjectByType<PlayerInventory>();

        foreach (ItemSocket socket in FindObjectsByType<ItemSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (socket.name == tabletName)
            {
                tablet = socket;
                break;
            }

        if (tablet != null && tablet.RequiredItem != null)
        {
            var all = new List<PickupItem>();
            foreach (PickupItem pickup in FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
                if (pickup.Item == tablet.RequiredItem)
                    all.Add(pickup);
            Vector3 at = tablet.transform.position;
            all.Sort((a, b) => (a.transform.position - at).sqrMagnitude.CompareTo((b.transform.position - at).sqrMagnitude));
            for (int i = 0; i < all.Count && i < tablet.RequiredAmount; i++)
                fragments.Add(all[i]);

            for (int i = 0; i < tablet.onFilled.GetPersistentEventCount(); i++)
                if (tablet.onFilled.GetPersistentTarget(i) is Door || tablet.onFilled.GetPersistentTarget(i) is DoubleDoor)
                {
                    door = (Component)tablet.onFilled.GetPersistentTarget(i);
                    break;
                }
        }

        RubbleFall rubble = chase.EndRubble;
        trident = rubble != null ? rubble.DropOnPickup : null;
    }

    private void Update()
    {
        bool running = chase != null && chase.IsRunning && !chase.InCutscene;
        if (!running)
        {
            step = null;
            if (chase == null || !chase.IsRunning)
                pastDoor = false;   // died and it starts over: the door is a step again
            return;
        }
        if (!found)
            Find();

        string was = step;
        Choose();
        if (step != was)
            stepChangedAt = Time.unscaledTime;
    }

    // What to do right now and where.
    private void Choose()
    {
        step = null;
        hasTarget = false;
        if (tablet != null && !tablet.IsFilled)
        {
            int need = tablet.RequiredAmount - tablet.Placed;
            int have = inventory != null && tablet.RequiredItem != null ? inventory.Count(tablet.RequiredItem) : 0;
            if (have < need)
            {
                PickupItem next = NearestLeft();
                string name = tablet.RequiredItem.DisplayName;
                step = $"Grab the {tablet.RequiredAmount} {name}s   {tablet.Placed + have} / {tablet.RequiredAmount}";
                if (next != null)
                    Aim(next.transform.position);
                return;
            }
            step = $"Put the {tablet.RequiredItem.DisplayName}s in the stone tablet: hold them (1-3) and press E on it";
            Aim(tablet.transform.position);
            return;
        }
        if (trident != null && !trident.Collected)
        {
            if (door != null && !pastDoor && player != null)
            {
                if (Vector3.Distance(player.position, door.transform.position) < throughDoorDistance)
                    pastDoor = true;
                else
                {
                    step = "The door is open - swim through it!";
                    Aim(door.transform.position + Vector3.up * 1.4f);
                    return;
                }
            }
            step = $"Grab the {(trident.Item != null ? trident.Item.DisplayName : "trident")} at the end of the corridor";
            Aim(trident.transform.position);
        }
    }

    private PickupItem NearestLeft()
    {
        PickupItem best = null;
        float bestDistance = float.MaxValue;
        foreach (PickupItem pickup in fragments)
        {
            if (pickup == null || pickup.Collected || !pickup.gameObject.activeInHierarchy)
                continue;
            float d = player != null ? (pickup.transform.position - player.position).sqrMagnitude : 0f;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = pickup;
            }
        }
        return best;
    }

    private void Aim(Vector3 at)
    {
        target = at;
        hasTarget = true;
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(step) || PauseMenu.IsOpen || PuzzleBoard.IsOpen || Event.current.type != EventType.Repaint)
            return;
        Ensure();
        float scale = Screen.height / 1080f * UIScale.Hud;
        float fresh = Mathf.Clamp01((Time.unscaledTime - stepChangedAt) / 0.35f);   // a new step slides in and flashes
        float flash = 1f - Mathf.Clamp01((Time.unscaledTime - stepChangedAt) / 1.2f);

        // The banner.
        bannerStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(30f * scale));
        var content = new GUIContent(step);
        Vector2 size = bannerStyle.CalcSize(content);
        float pad = 18f * scale;
        var box = new Rect((Screen.width - size.x) * 0.5f - pad, 150f * scale - (1f - fresh) * 20f * scale, size.x + pad * 2f, size.y + pad);
        Color was = GUI.color;
        GUI.color = new Color(0.02f, 0.06f, 0.08f, 0.78f * fresh);
        GUI.DrawTexture(box, white);
        GUI.color = new Color(color.r, color.g, color.b, fresh);
        GUI.DrawTexture(new Rect(box.x, box.yMax - Mathf.Max(2f, 3f * scale), box.width, Mathf.Max(2f, 3f * scale)), white);
        GUI.color = Color.Lerp(new Color(1f, 1f, 1f, fresh), new Color(color.r, color.g, color.b, fresh), 0.35f + 0.65f * flash);
        GUI.Label(box, content, bannerStyle);

        if (hasTarget)
            DrawMarker(scale);
        GUI.color = was;
    }

    // On screen: a diamond over the target with the distance under it, bobbing. Off screen or behind: an arrow on the
    // edge of the screen pointing the way.
    private void DrawMarker(float scale)
    {
        Camera eye = Camera.main;
        if (eye == null)
            return;
        Vector3 screen = eye.WorldToScreenPoint(target);
        float metres = player != null ? Vector3.Distance(player.position, target) : 0f;
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
        var middle = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var at = new Vector2(screen.x, Screen.height - screen.y);
        float margin = 60f * scale;
        bool onScreen = screen.z > 0f && at.x > margin && at.x < Screen.width - margin && at.y > margin && at.y < Screen.height - margin;

        if (onScreen)
        {
            float size = 34f * scale;
            float bob = Mathf.Sin(Time.unscaledTime * 3f) * 5f * scale;
            GUI.color = new Color(color.r, color.g, color.b, 0.9f * pulse);
            GUI.DrawTexture(new Rect(at.x - size * 0.5f, at.y - size * 1.6f + bob, size, size), diamond);
            distanceStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(20f * scale));
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.Label(new Rect(at.x - 60f * scale, at.y - size * 0.55f + bob, 120f * scale, 26f * scale), Mathf.RoundToInt(metres) + " m", distanceStyle);
            return;
        }

        // Which way: towards it on screen (flipped when it is behind the camera), pinned to an ellipse inside the edges.
        Vector2 direction = at - middle;
        if (screen.z < 0f)
            direction = -direction;
        if (direction.sqrMagnitude < 1f)
            direction = Vector2.down;
        direction.Normalize();
        var edge = middle + new Vector2(direction.x * (Screen.width * 0.5f - margin), direction.y * (Screen.height * 0.5f - margin));
        float arrowSize = 48f * scale;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;   // the texture points up
        Matrix4x4 keep = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, edge);
        GUI.color = new Color(color.r, color.g, color.b, 0.95f * pulse);
        GUI.DrawTexture(new Rect(edge.x - arrowSize * 0.5f, edge.y - arrowSize * 0.5f, arrowSize, arrowSize), arrow);
        GUI.matrix = keep;
        distanceStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(20f * scale));
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.Label(new Rect(edge.x - 60f * scale - direction.x * arrowSize, edge.y - 13f * scale - direction.y * arrowSize, 120f * scale, 26f * scale), Mathf.RoundToInt(metres) + " m", distanceStyle);
    }

    private void Ensure()
    {
        if (white == null)
        {
            white = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }
        arrow ??= Shape(64, true);
        diamond ??= Shape(64, false);
        if (bannerStyle == null)
        {
            bannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, fontStyle = FontStyle.Bold };
            bannerStyle.normal.textColor = Color.white;
            bannerStyle.font = GameFont.Font;
            distanceStyle = new GUIStyle(bannerStyle);
        }
    }

    // A soft-edged white shape: an arrowhead pointing up (arrow) or a diamond.
    private static Texture2D Shape(int size, bool isArrow)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;   // -1..1, left to right
                float v = (y + 0.5f) / size * 2f - 1f;   // -1..1, bottom to top
                float d;
                if (isArrow)
                {
                    // A chevron: the space under two lines meeting at the top, minus a notch out of the bottom.
                    float outer = (0.85f - v) * 0.85f - Mathf.Abs(u);
                    float notch = Mathf.Abs(u) - (0.2f - v) * 0.85f;
                    d = Mathf.Min(Mathf.Min(outer, notch), v + 0.8f);
                }
                else
                {
                    d = 0.8f - (Mathf.Abs(u) + Mathf.Abs(v));
                }
                float a = Mathf.Clamp01(d * size * 0.5f + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
