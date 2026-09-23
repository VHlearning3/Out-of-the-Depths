using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// The bone key tying minigame. Once the three fragments are in the seaweed (its Item Socket fills, which calls
// Begin), this opens: the three pieces lie on a board with two joints between them and a strand of seaweed hangs
// from the cursor. Two steps per joint, the next one always glowing:
//   1. Wrap: hold the mouse button and move in circles round the joint, at any speed, either way. Every full circle
//      is a wrap (Turns Per Joint of them); a faint dot circles the joint to show the motion while the button is up.
//      Finished wraps never come undone: letting go, stopping or going back only undoes the circle in progress.
//   2. Pull tight: keep holding (or press again) and drag away from the joint until the bar is in the green; hold it
//      there for a moment and the joint ties by itself. Too hard just waits until you ease off; nothing comes loose.
// Both joints tied, the key pulls together with a flash and a burst of sparks, the reward (the bone key) is shown in
// the inspect view (or goes straight into the inventory) and On Tied fires (the seaweed swoops). Escape puts it down
// with the wraps kept; press E on the seaweed to carry on.
// Freezes the player, hides the system cursor (the strand's end is the cursor) and captures Escape while it is up,
// like the inspect view. IMGUI, so it needs no canvas; every texture is made once and kept, nothing is built while
// playing. Lives on a child of the seaweed (Knot) with its own trigger collider, so the E prompt reaches it after the
// socket has switched itself off; disabled until the socket fills. Everything is an Inspector field.
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
    [Tooltip("Full circles round each joint to wrap it. Finished wraps never come undone.")]
    [SerializeField, Range(1, 6)] private int turnsPerJoint = 3;
    [Tooltip("Circling closer to the joint than this (pixels at 900p) is not counted: the angle jumps about there.")]
    [SerializeField] private float deadZone = 14f;
    [Tooltip("Pulling tight: how far to drag away from the joint for full tension, in pixels at 900p.")]
    [SerializeField] private float pullLength = 140f;
    [Tooltip("The green: the tension to hold, 0..1 of Pull Length. Past it the strand is pulled too hard and the tie waits until you ease off; nothing comes undone.")]
    [SerializeField] private Vector2 sweetSpot = new Vector2(0.45f, 0.9f);
    [Tooltip("Seconds the tension has to sit in the green; then the joint ties by itself.")]
    [SerializeField] private float holdToTie = 0.4f;
    [Tooltip("A faint dot circles the next joint to show the motion while the mouse button is up.")]
    [SerializeField] private bool showGhost = true;

    [Header("Text")]
    [SerializeField] private string prompt = "tie the bone key";
    [SerializeField] private string title = "Tie the bone key";
    [SerializeField, TextArea] private string windHint = "Hold the left mouse button and move in circles round the glowing joint to wrap the seaweed.";
    [SerializeField, TextArea] private string pullTightHint = "Wrapped! Keep holding and drag away from the joint until the bar sits in the green.";
    [SerializeField, TextArea] private string otherJointHint = "One joint holds. Now do the same with the other one.";

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
    [Tooltip("Seconds the finish plays before the board goes: the pieces glide together, a soft flash and a few sparks, the key settles in with its name, then the board fades and the key is shown in the inspect view.")]
    [SerializeField, Min(1.6f)] private float finishDuration = 3f;

    [Header("Sound")]
    [Tooltip("A slice of this plays every quarter circle (rope friction), louder on a full wrap.")]
    [SerializeField] private AudioClip windSound;
    [Tooltip("When the strand is pulled too hard, and when the tied key pulls together.")]
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
    private readonly float[] wound = new float[2];        // radians wound round each joint, signed (either way works)
    private readonly Phase[] phase = new Phase[2];
    private readonly float[] tension = new float[2];      // 0..1.4 while pulling
    private readonly float[] inGreenFor = new float[2];   // seconds the tension has sat in the green
    private readonly float[] wrappedAt = { -10f, -10f };  // when the last wrap finished (the track flashes)
    private readonly float[] tiedAt = { -10f, -10f };     // when the joint tied (a ring pops)
    private int activeJoint = -1;
    private bool wasHeld;
    private float lastAngle;
    private int lastQuarter;
    private float pullFrom;
    private bool tooHardSaid;
    private float finishedAt = -1f;
    private Vector2 cursor;
    private bool cursorKnown;
    private Vector2[] sparkDirections;
    private float[] sparkSpeeds;

    private SwimController swimmer;
    private PlayerInteractor interactor;
    private PlayerInventory inventory;
    private SlashAttack slash;
    private bool frozenWas, lookWas, attackWas;

    private Texture2D backdropTex;
    private Texture2D boardTex;
    private Texture2D dotTex;
    private Texture2D capsuleTex;
    private Texture2D barTex;
    private readonly Dictionary<int, Texture2D> rings = new Dictionary<int, Texture2D>();
    private GUIStyle titleStyle;
    private GUIStyle hintStyle;
    private GUIStyle footRightStyle;
    private GUIStyle footStyle;
    private GUIStyle barStyle;
    private GUIStyle stateStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle boardStyle;

    // The layout, in board units (900p): the board, the rule under the header, the row of pieces, the status line
    // under the joints and the rule over the footer (from the bottom).
    private const float BoardWidth = 960f;
    private const float BoardHeight = 500f;
    private const float RuleTop = 114f;
    private const float PlayCentre = 262f;
    private const float StatusTop = 352f;
    private const float RuleBottom = 54f;
    private const float MeetAt = 0.6f;    // the finish: when the pieces meet
    private const float FadeOut = 0.5f;   // the finish: the fade at the end
    private const float Tau = Mathf.PI * 2f;
    private const float Top = -Mathf.PI * 0.5f;   // circles are drawn from the top of the joint
    private const float BarRange = 1.25f;          // the tension bar runs 0..this, so pulling too hard shows

    private float TrackRadius => pieceGap * 0.45f;

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

    private void OnDestroy()
    {
        foreach (Texture2D tex in new[] { backdropTex, boardTex, dotTex, capsuleTex, barTex })
            if (tex != null)
                Destroy(tex);
        foreach (Texture2D tex in rings.Values)
            if (tex != null)
                Destroy(tex);
        rings.Clear();
    }

    // ---- the locks --------------------------------------------------------------------------------------------------

    private void Open()
    {
        if (open || done)
            return;
        open = true;
        wasHeld = true;   // a press already down when the board opens does not count until it is released
        activeJoint = -1;
        cursorKnown = false;

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
        // Free but hidden: the strand's end is drawn where the mouse is, so the system cursor would only trail it.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
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
        activeJoint = -1;
        for (int j = 0; j < 2; j++)
        {
            tension[j] = 0f;   // a pull in progress starts over next time; the wraps stay
            inGreenFor[j] = 0f;
        }
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

    // Board units to screen pixels: the board is laid out for 900p and scaled to fit, smaller screens included.
    private float Scale => Mathf.Clamp(Mathf.Min(Screen.height / 900f, Screen.width / 1100f), 0.5f, 4f);

    private Vector2 BoardOrigin()
    {
        float s = Scale;
        return new Vector2((Screen.width / s - BoardWidth) * 0.5f, (Screen.height / s - BoardHeight) * 0.5f);
    }

    // Where the pieces sit: three in a row across the board, the joints in the gaps. Pull = 0..1 draws them together.
    private Vector2 PieceCentre(int index, float pull)
    {
        Vector2 origin = BoardOrigin();
        float gap = Mathf.Lerp(pieceGap, 6f, pull);
        float span = pieceSize * 3f + gap * 2f;
        float x = origin.x + (BoardWidth - span) * 0.5f + pieceSize * 0.5f + index * (pieceSize + gap);
        return new Vector2(x, origin.y + PlayCentre);
    }

    private Vector2 JointCentre(int joint, float pull)
    {
        return (PieceCentre(joint, pull) + PieceCentre(joint + 1, pull)) * 0.5f;
    }

    private int Wraps(int joint) => Mathf.Min(turnsPerJoint, Mathf.FloorToInt(Mathf.Abs(wound[joint]) / Tau + 0.0001f));

    // The joint to work on next: the one in hand, else the untied one furthest along (the left one to begin with).
    private int NextJoint()
    {
        if (activeJoint >= 0)
            return activeJoint;
        int best = -1;
        for (int j = 0; j < 2; j++)
        {
            if (phase[j] == Phase.Tied)
                continue;
            if (best < 0 || Progress(j) > Progress(best) + 0.001f)
                best = j;
        }
        return best;
    }

    private float Progress(int joint) => phase[joint] == Phase.Pulling ? turnsPerJoint + 1f : Mathf.Abs(wound[joint]) / Tau;

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
        if (Cursor.visible)
            Cursor.visible = false;   // the editor shows it again when the Game view regains focus
        if (done)
        {
            if (Time.unscaledTime > finishedAt + finishDuration)
                Complete();
            return;
        }

        // Where the mouse is comes from OnGUI (the same coordinates the board is drawn in); the button from here.
        Mouse mouse = Mouse.current;
        if (mouse == null || !cursorKnown)
            return;
        bool held = mouse.leftButton.isPressed;

        if (!held)
        {
            // Let go: the circle in progress and any pull start over; finished wraps stay.
            if (activeJoint >= 0)
            {
                tension[activeJoint] = 0f;
                inGreenFor[activeJoint] = 0f;
            }
            activeJoint = -1;
            wasHeld = false;
            return;
        }
        bool pressedNow = !wasHeld;
        wasHeld = true;

        if (activeJoint < 0)
        {
            if (!pressedNow)
                return;   // held since the board opened, or since the last joint tied: wait for a new press
            // A press anywhere takes the nearest joint that still needs work.
            float best = float.MaxValue;
            for (int j = 0; j < 2; j++)
            {
                if (phase[j] == Phase.Tied)
                    continue;
                float d = Vector2.Distance(cursor, JointCentre(j, 0f));
                if (d < best)
                {
                    best = d;
                    activeJoint = j;
                }
            }
            if (activeJoint < 0)
                return;
            Vector2 c = JointCentre(activeJoint, 0f);
            lastAngle = Angle(cursor - c);
            lastQuarter = Mathf.FloorToInt(Mathf.Abs(wound[activeJoint]) / (Tau * 0.25f));
            pullFrom = Mathf.Max(best, CoilRadius(Wraps(activeJoint), 0f, 0f));
            tooHardSaid = false;
            return;
        }

        int joint = activeJoint;
        Vector2 centre = JointCentre(joint, 0f);
        float radius = Vector2.Distance(cursor, centre);
        if (phase[joint] == Phase.Pulling)
            Pull(joint, radius, dt);
        else
            Wind(joint, centre, radius);
    }

    // Circling: the turn since last frame counts, at any speed and either way. Going back only undoes the circle in
    // progress, never a finished wrap.
    private void Wind(int joint, Vector2 centre, float radius)
    {
        float angle = Angle(cursor - centre);
        float delta = Mathf.DeltaAngle(lastAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        lastAngle = angle;
        if (radius < deadZone || Mathf.Abs(delta) > 2.5f)
            return;   // right on the joint, or a jump across it: not a turn

        int before = Wraps(joint);
        float next = wound[joint] + delta;
        if (before > 0)
        {
            float direction = Mathf.Sign(wound[joint]);
            if (next * direction < before * Tau)
                next = direction * before * Tau;
        }
        wound[joint] = next;

        int quarter = Mathf.FloorToInt(Mathf.Abs(wound[joint]) / (Tau * 0.25f));
        if (quarter > lastQuarter)
        {
            bool fullWrap = quarter % 4 == 0;
            Play(windSound, fullWrap ? volume : volume * 0.4f, fullWrap ? 1f : Random.Range(0.95f, 1.1f), 0.3f);
        }
        lastQuarter = quarter;
        if (Wraps(joint) > before)
            wrappedAt[joint] = Time.unscaledTime;

        if (Mathf.Abs(wound[joint]) >= turnsPerJoint * Tau)
        {
            // Wrapped: straight on to pulling, with the button still down. Tension counts from here outwards.
            wound[joint] = Mathf.Sign(wound[joint]) * turnsPerJoint * Tau;
            phase[joint] = Phase.Pulling;
            tension[joint] = 0f;
            inGreenFor[joint] = 0f;
            pullFrom = Mathf.Max(radius, CoilRadius(turnsPerJoint, 0f, 0f));
            tooHardSaid = false;
            Play(windSound, volume, 0.9f, 0.4f);
        }
    }

    // Pulling tight: tension from how far the cursor has been dragged away from the joint. In the green for Hold To
    // Tie seconds and it ties; too hard and the meter waits (and drains) until you ease off.
    private void Pull(int joint, float radius, float dt)
    {
        tension[joint] = Mathf.Clamp((radius - pullFrom) / Mathf.Max(1f, pullLength), 0f, BarRange + 0.15f);
        float t = tension[joint];
        if (t >= sweetSpot.x && t <= sweetSpot.y)
        {
            inGreenFor[joint] += dt;
            tooHardSaid = false;
        }
        else
        {
            inGreenFor[joint] = Mathf.MoveTowards(inGreenFor[joint], 0f, dt * 1.5f);
            if (t > sweetSpot.y && !tooHardSaid)
            {
                tooHardSaid = true;
                Play(slideSound, volume * 0.5f, Random.Range(1.1f, 1.25f), 0f);
            }
        }
        if (inGreenFor[joint] >= holdToTie)
            Tie(joint);
    }

    private void Tie(int joint)
    {
        phase[joint] = Phase.Tied;
        tension[joint] = 0f;
        inGreenFor[joint] = 0f;
        tiedAt[joint] = Time.unscaledTime;
        activeJoint = -1;   // the next joint takes a fresh press
        Play(tiedSound, volume, 1f, 0f);
        if (phase[0] == Phase.Tied && phase[1] == Phase.Tied)
            Finish();
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

    // Both joints hold: the finish plays (the pieces glide together, a soft flash, the key), then Complete.
    private void Finish()
    {
        done = true;
        finishedAt = Time.unscaledTime;
        activeJoint = -1;
        var rng = new System.Random(12);
        sparkDirections = new Vector2[20];
        sparkSpeeds = new float[20];
        for (int i = 0; i < sparkDirections.Length; i++)
        {
            float a = (i + (float)rng.NextDouble() * 0.6f) / sparkDirections.Length * Tau;
            sparkDirections[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.75f);
            sparkSpeeds[i] = Mathf.Lerp(130f, 230f, (float)rng.NextDouble());
        }
        Play(windSound, volume * 0.8f, 0.85f, 0.5f);
        StartCoroutine(FinishSounds());
    }

    // The creak of the knots tightening (from Finish), the knock as the pieces meet, a lighter one as the key settles.
    private IEnumerator FinishSounds()
    {
        yield return new WaitForSecondsRealtime(MeetAt - 0.05f);
        Play(tiedSound, volume, 1f, 0f);
        yield return new WaitForSecondsRealtime(0.4f);
        Play(tiedSound, volume * 0.45f, 1.3f, 0f);
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
    //
    // The board, top to bottom: the title and what to do right now; a thin rule; the pieces in a row with the joints
    // between them (the joint to work on shows its track, the other a quiet marker); under each joint, always in the
    // same place, one line saying where it is and a row of pips (or the tension bar while pulling); a thin rule; the
    // Escape note on the left and the joints tied on the right.

    private void OnGUI()
    {
        if (!open)
            return;
        EnsureStyles();
        float s = Scale;
        // The cursor in board units, straight from the GUI event: the Input System's mouse position does not match
        // the drawing when the Game view is scaled (a fixed resolution shown smaller).
        GUI.matrix = Matrix4x4.identity;
        if (!done)
        {
            cursor = Event.current.mousePosition / s;
            cursorKnown = true;
        }
        float width = Screen.width / s;
        float height = Screen.height / s;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

        float since = done ? Time.unscaledTime - finishedAt : 0f;
        float fade = done ? 1f - Mathf.Clamp01((since - (finishDuration - FadeOut)) / FadeOut) : 1f;

        GUI.color = Tint(Color.white, fade);
        GUI.DrawTexture(new Rect(0f, 0f, width, height), backdropTex, ScaleMode.StretchToFill);
        Vector2 origin = BoardOrigin();
        var board = new Rect(origin.x, origin.y, BoardWidth, BoardHeight);
        GUI.Box(board, GUIContent.none, boardStyle);

        bool met = done && since >= MeetAt;
        GUI.Label(new Rect(board.x, board.y + 24f, board.width, 36f), met ? "Bone key tied" : title, titleStyle);
        if (!done)
            GUI.Label(new Rect(board.x + 70f, board.y + 62f, board.width - 140f, 42f), Instruction(), hintStyle);

        if (Event.current.type != EventType.Repaint)
        {
            GUI.matrix = Matrix4x4.identity;
            return;
        }

        GUI.color = Tint(Color.white, 0.07f * fade);
        GUI.DrawTexture(new Rect(board.x + 40f, board.y + RuleTop, board.width - 80f, 1f), Texture2D.whiteTexture);
        if (done)
        {
            DrawFinish(since, fade);
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.identity;
            return;
        }
        GUI.DrawTexture(new Rect(board.x + 40f, board.yMax - RuleBottom, board.width - 80f, 1f), Texture2D.whiteTexture);
        int tiedCount = (phase[0] == Phase.Tied ? 1 : 0) + (phase[1] == Phase.Tied ? 1 : 0);
        GUI.color = Color.white;
        GUI.Label(new Rect(board.x + 44f, board.yMax - 40f, 400f, 24f), "Esc  puts it down, the wraps stay", footStyle);
        GUI.Label(new Rect(board.xMax - 444f, board.yMax - 40f, 400f, 24f), tiedCount + " / 2 joints tied", footRightStyle);

        GUI.color = Color.white;
        for (int i = 0; i < 3; i++)
        {
            Vector2 c = PieceCentre(i, 0f);
            DrawSprite(new Rect(c.x - pieceSize * 0.5f, c.y - pieceSize * 0.5f, pieceSize, pieceSize), PieceIcon(i), new Color(0.85f, 0.82f, 0.7f));
        }

        int next = NextJoint();
        for (int j = 0; j < 2; j++)
            DrawJoint(j, j == next);

        // The strand: hanging from the cursor, or running from the joint being wound (or pulled) out to the cursor;
        // its end is the cursor.
        if (cursorKnown)
        {
            Color rope = strandColor;
            if (activeJoint >= 0)
            {
                int a = activeJoint;
                Vector2 c = JointCentre(a, 0f);
                float r = CoilRadius(Wraps(a), 0f, phase[a] == Phase.Pulling ? tension[a] * 0.3f : 0f);
                if (phase[a] == Phase.Pulling)
                {
                    float t = tension[a];
                    rope = t > sweetSpot.y ? warnColor : (t >= sweetSpot.x ? Color.Lerp(strandColor, Color.white, 0.35f) : strandColor);
                    DrawStrand(c + (cursor - c).normalized * r * 0.8f, cursor, 0f, rope);
                }
                else
                {
                    DrawStrand(c + new Vector2(Mathf.Cos(lastAngle), Mathf.Sin(lastAngle) * 0.6f) * r, cursor, 0.08f, rope);
                }
            }
            else
            {
                DrawHangingStrand(cursor);
            }
            GUI.color = Tint(rope, 0.3f);
            GUI.DrawTexture(new Rect(cursor.x - 16f, cursor.y - 16f, 32f, 32f), dotTex);
            GUI.color = Color.Lerp(rope, Color.white, 0.3f);
            GUI.DrawTexture(new Rect(cursor.x - 7f, cursor.y - 7f, 14f, 14f), dotTex);
        }

        GUI.color = Color.white;
        GUI.matrix = Matrix4x4.identity;
    }

    // The rope coiled round a joint: wrap k is an ellipse this big (smaller as it is pulled tight or the pieces meet).
    private static float CoilRadius(int wrap, float pull, float tight) => (30f + wrap * 7f) * (1f - 0.25f * pull) * (1f - tight);

    private void DrawCoils(Vector2 c, int wraps, float pull, float tight, float alpha)
    {
        for (int k = 0; k < wraps; k++)
        {
            Color shade = Color.Lerp(strandColor, Color.white, k * 0.08f);
            DrawRing(c, CoilRadius(k, pull, tight), 6f, 0.6f, Tint(shade, alpha));
        }
    }

    // One joint: the track to circle on (only on the joint to work on, with a guide dot going round it), the circle in
    // progress along it, the wraps as coils, and its status under it.
    private void DrawJoint(int j, bool next)
    {
        Vector2 c = JointCentre(j, 0f);
        float now = Time.unscaledTime;
        bool active = activeJoint == j;
        int full = Wraps(j);
        float turns = Mathf.Abs(wound[j]) / Tau;
        float part = phase[j] == Phase.Winding ? Mathf.Clamp01(turns - full) : 0f;
        float direction = wound[j] < 0f ? -1f : 1f;

        if (phase[j] == Phase.Winding)
        {
            if (next || active)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 3.5f);
                float strength = active ? 1f : 0.6f + 0.3f * pulse;
                DrawRing(c, TrackRadius, 14f, 1f, Tint(accent, 0.08f * strength));
                DrawRing(c, TrackRadius, 2.5f, 1f, Tint(accent, 0.55f * strength));
                if (part > 0.002f)
                    DrawArcDots(c, TrackRadius, Top, part * Tau * direction, 8f, Color.Lerp(strandColor, Color.white, 0.25f));
                if (showGhost && !active && full == 0 && part < 0.05f)
                    DrawGhost(c, now);
            }
            else if (full == 0)
            {
                DrawRing(c, 10f, 2f, 1f, Tint(Color.white, 0.22f));   // a quiet marker until it is this joint's turn
            }
        }

        // A finished wrap flashes the track; a tied joint pops a ring.
        float wrapFlash = 1f - Mathf.Clamp01((now - wrappedAt[j]) / 0.45f);
        if (wrapFlash > 0f)
            DrawRing(c, TrackRadius + (1f - wrapFlash) * 22f, 4f, 1f, Tint(Color.white, wrapFlash * 0.7f));
        float pop = 1f - Mathf.Clamp01((now - tiedAt[j]) / 0.6f);
        if (pop > 0f)
            DrawRing(c, 40f + (1f - pop) * 80f, 5f, 1f, Tint(Color.Lerp(strandColor, Color.white, 0.4f), pop));

        float tight = phase[j] == Phase.Pulling ? tension[j] * 0.3f : (phase[j] == Phase.Tied ? 0.2f : 0f);
        DrawCoils(c, full, 0f, tight, 1f);
        if (phase[j] == Phase.Pulling && !active)
            DrawArrow(c + Vector2.down * CoilRadius(full, 0f, 0f) * 0.6f, Vector2.down, 24f, new Color(1f, 1f, 1f, 0.85f));

        // The status: one line, then pips or the tension bar, in the same place under both joints.
        float lineY = BoardOrigin().y + StatusTop;
        float rowY = lineY + 28f;
        Color light = Color.Lerp(strandColor, Color.white, 0.35f);
        string label;
        Color labelColour = new Color(1f, 1f, 1f, 0.85f);
        if (phase[j] == Phase.Tied)
        {
            label = "tied";
            labelColour = light;
        }
        else if (phase[j] == Phase.Winding)
        {
            label = full == 0 && part < 0.05f ? (next ? "circle here" : "next") : full + " / " + turnsPerJoint + " wraps";
            if (!next && !active && full == 0)
                labelColour = new Color(1f, 1f, 1f, 0.35f);
        }
        else
        {
            float t = tension[j];
            bool tooHard = t > sweetSpot.y;
            label = !active ? "pull it tight" : (tooHard ? "too hard, ease off" : (t >= sweetSpot.x ? "hold it..." : "drag away"));
            if (active && tooHard)
                labelColour = warnColor;
        }
        GUI.color = labelColour;
        GUI.Label(new Rect(c.x - 120f, lineY, 240f, 22f), label, stateStyle);

        if (phase[j] == Phase.Pulling)
        {
            DrawTensionBar(j, new Rect(c.x - 90f, rowY + 1f, 180f, 12f), active);
            return;
        }
        float pipX = c.x - (turnsPerJoint - 1) * 11f;
        for (int k = 0; k < turnsPerJoint; k++)
        {
            bool on = k < full || phase[j] == Phase.Tied;
            GUI.color = on ? light : new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(new Rect(pipX + k * 22f - 6f, rowY, 12f, 12f), dotTex);
        }
    }

    // The tension bar: the green to aim for, the pull so far, and under it the tie meter filling while in the green.
    private void DrawTensionBar(int j, Rect bar, bool active)
    {
        float t = tension[j];
        bool inGreen = t >= sweetSpot.x && t <= sweetSpot.y;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.Box(bar, GUIContent.none, barStyle);
        GUI.color = Tint(strandColor, inGreen ? 0.8f : 0.45f);
        GUI.DrawTexture(new Rect(bar.x + bar.width * sweetSpot.x / BarRange, bar.y + 1f, bar.width * (sweetSpot.y - sweetSpot.x) / BarRange, bar.height - 2f), Texture2D.whiteTexture);
        float shown = Mathf.Clamp01(t / BarRange);
        if (shown > 0.02f)
        {
            GUI.color = t > sweetSpot.y ? warnColor : (inGreen ? Color.white : accent);
            GUI.Box(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * shown, bar.height - 2f), GUIContent.none, barStyle);
        }
        var meter = new Rect(bar.x, bar.yMax + 5f, bar.width, 4f);
        GUI.color = new Color(1f, 1f, 1f, 0.1f);
        GUI.DrawTexture(meter, Texture2D.whiteTexture);
        float ready = holdToTie > 0f ? Mathf.Clamp01(inGreenFor[j] / holdToTie) : 1f;
        if (ready > 0f && active)
        {
            GUI.color = Color.Lerp(strandColor, Color.white, 0.35f);
            GUI.DrawTexture(new Rect(meter.x, meter.y, meter.width * ready, meter.height), Texture2D.whiteTexture);
        }
    }

    // A guide: a dot with a short tail going round the track, showing the motion to make.
    private void DrawGhost(Vector2 c, float now)
    {
        for (int i = 0; i < 7; i++)
        {
            float a = Top + (now * 0.8f - i * 0.035f) * Tau;
            Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * TrackRadius;
            float size = 15f - i * 1.7f;
            GUI.color = new Color(1f, 1f, 1f, 0.55f - i * 0.075f);
            GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), dotTex);
        }
    }

    // Part of a circle, from start round by sweep (either way), as a row of overlapping dots: smooth at any length,
    // no texture per length.
    private void DrawArcDots(Vector2 c, float radius, float start, float sweep, float size, Color colour)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(sweep) * radius / (size * 0.4f)));
        GUI.color = colour;
        for (int i = 0; i <= count; i++)
        {
            float a = start + sweep * i / count;
            Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), dotTex);
        }
    }

    // The finish, calm and short: the pieces glide together as the coils tighten into them (0 - Meet At); where they
    // meet, one soft glow and one ring go out with a light spray of sparks, and the pieces give way to the key, which
    // settles in with a small overshoot, its name fading in under it; it holds there, breathing a little, and the
    // whole board fades for the inspect view (the last Fade Out seconds).
    private void DrawFinish(float since, float fade)
    {
        float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(since / MeetAt));
        float after = Mathf.Max(0f, since - MeetAt);
        float keyIn = Mathf.Clamp01(after / 0.45f);
        Vector2 middle = PieceCentre(1, slide);

        GUI.color = Tint(Color.white, (1f - keyIn) * fade);
        for (int i = 0; i < 3; i++)
        {
            Vector2 c = PieceCentre(i, slide);
            DrawSprite(new Rect(c.x - pieceSize * 0.5f, c.y - pieceSize * 0.5f, pieceSize, pieceSize), PieceIcon(i), new Color(0.85f, 0.82f, 0.7f));
        }
        float coilAlpha = 1f - Mathf.Clamp01((since - MeetAt * 0.6f) / (MeetAt * 0.4f));
        if (coilAlpha > 0f)
            for (int j = 0; j < 2; j++)
                DrawCoils(JointCentre(j, slide), turnsPerJoint, slide, 0.2f + 0.2f * slide, coilAlpha * fade);

        if (after > 0f)
        {
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(after / 0.9f), 3f);   // eased out
            GUI.color = Tint(accent, (1f - e) * 0.4f * fade);
            float glow = 220f + e * 240f;
            GUI.DrawTexture(new Rect(middle.x - glow * 0.5f, middle.y - glow * 0.5f, glow, glow), dotTex);
            DrawRing(middle, 70f + e * 280f, 3f, 1f, Tint(Color.white, (1f - e) * 0.6f * fade));

            float st = Mathf.Clamp01(after / 1.1f);
            float travel = 1f - (1f - st) * (1f - st);
            if (st < 1f && sparkDirections != null)
                for (int i = 0; i < sparkDirections.Length; i++)
                {
                    Vector2 p = middle + sparkDirections[i] * sparkSpeeds[i] * travel;
                    float size = Mathf.Lerp(7f, 2f, st);
                    GUI.color = Tint(i % 2 == 0 ? Color.white : accent, (1f - st) * 0.9f * fade);
                    GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), dotTex);
                }
        }

        if (keyIn > 0f && rewardItem != null)
        {
            float breathe = 1f + 0.025f * Mathf.Sin(after * 2.4f);
            GUI.color = Tint(accent, keyIn * 0.14f * fade);
            float halo = pieceSize * 2.4f * breathe;
            GUI.DrawTexture(new Rect(middle.x - halo * 0.5f, middle.y - halo * 0.5f, halo, halo), dotTex);
            float size = pieceSize * 1.6f * Mathf.LerpUnclamped(0.8f, 1f, EaseOutBack(keyIn)) * breathe;
            GUI.color = Tint(Color.white, keyIn * fade);
            DrawSprite(new Rect(middle.x - size * 0.5f, middle.y - size * 0.5f, size, size), rewardItem.Icon, Color.white);
            float nameIn = Mathf.Clamp01((after - 0.35f) / 0.4f);
            GUI.color = Tint(Color.white, nameIn * fade);
            GUI.Label(new Rect(middle.x - 220f, middle.y + pieceSize * 0.85f, 440f, 30f), rewardItem.DisplayName, subtitleStyle);
        }
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        float t = x - 1f;
        return 1f + (c1 + 1f) * t * t * t + c1 * t * t;
    }

    // The line under the title: what to do right now.
    private string Instruction()
    {
        int next = NextJoint();
        if (next >= 0 && phase[next] == Phase.Pulling)
            return pullTightHint;
        int tiedCount = (phase[0] == Phase.Tied ? 1 : 0) + (phase[1] == Phase.Tied ? 1 : 0);
        return tiedCount == 1 ? otherJointHint : windHint;
    }

    private Sprite PieceIcon(int index)
    {
        if (pieceIcons != null && index < pieceIcons.Length && pieceIcons[index] != null)
            return pieceIcons[index];
        return fragmentItem != null ? fragmentItem.Icon : null;
    }

    private static Color Tint(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, alpha);

    // A smooth ring: an anti-aliased annulus texture (one per thickness-to-radius ratio, made once and kept) stretched
    // to size; squash < 1 flattens it into an ellipse, so it reads as wrapping round a bar.
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
    private void DrawStrand(Vector2 from, Vector2 to, float sag, Color colour)
    {
        float length = Vector2.Distance(from, to);
        int steps = Mathf.Clamp(Mathf.CeilToInt(length / 22f), 1, 24);
        Vector2 previousPoint = from;
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 p = Vector2.Lerp(from, to, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * length * sag);
            DrawSegment(previousPoint, p, Mathf.Lerp(8f, 6f, t), colour);
            previousPoint = p;
        }
    }

    // A loose end dangling from the cursor, swaying a little.
    private void DrawHangingStrand(Vector2 at)
    {
        float sway = Mathf.Sin(Time.unscaledTime * 2.2f) * 10f;
        const int steps = 6;
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
        RotateAround(angle, a);
        GUI.color = new Color(colour.r, colour.g, colour.b, 0.25f * previous.a);
        GUI.DrawTexture(new Rect(a.x - thickness, a.y - thickness * 1.5f, length + thickness * 2f, thickness * 3f), capsuleTex);
        GUI.color = new Color(colour.r, colour.g, colour.b, previous.a);
        GUI.DrawTexture(new Rect(a.x - thickness * 0.5f, a.y - thickness * 0.5f, length + thickness, thickness), capsuleTex);
        GUI.color = previous;
        GUI.matrix = keep;
    }

    // Turns what is drawn next by angle (degrees) round pivot, in board units. GUIUtility.RotateAroundPivot takes the
    // pivot in screen pixels and ignores the board's scale, so everything it turned landed off to the side (the strand
    // away from the cursor) on any screen but 900p.
    private static void RotateAround(float angle, Vector2 pivot)
    {
        GUI.matrix = GUI.matrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, angle), Vector3.one) * Matrix4x4.Translate(-pivot);
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

    // Everything made once, the first time the board draws; the rings the board uses are made then too, so nothing is
    // built while playing.
    private void EnsureStyles()
    {
        if (boardTex != null)
            return;
        backdropTex = Vignette(backdropCentre, backdropEdge);
        boardTex = Rounded(96, 24f, boardColor, new Color(1f, 1f, 1f, 0.08f), 1.5f);
        dotTex = Dot(32);
        capsuleTex = Rounded(32, 16f, Color.white, Color.clear, 0f);
        barTex = Rounded(20, 5f, Color.white, Color.clear, 0f);
        boardStyle = new GUIStyle { border = new RectOffset(40, 40, 40, 40) };
        boardStyle.normal.background = boardTex;
        barStyle = new GUIStyle { border = new RectOffset(8, 8, 8, 8) };
        barStyle.normal.background = barTex;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        titleStyle.normal.textColor = textColor;
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        hintStyle.normal.textColor = new Color(0.72f, 0.8f, 0.87f);
        footStyle = new GUIStyle(hintStyle) { fontSize = 13, wordWrap = false };
        footStyle.normal.textColor = new Color(0.45f, 0.53f, 0.6f);
        footStyle.alignment = TextAnchor.MiddleLeft;
        footRightStyle = new GUIStyle(footStyle) { alignment = TextAnchor.MiddleRight };
        subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        subtitleStyle.normal.textColor = Color.white;
        stateStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        stateStyle.normal.textColor = Color.white;

        // The same font as the rest of the game.
        Font font = GameFont.Font;
        foreach (GUIStyle style in new[] { titleStyle, hintStyle, footStyle, footRightStyle, subtitleStyle, stateStyle })
            style.font = font;

        // The ring shapes the board draws (all fairly thin for their size), made now rather than mid-game.
        for (int key = RingSteps * 7 / 10; key <= RingSteps; key++)
            RingTexture(key / (float)RingSteps);
    }

    private const int RingSteps = 40;

    // An annulus, white, anti-aliased, from innerRatio of the half size out to the edge; one per ratio step, kept.
    private Texture2D RingTexture(float innerRatio)
    {
        int key = Mathf.RoundToInt(Mathf.Clamp01(innerRatio) * RingSteps);
        if (rings.TryGetValue(key, out Texture2D cached) && cached != null)
            return cached;
        const int size = 128;
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        float outer = half - 1f;
        float inner = key / (float)RingSteps * outer;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(Mathf.Min(d - inner, outer - d) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        rings[key] = tex;
        return tex;
    }

    private static Texture2D NewTexture(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
    }

    private static Texture2D Vignette(Color centre, Color edge)
    {
        const int size = 64;
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
        tex.Apply(false, true);
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
        tex.Apply(false, true);
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
        tex.Apply(false, true);
        return tex;
    }
}
