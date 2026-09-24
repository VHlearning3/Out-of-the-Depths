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
    private Texture2D white;

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
        if (white != null)
            Destroy(white);
        if (instance == this)
            instance = null;
    }

    private void OnGUI()
    {
        float age = Time.unscaledTime - shownAt;
        if (string.IsNullOrEmpty(text) || age > seconds || PauseMenu.IsOpen)
            return;

        float alpha = Mathf.Clamp01(age / FadeIn) * Mathf.Clamp01((seconds - age) / FadeOut);
        float rise = 1f - Mathf.Clamp01(age / FadeIn);
        float scale = Screen.height / 1080f * UIScale.Hud;
        if (white == null)
        {
            white = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }
        style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
        style.font = GameFont.Font;
        style.fontSize = Mathf.Max(12, Mathf.RoundToInt(30f * scale));

        float width = Mathf.Min(900f * scale, Screen.width - 40f);
        float pad = 18f * scale;
        float height = style.CalcHeight(new GUIContent(text), width - pad * 2f) + pad * 2f;
        var box = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.74f - height * 0.5f + rise * 24f * scale, width, height);

        Color was = GUI.color;
        GUI.color = new Color(0.02f, 0.08f, 0.12f, 0.78f * alpha);
        GUI.DrawTexture(box, white);
        GUI.color = new Color(0.45f, 0.85f, 0.95f, alpha);   // a thin accent line along the top
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, Mathf.Max(2f, 3f * scale)), white);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(box.x + pad, box.y + pad, box.width - pad * 2f, box.height - pad * 2f), text, style);
        GUI.color = was;
    }
}
