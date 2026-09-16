using UnityEngine;

public class DeadFish : MonoBehaviour, IInteractable
{
    [SerializeField] private float hungerRestoreAmount = 25f;

    public void Interact(GameObject interactor)
    {
        if (!interactor.TryGetComponent(out HungerSystem hunger))
            return;

        hunger.Feed(hungerRestoreAmount);
        Destroy(gameObject);
    }
}
