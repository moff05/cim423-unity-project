using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

// Plays the rocket-approach cinematic on its own camera, then swaps to the
// player's Main Camera (FreeLookWalker) inside the Docking Bay behind a fade.
public class IntroSequence : MonoBehaviour
{
    public PlayableDirector cinematicDirector;
    public GameObject cinematicRigRoot;
    public Camera cinematicCamera;
    public Camera playerCamera;
    public Light cinematicLight;
    public Light secondaryLight;
    public MonoBehaviour playerController;
    public Transform playerRoot;
    public Transform dockingBaySpawnPoint;
    // The interior is always-active with no layer/culling split from the
    // cinematic camera, so it bleeds into the cinematic's early frames
    // (it sits right where the rig launches from). Hide it for the flight.
    // PlatformRigSelector also hides it at boot, before Begin is ever
    // pressed - the player shouldn't be able to see (or stand in) the
    // destination station before they've arrived.
    public GameObject interiorRoot;
    // Disabled by PlatformRigSelector at boot alongside interiorRoot (no
    // floor to stand on while the station is hidden) and restored here at
    // the same moment interiorRoot comes back.
    public GameObject xrLocomotion;
    public float fadeOutTime = 1f;
    public float fadeInTime = 1.5f;

    // Scripted flight instead of a Timeline-driven one: Timeline's Animation
    // Track was applying the clip as an offset from the rig's position at
    // playback start rather than as absolute values, sending it hundreds of
    // units past the station. A plain lerp on the rig's transform (the camera
    // is a rigidly-parented child, so it follows for free) has no such
    // offset-mode ambiguity.
    //
    // Three phases so the flight doesn't have to cross the real distance to a
    // station parked far away (keeping it far away is what keeps it from
    // ever overlapping the interior): depart locally, jump-cut (a quick
    // fade) to a point near the station, then a short final approach.
    public Vector3 departStartPosition = new Vector3(0f, 2f, -160f);
    public Vector3 departEndPosition = new Vector3(0f, 2f, -30f);
    public float departDuration = 2f;

    public Vector3 postJumpPosition = new Vector3(0f, 0f, 2900f);
    public Vector3 flightEndPosition = new Vector3(0f, 0f, 2960f);
    public float approachDuration = 2.5f;
    public float jumpFlashDuration = 0.2f;
    public HyperspaceEffect hyperspaceEffect;
    public AudioSource jumpAudio;

    bool playing;

    void Awake()
    {
        // Keep the player camera live so something renders before Play() runs
        // (e.g. while the Start Screen is up) - only swap cameras inside PlayRoutine.
    }

    public void Play()
    {
        if (playing) return;
        playing = true;
        StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        GameManager.Instance?.MarkIntroStarted();

        if (playerController != null) playerController.enabled = false;
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
            // Hand the MainCamera tag to whichever camera is actually
            // rendering. World-space UI (WorldSpaceUIFollow: dialogue, fade)
            // positions itself off Camera.main every frame - leaving no
            // camera tagged MainCamera during the whole cinematic made
            // Camera.main return null, so that UI froze in stale space
            // relative to a camera that was no longer being rendered from,
            // instead of tracking the cinematic camera's flight.
            playerCamera.tag = "Untagged";
            var playerListener = playerCamera.GetComponent<AudioListener>();
            if (playerListener != null) playerListener.enabled = false;
        }

        if (cinematicRigRoot != null) cinematicRigRoot.SetActive(true);
        // Snap to the flight's actual starting position before the camera
        // ever renders a frame - it used to only get set once LerpPosition
        // began, so the first dialogue line rendered from wherever the rig
        // was last left in the editor, then visibly popped to the real
        // start position the instant that line was dismissed.
        if (cinematicRigRoot != null) cinematicRigRoot.transform.localPosition = departStartPosition;
        if (cinematicCamera != null)
        {
            cinematicCamera.enabled = true;
            cinematicCamera.tag = "MainCamera";
        }
        if (cinematicLight != null) cinematicLight.enabled = true;
        if (secondaryLight != null) secondaryLight.enabled = true;
        if (interiorRoot != null) interiorRoot.SetActive(false);

        if (cinematicRigRoot != null)
        {
            var rigTransform = cinematicRigRoot.transform;

            // Captions are player-paced (click to continue) so the opening
            // reads like a deliberate cinematic beat instead of racing a
            // timer - the flight itself only starts once dismissed.
            if (DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Departing, {PlayerName}. Punching the jump drive in three.");

            // Phase 1: depart the local area.
            yield return LerpPosition(rigTransform, departStartPosition, departEndPosition, departDuration);

            // Phase 2: jump-cut to near the (far away) station - a visible
            // hyperspace streak burst + whoosh, then a quick flash to hide the teleport.
            if (jumpAudio != null && jumpAudio.clip != null) jumpAudio.Play();
            if (hyperspaceEffect != null) yield return hyperspaceEffect.Play();
            if (FadeController.Instance != null)
                yield return FadeController.Instance.FadeToBlack(jumpFlashDuration);
            rigTransform.localPosition = postJumpPosition;
            if (FadeController.Instance != null)
                yield return FadeController.Instance.FadeFromBlack(jumpFlashDuration);

            // The jump whoosh clip runs longer than the visual jump-cut - cut
            // it off here instead of letting it play out over the dialogue
            // and final approach.
            if (jumpAudio != null && jumpAudio.isPlaying) StartCoroutine(FadeOutAudio(jumpAudio, 0.6f));

            if (DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Jump complete. Bringing us in to dock.");

            // Phase 3: final approach to the station.
            yield return LerpPosition(rigTransform, postJumpPosition, flightEndPosition, approachDuration);
        }
        else
        {
            yield return new WaitForSeconds(3f);
        }

        if (FadeController.Instance != null)
            yield return FadeController.Instance.FadeToBlack(fadeOutTime);

        if (cinematicCamera != null)
        {
            cinematicCamera.enabled = false;
            cinematicCamera.tag = "Untagged";
        }
        if (cinematicLight != null) cinematicLight.enabled = false;
        if (secondaryLight != null) secondaryLight.enabled = false;
        if (cinematicRigRoot != null) cinematicRigRoot.SetActive(false);
        if (interiorRoot != null) interiorRoot.SetActive(true);
        if (xrLocomotion != null) xrLocomotion.SetActive(true);

        if (playerRoot != null && dockingBaySpawnPoint != null)
        {
            playerRoot.position = dockingBaySpawnPoint.position;
            playerRoot.rotation = dockingBaySpawnPoint.rotation;
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = true;
            playerCamera.tag = "MainCamera";
            var playerListener = playerCamera.GetComponent<AudioListener>();
            if (playerListener != null) playerListener.enabled = true;
        }
        if (playerController != null) playerController.enabled = true;

        if (FadeController.Instance != null)
            yield return FadeController.Instance.FadeFromBlack(fadeInTime);

        // Per direct feedback ("no indicator for what I'm supposed to
        // interact with... no guidance at all") - the idle pulse glow
        // alone wasn't enough for a first-time player to even know there
        // was a glow to look for. One explicit, one-time line up front
        // covering both what (the gold glow) and how (point + trigger)
        // beats scattering assumed-obvious visual cues across six
        // different objects with no shared explanation.
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Anything glowing gold is something you can interact with. Point your controller at it and pull the trigger.");

        GameManager.Instance?.MarkStoryStarted();
    }

    IEnumerator FadeOutAudio(AudioSource source, float time)
    {
        float startVolume = source.volume;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, t / time);
            yield return null;
        }
        source.Stop();
        source.volume = startVolume;
    }

    IEnumerator LerpPosition(Transform t, Vector3 from, Vector3 to, float duration)
    {
        t.localPosition = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            u = 1f - (1f - u) * (1f - u); // ease-out
            t.localPosition = Vector3.Lerp(from, to, u);
            yield return null;
        }
        t.localPosition = to;
    }
}
