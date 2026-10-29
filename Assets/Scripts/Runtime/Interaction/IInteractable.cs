public interface IInteractable
{
    bool CanInteract
    {
        get;
    }
    void TryInteract();
}
