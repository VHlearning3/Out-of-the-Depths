// Anything the player can press E on (fish, doors, chests...). Implement this and give it a collider.
public interface IInteractable
{
    string Prompt { get; }
    void Interact(UnityEngine.GameObject interactor);
}
