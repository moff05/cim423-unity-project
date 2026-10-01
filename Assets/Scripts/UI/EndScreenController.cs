using UnityEngine;
using UnityEngine.UI;

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
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
