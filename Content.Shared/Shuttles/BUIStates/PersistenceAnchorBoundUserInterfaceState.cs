using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class PersistenceAnchorBoundUserInterfaceState : BoundUserInterfaceState
{
    public string AnchorId;
    public string GridName;
    public string? OwnerName;
    public string? InsertedIdName;
    public bool HasInsertedIdCard;
    public bool IsLocked;
    public bool CanClaimOwner;
    public bool CanUnlock;

    public PersistenceAnchorBoundUserInterfaceState(
        string anchorId,
        string gridName,
        string? ownerName,
        string? insertedIdName,
        bool hasInsertedIdCard,
        bool isLocked,
        bool canClaimOwner,
        bool canUnlock)
    {
        AnchorId = anchorId;
        GridName = gridName;
        OwnerName = ownerName;
        InsertedIdName = insertedIdName;
        HasInsertedIdCard = hasInsertedIdCard;
        IsLocked = isLocked;
        CanClaimOwner = canClaimOwner;
        CanUnlock = canUnlock;
    }
}

[Serializable, NetSerializable]
public enum PersistenceAnchorUiKey : byte
{
    Key,
}
