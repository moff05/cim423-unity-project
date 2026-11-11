using System.Collections;
using UnityEngine;

// Grab the lever and pull your hand down. Springs back if released early.
public class PullLever : HoldInteractable
{
    public Transform pivot;
    public Vector3 localAxis = Vector3.right;
    public float maxAngle = 90f;
    // How far the hand has to travel (metres, straight down) for a full pull.
    public float pullDistance = 0.35f;
    [Range(0.5f, 1f)] public float threshold = 0.85f;

    Quaternion baseRotation;
    float progress;
    float grabProgress;
    Vector3 grabHandPos;
    float lastTick;
    Coroutine spring;

    protected override void Awake()
    {
        base.Awake();
        if (pivot != null) baseRotation = pivot.localRotation;
    }

    protected override void OnHoldStart(Vector3 handPos)
    {
        if (spring != null) { StopCoroutine(spring); spring = null; }
        grabHandPos = handPos;
        grabProgress = progress;
    }

    protected override void OnHold(Vector3 handPos)
    {
        float pulled = Vector3.Dot(handPos - grabHandPos, Vector3.down) / pullDistance;
        Apply(Mathf.Clamp01(grabProgress + pulled));
        if (Mathf.Abs(progress - lastTick) >= 0.2f) { lastTick = progress; Tick(); }
        if (progress >= threshold)
        {
            Apply(1f);
            Complete();
        }
    }

    protected override void OnHoldEnd()
    {
        if (!done) spring = StartCoroutine(SpringBack());
    }

    void Apply(float value)
    {
        progress = value;
        if (pivot != null) pivot.localRotation = baseRotation * Quaternion.AngleAxis(progress * maxAngle, localAxis);
    }

    IEnumerator SpringBack()
    {
        while (progress > 0.001f)
        {
            Apply(Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2.5f));
            yield return null;
        }
        spring = null;
    }

    protected override IEnumerator DesktopRoutine()
    {
        auto = true;
        while (progress < 1f)
        {
            Apply(Mathf.MoveTowards(progress, 1f, Time.deltaTime * 1.6f));
            yield return null;
        }
        Complete();
    }
}
