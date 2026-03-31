using Robust.Shared.Serialization;

namespace Content.Shared._NF.Atmos.Events;

[Serializable, NetSerializable]
public sealed class GasPurchaseMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class GasPurchaseRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class GasPurchaseSelectGasMessage(int gasId) : BoundUserInterfaceMessage
{
    public int GasId = gasId;
}

[Serializable, NetSerializable]
public sealed class GasPurchaseSetMolesMessage(float moles) : BoundUserInterfaceMessage
{
    public float Moles = moles;
}