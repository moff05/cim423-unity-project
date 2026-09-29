#if UNITY_EDITOR
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

// Editor-only dev shortcut: press F9 in Play mode to jump straight to the
// artifact-reveal ending, skipping the full 6-step playthrough. Built so
// the ending could be iterated on without fighting the XR Device
// Simulator's unreliable grab/twist gestures through every earlier step
// just to see the last 5 seconds. Self-installs via
// RuntimeInitializeOnLoadMethod, so it needs no scene wiring, and the
// whole file is compiled out of every non-Editor build automatically.
public static class DebugSkipToEnding
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var go = new GameObject("~DebugSkipToEnding (Editor only - press F9)");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<Runner>();
    }

    class Runner : MonoBehaviour
    {
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
                Skip();
        }

        void Skip()
        {
            LabState.IntroShown = true;

            // Otherwise ambience (including the breathing layer) never
            // starts, and the Start Screen canvas sits on top of everything.
            GameManager.Instance?.MarkIntroStarted();
            GameManager.Instance?.MarkStoryStarted();
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas.gameObject.name == "StartScreenCanvas")
                    canvas.gameObject.SetActive(false);
            }

            var seq = InteractionSequencer.Instance;
            if (seq == null)
            {
                Debug.LogWarning("[DebugSkip] No InteractionSequencer in the scene yet - is the game actually running?");
                return;
            }
            var field = typeof(InteractionSequencer).GetField("currentIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(seq, seq.stepOrder.Count - 1);

            var artifacts = Resources.FindObjectsOfTypeAll<ArtifactInteractable>();
            if (artifacts.Length == 0)
            {
                Debug.LogWarning("[DebugSkip] No ArtifactInteractable found in the scene.");
                return;
            }

            // The Lab/Station room chain starts inactive until the player
            // reaches it normally - force the whole ancestor chain active
            // so the reveal has walls, lights, and the pedestal to work with.
            var t = artifacts[0].transform;
            while (t != null)
            {
                t.gameObject.SetActive(true);
                t = t.parent;
            }

            artifacts[0].OnActivate();
            Debug.Log("[DebugSkip] Jumped to the artifact ending.");
        }
    }
}
#endif
