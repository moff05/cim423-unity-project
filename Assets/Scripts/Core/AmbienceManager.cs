using UnityEngine;

// Two looping layers (ambient drone + player breathing) that start once the
// story begins and get louder as InteractionSequencer progresses, so the
// station feels more oppressive the deeper the player goes.
public class AmbienceManager : MonoBehaviour
{
    public AudioSource ambientSource;
    public AudioSource breathingSource;
    // Low-frequency sub-bass drone - meant to be felt more than heard, so it
    // sits under the audible drone/breathing layers the whole game long.
    public AudioSource bassRumbleSource;
    public float ambientBaseVolume = 0.1f;
    public float ambientMaxVolume = 0.32f;
    public float breathingBaseVolume = 0.07f;
    public float breathingMaxVolume = 0.26f;
    public float bassRumbleBaseVolume = 0.25f;
    public float bassRumbleMaxVolume = 0.55f;
    public float volumeLerpSpeed = 1.2f;

    bool started;

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
