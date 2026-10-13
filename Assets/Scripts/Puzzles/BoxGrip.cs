using UnityEngine;

// The E handle on a Pushable Box (the box adds it at start): E on the box takes hold of it, to pull it along behind you
// wherever you swim (Box Push on the player tows it); E again lets go. Switched off once the box is in its plate's pit,
// so a placed box is no longer something to press E on.
public class BoxGrip : MonoBehaviour, IInteractable
{
    private PushableBox box;

    public void Setup(PushableBox owner) => box = owner;

    public string Prompt => BoxPush.IsTowing(box) ? "let go of the box" : "pull the box";

    public void Interact(GameObject interactor)
    {
        if (box == null || interactor == null)
            return;
        BoxPush pusher = interactor.GetComponentInParent<BoxPush>();
        if (pusher == null)
            pusher = interactor.GetComponentInChildren<BoxPush>();
        if (pusher != null)
            pusher.ToggleTow(box);
    }
}
