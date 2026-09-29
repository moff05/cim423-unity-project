using UnityEngine;

// Keeps a world-space Canvas positioned like a HUD in front of whichever
// camera is currently tagged MainCamera. Screen Space - Overlay canvases
// don't render correctly under stereo XR rendering, so every UI canvas that
// needs to be visible in VR (dialogue, fade, start/end screens, info panel)
// has to be World Space instead - this is what keeps that world-space
// canvas glued to the player's view without needing to be re-parented under
// either rig (which would tie its visibility to that rig's active state).
public class WorldSpaceUIFollow : MonoBehaviour
{
    public float distance = 1.2f;

    Canvas canvas;
    void Awake() { canvas = GetComponent<Canvas>(); }

    // Fade has to keep tracking every frame - it needs to cover whatever
    // the active camera is looking at even mid-cinematic, while that camera
    // is flying through space on a completely different transform. A menu
    // or dialogue panel re-centering on your view every single frame with
    // zero damping instead reads as the UI chasing your head around -
    // those should snap into place once when they appear and then hold
    // still until they're hidden and re-shown.
    public bool continuousFollow = true;

    // How far (degrees from view center) the panel can drift before it
    // re-centers, for the non-continuous canvases. A one-shot snap on
    // enable sounds simpler, but the XR camera's tracked pose isn't
    // necessarily settled on the frame a panel first appears (the
    // simulator/headset can take a frame or two to report real head
    // height rather than a Y=0 rest pose) - a snap that fires on that
    // frame gets stuck wrong forever with no follow-up correction. A
    // deadzone re-checked every frame self-heals from that automatically
    // while still not chasing small head movements.
    public float repositionAngle = 30f;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Keeps ray/click hit-testing pointed at whichever camera is
        // actually active - was previously a fixed Inspector reference to
        // the XR camera specifically, which meant a GraphicRaycaster (or
        // the XR TrackedDeviceGraphicRaycaster) would raycast through a
        // stale or inactive camera the instant the active rig changed.
        if (canvas != null && canvas.worldCamera != cam)
            canvas.worldCamera = cam;

        if (continuousFollow)
        {
            Snap(cam);
            return;
        }

        Vector3 toPanel = transform.position - cam.transform.position;
        if (toPanel.sqrMagnitude < 0.0001f || Vector3.Angle(cam.transform.forward, toPanel) > repositionAngle)
            Snap(cam);
    }

    void Snap(Camera cam)
    {
        transform.SetPositionAndRotation(
            cam.transform.position + cam.transform.forward * distance,
            cam.transform.rotation);
    }
}
