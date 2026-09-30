using UnityEngine;

public class SecretDoorManager : MonoBehaviour
{
    public PressurePlates plate1;
    public PressurePlates plate2;
    public PressurePlates plate3;

    public Door door;

    private bool doorOpened = false;
    private bool warned;

    private void Update()
    {
        if (plate1 == null || plate2 == null || plate3 == null || door == null)
        {
            if (!warned)
                Debug.LogWarning($"{name}: Secret Door Manager is missing a pressure plate or the door; drag them into its slots.", this);
            warned = true;
            return;
        }

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
