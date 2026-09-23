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

    // Fade has to keep tracking every frame - it needs to cover whatever
    // the active camera is looking at even mid-cinematic, while that camera
    // is flying through space on a completely different transform. A menu
    // or dialogue panel re-centering on your view every single frame with
    // zero damping instead reads as the UI chasing your head around -
    // those should snap into place once when they appear and then hold
    // still until they're hidden and re-shown.
    public bool continuousFollow = true;

    void OnEnable()
    {
        Snap();
    }

    void LateUpdate()
    {
        if (continuousFollow) Snap();
    }

    void Snap()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.SetPositionAndRotation(
            cam.transform.position + cam.transform.forward * distance,
            cam.transform.rotation);
    }
}
