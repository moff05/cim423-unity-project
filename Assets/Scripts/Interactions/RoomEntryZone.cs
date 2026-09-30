using UnityEngine;

// Fires an ECHO line once when the player walks within range. Distance-based
// rather than a physics trigger since the player camera has no collider.
public class RoomEntryZone : MonoBehaviour
{
    public float radius = 3f;
    [TextArea] public string echoLine;
    public float holdDuration = 4f;

    bool fired;

    void Update()
    {
        if (fired || string.IsNullOrEmpty(echoLine)) return;
        // Was a fixed Transform reference wired in the Inspector to the
        // desktop rig's camera specifically - real builds force XR on
        // (PlatformRigSelector), so that camera sits permanently inactive at
        // wherever it was left in the editor and never moves. Distance to it
        // never closed, so this never fired on the Quest. Camera.main
        // resolves to whichever rig is actually active, every frame.
        var player = Camera.main;
        if (player == null) return;
        if (Vector3.Distance(player.transform.position, transform.position) <= radius)
        {
            fired = true;
            DialogueUI.Instance?.Show("ECHO", echoLine, holdDuration);
        }
    }
}
