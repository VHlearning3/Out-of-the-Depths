using UnityEngine;

// GameObject.CreatePrimitive for things made while the game runs. In the editor a new primitive gets the render
// pipeline's default material (URP Lit), but a build has no such default: there it gets a material with no shader the
// pipeline can draw, so every cube or sphere made at runtime came out flat magenta in the build only (the bars Fish
// Window puts across the roof holes and windows, the puffer nests). Create() makes the primitive and, while playing,
// gives it Material: a plain white URP Lit, the same as the editor default, so Renderer Tint colours it as before.
// Outside Play it keeps the editor default (an asset, saved with the scene; a material made here would not be).
public static class RuntimePrimitive
{
    private static Material lit;

    public static Material Material
    {
        get
        {
            if (lit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                    lit = new Material(shader) { name = "Lit (runtime primitives)", hideFlags = HideFlags.DontSave };
            }
            return lit;
        }
    }

    public static GameObject Create(PrimitiveType type)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        var renderer = primitive.GetComponent<Renderer>();
        if (Application.isPlaying && renderer != null && Material != null)
            renderer.sharedMaterial = Material;
        return primitive;
    }
}
