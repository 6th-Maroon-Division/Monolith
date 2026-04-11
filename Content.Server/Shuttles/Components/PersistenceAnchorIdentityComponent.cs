namespace Content.Server.Shuttles.Components;

/// <summary>
/// Stable GUID identity for a persistent grid.
/// Serialized with the grid snapshot so manifest records can consistently identify
/// the same physical ship across save/restore cycles.
/// </summary>
[RegisterComponent]
public sealed partial class PersistenceAnchorIdentityComponent : Component
{
    [DataField("persistentId")]
    public string? PersistentId;
}
