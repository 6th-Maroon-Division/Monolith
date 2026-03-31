using Content.Shared._NF.Atmos.Systems;
using Content.Shared.Atmos;

namespace Content.Server._NF.Atmos.Components;

[RegisterComponent, Access(typeof(SharedGasDepositSystem))]
public sealed partial class GasPurchasePointComponent : Component
{
    [DataField]
    public string OutletPipePortName = "outlet";

    [ViewVariables]
    public GasMixture GasStorage = new();
}