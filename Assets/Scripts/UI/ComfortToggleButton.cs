using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Start-screen button that flips one comfort option and shows its state.
public class ComfortToggleButton : MonoBehaviour
{
    public enum Option { TurnMode, Vignette }
    public Option option;
    public TMP_Text label;

    void Awake()
    {
        var button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Toggle);
        Refresh();
    }

    void Toggle()
    {
        if (option == Option.TurnMode) ComfortSettings.SmoothTurn = !ComfortSettings.SmoothTurn;
        else ComfortSettings.Vignette = !ComfortSettings.Vignette;
        Refresh();
    }

    void Refresh()
    {
        if (label == null) return;
        label.text = option == Option.TurnMode
            ? "TURN: " + (ComfortSettings.SmoothTurn ? "SMOOTH" : "SNAP")
            : "VIGNETTE: " + (ComfortSettings.Vignette ? "ON" : "OFF");
    }
}
