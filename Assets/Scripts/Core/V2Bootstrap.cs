using UnityEngine;
using UnityEngine.SceneManagement;

// Creates the runtime-only helpers (HUD, captions, lighting) every time the
// scene loads. They used to self-install with RuntimeInitializeOnLoadMethod,
// which only runs once per app launch: after Restart reloaded the scene the
// objective HUD was simply gone for the second playthrough.
public static class V2Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StoryState.Reset();
        LabState.IntroShown = false;
        new GameObject("ObjectiveHUD").AddComponent<ObjectiveHUD>();
        new GameObject("AmbientCaption").AddComponent<AmbientCaption>();
        new GameObject("LightingDirector").AddComponent<LightingDirector>();
        new GameObject("WaypointMarker").AddComponent<WaypointMarker>();
        ComfortSettings.Apply();
    }
}
