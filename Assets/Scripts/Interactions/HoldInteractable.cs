using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Shared base for every "grab it and move your hand" interaction added in v2
// (slide a hatch, pull a lever, tune a dial, carry a power cell). It uses the
// exact mechanism TwistHandle already proved on the headset: an
// XRSimpleInteractable that is selected while the button is held, with the
// interactor's world position read every frame. Subclasses only say what the
// hand movement means.
[RequireComponent(typeof(XRSimpleInteractable))]
public abstract class HoldInteractable : MonoBehaviour, IDesktopInteractable
{
    // Sequencer step this belongs to. Empty = usable at any time.
    public string stepId;
    public Renderer[] glowRenderers;
    public string hoverHint = "GRAB AND PULL";
    public string holdHint = "KEEP PULLING";
    public float hintHeight = 0.35f;
    // Clip name under Resources/V2Audio played when the action completes.
    public string completeClip;
    public UnityEvent onCompleted = new UnityEvent();

    protected bool done;
    protected bool holding;
    protected bool hovering;
    // True while the desktop stand-in is playing the gesture by itself.
    protected bool auto;
    protected Transform hand;
    protected object interactor;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;
    XRSimpleInteractable xr;
    protected FloatingHintText hint;

    protected virtual void Awake()
    {
        xr = GetComponent<XRSimpleInteractable>();
        mpb = new MaterialPropertyBlock();
        hint = FloatingHintText.Attach(transform, hintHeight);
    }

    void OnEnable()
    {
        if (xr == null) return;
        xr.hoverEntered.AddListener(HandleHoverEnter);
        xr.hoverExited.AddListener(HandleHoverExit);
        xr.selectEntered.AddListener(HandleSelectEnter);
        xr.selectExited.AddListener(HandleSelectExit);
    }

    void OnDisable()
    {
        if (xr == null) return;
        xr.hoverEntered.RemoveListener(HandleHoverEnter);
        xr.hoverExited.RemoveListener(HandleHoverExit);
        xr.selectEntered.RemoveListener(HandleSelectEnter);
        xr.selectExited.RemoveListener(HandleSelectExit);
        holding = false;
    }

    void OnDestroy()
    {
        if (hint != null) Destroy(hint.gameObject);
    }

    public virtual bool IsCurrentlyInteractable()
    {
        if (done || StoryState.EndingStarted) return false;
        var gm = GameManager.Instance;
        if (gm != null && !gm.StoryStarted) return false;
        return InteractionSequencer.Instance == null || string.IsNullOrEmpty(stepId)
            || InteractionSequencer.Instance.IsCurrentStep(stepId);
    }

    void HandleHoverEnter(HoverEnterEventArgs args) { OnHoverEnter(); }
    void HandleHoverExit(HoverExitEventArgs args) { OnHoverExit(); }

    public void OnHoverEnter()
    {
        hovering = true;
        if (holding || !IsCurrentlyInteractable()) return;
        SetGlow(Color.white * 2.5f);
        hint.Show(hoverHint);
    }

    public void OnHoverExit()
    {
        hovering = false;
        if (holding) return;
        hint.Hide();
    }

    void HandleSelectEnter(SelectEnterEventArgs args)
    {
        if (!IsCurrentlyInteractable()) return;
        interactor = args.interactorObject;
        hand = args.interactorObject.transform;
        holding = true;
        SetGlow(new Color(0.3f, 1f, 0.4f) * 2f);
        hint.Show(holdHint);
        HapticUtility.Pulse(interactor, 0.3f, 0.05f);
        OnHoldStart(hand.position);
    }

    void HandleSelectExit(SelectExitEventArgs args)
    {
        if (!holding) return;
        holding = false;
        if (done) return;
        OnHoldEnd();
        if (hovering) hint.Show(hoverHint); else hint.Hide();
    }

    protected virtual void Update()
    {
        if (done || auto) return;
        if (holding)
        {
            if (hand == null) { holding = false; OnHoldEnd(); return; }
            OnHold(hand.position);
            return;
        }
        if (hovering && IsCurrentlyInteractable()) return;
        SetGlow(IsCurrentlyInteractable() ? InteractionSequencer.PulseColor(1.6f, 0.15f, 0.45f) : Color.black);
    }

    protected abstract void OnHoldStart(Vector3 handPos);
    protected abstract void OnHold(Vector3 handPos);
    protected virtual void OnHoldEnd() { }
    // Desktop (Editor) stand-in: plays the whole gesture automatically.
    protected abstract IEnumerator DesktopRoutine();

    public void OnActivate()
    {
        if (holding || auto || !IsCurrentlyInteractable()) return;
        StartCoroutine(DesktopRoutine());
    }

    protected void Tick(float amplitude = 0.2f, float duration = 0.04f)
    {
        HapticUtility.Pulse(interactor, amplitude, duration);
    }

    protected void Complete()
    {
        if (done) return;
        done = true;
        holding = false;
        SetGlow(Color.black);
        hint.Hide();
        HapticUtility.Pulse(interactor, 0.7f, 0.18f);
        V2Audio.PlayAt(completeClip, transform.position, 1f);
        onCompleted.Invoke();
    }

    protected void SetGlow(Color c)
    {
        if (glowRenderers == null) return;
        mpb.SetColor(EmissionColor, c);
        foreach (var r in glowRenderers) if (r != null) r.SetPropertyBlock(mpb);
    }
}
