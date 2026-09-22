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
        "JOURNAL - Day 180: \"Resonance from the artifact is climbing again. Director wants to run a full-power test tomorrow. I've logged my objection.\"";

    public float idlePulseSpeed = 1.6f;
    public float idlePulseMin = 0.15f;
    public float idlePulseMax = 0.4f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;
    bool hovering;
    bool completed;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (infoPanel != null)
        {
            infoPanel.alpha = 0f;
            infoPanel.blocksRaycasts = false;
            infoPanel.interactable = false;
        }
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
    }

    public void OnHoverExit()
    {
        hovering = false;
    }

    public void OnActivate()
    {
        if (!IsCurrentlyInteractable()) return;
        completed = true;
        ApplyEmission(Color.black);
        StartCoroutine(ActivateRoutine());
    }

    IEnumerator ActivateRoutine()
    {
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Terminal's still got power. Might be worth a look before you start pulling wiring.");
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Crew roster says five people, {PlayerName}. The station's activity log, every entry, every day, automatic, stops dead on the same day, for all five. Not staggered. Not partial. Same day. That's not what a normal evacuation looks like.");

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

        InteractionSequencer.Instance?.CompleteStep(StepId);
    }

    void ApplyEmission(Color c)
    {
        if (highlightRenderer == null) return;
        mpb.SetColor(EmissionColor, c);
        highlightRenderer.SetPropertyBlock(mpb);
    }
}
