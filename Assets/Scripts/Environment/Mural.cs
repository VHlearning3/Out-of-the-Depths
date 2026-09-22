using UnityEngine;

// A picture slot on a wall. Drop the artwork on Picture and it shows on the mural, fitted inside the mural's area
// without stretching (or stretched to fill), glowing a little of its own so it reads in dim water. Empty = the plain
// placeholder colour, until there is a picture. Every mural shares one Mural.mat; each shows its own picture through
// a property block, so a picture is one drag and no material. The builders make these (MuralAt): the root faces
// into the room and the Picture quad under it is what shows.
[ExecuteAlways]
public class Mural : MonoBehaviour
{
    [Tooltip("The artwork. Empty = the plain placeholder colour, until there is a picture.")]
    [SerializeField] private Texture2D picture;
    [Tooltip("The wall area the mural fills, in metres: width and height.")]
    [SerializeField] private Vector2 size = new Vector2(4f, 1.6f);
    [Tooltip("Fit the picture inside the area without stretching it (centred). Off = stretch it to fill the area.")]
    [SerializeField] private bool keepAspect = true;
    [Tooltip("What shows while there is no picture.")]
    [SerializeField] private Color placeholderColor = new Color(0.45f, 0.2f, 0.7f);
    [Tooltip("How much the picture glows by itself so it reads in dim water. 0 = lit by the lights only.")]
    [Range(0f, 1f)] [SerializeField] private float glow = 0.25f;

    private const string PictureName = "Picture";
    private static MaterialPropertyBlock block;

    public Texture2D Picture
    {
        get => picture;
        set
        {
            picture = value;
            Apply();
        }
    }

    // The builders call this when they make one.
    public void Setup(Vector2 area, Color placeholder)
    {
        size = area;
        placeholderColor = placeholder;
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    // The quad sized to the area (or to the picture inside it) and showing the picture or the placeholder colour.
    public void Apply()
    {
        Transform quad = transform.Find(PictureName);
        if (quad == null)
            return;

        float width = Mathf.Max(0.01f, size.x);
        float height = Mathf.Max(0.01f, size.y);
        if (picture != null && keepAspect && picture.height > 0)
        {
            float aspect = picture.width / (float)picture.height;
            if (width / height > aspect)
                width = height * aspect;
            else
                height = width / aspect;
        }
        quad.localPosition = Vector3.zero;
        quad.localRotation = Quaternion.identity;
        quad.localScale = new Vector3(width, height, 1f);

        var renderer = quad.GetComponent<Renderer>();
        if (renderer == null)
            return;
        if (block == null)
            block = new MaterialPropertyBlock();
        block.Clear();
        if (picture != null)
        {
            block.SetTexture("_BaseMap", picture);
            block.SetTexture("_EmissionMap", picture);
            block.SetColor("_BaseColor", Color.white);
            block.SetColor("_EmissionColor", Color.white * glow);
        }
        else
        {
            block.SetColor("_BaseColor", placeholderColor);
            block.SetColor("_EmissionColor", placeholderColor * (glow * 0.5f));
        }
        renderer.SetPropertyBlock(block);
    }
}
