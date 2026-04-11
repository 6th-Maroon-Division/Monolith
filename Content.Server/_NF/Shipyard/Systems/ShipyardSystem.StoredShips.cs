using System.Text.Json;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Server._NF.Shipyard.Systems;

public sealed partial class ShipyardSystem
{
    [Dependency] private readonly IResourceManager _res = default!;

    private const string StoredShipsRootDirectory = "/ShipyardStorage";
    private const string StoredShipsManifestFile = StoredShipsRootDirectory + "/manifest.json";

    private static readonly JsonSerializerOptions StoredShipsJsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    private StoredShipManifest _storedShipManifest = new();

    private void LoadStoredShipManifest()
    {
        EnsureStoredShipDirectories();

        if (!_res.UserData.Exists(new ResPath(StoredShipsManifestFile)))
        {
            _storedShipManifest = new StoredShipManifest();
            return;
        }

        try
        {
            var json = _res.UserData.ReadAllText(new ResPath(StoredShipsManifestFile));
            var parsed = JsonSerializer.Deserialize<StoredShipManifest>(json, StoredShipsJsonOptions);
            if (parsed != null)
            {
                _storedShipManifest = parsed;
            }
            else
            {
                _storedShipManifest = new StoredShipManifest();
            }

            // Legacy migration: older manifests stored one ship per user.
            if (_storedShipManifest.Ships.Count == 0)
            {
                var legacy = JsonSerializer.Deserialize<StoredShipLegacyManifest>(json, StoredShipsJsonOptions);
                if (legacy?.Ships != null && legacy.Ships.Count > 0)
                {
                    foreach (var (userKey, record) in legacy.Ships)
                    {
                        record.SlotId = string.IsNullOrWhiteSpace(record.SlotId) ? Guid.NewGuid().ToString("N") : record.SlotId;
                        _storedShipManifest.Ships[userKey] = new List<StoredShipRecord> { record };
                    }

                    WriteStoredShipManifest();
                }
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"[ShipyardStorage] Failed to read manifest: {e}");
            _storedShipManifest = new StoredShipManifest();
        }
    }

    private void WriteStoredShipManifest()
    {
        EnsureStoredShipDirectories();
        var json = JsonSerializer.Serialize(_storedShipManifest, StoredShipsJsonOptions);
        _res.UserData.WriteAllText(new ResPath(StoredShipsManifestFile), json);
    }

    private void EnsureStoredShipDirectories()
    {
        var root = new ResPath(StoredShipsRootDirectory);
        if (!_res.UserData.IsDir(root))
            _res.UserData.CreateDir(root);
    }

    private static string ToStoredUserKey(Guid userId)
    {
        return userId.ToString("N");
    }

    private static ResPath GetStoredShipSnapshotPath(string userKey, string slotId)
    {
        return new ResPath($"{StoredShipsRootDirectory}/{userKey}_{slotId}.yml");
    }

    private bool TryPeekStoredShip(Guid userId, out StoredShipRecord? record)
    {
        record = null;

        var userKey = ToStoredUserKey(userId);
        if (!_storedShipManifest.Ships.TryGetValue(userKey, out var records) || records.Count == 0)
            return false;

        record = records[^1];
        return true;
    }

    private bool TryGetStoredShipBySlot(Guid userId, string slotId, out StoredShipRecord? record)
    {
        record = null;

        var userKey = ToStoredUserKey(userId);
        if (!_storedShipManifest.Ships.TryGetValue(userKey, out var records) || records.Count == 0)
            return false;

        foreach (var entry in records)
        {
            if (!string.Equals(entry.SlotId, slotId, StringComparison.Ordinal))
                continue;

            record = entry;
            return true;
        }

        return false;
    }

    private void SetStoredShip(Guid userId, StoredShipRecord record)
    {
        var userKey = ToStoredUserKey(userId);
        if (!_storedShipManifest.Ships.TryGetValue(userKey, out var records))
        {
            records = new List<StoredShipRecord>();
            _storedShipManifest.Ships[userKey] = records;
        }

        record.SlotId = string.IsNullOrWhiteSpace(record.SlotId) ? Guid.NewGuid().ToString("N") : record.SlotId;
        records.Add(record);
        WriteStoredShipManifest();
    }

    private bool RemoveStoredShip(Guid userId, string slotId)
    {
        var userKey = ToStoredUserKey(userId);
        if (!_storedShipManifest.Ships.TryGetValue(userKey, out var records) || records.Count == 0)
            return false;

        var removed = records.RemoveAll(x => string.Equals(x.SlotId, slotId, StringComparison.Ordinal)) > 0;
        if (!removed)
            return false;

        if (records.Count == 0)
            _storedShipManifest.Ships.Remove(userKey);

        WriteStoredShipManifest();
        return true;
    }

    private int GetStoredShipCount(Guid userId)
    {
        var userKey = ToStoredUserKey(userId);
        if (!_storedShipManifest.Ships.TryGetValue(userKey, out var records))
            return 0;

        return records.Count;
    }

    private List<StoredShipRecord> GetStoredShips(Guid userId)
    {
        var userKey = ToStoredUserKey(userId);
        if (!_storedShipManifest.Ships.TryGetValue(userKey, out var records))
            return new List<StoredShipRecord>();

        return records
            .OrderByDescending(r => r.StoredAtUtc)
            .ToList();
    }

    private bool TryGetActorUserId(EntityUid actor, out Guid userId)
    {
        userId = default;

        if (!TryComp<ActorComponent>(actor, out var actorComp) || actorComp.PlayerSession == null)
            return false;

        userId = actorComp.PlayerSession.UserId;
        return true;
    }

    [Serializable]
    private sealed class StoredShipManifest
    {
        public Dictionary<string, List<StoredShipRecord>> Ships { get; set; } = new(StringComparer.Ordinal);
    }

    [Serializable]
    private sealed class StoredShipLegacyManifest
    {
        public Dictionary<string, StoredShipRecord> Ships { get; set; } = new(StringComparer.Ordinal);
    }

    [Serializable]
    private sealed class StoredShipRecord
    {
        public string SlotId { get; set; } = string.Empty;
        public string SnapshotPath { get; set; } = string.Empty;
        public string ShipName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public int SellValue { get; set; }
        public bool PurchasedWithVoucher { get; set; }
        public DateTime StoredAtUtc { get; set; }
    }
}
