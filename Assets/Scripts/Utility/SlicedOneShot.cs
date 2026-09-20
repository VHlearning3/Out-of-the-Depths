using UnityEngine;

// Plays a clip once at a point in the world with its own pitch, and can play just a random slice of a long recording
// (faded in and out so it never clicks). PlayClipAtPoint can do none of that. Use: SlicedOneShot.Play(...).
public class SlicedOneShot : MonoBehaviour
{
    private AudioSource source;
    private float start;
    private float end;
    private float fade;
    private float volume;

    // sliceSeconds <= 0 or longer than the clip = play the whole clip. spatial 1 = 3D at the position, 0 = 2D.
    public static AudioSource Play(AudioClip clip, Vector3 position, float volume, float pitch = 1f, float sliceSeconds = 0f, float fade = 0.2f, float spatial = 1f, float maxDistance = 30f)
    {
        if (clip == null)
            return null;

        var go = new GameObject("OneShot " + clip.name);
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.pitch = Mathf.Max(0.1f, pitch);
        source.spatialBlend = spatial;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.playOnAwake = false;

        bool slice = sliceSeconds > 0f && clip.length > sliceSeconds + 0.1f;
        float seconds = slice ? sliceSeconds : clip.length / source.pitch;
        if (slice)
            source.time = Random.Range(0f, clip.length - sliceSeconds);

        var player = go.AddComponent<SlicedOneShot>();
        player.source = source;
        player.volume = volume;
        player.fade = slice ? Mathf.Max(0.01f, fade) : 0.01f;
        player.start = Time.unscaledTime;
        player.end = player.start + seconds;
        source.volume = slice ? 0f : volume;
        source.Play();
        Destroy(go, seconds + 0.2f);
        return source;
    }

    private void Update()
    {
        if (source == null)
            return;
        float now = Time.unscaledTime;
        float envelope = Mathf.Min((now - start) / fade, (end - now) / fade);
        source.volume = Mathf.Clamp01(envelope) * volume;
        if (now >= end && source.isPlaying)
            source.Stop();
    }
}
