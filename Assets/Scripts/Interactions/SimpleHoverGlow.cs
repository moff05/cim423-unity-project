using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Generic hover glow for any XRBaseInteractable (grabbable props included) -
// the rest of the project's interactables each roll their own emission
// pulse, but grabbables don't need the idle pulse, just a hover cue.
[RequireComponent(typeof(Renderer))]
public class SimpleHoverGlow : MonoBehaviour
{
    public Renderer targetRenderer;
    public Color glowColor = new Color(1f, 1f, 1f, 1f) * 2f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        var interactable = GetComponent<XRBaseInteractable>();
        if (interactable != null)
        {
            interactable.hoverEntered.AddListener(_ => SetGlow(true));
            interactable.hoverExited.AddListener(_ => SetGlow(false));
        }
    }

    void SetGlow(bool on)
    {
        mpb.SetColor(EmissionColor, on ? glowColor : Color.black);
        targetRenderer.SetPropertyBlock(mpb);
    }
}
