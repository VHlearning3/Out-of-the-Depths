using UnityEngine;

// A flat panel with its material tiled in metres, so a texture keeps one size on every surface (a 16 m wall shows 16 m
// of stone, not one stretched picture): a quad of Size facing the object's +Y (turn the object to make it a wall or a
// ceiling), Tile Metres of surface per repeat of the texture. Offset is where on the texture the panel's middle falls,
// in metres: panels side by side on one wall (or floor) line up when it is their middle's position along it.
// Normals and tangents are set, so a normal map lights the right way. No collider: it lines a wall, floor or slab
// that already has one. The mesh is made here (nothing to import), in the editor too. Room Dress Tools lays these.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TiledSurface : MonoBehaviour
{
    [Tooltip("Metres along the object's X and Z. The panel is centred on the object and faces its +Y.")]
    [SerializeField] private Vector2 size = new Vector2(4f, 4f);
    [Tooltip("Metres of surface per repeat of the texture, both ways.")]
    [SerializeField, Min(0.01f)] private float tileMetres = 2f;
    [Tooltip("Where on the texture the panel's middle falls, in metres (X, Z).")]
    [SerializeField] private Vector2 offset;
    [SerializeField] private Material material;
    [SerializeField] private bool castShadows;

    private Mesh mesh;

    public Vector2 Size => size;

    public void Setup(Vector2 metres, float tile, Vector2 middle, Material shared)
    {
        size = metres;
        tileMetres = tile;
        offset = middle;
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
        if (mesh == null)
            return;
        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);
        mesh = null;
    }

    public void Build()
    {
        if (mesh == null)
            mesh = new Mesh { name = "Tiled surface", hideFlags = HideFlags.DontSave };

        float hx = size.x * 0.5f, hz = size.y * 0.5f;
        float tile = Mathf.Max(0.01f, tileMetres);
        var vertices = new[] { new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz) };
        var uvs = new Vector2[4];
        for (int i = 0; i < 4; i++)
            uvs[i] = new Vector2((offset.x + vertices[i].x) / tile, (offset.y + vertices[i].z) / tile);
        var tangent = new Vector4(1f, 0f, 0f, -1f);   // U along +X, V along +Z

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.tangents = new[] { tangent, tangent, tangent, tangent };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
