using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// "Click to continue" for dialogue/UI prompts. Only real click inputs count:
// the XR controller trigger (or primary face button), mouse left, keyboard
// space/enter, gamepad south. The earlier version accepted ANY button control
// on ANY device, which on a Quest includes touch sensors and tracking-state
// flags that fire constantly, so dialogue skipped itself.
public static class AdvanceInput
{
#if UNITY_EDITOR
    // Editor-only: lets an automated playthrough click through dialogue.
    public static bool EditorAutoAdvance;
#endif

    public static bool WasPressedThisFrame()
    {
#if UNITY_EDITOR
        if (EditorAutoAdvance) return true;
#endif
        foreach (var device in InputSystem.devices)
        {
            if (device is Mouse mouse && mouse.leftButton.wasPressedThisFrame) return true;
            if (device is Keyboard kb && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) return true;
            if (device is Gamepad pad && pad.buttonSouth.wasPressedThisFrame) return true;

            if (device is UnityEngine.InputSystem.XR.XRController)
            {
                foreach (var name in new[] { "triggerPressed", "primaryButton" })
                {
                    if (device.TryGetChildControl<ButtonControl>(name) is ButtonControl b && b.wasPressedThisFrame)
                        return true;
                }
            }
        }
        return false;
    }
}
