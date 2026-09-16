using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform cameraPivot;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 2.5f;

    private InputAction interactAction;

    private void Awake()
    {
        interactAction = inputActions.FindActionMap("Player").FindAction("Interact");
    }

    private void OnEnable()
    {
        inputActions.FindActionMap("Player").Enable();
    }

    private void OnDisable()
    {
        inputActions.FindActionMap("Player").Disable();
    }

    private void Update()
    {
        if (!interactAction.WasPressedThisFrame())
            return;

        if (Physics.Raycast(cameraPivot.position, cameraPivot.forward, out RaycastHit hit, interactRange)
            && hit.collider.TryGetComponent(out IInteractable interactable))
        {
            interactable.Interact(gameObject);
        }
    }
}
