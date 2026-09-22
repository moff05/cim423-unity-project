using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Generic repeatable room-to-room door (the Airlock has its own bespoke
// version with dialogue beats). Gated by InteractionSequencer so it can
// only be opened on its turn.
public class DoorInteractable : MonoBehaviour, IDesktopInteractable
{
    public string stepId;
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
        return InteractionSequencer.Instance == null || InteractionSequencer.Instance.IsCurrentStep(stepId);
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
        if (doorAudio != null && doorAudio.clip != null) doorAudio.Play();
        if (door != null) yield return door.SwingOpen();
        ApplyEmission(Color.black);
        opened = true;
        busy = false;
        InteractionSequencer.Instance?.CompleteStep(stepId);
    }

    void ApplyEmission(Color c)
    {
        mpb.SetColor(EmissionColor, c);
        panelRenderer.SetPropertyBlock(mpb);
    }
}
