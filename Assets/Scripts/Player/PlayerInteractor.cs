using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform interactOrigin;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private LayerMask interactableMask = ~0;

    private InputAction interactAction;
    private readonly Collider[] overlapResults = new Collider[8];

    private void Awake()
    {
        var playerMap = inputActions.FindActionMap("Player");
        interactAction = playerMap.FindAction("Interact");
    }

    private void OnEnable()
    {
        interactAction.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        interactAction.performed -= OnInteractPerformed;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        Vector3 origin = interactOrigin != null ? interactOrigin.position : transform.position;

        IInteractable closest = null;
        float closestSqrDist = float.MaxValue;

        int count = Physics.OverlapSphereNonAlloc(origin, interactRange, overlapResults, interactableMask);
        for (int i = 0; i < count; i++)
        {
            var interactable = overlapResults[i].GetComponentInParent<IInteractable>();
            if (interactable == null)
                continue;

            float sqrDist = (overlapResults[i].transform.position - origin).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = interactable;
            }
        }

        closest?.Interact(gameObject);
    }
}
