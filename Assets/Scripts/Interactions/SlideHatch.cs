using System.Collections;
using UnityEngine;

// Grab the handle and drag the hatch sideways along its rail.
public class SlideHatch : HoldInteractable
{
    public Transform door;
    // Slide direction in the door's parent space.
    public Vector3 slideLocalDir = Vector3.forward;
    public float distance = 1.25f;
    [Range(0.5f, 1f)] public float openThreshold = 0.8f;

    Vector3 closedLocalPos;
    float offset;
    float grabOffset;
    Vector3 grabHandPos;
    float lastTick;

    protected override void Awake()
    {
        base.Awake();
        if (door != null) closedLocalPos = door.localPosition;
    }

    Vector3 WorldDir => door.parent != null ? door.parent.TransformDirection(slideLocalDir).normalized : slideLocalDir.normalized;

    protected override void OnHoldStart(Vector3 handPos)
    {
        grabHandPos = handPos;
        grabOffset = offset;
    }

    protected override void OnHold(Vector3 handPos)
    {
        Apply(Mathf.Clamp(grabOffset + Vector3.Dot(handPos - grabHandPos, WorldDir), 0f, distance));
        hint.Show(holdHint + " (" + Mathf.RoundToInt(Mathf.Clamp01(offset / (distance * openThreshold)) * 100f) + "%)");
        if (Mathf.Abs(offset - lastTick) >= 0.15f) { lastTick = offset; Tick(); }
        if (offset >= distance * openThreshold)
        {
            Complete();
            StartCoroutine(FinishOpen());
        }
    }

    void Apply(float value)
    {
        offset = value;
        door.localPosition = closedLocalPos + slideLocalDir.normalized * offset;
    }

    IEnumerator FinishOpen()
    {
        while (offset < distance - 0.001f)
        {
            Apply(Mathf.MoveTowards(offset, distance, Time.deltaTime * 1.2f));
            yield return null;
        }
    }

    protected override IEnumerator DesktopRoutine()
    {
        auto = true;
        while (offset < distance * openThreshold)
        {
            Apply(Mathf.MoveTowards(offset, distance, Time.deltaTime * 0.9f));
            yield return null;
        }
        Complete();
        yield return FinishOpen();
    }
}
