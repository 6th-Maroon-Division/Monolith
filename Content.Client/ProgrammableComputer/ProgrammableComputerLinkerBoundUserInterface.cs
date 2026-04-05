using Content.Shared.ProgrammableComputer;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.ProgrammableComputer;

public sealed class ProgrammableComputerLinkerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ProgrammableComputerLinkerWindow? _window;

    public ProgrammableComputerLinkerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<ProgrammableComputerLinkerWindow>();
        _window.OnClearAll += () => SendMessage(new ProgrammableComputerLinkerClearAllMessage());
        _window.OnRemoveOne += target => SendMessage(new ProgrammableComputerLinkerRemoveBufferedMessage(target));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not ProgrammableComputerLinkerUiState cast)
            return;

        _window.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
