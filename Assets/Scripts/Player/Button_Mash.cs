using UnityEngine;
using UnityEngine.InputSystem;

public class MashE : MonoBehaviour
{
    [SerializeField] private int requiredPresses = 10;
    [SerializeField] private float timeLimit = 3f;

    private int pressCount;
    private float timer;
    private bool mashing;

    public GameObject Net;

    void Update()
    {
        if (!mashing && Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            mashing = true;
            pressCount = 1;
            timer = timeLimit;
        }

        if (mashing)
        {
            timer -= Time.deltaTime;

            if (Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                pressCount++;

                if (pressCount >= requiredPresses)
                {
                    mashing = false;
                    Net.SetActive(false);
                }
            }

            if (timer <= 0f)
            {
                mashing = false;
                pressCount = 0;
            }
        }
    }
}


