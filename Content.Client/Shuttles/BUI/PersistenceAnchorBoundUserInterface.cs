using Content.Client.Shuttles.UI;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Events;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Shuttles.BUI;

[UsedImplicitly]
public sealed class PersistenceAnchorBoundUserInterface : BoundUserInterface
{
    private PersistenceAnchorWindow? _window;

    public PersistenceAnchorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<PersistenceAnchorWindow>();
        _window.ClaimOwnerPressed += OnClaimOwnerPressed;
        _window.UnlockPressed += OnUnlockPressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not PersistenceAnchorBoundUserInterfaceState current || _window == null)
            return;

        _window.UpdateState(
            current.AnchorId,
            current.GridName,
            current.OwnerName,
            current.InsertedIdName,
            current.HasInsertedIdCard,
            current.IsLocked,
            current.CanClaimOwner,
            current.CanUnlock);
    }

    private void OnClaimOwnerPressed()
    {
        SendMessage(new PersistenceAnchorClaimOwnerMessage());
    }

    private void OnUnlockPressed()
    {
        SendMessage(new PersistenceAnchorUnlockGridMessage());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _window?.Close();
        _window = null;
    }
}
