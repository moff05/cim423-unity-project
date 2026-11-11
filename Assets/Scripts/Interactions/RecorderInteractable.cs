using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to Recorder. Hover scales it up and lights it; activating plays back
// its last recording.
public class RecorderInteractable : MonoBehaviour, IDesktopInteractable
{
    public const string StepId = "recorder";

    public Renderer recorderRenderer;
    public AudioSource audioSource;
    public float hoverScale = 1.25f;
    public float scaleTweenTime = 0.2f;
    public float idlePulseSpeed = 1.6f;
    public float idlePulseMin = 0.15f;
    public float idlePulseMax = 0.4f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;
    Vector3 baseScale;
    Coroutine scaleRoutine;
    bool played;
    bool hovering;
    FloatingHintText hint;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        hint = FloatingHintText.Attach(transform);
    }

    void Update()
    {
        if (recorderRenderer == null || hovering || played) return;
        if (!IsCurrentlyInteractable()) { ApplyEmission(Color.black); return; }
        ApplyEmission(InteractionSequencer.PulseColor(idlePulseSpeed, idlePulseMin, idlePulseMax));
    }

    public bool IsCurrentlyInteractable()
    {
        if (played) return false;
        return InteractionSequencer.Instance == null || InteractionSequencer.Instance.IsCurrentStep(StepId);
    }

    void Start()
    {
        var interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null)
        {
            interactable.hoverEntered.AddListener(_ => OnHoverEnter());
            interactable.hoverExited.AddListener(_ => OnHoverExit());
            interactable.selectEntered.AddListener(args => { HapticUtility.Pulse(args.interactorObject); OnActivate(); });
        }
    }

    public void OnHoverEnter()
    {
        hovering = true;
        if (!IsCurrentlyInteractable()) return;
        ApplyEmission(Color.white * 2.5f);
        TweenScale(baseScale * hoverScale);
        hint.Show("PULL TRIGGER");
    }

    public void OnHoverExit()
    {
        hovering = false;
        TweenScale(baseScale);
        hint.Hide();
    }

    public void OnActivate()
    {
        if (!IsCurrentlyInteractable()) return;
        hint.Hide();
        StartCoroutine(ActivateRoutine());
    }

    IEnumerator ActivateRoutine()
    {
        if (!LabState.IntroShown)
        {
            LabState.IntroShown = true;
            if (DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "This is the room the journal entry meant.");
        }

        if (played) yield break;
        played = true;
        ApplyEmission(Color.black);

        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
        }

        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("", "The recorder plays back the last few seconds it ever captured: one ragged breath, then nothing. No one made a Day 181 entry, there was no one left to make it.");

        if (DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "Equipment noise. Don't read into it.");
        if (StoryState.Informed && DialogueUI.Instance != null)
            yield return DialogueUI.Instance.ShowAndWaitForInput("{PlayerName}", "That was a person, ECHO. And you know it.");

        InteractionSequencer.Instance?.CompleteStep(StepId);
    }

    void TweenScale(Vector3 target)
    {
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleRoutine(target));
    }

    IEnumerator ScaleRoutine(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < scaleTweenTime)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, target, t / scaleTweenTime);
            yield return null;
        }
        transform.localScale = target;
    }

    void ApplyEmission(Color c)
    {
        if (recorderRenderer == null) return;
        mpb.SetColor(EmissionColor, c);
        recorderRenderer.SetPropertyBlock(mpb);
    }
}
