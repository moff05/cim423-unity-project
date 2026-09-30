using System.Collections.Generic;
using UnityEngine;

// Enforces a strict interaction order across the whole playthrough: only the
// current step's object is allowed to glow (gold) or activate. Everything
// else stays dark until it's their turn.
public class InteractionSequencer : MonoBehaviour
{
    public static InteractionSequencer Instance { get; private set; }

    public static readonly Color HighlightColor = new Color(1f, 0.82f, 0.25f);

    // Shared idle "you can interact with this" glow: shimmers from a dim
    // gold up through a bright near-white flash and back, so the current
    // target reads clearly as a mask/highlight rather than a flat tint.
    //
    // Boosted per on-device feedback ("no indicators on what to interact
    // with") - every caller's min/max (roughly 0.15-0.2 to 0.4-0.45) stayed
    // well under the Bloom threshold (1.0 on the mobile URP asset), so the
    // "glow" never actually bloomed on the Quest and read as a flat, easy to
    // miss tint. This multiplier pushes the peak of every pulse cycle past
    // that threshold so the current target periodically flashes with real
    // bloom, not just a color shift, while the dim end stays dim.
    const float BloomBoost = 3.5f;

    public static Color PulseColor(float speed, float min, float max)
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * speed);
        float t = min + (max - min) * wave;
        Color c = Color.Lerp(HighlightColor, Color.white, wave * wave);
        return c * t * BloomBoost;
    }

    public List<string> stepOrder = new List<string>
    {
        "airlock", "corridordoor", "terminal", "labdoor", "recorder", "artifact"
    };

    // Player-facing objective text per step, for ObjectiveHUD - per direct
    // feedback ("the next item on the list should have a yellow glow or
    // something... very obvious what the next thing to do is"). The current
    // step's object already pulses gold, but that only helps once you're
    // already looking at the right thing; this tells you in words,
    // independent of where you're looking.
    static readonly Dictionary<string, string> StepDescriptions = new Dictionary<string, string>
    {
        { "airlock", "Open the airlock" },
        { "corridordoor", "Open the corridor door" },
        { "terminal", "Check the terminal" },
        { "labdoor", "Open the lab door" },
        { "recorder", "Play the recorder" },
        { "artifact", "Examine the artifact" },
    };

    int currentIndex;

    void Awake()
    {
        Instance = this;
    }

    // 0 at the start, 1 once every step is complete - drives things like
    // ambience volume that should build as the player goes deeper in.
    public float Progress => stepOrder.Count > 0 ? Mathf.Clamp01((float)currentIndex / stepOrder.Count) : 0f;

    public string CurrentStepId => currentIndex < stepOrder.Count ? stepOrder[currentIndex] : null;

    public string CurrentStepDescription
    {
        get
        {
            var id = CurrentStepId;
            if (id == null) return null;
            return StepDescriptions.TryGetValue(id, out var desc) ? desc : id;
        }
    }

    public bool IsCurrentStep(string stepId)
    {
        return currentIndex < stepOrder.Count && stepOrder[currentIndex] == stepId;
    }

    public void CompleteStep(string stepId)
    {
        if (IsCurrentStep(stepId)) currentIndex++;
    }
}
