using System.Collections.Generic;
using UnityEngine;

// One-shot sound effects for v2, loaded by name from Resources/V2Audio so the
// self-installing helpers need no scene wiring.
public static class V2Audio
{
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

    public static AudioClip Clip(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!cache.TryGetValue(name, out var clip) || clip == null)
        {
            clip = Resources.Load<AudioClip>("V2Audio/" + name);
            cache[name] = clip;
        }
        return clip;
    }

    // Positional one-shot: the direction a sound comes from is half the scare.
    public static AudioSource PlayAt(string name, Vector3 position, float volume = 1f, float maxDistance = 30f)
    {
        var clip = Clip(name);
        if (clip == null) return null;
        var go = new GameObject("OneShot_" + name);
        go.transform.position = position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1.5f;
        src.maxDistance = maxDistance;
        src.Play();
        Object.Destroy(go, clip.length + 0.3f);
        return src;
    }

    // Non-positional (in the player's head / suit radio).
    public static AudioSource Play2D(string name, float volume = 1f)
    {
        var src = PlayAt(name, Vector3.zero, volume);
        if (src != null) src.spatialBlend = 0f;
        return src;
    }
}
