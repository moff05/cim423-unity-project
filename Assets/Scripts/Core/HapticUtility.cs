using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Small shared helper so every interactable can give the controller a buzz
// without each script needing to know the concrete interactor type.
public static class HapticUtility
{
    public static void Pulse(object interactor, float amplitude = 0.4f, float duration = 0.08f)
    {
        if (interactor is XRBaseInputInteractor controller)
            controller.SendHapticImpulse(amplitude, duration);
    }
}
