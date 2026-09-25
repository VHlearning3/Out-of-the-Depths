using UnityEngine;

public class SecretDoorManager : MonoBehaviour
{
    public PressurePlates plate1;
    public PressurePlates plate2;
    public PressurePlates plate3;

    public GameObject door;

    private void Update()
    {
        if (plate1.isPressed && plate2.isPressed && plate3.isPressed)
        {
            door.SetActive(false);
        }
    }
}
