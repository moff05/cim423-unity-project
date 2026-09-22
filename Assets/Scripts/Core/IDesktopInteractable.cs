public interface IDesktopInteractable
{
    void OnHoverEnter();
    void OnHoverExit();
    void OnActivate();
    // False when it's not this object's turn in the InteractionSequencer -
    // the crosshair should not offer to interact with it yet.
    bool IsCurrentlyInteractable();
}
