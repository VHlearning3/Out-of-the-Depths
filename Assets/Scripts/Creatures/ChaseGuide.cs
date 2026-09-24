using System.Collections.Generic;
using UnityEngine;

// Tells the player what to do while the chase runs, one step at a time, without shouting: a small card at the top of
// the screen (a gold "OBJECTIVE" label, the step - "Grab the stone fragments", "Put them in the stone tablet", "Swim
// through the open door", "Grab the trident" - a line on how, and pips for the fragments), and the Direction
// Indicators' gold marker on the thing to go for, with how far it is; when that is off screen or behind you, a small
// chevron round the middle of the screen points the way. A new step slides in with a brief gold glow.
// Finds its steps by itself from the chase's own wiring, so a scene needs nothing extra: the stone tablet (the Item
// Socket on the object named Tablet Name), the fragments for it (the pickups of the item it wants nearest to it), the
// door it opens (its On Filled), and the trident (the pickup that drops the chase's End Rubble). Chase Sequence adds
// one to itself. Anything it cannot find is simply skipped.
[RequireComponent(typeof(ChaseSequence))]
public class ChaseGuide : MonoBehaviour
{
    [Tooltip("The object with the Item Socket the fragments go into.")]
    [SerializeField] private string tabletName = "RuneTablet";
    [SerializeField] private Color color = new Color(1f, 0.8f, 0.42f);
    [Tooltip("The door step ends once the player is this close to the door (then the trident is the goal).")]
    [SerializeField] private float throughDoorDistance = 2.5f;

    // The bottom of the objective card on screen (GUI pixels) while it shows, else 0, so other HUD at the top of the
    // screen (the fish health bar) can sit under it.
    public static float CardBottom { get; private set; }

    private ChaseSequence chase;
    private bool found;
    private ItemSocket tablet;
    private readonly List<PickupItem> fragments = new List<PickupItem>();
    private Component door;       // Door or DoubleDoor
    private PickupItem trident;
    private Transform player;
    private PlayerInventory inventory;
    private bool pastDoor;

    private string title;
    private string detail;
    private int have, need;       // pips (the fragments), need 0 = none
    private Vector3 target;
    private bool hasTarget;
    private float stepChangedAt = -10f;
    private float shown;

    private void Awake()
    {
        chase = GetComponent<ChaseSequence>();
    }

    private void OnDisable()
    {
        CardBottom = 0f;
        DirectionIndicators.Clear(this);
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
            shown = Mathf.MoveTowards(shown, 0f, Time.unscaledDeltaTime / 0.3f);
            if (shown <= 0f)
                title = null;
            if (chase == null || !chase.IsRunning)
                pastDoor = false;   // died and it starts over: the door is a step again
            return;
        }
        if (!found)
            Find();

        string was = title;
        Choose();
        if (title != was)
            stepChangedAt = Time.unscaledTime;
        shown = Mathf.MoveTowards(shown, title != null ? 1f : 0f, Time.unscaledDeltaTime / 0.3f);
        if (hasTarget)
            DirectionIndicators.Point(this, target);
    }

    // What to do right now and where.
    private void Choose()
    {
        title = detail = null;
        need = 0;
        hasTarget = false;
        if (tablet != null && !tablet.IsFilled)
        {
            string name = Plural(tablet.RequiredItem.DisplayName);
            int missing = tablet.RequiredAmount - tablet.Placed;
            int held = inventory != null && tablet.RequiredItem != null ? inventory.Count(tablet.RequiredItem) : 0;
            if (held < missing)
            {
                PickupItem next = NearestLeft();
                title = $"Grab the {name}";
                detail = "Follow the gold marker, keep moving";
                have = tablet.Placed + held;
                need = tablet.RequiredAmount;
                if (next != null)
                    Aim(next.transform.position);
                return;
            }
            title = $"Put the {name} in the stone tablet";
            detail = tablet.RequireHeld ? "Hold them (1-3) and press E on it" : "Swim up to it and press E";
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
                    title = "Swim through the open door";
                    detail = "Keep going, the swarm is right behind you";
                    Aim(door.transform.position + Vector3.up * 1.4f);
                    return;
                }
            }
            title = $"Grab the {(trident.Item != null ? trident.Item.DisplayName : "trident")}";
            detail = "At the end of the corridor";
            Aim(trident.transform.position);
        }
    }

    private static string Plural(string name) => string.IsNullOrEmpty(name) ? "pieces" : name.EndsWith("s") ? name : name + "s";

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

    // The card: a small dark panel at the top, the gold label, the step and how, pips on the right.
    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;
        if (string.IsNullOrEmpty(title) || shown <= 0f || PauseMenu.IsOpen || PuzzleBoard.IsOpen)
        {
            CardBottom = 0f;
            return;
        }
        float scale = HudStyle.Scale;
        float since = Time.unscaledTime - stepChangedAt;
        float slide = 1f - Ease.OutCubic(Mathf.Clamp01(since / 0.35f));
        float glow = 1f - Mathf.Clamp01(since / 1.4f);
        float alpha = shown * (1f - slide * 0.6f);

        GUIStyle label = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(11f * scale)));
        GUIStyle titleStyle = HudStyle.Label(Mathf.Max(10, Mathf.RoundToInt(21f * scale)));
        GUIStyle detailStyle = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(14f * scale)));
        float pad = 20f * scale;
        float pip = 10f * scale, pipGap = 6f * scale;
        float pipsWidth = need > 0 ? need * pip + (need - 1) * pipGap + 16f * scale : 0f;
        float titleWidth = titleStyle.CalcSize(new GUIContent(title)).x;
        float detailWidth = string.IsNullOrEmpty(detail) ? 0f : detailStyle.CalcSize(new GUIContent(detail)).x;
        float width = Mathf.Max(titleWidth + pipsWidth, detailWidth, 220f * scale) + pad * 2f;
        float height = (string.IsNullOrEmpty(detail) ? 64f : 84f) * scale;
        var card = new Rect((Screen.width - width) * 0.5f, 30f * scale - slide * 12f * scale, width, height);
        CardBottom = card.yMax;

        Color gold = color;
        HudStyle.Panel(card, 12f * scale, alpha);
        if (glow > 0f)   // a new step: a brief gold glow round the card
            HudStyle.Outline(card, 12f * scale, new Color(gold.r, gold.g, gold.b, 0.55f * glow * alpha));

        // The label: a small gold diamond and OBJECTIVE, spread out.
        float y = card.y + 10f * scale;
        float lineH = label.fontSize * 1.6f;
        if (HudStyle.BeginShapes())
        {
            HudStyle.Diamond(new Vector2(card.x + pad + 4f * scale, y + lineH * 0.5f), 4f * scale, 0.1f, Color.clear, new Color(gold.r, gold.g, gold.b, 0.9f * alpha));
            for (int i = 0; i < need; i++)
            {
                var at = new Vector2(card.xMax - pad - (need - 1 - i) * (pip + pipGap) - pip * 0.5f, card.y + 42f * scale);
                bool done = i < have;
                HudStyle.Arc(at, pip * 0.25f, pip * 0.5f, 0f, 360f, new Color(gold.r, gold.g, gold.b, (done ? 0.95f : 0.16f) * alpha));
                if (!done)
                    HudStyle.Arc(at, pip * 0.5f - 0.75f, 1.3f, 0f, 360f, new Color(gold.r, gold.g, gold.b, 0.6f * alpha));
            }
            HudStyle.EndShapes();
        }
        HudStyle.Spaced(new Vector2(card.x + pad + 14f * scale, y), "OBJECTIVE", label, gold, 2.2f * scale, alpha);
        HudStyle.Write(new Rect(card.x + pad, card.y + 42f * scale - titleStyle.fontSize * 0.8f, width - pad * 2f - pipsWidth, titleStyle.fontSize * 1.6f),
            title, titleStyle, Color.Lerp(HudStyle.Text, gold, 0.5f * glow), alpha);
        if (!string.IsNullOrEmpty(detail))
            HudStyle.Write(new Rect(card.x + pad, card.y + 66f * scale - detailStyle.fontSize * 0.8f, width - pad * 2f, detailStyle.fontSize * 1.6f),
                detail, detailStyle, HudStyle.Muted, alpha);
    }
}
