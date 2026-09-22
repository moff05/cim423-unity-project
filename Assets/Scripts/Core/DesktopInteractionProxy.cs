using UnityEngine;
using UnityEngine.InputSystem;

// Interim, headset-free way to test the XR Simple Interactables: aims from the
// screen center (matches FreeLookWalker's locked crosshair cursor) and treats
// left-click as the XR "activate" action. Each interactable's real logic lives
// behind IDesktopInteractable so the same code path fires later from a real
// XRSimpleInteractable's hoverEntered/selectEntered events once a headset is wired up.
public class DesktopInteractionProxy : MonoBehaviour
{
    public Camera cam;
    public float maxDistance = 5f;

    IDesktopInteractable hovered;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (cam == null) return;

        IDesktopInteractable next = null;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            var candidate = hit.collider.GetComponentInParent<IDesktopInteractable>();
            // Not this object's turn yet - don't offer to interact with it,
            // and don't let it eat the hover state either.
            if (candidate != null && candidate.IsCurrentlyInteractable()) next = candidate;
        }

        if (next != hovered)
        {
            hovered?.OnHoverExit();
            hovered = next;
            hovered?.OnHoverEnter();
            CrosshairUI.Instance?.SetHovering(hovered != null);
        }

        var mouse = Mouse.current;
        if (hovered != null && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            hovered.OnActivate();
        }
    }
}
