using UnityEngine;

// One collectable thing from the GDD list (key, fragment, weapon, pearl...). Create via Assets > Create > Out of the Depths > Item;
// pickups and sockets reference the asset, so renaming or re-iconing an item never touches a scene.
// Give it a World Model and every pickup of this item shows that model instead of the placeholder cube.
[CreateAssetMenu(menuName = "Out of the Depths/Item", fileName = "Item_")]
public class ItemDefinition : ScriptableObject
{
    public enum Category { Key, PuzzlePiece, Weapon, Collectible }

    [SerializeField] private string displayName = "item";
    [SerializeField] private Category category = Category.PuzzlePiece;
    [Tooltip("Optional hotbar icon. Without one the slot shows the name.")]
    [SerializeField] private Sprite icon;
    [Tooltip("How many fit in one hotbar slot.")]
    [SerializeField, Min(1)] private int maxStack = 9;
    [SerializeField, TextArea] private string description;

    [Header("World model")]
    [Tooltip("The 3D model (e.g. an .fbx from Art/Models) shown by pickups of this item. Empty = the pickup keeps its own placeholder mesh.")]
    [SerializeField] private GameObject worldModel;
    [Tooltip("Size multiplier on top of the pickup's automatic fit. 1 = fit exactly, 2 = twice as big.")]
    [SerializeField, Min(0.01f)] private float worldModelScale = 1f;
    [Tooltip("Extra rotation for the model in the pickup, in degrees, e.g. to lay a key flat.")]
    [SerializeField] private Vector3 worldModelRotation;
    [Tooltip("Other looks of the same item, e.g. the second and third piece of the bone key. A pickup shows the one its Model Variant points at: 0 = World Model, 1 = the first here, 2 = the second... Empty = every pickup shows World Model.")]
    [SerializeField] private GameObject[] worldModelVariants = new GameObject[0];

    public string DisplayName => displayName;
    public Category Kind => category;
    public Sprite Icon => icon;
    public int MaxStack => maxStack;
    public string Description => description;
    public GameObject WorldModel => worldModel;
    public float WorldModelScale => worldModelScale;
    public Vector3 WorldModelRotation => worldModelRotation;
    public GameObject[] WorldModelVariants => worldModelVariants;

    // The model for a pickup's Model Variant: 0 = World Model, 1, 2... = the variants (wrapping round), each falling
    // back to World Model when empty.
    public GameObject WorldModelFor(int variant)
    {
        if (variant <= 0 || worldModelVariants == null || worldModelVariants.Length == 0)
            return worldModel;
        GameObject other = worldModelVariants[(variant - 1) % worldModelVariants.Length];
        return other != null ? other : worldModel;
    }
}
