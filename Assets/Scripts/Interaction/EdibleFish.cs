using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EdibleFish : MonoBehaviour, IInteractable
{
    [Header("Hunger")]
    [SerializeField] private float hungerRestoreAmount = 25f;

    [Header("Feedback")]
    [SerializeField] private AudioClip eatSound;
    [SerializeField] private GameObject consumedVfx;

    private bool consumed;

    public void Interact(GameObject interactor)
    {
        if (consumed)
            return;

        var hunger = interactor.GetComponentInParent<HungerSystem>();
        if (hunger == null)
            return;

        consumed = true;
        hunger.Eat(hungerRestoreAmount);

        if (eatSound != null)
            AudioSource.PlayClipAtPoint(eatSound, transform.position);

        if (consumedVfx != null)
            Instantiate(consumedVfx, transform.position, transform.rotation);

        gameObject.SetActive(false);
    }
}
