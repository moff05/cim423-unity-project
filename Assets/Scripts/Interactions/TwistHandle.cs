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
        if (visualToRotate != null) closedRotation = visualToRotate.localRotation;
    }

    void OnEnable()
    {
        if (interactable == null) return;
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        if (interactable == null) return;
        interactable.selectEntered.RemoveListener(OnGrab);
        interactable.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        if (opened) return;
        lastHandAngle = GetHandAngle(args.interactorObject.transform.position);
        tracking = true;
        currentInteractor = args.interactorObject;
        HapticUtility.Pulse(currentInteractor, 0.3f, 0.05f);
    }

    void OnRelease(SelectExitEventArgs args)
    {
        tracking = false;
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

        if (accumulatedAngle - lastNotch >= notchSize)
        {
            lastNotch = accumulatedAngle;
            HapticUtility.Pulse(currentInteractor, 0.2f, 0.04f);
        }

        if (accumulatedAngle >= maxAngle * openThreshold)
        {
            opened = true;
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
