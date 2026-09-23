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

    [Header("Held (weapons)")]
    [Tooltip("How long the model is in your hand, tip to end, in metres (Held Weapon fits it to this).")]
    [SerializeField, Min(0.05f)] private float heldLength = 0.45f;
    [Tooltip("Where your hand grips it, from the back end (0) to the tip (1).")]
    [SerializeField, Range(0f, 1f)] private float heldGrip = 0.2f;
    [Tooltip("The model's tip direction in its own space. Assign Known Item Models works it out from the mesh (the long axis, towards the pointed end).")]
    [SerializeField] private Vector3 heldTipAxis = Vector3.up;
    [Tooltip("Extra turn in the hand, in degrees (x = tip up / down, y = left / right, z = roll).")]
    [SerializeField] private Vector3 heldRotation;
    // Which version of the tools' weapon fit this item has had (so a later fix is applied once, then left to you).
    [SerializeField, HideInInspector] private int heldFitVersion;

    public string DisplayName => displayName;
    public Category Kind => category;
    public Sprite Icon => icon;
    public int MaxStack => maxStack;
    public string Description => description;
    public GameObject WorldModel => worldModel;
    public float WorldModelScale => worldModelScale;
    public Vector3 WorldModelRotation => worldModelRotation;
    public GameObject[] WorldModelVariants => worldModelVariants;
    public float HeldLength => heldLength;
    public float HeldGrip => heldGrip;
    public Vector3 HeldTipAxis => heldTipAxis;
    public Vector3 HeldRotation => heldRotation;

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
