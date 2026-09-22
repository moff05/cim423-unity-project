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

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.SetPositionAndRotation(
            cam.transform.position + cam.transform.forward * distance,
            cam.transform.rotation);
    }
}
