using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// "Press anything to continue" for dialogue/UI prompts, checked against every
// connected device rather than a specific one - Mouse.current alone (the
// original check) never fires on a Quest headset with no mouse attached, so
// every dialogue box would hang forever on real hardware. Works for mouse,
// keyboard, gamepad, and XR controller trigger/grip/buttons alike.
public static class AdvanceInput
{
    public static bool WasPressedThisFrame()
    {
        foreach (var device in InputSystem.devices)
        {
            foreach (var control in device.allControls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame)
                    return true;
            }
        }
        return false;
    }
}
