using UnityEngine;

// One place to tune the underwater look. Applies fog and ambient light on load and
// gives the sun a slow shimmer, like light coming through moving water. FlickerFor() makes the light
// stutter for a moment (the chase reveal).
[ExecuteAlways]
public class UnderwaterLighting : MonoBehaviour
{
    [Header("Fog")]
    [SerializeField] private Color fogColor = new Color(0.03f, 0.18f, 0.28f);
    [SerializeField] private float fogDensity = 0.045f;

    [Header("Ambient")]
    [SerializeField] private Color ambientSky = new Color(0.1f, 0.35f, 0.5f);
    [SerializeField] private Color ambientEquator = new Color(0.05f, 0.2f, 0.3f);
    [SerializeField] private Color ambientGround = new Color(0.02f, 0.08f, 0.12f);

    [Header("Sun")]
    [SerializeField] private Light sun;
    [SerializeField] private Color sunColor = new Color(0.55f, 0.85f, 1f);
    [SerializeField] private float sunIntensity = 0.8f;
    [SerializeField] private float shimmerAmount = 0.15f;
    [SerializeField] private float shimmerSpeed = 1.3f;

    [Header("Flicker")]
    [Tooltip("How dark the sun drops at the bottom of a flicker (0 = out, 1 = no dip). FlickerFor(seconds) starts one.")]
    [SerializeField, Range(0f, 1f)] private float flickerDepth = 0.15f;

    private float flickerUntil = -1f;
    private float flickerValue = 1f;
    private float nextFlickerStep;

    // The light stutters for this many real seconds (so slow motion doesn't stretch it).
    public void FlickerFor(float seconds)
    {
        flickerUntil = Time.unscaledTime + seconds;
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void Update()
    {
        if (sun == null || !Application.isPlaying)
            return;

        float shimmer = (Mathf.Sin(Time.time * shimmerSpeed) + Mathf.Sin(Time.time * shimmerSpeed * 2.7f) * 0.5f) / 1.5f;

        float flicker = 1f;
        if (Time.unscaledTime < flickerUntil)
        {
            if (Time.unscaledTime >= nextFlickerStep)
            {
                flickerValue = Random.value < 0.45f ? Random.Range(flickerDepth, 0.5f) : Random.Range(0.8f, 1f);
                nextFlickerStep = Time.unscaledTime + Random.Range(0.03f, 0.12f);
            }
            flicker = flickerValue;
        }

        sun.intensity = sunIntensity * (1f + shimmer * shimmerAmount) * flicker;
    }

    public void Apply()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;

        if (sun != null)
        {
            sun.color = sunColor;
            sun.intensity = sunIntensity;
        }
    }
}
