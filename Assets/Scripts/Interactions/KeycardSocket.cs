using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Attach to the socket receptacle. Fires once when the keycard is plugged
// in - a small bonus beat, not required to finish the main run.
public class KeycardSocket : MonoBehaviour
{
    public Renderer indicatorRenderer;
    public AudioSource audioSource;
    public AudioClip plugInClip;
    public string dialogueSpeaker = "ECHO";
    [TextArea] public string dialogueLine = "Auxiliary power confirmed. Whoever left that card behind wasn't planning on coming back for it.";

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;
    bool used;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        var socket = GetComponent<XRSocketInteractor>();
        if (socket != null) socket.selectEntered.AddListener(OnCardInserted);
    }

    void OnCardInserted(SelectEnterEventArgs args)
    {
        if (used) return;
        used = true;

        if (audioSource != null && plugInClip != null) audioSource.PlayOneShot(plugInClip);

        if (indicatorRenderer != null)
        {
            mpb.SetColor(EmissionColor, new Color(0.2f, 1f, 0.3f) * 2f);
            indicatorRenderer.SetPropertyBlock(mpb);
        }

        if (DialogueUI.Instance != null) DialogueUI.Instance.Show(dialogueSpeaker, dialogueLine, 4f);
    }
}
