using System.Collections.Generic;

// What the player has actually found out this run. Drives which ending text
// they get (what ECHO told them vs. what they know) and the logs tally.
public static class StoryState
{
    public const int TotalLogs = 5;
    // Enough of the crew's own words to see through ECHO's version of events.
    public const int InformedThreshold = 3;

    static readonly HashSet<string> logs = new HashSet<string>();

    public static bool PowerRestored;
    public static bool HeardVoices;
    // Set the moment either final choice (touch / seal) is committed, so the
    // other one can no longer be started mid-sequence.
    public static bool EndingStarted;

    public static int LogsFound => logs.Count;
    public static bool Informed => logs.Count >= InformedThreshold;

    // True the first time a given log is read.
    public static bool MarkLog(string id) => logs.Add(id);

    public static void Reset()
    {
        logs.Clear();
        PowerRestored = false;
        HeardVoices = false;
        EndingStarted = false;
    }
}
