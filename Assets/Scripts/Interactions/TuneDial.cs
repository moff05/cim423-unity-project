using System.Collections;
using TMPro;
using UnityEngine;

// The lab door's remote lock: point at the dial and hold the trigger. It
// scans by itself, passes a band with voices on it, then finds the door's
// signal and unlocks. The first version made the player circle their hand to
// tune it; on the headset nobody could tell what they were meant to do, the
// text was too close to read while doing it, and it was uncomfortable. Now
// the only instruction is "hold".
public class TuneDial : HoldInteractable
{
    public Transform knob;
    public Vector3 localSpinAxis = Vector3.right;
    public TextMeshPro readout;
    public AudioSource staticSource;
    public AudioSource voiceSource;
    // Seconds of holding, not counting the pause on the voices.
    public float scanSeconds = 6f;
    public float voiceSeconds = 3.5f;
    [Range(0f, 1f)] public float voiceBand = 0.45f;

    float value;
    float voiceTimer;
    Quaternion knobBase;
    bool voicesCaptioned;
    float lastTick;

    protected override void Awake()
    {
        base.Awake();
        if (knob != null) knobBase = knob.localRotation;
        UpdateReadout(false);
    }

    protected override void OnHoldStart(Vector3 handPos)
    {
        if (staticSource != null && !staticSource.isPlaying) { staticSource.loop = true; staticSource.Play(); }
        if (voiceSource != null && !voiceSource.isPlaying) { voiceSource.loop = true; voiceSource.volume = 0f; voiceSource.Play(); }
    }

    protected override void OnHold(Vector3 handPos)
    {
        Step(Time.deltaTime);
    }

    void Step(float dt)
    {
        bool onVoices = value >= voiceBand && voiceTimer < voiceSeconds;
        if (onVoices)
        {
            voiceTimer += dt;
            if (!voicesCaptioned)
            {
                voicesCaptioned = true;
                StoryState.HeardVoices = true;
                AmbientCaption.Say("VOICES ON THE RADIO", "\"...we're still here... is someone there?... don't run the test...\"", 5f);
            }
        }
        else
        {
            value = Mathf.Min(1f, value + dt / scanSeconds);
            if (knob != null) knob.localRotation = knobBase * Quaternion.AngleAxis(value * 540f, Vector3.up);
            if (value - lastTick >= 0.06f) { lastTick = value; Tick(0.15f, 0.03f); }
        }

        if (staticSource != null) staticSource.volume = onVoices ? 0.08f : 0.45f;
        if (voiceSource != null) voiceSource.volume = Mathf.MoveTowards(voiceSource.volume, onVoices ? 1f : 0f, dt * 2f);
        UpdateReadout(onVoices);
        hint.Show(onVoices ? "KEEP HOLDING" : "KEEP HOLDING  " + Mathf.RoundToInt(value * 100f) + "%");

        if (value >= 1f)
        {
            if (staticSource != null) staticSource.Stop();
            if (voiceSource != null) voiceSource.Stop();
            if (readout != null) readout.text = "<color=#7CFF8A>DOOR SIGNAL FOUND\nLAB DOOR UNLOCKED</color>";
            Complete();
        }
    }

    protected override void OnHoldEnd()
    {
        if (staticSource != null) staticSource.volume = 0.1f;
        if (voiceSource != null) voiceSource.volume = 0f;
    }

    void UpdateReadout(bool onVoices)
    {
        if (readout == null) return;
        int filled = Mathf.RoundToInt(value * 12f);
        string bar = new string('|', filled) + "<color=#33555F>" + new string('|', 12 - filled) + "</color>";
        string status = onVoices ? "<color=#FF7A6A>UNKNOWN SIGNAL: VOICES</color>" : value > 0f ? "SCANNING FOR DOOR SIGNAL" : "HOLD THE DIAL TO SCAN";
        readout.text = status + "\n" + bar;
    }

    protected override IEnumerator DesktopRoutine()
    {
        auto = true;
        OnHoldStart(transform.position);
        while (!done)
        {
            Step(Time.deltaTime);
            yield return null;
        }
    }
}
