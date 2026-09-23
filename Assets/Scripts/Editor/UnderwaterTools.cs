using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The underwater look of the open scene, in one go: an Underwater Lighting (on Ambience, or a new Underwater object)
// with the sun and the motes material filled in, a global Volume beside it with the shared colour grade
// (Assets/Settings/UnderwaterProfile.asset: teal tint, vignette, bloom; made once, then yours to tune), and
// post-processing switched on for the main camera. Tools > Out of the Depths > Underwater Look (Open Scene); both
// scene builders run it.
public static class UnderwaterTools
{
    public const string ProfilePath = "Assets/Settings/UnderwaterProfile.asset";
    public const string MotesMaterialPath = "Assets/Art/Materials/WaterMotes.mat";
    public const string MotesTexturePath = "Assets/Art/Textures/WaterMote.png";

    [MenuItem("Tools/Out of the Depths/Underwater Look (Open Scene)")]
    public static void ApplyToOpenScene()
    {
        var lighting = Object.FindFirstObjectByType<UnderwaterLighting>(FindObjectsInactive.Include);
        if (lighting == null)
        {
            GameObject host = GameObject.Find("Ambience");
            if (host == null)
                host = new GameObject("Underwater");
            lighting = Undo.AddComponent<UnderwaterLighting>(host);
        }

        var so = new SerializedObject(lighting);
        SerializedProperty sun = so.FindProperty("sun");
        if (sun != null && sun.objectReferenceValue == null)
            sun.objectReferenceValue = FindSun();
        SerializedProperty motes = so.FindProperty("motesMaterial");
        if (motes != null && motes.objectReferenceValue == null)
            motes.objectReferenceValue = EnsureMotesMaterial();
        so.ApplyModifiedPropertiesWithoutUndo();

        // The colour grade: a global volume on the same object, with the shared profile.
        VolumeProfile profile = EnsureProfile();
        Volume volume = lighting.GetComponent<Volume>();
        if (volume == null)
            volume = Undo.AddComponent<Volume>(lighting.gameObject);
        volume.isGlobal = true;
        volume.priority = 1f;
        if (volume.sharedProfile == null)
            volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);

        // The camera: post-processing on, so the grade shows.
        Camera camera = Camera.main;
        if (camera == null)
            camera = Object.FindFirstObjectByType<Camera>();
        if (camera != null)
        {
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(camera);
        }

        lighting.Apply();
        EditorUtility.SetDirty(lighting);
        EditorSceneManager.MarkSceneDirty(lighting.gameObject.scene);
        Debug.Log("Underwater look applied: murk, no sky, the sun with caustics, motes while playing, and the colour grade in " + ProfilePath + " (tune it there).");
    }

    private static Light FindSun()
    {
        if (RenderSettings.sun != null)
            return RenderSettings.sun;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (light.type == LightType.Directional)
                return light;
        return null;
    }

    // The colour grade, made once: a gentle teal tint, a little less colour, a vignette and a soft bloom.
    private static VolumeProfile EnsureProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile != null)
            return profile;
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        ColorAdjustments color = profile.Add<ColorAdjustments>(false);
        color.colorFilter.Override(new Color(0.74f, 0.93f, 1f));
        color.saturation.Override(-12f);
        color.postExposure.Override(0.15f);
        AssetDatabase.AddObjectToAsset(color, profile);

        Vignette vignette = profile.Add<Vignette>(false);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0f, 0.05f, 0.08f));
        AssetDatabase.AddObjectToAsset(vignette, profile);

        Bloom bloom = profile.Add<Bloom>(false);
        bloom.intensity.Override(0.35f);
        bloom.threshold.Override(1f);
        bloom.scatter.Override(0.75f);
        AssetDatabase.AddObjectToAsset(bloom, profile);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("Created " + ProfilePath);
        return profile;
    }

    // WaterMotes.mat: URP particle material with a soft speck, saved so it ships in builds.
    private static Material EnsureMotesMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MotesMaterialPath);
        if (material != null)
            return material;

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MotesTexturePath);
        if (texture == null)
        {
            Texture2D generated = UnderwaterLighting.MakeMoteTexture();
            System.IO.File.WriteAllBytes(MotesTexturePath, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(MotesTexturePath);
            if (AssetImporter.GetAtPath(MotesTexturePath) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MotesTexturePath);
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        material = new Material(shader) { name = "WaterMotes" };
        UnderwaterLighting.ConfigureMoteMaterial(material, texture);
        AssetDatabase.CreateAsset(material, MotesMaterialPath);
        return material;
    }
}
