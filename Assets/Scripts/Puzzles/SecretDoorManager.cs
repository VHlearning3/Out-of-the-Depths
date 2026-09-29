using UnityEngine;

public class SecretDoorManager : MonoBehaviour
{
    public PressurePlates plate1;
    public PressurePlates plate2;
    public PressurePlates plate3;

    public Door door;

    private bool doorOpened = false;

    private void Update()
    {
        bool allPressed =
            plate1.isPressed &&
            plate2.isPressed &&
            plate3.isPressed;

        if (allPressed && !doorOpened)
        {
            door.SetOpen(true);
            doorOpened = true;
        }
    }
}
