using System.Collections.Generic;
using UnityEngine;

// Wall keypad gate: press the buttons in the right order to unlock a small
// supply locker. Self-contained bonus interaction, not required to finish
// the main run - a wrong press just resets the attempt.
public class KeypadLock : MonoBehaviour
{
    public int[] code = { 2, 4, 1, 3 };
    public DoorSwing rewardDoor;
    public AudioSource audioSource;
    public AudioClip successClip;
    public AudioClip failClip;
    public string dialogueSpeaker = "ECHO";
    [TextArea] public string dialogueLine = "Huh. Someone really didn't want this found.";

    readonly List<int> entered = new List<int>();
    bool unlocked;

    public void OnButtonPressed(int index)
    {
        if (unlocked) return;
        entered.Add(index);
        int i = entered.Count - 1;

        if (i >= code.Length || entered[i] != code[i])
        {
            if (audioSource != null && failClip != null) audioSource.PlayOneShot(failClip);
            entered.Clear();
            return;
        }

        if (entered.Count == code.Length)
        {
            unlocked = true;
            if (audioSource != null && successClip != null) audioSource.PlayOneShot(successClip);
            if (rewardDoor != null) StartCoroutine(rewardDoor.SwingOpen());
            if (DialogueUI.Instance != null) DialogueUI.Instance.Show(dialogueSpeaker, dialogueLine, 4f);
        }
    }
}
