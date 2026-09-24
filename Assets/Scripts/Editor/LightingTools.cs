using UnityEditor;
using UnityEngine;

// The lighting pieces both scene builders use, so the ship and the test arena look alike: plain fill lights, spot
// beams (the water light slanting in through windows, warm accents on what matters), faint light shafts, and
// the high sun with the ocean surface overhead. Materials it needs are saved once under Art.
internal static class LightingTools
{
    // The sun: high, so looking up you see it (through the ship's roof holes, over the arena's open top) and its
    // light falls steeply in.
    internal static readonly Quaternion SunRotation = Quaternion.Euler(68f, -30f, 0f);

    internal static readonly Color WaterBeam = new Color(0.5f, 0.85f, 1f);
    internal static readonly Color WaterShaft = new Color(0.5f, 0.85f, 1f, 0.26f);
    internal static readonly Color AccentColor = new Color(1f, 0.88f, 0.7f);

    private const string ShaftMaterialPath = "Assets/Art/Materials/LightShaft.mat";
    private const string ShaftTexturePath = "Assets/Art/Textures/LightShaft.png";

    internal static void RoomLight(string name, Vector3 position, Color color, float intensity, float range, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    // A spot light from `from` along `direction`. Shadows on = soft shadows, so walls and window frames shape it.
    internal static void BeamLight(string name, Vector3 from, Vector3 direction, float angle, float intensity, float range, bool shadows, Color color, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction.normalized, Mathf.Abs(direction.normalized.y) > 0.95f ? Vector3.forward : Vector3.up));
        var light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = angle;
        light.innerSpotAngle = angle * 0.5f;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
    }

    // A warm spot from `from` onto `target`: what the player should look at.
    internal static void Accent(string name, Vector3 target, Vector3 from, Transform parent) =>
        BeamLight("Accent_" + name, from, target - from, 38f, 2.2f, 8f, false, AccentColor, parent);

    // The water light through a wall window (a Fish Window, its arrow into the room): a cool spot from outside,
    // slanting down in through it, and a faint shaft along it.
    internal static void WindowBeam(Transform window, bool shadows, Transform parent)
    {
        Vector3 into = (window.forward * Mathf.Cos(35f * Mathf.Deg2Rad) + Vector3.down * Mathf.Sin(35f * Mathf.Deg2Rad)).normalized;
        BeamLight("Beam_" + window.name, window.position - into * 2f, into, 58f, 5.5f, 15f, shadows, WaterBeam, parent);
        Shaft("Shaft_" + window.name, window.position - into * 0.3f, into, 6.5f, 1.7f, WaterShaft, parent);
    }

    // A faint shaft of light: two crossed quads from `start` along `direction`, bright at the start and fading out
    // along its length and at its sides (additive, never casting or catching shadows).
    internal static void Shaft(string name, Vector3 start, Vector3 direction, float length, float width, Color color, Transform parent)
    {
        var shaft = new GameObject(name);
        shaft.transform.SetParent(parent, false);
        Vector3 d = direction.normalized;
        shaft.transform.SetPositionAndRotation(start, Quaternion.LookRotation(d, Mathf.Abs(d.y) > 0.95f ? Vector3.forward : Vector3.up));
        Material material = EnsureShaftMaterial();
        foreach (Quaternion turn in new[] { Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(90f, 0f, 0f) })
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Beam";
            quad.transform.SetParent(shaft.transform, false);
            quad.transform.localRotation = turn;
            quad.transform.localPosition = new Vector3(0f, 0f, length * 0.5f);
            quad.transform.localScale = new Vector3(width, length, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        shaft.AddComponent<RendererTint>().Tint = color;
    }

    // The sun turned high (Sun Rotation) with soft shadows, and the ocean surface overhead under `parent` (Ocean
    // Surface: the shimmering underside of the surface and the sun's glow).
    internal static void SunAndSurface(Transform parent)
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
        if (sun != null)
        {
            sun.transform.rotation = SunRotation;
            sun.shadows = LightShadows.Soft;
        }
        var surface = new GameObject("OceanSurface");
        surface.transform.SetParent(parent, false);
        var component = surface.AddComponent<OceanSurface>();
        if (sun != null)
        {
            var so = new SerializedObject(component);
            so.FindProperty("sun").objectReferenceValue = sun;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // LightShaft.mat: a URP particle material, additive, with a soft streak (LightShaft.png: bright near the start,
    // fading along its length and to its sides), saved once.
    internal static Material EnsureShaftMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(ShaftMaterialPath);
        if (material != null)
            return material;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ShaftTexturePath);
        if (texture == null)
        {
            const int w = 32, h = 128;
            var generated = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float side = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2f);
                    float along = Mathf.SmoothStep(0f, 1f, v / 0.08f) * Mathf.Pow(1f - v, 1.6f);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, side * along);
                }
            generated.SetPixels(pixels);
            generated.Apply();
            System.IO.File.WriteAllBytes(ShaftTexturePath, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(ShaftTexturePath);
            if (AssetImporter.GetAtPath(ShaftTexturePath) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ShaftTexturePath);
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        material = new Material(shader) { name = "LightShaft" };
        UnderwaterLighting.ConfigureMoteMaterial(material, texture);
        material.SetFloat("_Blend", 2f);   // additive
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        AssetDatabase.CreateAsset(material, ShaftMaterialPath);
        return material;
    }
}
