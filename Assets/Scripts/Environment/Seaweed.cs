using System.Collections.Generic;
using UnityEngine;

// Cartoon seaweed: a clump of flat, tapered leaves (generated meshes) like the kelp in an animated film. Each is a
// thin strip with rounded edges, a narrow stem, widest halfway up and narrowing to a round tip, waving gently side
// to side and front to back with a soft flutter along its edges, and painted from a deep base colour to a bright
// tip (a generated gradient with a lighter rib down the middle). The whole clump bends slowly from the base up in
// one shared current (together, symmetrically) with a wave travelling up each leaf and the odd slow swoop (or one
// on demand: Swoop(), wired to the bone-key socket).
// Any mesh under this object bends the same way, so to use a real model drop a prefab or an imported .obj/.fbx into
// Custom Model: it is scaled to Height, stood on the ground in the middle of the clump and painted with the same
// gradient (tick Read/Write in its import settings so it can sway). Or put the file in
// Assets/Art/Models/environment/seaweed and Rebuild Test Arena picks it up. The collider and the Item Socket stay on
// this root. Build() places the fronds (the arena builder calls it); their meshes and paint are regenerated whenever
// the component loads, so nothing procedural needs saving and the Scene view shows them too. Inspector changes show
// straight away.
[ExecuteAlways]
public class Seaweed : MonoBehaviour
{
    [Header("Model")]
    [Tooltip("Your own seaweed: a prefab or an imported model. It replaces the generated fronds and bends the same way. Empty = generated fronds.")]
    [SerializeField] private GameObject customModel;
    [Tooltip("Scale the custom model so it is Height tall and stand it on the ground in the middle of the clump.")]
    [SerializeField] private bool fitModel = true;
    [Tooltip("Give the custom model the blade material with the base-to-tip gradient. Off = it keeps its own materials.")]
    [SerializeField] private bool paintModel = true;
    [Tooltip("Material for the fronds (Assets/Art/Materials/Seaweed). The gradient is painted over a copy of it, the asset is untouched. Empty = a runtime copy of URP Lit.")]
    [SerializeField] private Material bladeMaterial;

    [Header("Cartoon leaves")]
    [SerializeField, Range(1, 64)] private int fronds = 18;
    [Tooltip("Rings up each leaf: more = smoother curves and a rounder tip.")]
    [SerializeField, Range(3, 48)] private int segments = 28;
    [Tooltip("Sides around each leaf: it is a closed strip with real (rounded) edges, not a single-sided plane.")]
    [SerializeField, Range(4, 16)] private int sides = 10;
    [Tooltip("Height of the tallest leaf, in metres. Others vary down to about a third.")]
    [SerializeField] private float height = 3.4f;
    [Tooltip("Radius of the clump the leaves stand in.")]
    [SerializeField] private float spread = 0.45f;
    [Tooltip("Degrees the leaves fan outward from the middle of the clump.")]
    [SerializeField] private float fan = 16f;
    [Tooltip("Width of a leaf at its widest and its thickness (front to back), in metres.")]
    [SerializeField] private float bladeWidth = 0.4f;
    [SerializeField] private float bladeThickness = 0.03f;
    [Tooltip("Flatness of the leaf in cross-section: 2 = an oval (looks like a tube), higher = a flat leaf with rounded edges.")]
    [SerializeField, Range(2f, 6f)] private float flatness = 3.5f;
    [Tooltip("The narrow stem at the bottom, as a fraction of the height.")]
    [SerializeField, Range(0.02f, 0.5f)] private float stemLength = 0.16f;
    [Tooltip("Where the leaf is widest, as a fraction of its height, and how wide the tip is compared to that.")]
    [SerializeField, Range(0.2f, 0.9f)] private float taperAt = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float tipWidth = 0.35f;
    [Tooltip("The round end of the tip, as a fraction of the height.")]
    [SerializeField, Range(0.02f, 0.4f)] private float tipRound = 0.16f;
    [Tooltip("How far the leaf snakes from side to side, as a fraction of its height, and how many S-waves fit up it.")]
    [SerializeField, Range(0f, 0.3f)] private float wiggle = 0.06f;
    [SerializeField, Range(0.5f, 4f)] private float waves = 1.8f;
    [Tooltip("How much of that wave goes front to back instead (the leaf waving in the current), as a fraction of Wiggle.")]
    [SerializeField, Range(0f, 1.5f)] private float waveOut = 0.6f;
    [Tooltip("Gentle swelling and narrowing of the width along the leaf, and a finer scallop on its outline.")]
    [SerializeField, Range(0f, 0.4f)] private float bulge = 0.1f;
    [SerializeField, Range(0f, 0.4f)] private float edgeWave = 0.06f;
    [Tooltip("How much the edges flutter up and down out of the leaf (the midrib stays put), as a fraction of the width, and how many waves of it.")]
    [SerializeField, Range(0f, 0.6f)] private float ruffle = 0.08f;
    [SerializeField, Range(0.5f, 8f)] private float ruffleWaves = 2.5f;
    [Tooltip("Degrees the leaf twists along its length.")]
    [SerializeField] private float twist = 8f;
    [Tooltip("How far the tip leans over at rest, in metres.")]
    [SerializeField] private float curl = 0.25f;
    [SerializeField] private int seed = 7;

    [Header("Paint")]
    [Tooltip("Colour at the base and at the tip; each blade runs from one to the other.")]
    [SerializeField] private Color color = new Color(0.035f, 0.3f, 0.22f);
    [SerializeField] private Color tipColor = new Color(0.3f, 0.64f, 0.26f);
    [Tooltip("How much lighter the rib down the middle of each face is.")]
    [SerializeField, Range(0f, 0.6f)] private float veinLight = 0.25f;
    [Tooltip("How much darker the rounded edges are. Keep it low or a leaf starts to look like a tube.")]
    [SerializeField, Range(0f, 0.6f)] private float edgeDark = 0.08f;
    [Tooltip("Toy-like shine.")]
    [SerializeField, Range(0f, 1f)] private float shine = 0.25f;
    [Tooltip("A little self-light so the colours stay bright in the dark water. 0 = none.")]
    [SerializeField, Range(0f, 1f)] private float glow = 0.03f;
    [Tooltip("How much the fronds differ from each other in brightness and hue.")]
    [SerializeField, Range(0f, 0.5f)] private float variety = 0.12f;
    [Tooltip("The mound the fronds grow out of.")]
    [SerializeField] private Color rootColor = new Color(0.16f, 0.14f, 0.1f);

    [Header("Sway")]
    [Tooltip("The whole clump sways together, back and forth along Sway Direction, like one current pushing it. Off = every frond in its own direction and time.")]
    [SerializeField] private bool swayTogether = true;
    [Tooltip("World-space direction the clump leans along (it swings both ways).")]
    [SerializeField] private Vector3 swayDirection = new Vector3(1f, 0f, 0.3f);
    [Tooltip("With Sway Together: how far apart the fronds are in their timing, as a fraction of a cycle. 0 = perfectly in step.")]
    [SerializeField, Range(0f, 0.5f)] private float phaseSpread = 0.05f;
    [Tooltip("How far the tips move in the ordinary current, in metres.")]
    [SerializeField] private float swayMetres = 0.35f;
    [Tooltip("Cycles per second of the ordinary sway. 0.1 = one slow swing every ten seconds.")]
    [SerializeField] private float swaySpeed = 0.1f;
    [Tooltip("How much the tip lags behind the base, as a fraction of a cycle: the wave that travels up the blade.")]
    [SerializeField, Range(0f, 0.5f)] private float tipLag = 0.22f;
    [Tooltip("The ripple running up each frond: size in metres and cycles per second.")]
    [SerializeField] private float rippleMetres = 0.02f;
    [SerializeField] private float rippleSpeed = 0.3f;
    [Tooltip("Seconds between the stronger swoops, chosen at random in this range.")]
    [SerializeField] private Vector2 gustEvery = new Vector2(9f, 18f);
    [Tooltip("How much stronger a swoop is than the ordinary sway, and how long it takes.")]
    [SerializeField] private float gustStrength = 1.8f;
    [SerializeField] private float gustSeconds = 4.5f;

    [Header("Reacts to the player")]
    [Tooltip("The player (found automatically: the Swim Controller). The leaves part around their body and are dragged along by their wake, then swing back.")]
    [SerializeField] private Transform player;
    [Tooltip("How far beyond the player's body a leaf is pushed aside, in metres (their collider radius is added).")]
    [SerializeField] private float touchRadius = 0.35f;
    [Tooltip("How far a touched leaf bends away per metre the player overlaps it. 1 = it gets out of the way exactly.")]
    [SerializeField] private float touchStrength = 1.1f;
    [Tooltip("Leaves within this distance of the player, in metres, are dragged along by their movement.")]
    [SerializeField] private float wakeRadius = 1.8f;
    [Tooltip("How far the wake bends a leaf, in metres, per metre per second of player speed (at the player, fading to nothing at Wake Radius).")]
    [SerializeField] private float wakeStrength = 0.12f;
    [Tooltip("The most the player can push a leaf tip, in metres.")]
    [SerializeField] private float pushLimit = 0.9f;
    [Tooltip("How hard a leaf springs back after being pushed, and how quickly the swinging dies away (lower = more wobble).")]
    [SerializeField] private float springStiffness = 30f;
    [SerializeField] private float springDamping = 5f;

    // What the last Build() used, so a swapped model or paint setting rebuilds on load.
    [SerializeField, HideInInspector] private GameObject builtModel;
    [SerializeField, HideInInspector] private bool builtPainted;

    private class Blade
    {
        public Transform transform;
        public Mesh mesh;
        public Vector3[] rest;
        public Vector3[] work;
        public float[] height01;
        public float phase;
        public Vector3 direction;   // world-space direction of its sway
        public Vector3 side;        // world-space direction of its ripple
        public float worldHeight;   // how tall it stands, in metres
        public Vector3 push;        // where the player has pushed it to (world space, horizontal), on a spring
        public Vector3 pushVelocity;
    }

    private const int PaintWidth = 32;
    private const int PaintHeight = 64;
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly List<Blade> blades = new List<Blade>();
    private float nextGust;
    private float gustStart = -100f;
    private float gustPower;
    private Vector3 gustDirection = Vector3.right;
    private Material runtimeMaterial;
    private Material paint;
    private Texture2D gradient;
    private Transform playerFound;
    private CharacterController playerController;
    private Vector3 playerLastPosition;
    private Vector3 playerVelocitySmoothed;
    private bool playerHasLast;

    private void OnEnable()
    {
        if (NeedsBuild())
            Build();
        else
            Collect();
        nextGust = Time.time + Random.Range(gustEvery.x, gustEvery.y);
        if (Application.isPlaying && player == null && playerFound == null)
        {
            SwimController swimmer = FindFirstObjectByType<SwimController>();
            if (swimmer != null)
                playerFound = swimmer.transform;
        }
    }

    private void OnDisable()
    {
        blades.Clear();
    }

    private void OnDestroy()
    {
        SafeDestroy(paint);
        SafeDestroy(gradient);
        SafeDestroy(runtimeMaterial);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Inspector changes show straight away: the clump is rebuilt (deferred, as Unity does not like objects being
        // made inside OnValidate). Cheap: a dozen small meshes.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || !isActiveAndEnabled || Application.isPlaying)
                return;
            Build();
        };
    }
#endif

    // Nothing here yet, the frond count changed, or the custom model (or whether it is painted) changed.
    private bool NeedsBuild()
    {
        if (customModel != null)
            return builtModel != customModel || builtPainted != paintModel || transform.Find("Model") == null;
        return builtModel != null || GeneratedFrondCount() != fronds;
    }

    private int GeneratedFrondCount()
    {
        int count = 0;
        foreach (Transform child in transform)
            if (child.GetComponent<SeaweedFrond>() != null)
                count++;
        return count;
    }

    // A big swoop right now (the socket fires this when the bone key is made).
    public void Swoop()
    {
        StartGust(gustStrength * 1.5f);
    }

    // Places the fronds (or the custom model). Safe to call again: the old ones are cleared first.
    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("Frond") || child.name == "Roots" || child.name == "Model")
                SafeDestroy(child.gameObject);
        }
        builtModel = customModel;
        builtPainted = paintModel;

        // A small dark rock, mostly sunk into the floor, for the leaves to grow out of.
        GameObject roots = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        roots.name = "Roots";
        SafeDestroy(roots.GetComponent<Collider>());
        roots.transform.SetParent(transform, false);
        roots.transform.localPosition = new Vector3(0f, -spread * 0.2f, 0f);
        roots.transform.localScale = new Vector3(spread * 1.1f, spread * 0.9f, spread * 1.1f);
        roots.AddComponent<RendererTint>().Tint = rootColor;

        if (customModel != null)
        {
            GameObject model = Instantiate(customModel, transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            if (fitModel)
                FitModel(model.transform);
        }
        else
        {
            var rng = new System.Random(seed);
            for (int f = 0; f < fronds; f++)
            {
                // Spread over the whole clump (not just a rim), the tall ones toward the middle, shorter ones around them.
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius01 = Mathf.Sqrt((float)rng.NextDouble());
                float radius = spread * radius01;
                var frond = new GameObject("Frond " + (f + 1));
                frond.transform.SetParent(transform, false);
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                frond.transform.localPosition = outward * radius + Vector3.down * 0.05f;   // rooted a little into the mound
                // Flat face outward (roughly) and leaning outward a little, more so at the edge, like a clump opening up.
                float yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg + Mathf.Lerp(-35f, 35f, (float)rng.NextDouble());
                float lean = fan * Mathf.Lerp(0.3f, 1.2f, radius01) * Mathf.Lerp(0.6f, 1.3f, (float)rng.NextDouble());
                frond.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(lean, 0f, 0f);

                var shape = frond.AddComponent<SeaweedFrond>();
                float tallness = (1f - radius01) * 0.55f + (float)rng.NextDouble() * 0.45f;
                shape.height = height * Mathf.Lerp(0.35f, 1f, tallness);
                shape.widthScale = Mathf.Lerp(0.85f, 1.15f, (float)rng.NextDouble());
                shape.curl = curl * Mathf.Lerp(0.5f, 1.4f, (float)rng.NextDouble());
                shape.phase = (float)rng.NextDouble();
                shape.shade = Mathf.Lerp(-1f, 1f, (float)rng.NextDouble());

                frond.AddComponent<MeshFilter>();
                var renderer = frond.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // A little brighter or darker, a little warmer or cooler, so they are not all the same green.
                float warm = Mathf.Lerp(-1f, 1f, (float)rng.NextDouble());
                frond.AddComponent<RendererTint>().Tint = new Color(
                    1f + variety * (shape.shade + warm) * 0.5f,
                    1f + variety * shape.shade * 0.5f,
                    1f + variety * (shape.shade - warm) * 0.5f);
            }
        }
        Collect();
    }

    // Scales the custom model to Height and stands it on the ground in the middle of the clump.
    private void FitModel(Transform model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (Renderer r in renderers)
        {
            Bounds b = r.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                Vector3 local = transform.InverseTransformPoint(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }
        float tall = max.y - min.y;
        if (tall < 0.0001f)
            return;
        float scale = height / tall;
        model.localScale = Vector3.one * scale;
        Vector3 centre = (min + max) * 0.5f;
        model.localPosition = new Vector3(-centre.x * scale, -min.y * scale, -centre.z * scale);
    }

    private Material BladeMaterial()
    {
        if (bladeMaterial != null)
            return bladeMaterial;
        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            runtimeMaterial = new Material(shader) { name = "Seaweed (runtime)", hideFlags = HideFlags.DontSave };
            runtimeMaterial.SetFloat("_Cull", 0f);
        }
        return runtimeMaterial;
    }

    // The blade material with the base-to-tip gradient painted over it: a copy, so the asset is untouched. The paint
    // is a small texture, v running base to tip and u around the blade (edges darker, a lighter rib down each face).
    private Material Paint()
    {
        Material source = BladeMaterial();
        if (paint == null)
            paint = new Material(source) { name = source.name + " (painted)", hideFlags = HideFlags.DontSave };
        if (gradient == null)
        {
            gradient = new Texture2D(PaintWidth, PaintHeight, TextureFormat.RGBA32, false)
            {
                name = "Seaweed gradient",
                hideFlags = HideFlags.DontSave,
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }
        var pixels = new Color32[PaintWidth * PaintHeight];
        for (int y = 0; y < PaintHeight; y++)
        {
            float t = y / (float)(PaintHeight - 1);
            Color c = Color.Lerp(color, tipColor, Mathf.SmoothStep(0f, 1f, t));
            for (int x = 0; x < PaintWidth; x++)
            {
                float face = Mathf.Abs(Mathf.Sin((x + 0.5f) / PaintWidth * Mathf.PI * 2f));   // 1 mid-face, 0 at the edges
                float rib = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 1f, face));   // only right down the middle
                float light = Mathf.Lerp(1f - edgeDark, 1f, face) + rib * veinLight * (1f - t * 0.6f);
                Color p = c * light;
                p.a = 1f;
                pixels[y * PaintWidth + x] = p;
            }
        }
        gradient.SetPixels32(pixels);
        gradient.Apply(false, false);

        paint.SetTexture(BaseMapId, gradient);
        if (paint.HasProperty(SmoothnessId))
            paint.SetFloat(SmoothnessId, shine);
        if (paint.HasProperty(EmissionColorId))
        {
            if (glow > 0f)
            {
                paint.EnableKeyword("_EMISSION");
                paint.SetTexture(EmissionMapId, gradient);
                paint.SetColor(EmissionColorId, Color.white * glow);
            }
            else
            {
                paint.DisableKeyword("_EMISSION");
            }
        }
        return paint;
    }

    // Gathers every mesh under this object as a blade to bend, regenerating the meshes and paint of generated fronds
    // and painting the custom model.
    private void Collect()
    {
        blades.Clear();
        Material painted = Paint();
        var rng = new System.Random(seed + 1);
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.transform == transform || filter.name == "Roots")
                continue;
            SeaweedFrond shape = filter.GetComponent<SeaweedFrond>();
            var renderer = filter.GetComponent<Renderer>();
            Mesh mesh;
            if (shape != null)
            {
                mesh = FrondMesh(shape);
                filter.sharedMesh = mesh;
                if (renderer != null)
                    renderer.sharedMaterial = painted;
            }
            else
            {
                // Part of the custom model.
                if (paintModel && renderer != null)
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = painted;
                    renderer.sharedMaterials = materials;
                }
                if (!Application.isPlaying)
                    continue;   // in edit mode its shared mesh is left alone (it only sways in play mode anyway)
                Mesh source = filter.sharedMesh;
                if (source == null)
                    continue;
                if (!source.isReadable)
                {
                    Debug.LogWarning($"Seaweed: {source.name} cannot sway because its mesh is not readable. Tick Read/Write in the import settings of the model.", filter);
                    continue;
                }
                mesh = filter.mesh;   // our own copy of the mesh of the model, ours to bend
            }

            Vector3[] rest = mesh.vertices;
            var blade = new Blade
            {
                transform = filter.transform,
                mesh = mesh,
                rest = rest,
                work = new Vector3[rest.Length],
                height01 = new float[rest.Length],
                phase = (shape != null ? shape.phase : (float)rng.NextDouble()) * (swayTogether ? phaseSpread : 1f),
            };
            float minY = mesh.bounds.min.y, maxY = mesh.bounds.max.y;
            for (int i = 0; i < rest.Length; i++)
                blade.height01[i] = maxY > minY ? Mathf.Clamp01((rest[i].y - minY) / (maxY - minY)) : 0f;
            blade.worldHeight = (maxY - minY) * Mathf.Abs(filter.transform.lossyScale.y);

            if (shape == null && paintModel)
            {
                // Run the gradient up the model, bottom to top, partway between an edge and the middle of a face.
                var uv = new Vector2[rest.Length];
                for (int i = 0; i < rest.Length; i++)
                    uv[i] = new Vector2(0.125f, blade.height01[i]);
                mesh.uv = uv;
            }

            Vector3 flat = new Vector3(swayDirection.x, 0f, swayDirection.z);
            Vector3 direction = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.right;
            if (!swayTogether)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            blade.direction = direction;
            blade.side = new Vector3(-direction.z, 0f, direction.x);
            mesh.MarkDynamic();
            blades.Add(blade);
        }
    }

    // A cartoon blade: a fat rounded ribbon (a tube of Sides verts per ring, elliptical, so its edges are round) with a
    // narrow stem, an S-curve snaking up it, a gentle swell along the body and a round spoon tip. Each ring sits
    // square to the curve, so the ribbon keeps its width round the bends. Rings pack tighter toward the tip.
    private Mesh FrondMesh(SeaweedFrond shape)
    {
        int rings = Mathf.Max(3, segments + 1);
        int around = Mathf.Max(4, sides);
        var verts = new Vector3[rings * around];
        var uvs = new Vector2[rings * around];
        var tris = new int[(rings - 1) * around * 6];
        float phase = shape.phase * Mathf.PI * 2f;
        float amp = wiggle * shape.height;
        for (int r = 0; r < rings; r++)
        {
            float h = RingHeight(r, rings);
            Vector3 centre = Centreline(h, shape, amp, phase);
            Vector3 tangent = Centreline(Mathf.Min(1f, h + 0.01f), shape, amp, phase) - Centreline(Mathf.Max(0f, h - 0.01f), shape, amp, phase);
            tangent = tangent.sqrMagnitude > 1e-8f ? tangent.normalized : Vector3.up;
            Vector3 across = Vector3.Cross(tangent, Vector3.forward).normalized;   // the width, square to the curve
            Vector3 through = Vector3.Cross(across, tangent).normalized;            // the thickness, front to back
            float turn = twist * Mathf.Deg2Rad * Mathf.Sin(h * 2.2f + phase);
            Vector3 wide = across * Mathf.Cos(turn) + through * Mathf.Sin(turn);
            Vector3 deep = through * Mathf.Cos(turn) - across * Mathf.Sin(turn);

            float stem = Mathf.SmoothStep(0.3f, 1f, Mathf.Clamp01(h / stemLength));
            float body = 1f + bulge * Mathf.Sin(h * 7.3f + phase) + edgeWave * Mathf.Sin(h * 11f + phase * 1.3f) * Mathf.Clamp01(h / 0.3f);
            float tipT = Mathf.InverseLerp(1f - tipRound, 1f, h);
            float cap = Mathf.Sqrt(Mathf.Max(0f, 1f - tipT * tipT));               // a semicircle: the round tip
            // Widest at Taper At, narrowing to Tip Width toward the end: a tongue, not a strap.
            float profile = h < taperAt
                ? Mathf.Lerp(0.8f, 1f, Mathf.SmoothStep(0f, 1f, h / taperAt))
                : Mathf.Lerp(1f, tipWidth, Mathf.SmoothStep(0f, 1f, (h - taperAt) / (1f - taperAt)));
            float width = Mathf.Max(0.004f, bladeWidth * shape.widthScale * stem * body * cap * profile);
            float thick = Mathf.Max(0.003f, bladeThickness * shape.widthScale * Mathf.Lerp(1f, 0.55f, h) * Mathf.Max(stem, 0.5f) * Mathf.Max(cap, 0.3f));
            // The two edges flutter up and down out of the leaf, each to its own rhythm; the midrib stays put.
            float flutter = ruffle * width * stem * Mathf.Max(cap, 0.2f);
            float ruffleLeft = Mathf.Sin(h * ruffleWaves * Mathf.PI * 2f + phase * 1.7f) * flutter;
            float ruffleRight = Mathf.Sin(h * ruffleWaves * Mathf.PI * 2f + phase * 1.7f + 2.1f) * flutter;
            for (int k = 0; k < around; k++)
            {
                float a = k / (float)around * Mathf.PI * 2f;
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);
                // A flattened cross-section (a superellipse): flat faces, rounded edges.
                float fx = Mathf.Sign(ca) * Mathf.Pow(Mathf.Abs(ca), 2f / flatness);
                float fz = Mathf.Sign(sa) * Mathf.Pow(Mathf.Abs(sa), 2f / flatness);
                float lift = (ca >= 0f ? ruffleLeft : ruffleRight) * Mathf.Pow(Mathf.Abs(ca), 1.6f);
                verts[r * around + k] = centre + wide * (fx * width * 0.5f) + deep * (fz * thick * 0.5f + lift);
                // u runs edge (0) - face (0.25) - edge (0.5) and back, so the paint wraps without a seam.
                float u = k / (float)around;
                uvs[r * around + k] = new Vector2(u <= 0.5f ? u : 1f - u, h);
            }
            if (r < rings - 1)
            {
                for (int k = 0; k < around; k++)
                {
                    int a = r * around + k;
                    int b = r * around + (k + 1) % around;
                    int c = a + around;
                    int d = b + around;
                    int t = (r * around + k) * 6;
                    tris[t] = a; tris[t + 1] = c; tris[t + 2] = b;
                    tris[t + 3] = b; tris[t + 4] = c; tris[t + 5] = d;
                }
            }
        }
        var mesh = new Mesh { name = "Seaweed frond", hideFlags = HideFlags.DontSave };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float RingHeight(int ring, int rings)
    {
        float t = ring / (float)(rings - 1);
        return 1f - Mathf.Pow(1f - t, 1.4f);
    }

    // The path up the middle of a blade: an S-curve that grows toward the tip, a subtler one front to back, and a lean.
    private Vector3 Centreline(float h, SeaweedFrond shape, float amp, float phase)
    {
        float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(h / 0.3f)) * Mathf.Lerp(0.35f, 1f, h);
        float x = Mathf.Sin(h * waves * Mathf.PI * 2f + phase) * amp * grow;
        float z = Mathf.Sin(h * waves * Mathf.PI * 1.3f + phase * 0.7f) * amp * waveOut * h + shape.curl * h * h;
        return new Vector3(x, h * shape.height, z);
    }

    private void StartGust(float power)
    {
        gustStart = Time.time;
        gustPower = power;
        Vector3 flat = new Vector3(swayDirection.x, 0f, swayDirection.z);
        if (swayTogether && flat.sqrMagnitude > 0.0001f)
        {
            // Along the same line as the sway, either way, so the clump keeps swinging symmetrically.
            gustDirection = flat.normalized * (Random.value < 0.5f ? -1f : 1f);
            return;
        }
        float angle = Random.value * Mathf.PI * 2f;
        gustDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    private void Update()
    {
        if (!Application.isPlaying || blades.Count == 0)
            return;

        float now = Time.time;
        if (now >= nextGust)
        {
            StartGust(gustStrength);
            nextGust = now + Random.Range(gustEvery.x, gustEvery.y);
        }
        float g = gustSeconds > 0f ? (now - gustStart) / gustSeconds : 1f;
        float gust = g >= 0f && g < 1f ? Mathf.Sin(g * Mathf.PI) * gustPower : 0f;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        bool playerNear = PlayerBody(dt, out Vector3 bodyCentre, out float bodyHalf, out float bodyRadius, out Vector3 playerVelocity);

        foreach (Blade blade in blades)
        {
            if (blade.mesh == null || blade.transform == null)
                continue;

            // Pushed aside by the player and dragged by their wake, on a spring, so it swings back once they have passed.
            Vector3 target = playerNear ? PlayerPush(blade, bodyCentre, bodyHalf, bodyRadius, playerVelocity) : Vector3.zero;
            blade.pushVelocity += (target - blade.push) * (springStiffness * dt);
            blade.pushVelocity *= Mathf.Exp(-springDamping * dt);
            blade.push += blade.pushVelocity * dt;
            Vector3 shoveLocal = blade.transform.InverseTransformVector(blade.push);

            // The push on this blade, in its own space: the slow sway (the tip lagging the base) plus any swoop.
            float cycle = now * swaySpeed + blade.phase;
            Vector3 swayLocal = blade.transform.InverseTransformVector(blade.direction * swayMetres);
            Vector3 gustLocal = blade.transform.InverseTransformVector(gustDirection * (swayMetres * gust));
            Vector3 side = blade.transform.InverseTransformVector(blade.side * rippleMetres);

            for (int i = 0; i < blade.rest.Length; i++)
            {
                float h = blade.height01[i];
                float bend = Mathf.Pow(h, 1.7f);                       // rooted at the base, moving most at the tip
                float shove = Mathf.Pow(h, 1.3f);                      // a body pushes most of the leaf, not just the tip
                float wave = Mathf.Sin((cycle - h * tipLag) * Mathf.PI * 2f);
                Vector3 moved = (swayLocal * wave + gustLocal) * bend + shoveLocal * shove;
                Vector3 p = blade.rest[i] + moved;
                p += side * (Mathf.Sin(h * 6f - now * rippleSpeed * Mathf.PI * 2f + blade.phase * 6.28f) * h);
                p.y -= moved.sqrMagnitude * 0.5f;                       // a bent blade does not get longer
                blade.work[i] = p;
            }
            blade.mesh.vertices = blade.work;
            blade.mesh.RecalculateNormals();
        }
    }

    // The player's body as an upright capsule (centre, half height, radius) and their smoothed horizontal velocity,
    // taken from their position so it works with any controller. False when there is no player.
    private bool PlayerBody(float dt, out Vector3 centre, out float half, out float radius, out Vector3 velocity)
    {
        Transform body = player != null ? player : playerFound;
        if (body == null)
        {
            centre = Vector3.zero;
            half = 0f;
            radius = 0f;
            velocity = Vector3.zero;
            return false;
        }
        if (playerController == null || playerController.transform != body)
            playerController = body.GetComponent<CharacterController>();
        if (playerController != null)
        {
            centre = body.TransformPoint(playerController.center);
            half = playerController.height * 0.5f * Mathf.Abs(body.lossyScale.y);
            radius = playerController.radius * Mathf.Max(Mathf.Abs(body.lossyScale.x), Mathf.Abs(body.lossyScale.z));
        }
        else
        {
            centre = body.position + Vector3.up * 0.9f;
            half = 0.9f;
            radius = 0.35f;
        }
        if (dt > 0f)
        {
            Vector3 raw = playerHasLast ? (body.position - playerLastPosition) / dt : Vector3.zero;
            playerVelocitySmoothed = Vector3.Lerp(playerVelocitySmoothed, raw, 1f - Mathf.Exp(-8f * dt));
        }
        playerLastPosition = body.position;
        playerHasLast = true;
        velocity = new Vector3(playerVelocitySmoothed.x, 0f, playerVelocitySmoothed.z);
        return true;
    }

    // Where the player pushes this leaf to (world space, horizontal): away from their body where it overlaps the leaf,
    // plus along their movement when they are close (the wake). Capped at Push Limit.
    private Vector3 PlayerPush(Blade blade, Vector3 centre, float half, float radius, Vector3 velocity)
    {
        Vector3 foot = blade.transform.position;
        Vector3 toLeaf = foot - centre;
        toLeaf.y = 0f;
        float distance = toLeaf.magnitude;
        Vector3 target = Vector3.zero;

        float reach = radius + touchRadius;
        bool overlapsHeight = centre.y + half > foot.y && centre.y - half < foot.y + blade.worldHeight;
        if (overlapsHeight && distance < reach)
        {
            Vector3 away = distance > 0.01f ? toLeaf / distance : (velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector3.right);
            target += away * ((reach - distance) * touchStrength);
        }
        if (distance < wakeRadius)
            target += velocity * (wakeStrength * (1f - distance / wakeRadius));
        // Never more than about half the leaf's own height, so a short leaf bends rather than lies flat.
        return Vector3.ClampMagnitude(target, Mathf.Min(pushLimit, blade.worldHeight * 0.55f));
    }

    private static void SafeDestroy(Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
