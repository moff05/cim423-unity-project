using TMPro;
using UnityEngine;

// Shared text styling. The overlay material draws on top of world geometry,
// so hints, the objective line and captions can never sink into a wall
// (headset feedback: "the words bleed into the wall").
public static class V2Text
{
    static Material overlay;
    static bool looked;

    public static void MakeOverlay(TMP_Text text)
    {
        if (text == null) return;
        if (!looked) { looked = true; overlay = Resources.Load<Material>("V2/TMPOverlay"); }
        if (overlay != null) text.fontSharedMaterial = overlay;
    }
}
