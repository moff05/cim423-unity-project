using UnityEngine;

// Two looping layers (ambient drone + player breathing) that start once the
// story begins and get louder as InteractionSequencer progresses, so the
// station feels more oppressive the deeper the player goes.
public class AmbienceManager : MonoBehaviour
{
    public static AmbienceManager Instance { get; private set; }

    public AudioSource ambientSource;
    public AudioSource breathingSource;
    // Low-frequency sub-bass drone - meant to be felt more than heard, so it
    // sits under the audible drone/breathing layers the whole game long.
    public AudioSource bassRumbleSource;
    // Rebalanced - breathing was still inaudible even after the first widen,
    // because bass rumble/drone sat at or above it the whole time. Breathing
    // is now the loudest layer by the end; the other two pulled back so
    // they support it instead of masking it.
    public float ambientBaseVolume = 0.08f;
    public float ambientMaxVolume = 0.22f;
    public float breathingBaseVolume = 0.18f;
    public float breathingMaxVolume = 0.55f;
    public float bassRumbleBaseVolume = 0.15f;
    public float bassRumbleMaxVolume = 0.35f;
    public float volumeLerpSpeed = 1.2f;

    bool started;
    bool muted;

    void Awake()
    {
        Instance = this;
    }

    // Pauses (not stops) all three loops so they resume exactly where they
    // left off, at whatever volume the escalation curve has already reached,
    // instead of restarting or popping back in at the wrong loudness.
    public void SetMuted(bool value)
    {
        if (muted == value) return;
        muted = value;
        if (ambientSource != null) { if (value) ambientSource.Pause(); else ambientSource.UnPause(); }
        if (breathingSource != null) { if (value) breathingSource.Pause(); else breathingSource.UnPause(); }
        if (bassRumbleSource != null) { if (value) bassRumbleSource.Pause(); else bassRumbleSource.UnPause(); }
    }

    void Update()
    {
        if (!started)
        {
            // Starts as soon as the intro cinematic begins, not once the
            // player is actually inside the station - the ship shouldn't be
            // dead silent during the flight/jump.
            if (GameManager.Instance != null && GameManager.Instance.IntroStarted)
            {
                started = true;
                if (ambientSource != null)
                {
                    ambientSource.loop = true;
                    ambientSource.volume = ambientBaseVolume;
                    ambientSource.Play();
                }
                if (breathingSource != null)
                {
                    breathingSource.loop = true;
                    breathingSource.volume = breathingBaseVolume;
                    breathingSource.Play();
                }
                if (bassRumbleSource != null)
                {
                    bassRumbleSource.loop = true;
                    bassRumbleSource.volume = bassRumbleBaseVolume;
                    bassRumbleSource.Play();
                }
            }
            return;
        }

        float progress = InteractionSequencer.Instance != null ? InteractionSequencer.Instance.Progress : 0f;
        if (ambientSource != null)
            ambientSource.volume = Mathf.Lerp(ambientSource.volume, Mathf.Lerp(ambientBaseVolume, ambientMaxVolume, progress), Time.deltaTime * volumeLerpSpeed);
        if (breathingSource != null)
            breathingSource.volume = Mathf.Lerp(breathingSource.volume, Mathf.Lerp(breathingBaseVolume, breathingMaxVolume, progress), Time.deltaTime * volumeLerpSpeed);
        if (bassRumbleSource != null)
            bassRumbleSource.volume = Mathf.Lerp(bassRumbleSource.volume, Mathf.Lerp(bassRumbleBaseVolume, bassRumbleMaxVolume, progress), Time.deltaTime * volumeLerpSpeed);
    }
}
