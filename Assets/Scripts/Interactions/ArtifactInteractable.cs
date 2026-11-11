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
    // Alarm siren, looping for the whole shake - per request ("i want like
    // an alarm noise to play when the vibrating is happening too kind of
    // like a siren").
    public AudioSource sirenAudio;
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
    // So ObjectiveHUD can stop showing "NEXT: Examine the artifact" once
    // the ending is actually underway - InteractionSequencer.CompleteStep
    // for this step only fires at the very end of the whole reveal/shake/
    // float sequence, so without this the HUD would otherwise linger
    // through the entire dramatic beat.
    public bool HasTriggered => triggered;
    Color[] glitchLightBaseColors;
    float[] glitchLightBaseIntensities;
    Vector3[] wallBasePositions;
    Vector3 pedestalBasePosition;
    FloatingHintText hint;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        hint = FloatingHintText.Attach(transform);

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
            // Wasn't wired at all before - OnHoverEnter/OnHoverExit existed
            // as empty stubs, so the Artifact never got the same "PULL
            // TRIGGER" hint every other simple-select interactable now has.
            interactable.hoverEntered.AddListener(_ => OnHoverEnter());
            interactable.hoverExited.AddListener(_ => OnHoverExit());
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

    public void OnHoverEnter()
    {
        if (!IsCurrentlyInteractable()) return;
        hint.Show("PULL TRIGGER");
    }

    public void OnHoverExit()
    {
        hint.Hide();
    }

    // Deadlock fix, confirmed via a real console error: "Coroutine couldn't
    // be started because the game object 'Artifact' is inactive." The
    // Artifact GameObject (running this whole sequence) is itself nested
    // inside Station - the moment the float phase hides Station, it also
    // deactivates Artifact, which pauses every coroutine running on it,
    // including the one that's supposed to reactivate it later. A frozen
    // coroutine can't wake itself back up - the player was stuck in the
    // silent float forever. GameManager is a persistent root-level object
    // never hidden by anything here, so running coroutines on it instead
    // of `this` keeps them ticking regardless of Artifact's own active
    // state. Used for every StartCoroutine call in this whole sequence,
    // not just the outer one - the door-swing calls below would hit the
    // exact same failure the instant they're called while Artifact is
    // inactive.
    MonoBehaviour CoroutineHost => GameManager.Instance != null ? (MonoBehaviour)GameManager.Instance : this;

    bool roomHidden;
    bool endingComplete;

    public void OnActivate()
    {
        if (!IsCurrentlyInteractable()) return;
        triggered = true;
        StoryState.EndingStarted = true;
        hint.Hide();
        CoroutineHost.StartCoroutine(RevealRoutine());
    }

    void StartPreHideWatchdog()
    {
        // Covers reveal+shake specifically - the one leg of this sequence
        // with no player-input dependency before the room ever hides (the
        // one dialogue wait that can precede it, LabState.IntroShown, is
        // already true by the time Artifact fires in normal sequential
        // play, since Recorder - step 5 of 6 - sets it first). Deliberately
        // tighter and separately scoped from FloatWatchdog below, which
        // only covers the float itself and has no visibility into whether
        // the room ever hid in the first place.
        CoroutineHost.StartCoroutine(PreHideWatchdog());
    }

    IEnumerator PreHideWatchdog()
    {
        float deadline = revealDuration + shakeDuration + 10f;
        yield return new WaitForSecondsRealtime(deadline);
        if (endingComplete || roomHidden) yield break;
        Debug.LogWarning("[ArtifactEnding] PreHideWatchdog fired at realtime=" + Time.realtimeSinceStartup + " - room never hid " + deadline + "s after trigger. Forcing completion.");
        ForceComplete();
    }

    IEnumerator RevealRoutine()
    {
        if (!LabState.IntroShown)
        {
            LabState.IntroShown = true;
            if (DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "This is the room the journal entry meant.");
        }

        // ECHO has been steering the player here the whole run.
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Go on, {PlayerName}. This is what we came for.");
        if (StoryState.Informed && DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("{PlayerName}", "We?");

        // Started only now: the watchdog's deadline is real time, and the
        // lines above wait on the player.
        StartPreHideWatchdog();
        if (LightingDirector.Instance != null) LightingDirector.Instance.Suspended = true;

        float t = 0f;
        while (t < revealDuration)
        {
            t += Time.deltaTime;
            ApplyEmission(Color.Lerp(dimBlue, brightWhite, t / revealDuration) * (2.5f + Mathf.PingPong(t * 8f, 1.5f)));
            yield return null;
        }

        if (screechAudio != null && screechAudio.clip != null) screechAudio.Play();

        Debug.Log("[ArtifactEnding] shake starting at realtime=" + Time.realtimeSinceStartup);
        // The room shakes and the lights flicker, building for shakeDuration.
        yield return ShakeAndFlickerRoutine(shakeDuration);
        Debug.Log("[ArtifactEnding] shake done at realtime=" + Time.realtimeSinceStartup);

        // Everything disappears - hide the room, seal the door, cut the
        // ambient drone/bass rumble (plus anything tailing off from the
        // shake) and float in open space with the stars spinning for
        // floatDuration. Breathing is not a continuous layer anymore - it
        // plays only for this one beat, per direct feedback ("mute the
        // breaths until you trigger the disappearing act, then that's the
        // only part you can hear the breath").
        FreezePlayer();
        SetRoomVisible(false);
        roomHidden = true;
        if (labDoor != null) CoroutineHost.StartCoroutine(labDoor.SwingClosed());
        if (screechAudio != null) screechAudio.Stop();
        if (glitchAudioSource != null) glitchAudioSource.Stop();
        AmbienceManager.Instance?.SetMuted(true);
        AmbienceManager.Instance?.PlayBreathingOnce();
        // If the player read enough of the crew's logs, they know who is
        // breathing next to them, and now they can make out the words.
        if (StoryState.Informed) CoroutineHost.StartCoroutine(FloatWhispers());

        // Belt-and-suspenders: this exact beat ("space never comes back")
        // has been reported multiple times on real Quest hardware and could
        // not be reproduced after repeated live Editor testing post the
        // CoroutineHost deadlock fix - GameManager/Systems verified never
        // hidden, no Time.timeScale manipulation anywhere in the project.
        // Whatever the real cause turns out to be, the player should never
        // be permanently stranded over it. This runs as a fully independent
        // coroutine on the same host - if the main sequence is somehow
        // blocked rather than just slow, this still fires and forces
        // recovery, well past any legitimate duration this beat should take.
        CoroutineHost.StartCoroutine(FloatWatchdog(floatDuration));

        Debug.Log("[ArtifactEnding] float starting at realtime=" + Time.realtimeSinceStartup + " stationActive=" + (stationRoot != null && stationRoot.activeSelf));
        yield return SpinSkyboxRoutine(floatDuration);
        Debug.Log("[ArtifactEnding] float done at realtime=" + Time.realtimeSinceStartup);

        AmbienceManager.Instance?.StopBreathing();
        AmbienceManager.Instance?.SetMuted(false);

        // Back to normal.
        SetRoomVisible(true);
        RestorePlayer();
        if (LightingDirector.Instance != null) LightingDirector.Instance.Suspended = false;
        // Camera pose logged here specifically to test a theory from
        // direct feedback ("didn't see the station reappear before the
        // dialogue triggers") - the float has no visual anchor, so the
        // player can end up facing any direction by the time it ends;
        // the dialogue snaps to face them regardless (WorldSpaceUIFollow)
        // while the room, being real geometry, doesn't. This records
        // exactly where the player was looking at the moment the room
        // actually reactivates, to confirm or rule that out directly
        // instead of guessing again.
        var cam = Camera.main;
        Debug.Log("[ArtifactEnding] station reactivated at realtime=" + Time.realtimeSinceStartup
            + " stationActive=" + (stationRoot != null && stationRoot.activeSelf)
            + " camPos=" + (cam != null ? cam.transform.position.ToString() : "null")
            + " camForward=" + (cam != null ? cam.transform.forward.ToString() : "null")
            + " pedestalPos=" + (pedestal != null ? pedestal.transform.position.ToString() : "null"));
        if (labDoor != null) CoroutineHost.StartCoroutine(labDoor.SwingOpen());

        t = 0f;
        Color from = brightWhite;
        while (t < resetDuration)
        {
            t += Time.deltaTime;
            ApplyEmission(Color.Lerp(from, dimBlue, t / resetDuration));
            yield return null;
        }
        ApplyEmission(dimBlue);

        if (StoryState.Informed)
        {
            if (DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "You heard them. So did I, {PlayerName}. Six months ago, five voices on a dead band. That's why I brought us here.");
            if (DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("{PlayerName}", "You told me this was salvage.");
            if (DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "It was a rescue. I just couldn't open the doors myself.");
        }
        else
        {
            if (DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "{PlayerName}. Whatever that was, it's done now. Don't touch it again.");
            if (DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("{PlayerName}", "Wasn't planning on it.");
        }

        yield return new WaitForSeconds(0.6f);
        if (!endingComplete)
        {
            endingComplete = true;
            InteractionSequencer.Instance?.CompleteStep(StepId);
            DisableBlockingColliders();
            endScreen?.Show(TouchEnding);
        }
    }

    EndingKind TouchEnding => StoryState.Informed ? EndingKind.Day181 : EndingKind.Salvage;

    IEnumerator FloatWhispers()
    {
        V2Audio.Play2D("FloatWhispers", 0.9f);
        yield return new WaitForSeconds(1.2f);
        AmbientCaption.Say("", "\"...is someone there?...\"", 2.2f);
        yield return new WaitForSeconds(3.2f);
        AmbientCaption.Say("", "\"...there's six now...\"", 2.2f);
        yield return new WaitForSeconds(3.2f);
        AmbientCaption.Say("", "\"...don't let it close...\"", 2.2f);
    }

    // The other final choice: the containment lever by the lab door. No
    // float, nothing is hidden and the player is never moved, so none of the
    // free-fall safeguards are needed until the end screen itself.
    public void SealInstead()
    {
        if (triggered) return;
        triggered = true;
        StoryState.EndingStarted = true;
        hint.Hide();
        CoroutineHost.StartCoroutine(SealRoutine());
    }

    IEnumerator SealRoutine()
    {
        if (LightingDirector.Instance != null) LightingDirector.Instance.Suspended = true;
        V2Audio.Play2D("PowerDown", 0.9f);
        if (sirenAudio != null && sirenAudio.clip != null) { sirenAudio.loop = true; sirenAudio.Play(); }

        // Lab lights swing to alarm red while the artifact's glow drains.
        float t = 0f;
        const float drain = 3.5f;
        while (t < drain)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / drain);
            ApplyEmission(Color.Lerp(InteractionSequencer.HighlightColor * 1.5f, Color.black, u));
            if (glitchLights != null)
                for (int i = 0; i < glitchLights.Length; i++)
                {
                    if (glitchLights[i] == null) continue;
                    glitchLights[i].color = Color.Lerp(glitchLightBaseColors[i], Color.red, u);
                    glitchLights[i].intensity = glitchLightBaseIntensities[i] * Mathf.Lerp(1f, 0.45f, u) * (0.8f + 0.2f * Mathf.Sin(t * 14f));
                }
            if (u > 0.3f && !sealInterrupted)
            {
                sealInterrupted = true;
                AmbientCaption.Say("ECHO", "{PlayerName}, wait. Don't cut it. They're still", 2.6f);
            }
            yield return null;
        }
        if (sirenAudio != null) sirenAudio.Stop();
        ApplyEmission(Color.black);
        V2Audio.Play2D("LeverClunk", 1f);
        AmbienceManager.Instance?.SetMuted(true);

        yield return new WaitForSeconds(2.5f);
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("{PlayerName}", "ECHO?");
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("", "Nothing answers. The band is empty.");

        if (!endingComplete)
        {
            endingComplete = true;
            InteractionSequencer.Instance?.CompleteStep(StepId);
            DisableBlockingColliders();
            endScreen?.Show(EndingKind.Sealed);
        }
    }

    bool sealInterrupted;

    // Real bug, confirmed against the XR Interaction Toolkit source
    // (NearFarInteractor.cs ProcessInteractor -> shouldProcess2dHit): the
    // far-ray interactor picks whichever hit is closer, UI or 3D physics,
    // and drops the UI hit entirely if a collider is nearer - it doesn't
    // just prefer UI. EndScreenCanvas snaps to 1.6m in front of the camera
    // (WorldSpaceUIFollow), but SetRoomVisible(true) re-activates the whole
    // Station right before the end screen shows, putting every room
    // collider back in play - and the player is standing in the middle of
    // it. First pass only disabled the Pedestal + this object's own
    // collider, which turned out not to be enough: `pedestal` is unset in
    // the Inspector (confirmed via get_serialized_fields - pedestalPos
    // logs null every time), and the actual object that's always in view
    // is whatever's under Station, not a specific named field - wallsToHide
    // alone lists 44 objects (Floor, Ceiling, every wall, Desk, Chair,
    // Shelves, the real Pedestal, Recorder, 20+ clutter pieces). Any one of
    // those can be the collider that's closer than the canvas depending on
    // where the player ends up standing - there's no single fixed culprit
    // to name. Disabling colliders across stationRoot + wallsToHide +
    // otherObjectsToHide covers the entire reactivated room in one pass
    // instead of guessing at individual objects again. Safe: nothing in
    // the room needs to be physically interactable once the ending has
    // actually completed, and Restart reloads the whole scene fresh
    // anyway, so these come back enabled on replay.
    // Root cause of "station never comes back" / "restart does nothing":
    // hiding Station removes the floor, the XR rig's GravityProvider keeps
    // applying gravity for the whole float (~12s = ~700m of free fall,
    // confirmed from device logs: camPos y=-669 when the room reactivated),
    // so the player is hundreds of metres under the station and the end
    // screen canvas (which only repositions on a >30deg turn) is left
    // behind. Freeze gravity + locomotion for the float and put the rig
    // back exactly where it was.
    Transform frozenRig;
    Vector3 frozenPos;
    Quaternion frozenRot;
    Behaviour[] frozenProviders;

    void FreezePlayer()
    {
        var cam = Camera.main;
        if (cam == null) return;
        frozenRig = cam.transform.root;
        frozenPos = frozenRig.position;
        frozenRot = frozenRig.rotation;
        var list = new System.Collections.Generic.List<Behaviour>();
        foreach (var p in frozenRig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true))
            if (p.enabled) { p.enabled = false; list.Add(p); }
        frozenProviders = list.ToArray();
    }

    void RestorePlayer()
    {
        if (frozenRig == null) return;
        var cc = frozenRig.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        frozenRig.SetPositionAndRotation(frozenPos, frozenRot);
        if (cc != null) cc.enabled = true;
        if (frozenProviders != null)
            foreach (var p in frozenProviders) if (p != null) p.enabled = true;
        Debug.Log("[ArtifactEnding] player restored to " + frozenPos);
        frozenRig = null;
    }

    void DisableBlockingColliders()
    {
        // The floor is one of the colliders disabled below, so stop gravity
        // and locomotion first or the player free-falls forever (confirmed
        // on device). Restart reloads the scene, which resets the rig.
        var cam = Camera.main;
        if (cam != null)
            foreach (var p in cam.transform.root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true))
                p.enabled = false;
        int count = 0;
        void DisableAll(GameObject go)
        {
            if (go == null) return;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) { col.enabled = false; count++; }
        }
        DisableAll(stationRoot);
        DisableAll(gameObject);
        if (wallsToHide != null) foreach (var w in wallsToHide) DisableAll(w);
        if (otherObjectsToHide != null) foreach (var o in otherObjectsToHide) DisableAll(o);
        Debug.Log("[EndScreen] Disabled " + count + " room colliders ahead of Show() to stop them blocking the far-ray UI hit");
    }

    // Shared recovery path for both watchdogs - skips straight to "back to
    // normal" rather than trying to resume the interrupted animation
    // in-place, since whatever state the shake/float left things in at the
    // point of hanging isn't known to be safe to continue from.
    void ForceComplete()
    {
        if (endingComplete) return;
        endingComplete = true;
        SetRoomVisible(true);
        RestorePlayer();
        if (labDoor != null) CoroutineHost.StartCoroutine(labDoor.SwingOpen());
        AmbienceManager.Instance?.StopBreathing();
        AmbienceManager.Instance?.SetMuted(false);
        ApplyEmission(dimBlue);
        if (LightingDirector.Instance != null) LightingDirector.Instance.Suspended = false;
        InteractionSequencer.Instance?.CompleteStep(StepId);
        DisableBlockingColliders();
        endScreen?.Show(TouchEnding);
    }

    IEnumerator ShakeAndFlickerRoutine(float duration)
    {
        if (sirenAudio != null && sirenAudio.clip != null)
        {
            sirenAudio.loop = true;
            sirenAudio.Play();
        }

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
                        // Alarm-red on the spikes themselves - per request
                        // ("trigger an alarm siren where like the red
                        // flashing happens as well with everything else").
                        // Only the spike frames go red so it still reads as
                        // flicker/strobe, not a flat red wash the whole time;
                        // buildT pushes it toward fully saturated red as the
                        // climax approaches, same climb as the intensity.
                        glitchLights[i].color = spike
                            ? Color.Lerp(glitchLightBaseColors[i], Color.red, Mathf.Lerp(0.5f, 1f, buildT))
                            : glitchLightBaseColors[i];
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

        if (sirenAudio != null) sirenAudio.Stop();

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

    IEnumerator FloatWatchdog(float expectedFloatDuration)
    {
        // Tightened from +15s to +8s - the original buffer meant a genuinely
        // stuck player could be waiting close to 30s total before recovery
        // kicked in, which is a long time to sit wondering if the whole
        // game is broken.
        float deadline = expectedFloatDuration + 8f;
        yield return new WaitForSecondsRealtime(deadline);
        if (endingComplete) yield break;
        if (stationRoot != null && !stationRoot.activeSelf)
        {
            Debug.LogWarning("[ArtifactEnding] FloatWatchdog fired at realtime=" + Time.realtimeSinceStartup + " - station still hidden " + deadline + "s after the float began. Forcing recovery.");
            ForceComplete();
        }
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
