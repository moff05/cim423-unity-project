using UnityEngine;

// Ambient drone + bass rumble loop and get louder as InteractionSequencer
// progresses, starting with the intro cinematic. Breathing is NOT a
// continuous ambient layer anymore, per direct feedback ("mute the breaths
// until you trigger the disappearing act, then that's the only part you
// can hear the breath") - it's silent the entire rest of the game and only
// played, at a fixed volume, by ArtifactInteractable itself for the exact
// duration of that one beat. See PlayBreathingOnce/StopBreathing below.
public class AmbienceManager : MonoBehaviour
{
    public static AmbienceManager Instance { get; private set; }

    public AudioSource ambientSource;
    public AudioSource breathingSource;
    // Low-frequency sub-bass drone - meant to be felt more than heard, so it
    // sits under the audible drone layer the whole game long.
    public AudioSource bassRumbleSource;
    public float ambientBaseVolume = 0.08f;
    public float ambientMaxVolume = 0.22f;
    public float bassRumbleBaseVolume = 0.15f;
    public float bassRumbleMaxVolume = 0.35f;
    // Fixed volume breathing plays at during its one scripted moment - not
    // an escalation curve anymore, since it's not a continuous layer.
    public float breathingPlayVolume = 0.5f;
    public float volumeLerpSpeed = 1.2f;

    bool started;
    bool muted;

    // Called by ArtifactInteractable right when the disappearing-act beat
    // starts. Loops for as long as StopBreathing isn't called.
    public void PlayBreathingOnce()
    {
        if (breathingSource == null) return;
        breathingSource.loop = true;
        breathingSource.volume = breathingPlayVolume;
        breathingSource.Play();
    }

    public void StopBreathing()
    {
        if (breathingSource != null) breathingSource.Stop();
    }

    void Awake()
    {
        Instance = this;
    }

    // Pauses (not stops) the drone/rumble so they resume exactly where they
    // left off, at whatever volume the escalation curve has already reached,
    // instead of restarting or popping back in at the wrong loudness.
    // Breathing deliberately keeps playing through a mute - it's the one
    // sound that's still "you" even once the room and its ambience are gone.
    public void SetMuted(bool value)
    {
        if (muted == value) return;
        muted = value;
        if (ambientSource != null) { if (value) ambientSource.Pause(); else ambientSource.UnPause(); }
        if (bassRumbleSource != null) { if (value) bassRumbleSource.Pause(); else bassRumbleSource.UnPause(); }
    }

    void Update()
    {
        // Ambient drone/bass rumble start as soon as the intro cinematic
        // begins - the ship shouldn't be dead silent during the flight/jump.
        if (!started && GameManager.Instance != null && GameManager.Instance.IntroStarted)
        {
            started = true;
            if (ambientSource != null)
            {
                ambientSource.loop = true;
                ambientSource.volume = ambientBaseVolume;
                ambientSource.Play();
            }
            if (bassRumbleSource != null)
            {
                bassRumbleSource.loop = true;
                bassRumbleSource.volume = bassRumbleBaseVolume;
                bassRumbleSource.Play();
            }
        }

        float progress = InteractionSequencer.Instance != null ? InteractionSequencer.Instance.Progress : 0f;
        if (started)
        {
            if (ambientSource != null)
                ambientSource.volume = Mathf.Lerp(ambientSource.volume, Mathf.Lerp(ambientBaseVolume, ambientMaxVolume, progress), Time.deltaTime * volumeLerpSpeed);
            if (bassRumbleSource != null)
                bassRumbleSource.volume = Mathf.Lerp(bassRumbleSource.volume, Mathf.Lerp(bassRumbleBaseVolume, bassRumbleMaxVolume, progress), Time.deltaTime * volumeLerpSpeed);
        }
    }
}
