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

    int currentIndex;

    void Awake()
    {
        Instance = this;
    }

    // 0 at the start, 1 once every step is complete - drives things like
    // ambience volume that should build as the player goes deeper in.
    public float Progress => stepOrder.Count > 0 ? Mathf.Clamp01((float)currentIndex / stepOrder.Count) : 0f;

    public bool IsCurrentStep(string stepId)
    {
        return currentIndex < stepOrder.Count && stepOrder[currentIndex] == stepId;
    }

    public void CompleteStep(string stepId)
    {
        if (IsCurrentStep(stepId)) currentIndex++;
    }
}
