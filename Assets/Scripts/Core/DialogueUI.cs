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


    void Awake()
    {
        Instance = this;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    // Every line is player-paced: it waits for a trigger click and never
    // auto-dismisses. holdDuration is kept for call-site compatibility but
    // ignored. Lines queue instead of replacing each other, so a line fired
    // by an interaction (door opening, zone entry) can't cut the previous
    // one off before it has been read.
    bool showing;
    int pending;
    // True while a line is on screen or queued behind one.
    public bool IsBusy => showing || pending > 0;

    void OnDisable() { showing = false; pending = 0; }

    public void Show(string speaker, string text, float holdDuration = 4f)
    {
        if (GameManager.Instance != null) { speaker = GameManager.Instance.Format(speaker); text = GameManager.Instance.Format(text); }
        StartCoroutine(ShowUntilInputRoutine(speaker, text));
    }

    public IEnumerator ShowAndWait(string speaker, string text, float holdDuration = 4f)
    {
        yield return ShowAndWaitForInput(speaker, text);
    }

    public IEnumerator ShowAndWaitForInput(string speaker, string text)
    {
        if (GameManager.Instance != null) { speaker = GameManager.Instance.Format(speaker); text = GameManager.Instance.Format(text); }
        yield return StartCoroutine(ShowUntilInputRoutine(speaker, text));
    }

    IEnumerator ShowUntilInputRoutine(string speaker, string text)
    {
        pending++;
        while (showing) yield return null;
        pending--;
        showing = true;

        if (speakerText != null) speakerText.text = speaker;
        if (bodyText != null) bodyText.text = text;
        yield return Fade(1f, 0.3f);

        // Input is ignored for a minimum read time (scaled by length) so the
        // click that triggered this line, or a fast double-click, can't
        // skip it before it can be read.
        yield return new WaitForSeconds(Mathf.Clamp(text.Length * 0.03f, 1.2f, 4f));
        if (continuePrompt != null) continuePrompt.SetActive(true);

        while (!AdvanceInput.WasPressedThisFrame())
            yield return null;

        if (continuePrompt != null) continuePrompt.SetActive(false);
        yield return Fade(0f, 0.5f);
        showing = false;
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
