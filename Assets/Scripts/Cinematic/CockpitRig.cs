using UnityEngine;

// The player's own ship: a bridge they stand in for the start screen and the
// whole opening flight. It sits at a fixed place in the world (it is not
// attached to the head), so looking around shows the cabin, the dashboard and
// space through the windshield. The start screen is mounted in it as the
// captain's console instead of floating after the player's gaze.
public class CockpitRig : MonoBehaviour
{
    public Transform xrOrigin;
    public Transform cinematicRig;
    public Canvas startCanvas;

    Vector3 headOffset;
    float yaw;
    bool aligned;
    float timer;
    bool wasFlying;

    void LateUpdate()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.StoryStarted) { gameObject.SetActive(false); return; }
        var cam = Camera.main;
        if (cam == null) return;
        if (startCanvas != null && startCanvas.worldCamera != cam) startCanvas.worldCamera = cam;

        bool flying = cinematicRig != null && cinematicRig.gameObject.activeInHierarchy;
        // The flight rig switching off again means the player has just been
        // put down inside the station: the ship must not come with them.
        if (wasFlying && !flying) { gameObject.SetActive(false); return; }
        wasFlying = flying;
        Transform anchor = flying ? cinematicRig : xrOrigin;
        if (anchor == null) return;

        if (!aligned)
        {
            // Wait for the headset to report a real pose, then build the
            // cabin around wherever the player is actually standing and
            // facing, once.
            timer += Time.deltaTime;
            Vector3 local = anchor.InverseTransformPoint(cam.transform.position);
            if (timer > 0.5f && (local.y > 0.4f || timer > 3f))
            {
                aligned = true;
                headOffset = new Vector3(local.x, 0f, local.z);
                yaw = cam.transform.eulerAngles.y - anchor.eulerAngles.y;
            }
            else
            {
                transform.SetPositionAndRotation(anchor.position, anchor.rotation);
                return;
            }
        }

        // IntroSequence turns the flight rig so the player's forward is world
        // +Z, so during the flight the cabin simply faces +Z.
        Quaternion rot = flying ? Quaternion.identity : anchor.rotation * Quaternion.Euler(0f, yaw, 0f);
        transform.SetPositionAndRotation(anchor.TransformPoint(headOffset), rot);
    }
}
