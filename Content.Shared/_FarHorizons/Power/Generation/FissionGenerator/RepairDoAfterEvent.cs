using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Power.Generation.FissionGenerator;

[Serializable]
[NetSerializable]
public sealed partial class RepairDoAfterEvent : SimpleDoAfterEvent;
