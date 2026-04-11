using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared._NF.Shipyard.BUI;

[NetSerializable, Serializable]
public sealed class ShipyardConsoleInterfaceState : BoundUserInterfaceState
{
    public int Balance;
    public readonly bool AccessGranted;
    public readonly string? ShipDeedTitle;
    public int ShipSellValue;
    public readonly bool IsTargetIdPresent;
    public readonly byte UiKey;

    public readonly (List<string> available, List<string> unavailable) ShipyardPrototypes;
    public readonly string ShipyardName;
    public readonly bool FreeListings;
    public readonly float SellRate;
    public readonly string? StoredShipName;
    public readonly int StoredShipCount;
    public readonly List<ShipyardStoredShipEntry> StoredShips;
    public readonly bool CanStoreShip;
    public readonly bool CanRetrieveShip;

    public ShipyardConsoleInterfaceState(
        int balance,
        bool accessGranted,
        string? shipDeedTitle,
        int shipSellValue,
        bool isTargetIdPresent,
        byte uiKey,
        (List<string> available, List<string> unavailable) shipyardPrototypes,
        string shipyardName,
        bool freeListings,
        float sellRate,
        string? storedShipName,
        int storedShipCount,
        List<ShipyardStoredShipEntry> storedShips,
        bool canStoreShip,
        bool canRetrieveShip)
    {
        Balance = balance;
        AccessGranted = accessGranted;
        ShipDeedTitle = shipDeedTitle;
        ShipSellValue = shipSellValue;
        IsTargetIdPresent = isTargetIdPresent;
        UiKey = uiKey;
        ShipyardPrototypes = shipyardPrototypes;
        ShipyardName = shipyardName;
        FreeListings = freeListings;
        SellRate = sellRate;
        StoredShipName = storedShipName;
        StoredShipCount = storedShipCount;
        StoredShips = storedShips;
        CanStoreShip = canStoreShip;
        CanRetrieveShip = canRetrieveShip;
    }
}
