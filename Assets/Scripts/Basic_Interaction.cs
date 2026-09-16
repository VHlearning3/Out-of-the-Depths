using UnityEngine;
using UnityEngine.InputSystem;

public class Basic_Interaction : MonoBehaviour
{
    public float interactionDistance;
    public GameObject interactionText;
    public LayerMask interactionLayers;
    private Vector2 interactInput;

    //pickup>inventory


    void Update()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, interactionDistance, interactionLayers ))
        {
            if (hit.collider.gameObject.CompareTag("Interactable"))
            {
                interactionText.SetActive(true);

              //  if (interactInput)
            }
        }
        else 
        {
            interactionText.SetActive(false);
        }
    }
}
