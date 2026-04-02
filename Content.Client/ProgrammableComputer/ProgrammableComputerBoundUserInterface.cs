using Content.Client.Computer;
using Content.Shared.ProgrammableComputer;
using JetBrains.Annotations;

namespace Content.Client.ProgrammableComputer;

[UsedImplicitly]
public sealed class ProgrammableComputerBoundUserInterface : ComputerBoundUserInterface<ProgrammableComputerWindow, ProgrammableComputerBoundUserInterfaceState>
{
    public ProgrammableComputerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }
}