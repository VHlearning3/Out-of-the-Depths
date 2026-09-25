using UnityEngine;

public class PressurePlates : MonoBehaviour
{
    public GameObject door;
    private bool isPressed;

    private void OnTriggerStay(Collider other)
    {
            if (other.tag == "PushableObject")
            {
                float distance = Vector3.Distance(transform.position, other.transform.position);
                isPressed = true;
            }
               

            if (isPressed == true)
            {
                door.SetActive(false);
            }
    }
}
