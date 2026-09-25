using UnityEngine;

public class PressurePlates : MonoBehaviour
{
    public bool isPressed;

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("PushableObject"))
        {
            isPressed = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("PushableObject"))
        {
            isPressed = false;
        }
    }
}
