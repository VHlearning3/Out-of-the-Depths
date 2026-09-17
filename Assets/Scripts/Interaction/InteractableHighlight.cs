using UnityEngine;

// Lights up the meshes under this object while the player is looking at it. Works on any material (tint + brightness);
// materials with Emission enabled also glow. Keeps whatever Renderer Tint has set.
public class InteractableHighlight : MonoBehaviour, IInteractTargetListener
{
    [Header("Look")]
    [Tooltip("Colour the meshes blend towards while targeted.")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.95f, 0.75f);
    [Tooltip("0 = keep the mesh colour, 1 = fully the highlight colour.")]
    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.3f;
    [Tooltip("Extra brightness on top of the tint, so even white placeholders visibly light up.")]
    [SerializeField, Range(0f, 2f)] private float brightness = 0.6f;
    [Tooltip("Added glow. Only visible on materials that have Emission enabled.")]
    [SerializeField, ColorUsage(false, true)] private Color emission = new Color(1.5f, 1.2f, 0.6f);

    [Header("Animation")]
    [SerializeField] private float fadeTime = 0.12f;
    [Tooltip("Pulses per second while targeted. 0 = steady.")]
    [SerializeField] private float pulseSpeed = 1.5f;
    [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.35f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private Renderer[] renderers;
    private Color[] baseColors;
    private Color[] baseEmissions;
    private bool[] hadBlockColor;
    private bool[] hadBlockEmission;
    private MaterialPropertyBlock block;
    private bool targeted;
    private bool applied;
    private float weight;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => !(r is ParticleSystemRenderer));
        baseColors = new Color[renderers.Length];
        baseEmissions = new Color[renderers.Length];
        hadBlockColor = new bool[renderers.Length];
        hadBlockEmission = new bool[renderers.Length];
    }

    public void OnTargeted(bool value)
    {
        // Still fading out from the last look: the saved base state is still the true one, don't re-capture the lit colours.
        if (value && !applied)
            CaptureBaseState();
        targeted = value;
    }

    private void Update()
    {
        if (!targeted && weight <= 0f)
            return;

        float goal = targeted ? 1f : 0f;
        weight = fadeTime > 0f ? Mathf.MoveTowards(weight, goal, Time.deltaTime / fadeTime) : goal;

        if (weight <= 0f)
        {
            Restore();
            return;
        }

        float pulse = 1f - pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * 2f * Mathf.PI));
        Apply(weight * pulse);
    }

    private void CaptureBaseState()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            Material material = r.sharedMaterial;
            r.GetPropertyBlock(block);

            hadBlockColor[i] = block.HasColor(BaseColorId);
            baseColors[i] = hadBlockColor[i] ? block.GetColor(BaseColorId) : MaterialColor(material, BaseColorId, ColorId);

            hadBlockEmission[i] = block.HasColor(EmissionId);
            baseEmissions[i] = hadBlockEmission[i] ? block.GetColor(EmissionId) : MaterialColor(material, EmissionId, EmissionId, Color.black);
        }
    }

    private void Apply(float t)
    {
        Color glow = emission * t;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            r.GetPropertyBlock(block);

            Color lit = Color.Lerp(baseColors[i], highlightColor, tintStrength * t) * (1f + brightness * t);
            lit.a = baseColors[i].a;
            block.SetColor(BaseColorId, lit);
            block.SetColor(ColorId, lit);
            block.SetColor(EmissionId, baseEmissions[i] + glow);

            r.SetPropertyBlock(block);
        }
        applied = true;
    }

    private void Restore()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            r.GetPropertyBlock(block);
            block.Clear();

            if (hadBlockColor[i])
            {
                block.SetColor(BaseColorId, baseColors[i]);
                block.SetColor(ColorId, baseColors[i]);
            }
            if (hadBlockEmission[i])
                block.SetColor(EmissionId, baseEmissions[i]);

            r.SetPropertyBlock(block);
        }
        applied = false;
    }

    private static Color MaterialColor(Material material, int id, int fallbackId, Color? missing = null)
    {
        if (material != null)
        {
            if (material.HasProperty(id))
                return material.GetColor(id);
            if (material.HasProperty(fallbackId))
                return material.GetColor(fallbackId);
        }
        return missing ?? Color.white;
    }
}
