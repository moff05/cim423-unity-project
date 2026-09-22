using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    public CanvasGroup canvasGroup;
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public GameObject continuePrompt;

    Coroutine current;

    void Awake()
    {
        Instance = this;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void Show(string speaker, string text, float holdDuration = 4f)
    {
        if (GameManager.Instance != null) { speaker = GameManager.Instance.Format(speaker); text = GameManager.Instance.Format(text); }
        if (current != null) StopCoroutine(current);
        current = StartCoroutine(ShowRoutine(speaker, text, holdDuration));
    }

    public IEnumerator ShowAndWait(string speaker, string text, float holdDuration = 4f)
    {
        if (GameManager.Instance != null) { speaker = GameManager.Instance.Format(speaker); text = GameManager.Instance.Format(text); }
        if (current != null) StopCoroutine(current);
        yield return current = StartCoroutine(ShowRoutine(speaker, text, holdDuration));
    }

    // Player-paced: shows the line and waits for a click instead of a fixed
    // timer, so the intro can be read at whatever speed someone wants.
    public IEnumerator ShowAndWaitForInput(string speaker, string text)
    {
        if (GameManager.Instance != null) { speaker = GameManager.Instance.Format(speaker); text = GameManager.Instance.Format(text); }
        if (current != null) StopCoroutine(current);
        yield return current = StartCoroutine(ShowUntilInputRoutine(speaker, text));
    }

    IEnumerator ShowUntilInputRoutine(string speaker, string text)
    {
        if (speakerText != null) speakerText.text = speaker;
        if (bodyText != null) bodyText.text = text;
        yield return Fade(1f, 0.3f);

        // Small grace period so the click that triggered this line doesn't
        // immediately double as the "continue" input.
        yield return new WaitForSeconds(0.35f);
        if (continuePrompt != null) continuePrompt.SetActive(true);

        while (true)
        {
            var mouse = Mouse.current;
            bool advance = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (advance) break;
            yield return null;
        }

        if (continuePrompt != null) continuePrompt.SetActive(false);
        yield return Fade(0f, 0.5f);
    }

    IEnumerator ShowRoutine(string speaker, string text, float holdDuration)
    {
        if (speakerText != null) speakerText.text = speaker;
        if (bodyText != null) bodyText.text = text;
        yield return Fade(1f, 0.3f);
        yield return new WaitForSeconds(holdDuration);
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
