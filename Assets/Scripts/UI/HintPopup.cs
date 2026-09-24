using UnityEngine;

// A short tip that slides up near the bottom of the screen, stays a few seconds and fades away, on a dark panel in
// the game font (the first dash says what dashing costs). HintPopup.Show(text) from anywhere: it makes itself, and
// a newer tip replaces the one showing. It hides while the pause menu is open.
public class HintPopup : MonoBehaviour
{
    private const float FadeIn = 0.3f;
    private const float FadeOut = 0.6f;

    private static HintPopup instance;

    private string text;
    private float shownAt = -100f;
    private float seconds;
    private GUIStyle style;

    public static void Show(string message, float forSeconds = 4.5f)
    {
        if (instance == null)
            instance = new GameObject("HintPopup").AddComponent<HintPopup>();
        instance.text = message;
        instance.shownAt = Time.unscaledTime;
        instance.seconds = Mathf.Max(FadeIn + FadeOut, forSeconds);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // A small rounded card in the HUD style (HudStyle): soft shadow, a short accent tick on top, the text centred and
    // wrapped to at most Max Width; it rises a little as it fades in.
    private void OnGUI()
    {
        float age = Time.unscaledTime - shownAt;
        if (string.IsNullOrEmpty(text) || age > seconds || PauseMenu.IsOpen || Event.current.type != EventType.Repaint)
            return;

        float alpha = Mathf.Clamp01(age / FadeIn) * Mathf.Clamp01((seconds - age) / FadeOut);
        float rise = 1f - Ease.OutCubic(Mathf.Clamp01(age / FadeIn));
        float scale = HudStyle.Scale;
        style = HudStyle.Label(Mathf.Max(11, Mathf.RoundToInt(18f * scale)), TextAnchor.MiddleCenter, true);

        var content = new GUIContent(text);
        float padX = 22f * scale, padY = 13f * scale;
        float maxWidth = Mathf.Min(640f * scale, Screen.width - 40f);
        float width = Mathf.Min(maxWidth, style.CalcSize(content).x + padX * 2f + 2f);
        float height = style.CalcHeight(content, width - padX * 2f) + padY * 2f;
        var box = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.72f - height * 0.5f + rise * 14f * scale, width, height);

        HudStyle.Panel(box, 12f * scale, alpha);
        HudStyle.Fill(new Rect(box.center.x - 18f * scale, box.y, 36f * scale, Mathf.Max(2f, 2.5f * scale)), 2f * scale, new Color(HudStyle.Accent.r, HudStyle.Accent.g, HudStyle.Accent.b, alpha));
        HudStyle.Write(new Rect(box.x + padX, box.y + padY, box.width - padX * 2f, box.height - padY * 2f), text, style, HudStyle.Text, alpha);
    }
}
