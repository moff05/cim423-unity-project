using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartScreenController : MonoBehaviour
{
    public CanvasGroup startCanvasGroup;
    public TMP_InputField callSignInput;
    public Button beginButton;
    public IntroSequence introSequence;

    void Awake()
    {
        if (beginButton != null) beginButton.onClick.AddListener(OnBeginPressed);
    }

    void OnBeginPressed()
    {
        string enteredName = callSignInput != null ? callSignInput.text : null;
        GameManager.Instance?.SetPlayerName(enteredName);

        if (startCanvasGroup != null)
        {
            startCanvasGroup.alpha = 0f;
            startCanvasGroup.blocksRaycasts = false;
            startCanvasGroup.interactable = false;
        }

        introSequence?.Play();
    }
}
