using UnityEngine;

// Two settings from the Settings page that the rest of the game reads: the frame rate limit (Application.target
// FrameRate; V-Sync, when on, wins over it) and how much the view bobs (0 = a steady camera, 1 = the full bob, sway
// and banking; Swim Controller multiplies its camera feel by it). Remembered in PlayerPrefs and applied as the game
// starts, so they hold in every scene whether or not the pause menu is ever opened.
public static class ComfortSettings
{
    public const string FpsKey = "settings.fpsLimit";
    public const string BobKey = "settings.viewBobbing";

    // 0 = unlimited.
    public static readonly int[] FpsOptions = { 0, 30, 60, 90, 120, 144, 165, 240 };
    public static readonly string[] FpsLabels = { "Unlimited", "30", "60", "90", "120", "144", "165", "240" };

    public static int FpsLimit { get; private set; }
    public static float ViewBobbing { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        FpsLimit = Mathf.Max(0, PlayerPrefs.GetInt(FpsKey, 0));
        ViewBobbing = Mathf.Clamp01(PlayerPrefs.GetFloat(BobKey, 1f));
        ApplyFps();
    }

    public static int FpsIndex
    {
        get
        {
            int index = System.Array.IndexOf(FpsOptions, FpsLimit);
            return index >= 0 ? index : 0;
        }
    }

    public static void SetFpsIndex(int index)
    {
        FpsLimit = FpsOptions[Mathf.Clamp(index, 0, FpsOptions.Length - 1)];
        PlayerPrefs.SetInt(FpsKey, FpsLimit);
        ApplyFps();
    }

    public static void SetViewBobbing(float amount)
    {
        ViewBobbing = Mathf.Clamp01(amount);
        PlayerPrefs.SetFloat(BobKey, ViewBobbing);
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey(FpsKey);
        PlayerPrefs.DeleteKey(BobKey);
        FpsLimit = 0;
        ViewBobbing = 1f;
        ApplyFps();
    }

    private static void ApplyFps() => Application.targetFrameRate = FpsLimit > 0 ? FpsLimit : -1;
}
