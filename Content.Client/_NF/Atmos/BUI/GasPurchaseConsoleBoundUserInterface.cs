using Content.Client._NF.Atmos.UI;
using Content.Shared._NF.Atmos.BUI;
using Content.Shared._NF.Atmos.Events;
using Robust.Client.UserInterface;

namespace Content.Client._NF.Atmos.BUI;

public sealed class GasPurchaseConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private GasPurchaseMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<GasPurchaseMenu>();
        _menu.RefreshRequested += OnRefresh;
        _menu.PurchaseRequested += OnPurchase;
        _menu.GasSelected += OnGasSelected;
        _menu.AmountChanged += OnAmountChanged;
    }

    private void OnRefresh()
    {
        SendMessage(new GasPurchaseRefreshMessage());
    }

    private void OnPurchase()
    {
        SendMessage(new GasPurchaseMessage());
    }

    private void OnGasSelected(int gasId)
    {
        SendMessage(new GasPurchaseSelectGasMessage(gasId));
    }

    private void OnAmountChanged(float moles)
    {
        SendMessage(new GasPurchaseSetMolesMessage(moles));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not GasPurchaseConsoleBoundUserInterfaceState gasState)
            return;

        _menu?.SetState(gasState);
    }
}