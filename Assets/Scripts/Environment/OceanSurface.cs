using UnityEngine;

// What you see looking up out of the broken ship: the underside of the ocean surface high overhead (a shimmering
// sheet of light) and the sun burning through it. Both are quads made at start: the surface lies flat at Height and
// follows the camera sideways, so it never ends; the sun sits Sun Distance away along the sun's light (the scene's
// directional light, the one Underwater Lighting drives) and always faces the camera. The roof hides them both, so
// they show through the holes in it and the windows. Materials use the Ocean Surface and Sun Glow shaders
// (Resources/Shaders), made on the spot if none are set.
public class OceanSurface : MonoBehaviour
{
    [Tooltip("World height of the surface, in metres.")]
    [SerializeField] private float height = 26f;
    [Tooltip("How wide the surface sheet is, in metres (it fades out well before its edge).")]
    [SerializeField] private float size = 300f;
    [SerializeField] private Material surfaceMaterial;

    [Header("Sun")]
    [Tooltip("The directional light it follows. Empty = the scene's sun.")]
    [SerializeField] private Light sun;
    [SerializeField] private Material sunMaterial;
    [Tooltip("How far from the camera the sun is drawn, in metres (inside the camera's far clip).")]
    [SerializeField] private float sunDistance = 80f;
    [Tooltip("How big the sun's glow is, in metres at that distance.")]
    [SerializeField] private float sunSize = 26f;

    private Transform surface;
    private Transform sunQuad;

    private void Start()
    {
        if (surfaceMaterial == null)
            surfaceMaterial = Make("Out of the Depths/Ocean Surface");
        if (sunMaterial == null)
            sunMaterial = Make("Out of the Depths/Sun Glow");
        if (sun == null)
            sun = RenderSettings.sun;
        if (sun == null)
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }

        if (surfaceMaterial != null)
        {
            surface = Quad("Surface", surfaceMaterial).transform;
            surface.rotation = Quaternion.Euler(90f, 0f, 0f);   // flat, facing up (seen from below through Cull Off)
            surface.localScale = new Vector3(size, size, 1f);
        }
        if (sunMaterial != null && sun != null)
        {
            sunQuad = Quad("Sun", sunMaterial).transform;
            sunQuad.localScale = new Vector3(sunSize, sunSize, 1f);
        }
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;
        Vector3 eye = camera.transform.position;
        if (surface != null)
            surface.position = new Vector3(eye.x, height, eye.z);
        if (sunQuad != null && sun != null)
        {
            Vector3 toSun = -sun.transform.forward;
            float distance = Mathf.Min(sunDistance, camera.farClipPlane * 0.9f);
            sunQuad.position = eye + toSun * distance;
            sunQuad.rotation = Quaternion.LookRotation(sunQuad.position - eye);
        }
    }

    private GameObject Quad(string label, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = label;
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return quad;
    }

    private static Material Make(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        return shader != null ? new Material(shader) { name = shaderName + " (generated)" } : null;
    }
}
