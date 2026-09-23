using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// VR-only "grab and twist" valve wheel. Grab it and move the controller
// around the wheel's own axis to spin it open; release and grab again to
// keep going. Fires onOpened once the accumulated twist crosses the
// threshold - wired to the same OnActivate() the desktop click path uses,
// so this is purely an extra requirement layered on top for VR, not a
// replacement for how desktop testing already works.
public class TwistHandle : MonoBehaviour
{
    // The mesh that actually spins for visual feedback - usually a sibling
    // of this grab point rather than this object itself, since the visual
    // wheel geometry was built unscaled to dodge the door disc's crushed
    // parent scale.
    public Transform visualToRotate;
    public Vector3 localSpinAxis = Vector3.up;
    public float maxAngle = 270f;
    [Range(0f, 1f)] public float openThreshold = 0.9f;
    public UnityEvent onOpened = new UnityEvent();

    // The wheel had zero hover feedback - nothing told you it was
    // interactable until you already knew to grab it, unlike every other
    // interactable in the scene (doors, terminal, recorder, artifact all
    // glow on hover). visualToRotate is a pivot with no renderer of its
    // own; the actual mesh is split across its child hub/spokes.
    public Color hoverGlowColor = new Color(1f, 1f, 1f, 1f) * 2f;
    // Distinct from hover - grabbing produced zero visible change (only a
    // haptic pulse, which does nothing on keyboard/mouse), so there was no
    // way to tell "I'm hovering" from "I'm actually gripping this and my
    // movement is being tracked" apart from the door already starting to
    // creak open.
    public Color grabbedGlowColor = new Color(0.3f, 1f, 0.4f, 1f) * 2f;
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;
    Renderer[] wheelRenderers;
    bool hovering;

    // The glow says "this is interactable," not "here is what to do with
    // it" - a grab-and-sweep-in-a-circle gesture isn't self-explanatory
    // the way a button press is. Floating world-space text, built at
    // runtime so no prefab/scene wiring is needed, billboarded toward
    // whichever camera is active so it's readable regardless of approach
    // angle.
    public float hintHeight = 0.45f;
    public float hintScale = 0.0025f;
    TextMeshPro hintText;

    XRSimpleInteractable interactable;
    Quaternion closedRotation;
    float accumulatedAngle;
    float lastHandAngle;
    bool tracking;
    bool opened;

    // Small buzz every time the wheel clicks past another notch, so turning
    // it reads as a mechanical ratchet rather than a smooth, weightless drag.
    public float notchSize = 30f;
    float lastNotch;
    object currentInteractor;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (visualToRotate != null)
        {
            closedRotation = visualToRotate.localRotation;
            wheelRenderers = visualToRotate.GetComponentsInChildren<Renderer>(true);
        }
        mpb = new MaterialPropertyBlock();
        CreateHint();
    }

    void CreateHint()
    {
        // Deliberately NOT parented under this transform: AirlockDoorDisc's
        // local axes are badly skewed (its "forward" points straight down,
        // not through the door) from the crushed parent scale mentioned
        // above, so a local offset landed the hint overlapping the wheel's
        // own hover reticle instead of clear of it. Positioned in world
        // space every frame instead - see LateUpdate.
        var go = new GameObject("InteractionHint");
        go.transform.localScale = Vector3.one * hintScale;
        hintText = go.AddComponent<TextMeshPro>();
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = new Color(0.6f, 0.95f, 1f);
        hintText.fontSize = 36f;
        hintText.enableWordWrapping = false;
        hintText.outlineWidth = 0.2f;
        hintText.outlineColor = Color.black;
        go.SetActive(false);
    }

    void ShowHint(string text)
    {
        if (hintText == null) return;
        hintText.text = text;
        hintText.gameObject.SetActive(true);
    }

    void HideHint()
    {
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (hintText == null || !hintText.gameObject.activeSelf) return;
        var cam = Camera.main;
        if (cam == null) return;
        hintText.transform.position = transform.position + Vector3.up * hintHeight;
        hintText.transform.rotation = Quaternion.LookRotation(hintText.transform.position - cam.transform.position, Vector3.up);
    }

    void OnDestroy()
    {
        if (hintText != null) Destroy(hintText.gameObject);
    }

    void OnEnable()
    {
        if (interactable == null) return;
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
        interactable.hoverEntered.AddListener(OnWheelHoverEnter);
        interactable.hoverExited.AddListener(OnWheelHoverExit);
    }

    void OnDisable()
    {
        if (interactable == null) return;
        interactable.selectEntered.RemoveListener(OnGrab);
        interactable.selectExited.RemoveListener(OnRelease);
        interactable.hoverEntered.RemoveListener(OnWheelHoverEnter);
        interactable.hoverExited.RemoveListener(OnWheelHoverExit);
    }

    void OnWheelHoverEnter(HoverEnterEventArgs args)
    {
        hovering = true;
        if (tracking || opened) return;
        ApplyGlow(hoverGlowColor);
        ShowHint("GRAB, THEN MOVE IN A CIRCLE");
    }

    void OnWheelHoverExit(HoverExitEventArgs args)
    {
        hovering = false;
        if (tracking) return;
        ApplyGlow(Color.black);
        HideHint();
    }

    void ApplyGlow(Color color)
    {
        if (opened || wheelRenderers == null) return;
        mpb.SetColor(EmissionColor, color);
        foreach (var r in wheelRenderers) r.SetPropertyBlock(mpb);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        if (opened) return;
        lastHandAngle = GetHandAngle(args.interactorObject.transform.position);
        tracking = true;
        currentInteractor = args.interactorObject;
        ApplyGlow(grabbedGlowColor);
        ShowHint("KEEP MOVING IN A CIRCLE");
        HapticUtility.Pulse(currentInteractor, 0.3f, 0.05f);
    }

    void OnRelease(SelectExitEventArgs args)
    {
        tracking = false;
        if (opened) return;
        ApplyGlow(hovering ? hoverGlowColor : Color.black);
        if (hovering) ShowHint("GRAB, THEN MOVE IN A CIRCLE");
        else HideHint();
    }

    void Update()
    {
        if (opened || !tracking || interactable == null) return;
        var interactor = interactable.firstInteractorSelecting;
        if (interactor == null) return;

        float handAngle = GetHandAngle(interactor.transform.position);
        float delta = Mathf.DeltaAngle(lastHandAngle, handAngle);
        lastHandAngle = handAngle;

        accumulatedAngle = Mathf.Clamp(accumulatedAngle + delta, 0f, maxAngle);
        if (visualToRotate != null)
            visualToRotate.localRotation = closedRotation * Quaternion.AngleAxis(accumulatedAngle, localSpinAxis);

        // Live percentage so a slow/fiddly sweep still visibly counts for
        // something each time it ticks up - without this, a partial or
        // hesitant turn looks and feels completely inert until the moment
        // it crosses the threshold.
        int percent = Mathf.RoundToInt(Mathf.Clamp01(accumulatedAngle / (maxAngle * openThreshold)) * 100f);
        ShowHint("KEEP MOVING IN A CIRCLE (" + percent + "%)");

        if (accumulatedAngle - lastNotch >= notchSize)
        {
            lastNotch = accumulatedAngle;
            HapticUtility.Pulse(currentInteractor, 0.2f, 0.04f);
        }

        if (accumulatedAngle >= maxAngle * openThreshold)
        {
            opened = true;
            ApplyGlow(Color.black);
            HideHint();
            tracking = false;
            HapticUtility.Pulse(currentInteractor, 0.6f, 0.15f);
            onOpened.Invoke();
        }
    }

    // Angle of the hand around this transform's own spin axis, measured
    // against a fixed reference direction cached once so it doesn't drift.
    Vector3 referenceDir;
    bool referenceCached;

    float GetHandAngle(Vector3 worldHandPos)
    {
        Vector3 axisWorld = transform.TransformDirection(localSpinAxis).normalized;
        if (!referenceCached)
        {
            referenceDir = Vector3.ProjectOnPlane(transform.up, axisWorld);
            if (referenceDir.sqrMagnitude < 0.0001f) referenceDir = Vector3.ProjectOnPlane(transform.right, axisWorld);
            referenceCached = true;
        }
        Vector3 toHand = Vector3.ProjectOnPlane(worldHandPos - transform.position, axisWorld);
        return Vector3.SignedAngle(referenceDir, toHand, axisWorld);
    }
}
