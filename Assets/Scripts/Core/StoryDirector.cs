using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runs the v2 story beats that sit between interactions: what ECHO says when
// a step finishes, the power coming back, the blackout, and the authored
// scares (a sound from the room you just left, a light that flickers up
// ahead, a whisper behind you). Also nudges the player if they are stuck.
//
// Scare rules: nothing here moves the camera, hides geometry or takes
// control away, and every loud moment has a quiet lead-in.
public class StoryDirector : MonoBehaviour
{
    public Transform quartersDoor;
    public Vector3 quartersDoorOpenOffset = new Vector3(0f, 0f, -1.45f);
    public Renderer quartersDoorLamp;
    public Renderer monitorRenderer;
    public Renderer[] reactorGlow;
    public AudioSource reactorHum;
    public ArtifactInteractable artifact;
    // Where positional sounds come from.
    public Transform dockingBayPoint;
    public Transform corridorPoint;
    public Transform controlRoomPoint;
    public Transform labDoorPoint;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly Color ReactorTeal = new Color(0.2f, 1f, 0.85f);
    MaterialPropertyBlock mpb;
    float lastHintTime;
    // >0 while one of the beats below is mid-sequence; ECHO does not nag then.
    int busy;

    static readonly Dictionary<string, string> StuckHints = new Dictionary<string, string>
    {
        { "airlock", "The round door ahead of you. Grab the wheel and move your hand in a circle." },
        { "corridordoor", "Far end of the corridor. Same as the airlock: grab the wheel, turn it." },
        { "enghatch", "Engineering is the hatch on the right-hand wall of the control room. Grab the handle and drag it sideways." },
        { "powercell", "Grab the glowing cell from the rack by the Engineering door and carry it to the socket on the reactor." },
        { "breaker", "The breaker is the lever on the far wall of Engineering. Grab it and pull down." },
        { "terminal", "The terminal is on the desk in the control room. Point at it and pull the trigger." },
        { "radio", "Crew quarters is the side door halfway down the corridor. Point at the big dial on the far wall and hold the trigger until it unlocks the lab." },
        { "labdoor", "The lab door is at the far end of the control room. Grab the wheel and turn it." },
        { "recorder", "There's a recorder on the lab desk. Point at it and pull the trigger." },
        { "artifact", "Your call, {PlayerName}. Touch it, or pull the lever by the lab door." },
    };

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        var seq = InteractionSequencer.Instance;
        if (seq != null) seq.StepCompleted += OnStepCompleted;
        SetEmission(quartersDoorLamp, new Color(1f, 0.15f, 0.1f) * 1.6f);
        SetReactor(0f);
    }

    void OnDestroy()
    {
        var seq = InteractionSequencer.Instance;
        if (seq != null) seq.StepCompleted -= OnStepCompleted;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        var seq = InteractionSequencer.Instance;
        if (gm == null || seq == null || !gm.StoryStarted || StoryState.EndingStarted) return;
        if (busy > 0 || (DialogueUI.Instance != null && DialogueUI.Instance.IsBusy)) { seq.ResetStepTimer(); return; }
        string step = seq.CurrentStepId;
        if (step == null) return;
        if (seq.TimeOnStep > 50f && Time.time - lastHintTime > 45f && StuckHints.TryGetValue(step, out var hintLine))
        {
            lastHintTime = Time.time;
            AmbientCaption.Say("ECHO", hintLine, 7f);
        }
    }

    Vector3 PlayerPos => Camera.main != null ? Camera.main.transform.position : Vector3.zero;

    void OnStepCompleted(string step)
    {
        lastHintTime = Time.time;
        switch (step)
        {
            case "airlock": StartCoroutine(AirlockBeat()); break;
            case "corridordoor": StartCoroutine(ControlRoomBeat()); break;
            case "labdoor": StartCoroutine(LabBeat()); break;
        }
    }

    IEnumerator AirlockBeat()
    {
        // Once the player is well into the corridor: something heavy shifts
        // back in the docking bay they just left.
        yield return new WaitUntil(() => PlayerPos.z > 12.5f);
        yield return new WaitForSeconds(1.5f);
        if (dockingBayPoint != null) V2Audio.PlayAt("MetalClang", dockingBayPoint.position, 0.8f, 40f);
        yield return new WaitForSeconds(2.2f);
        AmbientCaption.Say("ECHO", "Thermal contraction. The hull does that.", 4f);
    }

    IEnumerator ControlRoomBeat()
    {
        busy++;
        yield return new WaitForSeconds(1.2f);
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Control room, and the terminal's dead. Nothing in here has power. Engineering is the hatch on your right. Get the main bus up and we can see what they left.");
        busy--;
    }

    // ---- Called from the interactions' onCompleted events (wired in the scene) ----

    public void OnHatchOpened()
    {
        StartCoroutine(HatchRoutine());
    }

    IEnumerator HatchRoutine()
    {
        busy++;
        yield return new WaitForSeconds(0.8f);
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Reactor's cold. Somebody pulled the cell and never put it back. There's a rack by the door. Carry a fresh one over to the socket.");
        InteractionSequencer.Instance?.CompleteStep("enghatch");
        busy--;
    }

    public void OnCellSeated()
    {
        StartCoroutine(CellRoutine());
    }

    IEnumerator CellRoutine()
    {
        busy++;
        float t = 0f;
        if (reactorHum != null) { reactorHum.loop = true; reactorHum.volume = 0f; reactorHum.Play(); }
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            SetReactor(t / 1.5f * 0.35f);
            if (reactorHum != null) reactorHum.volume = t / 1.5f * 0.25f;
            yield return null;
        }
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Cell's seated. Now the main bus breaker, on the far wall. Pull it down.");
        InteractionSequencer.Instance?.CompleteStep("powercell");
        busy--;
    }

    public void OnBreakerPulled()
    {
        StartCoroutine(BreakerRoutine());
    }

    IEnumerator BreakerRoutine()
    {
        busy++;
        V2Audio.PlayAt("PowerSurge", transform.position, 1f, 60f).spatialBlend = 0.2f;
        yield return new WaitForSeconds(2.4f);

        // The station picks up exactly where it stopped.
        StoryState.PowerRestored = true;
        if (LightingDirector.Instance != null) LightingDirector.Instance.mode = LightingDirector.Mode.Powered;
        SetReactor(1f);
        if (reactorHum != null) reactorHum.volume = 0.5f;
        SetEmission(monitorRenderer, new Color(0.35f, 0.8f, 1f) * 1.4f);
        yield return new WaitForSeconds(1.6f);

        if (controlRoomPoint != null) V2Audio.PlayAt("PA_Day181", controlRoomPoint.position, 1f, 45f);
        AmbientCaption.Say("STATION PA", "\"Power restored. Resuming scheduled operation. Mission day one eight one. Full-power test: standing by.\"", 8f);
        yield return new WaitForSeconds(5f);

        // Somewhere behind the player a door that had no power finally opens.
        StartCoroutine(OpenQuartersDoor());
        yield return new WaitForSeconds(3.5f);

        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Ignore that. The station thinks it is still the day it shut down. The terminal has power now. Back to the control room, follow the marker.");
        InteractionSequencer.Instance?.CompleteStep("breaker");
        busy--;
    }

    IEnumerator OpenQuartersDoor()
    {
        if (quartersDoor == null) yield break;
        V2Audio.PlayAt("HatchSlide", quartersDoor.position, 1f, 40f);
        SetEmission(quartersDoorLamp, new Color(0.2f, 1f, 0.3f) * 1.6f);
        Vector3 from = quartersDoor.localPosition;
        Vector3 to = from + quartersDoorOpenOffset;
        float t = 0f;
        while (t < 1.4f)
        {
            t += Time.deltaTime;
            quartersDoor.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 1.4f));
            yield return null;
        }
        quartersDoor.localPosition = to;
    }

    public void OnRadioTuned()
    {
        StartCoroutine(RadioRoutine());
    }

    IEnumerator RadioRoutine()
    {
        busy++;
        if (labDoorPoint != null) V2Audio.PlayAt("LockRelease", labDoorPoint.position, 1f, 45f);
        yield return new WaitForSeconds(1.6f);
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Lab door is unlocked. Those voices were old recordings bleeding through. Nobody is transmitting, {PlayerName}.");

        // Quiet lead-in, then the lights go.
        yield return new WaitForSeconds(1.2f);
        V2Audio.Play2D("PowerDown", 0.9f);
        if (LightingDirector.Instance != null) LightingDirector.Instance.mode = LightingDirector.Mode.Blackout;
        if (reactorHum != null) reactorHum.volume = 0.12f;
        SetReactor(0.15f);
        SetEmission(monitorRenderer, Color.black);
        yield return new WaitForSeconds(2f);
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Something in the lab just drank the whole power bus. Lights are gone, your suit lamp is on. The lab door is at the far end of the control room. Follow the marker. It's nothing.");
        InteractionSequencer.Instance?.CompleteStep("radio");
        busy--;
        StartCoroutine(BlackoutBeats());
    }

    IEnumerator BlackoutBeats()
    {
        // Walking back up the dark corridor: the control room light ahead
        // stutters on for a moment, then nothing.
        yield return new WaitUntil(() => { var p = PlayerPos; return p.x > -1.2f && p.z > 12.6f; });
        yield return new WaitForSeconds(0.6f);
        if (LightingDirector.Instance != null && LightingDirector.Instance.mode == LightingDirector.Mode.Blackout)
        {
            LightingDirector.Instance.Flash("Light_ControlRoom", 0.14f);
            yield return new WaitForSeconds(0.3f);
            LightingDirector.Instance.Flash("Light_ControlRoom", 0.08f);
        }
        // Crossing the control room: a whisper from where they just were.
        yield return new WaitUntil(() => PlayerPos.z > 18.5f);
        var cam = Camera.main;
        if (cam != null && !StoryState.EndingStarted)
        {
            Vector3 behind = cam.transform.position - Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized * 2.5f;
            V2Audio.PlayAt("WhisperBehind", behind, 0.75f, 12f);
        }
    }

    IEnumerator LabBeat()
    {
        if (LightingDirector.Instance != null) LightingDirector.Instance.mode = LightingDirector.Mode.LabOnly;
        yield return new WaitUntil(() => PlayerPos.z > 25.5f);
        yield return new WaitForSeconds(5f);
        // The door they just came through, behind them.
        if (labDoorPoint != null && !StoryState.EndingStarted)
            V2Audio.PlayAt("DoorCreak", labDoorPoint.position, 0.7f, 20f);
    }

    void SetReactor(float level)
    {
        if (reactorGlow == null) return;
        foreach (var r in reactorGlow) SetEmission(r, ReactorTeal * (level * 2.2f));
    }

    void SetEmission(Renderer r, Color c)
    {
        if (r == null) return;
        mpb.SetColor(EmissionColor, c);
        r.SetPropertyBlock(mpb);
    }
}
