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
    // Lengthened again (4s -> 6s) and made more intense per "make the
    // vibrations last a little longer, get a little more climactic."
    public float shakeDuration = 6f;
    // How hard the room shakes/how intense the light spikes get at the very
    // peak of the climax (t=1 on the squared ramp).
    public float maxShakeAmplitude = 0.08f;

    // wallsToHide/pedestal are used only for the shake jitter below (need
    // individual objects to nudge). Real bug found via direct feedback
    // ("the stuff behind me doesn't disappear either"): wallsToHide only
    // ever listed the Lab's own contents, but the Lab's only doorway opens
    // into ControlRoom (a completely separate room, never hidden) - hiding
    // just the Lab's door left a clear, unobstructed view straight into
    // it. "Everything disappears" now hides the whole Station root in one
    // call instead of a hand-maintained per-room object list, which is the
    // only way to actually guarantee nothing else is visible regardless of
    // what's connected to what.
    public GameObject[] wallsToHide;
    public GameObject pedestal;
    public GameObject stationRoot;
    // Found via direct feedback + a screenshot showing a tall dark pillar
    // and a small panel still floating in "empty space": the supply-locker
    // side puzzle (SupplyLocker, LockerDoorHinge, KeypadLock, Keycard,
    // KeycardSocket) and the room dust-mote particle systems all live as
    // separate root-level objects, never nested under Station at all -
    // hiding Station couldn't have caught them.
    public GameObject[] otherObjectsToHide;
    // Sealed shut for the duration so there's nothing visible through the
    // doorway when the player turns around.
    public DoorSwing labDoor;
    // 20s -> 8s -> 12s. 8s then read as "way too long" too (subjective, and
    // possibly compounded by the stray floating objects being distracting) -
    // 12s is the explicitly requested number.
    public float floatDuration = 12f;
    // The Starfield panoramic texture has a bright planet feature sitting
    // near the projection's pole, not the horizon band - the Panoramic
    // shader's _Rotation only exposes a single yaw axis, so anything near
    // that pole traces a visible circle around it rather than just panning
    // sideways like the rest of the sky does. That reads as "the planet is
    // spinning" if the rotation is fast enough to notice; there's no second
    // axis to rotate around instead, so slowing this down a lot is the
    // available fix, not eliminating the rotation entirely (which was the
    // whole point of this beat).
    public float skyboxSpinSpeed = 5f;

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
        // floatDuration. Breathing is not a continuous layer anymore - it
        // plays only for this one beat, per direct feedback ("mute the
        // breaths until you trigger the disappearing act, then that's the
        // only part you can hear the breath").
        SetRoomVisible(false);
        if (labDoor != null) StartCoroutine(labDoor.SwingClosed());
        if (screechAudio != null) screechAudio.Stop();
        if (glitchAudioSource != null) glitchAudioSource.Stop();
        AmbienceManager.Instance?.SetMuted(true);
        AmbienceManager.Instance?.PlayBreathingOnce();

        yield return SpinSkyboxRoutine(floatDuration);

        AmbienceManager.Instance?.StopBreathing();
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
        // The real fix: the whole Station (every room, not just the Lab)
        // hides in one call, so nothing connected to the Lab - through the
        // door, or anywhere else - can be left visible by omission.
        if (stationRoot != null) stationRoot.SetActive(visible);
        if (wallsToHide != null)
            foreach (var w in wallsToHide) if (w != null) w.SetActive(visible);
        if (pedestal != null) pedestal.SetActive(visible);
        // Supply-locker puzzle + dust-mote particle systems - all root-level,
        // outside Station entirely, so the above never touched them.
        if (otherObjectsToHide != null)
            foreach (var o in otherObjectsToHide) if (o != null) o.SetActive(visible);
    }

    void ApplyEmission(Color c)
    {
        if (artifactRenderer == null) return;
        mpb.SetColor(EmissionColor, c);
        artifactRenderer.SetPropertyBlock(mpb);
    }
}
