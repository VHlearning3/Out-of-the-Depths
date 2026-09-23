using UnityEngine;
using UnityEngine.Rendering;

// Placeholder slash visual until the hand art comes: a see-through band along the blade's path across the screen, as
// wide as its hit area, growing as the swing goes (right to left for the sweep, top to bottom for the chop), tapering
// towards where it started, with a bright line where the blade is. It turns warm when the slash cuts something and
// fades out after the swing. It is the same arc the hits are checked on, projected onto the screen, so what it covers
// is what you attack. (Not the swept plane itself: that passes through the eye, so on screen it has no width.)
// Drawn straight onto the screen in OnGUI with GL, over everything, the way the rest of the game's IMGUI is drawn.
// Added to the player by Slash Attack (Placeholder Trail); switch that off once the hands show the slashes.
public class SlashTrail : MonoBehaviour
{
    [Tooltip("How far out along the reach the path is drawn (0..1): where most things get hit.")]
    [SerializeField, Range(0.2f, 1f)] private float depth = 0.6f;
    [Tooltip("How thick the band is next to the blade's real hit width (1 = the whole hit area, which is big; smaller = a slimmer slash mark along its middle).")]
    [SerializeField, Range(0.1f, 1f)] private float bandWidth = 0.35f;
    [Tooltip("The band is never thinner than this, in pixels at 1080p.")]
    [SerializeField] private float minWidth = 24f;
    [SerializeField] private Color missColor = new Color(0.75f, 0.95f, 1f, 0.35f);
    [SerializeField] private Color hitColor = new Color(1f, 0.6f, 0.3f, 0.5f);
    [Tooltip("The blade: a bright line across the band's leading edge, this wide in pixels at 1080p (0 = none).")]
    [SerializeField] private float edgeWidth = 7f;
    [SerializeField] private Color edgeColor = new Color(1f, 1f, 1f, 0.95f);
    [Tooltip("How faint the start of the band is next to the blade (0..1).")]
    [SerializeField, Range(0f, 1f)] private float tail = 0.1f;
    [Tooltip("Seconds it takes to fade after the swing.")]
    [SerializeField] private float fadeSeconds = 0.3f;
    [Tooltip("Write a line to the Console for each slash (what it drew and where), to check it is working.")]
    [SerializeField] private bool logSlashes = false;

    private const float StepDegrees = 3f;

    private static Material lineMaterial;

    private SlashAttack slash;
    private Camera view;
    private Vector2[] path = new Vector2[0];
    private float[] along = new float[0];
    private int used;
    private float halfWidth;
    private float fromAngle, toAngle;
    private SlashAttack.Direction direction;
    private bool hit;
    private float fade;
    private bool wasSwinging;
    private bool logged;

    private void Awake()
    {
        slash = GetComponent<SlashAttack>();
    }

    // Track the swing after everything has moved this frame.
    private void LateUpdate()
    {
        if (slash == null || slash.Origin == null)
            return;
        if (slash.Swinging)
        {
            if (!wasSwinging)
            {
                hit = false;   // a new swing
                logged = false;
            }
            direction = slash.LastDirection;
            fromAngle = slash.ArcStartAngle;
            toAngle = slash.BladeAngle;
            hit |= slash.HitsThisSwing > 0;
            fade = 1f;
        }
        else
        {
            if (wasSwinging)
            {
                toAngle = slash.BladeAngle;   // the swing ended this frame: take its last stretch too
                hit |= slash.HitsThisSwing > 0;
            }
            fade = fadeSeconds > 0f ? Mathf.MoveTowards(fade, 0f, Time.deltaTime / fadeSeconds) : 0f;
        }
        wasSwinging = slash.Swinging;
        used = 0;
        if (fade > 0f && Mathf.Abs(toAngle - fromAngle) >= 0.5f)
            BuildPath();
    }

    // The blade tip's path in screen pixels (origin top left, as GL draws in OnGUI), every few degrees, and the band's half width.
    private void BuildPath()
    {
        if (view == null || !view.isActiveAndEnabled)
        {
            view = FirstEnabled(slash.Origin.GetComponentsInChildren<Camera>());
            if (view == null)
                view = FirstEnabled(GetComponentsInChildren<Camera>());
            if (view == null)
                view = slash.Origin.GetComponentInParent<Camera>();
            if (view == null)
                view = Camera.main;
            if (view == null)
                return;
        }
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(toAngle - fromAngle) / StepDegrees));
        if (path.Length < steps + 1)
        {
            path = new Vector2[steps + 1];
            along = new float[steps + 1];
        }
        Vector3 origin = slash.Origin.position;
        float distance = Mathf.Max(0.5f, slash.Reach * depth);
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 world = origin + slash.BladeDirectionAt(Mathf.Lerp(fromAngle, toAngle, t), direction) * distance;
            Vector3 screen = view.WorldToScreenPoint(world);
            if (screen.z <= 0.01f)
                continue;   // behind the camera
            path[used] = new Vector2(screen.x, Screen.height - screen.y);   // GL in OnGUI counts pixels from the top
            along[used] = t;
            used++;
        }

        // The blade's radius seen at that distance, across the path.
        float scale = Screen.height / 1080f;
        Vector3 middle = origin + slash.BladeDirectionAt((fromAngle + toAngle) * 0.5f, direction) * distance;
        Vector3 across = direction == SlashAttack.Direction.LeftRight ? slash.Origin.up : slash.Origin.right;
        Vector3 a = view.WorldToScreenPoint(middle), b = view.WorldToScreenPoint(middle + across * slash.BladeRadius);
        halfWidth = Mathf.Max(a.z > 0f && b.z > 0f ? Vector2.Distance(a, b) * bandWidth : 0f, minWidth * 0.5f * scale);

        if (logSlashes && !logged && used >= 2)
        {
            logged = true;
            Debug.Log($"SlashTrail: {direction} slash, {used} points on a {Screen.width}x{Screen.height} screen through '{view.name}', band {halfWidth * 2f:0} px wide, from {path[0]} to {path[used - 1]}.", this);
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || used < 2 || fade <= 0f)
            return;
        if (!EnsureMaterial())
            return;

        Color colour = hit ? hitColor : missColor;
        float scale = Screen.height / 1080f;
        lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix();
        GL.Begin(GL.TRIANGLES);
        Vector2 lastInner = default, lastOuter = default;
        for (int i = 0; i < used; i++)
        {
            Vector2 forward = (path[Mathf.Min(i + 1, used - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x);
            float width = halfWidth * Mathf.Lerp(0.3f, 1f, along[i]);   // thin where the swing started, full at the blade
            Vector2 inner = path[i] - side * width, outer = path[i] + side * width;
            if (i > 0)
            {
                Color from = Fade(colour, along[i - 1]), to = Fade(colour, along[i]);
                Quad(lastInner, lastOuter, outer, inner, from, from, to, to);
            }
            lastInner = inner;
            lastOuter = outer;
        }

        // The blade: a bright bar across the band where the tip is now.
        if (edgeWidth > 0f)
        {
            Vector2 tip = path[used - 1];
            Vector2 forward = (path[used - 1] - path[used - 2]).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x);
            Vector2 thick = forward * (edgeWidth * 0.5f * scale);
            Vector2 reach = side * (halfWidth * 1.05f);
            Color bright = new Color(edgeColor.r, edgeColor.g, edgeColor.b, edgeColor.a * fade);
            Color soft = new Color(bright.r, bright.g, bright.b, bright.a * 0.4f);
            Quad(tip - reach - thick, tip + reach - thick, tip + reach + thick, tip - reach + thick, soft, bright, bright, soft);
        }
        GL.End();
        GL.PopMatrix();
    }

    private Color Fade(Color colour, float t) => new Color(colour.r, colour.g, colour.b, colour.a * Mathf.Lerp(tail, 1f, t * t) * fade);

    private static void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
    {
        GL.Color(ca); GL.Vertex3(a.x, a.y, 0f);
        GL.Color(cb); GL.Vertex3(b.x, b.y, 0f);
        GL.Color(cc); GL.Vertex3(c.x, c.y, 0f);
        GL.Color(ca); GL.Vertex3(a.x, a.y, 0f);
        GL.Color(cc); GL.Vertex3(c.x, c.y, 0f);
        GL.Color(cd); GL.Vertex3(d.x, d.y, 0f);
    }

    // Unity's own flat-colour shader, see-through, both sides, over everything.
    private static bool EnsureMaterial()
    {
        if (lineMaterial != null)
            return true;
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
            return false;
        lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
        lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        return true;
    }

    private static Camera FirstEnabled(Camera[] cameras)
    {
        foreach (Camera camera in cameras)
            if (camera.isActiveAndEnabled)
                return camera;
        return null;
    }
}
