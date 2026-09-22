using System.Collections;
using UnityEngine;

// Rotates a door leaf open on a hinge (vault-door style) instead of sliding.
// Put this on the hinge pivot; the door's visual geometry should be a child
// of this transform, offset away from the pivot so it swings on its edge.
public class DoorSwing : MonoBehaviour
{
    public float openAngle = 100f;
    public float openDuration = 1.6f;

    Quaternion closedRotation;
    bool opened;

    void Awake()
    {
        closedRotation = transform.localRotation;
    }

    public IEnumerator SwingOpen()
    {
        if (opened) yield break;
        opened = true;
        yield return SwingTo(closedRotation * Quaternion.Euler(0f, openAngle, 0f));
    }

    // Seals a door that's already open, e.g. so nothing is visible through
    // the doorway during a scripted beat - and reopens it after.
    public IEnumerator SwingClosed()
    {
        if (!opened) yield break;
        opened = false;
        yield return SwingTo(closedRotation);
    }

    IEnumerator SwingTo(Quaternion target)
    {
        Quaternion start = transform.localRotation;
        float t = 0f;
        while (t < openDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / openDuration);
            u = u * u * (3f - 2f * u); // smoothstep
            transform.localRotation = Quaternion.Slerp(start, target, u);
            yield return null;
        }
        transform.localRotation = target;
    }
}
