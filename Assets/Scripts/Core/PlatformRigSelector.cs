using UnityEngine;

// Chooses which player rig actually drives the game: the desktop mouse/
// keyboard FreeLookWalker rig, or the tracked XR Origin (head + Quest 3
// controllers) - real on an Android device build, simulated via the XR
// Device Simulator in the Editor. Runs in Awake, before anything (Start
// Screen -> IntroSequence.Play) can read IntroSequence's player references,
// and retargets them to whichever rig is actually active.
public class PlatformRigSelector : MonoBehaviour
{
    public GameObject desktopRig;
    public GameObject xrRig;
    public Camera xrCamera;
    public Transform xrPlayerRoot;
    public GameObject xrLocomotion;
    public IntroSequence introSequence;

    void Awake()
    {
        // Real builds (Android/Quest) always use XR. In the Editor, default
        // to the flat desktop rig (mouse-look + click-to-interact via
        // DesktopInteractionProxy) instead of the XR Device Simulator -
        // walking through the whole sequence with simulated VR grab/twist
        // gestures just to test something like the ending was the opposite
        // of easy. Flip this back to true (or wrap it in an Editor-only
        // toggle) if you specifically need to test simulated-XR behavior
        // in the Editor rather than real desktop input.
#if UNITY_EDITOR
        bool useXR = false;
#else
        bool useXR = true;
#endif

        if (desktopRig != null) desktopRig.SetActive(!useXR);
        if (xrRig != null) xrRig.SetActive(useXR);

        // Only the active rig's camera should carry the MainCamera tag -
        // Camera.main, and anything built on it (including editor tooling),
        // resolves to whichever camera has this tag, active or not.
        if (desktopRig != null) desktopRig.tag = useXR ? "Untagged" : "MainCamera";
        if (xrCamera != null) xrCamera.tag = useXR ? "MainCamera" : "Untagged";

        if (useXR && introSequence != null)
        {
            introSequence.playerCamera = xrCamera;
            introSequence.playerController = null;
            introSequence.playerRoot = xrPlayerRoot;
            introSequence.xrLocomotion = xrLocomotion;
        }

        // The player starts parked essentially on top of the docking bay
        // spawn point, so the destination station was visible (and
        // standable) before the player ever pressed Begin - the intro
        // cinematic's "departure" then visibly teleported them out of a
        // room they were already in. Hide the station and freeze movement
        // (no floor to stand on with it hidden) until IntroSequence reveals
        // both again at the end of the flight.
        if (introSequence != null && introSequence.interiorRoot != null)
            introSequence.interiorRoot.SetActive(false);
        if (xrLocomotion != null) xrLocomotion.SetActive(false);
    }
}
