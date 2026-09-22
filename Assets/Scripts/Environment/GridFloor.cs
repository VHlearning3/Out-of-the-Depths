using UnityEngine;

// A floor patch with a metre grid drawn on it (greybox aid): a flat quad whose texture coordinates are world metres,
// so the lines run straight on from room to room, with a heavier line every Major cells. Tint colours the floor
// between the lines. The quad is generated here (nothing to import); the material is the shared GridFloor.mat the
// builders make, or, if none is set, one made at start from the same generated texture.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridFloor : MonoBehaviour
{
    [Tooltip("Metres, x and z. The patch is centred on this object.")]
    [SerializeField] private Vector2 size = new Vector2(10f, 10f);
    [SerializeField] private Color tint = new Color(0.25f, 0.3f, 0.35f);
    [Tooltip("Metres between grid lines.")]
    [SerializeField] private float cell = 1f;
    [Tooltip("Every this many cells the line is heavier.")]
    [SerializeField, Min(1)] private int major = 5;
    [Tooltip("Empty = a material is made at start from the generated grid texture. Assign GridFloor.mat to share one.")]
    [SerializeField] private Material material;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    private Mesh mesh;
    private Material runtimeMaterial;
    private MaterialPropertyBlock block;

    public void Setup(Vector2 metres, Color color, Material shared)
    {
        size = metres;
        tint = color;
        material = shared;
        Build();
    }

    private void OnEnable()
    {
        Build();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled)
            Build();
    }

    private void OnDisable()
    {
        Release(mesh);
        Release(runtimeMaterial);
        mesh = null;
        runtimeMaterial = null;
    }

    public void Build()
    {
        if (mesh == null)
            mesh = new Mesh { name = "Grid floor", hideFlags = HideFlags.DontSave };

        // A quad centred on the object; texture coordinates are world metres over one major block, so the grid lines
        // land on whole metres everywhere and match up between neighbouring patches.
        float hx = size.x * 0.5f, hz = size.y * 0.5f;
        var vertices = new[] { new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz) };
        var uvs = new Vector2[4];
        float blockMetres = Mathf.Max(0.01f, cell * Mathf.Max(1, major));
        for (int i = 0; i < 4; i++)
        {
            Vector3 world = transform.TransformPoint(vertices[i]);
            uvs[i] = new Vector2(world.x / blockMetres, world.z / blockMetres);
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.sharedMaterial = material != null ? material : RuntimeMaterial();

        if (block == null)
            block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, tint);
        renderer.SetPropertyBlock(block);
    }

    private Material RuntimeMaterial()
    {
        if (runtimeMaterial != null)
            return runtimeMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        runtimeMaterial = new Material(shader) { name = "Grid floor (runtime)", hideFlags = HideFlags.DontSave };
        Texture2D texture = MakeTexture(Mathf.Max(1, major));
        texture.hideFlags = HideFlags.DontSave;
        runtimeMaterial.SetTexture(BaseMapId, texture);
        runtimeMaterial.SetTexture("_MainTex", texture);
        return runtimeMaterial;
    }

    // One major block of the grid: major x major cells, 64 px each, white with grey cell lines and a darker heavier
    // line on the block edge. Tiled with Repeat it becomes the whole grid.
    public static Texture2D MakeTexture(int majorCells)
    {
        const int cellPx = 64;
        int px = cellPx * Mathf.Max(1, majorCells);
        var texture = new Texture2D(px, px, TextureFormat.RGBA32, true)
        {
            name = "GridFloor",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8,
        };
        var pixels = new Color32[px * px];
        var floor = new Color32(255, 255, 255, 255);
        var minor = new Color32(150, 150, 150, 255);
        var heavy = new Color32(95, 95, 95, 255);
        for (int y = 0; y < px; y++)
        {
            for (int x = 0; x < px; x++)
            {
                bool heavyLine = x < 2 || x >= px - 2 || y < 2 || y >= px - 2;
                bool minorLine = x % cellPx < 1 || x % cellPx == cellPx - 1 || y % cellPx < 1 || y % cellPx == cellPx - 1;
                pixels[y * px + x] = heavyLine ? heavy : minorLine ? minor : floor;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(true, false);
        return texture;
    }

    private static void Release(Object o)
    {
        if (o == null)
            return;
        if (Application.isPlaying)
            Destroy(o);
        else
            DestroyImmediate(o);
    }
}
