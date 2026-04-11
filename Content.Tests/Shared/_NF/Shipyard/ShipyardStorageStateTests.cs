using System.Collections.Generic;
using Content.Shared._NF.Shipyard.BUI;
using Content.Shared._NF.Shipyard.Events;
using NUnit.Framework;

namespace Content.Tests.Shared._NF.Shipyard;

[TestFixture]
public sealed class ShipyardStorageStateTests
{
    [Test]
    public void ShipyardConsoleInterfaceState_RetainsStoredShipFields()
    {
        var storedShips = new List<ShipyardStoredShipEntry>
        {
            new("slot-a", "Atlas", 42000, false),
            new("slot-b", "Voucher Dinghy", 0, true),
        };

        var state = new ShipyardConsoleInterfaceState(
            balance: 1337,
            accessGranted: true,
            shipDeedTitle: "Registered Shuttle",
            shipSellValue: 12000,
            isTargetIdPresent: true,
            uiKey: 0,
            shipyardPrototypes: (new List<string>(), new List<string>()),
            shipyardName: "Shipyard",
            freeListings: false,
            sellRate: 0.75f,
            storedShipName: "Atlas",
            storedShipCount: 2,
            storedShips: storedShips,
            canStoreShip: true,
            canRetrieveShip: true);

        Assert.Multiple(() =>
        {
            Assert.That(state.StoredShipName, Is.EqualTo("Atlas"));
            Assert.That(state.StoredShipCount, Is.EqualTo(2));
            Assert.That(state.StoredShips.Count, Is.EqualTo(2));
            Assert.That(state.StoredShips[0].SlotId, Is.EqualTo("slot-a"));
            Assert.That(state.StoredShips[1].PurchasedWithVoucher, Is.True);
            Assert.That(state.CanStoreShip, Is.True);
            Assert.That(state.CanRetrieveShip, Is.True);
        });
    }

    [Test]
    public void ShipyardStorageMessages_RetainSlotSelection()
    {
        var retrieveDefault = new ShipyardConsoleRetrieveMessage();
        var retrieveSpecific = new ShipyardConsoleRetrieveMessage("slot-123");
        var sellStored = new ShipyardConsoleSellStoredMessage("slot-xyz");

        Assert.Multiple(() =>
        {
            Assert.That(retrieveDefault.SlotId, Is.Null);
            Assert.That(retrieveSpecific.SlotId, Is.EqualTo("slot-123"));
            Assert.That(sellStored.SlotId, Is.EqualTo("slot-xyz"));
        });
    }
}
