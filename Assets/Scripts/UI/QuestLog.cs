using System;
using UnityEngine;
using Object = UnityEngine.Object;

// It opens as the tutorial: the first steps are the controls (look around, swim, up and down, dash), their how shown at
// once (Show How At Once), each with a Tutorial Card (Lesson: the game pauses and a picture shows the keys); and it
// watches for the other firsts (the first item, the first lock, the first checkpoint, low hunger, the first pufferfish)
// and shows their cards too.
// Where to go next, without giving it away: the card only names the goal, loosely ("Find a way out of this room"), and
// only once the player has been stuck on it for Help After seconds of play does the hint line (Detail) fade in, with the
// gold marker on the thing to go for. Exploring is the point; the help is for when it has stopped being fun.
// The level's steps in order (find the key, unlock the door, get the dagger, ...), each with what
// finishes it and what to point at. The first step not finished yet is shown in a small card at the top of the screen
// (a gold GOAL label, the step, a line on how) with the gold marker from Direction Indicators on the thing to go for
// (the nearest of its Point At that is still there: pickups first, while any are left, then the rest; off screen a
// chevron round the middle of the screen points the way). A step counts as done when any of its Done When holds, or any
// later step is done (so doing things in a different order, or a checkpoint reload, never leaves it behind); a new step
// slides in with a soft chime and a gold glow, and after a few seconds the card settles to just the step. Hidden during
// the chase (the Chase Guide has its own), the pause menu, a puzzle board and the credits.
// The Ship Greybox builder fills in the steps for Main_Scene; edit them freely in the Inspector.
public class QuestLog : MonoBehaviour
{
    public enum Check { None, HaveItem, SocketFilled, PuzzleSolved, DoorOpen, DoorUnlocked, PickupTaken, KnotTied, ChaseOver, PlayerBelow, LookedAround, Swam, Rose, Dashed }

    [Serializable]
    public class Condition
    {
        public Check check;
        [Tooltip("Have Item: which, and how many.")]
        public ItemDefinition item;
        public int amount = 1;
        [Tooltip("Socket Filled / Puzzle Solved / Door Open / Door Unlocked / Pickup Taken / Knot Tied / Chase Over: the component to watch (an Item Socket, a Puzzle Station, a Door or Double Door, a Pickup Item, a Bone Key Tying, a Chase Sequence).")]
        public Object target;
        [Tooltip("Player Below: the height (y) the player has to be under (down the hatch).")]
        public float height;
    }

    [Serializable]
    public class Step
    {
        public string title = "";
        [TextArea(1, 3)] public string detail = "";
        [Tooltip("Done when any of these holds (or any later step is done).")]
        public Condition[] doneWhen = new Condition[0];
        [Tooltip("The marker goes on the nearest of these that is still there (pickups first while any are left).")]
        public Transform[] pointAt = new Transform[0];
        [Tooltip("Tutorial Cards to show as this step comes up (ids, comma-separated: swim, dash, goals, interact, items, locks, fight, eat, checkpoint, danger).")]
        public string lesson = "";
        [Tooltip("Show the how line straight away (the tutorial steps), not only once the player has been stuck a while.")]
        public bool showHowAtOnce;
    }

    [SerializeField] private Step[] steps = new Step[0];
    [SerializeField] private Color color = new Color(1f, 0.8f, 0.42f);
    [Tooltip("Played when a step is done and the next one comes up.")]
    [SerializeField] private AudioClip stepSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [Tooltip("Seconds of play (pausing does not count) on one step before its hint line and the marker show: a nudge when stuck, not a guide. 0 = straight away; negative = never.")]
    [SerializeField] private float helpAfter = 120f;

    public Step Current => current >= 0 && current < steps.Length ? steps[current] : null;
    // The bottom of the card on screen (GUI pixels) while it shows, else 0, so the fish health bar can sit under it.
    public static float CardBottom { get; private set; }

    private int current = -2;       // -2 = not worked out yet
    private float changedAt = -100f;
    private float stepPlayTime;     // seconds of play on the current step
    private float helpShown;        // 0..1, the hint and marker fading in
    private float shown;
    private PlayerInventory inventory;
    private Transform player;
    private Vector3 target;
    private bool hasTarget;

    // The builders set the steps.
    public void Configure(Step[] newSteps, AudioClip sound)
    {
        steps = newSteps ?? new Step[0];
        stepSound = sound;
    }

    private void OnDisable()
    {
        CardBottom = 0f;
        DirectionIndicators.Clear(this);
    }

    private void Update()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();
        if (player == null)
        {
            var swimmer = FindFirstObjectByType<SwimController>();
            player = swimmer != null ? swimmer.transform : null;
        }

        int now = FirstUnfinished();
        if (now != current)
        {
            bool first = current == -2;
            current = now;
            changedAt = Time.unscaledTime;
            stepPlayTime = 0f;
            helpShown = 0f;
            if (!first && Current != null && stepSound != null)
                SoundVariety.PlayAt(stepSound, player != null ? player.position : transform.position, volume);
            if (Current != null && !string.IsNullOrEmpty(Current.lesson))
                foreach (string lesson in Current.lesson.Split(','))
                    TutorialCards.Show(lesson.Trim());
        }

        bool hidden = Hidden();
        shown = Mathf.MoveTowards(shown, hidden || Current == null ? 0f : 1f, Time.unscaledDeltaTime / 0.3f);
        if (!hidden)
            stepPlayTime += Time.deltaTime;
        Track();
        WatchFirsts();
        bool help = Current != null && Current.showHowAtOnce || helpAfter >= 0f && stepPlayTime >= helpAfter;
        helpShown = Mathf.MoveTowards(helpShown, help ? 1f : 0f, Time.unscaledDeltaTime / 0.8f);
        hasTarget = !hidden && help && Current != null && Aim(Current, out target);
        if (hasTarget)
            DirectionIndicators.Point(this, target);
        else
            DirectionIndicators.Clear(this);
    }

    private static bool Hidden()
    {
        if (PauseMenu.IsOpen || PuzzleBoard.IsOpen || EndCredits.Playing)
            return true;
        ChaseSequence chase = ChaseSequence.Active;
        return chase != null && chase.IsRunning;
    }

    // The first step not done, where a step also counts as done when any later one is.
    private int FirstUnfinished()
    {
        int lastDone = -1;
        for (int i = steps.Length - 1; i >= 0; i--)
            if (Done(steps[i]))
            {
                lastDone = i;
                break;
            }
        return lastDone + 1 < steps.Length ? lastDone + 1 : -1;
    }

    private bool Done(Step step)
    {
        if (step == null || step.doneWhen == null)
            return false;
        foreach (Condition c in step.doneWhen)
            if (c != null && Holds(c))
                return true;
        return false;
    }

    private bool Holds(Condition c)
    {
        switch (c.check)
        {
            case Check.HaveItem:
                return inventory != null && c.item != null && inventory.Count(c.item) >= Mathf.Max(1, c.amount);
            case Check.SocketFilled:
                return c.target is ItemSocket socket && socket != null && socket.IsFilled;
            case Check.PuzzleSolved:
                return c.target is PuzzleStation station && station != null && station.IsSolved;
            case Check.DoorOpen:
                return (c.target is Door door && door != null && door.IsOpen) || (c.target is DoubleDoor doors && doors != null && doors.IsOpen);
            case Check.DoorUnlocked:
                return (c.target is Door door2 && door2 != null && !door2.IsLocked) || (c.target is DoubleDoor doors2 && doors2 != null && !doors2.IsLocked);
            case Check.PickupTaken:
                // Taken, or gone from the world (a checkpoint reload removes what you had already taken).
                return c.target is PickupItem pickup && (pickup == null || pickup.Collected);
            case Check.KnotTied:
                return c.target is BoneKeyTying knot && knot != null && knot.IsTied;
            case Check.ChaseOver:
                return c.target is ChaseSequence chase && chase != null && chase.IsFinished;
            case Check.LookedAround:
                return turned >= Mathf.Max(1, c.amount);
            case Check.Swam:
                return swum >= Mathf.Max(1, c.amount);
            case Check.Rose:
                return risen >= Mathf.Max(1, c.amount);
            case Check.Dashed:
                return swimmer != null && swimmer.DashCount >= Mathf.Max(1, c.amount);
            case Check.PlayerBelow:
                return player != null && player.position.y < c.height;
            default:
                return false;
        }
    }

    // ---- The tutorial -----------------------------------------------------------------------------------------------

    private SwimController swimmer;
    private PlayerInteractor interactor;
    private HungerSystem hunger;
    private float swum, risen, turned;   // metres swum along, metres up or down, degrees looked round (this level)
    private Vector3 lastPosition;
    private float lastYaw = float.NaN;

    // How far the player has swum, gone up or down and turned, for the first tutorial steps.
    private void Track()
    {
        if (swimmer == null)
            swimmer = FindFirstObjectByType<SwimController>();
        if (player == null || swimmer == null || swimmer.Frozen || Time.deltaTime <= 0f)
            return;
        Vector3 moved = player.position - lastPosition;
        lastPosition = player.position;
        if (moved.sqrMagnitude < 4f)   // not a teleport (a respawn)
        {
            swum += new Vector2(moved.x, moved.z).magnitude;
            risen += Mathf.Abs(moved.y);
        }
        Camera eye = Camera.main;
        if (eye != null)
        {
            float yaw = eye.transform.eulerAngles.y;
            if (!float.IsNaN(lastYaw))
                turned += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
            lastYaw = yaw;
        }
    }

    // The other firsts, each with its card the first time it happens.
    private void WatchFirsts()
    {
        if (!TutorialCards.Enabled)
            return;
        if (interactor == null)
            interactor = FindFirstObjectByType<PlayerInteractor>();
        if (hunger == null)
            hunger = FindFirstObjectByType<HungerSystem>();
        if (inventory != null)
            for (int i = 0; i < inventory.SlotCount; i++)
                if (!inventory.GetSlot(i).IsEmpty)
                {
                    TutorialCards.Show("items");
                    break;
                }
        if (interactor != null && !interactor.Busy)
        {
            IInteractable target = interactor.CurrentTarget;
            if (target is IPromptTone toned && toned.Tone != PromptTone.Normal)
                TutorialCards.Show("locks");
            if (target is Checkpoint)
                TutorialCards.Show("checkpoint");
        }
        if (hunger != null && hunger.HungerPercent01 < 0.6f)
            TutorialCards.Show("eat");
        if (FishAggression.Chasing.Count > 0)
            TutorialCards.Show("danger");
    }

    // The nearest thing to go for: pickups still lying about first, else the rest.
    private bool Aim(Step step, out Vector3 at)
    {
        at = default;
        if (step.pointAt == null || step.pointAt.Length == 0)
            return false;
        Vector3 from = player != null ? player.position : transform.position;
        float best = float.MaxValue;
        bool pickups = false, found = false;
        for (int pass = 0; pass < 2 && !found; pass++)
        {
            foreach (Transform t in step.pointAt)
            {
                if (t == null || !t.gameObject.activeInHierarchy)
                    continue;
                var pickup = t.GetComponentInChildren<PickupItem>();
                bool isPickup = pickup != null;
                if (isPickup && pickup.Collected)
                    continue;
                if (pass == 0 && !isPickup)
                    continue;
                float d = (t.position - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    at = t.position;
                    found = true;
                    pickups |= isPickup;
                }
            }
        }
        return found;
    }

    // The card: a small dark panel at the top, GOAL in gold, the step, and for a while after it came up the how.
    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;
        if (shown <= 0f || Current == null)
        {
            CardBottom = 0f;
            return;
        }
        Step step = Current;
        float s = HudStyle.Scale;
        float since = Time.unscaledTime - changedAt;
        float slide = 1f - Ease.OutCubic(Mathf.Clamp01(since / 0.35f));
        float glow = 1f - Mathf.Clamp01(since / 1.6f);
        float full = helpShown;   // the hint line only once the player has been stuck a while
        float alpha = shown * (1f - slide * 0.6f);
        bool withDetail = !string.IsNullOrEmpty(step.detail) && full > 0.01f;

        GUIStyle label = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(11f * s)));
        GUIStyle titleStyle = HudStyle.Label(Mathf.Max(10, Mathf.RoundToInt(19f * s)));
        GUIStyle detailStyle = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(14f * s)));
        float pad = 18f * s;
        float titleWidth = titleStyle.CalcSize(new GUIContent(step.title)).x;
        float detailWidth = withDetail ? detailStyle.CalcSize(new GUIContent(step.detail)).x : 0f;
        float width = Mathf.Max(titleWidth, detailWidth * full, 200f * s) + pad * 2f;
        float height = (56f + 22f * (withDetail ? full : 0f)) * s;
        var card = new Rect((Screen.width - width) * 0.5f, 26f * s - slide * 12f * s, width, height);
        CardBottom = card.yMax;

        HudStyle.Panel(card, 12f * s, alpha * (0.75f + 0.25f * full));
        if (glow > 0f)
            HudStyle.Outline(card, 12f * s, new Color(color.r, color.g, color.b, 0.55f * glow * alpha));
        float y = card.y + 9f * s;
        if (HudStyle.BeginShapes())
        {
            HudStyle.Diamond(new Vector2(card.x + pad + 4f * s, y + label.fontSize * 0.8f), 4f * s, 0.1f, Color.clear, new Color(color.r, color.g, color.b, 0.9f * alpha));
            HudStyle.EndShapes();
        }
        HudStyle.Spaced(new Vector2(card.x + pad + 14f * s, y), "GOAL", label, color, 2.2f * s, alpha);
        HudStyle.Write(new Rect(card.x + pad, card.y + 38f * s - titleStyle.fontSize * 0.8f, width - pad * 2f, titleStyle.fontSize * 1.6f),
            step.title, titleStyle, Color.Lerp(HudStyle.Text, color, 0.5f * glow), alpha);
        if (withDetail)
            HudStyle.Write(new Rect(card.x + pad, card.y + 60f * s - detailStyle.fontSize * 0.8f, width - pad * 2f, detailStyle.fontSize * 1.6f),
                step.detail, detailStyle, HudStyle.Muted, alpha * full);
    }
}
