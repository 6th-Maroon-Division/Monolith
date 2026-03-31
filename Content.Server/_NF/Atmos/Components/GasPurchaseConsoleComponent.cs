using Content.Shared._NF.Atmos.Systems;
using Robust.Shared.Audio;

namespace Content.Server._NF.Atmos.Components;

[RegisterComponent, Access(typeof(SharedGasDepositSystem))]
public sealed partial class GasPurchaseConsoleComponent : Component
{
    [DataField]
    public int PurchasePointDistance = 8;

    [DataField]
    public float MaxPurchaseMoles = 10000f;

    [DataField]
    public float PurchaseAmount = 100f;

    [DataField]
    public int SelectedGasId;

    [DataField]
    public SoundSpecifier ApproveSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
}