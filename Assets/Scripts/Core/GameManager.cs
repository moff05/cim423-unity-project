using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public string PlayerName { get; private set; } = "Traveler";
    public bool StoryStarted { get; private set; }
    // Set the moment the intro cinematic begins (before the flight/jump),
    // as opposed to StoryStarted which fires once the player is actually
    // standing in the station - ambience listens to this one so the ship
    // isn't dead silent during the opening.
    public bool IntroStarted { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SetPlayerName(string enteredName)
    {
        PlayerName = string.IsNullOrWhiteSpace(enteredName) ? "Traveler" : enteredName.Trim();
    }

    public void MarkIntroStarted()
    {
        IntroStarted = true;
    }

    public void MarkStoryStarted()
    {
        StoryStarted = true;
    }

    public string Format(string line)
    {
        return string.IsNullOrEmpty(line) ? line : line.Replace("{PlayerName}", PlayerName);
    }
}
