using System;
using UnityEngine;

// Fades a piece of the HUD out while it is not needed and back in when it is: the health and hunger bars while they
// are low or just jumped (a bite, eating, a dash), the hotbar for a few seconds after you switch slots or get
// something (and while you look at a lock or socket that wants an item), the collectible counter when it changes.
// The pieces add it to themselves (HudAutoHide.On) and call Wake() when something happens; Needed keeps it up for as
// long as it returns true. Everything shows for the first few seconds of a scene. The Settings page's "Hide HUD when
// not needed" switch (Enabled, remembered in PlayerPrefs) turns it all off.
[DisallowMultipleComponent]
public class HudAutoHide : MonoBehaviour
{
    public const string PrefsKey = "settings.hudAutoHide";
    private const float StartSeconds = 5f;    // everything is up this long when a scene starts
    private const float FadeInSpeed = 6f;     // alpha per second
    private const float FadeOutSpeed = 1.5f;

    private static bool? enabled;
    public static bool Enabled
    {
        get
        {
            enabled ??= PlayerPrefs.GetInt(PrefsKey, 1) == 1;
            return enabled.Value;
        }
        set
        {
            enabled = value;
            PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
        }
    }

    // How long it stays up after Wake().
    public float Linger = 3f;
    // Stays up while this is true (a bar that is low).
    public Func<bool> Needed;

    private CanvasGroup group;
    private float shownUntil;
    private float alpha = 1f;

    // The auto-hide on `piece`'s object, added if it is not there yet.
    public static HudAutoHide On(Component piece, float linger)
    {
        var fade = piece.GetComponent<HudAutoHide>();
        if (fade == null)
            fade = piece.gameObject.AddComponent<HudAutoHide>();
        fade.Linger = linger;
        return fade;
    }

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        shownUntil = Time.unscaledTime + StartSeconds;
    }

    // Something happened: show it for Linger seconds (or `seconds`).
    public void Wake(float seconds = -1f)
    {
        shownUntil = Mathf.Max(shownUntil, Time.unscaledTime + (seconds >= 0f ? seconds : Linger));
    }

    private void LateUpdate()
    {
        bool show = !Enabled || Time.unscaledTime < shownUntil || (Needed != null && Needed());
        alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, Time.unscaledDeltaTime * (show ? FadeInSpeed : FadeOutSpeed));
        group.alpha = alpha;
    }
}
