using Robust.Shared.Serialization;

namespace Content.Shared._NF.Shipyard.Events;

[Serializable, NetSerializable]
public sealed class ShipyardConsoleStoreMessage : BoundUserInterfaceMessage
{
    public ShipyardConsoleStoreMessage()
    {
    }
}
