// Put this on any component next to an IInteractable to react when the player starts/stops looking at it (highlight, particles, sound...).
public interface IInteractTargetListener
{
    void OnTargeted(bool targeted);
}
