using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles;

/// <summary>
/// Appearance data key used by <c>PersistenceAnchorSystem</c> to drive the
/// visual state of a persistence anchor entity.
/// </summary>
[Serializable, NetSerializable]
public enum PersistenceAnchorVisuals : byte
{
    State,
}

/// <summary>Visual states for a persistence anchor.</summary>
[Serializable, NetSerializable]
public enum PersistenceAnchorState : byte
{
    /// <summary>No active tracking – anchor is unregistered or archived.</summary>
    Inactive,

    /// <summary>Tracking active and the last snapshot is up-to-date.</summary>
    Clean,

    /// <summary>Tracking active but unsaved changes are queued.</summary>
    Dirty,
}
