using UnityEngine;

// Comfort options chosen on the start screen. Turn mode is applied by
// reflection so this file does not depend on the XRI Starter Assets assembly.
public static class ComfortSettings
{
    const string TurnKey = "comfort.smoothTurn";
    const string VignetteKey = "comfort.vignette";

    public static bool SmoothTurn
    {
        get => PlayerPrefs.GetInt(TurnKey, 0) == 1;
        set { PlayerPrefs.SetInt(TurnKey, value ? 1 : 0); Apply(); }
    }

    public static bool Vignette
    {
        get => PlayerPrefs.GetInt(VignetteKey, 0) == 1;
        set { PlayerPrefs.SetInt(VignetteKey, value ? 1 : 0); Apply(); }
    }

    public static void Apply()
    {
        bool smooth = SmoothTurn;
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            var type = mb.GetType();
            if (type.Name == "ControllerInputActionManager")
            {
                var prop = type.GetProperty("smoothTurnEnabled");
                if (prop != null && prop.CanWrite) prop.SetValue(mb, smooth);
            }
            else if (type.Name == "TunnelingVignetteController")
            {
                mb.gameObject.SetActive(Vignette);
            }
        }
    }
}
