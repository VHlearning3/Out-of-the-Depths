using UnityEngine;

// An old lamp on its last legs: every so often (Min / Max Gap seconds apart) it stutters for a moment, dipping towards
// Dip and back, then steadies. Bulb, if set, dims along with it (its emission). Put it on the Light.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Tooltip("Seconds between stutters: a random wait in this range.")]
    [SerializeField] private float minGap = 2.5f;
    [SerializeField] private float maxGap = 8f;
    [Tooltip("About how long a stutter lasts, in seconds.")]
    [SerializeField] private float burstSeconds = 0.6f;
    [Tooltip("How dark it drops at the bottom of a stutter (0 = out, 1 = no dip).")]
    [SerializeField, Range(0f, 1f)] private float dip = 0.1f;
    [Tooltip("The lamp's glowing bulb: its emission follows the light. Optional.")]
    [SerializeField] private Renderer bulb;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private Light lamp;
    private float baseIntensity;
    private float nextBurst;
    private float burstUntil;
    private float nextStep;
    private float value = 1f;
    private Color baseEmission;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        nextBurst = Time.time + Random.Range(minGap, maxGap);
        if (bulb != null && bulb.sharedMaterial != null && bulb.sharedMaterial.HasProperty(EmissionId))
        {
            baseEmission = bulb.sharedMaterial.GetColor(EmissionId);
            block = new MaterialPropertyBlock();
        }
    }

    private void Update()
    {
        if (Time.time >= nextBurst)
        {
            burstUntil = Time.time + burstSeconds * Random.Range(0.5f, 1.5f);
            nextBurst = burstUntil + Random.Range(minGap, maxGap);
        }
        if (Time.time < burstUntil)
        {
            if (Time.time >= nextStep)
            {
                value = Random.value < 0.5f ? Random.Range(dip, 0.4f) : Random.Range(0.8f, 1f);
                nextStep = Time.time + Random.Range(0.03f, 0.1f);
            }
        }
        else
        {
            value = Mathf.MoveTowards(value, 1f, Time.deltaTime * 4f);
        }

        lamp.intensity = baseIntensity * value;
        if (block != null)
        {
            bulb.GetPropertyBlock(block);   // keep whatever tint is on it
            block.SetColor(EmissionId, baseEmission * value);
            bulb.SetPropertyBlock(block);
        }
    }
}
