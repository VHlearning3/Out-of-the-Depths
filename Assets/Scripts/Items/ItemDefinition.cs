using UnityEngine;

// One collectable thing from the GDD list (key, fragment, weapon, pearl...). Create via Assets > Create > Out of the Depths > Item;
// pickups and sockets reference the asset, so renaming or re-iconing an item never touches a scene.
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

    public string DisplayName => displayName;
    public Category Kind => category;
    public Sprite Icon => icon;
    public int MaxStack => maxStack;
    public string Description => description;
}
