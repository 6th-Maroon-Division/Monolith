using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.ProgrammableComputer;

[RegisterComponent]
public sealed partial class ProgrammableComputerLinkerComponent : Component
{
    /// <summary>
    /// Devices queued by clicking them with the linker, applied when clicking a programmable computer.
    /// </summary>
    [DataField]
    public HashSet<NetEntity> BufferedDevices = new();
}
