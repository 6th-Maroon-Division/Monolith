using Robust.Shared.Serialization;

namespace Content.Shared._NF.Shipyard.Events;

[Serializable, NetSerializable]
public sealed class ShipyardConsoleRetrieveMessage : BoundUserInterfaceMessage
{
    public readonly string? SlotId;

    public ShipyardConsoleRetrieveMessage(string? slotId = null)
    {
        SlotId = slotId;
    }
}
