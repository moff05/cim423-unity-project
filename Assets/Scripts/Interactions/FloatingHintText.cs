using TMPro;
using UnityEngine;

// Small reusable floating world-space label, billboarded toward whichever
// camera is active. Built at runtime (no prefab/scene wiring needed) - the
// same pattern TwistHandle already used for its own "GRAB, THEN MOVE IN A
// CIRCLE" hint, pulled out here so every interactable can show an equally
// explicit "what to do" label instead of relying on the idle glow alone to
// communicate that ("no indicator... no guidance at all", direct feedback
// after the emission-only version still wasn't clear enough).
public class FloatingHintText : MonoBehaviour
{
    public Transform anchor;
    public float height = 0.45f;
    TextMeshPro text;

    public static FloatingHintText Attach(Transform anchor, float height = 0.45f, float scale = 0.02f)
    {
        var go = new GameObject("InteractionHint");
        var hint = go.AddComponent<FloatingHintText>();
        hint.anchor = anchor;
        hint.height = height;
        go.transform.localScale = Vector3.one * scale;

        hint.text = go.AddComponent<TextMeshPro>();
        hint.text.alignment = TextAlignmentOptions.Center;
        hint.text.color = new Color(0.6f, 0.95f, 1f);
        hint.text.fontSize = 36f;
        hint.text.enableWordWrapping = false;
        hint.text.outlineWidth = 0.2f;
        hint.text.outlineColor = Color.black;

        go.SetActive(false);
        return hint;
    }

    public void Show(string message)
    {
        if (text == null) return;
        text.text = message;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || anchor == null) return;
        transform.position = anchor.position + Vector3.up * height;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }
}
