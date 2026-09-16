using UnityEngine;

// Colours every renderer under this object without touching the shared material.
[ExecuteAlways]
public class RendererTint : MonoBehaviour
{
    [SerializeField] private Color tint = Color.white;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock block;

    public Color Tint
    {
        get => tint;
        set { tint = value; Apply(); }
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        block ??= new MaterialPropertyBlock();
        block.SetColor(BaseColorId, tint);
        block.SetColor(ColorId, tint);
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.SetPropertyBlock(block);
    }
}
