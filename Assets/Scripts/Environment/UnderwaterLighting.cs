using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One place to tune the underwater look, applied on load and kept up while playing: the murk (fog) and the ambient
// light; no sky, the camera clearing to the fog colour so the far end of every view is water; the sun tinted,
// shimmering, and throwing a slowly drifting web of caustics on whatever it reaches; and specks drifting round the
// camera. FlickerFor() makes the sun stutter for a moment (the chase reveal). The colour grade on top (tint,
// vignette, bloom) is a Volume on this object with Assets/Settings/UnderwaterProfile.asset, made by
// Tools > Out of the Depths > Underwater Look (Open Scene), which the scene builders run too.
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
    [Tooltip("The directional light. Empty = the scene's sun, else the first directional light in the scene.")]
    [SerializeField] private Light sun;
    [SerializeField] private Color sunColor = new Color(0.55f, 0.85f, 1f);
    [SerializeField] private float sunIntensity = 0.8f;
    [SerializeField] private float shimmerAmount = 0.15f;
    [SerializeField] private float shimmerSpeed = 1.3f;

    [Header("Caustics")]
    [Tooltip("A web of light on whatever the sun reaches, drifting slowly: a cookie on the sun. Off unless you like it.")]
    [SerializeField] private bool caustics = false;
    [Tooltip("Metres one tile of the web covers.")]
    [SerializeField] private float causticSize = 14f;
    [Tooltip("How much of the sun the web takes: 0 = none, 1 = only the bright lines light anything.")]
    [SerializeField, Range(0f, 1f)] private float causticStrength = 0.55f;
    [Tooltip("Metres per second the web drifts.")]
    [SerializeField] private float causticDrift = 0.4f;

    [Header("Water")]
    [Tooltip("No sky: the camera clears to the fog colour, so the far end of every view is water.")]
    [SerializeField] private bool noSky = true;
    [Tooltip("Specks drifting round the camera while playing.")]
    [SerializeField] private bool motes = true;
    [Tooltip("Their material (Underwater Look makes Art/Materials/WaterMotes.mat). Empty = one made on the spot.")]
    [SerializeField] private Material motesMaterial;
    [Tooltip("How many are about at a time.")]
    [SerializeField] private int motesCount = 260;
    [Tooltip("Metres across, the box round the camera they drift in.")]
    [SerializeField] private float motesArea = 26f;
    [SerializeField] private float motesSize = 0.05f;
    [Tooltip("How much they wander, metres per second.")]
    [SerializeField] private float motesDrift = 0.25f;

    [Header("Flicker")]
    [Tooltip("How dark the sun drops at the bottom of a flicker (0 = out, 1 = no dip). FlickerFor(seconds) starts one.")]
    [SerializeField, Range(0f, 1f)] private float flickerDepth = 0.15f;

    private float flickerUntil = -1f;
    private float flickerValue = 1f;
    private float nextFlickerStep;
    private Vector2 causticOffset;
    private ParticleSystem motesSystem;

    private static Texture2D causticTexture;
    private static float causticTextureStrength = -1f;
    private static Texture2D moteTexture;

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

    private void Start()
    {
        if (Application.isPlaying && motes)
            CreateMotes();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;
        Light light = Sun;
        if (light == null)
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

        light.intensity = sunIntensity * (1f + shimmer * shimmerAmount) * flicker;

        if (caustics && causticDrift != 0f && light.cookie != null)
        {
            causticOffset += new Vector2(0.7f, 0.4f) * (causticDrift * Time.deltaTime);
            light.GetUniversalAdditionalLightData().lightCookieOffset = causticOffset;
        }
    }

    private void LateUpdate()
    {
        if (motesSystem == null)
            return;
        Camera camera = Camera.main;
        if (camera != null)
            motesSystem.transform.position = camera.transform.position;
    }

    // The sun: the one assigned, else the scene's, else the first directional light about.
    private Light Sun
    {
        get
        {
            if (sun == null)
            {
                sun = RenderSettings.sun;
                if (sun == null)
                    foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (light.type == LightType.Directional)
                        {
                            sun = light;
                            break;
                        }
            }
            return sun;
        }
    }

    public void Apply()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;

        if (noSky)
        {
            RenderSettings.skybox = null;
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = fogColor;
            }
        }

        Light light = Sun;
        if (light == null)
            return;
        RenderSettings.sun = light;
        light.color = sunColor;
        light.intensity = sunIntensity;

        if (caustics)
        {
            if (causticTexture == null || !Mathf.Approximately(causticTextureStrength, causticStrength))
            {
                causticTexture = MakeCausticTexture(256, 6, causticStrength);
                causticTextureStrength = causticStrength;
            }
            light.cookie = causticTexture;
            UniversalAdditionalLightData data = light.GetUniversalAdditionalLightData();
            data.lightCookieSize = Vector2.one * Mathf.Max(1f, causticSize);
            data.lightCookieOffset = causticOffset;
        }
        else if (light.cookie != null && light.cookie == causticTexture)
            light.cookie = null;
    }

    // ---- the caustic web ------------------------------------------------------------------------------------------

    // A tileable web of bright lines (the borders between jittered cells), the rest at 1 - strength: the sun through
    // a rippling surface. Grey in every channel, so it works as a cookie whichever channel the pipeline reads.
    public static Texture2D MakeCausticTexture(int size, int cells, float strength, int seed = 7)
    {
        var random = new System.Random(seed);
        var points = new Vector2[cells * cells];
        for (int cy = 0; cy < cells; cy++)
            for (int cx = 0; cx < cells; cx++)
                points[cy * cells + cx] = new Vector2((cx + (float)random.NextDouble()) / cells, (cy + (float)random.NextDouble()) / cells);

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "Caustics (generated)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        var pixels = new Color[size * size];
        float floor = 1f - strength;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float first = 10f, second = 10f;
                foreach (Vector2 q in points)
                {
                    float dx = Mathf.Abs(p.x - q.x);
                    float dy = Mathf.Abs(p.y - q.y);
                    dx = Mathf.Min(dx, 1f - dx);
                    dy = Mathf.Min(dy, 1f - dy);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < first)
                    {
                        second = first;
                        first = d;
                    }
                    else if (d < second)
                        second = d;
                }
                float line = 1f - Mathf.Clamp01((second - first) / 0.07f);
                line *= line;
                float v = floor + strength * line;
                pixels[y * size + x] = new Color(v, v, v, v);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    // ---- the motes --------------------------------------------------------------------------------------------------

    private void CreateMotes()
    {
        var go = new GameObject("WaterMotes");
        go.transform.SetParent(transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop();

        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 16f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(motesSize * 0.5f, motesSize * 1.5f);
        main.startColor = new Color(0.8f, 0.95f, 1f, 0.4f);
        main.maxParticles = Mathf.Max(10, motesCount * 2);

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = motesCount / 12f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(motesArea, motesArea * 0.6f, motesArea);

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = motesDrift;
        noise.frequency = 0.15f;
        noise.scrollSpeed = 0.1f;
        noise.damping = true;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);   // all three the same mode, or Unity complains
        velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = motesMaterial != null ? motesMaterial : MakeMoteMaterial();

        system.Play();
        motesSystem = system;
    }

    // A soft round speck.
    public static Texture2D MakeMoteTexture(int size = 64)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "WaterMote (generated)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Material MakeMoteMaterial()
    {
        if (moteTexture == null)
            moteTexture = MakeMoteTexture();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = "WaterMotes (generated)", hideFlags = HideFlags.DontSave };
        ConfigureMoteMaterial(material, moteTexture);
        return material;
    }

    // Makes a URP particle material a soft alpha-blended speck: shared with the editor tool that saves one as an asset.
    public static void ConfigureMoteMaterial(Material material, Texture2D speck)
    {
        material.SetTexture("_BaseMap", speck);
        material.SetTexture("_MainTex", speck);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);   // transparent
        material.SetFloat("_Blend", 0f);     // alpha
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
