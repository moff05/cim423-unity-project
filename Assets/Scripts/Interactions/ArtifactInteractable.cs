using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to Artifact. Activating it shifts its material from dim blue to pulsing
// white, reveals the five crew silhouettes for a few seconds, then resets and
// hands off to the End Screen.
public class ArtifactInteractable : MonoBehaviour, IDesktopInteractable
{
    public const string StepId = "artifact";

    public Renderer artifactRenderer;
    public GameObject[] crewSilhouettes;
    public AudioSource screechAudio;
    // Short, harsh distortion/glitch stingers fired at random through the
    // reveal - layered under the screech to sell "reality is breaking",
    // not just "the light is flickering".
    public AudioSource glitchAudioSource;
    public AudioClip[] glitchClips;
    [Range(0f, 1f)] public float glitchClipChancePerTick = 0.35f;
    public EndScreenController endScreen;
    public Color dimBlue = new Color(0.1f, 0.3f, 0.6f);
    public Color brightWhite = Color.white;
    public float revealDuration = 1.1f;
    public float holdDuration = 1.8f;
    public float resetDuration = 0.9f;
    public float idlePulseSpeed = 1.2f;

    // Room lights that glitch through vivid colors while the silhouettes are
    // revealed, synced with the screech - sells the "walls cut out" moment
    // as something wrong with reality, not just the artifact glowing.
    public Light[] glitchLights;
    public float glitchInterval = 0.06f;

    // "The walls cut out": hide the room's walls and the pedestal, swap in an
    // alien skybox so it's visible through the gaps, for the duration of the
    // reveal - then everything snaps back.
    public GameObject[] wallsToHide;
    public GameObject pedestal;
    public Material alienSkyboxMaterial;
    // Sealed shut for the duration of the reveal so there's nothing visible
    // through the doorway when the player turns around - the alien skybox
    // is the only thing visible in every direction, not just where the
    // walls used to be.
    public DoorSwing labDoor;
    // A large sphere enclosing the whole room, textured with the alien
    // pano and rendered back-face-in so it's visible from inside - this is
    // the real guarantee that literally nothing (doors, distant planets,
    // other rooms) is visible in any direction during the reveal, instead
    // of relying on hiding every individual object that might be in view.
    public GameObject glitchDome;
    Material originalSkybox;

    // Silhouettes snap-teleport around their starting spot instead of
    // standing still (or smoothly flying, which read as balletic) - see
    // FlySilhouettesRoutine below for the jump-cut + flicker behavior.
    public float flyRadius = 1.6f;
    public float flyRetargetInterval = 0.35f;
    // Restrained, sickly palette instead of a full neon rainbow - deep
    // blood red, bile green, a near-black violet, and harsh white strobes.
    // The old set (bright cyan/pink/purple cycling fast) read as a rave
    // filter, not dread; horror lighting stays narrow and wrong-feeling.
    static readonly Color[] GlitchColors =
    {
        new Color(0.55f, 0.04f, 0.04f), new Color(0.4f, 0.55f, 0.12f), new Color(0.12f, 0f, 0.18f),
        new Color(1f, 1f, 1f), new Color(0.25f, 0.3f, 0.35f)
    };

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;
    bool triggered;
    Color[] glitchLightBaseColors;
    float[] glitchLightBaseIntensities;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        SetSilhouettesActive(false);
        // Cached once, here, rather than every time the glitch turns on -
        // re-caching on each toggle risks capturing the alien skybox
        // itself as "original" if the glitch is ever triggered twice
        // without a clean off in between.
        originalSkybox = RenderSettings.skybox;

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

        SetSilhouettesActive(true);
        if (screechAudio != null && screechAudio.clip != null) screechAudio.Play();
        SetEnvironmentGlitch(true);
        if (labDoor != null) StartCoroutine(labDoor.SwingClosed());
        StartCoroutine(FlySilhouettesRoutine(holdDuration));

        yield return GlitchLightsRoutine(holdDuration);

        SetEnvironmentGlitch(false);
        SetSilhouettesActive(false);
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

    IEnumerator GlitchLightsRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += glitchInterval;
            if (glitchLights != null)
            {
                foreach (var l in glitchLights)
                {
                    if (l == null) continue;
                    l.color = GlitchColors[Random.Range(0, GlitchColors.Length)];
                    l.intensity = Random.Range(1.5f, 4f);
                }
            }
            if (glitchAudioSource != null && glitchClips != null && glitchClips.Length > 0
                && Random.value < glitchClipChancePerTick)
            {
                var clip = glitchClips[Random.Range(0, glitchClips.Length)];
                if (clip != null) glitchAudioSource.PlayOneShot(clip, Random.Range(0.5f, 1f));
            }
            yield return new WaitForSeconds(glitchInterval);
        }

        if (glitchLights != null)
        {
            for (int i = 0; i < glitchLights.Length; i++)
            {
                if (glitchLights[i] == null) continue;
                glitchLights[i].color = glitchLightBaseColors[i];
                glitchLights[i].intensity = glitchLightBaseIntensities[i];
            }
        }
    }

    // Snaps between positions instead of smoothly flying there - a graceful
    // float-and-spin read as balletic/whimsical, not wrong. A jump-cut
    // teleport every retarget tick (with a couple of quick visibility
    // flickers around each snap) reads as broken reality instead.
    public float flickerChancePerSnap = 0.6f;
    public float flickerBlinkDuration = 0.04f;

    IEnumerator FlySilhouettesRoutine(float duration)
    {
        if (crewSilhouettes == null || crewSilhouettes.Length == 0) yield break;

        var basePos = new Vector3[crewSilhouettes.Length];
        for (int i = 0; i < crewSilhouettes.Length; i++)
        {
            if (crewSilhouettes[i] == null) continue;
            basePos[i] = crewSilhouettes[i].transform.position;
        }

        float retargetTimer = 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            retargetTimer += Time.deltaTime;
            if (retargetTimer >= flyRetargetInterval)
            {
                retargetTimer = 0f;
                for (int i = 0; i < crewSilhouettes.Length; i++)
                {
                    if (crewSilhouettes[i] == null) continue;
                    var tr = crewSilhouettes[i].transform;
                    tr.position = basePos[i] + Random.insideUnitSphere * flyRadius;
                    // Snap to a random facing too - a spin is smooth motion,
                    // a sudden wrong-way stare is not.
                    tr.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    if (Random.value < flickerChancePerSnap)
                        StartCoroutine(FlickerOnce(crewSilhouettes[i]));
                }
            }
            yield return null;
        }

        for (int i = 0; i < crewSilhouettes.Length; i++)
        {
            if (crewSilhouettes[i] == null) continue;
            crewSilhouettes[i].transform.position = basePos[i];
        }
    }

    IEnumerator FlickerOnce(GameObject go)
    {
        if (go == null) yield break;
        go.SetActive(false);
        yield return new WaitForSeconds(flickerBlinkDuration);
        if (go != null) go.SetActive(true);
    }

    void SetEnvironmentGlitch(bool active)
    {
        if (active)
        {
            if (alienSkyboxMaterial != null) RenderSettings.skybox = alienSkyboxMaterial;
        }
        else
        {
            RenderSettings.skybox = originalSkybox;
        }

        if (wallsToHide != null)
            foreach (var w in wallsToHide) if (w != null) w.SetActive(!active);
        if (pedestal != null) pedestal.SetActive(!active);
        if (glitchDome != null) glitchDome.SetActive(active);
    }

    void ApplyEmission(Color c)
    {
        if (artifactRenderer == null) return;
        mpb.SetColor(EmissionColor, c);
        artifactRenderer.SetPropertyBlock(mpb);
    }

    void SetSilhouettesActive(bool active)
    {
        if (crewSilhouettes == null) return;
        foreach (var go in crewSilhouettes)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
