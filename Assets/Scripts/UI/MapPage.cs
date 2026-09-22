using UnityEngine;

// The Map page of the pause menu: a framed area with the map in it. Drop a texture into Map Texture (a painted map,
// or a Render Texture from a top-down camera), set World Min / World Max to the world x and z of its bottom-left and
// top-right corners, and the player shows as a dot on it. Nothing assigned = a placeholder that holds the space.
public class MapPage : MonoBehaviour, IPauseMenuPage
{
    [SerializeField] private string pageTitle = "Map";
    [Tooltip("The map image. A Render Texture from a top-down camera works too.")]
    [SerializeField] private Texture mapTexture;
    [Tooltip("Shown instead while the player is below Basement Below Y (a second floor plan). Optional.")]
    [SerializeField] private Texture basementTexture;
    [SerializeField] private float basementBelowY = -0.5f;
    [Tooltip("World x and z at the bottom-left corner of the image, and at the top-right corner.")]
    [SerializeField] private Vector2 worldMin = new Vector2(-50f, -50f);
    [SerializeField] private Vector2 worldMax = new Vector2(50f, 50f);
    [Tooltip("Found automatically (the Swim Controller).")]
    [SerializeField] private Transform player;
    [SerializeField] private Color markerColor = new Color(0.35f, 0.85f, 0.95f);
    [Tooltip("Width : height of the map area while there is no texture.")]
    [SerializeField] private float placeholderAspect = 1.6f;

    private static Texture2D dot;
    private GUIStyle placeholderStyle;

    public string PageTitle => pageTitle;
    public int Order => 0;

    public void OnPageShown()
    {
        if (player == null)
        {
            SwimController swimmer = FindFirstObjectByType<SwimController>();
            if (swimmer != null)
                player = swimmer.transform;
        }
    }

    public void DrawPage()
    {
        Texture mapTexture = ActiveMap();
        float aspect = mapTexture != null && mapTexture.height > 0 ? (float)mapTexture.width / mapTexture.height : placeholderAspect;
        Rect area = GUILayoutUtility.GetAspectRect(Mathf.Max(0.2f, aspect));
        GUI.Box(area, GUIContent.none);
        Rect inner = new Rect(area.x + 8f, area.y + 8f, area.width - 16f, area.height - 16f);

        if (mapTexture != null)
        {
            GUI.DrawTexture(inner, mapTexture, ScaleMode.ScaleToFit);
        }
        else if (Event.current.type == EventType.Repaint)
        {
            // A faint grid, so the space reads as a map that is still to come.
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.05f * previous.a);
            for (int i = 1; i < 8; i++)
            {
                GUI.DrawTexture(new Rect(inner.x + inner.width * i / 8f, inner.y, 1f, inner.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(inner.x, inner.y + inner.height * i / 8f, inner.width, 1f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        if (mapTexture == null)
        {
            placeholderStyle ??= new GUIStyle(MenuGUI.NoteStyle ?? GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            GUI.Label(inner, "No map yet. Assign a texture (or a Render Texture from a top-down camera) on the Map Page component and set its world corners; the player then shows as a dot.", placeholderStyle);
        }

        if (player != null && Event.current.type == EventType.Repaint)
        {
            Vector2 span = worldMax - worldMin;
            if (span.x > 0.001f && span.y > 0.001f)
            {
                float u = Mathf.Clamp01((player.position.x - worldMin.x) / span.x);
                float v = Mathf.Clamp01((player.position.z - worldMin.y) / span.y);
                Rect fit = Fit(inner, aspect);
                float x = fit.x + u * fit.width;
                float y = fit.y + (1f - v) * fit.height;
                Color previous = GUI.color;
                GUI.color = new Color(markerColor.r, markerColor.g, markerColor.b, previous.a);
                GUI.DrawTexture(new Rect(x - 7f, y - 7f, 14f, 14f), Dot());
                GUI.color = previous;
            }
        }
        GUILayout.Space(10f);
        GUILayout.Label("The map fills the page. Any script that implements IPauseMenuPage can replace this one.", MenuGUI.NoteStyle ?? GUI.skin.label);
    }

    // The deck the player is on: the basement plan while they are down there, else the main one.
    private Texture ActiveMap()
    {
        return basementTexture != null && player != null && player.position.y < basementBelowY ? basementTexture : mapTexture;
    }

    // The part of the frame the image actually covers when scaled to fit.
    private static Rect Fit(Rect frame, float aspect)
    {
        float frameAspect = frame.width / Mathf.Max(1f, frame.height);
        if (aspect > frameAspect)
        {
            float h = frame.width / aspect;
            return new Rect(frame.x, frame.y + (frame.height - h) * 0.5f, frame.width, h);
        }
        float w = frame.height * aspect;
        return new Rect(frame.x + (frame.width - w) * 0.5f, frame.y, w, frame.height);
    }

    // A soft round dot.
    private static Texture2D Dot()
    {
        if (dot != null)
            return dot;
        const int size = 16;
        dot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(size * 0.5f - 1f - d + 0.5f));
            }
        dot.SetPixels(pixels);
        dot.Apply();
        return dot;
    }
}
