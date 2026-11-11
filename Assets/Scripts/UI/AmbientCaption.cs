using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Short captions that fade in and out on their own: PA announcements, voices
// on the radio, ECHO's stuck hints. Unlike DialogueUI they never wait for a
// click, so a beat can simply happen around the player. Also the subtitle
// track for every audio-only line.
public class AmbientCaption : MonoBehaviour
{
    public static AmbientCaption Instance { get; private set; }

    CanvasGroup group;
    TMP_Text text;
    readonly Queue<(string line, float seconds)> queue = new Queue<(string, float)>();
    bool running;

    void Awake()
    {
        Instance = this;

        var canvasGO = new GameObject("AmbientCaptionCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        group = canvasGO.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1300, 520);
        canvasGO.transform.localScale = Vector3.one * 0.0009f;

        var follow = canvasGO.AddComponent<WorldSpaceUIFollow>();
        follow.distance = 1.35f;
        follow.continuousFollow = false;
        follow.repositionAngle = 30f;
        follow.distanceTolerance = 0.5f;

        // Dark plate behind the words so they read against bright walls.
        var plateGO = new GameObject("CaptionPlate");
        plateGO.transform.SetParent(canvasGO.transform, false);
        var plate = plateGO.AddComponent<UnityEngine.UI.Image>();
        plate.color = new Color(0f, 0.03f, 0.05f, 0.72f);
        plate.raycastTarget = false;
        var plateRT = plateGO.GetComponent<RectTransform>();
        plateRT.anchorMin = new Vector2(0.02f, 0.18f);
        plateRT.anchorMax = new Vector2(0.98f, 0.5f);
        plateRT.offsetMin = Vector2.zero;
        plateRT.offsetMax = Vector2.zero;

        var textGO = new GameObject("CaptionText");
        textGO.transform.SetParent(canvasGO.transform, false);
        text = textGO.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 28f;
        text.fontSizeMax = 40f;
        text.color = new Color(0.86f, 0.93f, 1f);
        text.fontStyle = FontStyles.Italic;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.9f);
        text.text = "";
        V2Text.MakeOverlay(text);
        // Pinned to the bottom of a tall canvas: below the eyeline, above the
        // dialogue bar.
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.05f, 0.2f);
        textRT.anchorMax = new Vector2(0.95f, 0.48f);
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
    }

    public static void Say(string speaker, string line, float seconds = 5f)
    {
        if (Instance == null) return;
        if (GameManager.Instance != null) line = GameManager.Instance.Format(line);
        string full = string.IsNullOrEmpty(speaker) ? line : "<color=#59D9FF><b>" + speaker + "</b></color>  " + line;
        Instance.queue.Enqueue((full, seconds));
        if (!Instance.running) Instance.StartCoroutine(Instance.Run());
    }

    IEnumerator Run()
    {
        running = true;
        while (queue.Count > 0)
        {
            var (line, seconds) = queue.Dequeue();
            text.text = line;
            yield return Fade(1f, 0.35f);
            yield return new WaitForSeconds(seconds);
            yield return Fade(0f, 0.5f);
        }
        running = false;
    }

    IEnumerator Fade(float target, float time)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, target, t / time);
            yield return null;
        }
        group.alpha = target;
    }
}
