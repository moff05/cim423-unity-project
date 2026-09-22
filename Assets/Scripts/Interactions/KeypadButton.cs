using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// One button on a KeypadLock panel. Works with both a ray/near-far select
// and a physical poke, since it's just a plain XRSimpleInteractable.
public class KeypadButton : MonoBehaviour
{
    public int index;
    public KeypadLock lockPanel;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;
    Renderer rend;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        rend = GetComponent<Renderer>();
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null)
            interactable.selectEntered.AddListener(args => { HapticUtility.Pulse(args.interactorObject, 0.25f, 0.05f); Press(); });
    }

    void Press()
    {
        FlashColor(Color.white * 2f);
        lockPanel?.OnButtonPressed(index);
    }

    public void FlashColor(Color c)
    {
        if (rend == null) return;
        mpb.SetColor(EmissionColor, c);
        rend.SetPropertyBlock(mpb);
        Invoke(nameof(ClearFlash), 0.2f);
    }

    void ClearFlash()
    {
        mpb.SetColor(EmissionColor, Color.black);
        rend.SetPropertyBlock(mpb);
    }
}
