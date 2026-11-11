using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Persistent "what do I do next" readout - per direct, repeated feedback
// ("the next item on the list should have a yellow glow or something...
// very obvious what the next thing to do is"). The current step's object
// already pulses gold, but that only helps once you're already looking at
// the right thing. This says it in words, independent of where you're
// looking, and updates automatically as InteractionSequencer advances.
//
// Self-installs at runtime (same "no scene wiring needed" pattern
// TwistHandle already used for its own hint text), so it works without
// touching the scene file.
//
// Uses WorldSpaceUIFollow with continuousFollow=false (snap only when the
// player turns far enough away, not every single frame) rather than a raw
// camera-locked billboard. A HUD element that rigidly chases your head
// every frame is exactly the "the background moves with me" bug already
// found and fixed on DialogueCanvas - this deliberately reuses that same,
// now-correct, non-chasing follow behavior instead of reintroducing it
// here for a different element.
public class ObjectiveHUD : MonoBehaviour
{
    // Created by V2Bootstrap on every scene load (so it survives Restart).

    CanvasGroup canvasGroup;
    TMP_Text text;
    string lastShown;
    string lastLine;
    ArtifactInteractable artifact;

    void Awake()
    {
        var canvasGO = new GameObject("ObjectiveHUDCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        var rt = canvasGO.GetComponent<RectTransform>();
        // Tall canvas with the text pinned to its top edge: the line sits
        // above the player's eyeline instead of across the middle of it.
        rt.sizeDelta = new Vector2(1100, 760);
        canvasGO.transform.localScale = Vector3.one * 0.0009f;

        var follow = canvasGO.AddComponent<WorldSpaceUIFollow>();
        follow.distance = 1.3f;
        follow.continuousFollow = false;
        follow.repositionAngle = 30f;
        follow.distanceTolerance = 0.5f;

        var textGO = new GameObject("ObjectiveText");
        textGO.transform.SetParent(canvasGO.transform, false);
        text = textGO.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Top;
        text.fontSize = 34f;
        text.color = new Color(1f, 0.85f, 0.3f);
        text.text = "";
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 0.86f);
        textRT.anchorMax = new Vector2(1f, 1f);
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        canvasGroup.alpha = 0f;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.StoryStarted) { canvasGroup.alpha = 0f; return; }

        var seq = InteractionSequencer.Instance;
        string stepId = seq != null ? seq.CurrentStepId : null;

        if (stepId == "artifact")
        {
            if (artifact == null) artifact = FindFirstObjectByType<ArtifactInteractable>(FindObjectsInactive.Include);
            if (artifact != null && artifact.HasTriggered) { canvasGroup.alpha = 0f; return; }
        }

        string desc = seq != null ? seq.CurrentStepDescription : null;
        if (desc == null) { canvasGroup.alpha = 0f; return; }

        canvasGroup.alpha = 1f;
        if (desc != lastShown)
        {
            lastShown = desc;
        }
        string line = "NEXT: " + desc;
        if (StoryState.LogsFound > 0)
            line += "\n<size=70%><color=#59D9FF>CREW LOGS " + StoryState.LogsFound + "/" + StoryState.TotalLogs + "</color></size>";
        if (line != lastLine)
        {
            lastLine = line;
            text.text = line;
        }
    }
}
