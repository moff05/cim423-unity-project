using System.Collections;
using TMPro;
using UnityEngine;

// A scanning dial: grab it and move your hand in a circle (same gesture as
// the door wheels) to sweep the band. The sweep only ever runs forward and
// wraps around, so it cannot be left in a dead position. Partway round it
// passes a band with voices on it; further on is the lab door's band, where
// holding still locks it in.
public class TuneDial : HoldInteractable
{
    public Transform knob;
    public Vector3 localSpinAxis = Vector3.right;
    public TextMeshPro readout;
    public AudioSource staticSource;
    public AudioSource voiceSource;
    // Degrees of hand travel for one full sweep of the band.
    public float sweepAngle = 420f;
    [Range(0f, 1f)] public float voiceBand = 0.42f;
    [Range(0f, 1f)] public float targetBand = 0.8f;
    public float bandWidth = 0.07f;
    public float lockSeconds = 1.1f;

    float value = 0.06f;
    float lastHandAngle;
    float lockTimer;
    float knobAngle;
    Quaternion knobBase;
    Vector3 referenceDir;
    bool referenceCached;
    bool voicesCaptioned;
    float lastTick;

    protected override void Awake()
    {
        base.Awake();
        if (knob != null) knobBase = knob.localRotation;
        UpdateReadout(false);
    }

    float Closeness(float band) => Mathf.Clamp01(1f - Mathf.Abs(value - band) / bandWidth);

    protected override void OnHoldStart(Vector3 handPos)
    {
        lastHandAngle = HandAngle(handPos);
        if (staticSource != null && !staticSource.isPlaying) { staticSource.loop = true; staticSource.Play(); }
        if (voiceSource != null && !voiceSource.isPlaying) { voiceSource.loop = true; voiceSource.volume = 0f; voiceSource.Play(); }
    }

    protected override void OnHold(Vector3 handPos)
    {
        float angle = HandAngle(handPos);
        float delta = Mathf.Abs(Mathf.DeltaAngle(lastHandAngle, angle));
        lastHandAngle = angle;
        // Ignore tracking jitter so a still hand really reads as still.
        if (delta < 0.35f) delta = 0f;
        Step(delta, Time.deltaTime);
    }

    // Advance the sweep by `degrees` of hand travel.
    void Step(float degrees, float dt)
    {
        float voice = Closeness(voiceBand);
        float target = Closeness(targetBand);
        // The sweep drags through the two interesting bands so they are hard
        // to skate past.
        float rate = Mathf.Lerp(1f, 0.3f, Mathf.Max(voice, target));
        value = Mathf.Repeat(value + degrees / sweepAngle * rate, 1f);
        knobAngle += degrees;
        if (knob != null) knob.localRotation = knobBase * Quaternion.AngleAxis(knobAngle, Vector3.up);
        if (knobAngle - lastTick >= 25f) { lastTick = knobAngle; Tick(0.15f, 0.03f); }

        voice = Closeness(voiceBand);
        target = Closeness(targetBand);
        if (staticSource != null) staticSource.volume = 0.3f * (1f - Mathf.Max(voice, target * 0.8f));
        if (voiceSource != null) voiceSource.volume = voice * 0.9f;

        if (voice > 0.5f && !voicesCaptioned)
        {
            voicesCaptioned = true;
            StoryState.HeardVoices = true;
            AmbientCaption.Say("CREW BAND", "\"...we're still here... is someone there?... don't run the test...\"", 6f);
        }

        bool inTarget = target > 0.35f;
        if (inTarget && degrees <= 0f) lockTimer += dt; else if (!inTarget) lockTimer = 0f;
        UpdateReadout(inTarget);
        hint.Show(inTarget ? "DOOR BAND: HOLD STILL" : voice > 0.3f ? "...VOICES?" : holdHint);

        if (lockTimer >= lockSeconds)
        {
            if (staticSource != null) staticSource.Stop();
            if (voiceSource != null) voiceSource.Stop();
            if (readout != null) readout.text = "<color=#7CFF8A>LOCKED\nLAB DOOR RELEASED</color>";
            Complete();
        }
    }

    protected override void OnHoldEnd()
    {
        lockTimer = 0f;
        if (staticSource != null) staticSource.volume *= 0.5f;
    }

    void UpdateReadout(bool inTarget)
    {
        if (readout == null) return;
        float khz = 140f + value * 60f;
        string status = inTarget ? "<color=#FFD24A>DOOR BAND</color>" : Closeness(voiceBand) > 0.3f ? "<color=#FF7A6A>UNLISTED SIGNAL</color>" : "SCANNING";
        readout.text = khz.ToString("0.0") + " kHz\n" + status;
    }

    float HandAngle(Vector3 worldHandPos)
    {
        Vector3 axis = transform.TransformDirection(localSpinAxis).normalized;
        if (!referenceCached)
        {
            referenceDir = Vector3.ProjectOnPlane(Vector3.up, axis);
            if (referenceDir.sqrMagnitude < 0.0001f) referenceDir = Vector3.ProjectOnPlane(Vector3.right, axis);
            referenceCached = true;
        }
        Vector3 toHand = Vector3.ProjectOnPlane(worldHandPos - transform.position, axis);
        return Vector3.SignedAngle(referenceDir, toHand, axis);
    }

    protected override IEnumerator DesktopRoutine()
    {
        auto = true;
        OnHoldStart(transform.position + Vector3.up);
        // Sweep up to the door band, lingering wherever the dial drags.
        while (Closeness(targetBand) < 0.9f)
        {
            Step(140f * Time.deltaTime, Time.deltaTime);
            yield return null;
        }
        while (!done)
        {
            Step(0f, Time.deltaTime);
            yield return null;
        }
    }
}
