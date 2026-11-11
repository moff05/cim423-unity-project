using System.Collections.Generic;
using TMPro;
using UnityEngine;

// A gold "NEXT" marker that hangs over the current objective and shows
// through walls, with the distance to it. Headset feedback was that it was
// not obvious where the crew quarters were or that the terminal was the thing
// to go back to; the glow only helps once the object is already in view.
public class WaypointMarker : MonoBehaviour
{
    TextMeshPro text;
    readonly Dictionary<string, Transform> targets = new Dictionary<string, Transform>();
    bool gathered;

    void Awake()
    {
        text = gameObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 36f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = new Color(1f, 0.82f, 0.25f);
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;
        V2Text.MakeOverlay(text);
        text.enabled = false;
    }

    void Gather()
    {
        gathered = true;
        foreach (var t in FindObjectsByType<TwistHandle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!string.IsNullOrEmpty(t.stepId)) targets[t.stepId] = t.transform;
        foreach (var h in FindObjectsByType<HoldInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!string.IsNullOrEmpty(h.stepId) && h.stepId != ArtifactInteractable.StepId) targets[h.stepId] = h.transform;
        var terminal = FindFirstObjectByType<TerminalInteractable>(FindObjectsInactive.Include);
        if (terminal != null) targets[TerminalInteractable.StepId] = terminal.transform;
        var recorder = FindFirstObjectByType<RecorderInteractable>(FindObjectsInactive.Include);
        if (recorder != null) targets[RecorderInteractable.StepId] = recorder.transform;
        var artifact = FindFirstObjectByType<ArtifactInteractable>(FindObjectsInactive.Include);
        if (artifact != null) targets[ArtifactInteractable.StepId] = artifact.transform;
    }

    void LateUpdate()
    {
        var gm = GameManager.Instance;
        var seq = InteractionSequencer.Instance;
        var cam = Camera.main;
        if (gm == null || seq == null || cam == null || !gm.StoryStarted || StoryState.EndingStarted) { text.enabled = false; return; }
        if (!gathered) Gather();
        string step = seq.CurrentStepId;
        if (step == null || !targets.TryGetValue(step, out var target) || target == null || !target.gameObject.activeInHierarchy)
        {
            text.enabled = false;
            return;
        }

        Vector3 pos = target.position + Vector3.up * 0.95f;
        float dist = Vector3.Distance(cam.transform.position, target.position);
        // Up close the object's own glow and hint take over.
        if (dist < 1.8f) { text.enabled = false; return; }
        text.enabled = true;
        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
        // Grow with distance so it stays about the same size in view.
        transform.localScale = Vector3.one * 0.02f * Mathf.Clamp(dist / 3.5f, 0.8f, 4f);
        text.text = "NEXT\n<size=65%>" + Mathf.RoundToInt(dist) + " m</size>";
    }
}
