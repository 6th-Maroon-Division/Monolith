using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.ProgrammableComputer;

/// <summary>
///     Stores the named atmos-device links for a programmable computer.
///     Links survive map save/load via <see cref="NetEntity"/> serialization.
/// </summary>
[RegisterComponent]
public sealed partial class ProgrammableComputerAtmosLinksComponent : Component
{
    /// <summary>
    ///     User-assigned label -> target entity.
    ///     Labels are trimmed and capped at 32 characters by the server.
    /// </summary>
    [DataField]
    public Dictionary<string, NetEntity> Links { get; set; } = new();

    /// <summary>Transient scan results, never serialized.</summary>
    [ViewVariables]
    public List<EntityUid>? PendingScanResults;
}
