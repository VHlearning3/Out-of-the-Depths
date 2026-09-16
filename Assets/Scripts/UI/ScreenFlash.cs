using UnityEngine;
using UnityEngine.UI;

// Full-screen colour flash that fades out (damage = red, food = green).
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
        Flash(1f);
    }

    // strength scales the configured alpha, so a big hit flashes harder than a scratch.
    public void Flash(float strength)
    {
        if (flashImage == null)
            return;

        var c = flashImage.color;
        c.a = Mathf.Max(c.a, flashAlpha * Mathf.Clamp(strength, 0.25f, 2f));
        flashImage.color = c;
    }
}
