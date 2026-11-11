using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EndScreenController : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public Button restartButton;

    void Awake()
    {
        // Diagnostic breadcrumbs, same pattern as ArtifactInteractable's
        // [ArtifactEnding] logs - added after "restart and quit don't
        // work" couldn't be reproduced or confirmed from the last device
        // test (the app was paused ~15s after the ending sequence
        // completed, before there was clear evidence either button was
        // ever actually pressed). Tagged [EndScreen], visible via
        // adb logcat.
        Debug.Log("[EndScreen] Awake - canvasGroup=" + (canvasGroup != null) + " restartButton=" + (restartButton != null));
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
    }

    // Which ending text to show depends on the final choice and on how many
    // crew logs the player actually read: what ECHO told them vs. what they
    // found out for themselves.
    public void Show(EndingKind kind)
    {
        string title, body;
        bool informed = StoryState.Informed;
        switch (kind)
        {
            case EndingKind.Day181:
                title = "DAY 181";
                body = "For the five crew, mission Day 181 never ended. They are still inside it, and for twelve seconds, so were you. ECHO has not stopped listening.";
                break;
            case EndingKind.Sealed:
                title = "STATION SEALED";
                body = informed
                    ? "You knew they were still in there, and you sealed it anyway. Maybe that was mercy. ECHO has not spoken since."
                    : "You cut the power and sealed the lab. The transmission stopped. ECHO has not spoken since, and you never learned why.";
                break;
            default:
                title = "COORDINATES LOGGED";
                body = "You logged the station for recovery and left. Whatever happened to the crew on Day 181, you never found out. They left more behind than you read.";
                break;
        }
        body += "\n\n<color=#59D9FF>CREW LOGS RECOVERED  " + StoryState.LogsFound + " / " + StoryState.TotalLogs + "</color>";
        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name == "EndTitle") t.text = title;
            else if (t.name == "EndBody") t.text = body;
        }
        Debug.Log("[EndScreen] ending=" + kind + " logs=" + StoryState.LogsFound);
        Show();
    }

    public void Show()
    {
        Debug.Log("[EndScreen] Show() called at realtime=" + Time.realtimeSinceStartup);
        if (canvasGroup == null) return;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
    }

    void OnRestart()
    {
        Debug.Log("[EndScreen] OnRestart clicked at realtime=" + Time.realtimeSinceStartup);
        // LabState.IntroShown is a plain static bool, not scene state - it
        // survives a scene reload on its own (statics only reset on a full
        // domain reload/app relaunch). Without this, restarting after
        // reaching the ending would silently skip ECHO's one-time room
        // intro line on the second playthrough.
        LabState.IntroShown = false;
        StoryState.Reset();
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
