using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// An optional crew datapad. Not part of the required sequence: it glows blue
// (required objects glow gold) and can be read at any time. Each one is a
// different crew member, and together they contradict what ECHO says.
public class CrewLog : MonoBehaviour, IDesktopInteractable
{
    public string logId;
    public string header = "CREW LOG";
    [TextArea(3, 8)] public string[] pages;
    public Renderer screenRenderer;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly Color LogBlue = new Color(0.2f, 0.75f, 1f);
    MaterialPropertyBlock mpb;
    FloatingHintText hint;
    bool hovering;
    bool busy;
    bool read;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        hint = FloatingHintText.Attach(transform, 0.3f);
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

    void OnDestroy()
    {
        if (hint != null) Destroy(hint.gameObject);
    }

    void Update()
    {
        if (screenRenderer == null) return;
        Color c;
        if (hovering && IsCurrentlyInteractable()) c = Color.white * 2.2f;
        else if (read) c = LogBlue * 0.35f;
        else c = LogBlue * (0.5f + 1.6f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f)));
        mpb.SetColor(EmissionColor, c);
        screenRenderer.SetPropertyBlock(mpb);
    }

    public bool IsCurrentlyInteractable()
    {
        if (busy || StoryState.EndingStarted) return false;
        var gm = GameManager.Instance;
        return gm == null || gm.StoryStarted;
    }

    public void OnHoverEnter()
    {
        hovering = true;
        if (!IsCurrentlyInteractable()) return;
        hint.Show(read ? "CREW LOG (READ AGAIN)" : "CREW LOG: PULL TRIGGER");
    }

    public void OnHoverExit()
    {
        hovering = false;
        hint.Hide();
    }

    public void OnActivate()
    {
        if (!IsCurrentlyInteractable()) return;
        hint.Hide();
        StartCoroutine(ReadRoutine());
    }

    IEnumerator ReadRoutine()
    {
        busy = true;
        V2Audio.PlayAt("LogBlip", transform.position, 0.8f, 12f);
        bool first = StoryState.MarkLog(logId);
        read = true;
        if (DialogueUI.Instance != null && pages != null)
            foreach (var page in pages)
                yield return DialogueUI.Instance.ShowAndWaitForInput(header, page);
        if (first)
        {
            AmbientCaption.Say("", "Crew log recovered  " + StoryState.LogsFound + "/" + StoryState.TotalLogs, 3f);
            // The step timer restarts so ECHO does not nag someone who is
            // busy reading.
            InteractionSequencer.Instance?.ResetStepTimer();
            if (StoryState.LogsFound == StoryState.InformedThreshold && DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("{PlayerName}", "ECHO. These logs don't sound like an evacuation.");
            if (StoryState.LogsFound == StoryState.InformedThreshold && DialogueUI.Instance != null)
                yield return DialogueUI.Instance.ShowAndWaitForInput("ECHO", "People write strange things when they're tired, {PlayerName}. Keep moving.");
        }
        busy = false;
    }
}
