using Content.Server.Shuttles.Systems;

namespace Content.Server.Shuttles.Components;

[RegisterComponent]
[Access(typeof(PersistenceAnchorSystem))]
public sealed partial class PersistenceAnchorComponent : Component
{
    public const string OwnerIdCardSlotId = "PersistenceAnchor-ownerId";

    [ViewVariables(VVAccess.ReadWrite)]
    public string? AnchorId { get; set; }

}
