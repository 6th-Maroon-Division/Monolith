using Robust.Shared.Serialization;

namespace Content.Shared._NF.Atmos.BUI;

[NetSerializable, Serializable]
public sealed class GasPurchaseConsoleBoundUserInterfaceState(
    int[] availableGasIds,
    int selectedGasId,
    float moles,
    int price,
    int linkedPoints,
    bool canPurchase) : BoundUserInterfaceState
{
    public int[] AvailableGasIds = availableGasIds;
    public int SelectedGasId = selectedGasId;
    public float Moles = moles;
    public int Price = price;
    public int LinkedPoints = linkedPoints;
    public bool CanPurchase = canPurchase;
}

[Serializable, NetSerializable]
public enum GasPurchaseConsoleUiKey : byte
{
    Key,
}