using System.Collections;
using UnityEngine;

public class FadeController : MonoBehaviour
{
    public static FadeController Instance { get; private set; }

    public CanvasGroup canvasGroup;

    void Awake()
    {
        Instance = this;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public IEnumerator FadeToBlack(float duration)
    {
        yield return Fade(0f, 1f, duration);
    }

    public IEnumerator FadeFromBlack(float duration)
    {
        yield return Fade(1f, 0f, duration);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = from;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
        canvasGroup.blocksRaycasts = to > 0.99f;
    }
}
