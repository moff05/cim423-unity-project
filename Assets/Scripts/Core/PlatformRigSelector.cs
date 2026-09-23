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
        // XR is the default everywhere now: real on Android, simulated in
        // the Editor via the XR Device Simulator. Flip this to force the
        // flat desktop rig back on for a quick non-VR sanity check.
        bool useXR = true;

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
