using Robust.Shared.Serialization;

namespace Content.Shared._NF.Shipyard.BUI;

[Serializable, NetSerializable]
public sealed class ShipyardStoredShipEntry
{
    public readonly string SlotId;
    public readonly string ShipName;
    public readonly int SellValue;
    public readonly bool PurchasedWithVoucher;

    public ShipyardStoredShipEntry(string slotId, string shipName, int sellValue, bool purchasedWithVoucher)
    {
        SlotId = slotId;
        ShipName = shipName;
        SellValue = sellValue;
        PurchasedWithVoucher = purchasedWithVoucher;
    }
}
