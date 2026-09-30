using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Quest's system virtual keyboard doesn't render inside a fully immersive
// VR app - confirmed via logcat during an actual on-device attempt to
// type: zero InputMethodManager/IME activity at all, meaning
// TMP_InputField's normal TouchScreenKeyboard.Open() flow never got
// anywhere, regardless of the shouldHideMobileInput/shouldHideSoftKeyboard
// flags. This is a small in-scene keyboard instead, built from the same
// Button+Image components the already-working BEGIN button uses (proven
// to register clicks correctly through TrackedDeviceGraphicRaycaster),
// so typing a call sign doesn't depend on the OS keyboard at all - point
// the controller ray at a letter and pull the trigger, same interaction
// model as everything else in the game.
public class OnScreenKeyboard : MonoBehaviour
{
    public TMP_InputField target;
    public int maxLength = 16;

    static readonly Color KeyColor = new Color(0.08f, 0.35f, 0.4f);
    static readonly Color KeyLabelColor = new Color(0.7f, 1f, 1f);
    static readonly Color WideKeyColor = new Color(0.12f, 0.45f, 0.5f);

    void Awake()
    {
        BuildKeyboard();
    }

    void BuildKeyboard()
    {
        var root = GetComponent<RectTransform>();

        string[] rows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
        for (int r = 0; r < rows.Length; r++)
        {
            var rowGO = new GameObject("Row" + r, typeof(RectTransform));
            var rowRT = (RectTransform)rowGO.transform;
            rowRT.SetParent(root, false);
            float rowH = 1f / (rows.Length + 1);
            float top = 1f - r * rowH;
            rowRT.anchorMin = new Vector2(0f, top - rowH);
            rowRT.anchorMax = new Vector2(1f, top);
            rowRT.offsetMin = Vector2.zero;
            rowRT.offsetMax = Vector2.zero;

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            string row = rows[r];
            foreach (char c in row)
            {
                char captured = c;
                CreateKey(rowRT, c.ToString(), KeyColor, () => AppendChar(captured));
            }
        }

        // Bottom row: space + backspace, wider and visually distinct so
        // they read as different-purpose keys rather than more letters.
        var bottomGO = new GameObject("RowBottom", typeof(RectTransform));
        var bottomRT = (RectTransform)bottomGO.transform;
        bottomRT.SetParent(root, false);
        float bottomRowH = 1f / (rows.Length + 1);
        bottomRT.anchorMin = new Vector2(0f, 0f);
        bottomRT.anchorMax = new Vector2(1f, bottomRowH);
        bottomRT.offsetMin = Vector2.zero;
        bottomRT.offsetMax = Vector2.zero;
        var bottomLayout = bottomGO.AddComponent<HorizontalLayoutGroup>();
        bottomLayout.childAlignment = TextAnchor.MiddleCenter;
        bottomLayout.spacing = 6f;
        bottomLayout.childForceExpandWidth = true;
        bottomLayout.childForceExpandHeight = true;

        var spaceBtn = CreateKey(bottomRT, "SPACE", WideKeyColor, () => AppendChar(' '));
        var spaceLE = spaceBtn.gameObject.AddComponent<LayoutElement>();
        spaceLE.flexibleWidth = 3f;

        var delBtn = CreateKey(bottomRT, "DEL", WideKeyColor, Backspace);
        var delLE = delBtn.gameObject.AddComponent<LayoutElement>();
        delLE.flexibleWidth = 1.4f;
    }

    Button CreateKey(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Key_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = color;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = (RectTransform)labelGO.transform;
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        var text = labelGO.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = KeyLabelColor;
        text.fontSize = label.Length > 1 ? 26f : 32f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = text.fontSize;

        return button;
    }

    void AppendChar(char c)
    {
        if (target == null) return;
        if (target.text.Length >= maxLength) return;
        target.text += c;
        target.caretPosition = target.text.Length;
    }

    void Backspace()
    {
        if (target == null || target.text.Length == 0) return;
        target.text = target.text.Substring(0, target.text.Length - 1);
        target.caretPosition = target.text.Length;
    }
}
