using UnityEngine;

// Lights the item you are inspecting: a key light above and to the right of the camera and a softer fill from the
// other side, both riding on the camera so the item is lit however you turn. Fades in and out. Static Show / Hide,
// used by PickupItem. To tune it in the Inspector, add an Inspect Light to any object in the scene; that one is used.
public class InspectLight : MonoBehaviour
{
    [Tooltip("Key light brightness (URP point light intensity).")]
    [SerializeField] private float keyIntensity = 2.5f;
    [Tooltip("Fill light brightness, from the opposite side, so the shadow side is not black.")]
    [SerializeField] private float fillIntensity = 0.8f;
    [SerializeField] private Color color = new Color(1f, 0.96f, 0.9f);
    [Tooltip("Where the key light sits relative to the camera: right, up, forward, in metres.")]
    [SerializeField] private Vector3 keyOffset = new Vector3(0.6f, 0.7f, 0.4f);
    [SerializeField] private Vector3 fillOffset = new Vector3(-0.8f, -0.2f, 0.6f);
    [Tooltip("How far the lights reach. Keep it short so they only touch the held item.")]
    [SerializeField] private float range = 4f;
    [SerializeField] private float fadeSeconds = 0.2f;

    private static InspectLight instance;
    private Light key;
    private Light fill;
    private float target;
    private float level;

    public static void Show(Camera camera)
    {
        InspectLight light = Get();
        if (light != null)
            light.Begin(camera);
    }

    public static void Hide()
    {
        if (instance != null)
            instance.target = 0f;
    }

    private static InspectLight Get()
    {
        if (instance != null)
            return instance;
        instance = FindFirstObjectByType<InspectLight>(FindObjectsInactive.Include);
        if (instance == null)
            instance = new GameObject("InspectLight").AddComponent<InspectLight>();
        return instance;
    }

    private void Awake()
    {
        instance = this;
        key = MakeLight("Key");
        fill = MakeLight("Fill");
    }

    private Light MakeLight(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.color = color;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        light.enabled = false;
        return light;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Begin(Camera camera)
    {
        target = 1f;
        if (camera == null)
            return;
        // Ride on the camera, so the item stays lit from the same side whichever way the player looks.
        transform.SetParent(camera.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        key.transform.localPosition = keyOffset;
        fill.transform.localPosition = fillOffset;
    }

    private void Update()
    {
        if (key == null || fill == null)
            return;
        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        level = Mathf.MoveTowards(level, target, step);
        bool on = level > 0.001f;
        key.enabled = on;
        fill.enabled = on;
        key.intensity = keyIntensity * level;
        fill.intensity = fillIntensity * level;
        key.range = range;
        fill.range = range;
        key.color = color;
        fill.color = color;
    }
}
