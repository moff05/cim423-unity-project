using System.Collections;
using TMPro;
using UnityEngine;

public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    public CanvasGroup canvasGroup;
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public GameObject continuePrompt;

    // A generation token instead of StopCoroutine. Stopping the in-flight
    // routine used to permanently orphan any caller doing
    // "yield return ShowAndWaitForInput(...)" - Unity only resumes a
    // "yield return StartCoroutine(x)" caller when x finishes on its own;
    // externally stopping x leaves that caller suspended forever. That was
    // a real soft-lock: interacting with a second dialogue-triggering
    // object (another interactable, a RoomEntryZone echo line) while an
    // interactable's own ActivateRoutine was still "yield return"-ing on
    // its first line froze that interactable's step permanently, since
    // InteractionSequencer.CompleteStep never got reached. Superseded
    // lines now finish themselves (yield break) instead of being killed
    // from outside, so every caller's "yield return" always actually
    // returns.
    int token;

    void Awake()
    {
        Instance = this;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void Show(string speaker, string text, float holdDuration = 4f)
    {
        if (GameManager.Instance != null) { speaker = GameManager.Instance.Format(speaker); text = GameManager.Instance.Format(text); }
        int myToken = ++token;
        StartCoroutine(ShowRoutine(myToken, speaker, text, holdDuration));
    }

    public IEnumerator ShowAndWait(string speaker, string text, float holdDuration = 4f)
    {
        if (GameManager.Instance != null) { speaker = GameManager.Instance.Format(speaker); text = GameManager.Instance.Format(text); }
        int myToken = ++token;
        yield return StartCoroutine(ShowRoutine(myToken, speaker, text, holdDuration));
    }

    // Player-paced: shows the line and waits for a click instead of a fixed
    // timer, so the intro can be read at whatever speed someone wants.
    public IEnumerator ShowAndWaitForInput(string speaker, string text)
    {
        if (GameManager.Instance != null) { speaker = GameManager.Instance.Format(speaker); text = GameManager.Instance.Format(text); }
        int myToken = ++token;
        yield return StartCoroutine(ShowUntilInputRoutine(myToken, speaker, text));
    }

    IEnumerator ShowUntilInputRoutine(int myToken, string speaker, string text)
    {
        if (speakerText != null) speakerText.text = speaker;
        if (bodyText != null) bodyText.text = text;
        yield return Fade(1f, 0.3f);
        if (myToken != token) yield break;

        // Small grace period so the click that triggered this line doesn't
        // immediately double as the "continue" input.
        yield return new WaitForSeconds(0.35f);
        if (myToken != token) yield break;
        if (continuePrompt != null) continuePrompt.SetActive(true);

        while (!AdvanceInput.WasPressedThisFrame())
        {
            if (myToken != token)
            {
                if (continuePrompt != null) continuePrompt.SetActive(false);
                yield break;
            }
            yield return null;
        }

        if (continuePrompt != null) continuePrompt.SetActive(false);
        if (myToken != token) yield break;
        yield return Fade(0f, 0.5f);
    }

    IEnumerator ShowRoutine(int myToken, string speaker, string text, float holdDuration)
    {
        if (speakerText != null) speakerText.text = speaker;
        if (bodyText != null) bodyText.text = text;
        yield return Fade(1f, 0.3f);
        if (myToken != token) yield break;

        float t = 0f;
        while (t < holdDuration)
        {
            if (myToken != token) yield break;
            t += Time.deltaTime;
            yield return null;
        }
        if (myToken != token) yield break;
        yield return Fade(0f, 0.5f);
    }

    IEnumerator Fade(float target, float time)
    {
        if (canvasGroup == null) yield break;
        float start = canvasGroup.alpha;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, t / time);
            yield return null;
        }
        canvasGroup.alpha = target;
    }
}
