using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Screen-center plus-shaped reticle: brightens and shows a prompt when the
// desktop interaction proxy is aiming at something interactable.
public class CrosshairUI : MonoBehaviour
{
    public static CrosshairUI Instance { get; private set; }

    public Image barH;
    public Image barV;
    public TMP_Text promptText;
    public Color normalColor = new Color(1f, 1f, 1f, 0.55f);
    public Color hoverColor = new Color(1f, 0.82f, 0.25f, 1f);

    void Awake()
    {
        Instance = this;
        SetHovering(false);
    }

    public void SetHovering(bool hovering)
    {
        Color c = hovering ? hoverColor : normalColor;
        float scale = hovering ? 1.5f : 1f;
        if (barH != null) { barH.color = c; barH.rectTransform.localScale = new Vector3(scale, scale, 1f); }
        if (barV != null) { barV.color = c; barV.rectTransform.localScale = new Vector3(scale, scale, 1f); }
        if (promptText != null) promptText.gameObject.SetActive(hovering);
    }
}
