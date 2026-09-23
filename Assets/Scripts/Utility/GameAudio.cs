using UnityEngine;

// The game's overall loudness: the player's Master volume (the Settings slider, 0..1, remembered) times Mix Trim, a
// project-wide trim that sits under it so every sound in the game can be turned down together without touching each
// one. Applied as the game starts (before any scene), so the main menu and every level get it.
public static class GameAudio
{
    public const string VolumeKey = "settings.masterVolume";

    // Everything together at this share of full volume, on top of the player's own Master volume.
    public static float MixTrim = 0.75f;

    private static float master = 1f;

    // The player's own volume (what the slider shows), 0..1. Setting it applies it at once and remembers it.
    public static float Master
    {
        get => master;
        set
        {
            master = Mathf.Clamp01(value);
            AudioListener.volume = master * MixTrim;
            PlayerPrefs.SetFloat(VolumeKey, master);
        }
    }

    // Back to full, forgetting the saved value (the Settings page's Reset).
    public static void ResetMaster()
    {
        PlayerPrefs.DeleteKey(VolumeKey);
        master = 1f;
        AudioListener.volume = MixTrim;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyAtStart()
    {
        master = PlayerPrefs.HasKey(VolumeKey) ? Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey)) : 1f;
        AudioListener.volume = master * MixTrim;
    }
}
