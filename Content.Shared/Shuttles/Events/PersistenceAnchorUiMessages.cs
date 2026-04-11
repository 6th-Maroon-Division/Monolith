using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.Events;

[Serializable, NetSerializable]
public sealed class PersistenceAnchorClaimOwnerMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PersistenceAnchorUnlockGridMessage : BoundUserInterfaceMessage;
