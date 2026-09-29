using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to Artifact. Activating it shifts its material from dim blue to pulsing
// white, then: the room shakes and the lights flicker, everything disappears
// and the player is floating in open space with the stars wheeling past, then
// it all snaps back to normal before handing off to the End Screen. No crew
// silhouettes - an earlier version had them, cut per direct feedback after
// playtesting ("not scary, loses all the cool vibes... no silhouettes no
// nothing else").
public class ArtifactInteractable : MonoBehaviour, IDesktopInteractable
{
    public const string StepId = "artifact";

    public Renderer artifactRenderer;
    public AudioSource screechAudio;
    // Short, harsh distortion/glitch stingers fired at random through the
    // shake - layered under the screech to sell "reality is breaking", not
    // just "the light is flickering".
    public AudioSource glitchAudioSource;
    public AudioClip[] glitchClips;
    [Range(0f, 1f)] public float glitchClipChancePerTick = 0.35f;
    public EndScreenController endScreen;
    public Color dimBlue = new Color(0.1f, 0.3f, 0.6f);
    public Color brightWhite = Color.white;
    public float revealDuration = 1.1f;
    public float resetDuration = 0.9f;
    public float idlePulseSpeed = 1.2f;

    // Room lights flicker (near-off to a bright spike) rather than cycling
    // color - reads as a physical power failure, which fits "the room
    // shakes and the lights flicker" more literally than a rainbow strobe.
    public Light[] glitchLights;
    public float glitchInterval = 0.06f;
    // Longer, with a squared (not linear) ramp - stays subtle for most of
    // the window then ramps sharply right at the end, so it reads as
    // building to a real climax instead of a flat shake that's just "on."
    public float shakeDuration = 4f;
    // How hard the room shakes/how intense the light spikes get at the very
    // peak of the climax (t=1 on the squared ramp).
    public float maxShakeAmplitude = 0.06f;

    // "Everything disappears": hide the room and the pedestal so the
    // scene's real default skybox (a starfield, already what
    // RenderSettings.skybox is outside this effect) becomes visible in
    // every direction - the player is standing in the same spot the whole
    // time, it just reads as floating in open space once there's no room
    // left to see. No separate "space" skybox material needed for this.
    public GameObject[] wallsToHide;
    public GameObject pedestal;
    // Sealed shut for the duration so there's nothing visible through the
    // doorway when the player turns around.
    public DoorSwing labDoor;
    // The earlier 2s->5s->12s bumps never actually took effect - the scene's
    // already-open ArtifactInteractable instance had these fields serialized
    // (in-memory) at their very first value the moment the fields were
    // introduced, and every later code-default change silently never
    // reached that live instance. Fixed by explicitly setting these on the
    // instance directly and pinning them in the scene file, not just
    // bumping the C# default again. Confirmed the actual bug via a real
    // eval readback: shakeDuration/floatDuration/maxShakeAmplitude/
    // skyboxSpinSpeed were ALL still their original 774510a values no
    // matter how many times the default here was edited.
    public float floatDuration = 20f;
    // Degrees/second the skybox rotates during the float, so the stars
    // visibly wheel past instead of just sitting there - this is what
    // sells "spinning," not any change to the player's own transform
    // (deliberately not touched - safe regardless of desktop or XR rig).
    public float skyboxSpinSpeed = 15f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int SkyboxRotation = Shader.PropertyToID("_Rotation");
    MaterialPropertyBlock mpb;
    bool triggered;
    Color[] glitchLightBaseColors;
    float[] glitchLightBaseIntensities;
    Vector3[] wallBasePositions;
    Vector3 pedestalBasePosition;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();

        if (wallsToHide != null)
        {
            wallBasePositions = new Vector3[wallsToHide.Length];
            for (int i = 0; i < wallsToHide.Length; i++)
                if (wallsToHide[i] != null) wallBasePositions[i] = wallsToHide[i].transform.localPosition;
        }
        if (pedestal != null) pedestalBasePosition = pedestal.transform.localPosition;

        if (glitchLights != null)
        {
            glitchLightBaseColors = new Color[glitchLights.Length];
            glitchLightBaseIntensities = new float[glitchLights.Length];
            for (int i = 0; i < glitchLights.Length; i++)
            {
                if (glitchLights[i] == null) continue;
                glitchLightBaseColors[i] = glitchLights[i].color;
                glitchLightBaseIntensities[i] = glitchLights[i].intensity;
            }
        }
    }

    void Start()
    {
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(args => { HapticUtility.Pulse(args.interactorObject, 0.7f, 0.2f); OnActivate(); });
        }
    }

    void Update()
    {
        if (triggered) return;
        if (!IsCurrentlyInteractable()) { ApplyEmission(Color.black); return; }
        ApplyEmission(InteractionSequencer.PulseColor(idlePulseSpeed, 0.2f, 0.45f));
    }

    public bool IsCurrentlyInteractable()
    {
        if (triggered) return false;
        return InteractionSequencer.Instance == null || InteractionSequencer.Instance.IsCurrentStep(StepId);
    }

    public void OnHoverEnter() { }
    public void OnHoverExit() { }

    public void OnActivate()
    {
        if (!IsCurrentlyInteractable()) return;
        triggered = true;
        StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
    {
        if (!LabState.IntroShown)
        {
            LabState.IntroShown = true;
            if (DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "This is the room the journal entry meant.");
        }

        float t = 0f;
        while (t < revealDuration)
        {
            t += Time.deltaTime;
            ApplyEmission(Color.Lerp(dimBlue, brightWhite, t / revealDuration) * (2.5f + Mathf.PingPong(t * 8f, 1.5f)));
            yield return null;
        }

        if (screechAudio != null && screechAudio.clip != null) screechAudio.Play();

        // The room shakes and the lights flicker, building for shakeDuration.
        yield return ShakeAndFlickerRoutine(shakeDuration);

        // Everything disappears - hide the room, seal the door, cut the
        // ambient drone/bass rumble (plus anything tailing off from the
        // shake) and float in open space with the stars spinning for
        // floatDuration. Breathing keeps playing through this - it's the
        // one sound that's still "you" even once the room is gone.
        SetRoomVisible(false);
        if (labDoor != null) StartCoroutine(labDoor.SwingClosed());
        if (screechAudio != null) screechAudio.Stop();
        if (glitchAudioSource != null) glitchAudioSource.Stop();
        AmbienceManager.Instance?.SetMuted(true);

        yield return SpinSkyboxRoutine(floatDuration);

        AmbienceManager.Instance?.SetMuted(false);

        // Back to normal.
        SetRoomVisible(true);
        if (labDoor != null) StartCoroutine(labDoor.SwingOpen());

        t = 0f;
        Color from = brightWhite;
        while (t < resetDuration)
        {
            t += Time.deltaTime;
            ApplyEmission(Color.Lerp(from, dimBlue, t / resetDuration));
            yield return null;
        }
        ApplyEmission(dimBlue);

        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "{PlayerName}. Whatever that was, whatever they were, it's done now. Don't touch it again.");
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("{PlayerName}", "Wasn't planning on it.");

        yield return new WaitForSeconds(0.6f);
        InteractionSequencer.Instance?.CompleteStep(StepId);
        endScreen?.Show();
    }

    IEnumerator ShakeAndFlickerRoutine(float duration)
    {
        float elapsed = 0f;
        float flickerTimer = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            flickerTimer += Time.deltaTime;

            // Squared, not linear - stays mild for most of the window, then
            // ramps sharply right at the end. A real climax builds instead
            // of just being "on" at a constant rate the whole time.
            float buildT = Mathf.Pow(Mathf.Clamp01(elapsed / duration), 2f);
            float amplitude = maxShakeAmplitude * buildT;
            if (wallsToHide != null)
            {
                for (int i = 0; i < wallsToHide.Length; i++)
                {
                    if (wallsToHide[i] == null) continue;
                    wallsToHide[i].transform.localPosition = wallBasePositions[i] + Random.insideUnitSphere * amplitude;
                }
            }
            if (pedestal != null)
                pedestal.transform.localPosition = pedestalBasePosition + Random.insideUnitSphere * amplitude;

            if (flickerTimer >= glitchInterval)
            {
                flickerTimer = 0f;
                if (glitchLights != null)
                {
                    for (int i = 0; i < glitchLights.Length; i++)
                    {
                        if (glitchLights[i] == null) continue;
                        // Spikes get more frequent and more intense as the
                        // build climbs - a power failure that's getting worse,
                        // not a fixed flicker rate from the first frame.
                        bool spike = Random.value < Mathf.Lerp(0.1f, 0.55f, buildT);
                        glitchLights[i].intensity = spike
                            ? glitchLightBaseIntensities[i] * Random.Range(1.5f, Mathf.Lerp(2f, 3.2f, buildT))
                            : glitchLightBaseIntensities[i] * Random.Range(0f, 0.15f);
                    }
                }
                if (glitchAudioSource != null && glitchClips != null && glitchClips.Length > 0
                    && Random.value < glitchClipChancePerTick)
                {
                    var clip = glitchClips[Random.Range(0, glitchClips.Length)];
                    if (clip != null) glitchAudioSource.PlayOneShot(clip, Random.Range(0.5f, 1f));
                }
            }
            yield return null;
        }

        // Restore exact original positions/light state - a ramping random
        // jitter never lands back on zero on its own.
        if (wallsToHide != null)
            for (int i = 0; i < wallsToHide.Length; i++)
                if (wallsToHide[i] != null) wallsToHide[i].transform.localPosition = wallBasePositions[i];
        if (pedestal != null) pedestal.transform.localPosition = pedestalBasePosition;
        if (glitchLights != null)
            for (int i = 0; i < glitchLights.Length; i++)
            {
                if (glitchLights[i] == null) continue;
                glitchLights[i].color = glitchLightBaseColors[i];
                glitchLights[i].intensity = glitchLightBaseIntensities[i];
            }
    }

    IEnumerator SpinSkyboxRoutine(float duration)
    {
        var sky = RenderSettings.skybox;
        bool hasRotation = sky != null && sky.HasProperty(SkyboxRotation);
        float startRotation = hasRotation ? sky.GetFloat(SkyboxRotation) : 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (hasRotation)
                sky.SetFloat(SkyboxRotation, (startRotation + skyboxSpinSpeed * t) % 360f);
            yield return null;
        }

        if (hasRotation) sky.SetFloat(SkyboxRotation, startRotation);
    }

    void SetRoomVisible(bool visible)
    {
        if (wallsToHide != null)
            foreach (var w in wallsToHide) if (w != null) w.SetActive(visible);
        if (pedestal != null) pedestal.SetActive(visible);
    }

    void ApplyEmission(Color c)
    {
        if (artifactRenderer == null) return;
        mpb.SetColor(EmissionColor, c);
        artifactRenderer.SetPropertyBlock(mpb);
    }
}
