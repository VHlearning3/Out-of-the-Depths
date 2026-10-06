using UnityEngine;

// So the same sound never plays exactly the same twice: every play gets its own pitch (a little higher or lower) and a
// touch of volume variation. PlayAt is AudioSource.PlayClipAtPoint with that; OneShot is AudioSource.PlayOneShot with
// it (it sets the source's pitch, so use it on sources that only play one-shots). Pitch Spread and Volume Spread are
// how much, for the whole game.
public static class SoundVariety
{
    // ±8% pitch (a bit over a semitone either way) and up to 12% quieter.
    public static float PitchSpread = 0.08f;
    public static float VolumeSpread = 0.12f;

    public static float Pitch() => Random.Range(1f - PitchSpread, 1f + PitchSpread);
    public static float Volume(float volume) => volume * Random.Range(1f - VolumeSpread, 1f);

    // Like AudioSource.PlayClipAtPoint: a 3D sound at `position`, gone once it has played. Pitch 0 = the usual
    // little random spread; Muffle = low-pass cutoff in Hz (0 = clear).
    public static AudioSource PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 0f, float muffle = 0f)
    {
        if (clip == null)
            return null;
        var go = new GameObject("One shot audio");
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.volume = Volume(volume);
        source.pitch = pitch > 0f ? pitch : Pitch();
        if (muffle > 0f)
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = muffle;
        source.Play();
        Object.Destroy(go, clip.length / source.pitch * Mathf.Max(0.01f, Time.timeScale) + 0.1f);
        return source;
    }

    // A sound the player makes (eating, the dash, a quest chime): it rides along on `follow` (the player) instead of
    // hanging in the water where it started, heard as the player's own (not placed in 3D). Muffle = low-pass cutoff in
    // Hz, so it sounds as if heard through water (0 = clear).
    public static AudioSource PlayOn(AudioClip clip, Transform follow, float volume = 1f, float muffle = 0f)
    {
        if (clip == null)
            return null;
        if (follow == null)
            return PlayAt(clip, Vector3.zero, volume);
        var go = new GameObject("One shot audio");
        go.transform.SetParent(follow, false);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 0f;
        source.volume = Volume(volume);
        source.pitch = Pitch();
        if (muffle > 0f)
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = muffle;
        source.Play();
        Object.Destroy(go, clip.length / source.pitch * Mathf.Max(0.01f, Time.timeScale) + 0.1f);
        return source;
    }

    // PlayOneShot on `source` at `basePitch` give or take the spread.
    public static void OneShot(AudioSource source, AudioClip clip, float volume = 1f, float basePitch = 1f)
    {
        if (source == null || clip == null)
            return;
        source.pitch = basePitch * Pitch();
        source.PlayOneShot(clip, Volume(volume));
    }
}
