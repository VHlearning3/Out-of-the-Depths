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

    // Like AudioSource.PlayClipAtPoint: a 3D sound at `position`, gone once it has played.
    public static AudioSource PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null)
            return null;
        var go = new GameObject("One shot audio");
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.volume = Volume(volume);
        source.pitch = Pitch();
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
