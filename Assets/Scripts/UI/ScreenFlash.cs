using UnityEngine;
using UnityEngine.UI;

public class ScreenFlash : MonoBehaviour
{
    [SerializeField] private Image flashImage;
    [SerializeField] private float flashAlpha = 0.35f;
    [SerializeField] private float fadeSpeed = 2f;

    private void Awake()
    {
        if (flashImage == null)
            return;

        var c = flashImage.color;
        c.a = 0f;
        flashImage.color = c;
    }

    private void Update()
    {
        if (flashImage == null || flashImage.color.a <= 0f)
            return;

        var c = flashImage.color;
        c.a = Mathf.MoveTowards(c.a, 0f, fadeSpeed * Time.deltaTime);
        flashImage.color = c;
    }

    public void Flash()
    {
        if (flashImage == null)
            return;

        var c = flashImage.color;
        c.a = flashAlpha;
        flashImage.color = c;
    }
}
