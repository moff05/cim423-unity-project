using UnityEngine;

// Fires an ECHO line once when the player walks within range. Distance-based
// rather than a physics trigger since the player camera has no collider.
public class RoomEntryZone : MonoBehaviour
{
    public Transform player;
    public float radius = 3f;
    [TextArea] public string echoLine;
    public float holdDuration = 4f;

    bool fired;

    void Update()
    {
        if (fired || player == null || string.IsNullOrEmpty(echoLine)) return;
        if (Vector3.Distance(player.position, transform.position) <= radius)
        {
            fired = true;
            DialogueUI.Instance?.Show("ECHO", echoLine, holdDuration);
        }
    }
}
