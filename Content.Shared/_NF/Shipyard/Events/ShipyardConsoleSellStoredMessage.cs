using Robust.Shared.Serialization;

namespace Content.Shared._NF.Shipyard.Events;

[Serializable, NetSerializable]
public sealed class ShipyardConsoleSellStoredMessage : BoundUserInterfaceMessage
{
    public readonly string SlotId;

    public ShipyardConsoleSellStoredMessage(string slotId)
    {
        SlotId = slotId;
    }
}
