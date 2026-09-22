using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to AirlockDoorPanel (it carries the collider). Only glows/activates
// when it's the current step in InteractionSequencer. Activating swings the
// vault door leaf open (DoorSwing does the actual rotation) - the player
// walks through under their own control, no teleport.
public class AirlockInteractable : MonoBehaviour, IDesktopInteractable
{
    public const string StepId = "airlock";

    public Renderer panelRenderer;
    public DoorSwing door;
    public AudioSource doorAudio;
    public float idlePulseSpeed = 1.6f;
    public float idlePulseMin = 0.15f;
    public float idlePulseMax = 0.45f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    MaterialPropertyBlock mpb;
    bool opened;
    bool busy;
    bool hovering;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null)
        {
            interactable.hoverEntered.AddListener(_ => OnHoverEnter());
            interactable.hoverExited.AddListener(_ => OnHoverExit());
            // No selectEntered->OnActivate wiring here on purpose: in VR the
            // door panel itself no longer opens on a simple point+select,
            // only the wheel handle's TwistHandle can trigger it. Desktop
            // testing is unaffected - it opens via DesktopInteractionProxy
            // calling OnActivate() directly, a separate path entirely.
        }
    }

    void Update()
    {
        if (panelRenderer == null || opened || hovering) return;
        if (!IsCurrentlyInteractable()) { ApplyEmission(Color.black); return; }
        ApplyEmission(InteractionSequencer.PulseColor(idlePulseSpeed, idlePulseMin, idlePulseMax));
    }

    public bool IsCurrentlyInteractable()
    {
        if (opened || busy) return false;
        return InteractionSequencer.Instance == null || InteractionSequencer.Instance.IsCurrentStep(StepId);
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
        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        busy = true;

        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Docking clamps engaged. Life support's still running in there, {PlayerName}. Lights, gravity, air, all of it. Somebody left the lights on for six months.");
        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("{PlayerName}", "Great. More parts for us, then.");

        if (doorAudio != null && doorAudio.clip != null) doorAudio.Play();
        if (door != null) yield return door.SwingOpen();

        ApplyEmission(Color.black);
        opened = true;
        busy = false;
        InteractionSequencer.Instance?.CompleteStep(StepId);
    }

    void ApplyEmission(Color c)
    {
        mpb.SetColor(EmissionColor, c);
        panelRenderer.SetPropertyBlock(mpb);
    }
}
