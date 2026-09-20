using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Blurs everything behind the item while a pickup is inspected: a runtime URP Volume with a Gaussian depth of field
// whose sharp band sits at the item and follows the zoom, fading in and out. Post-processing has to be on for the
// camera: it is switched on while this runs and put back after. Static Show / Focus / Hide, used by PickupItem.
// To tune it in the Inspector, add an Inspect Focus to any object in the scene; that one is used.
public class InspectFocus : MonoBehaviour
{
    [Tooltip("Blur strength (URP Gaussian max radius, 0..1.5).")]
    [SerializeField, Range(0f, 1.5f)] private float blur = 0.9f;
    [Tooltip("Metres behind the item that stay sharp...")]
    [SerializeField] private float sharpBand = 0.5f;
    [Tooltip("...and over how many more metres the blur reaches full strength.")]
    [SerializeField] private float blurBand = 2.5f;
    [SerializeField] private float fadeSeconds = 0.3f;

    private static InspectFocus instance;
    private Volume volume;
    private DepthOfField depthOfField;
    private float target;
    private float distance = 1.5f;
    private UniversalAdditionalCameraData cameraData;
    private bool hadPostProcessing;
    private bool running;

    public static void Show(Camera camera, float focusDistance)
    {
        InspectFocus focus = Get();
        if (focus != null)
            focus.Begin(camera, focusDistance);
    }

    // Where the sharp band sits, in metres from the camera (the item's hold distance).
    public static void Focus(float focusDistance)
    {
        if (instance != null)
            instance.distance = focusDistance;
    }

    public static void Hide()
    {
        if (instance != null)
            instance.target = 0f;
    }

    private static InspectFocus Get()
    {
        if (instance != null)
            return instance;
        instance = FindFirstObjectByType<InspectFocus>(FindObjectsInactive.Include);
        if (instance == null)
            instance = new GameObject("InspectFocus").AddComponent<InspectFocus>();
        return instance;
    }

    private void Awake()
    {
        instance = this;
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 0f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "InspectFocus (runtime)";
        depthOfField = profile.Add<DepthOfField>(true);
        depthOfField.mode.value = DepthOfFieldMode.Gaussian;
        depthOfField.highQualitySampling.value = true;
        volume.sharedProfile = profile;
        Apply();
    }

    private void OnDestroy()
    {
        if (running && cameraData != null)
            cameraData.renderPostProcessing = hadPostProcessing;
        if (instance == this)
            instance = null;
    }

    private void Begin(Camera camera, float focusDistance)
    {
        distance = focusDistance;
        target = 1f;
        if (running)
            return;
        running = true;
        cameraData = camera != null ? camera.GetUniversalAdditionalCameraData() : null;
        if (cameraData != null)
        {
            hadPostProcessing = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = true;
        }
    }

    private void Update()
    {
        if (volume == null)
            return;
        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        volume.weight = Mathf.MoveTowards(volume.weight, target, step);
        Apply();

        if (running && target <= 0f && volume.weight <= 0f)
        {
            running = false;
            if (cameraData != null)
                cameraData.renderPostProcessing = hadPostProcessing;
        }
    }

    private void Apply()
    {
        if (depthOfField == null)
            return;
        depthOfField.gaussianStart.value = distance + sharpBand;
        depthOfField.gaussianEnd.value = distance + sharpBand + blurBand;
        depthOfField.gaussianMaxRadius.value = blur;
    }
}
