using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to Console. Selecting it toggles a UI info panel showing the crew's journal entry.
public class TerminalInteractable : MonoBehaviour, IDesktopInteractable
{
    public const string StepId = "terminal";

    public Renderer highlightRenderer;
    public CanvasGroup infoPanel;
    public TMP_Text infoBodyText;
    [TextArea]
    public string journalEntry =
        "DR. OKAFOR, JOURNAL, DAY 180\n\"Resonance from the artifact is climbing again. The Director wants a full-power test tomorrow, with all five of us in the room. I've logged my objection.\"\n\nLAB DOOR: REMOTE LOCK HELD BY THE CREW-BAND RELAY (CREW QUARTERS). TUNE THE RELAY TO THE DOOR BAND TO RELEASE.";

    public float idlePulseSpeed = 1.6f;
    public float idlePulseMin = 0.15f;
    public float idlePulseMax = 0.4f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;
    bool hovering;
    bool completed;
    FloatingHintText hint;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (infoPanel != null)
        {
            infoPanel.alpha = 0f;
            infoPanel.blocksRaycasts = false;
            infoPanel.interactable = false;
        }
        // Default height (0.45) sat just under the console's own top
        // (measured at 0.4875 above its pivot), so the label rendered
        // partially behind/inside the monitor housing - confirmed via
        // direct feedback ("the pull trigger is kind of blocked by the
        // item"). 0.65 clears it with real margin.
        hint = FloatingHintText.Attach(transform, height: 0.65f);
    }

    void Update()
    {
        if (highlightRenderer == null || hovering || completed) return;
        if (!IsCurrentlyInteractable()) { ApplyEmission(Color.black); return; }
        ApplyEmission(InteractionSequencer.PulseColor(idlePulseSpeed, idlePulseMin, idlePulseMax));
    }

    public bool IsCurrentlyInteractable()
    {
        if (completed) return false;
        return InteractionSequencer.Instance == null || InteractionSequencer.Instance.IsCurrentStep(StepId);
    }

    void Start()
    {
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null)
        {
            interactable.hoverEntered.AddListener(_ => OnHoverEnter());
            interactable.hoverExited.AddListener(_ => OnHoverExit());
            interactable.selectEntered.AddListener(args => { HapticUtility.Pulse(args.interactorObject); OnActivate(); });
        }
    }

    public void OnHoverEnter()
    {
        hovering = true;
        if (!IsCurrentlyInteractable()) return;
        ApplyEmission(Color.white * 2.5f);
        hint.Show("PULL TRIGGER");
    }

    public void OnHoverExit()
    {
        hovering = false;
        hint.Hide();
    }

    public void OnActivate()
    {
        if (!IsCurrentlyInteractable()) return;
        completed = true;
        ApplyEmission(Color.black);
        hint.Hide();
        StartCoroutine(ActivateRoutine());
    }

    IEnumerator ActivateRoutine()
    {
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "There. Terminal's live. Let's see who we're salvaging from.");
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Crew roster says five, {PlayerName}. Halvorsen, Okafor, Reyes, Lindqvist, Tanaka. The automatic activity log stops on the same day for all five. Not staggered. Same minute.");

        if (infoBodyText != null) infoBodyText.text = journalEntry;
        if (infoPanel != null)
        {
            infoPanel.alpha = 1f;
            infoPanel.blocksRaycasts = true;
            infoPanel.interactable = true;
        }

        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("{PlayerName}", "Well, that's not ominous at all.");

        // Same click that dismisses the last line also closes the panel -
        // no separate click needed just to make it go away.
        if (infoPanel != null)
        {
            infoPanel.alpha = 0f;
            infoPanel.blocksRaycasts = false;
            infoPanel.interactable = false;
        }

        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Standard evacuation, most likely. The lab is locked off a relay in crew quarters, back off the corridor. That door has power now. Go tune it.");

        InteractionSequencer.Instance?.CompleteStep(StepId);
    }

    void ApplyEmission(Color c)
    {
        if (highlightRenderer == null) return;
        mpb.SetColor(EmissionColor, c);
        highlightRenderer.SetPropertyBlock(mpb);
    }
}
