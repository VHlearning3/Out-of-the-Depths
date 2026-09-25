// Anything the player can press E on (fish, doors, chests...). Implement this and give it a collider.
public interface IInteractable
{
    string Prompt { get; }
    void Interact(UnityEngine.GameObject interactor);
}

// How the E prompt is coloured: Blocked (red) = it will not open / you do not have what it needs, Ready (green) = you
// have what it needs, Normal = the prompt's own colour.
public enum PromptTone { Normal, Blocked, Ready }

// Optional, next to IInteractable: a lock, socket or puzzle that says whether it can be used right now.
public interface IPromptTone
{
    PromptTone Tone { get; }
}
