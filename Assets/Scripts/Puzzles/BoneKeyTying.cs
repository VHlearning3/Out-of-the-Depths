using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// The bone key tying minigame. Once the three fragments are in the seaweed (its Item Socket fills, which calls
// Begin), this opens: the three pieces lie on a board with two joints between them and a strand of seaweed hangs
// from the cursor. Hold the mouse button and wind round a joint, keeping the strand inside the ring: every full turn
// is a wrap, winding back unwinds, too fast and the strand slips a quarter turn, too slow (or outside the ring) and
// the wrap comes loose. With Wraps Per Joint wraps on, pull it tight: drag away from the joint and let go with the
// tension in the green; let go early and a wrap comes loose, pull too hard and the strand snaps. Both joints tied,
// the key pulls together with a flash and a burst of sparks, the reward (the bone key) goes into the inventory and
// On Tied fires (the seaweed swoops). Escape puts it down with the wraps kept; press E on the seaweed to carry on.
// Freezes the player, frees the cursor and captures Escape while it is up, like the inspect view. IMGUI, so it needs
// no canvas. Lives on a child of the seaweed (Knot) with its own trigger collider, so the E prompt reaches it after
// the socket has switched itself off; disabled until the socket fills. Everything is an Inspector field.
public class BoneKeyTying : MonoBehaviour, IInteractable
{
    private enum Phase { Winding, Pulling, Tied }

    [Header("Pieces")]
    [Tooltip("The fragment: its icon is the piece picture when Piece Icons has none for a slot.")]
    [SerializeField] private ItemDefinition fragmentItem;
    [Tooltip("One picture per piece, left to right (the builders render one from each piece model). Empty = the fragment's icon.")]
    [SerializeField] private Sprite[] pieceIcons = new Sprite[3];
    [Tooltip("Given when the key is tied (the bone key); its icon is the finished key.")]
    [SerializeField] private ItemDefinition rewardItem;
    [SerializeField, Min(1)] private int rewardAmount = 1;
    [Tooltip("A pickup prefab (Pickup_Placeholder). The tied key is spawned as one of these and shown in the inspect view, name and all, and E takes it. Empty = straight into the inventory.")]
    [SerializeField] private GameObject keyPickup;

    [Header("Tying")]
    [Tooltip("Full turns round each joint before it can be pulled tight.")]
    [SerializeField, Range(1, 8)] private int wrapsPerJoint = 4;
    [Tooltip("The ring the strand has to stay in while winding: nearest and farthest the cursor may be from the joint, in pixels at 900p.")]
    [SerializeField] private float innerRadius = 40f;
    [SerializeField] private float outerRadius = 110f;
    [Tooltip("Wind slower than this (turns per second, while holding) and the wrap comes loose; faster than Max and the strand slips a quarter turn.")]
    [SerializeField] private float minTurnsPerSecond = 0.25f;
    [SerializeField] private float maxTurnsPerSecond = 2f;
    [Tooltip("How fast a held but stalled wrap comes loose, in turns per second, after a moment of grace.")]
    [SerializeField] private float looseningTurnsPerSecond = 0.2f;
    [SerializeField] private float stallGrace = 0.4f;
    [Tooltip("Pulling tight: how far past the ring the cursor has to go for full tension, in pixels at 900p.")]
    [SerializeField] private float pullDistance = 150f;
    [Tooltip("The green: a narrow band of tension that sits somewhere in this range, picked afresh for every joint. Let go with the tension inside it, after holding it there for Hold Seconds, and the joint holds. Below it a wrap comes loose; above it the strand snaps and two do.")]
    [SerializeField] private Vector2 greenZoneCentre = new Vector2(0.55f, 0.85f);
    [SerializeField, Range(0.04f, 0.4f)] private float greenZoneWidth = 0.12f;
    [SerializeField] private float holdSeconds = 0.7f;
    [SerializeField] private string prompt = "tie the bone key";
    [SerializeField] private string title = "Tie the bone key";
    [SerializeField, TextArea] private string hint = "Hold the mouse button and wind the seaweed round a joint, steadily, inside the ring.";
    [SerializeField, TextArea] private string pullHint = "Now pull it tight: drag away from the joint and let go while the tension is in the green.";

    [Header("Look")]
    [SerializeField] private Color strandColor = new Color(0.3f, 0.64f, 0.26f);
    [SerializeField] private Color boardColor = new Color(0.05f, 0.08f, 0.12f, 0.94f);
    [SerializeField] private Color accent = new Color(0.35f, 0.85f, 0.95f);
    [SerializeField] private Color warnColor = new Color(0.9f, 0.4f, 0.3f);
    [SerializeField] private Color textColor = new Color(0.93f, 0.96f, 1f);
    [SerializeField] private Color backdropCentre = new Color(0f, 0.02f, 0.05f, 0.6f);
    [SerializeField] private Color backdropEdge = new Color(0f, 0.01f, 0.03f, 0.9f);
    [Tooltip("Size of the pieces on the board, in pixels at 900p, and the gap between them (where the joints are).")]
    [SerializeField] private float pieceSize = 150f;
    [SerializeField] private float pieceGap = 140f;
    [Tooltip("Seconds the finish plays before the board goes: the knots tighten and the pieces slam together, a flash, sparks, the key rising in a halo, then everything fades and the key is shown in the inspect view.")]
    [SerializeField] private float finishSeconds = 4.4f;

    [Header("Sound")]
    [Tooltip("A slice of this plays every quarter turn (rope friction), louder on a full wrap.")]
    [SerializeField] private AudioClip windSound;
    [Tooltip("When the strand slips or a wrap comes loose, and when the tied key pulls together.")]
    [SerializeField] private AudioClip slideSound;
    [Tooltip("When a joint holds, and when the key is done.")]
    [SerializeField] private AudioClip tiedSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

    [Header("Events")]
    public UnityEvent onTied = new UnityEvent();

    public string Prompt => prompt;
    public bool IsOpen => open;
    public bool IsTied => done;

    private bool open;
    private bool done;
    private readonly float[] wound = new float[2];    // radians wound round each joint, signed
    private readonly Phase[] phase = new Phase[2];
    private readonly float[] tension = new float[2];  // 0..1+ while pulling
    private readonly Vector2[] zone = { new Vector2(0.6f, 0.72f), new Vector2(0.6f, 0.72f) };   // the green of each joint
    private readonly float[] inGreenFor = new float[2];   // seconds the tension has sat in the green
    private int activeJoint = -1;
    private bool winding;
    private bool wasHeld;
    private float lastAngle;
    private int lastQuarter;
    private float stalledFor;
    private float slipCooldown;
    private float finishedAt = -1f;
    private Vector2 cursor;
    private bool cursorKnown;
    private string notice;
    private float noticeUntil;
    private Vector2[] sparkDirections;
    private float[] sparkSpeeds;
    private Vector2 shakeOffset;

    private SwimController swimmer;
    private PlayerInteractor interactor;
    private PlayerInventory inventory;
    private SlashAttack slash;
    private bool frozenWas, lookWas, attackWas;

    private Texture2D backdropTex;
    private Texture2D boardTex;
    private Texture2D dotTex;
    private GUIStyle titleStyle;
    private GUIStyle hintStyle;
    private GUIStyle noticeStyle;
    private GUIStyle footStyle;
    private GUIStyle barStyle;
    private Texture2D capsuleTex;
    private readonly System.Collections.Generic.Dictionary<int, Texture2D> rings = new System.Collections.Generic.Dictionary<int, Texture2D>();
    private readonly System.Collections.Generic.Dictionary<int, Texture2D> arcs = new System.Collections.Generic.Dictionary<int, Texture2D>();
    private GUIStyle stateStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle boardStyle;

    private const float BoardWidth = 900f;
    private const float BoardHeight = 470f;
    private const float Tau = Mathf.PI * 2f;

    // Called by the socket's On Filled: the pieces are in, so open the board.
    public void Begin()
    {
        enabled = true;
        Open();
    }

    public void Interact(GameObject who)
    {
        if (!done)
            Open();
    }

    private void OnDisable()
    {
        if (open)
            Close();
    }

    // ---- the locks --------------------------------------------------------------------------------------------------

    private void Open()
    {
        if (open || done)
            return;
        open = true;
        winding = false;
        wasHeld = true;   // a press already down when the board opens does not count until it is released
        activeJoint = -1;
        cursorKnown = false;
        notice = null;

        swimmer = FindFirstObjectByType<SwimController>();
        interactor = FindFirstObjectByType<PlayerInteractor>();
        inventory = FindFirstObjectByType<PlayerInventory>();
        slash = FindFirstObjectByType<SlashAttack>();
        if (swimmer != null)
        {
            frozenWas = swimmer.Frozen;
            lookWas = swimmer.LookLocked;
            swimmer.Frozen = true;
            swimmer.LookLocked = true;
        }
        if (interactor != null)
            interactor.Busy = true;
        if (inventory != null)
            inventory.InputBlocked = true;
        if (slash != null)
        {
            attackWas = slash.CanAttack;
            slash.SetCanAttack(false);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        HitMarker.SetReticleVisible(false);
        PauseMenu.CaptureInput(true);
    }

    // Handover = the pickup's inspect view takes over straight after: the cursor is set back once so the pickup sees
    // the game's state, but nothing keeps forcing it while the pickup frees it again.
    private void Close(bool handover = false)
    {
        if (!open)
            return;
        open = false;
        winding = false;
        if (swimmer != null)
        {
            swimmer.Frozen = frozenWas;
            swimmer.LookLocked = lookWas;
        }
        if (interactor != null)
            interactor.Busy = false;
        if (inventory != null)
            inventory.InputBlocked = false;
        if (slash != null)
            slash.SetCanAttack(attackWas);
        HitMarker.SetReticleVisible(true);
        PauseMenu.CaptureInput(false);
        SetGameCursor();
        if (!handover && isActiveAndEnabled)
            StartCoroutine(RestoreCursor());
    }

    // Back to the game's cursor, locked and hidden, unless the pause menu has it; not to whatever was captured, which
    // can itself be a leftover from an earlier board.
    private static void SetGameCursor()
    {
        bool free = PauseMenu.IsOpen;
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    // The editor lets go of the cursor lock on Escape after it has been set back: keep setting it for a moment.
    private IEnumerator RestoreCursor()
    {
        float until = Time.unscaledTime + 0.4f;
        while (Time.unscaledTime < until && !open)
        {
            SetGameCursor();
            yield return null;
        }
    }

    // ---- the game ---------------------------------------------------------------------------------------------------

    private float Scale => Mathf.Max(1f, Screen.height / 900f);

    private Vector2 BoardOrigin()
    {
        float s = Scale;
        return new Vector2((Screen.width / s - BoardWidth) * 0.5f, (Screen.height / s - BoardHeight) * 0.5f) + shakeOffset;
    }

    // Where the pieces sit: three in a row across the board, the joints in the gaps. Pull = 0..1 draws them together.
    private Vector2 PieceCentre(int index, float pull)
    {
        Vector2 origin = BoardOrigin();
        float gap = Mathf.Lerp(pieceGap, 6f, pull);
        float span = pieceSize * 3f + gap * 2f;
        float x = origin.x + (BoardWidth - span) * 0.5f + pieceSize * 0.5f + index * (pieceSize + gap);
        return new Vector2(x, origin.y + BoardHeight * 0.5f - 10f);
    }

    private Vector2 JointCentre(int joint, float pull)
    {
        return (PieceCentre(joint, pull) + PieceCentre(joint + 1, pull)) * 0.5f;
    }

    private int Wraps(int joint) => Mathf.FloorToInt(Mathf.Abs(wound[joint]) / Tau + 0.0001f);

    private void Update()
    {
        if (!open)
            return;
        float dt = Time.unscaledDeltaTime;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !done)
        {
            Close();
            return;
        }
        if (done)
        {
            if (Time.unscaledTime > finishedAt + finishSeconds)
                Complete();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;
        float s = Scale;
        Vector2 raw = mouse.position.ReadValue();
        cursor = new Vector2(raw.x / s, (Screen.height - raw.y) / s);
        cursorKnown = true;
        bool held = mouse.leftButton.isPressed;
        slipCooldown = Mathf.Max(0f, slipCooldown - dt);

        if (!held)
        {
            if (wasHeld && activeJoint >= 0 && phase[activeJoint] == Phase.Pulling)
                ReleasePull(activeJoint);
            winding = false;
            activeJoint = -1;
            wasHeld = false;
            return;
        }
        bool pressedNow = !wasHeld;
        wasHeld = true;

        if (!winding)
        {
            if (!pressedNow)
                return;   // a press that started outside any ring, or before the board opened: wait for a new one
            // Start on the nearest joint that still needs work, if the cursor is in reach.
            int nearest = -1;
            float best = outerRadius + pullDistance;
            for (int j = 0; j < 2; j++)
            {
                if (phase[j] == Phase.Tied)
                    continue;
                float d = Vector2.Distance(cursor, JointCentre(j, 0f));
                if (d < best)
                {
                    best = d;
                    nearest = j;
                }
            }
            if (nearest < 0)
                return;
            activeJoint = nearest;
            winding = true;
            lastAngle = Angle(cursor - JointCentre(nearest, 0f));
            lastQuarter = Mathf.FloorToInt(Mathf.Abs(wound[nearest]) / (Tau * 0.25f));
            stalledFor = 0f;
            tension[nearest] = 0f;
            inGreenFor[nearest] = 0f;
            return;
        }

        int joint = activeJoint;
        Vector2 centre = JointCentre(joint, 0f);
        float radius = Vector2.Distance(cursor, centre);

        if (phase[joint] == Phase.Pulling)
        {
            // Pulling tight: tension from how far past the ring the cursor is. It has to sit in the green for a
            // moment before letting go counts; too far and it snaps.
            tension[joint] = Mathf.Clamp((radius - innerRadius) / Mathf.Max(1f, pullDistance), 0f, 1.3f);
            bool inGreenNow = tension[joint] >= zone[joint].x && tension[joint] <= zone[joint].y;
            inGreenFor[joint] = inGreenNow ? inGreenFor[joint] + dt : 0f;
            if (tension[joint] > zone[joint].y + 0.12f)
            {
                Loosen(joint, 2, "Too hard: the strand snapped.");
                winding = false;
                activeJoint = -1;
            }
            return;
        }

        // Winding: the turn since last frame, its speed, and whether the strand is in the ring.
        float angle = Angle(cursor - centre);
        float delta = Mathf.DeltaAngle(lastAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        lastAngle = angle;
        if (Mathf.Abs(delta) > 2.5f)
            return;   // a jump across the joint, not a turn
        float turnsPerSecond = dt > 0f ? Mathf.Abs(delta) / Tau / dt : 0f;

        if (radius < innerRadius || radius > outerRadius)
        {
            Stall(joint, dt, radius < innerRadius ? "Too close: keep the strand in the ring." : "Too far: keep the strand in the ring.");
            return;
        }
        if (turnsPerSecond > maxTurnsPerSecond)
        {
            if (slipCooldown <= 0f)
            {
                wound[joint] -= Mathf.Sign(wound[joint] == 0f ? delta : wound[joint]) * Tau * 0.25f;
                if (Mathf.Sign(wound[joint]) != Mathf.Sign(delta) && Mathf.Abs(wound[joint]) < 0.01f)
                    wound[joint] = 0f;
                slipCooldown = 0.35f;
                Say("Too fast: the strand slipped.");
                Play(slideSound, volume * 0.7f, Random.Range(1.1f, 1.3f), 0f);
            }
            return;
        }
        if (turnsPerSecond < minTurnsPerSecond)
        {
            Stall(joint, dt, "Too slow: it is coming loose.");
            return;
        }
        stalledFor = 0f;
        wound[joint] += delta;

        int quarter = Mathf.FloorToInt(Mathf.Abs(wound[joint]) / (Tau * 0.25f));
        if (quarter != lastQuarter)
        {
            bool fullWrap = quarter % 4 == 0 && quarter > lastQuarter;
            Play(windSound, fullWrap ? volume : volume * 0.45f, fullWrap ? 1f : Random.Range(0.95f, 1.1f), 0.3f);
            lastQuarter = quarter;
        }

        if (Mathf.Abs(wound[joint]) >= wrapsPerJoint * Tau)
        {
            wound[joint] = Mathf.Sign(wound[joint]) * wrapsPerJoint * Tau;
            phase[joint] = Phase.Pulling;
            tension[joint] = 0f;
            inGreenFor[joint] = 0f;
            float greenMid = Random.Range(greenZoneCentre.x, greenZoneCentre.y);   // the green lands somewhere new each time
            zone[joint] = new Vector2(Mathf.Clamp01(greenMid - greenZoneWidth * 0.5f), Mathf.Clamp01(greenMid + greenZoneWidth * 0.5f));
            winding = false;   // a fresh press starts the pull
            activeJoint = -1;
            Say("Wrapped. Now pull it tight.");
            Play(windSound, volume, 0.9f, 0.4f);
        }
    }

    // Held but not turning enough: after a moment the wrap starts to come loose.
    private void Stall(int joint, float dt, string why)
    {
        stalledFor += dt;
        if (stalledFor < stallGrace || Mathf.Abs(wound[joint]) <= 0f)
            return;
        float loss = looseningTurnsPerSecond * Tau * dt;
        wound[joint] = Mathf.MoveTowards(wound[joint], 0f, loss);
        lastQuarter = Mathf.FloorToInt(Mathf.Abs(wound[joint]) / (Tau * 0.25f));
        Say(why);
    }

    // The button let go while pulling: in the green it holds, early it loosens.
    private void ReleasePull(int joint)
    {
        float t = tension[joint];
        bool inGreen = t >= zone[joint].x && t <= zone[joint].y;
        bool longEnough = inGreenFor[joint] >= holdSeconds;
        tension[joint] = 0f;
        inGreenFor[joint] = 0f;
        if (inGreen && longEnough)
        {
            phase[joint] = Phase.Tied;
            Say(null);
            Play(tiedSound, volume, 1f, 0f);
            if (phase[0] == Phase.Tied && phase[1] == Phase.Tied)
                Finish();
        }
        else if (inGreen)
        {
            Loosen(joint, 1, "Too quick: hold it in the green until the meter fills, then let go.");
        }
        else if (t < zone[joint].x)
        {
            Loosen(joint, 1, "Let go too soon: a wrap came loose.");
        }
        else
        {
            Loosen(joint, 1, "Too hard: a wrap came loose.");
        }
    }

    private void Loosen(int joint, int wraps, string why)
    {
        phase[joint] = Phase.Winding;
        tension[joint] = 0f;
        wound[joint] = Mathf.MoveTowards(wound[joint], 0f, wraps * Tau);
        lastQuarter = Mathf.FloorToInt(Mathf.Abs(wound[joint]) / (Tau * 0.25f));
        Say(why);
        Play(slideSound, volume * 0.8f, Random.Range(0.8f, 0.95f), 0f);
    }

    private void Say(string text)
    {
        notice = text;
        noticeUntil = Time.unscaledTime + 2f;
    }

    private static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x);

    private void Play(AudioClip clip, float level, float pitch, float slice)
    {
        if (clip == null)
            return;
        AudioSource source = SlicedOneShot.Play(clip, Camera.main != null ? Camera.main.transform.position : transform.position, level, pitch, slice, 0.06f, 0f);
        if (source != null)
            source.ignoreListenerPause = true;
    }

    // Both joints hold: the finish plays (the key pulls together, flashes, sparks fly), then Complete.
    private void Finish()
    {
        done = true;
        finishedAt = Time.unscaledTime;
        winding = false;
        activeJoint = -1;
        notice = null;
        var rng = new System.Random(12);
        sparkDirections = new Vector2[48];
        sparkSpeeds = new float[48];
        for (int i = 0; i < sparkDirections.Length; i++)
        {
            float a = (float)rng.NextDouble() * Tau;
            sparkDirections[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f);
            sparkSpeeds[i] = Mathf.Lerp(160f, 380f, (float)rng.NextDouble());
        }
        Play(windSound, volume, 0.8f, 0.6f);
        StartCoroutine(FinishSounds());
    }

    // The creak of the knots tightening, the slam as the pieces meet, the snap of the flash, a second burst.
    private IEnumerator FinishSounds()
    {
        yield return new WaitForSecondsRealtime(0.45f);
        Play(slideSound, volume * 0.9f, 0.85f, 0f);
        yield return new WaitForSecondsRealtime(0.4f);
        Play(tiedSound, volume, 1.05f, 0f);
        yield return new WaitForSecondsRealtime(0.7f);
        Play(tiedSound, volume * 0.6f, 1.25f, 0f);
    }

    // The key is tied: put the board away and hand the key over, shown in the pickup inspect view when there is a
    // pickup prefab to spawn it as (E takes it from there), else straight into the inventory. Then tell the seaweed.
    private void Complete()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();
        GameObject player = inventory != null ? inventory.gameObject : (swimmer != null ? swimmer.gameObject : null);
        bool shown = false;
        if (keyPickup != null && rewardItem != null && player != null)
        {
            Vector3 at = transform.position + Vector3.up * 1.2f;
            GameObject spawned = Instantiate(keyPickup, at, Quaternion.identity);
            spawned.name = "Pickup_" + rewardItem.name;
            PickupItem pickup = spawned.GetComponent<PickupItem>();
            if (pickup != null)
            {
                Close(true);
                pickup.SetItem(rewardItem, rewardAmount);
                pickup.Interact(player);
                shown = true;
            }
            else
            {
                Destroy(spawned);
            }
        }
        if (!shown)
        {
            if (rewardItem != null && inventory != null)
                inventory.Add(rewardItem, rewardAmount);
            Close();
        }
        enabled = false;   // no more prompt on the seaweed
        onTied.Invoke();
    }

    // ---- drawing ----------------------------------------------------------------------------------------------------

    private void OnGUI()
    {
        if (!open)
            return;
        EnsureStyles();
        float s = Scale;
        float width = Screen.width / s;
        float height = Screen.height / s;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

        // The finish, in order: the knots creak tighter and the board trembles (0 - 0.9 s), the pieces slam together
        // and a white flash floods the board (0.9 s), sparks burst twice and the key pops in with a halo of light
        // circling it (1 - 2.6 s), the key rises with its name under it (2.4 s on), and everything fades out for the
        // inspect view (the last 0.7 s).
        float since = done ? Time.unscaledTime - finishedAt : 0f;
        float pull = done ? Mathf.Pow(Mathf.Clamp01(since / 0.9f), 3f) : 0f;
        float tremble = done ? Mathf.Clamp01(since / 0.9f) * (1f - Mathf.Clamp01((since - 0.9f) / 0.3f)) : 0f;
        float flash = done ? Mathf.Clamp01((since - 0.85f) / 0.12f) * (1f - Mathf.Clamp01((since - 0.97f) / 0.9f)) : 0f;
        float keyFade = done ? Mathf.Clamp01((since - 0.95f) / 0.5f) : 0f;
        float keyPop = done ? 1f + 0.45f * Mathf.Sin(Mathf.Clamp01((since - 0.95f) / 0.7f) * Mathf.PI) : 1f;
        float rise = done ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((since - 2.4f) / 1.3f)) * 46f : 0f;
        float fade = done ? 1f - Mathf.Clamp01((since - (finishSeconds - 0.7f)) / 0.7f) : 1f;
        shakeOffset = tremble > 0f ? new Vector2(Mathf.Sin(since * 71f) * 7f * tremble, Mathf.Cos(since * 59f) * 5f * tremble) : Vector2.zero;

        GUI.color = new Color(1f, 1f, 1f, fade);
        GUI.DrawTexture(new Rect(0f, 0f, width, height), backdropTex, ScaleMode.StretchToFill);

        Vector2 origin = BoardOrigin();
        var board = new Rect(origin.x, origin.y, BoardWidth, BoardHeight);
        GUI.Box(board, GUIContent.none, boardStyle);

        // Title and the one line under it, centred; at the bottom the escape note, and a toast for mistakes.
        GUI.Label(new Rect(board.x, board.y + 28f, board.width, 36f), done ? (since > 0.9f ? "Tied!" : "Holding...") : title, titleStyle);
        GUI.Label(new Rect(board.x + 60f, board.y + 66f, board.width - 120f, 24f), done ? (since > 0.9f ? "The bone key is yours." : "") : Instruction(), hintStyle);
        if (!done)
        {
            GUI.Label(new Rect(board.x, board.yMax - 44f, board.width, 22f), "Escape puts it down; the wraps stay.", footStyle);
            if (!string.IsNullOrEmpty(notice) && Time.unscaledTime < noticeUntil)
            {
                float toast = Mathf.Clamp01((noticeUntil - Time.unscaledTime) / 0.4f);
                GUI.color = new Color(1f, 1f, 1f, toast * fade);
                GUI.Label(new Rect(board.x, board.yMax - 92f, board.width, 24f), notice, noticeStyle);
                GUI.color = new Color(1f, 1f, 1f, fade);
            }
        }

        if (Event.current.type != EventType.Repaint)
        {
            GUI.matrix = Matrix4x4.identity;
            return;
        }

        // The pieces (their own pictures), fading into the finished key at the end.
        GUI.color = new Color(1f, 1f, 1f, (1f - keyFade) * fade);
        for (int i = 0; i < 3; i++)
        {
            Vector2 c = PieceCentre(i, pull);
            DrawSprite(new Rect(c.x - pieceSize * 0.5f, c.y - pieceSize * 0.5f, pieceSize, pieceSize), PieceIcon(i), new Color(0.85f, 0.82f, 0.7f));
        }

        if (done)
            DrawFinish(board, since, pull, flash, keyFade, keyPop, rise, fade);

        // Each joint: the ring to wind in (a faint band between two thin circles, brighter on the joint in hand), the
        // wraps as rings round the joint, the turn in progress as an arc, pips for the count, and the tension while
        // pulling.
        for (int j = 0; j < 2; j++)
        {
            Vector2 c = JointCentre(j, pull);
            float turns = Mathf.Abs(wound[j]) / Tau;
            int full = Mathf.FloorToInt(turns + 0.0001f);
            float part = turns - full;
            float shrink = 1f - 0.25f * pull;
            float tight = phase[j] == Phase.Pulling ? tension[j] * 0.3f : 0f;
            bool active = activeJoint == j;

            if (phase[j] == Phase.Winding && !done)
            {
                float bright = active ? 1f : 0.45f;
                DrawRing(c, (innerRadius + outerRadius) * 0.5f, outerRadius - innerRadius, 1f, new Color(accent.r, accent.g, accent.b, 0.07f * bright * fade));
                DrawRing(c, innerRadius, 2f, 1f, new Color(accent.r, accent.g, accent.b, 0.55f * bright * fade));
                DrawRing(c, outerRadius, 2f, 1f, new Color(accent.r, accent.g, accent.b, 0.55f * bright * fade));
            }

            // The wraps vanish the moment the pieces slam together in the finish.
            float ringFade = done ? 1f - Mathf.Clamp01((since - 0.85f) / 0.25f) : 1f;
            if (ringFade <= 0f)
                continue;   // nothing of the joint shows once the pieces have met
            Color rope = new Color(strandColor.r, strandColor.g, strandColor.b, (1f - keyFade) * fade * ringFade);
            for (int k = 0; k < full; k++)
            {
                Color shade = Color.Lerp(rope, Color.white, k * 0.08f);
                shade.a = rope.a;   // lighter, not more opaque: it still fades out with the rest
                DrawRing(c, (30f + k * 7f) * shrink * (1f - tight), 6f, 0.6f, shade);
            }
            if (part > 0.001f && phase[j] == Phase.Winding)
                DrawArc(c, (30f + full * 7f) * shrink, part * Tau * Mathf.Sign(wound[j] == 0f ? 1f : wound[j]), 6f, 0.6f, rope);
            if (phase[j] == Phase.Tied && !done)
            {
                GUI.color = new Color(strandColor.r, strandColor.g, strandColor.b, fade);
                GUI.Label(new Rect(c.x - 80f, c.y + pieceSize * 0.5f + 16f, 160f, 20f), "tied", stateStyle);
            }
            if (phase[j] == Phase.Tied || done)
                continue;

            // Under the joint: pips (one per wrap, filled as they go) and what to do with it. While pulling: a bobbing
            // arrow from the joint saying which way to drag, the tension bar with its green, and the hold meter.
            float pipY = c.y + pieceSize * 0.5f + 22f;
            float pipX = c.x - (wrapsPerJoint - 1) * 10f;
            for (int k = 0; k < wrapsPerJoint; k++)
            {
                bool on = k < full || phase[j] == Phase.Pulling;
                GUI.color = on ? new Color(strandColor.r, strandColor.g, strandColor.b, fade) : new Color(1f, 1f, 1f, 0.16f * fade);
                GUI.DrawTexture(new Rect(pipX + k * 20f - 6f, pipY - 6f, 12f, 12f), dotTex);
            }
            GUI.color = new Color(1f, 1f, 1f, fade);
            string state = phase[j] == Phase.Pulling ? "pull tight" : (full == 0 ? (active ? "wind" : "wind here") : full + " of " + wrapsPerJoint + " wraps");
            GUI.Label(new Rect(c.x - 80f, pipY + 10f, 160f, 20f), state, stateStyle);

            if (phase[j] == Phase.Pulling)
            {
                DrawArrow(c + Vector2.down * innerRadius, Vector2.down, 24f, new Color(1f, 1f, 1f, 0.9f * fade));
                var bar = new Rect(c.x - 75f, pipY + 34f, 150f, 10f);
                GUI.color = new Color(0f, 0f, 0f, 0.55f * fade);
                GUI.Box(bar, GUIContent.none, barStyle);
                GUI.color = new Color(strandColor.r, strandColor.g, strandColor.b, 0.45f * fade);
                GUI.DrawTexture(new Rect(bar.x + bar.width * zone[j].x, bar.y + 1f, bar.width * (zone[j].y - zone[j].x), bar.height - 2f), Texture2D.whiteTexture);
                float t = Mathf.Clamp01(tension[j]);
                bool inGreen = tension[j] >= zone[j].x && tension[j] <= zone[j].y;
                Color fill = tension[j] > zone[j].y ? warnColor : (inGreen ? Color.white : accent);
                GUI.color = new Color(fill.r, fill.g, fill.b, fade);
                if (t > 0.02f)
                    GUI.Box(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * t, bar.height - 2f), GUIContent.none, barStyle);
                // The hold meter under the bar: fills while the tension sits in the green; let go when it is full.
                var hold = new Rect(bar.x, bar.yMax + 4f, bar.width, 4f);
                GUI.color = new Color(1f, 1f, 1f, 0.12f * fade);
                GUI.DrawTexture(hold, Texture2D.whiteTexture);
                float ready = holdSeconds > 0f ? Mathf.Clamp01(inGreenFor[j] / holdSeconds) : 1f;
                GUI.color = ready >= 1f ? new Color(strandColor.r, strandColor.g, strandColor.b, fade) : new Color(1f, 1f, 1f, 0.7f * fade);
                if (ready > 0f)
                    GUI.DrawTexture(new Rect(hold.x, hold.y, hold.width * ready, hold.height), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, fade);
                GUI.Label(new Rect(c.x - 80f, hold.yMax + 2f, 160f, 18f), ready >= 1f ? "let go" : (inGreen ? "hold it there" : "drag down into the green"), stateStyle);
            }
        }

        // The strand: hanging from the cursor, or running from the joint being wound (or pulled) out to the cursor.
        if (cursorKnown && !done)
        {
            if (activeJoint >= 0)
            {
                Vector2 c = JointCentre(activeJoint, 0f);
                float r = 30f + Wraps(activeJoint) * 7f;
                Vector2 from = phase[activeJoint] == Phase.Pulling
                    ? c + (cursor - c).normalized * r * 0.8f
                    : c + new Vector2(Mathf.Cos(lastAngle), Mathf.Sin(lastAngle) * 0.6f) * r;
                DrawStrand(from, cursor, phase[activeJoint] == Phase.Pulling ? 0f : 0.08f);
            }
            else
            {
                DrawHangingStrand(cursor);
            }
            GUI.color = new Color(strandColor.r, strandColor.g, strandColor.b, 0.35f);
            GUI.DrawTexture(new Rect(cursor.x - 16f, cursor.y - 16f, 32f, 32f), dotTex);
            GUI.color = Color.Lerp(strandColor, Color.white, 0.25f);
            GUI.DrawTexture(new Rect(cursor.x - 8f, cursor.y - 8f, 16f, 16f), dotTex);
        }

        GUI.color = Color.white;
        GUI.matrix = Matrix4x4.identity;
    }

    // The flash, the sparks, the key with its halo and its name.
    private void DrawFinish(Rect board, float since, float pull, float flash, float keyFade, float keyPop, float rise, float fade)
    {
        Vector2 middle = PieceCentre(1, pull) + Vector2.down * rise;

        if (flash > 0f)
        {
            GUI.color = new Color(1f, 1f, 1f, flash * 0.85f * fade);
            GUI.DrawTexture(board, Texture2D.whiteTexture);
            GUI.color = new Color(accent.r, accent.g, accent.b, flash * 0.7f * fade);
            float glow = 300f + 200f * (1f - flash);
            GUI.DrawTexture(new Rect(middle.x - glow * 0.5f, middle.y - glow * 0.5f, glow, glow), dotTex);
            Color ring = new Color(accent.r, accent.g, accent.b, flash * fade);
            DrawRing(middle, 80f + (1f - flash) * 420f, 10f, 0.6f, ring);
            DrawRing(middle, 40f + (1f - flash) * 300f, 6f, 0.6f, ring);
        }

        for (int wave = 0; wave < 2; wave++)
        {
            float sparkT = Mathf.Clamp01((since - (wave == 0 ? 0.9f : 1.5f)) / 1.5f);
            if (sparkT <= 0f || sparkT >= 1f || sparkDirections == null)
                continue;
            for (int i = wave; i < sparkDirections.Length; i += 2)
            {
                float travel = sparkSpeeds[i] * sparkT * (1f - sparkT * 0.45f);
                Vector2 p = middle + sparkDirections[i] * travel + Vector2.up * (sparkT * sparkT * 60f);
                float size = Mathf.Lerp(10f, 3f, sparkT);
                Color c = i % 3 == 0 ? Color.white : (i % 3 == 1 ? accent : strandColor);
                GUI.color = new Color(c.r, c.g, c.b, (1f - sparkT) * fade);
                GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), dotTex);
            }
        }

        if (keyFade > 0f && rewardItem != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(since * 4f);
            GUI.color = new Color(accent.r, accent.g, accent.b, keyFade * (0.25f + 0.15f * pulse) * fade);
            float glow = pieceSize * 2.6f;
            GUI.DrawTexture(new Rect(middle.x - glow * 0.5f, middle.y - glow * 0.5f, glow, glow), dotTex);
            for (int i = 0; i < 12; i++)
            {
                float a = since * 1.4f + i * Tau / 12f;
                float r = pieceSize * 0.95f + Mathf.Sin(since * 3f + i) * 8f;
                Vector2 p = middle + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.55f);
                float size = 6f + 4f * Mathf.Sin(since * 5f + i * 0.7f);
                Color c = i % 2 == 0 ? Color.white : accent;
                GUI.color = new Color(c.r, c.g, c.b, keyFade * 0.85f * fade);
                GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), dotTex);
            }
            if (rewardItem.Icon != null)
            {
                Matrix4x4 keep = GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Sin((since - 0.95f) * 1.7f) * 10f, middle);
                GUI.color = new Color(1f, 1f, 1f, keyFade * fade);
                float size = pieceSize * 1.7f * keyPop;
                DrawSprite(new Rect(middle.x - size * 0.5f, middle.y - size * 0.5f, size, size), rewardItem.Icon, Color.white);
                GUI.matrix = keep;
            }
            GUI.color = new Color(accent.r, accent.g, accent.b, Mathf.Clamp01((since - 1.5f) / 0.5f) * fade);
            GUI.Label(new Rect(middle.x - 200f, middle.y + pieceSize * 0.95f, 400f, 30f), rewardItem.DisplayName, subtitleStyle);
        }
    }

    // The one line under the title: what to do right now.
    private string Instruction()
    {
        if (phase[0] == Phase.Pulling || phase[1] == Phase.Pulling)
            return pullHint;
        int tiedCount = (phase[0] == Phase.Tied ? 1 : 0) + (phase[1] == Phase.Tied ? 1 : 0);
        if (tiedCount == 1)
            return "One joint holds. Wind the other one the same way.";
        return hint;
    }

    private Sprite PieceIcon(int index)
    {
        if (pieceIcons != null && index < pieceIcons.Length && pieceIcons[index] != null)
            return pieceIcons[index];
        return fragmentItem != null ? fragmentItem.Icon : null;
    }

    // A smooth ring: an anti-aliased annulus texture (one per thickness-to-radius ratio, cached) stretched to size;
    // squash < 1 flattens it into an ellipse, so it reads as wrapping round a bar.
    private void DrawRing(Vector2 centre, float radius, float thickness, float squash, Color colour)
    {
        if (radius <= 0.5f)
            return;
        float outer = radius + thickness * 0.5f;
        float inner = Mathf.Max(0f, radius - thickness * 0.5f);
        Texture2D tex = RingTexture(inner / outer);
        Color previous = GUI.color;
        GUI.color = colour;
        GUI.DrawTexture(new Rect(centre.x - outer, centre.y - outer * squash, outer * 2f, outer * 2f * squash), tex);
        GUI.color = previous;
    }

    // Part of a ring, from angle 0 round by sweep (either way): an anti-aliased sector texture stretched like the
    // ring, with round caps on both ends.
    private void DrawArc(Vector2 centre, float radius, float sweep, float thickness, float squash, Color colour)
    {
        if (radius <= 0.5f || Mathf.Abs(sweep) < 0.01f)
            return;
        float outer = radius + thickness * 0.5f;
        float inner = Mathf.Max(0f, radius - thickness * 0.5f);
        Texture2D tex = ArcTexture(inner / outer, sweep);
        Color previous = GUI.color;
        GUI.color = colour;
        GUI.DrawTexture(new Rect(centre.x - outer, centre.y - outer * squash, outer * 2f, outer * 2f * squash), tex);
        foreach (float a in new[] { 0f, sweep })
        {
            float x = centre.x + Mathf.Cos(a) * radius;
            float y = centre.y + Mathf.Sin(a) * radius * squash;
            GUI.DrawTexture(new Rect(x - thickness * 0.5f, y - thickness * 0.5f, thickness, thickness), dotTex);
        }
        GUI.color = previous;
    }

    // A bobbing arrow from a point in a direction: the cue to pull.
    private void DrawArrow(Vector2 from, Vector2 direction, float length, Color colour)
    {
        float bob = Mathf.Sin(Time.unscaledTime * 5f) * 6f + 6f;
        Vector2 dir = direction.normalized;
        Vector2 start = from + dir * bob;
        Vector2 tip = start + dir * length;
        Vector2 side = new Vector2(-dir.y, dir.x);
        DrawSegment(start, tip, 5f, colour);
        DrawSegment(tip, tip - dir * 12f + side * 10f, 5f, colour);
        DrawSegment(tip, tip - dir * 12f - side * 10f, 5f, colour);
    }

    // A rope: capsule segments laid along a curve that sags a little, with a soft glow under them.
    private void DrawStrand(Vector2 from, Vector2 to, float sag)
    {
        float length = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / 14f));
        Vector2 previousPoint = from;
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 p = Vector2.Lerp(from, to, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * length * sag);
            DrawSegment(previousPoint, p, Mathf.Lerp(8f, 6f, t), strandColor);
            previousPoint = p;
        }
    }

    // A loose end dangling from the cursor, swaying a little.
    private void DrawHangingStrand(Vector2 at)
    {
        float sway = Mathf.Sin(Time.unscaledTime * 2.2f) * 10f;
        const int steps = 8;
        Vector2 previousPoint = at;
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 p = at + new Vector2(Mathf.Sin(t * Mathf.PI) * sway - t * 18f, t * 80f);
            DrawSegment(previousPoint, p, Mathf.Lerp(8f, 4f, t), strandColor);
            previousPoint = p;
        }
    }

    // One capsule from a to b: a glow underneath, the rope on top, rotated into place.
    private void DrawSegment(Vector2 a, Vector2 b, float thickness, Color colour)
    {
        Vector2 d = b - a;
        float length = d.magnitude;
        if (length < 0.5f)
            return;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        Matrix4x4 keep = GUI.matrix;
        Color previous = GUI.color;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.color = new Color(colour.r, colour.g, colour.b, 0.25f * previous.a);
        GUI.DrawTexture(new Rect(a.x - thickness, a.y - thickness * 1.5f, length + thickness * 2f, thickness * 3f), capsuleTex);
        GUI.color = new Color(colour.r, colour.g, colour.b, previous.a);
        GUI.DrawTexture(new Rect(a.x - thickness * 0.5f, a.y - thickness * 0.5f, length + thickness, thickness), capsuleTex);
        GUI.color = previous;
        GUI.matrix = keep;
    }

    private static void DrawSprite(Rect rect, Sprite sprite, Color fallback)
    {
        if (sprite == null || sprite.texture == null)
        {
            Color previous = GUI.color;
            GUI.color = new Color(fallback.r, fallback.g, fallback.b, previous.a);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.2f, rect.y + rect.height * 0.35f, rect.width * 0.6f, rect.height * 0.3f), Texture2D.whiteTexture);
            GUI.color = previous;
            return;
        }
        Rect tex = sprite.textureRect;
        var coords = new Rect(tex.x / sprite.texture.width, tex.y / sprite.texture.height, tex.width / sprite.texture.width, tex.height / sprite.texture.height);
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, coords, true);
    }

    // ---- styles and textures ----------------------------------------------------------------------------------------

    private void EnsureStyles()
    {
        if (boardTex != null)
            return;
        backdropTex = Vignette(backdropCentre, backdropEdge);
        boardTex = Rounded(96, 24f, boardColor, new Color(1f, 1f, 1f, 0.08f), 1.5f);
        dotTex = Dot(32);
        capsuleTex = Rounded(32, 16f, Color.white, Color.clear, 0f);
        boardStyle = new GUIStyle { border = new RectOffset(40, 40, 40, 40) };
        boardStyle.normal.background = boardTex;
        barStyle = new GUIStyle { border = new RectOffset(8, 8, 8, 8) };
        barStyle.normal.background = Rounded(20, 5f, Color.white, Color.clear, 0f);
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        titleStyle.normal.textColor = Color.white;
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        hintStyle.normal.textColor = new Color(0.6f, 0.68f, 0.76f);
        footStyle = new GUIStyle(hintStyle) { fontSize = 13 };
        footStyle.normal.textColor = new Color(0.45f, 0.53f, 0.6f);
        noticeStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        noticeStyle.normal.textColor = warnColor;
        subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        subtitleStyle.normal.textColor = Color.white;
        stateStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        stateStyle.normal.textColor = new Color(0.75f, 0.82f, 0.9f);

        // The same font as the rest of the game.
        Font font = GameFont.Font;
        foreach (GUIStyle style in new[] { titleStyle, hintStyle, footStyle, noticeStyle, subtitleStyle, stateStyle })
            style.font = font;
    }

    // A ring sector from angle 0 round by sweep (radians, either way), white, anti-aliased, kept per shape and sweep.
    private Texture2D ArcTexture(float innerRatio, float sweep)
    {
        int ratioKey = Mathf.RoundToInt(Mathf.Clamp01(innerRatio) * 50f);
        int sweepKey = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(sweep) * Mathf.Rad2Deg / 5f), 1, 72) * (sweep < 0f ? -1 : 1);
        int key = ratioKey * 1000 + sweepKey + 500;
        if (arcs.TryGetValue(key, out Texture2D cached) && cached != null)
            return cached;
        const int size = 128;
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        float inner = ratioKey / 50f * (half - 1f);
        float outer = half - 1f;
        float span = Mathf.Abs(sweepKey) * 5f * Mathf.Deg2Rad;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = half - (y + 0.5f);   // texture rows run bottom up; on screen y runs down
                if (sweepKey < 0)
                    py = -py;
                float d = new Vector2(px, py).magnitude;
                float band = Mathf.Clamp01(Mathf.Min(d - inner, outer - d) + 0.5f);
                float angle = Mathf.Atan2(py, px);
                if (angle < 0f)
                    angle += Tau;
                float along = Mathf.Clamp01((span - angle) * Mathf.Max(d, 1f) + 0.5f);   // soft edge where the arc ends
                pixels[y * size + x] = new Color(1f, 1f, 1f, band * along);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        arcs[key] = tex;
        return tex;
    }

    // An annulus, white, anti-aliased, from innerRatio of the half size out to the edge; one per ratio, kept.
    private Texture2D RingTexture(float innerRatio)
    {
        int key = Mathf.RoundToInt(Mathf.Clamp01(innerRatio) * 50f);
        if (rings.TryGetValue(key, out Texture2D cached) && cached != null)
            return cached;
        const int size = 256;
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        float inner = key / 50f * (half - 1f);
        float outer = half - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                float a = Mathf.Clamp01(Mathf.Min(d - inner, outer - d) + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        rings[key] = tex;
        return tex;
    }

    private static Texture2D NewTexture(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
    }

    private static Texture2D Vignette(Color centre, Color edge)
    {
        const int size = 128;
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v) / 1.25f);
                pixels[y * size + x] = Color.Lerp(centre, edge, Mathf.SmoothStep(0f, 1f, d));
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D Rounded(int size, float radius, Color fill, Color rim, float rimWidth)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - half + radius;
                float qy = Mathf.Abs(y + 0.5f - half) - half + radius;
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float d = Mathf.Min(Mathf.Max(qx, qy), 0f) + outside - radius;
                float fillA = Mathf.Clamp01(0.5f - d);
                float rimA = rimWidth > 0f ? Mathf.Clamp01(rimWidth * 0.5f - Mathf.Abs(d + rimWidth * 0.5f) + 0.5f) : 0f;
                Color c = Color.Lerp(fill, new Color(rim.r, rim.g, rim.b, 1f), rimA * rim.a);
                c.a = Mathf.Max(fill.a * fillA, rim.a * rimA);
                pixels[y * size + x] = c;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D Dot(int size)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / (half - 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 2.5f));
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
