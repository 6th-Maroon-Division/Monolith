using System.Linq;
using System.Numerics;
using Content.Server._NF.Atmos.Components;
using Content.Server._NF.Bank;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Audio;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Shared._NF.Atmos.BUI;
using Content.Shared._NF.Atmos.Components;
using Content.Shared._NF.Atmos.Events;
using Content.Shared._NF.Atmos.Prototypes;
using Content.Shared._NF.Atmos.Systems;
using Content.Shared.Popups;
using Content.Shared._NF.Atmos.Visuals;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Power;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._NF.Atmos.Systems;

/// <summary>
/// System for handling gas deposits and machines for extracting from gas deposits
/// </summary>
public sealed class GasDepositSystem : SharedGasDepositSystem
{
    [Dependency] private readonly AmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly BankSystem _bankSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    /// <summary>
    /// The fraction that a deposit's volume should be depleted to before it is considered "low volume".
    /// </summary>
    private const float LowMoleCoefficient = 0.25f;

    /// <summary>
    /// The maximum distance to check for nearby gas sale points when selling gas.
    /// </summary>
    private const double DefaultMaxSalePointDistance = 8.0;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomGasDepositComponent, MapInitEvent>(OnRandomDepositMapInit);

        SubscribeLocalEvent<GasDepositExtractorComponent, MapInitEvent>(OnExtractorMapInit);
        SubscribeLocalEvent<GasDepositExtractorComponent, BoundUIOpenedEvent>(OnExtractorUiOpened);
        SubscribeLocalEvent<GasDepositExtractorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<GasDepositExtractorComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<GasDepositExtractorComponent, AtmosDeviceUpdateEvent>(OnExtractorUpdate);

        SubscribeLocalEvent<GasDepositExtractorComponent, GasPressurePumpChangeOutputPressureMessage>(
            OnOutputPressureChangeMessage);
        SubscribeLocalEvent<GasDepositExtractorComponent, GasPressurePumpToggleStatusMessage>(OnToggleStatusMessage);

        SubscribeLocalEvent<GasSalePointComponent, AtmosDeviceUpdateEvent>(OnSalePointUpdate);
        SubscribeLocalEvent<GasPurchasePointComponent, AtmosDeviceUpdateEvent>(OnPurchasePointUpdate);
        SubscribeLocalEvent<GasPurchasePointComponent, ExaminedEvent>(OnPurchasePointExamined);

        SubscribeLocalEvent<GasSaleConsoleComponent, BoundUIOpenedEvent>(OnConsoleUiOpened);
        SubscribeLocalEvent<GasSaleConsoleComponent, GasSaleSellMessage>(OnConsoleSell);
        SubscribeLocalEvent<GasSaleConsoleComponent, GasSaleRefreshMessage>(OnConsoleRefresh);

        SubscribeLocalEvent<GasPurchaseConsoleComponent, BoundUIOpenedEvent>(OnPurchaseConsoleUiOpened);
        SubscribeLocalEvent<GasPurchaseConsoleComponent, GasPurchaseRefreshMessage>(OnPurchaseConsoleRefresh);
        SubscribeLocalEvent<GasPurchaseConsoleComponent, GasPurchaseSelectGasMessage>(OnPurchaseConsoleSelectGas);
        SubscribeLocalEvent<GasPurchaseConsoleComponent, GasPurchaseSetMolesMessage>(OnPurchaseConsoleSetMoles);
        SubscribeLocalEvent<GasPurchaseConsoleComponent, GasPurchaseMessage>(OnPurchaseConsolePurchase);
    }

    private void OnExtractorMapInit(Entity<GasDepositExtractorComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnExtractorUiOpened(Entity<GasDepositExtractorComponent> ent, ref BoundUIOpenedEvent args)
    {
        Dirty(ent);
    }

    private void OnPowerChanged(Entity<GasDepositExtractorComponent> ent, ref PowerChangedEvent args)
    {
        UpdateAppearance(ent);
    }

    public void OnAnchorChanged(Entity<GasDepositExtractorComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            ent.Comp.DepositEntity = null;
    }

    public void OnRandomDepositMapInit(Entity<RandomGasDepositComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<GasDepositComponent>(ent, out var deposit);
        if (!_prototype.TryIndex(ent.Comp.DepositPrototype, out var depositPrototype))
        {
            if (!_prototype.TryGetRandom<GasDepositPrototype>(_random, out var randomPrototype))
                return;
            depositPrototype = (GasDepositPrototype)randomPrototype;
        }

        // Mono
        var mix = new GasMixture();

        for (var i = 0; i < depositPrototype.Gases.Length && i < Atmospherics.TotalNumberOfGases; i++)
        {
            var gasRange = depositPrototype.Gases[i];
            var gasAmount = gasRange[0] + _random.NextFloat() * (gasRange[1] - gasRange[0]);
            mix.SetMoles(i, gasAmount);
        }

        // Mono
        var moleCount = mix.TotalMoles;
        deposit.Composition = mix;
        deposit.Composition.Multiply(1f / moleCount);
        deposit.Yield = ent.Comp.Scale;
        deposit.MinYield *= MathF.Sqrt(ent.Comp.Scale);
        deposit.GasLeft = moleCount * ent.Comp.Scale;
        deposit.LowMoles = moleCount * LowMoleCoefficient;
    }

    // Mono - changes throughout the method
    private void OnExtractorUpdate(Entity<GasDepositExtractorComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!ent.Comp.Enabled
            || !TryComp(ent.Comp.DepositEntity, out GasDepositComponent? depositComp)
            || TryComp<ApcPowerReceiverComponent>(ent, out var power) && !power.Powered
            || !_nodeContainer.TryGetNode(ent.Owner, ent.Comp.PortName, out PipeNode? port))
        {
            _ambientSound.SetAmbience(ent, false);
            SetDepositState(ent, GasDepositExtractorState.Off);
            return;
        }

        if (!depositComp.YieldBased && depositComp.GasLeft < Atmospherics.GasMinMoles)
        {
            _ambientSound.SetAmbience(ent, false);
            SetDepositState(ent, GasDepositExtractorState.Empty);
            return;
        }

        // Nowhere to pipe gas, say it's blocked.
        if (port.NodeGroup is not PipeNet { NodeCount: > 1 } net)
        {
            _ambientSound.SetAmbience(ent, false);
            SetDepositState(ent, GasDepositExtractorState.Blocked);
            return;
        }

        var extractionRate = ent.Comp.ExtractionRate;
        if (depositComp.YieldBased)
            extractionRate *= depositComp.Yield;

        var targetPressure = float.Clamp(ent.Comp.TargetPressure, 0, ent.Comp.MaxTargetPressure);

        // How many moles could we theoretically spawn. Cap by pressure, amount, and extractor limit.
        var allowableMoles = (targetPressure - net.Air.Pressure) * net.Air.Volume /
                             (depositComp.OutputTemperature * Atmospherics.R);
        allowableMoles = float.Min(allowableMoles, extractionRate * args.dt);

        if (allowableMoles < Atmospherics.GasMinMoles)
        {
            _ambientSound.SetAmbience(ent, false);
            SetDepositState(ent, GasDepositExtractorState.Blocked);
            return;
        }

        var extracted = depositComp.Composition.Clone();
        extracted.Multiply(allowableMoles);
        extracted.Temperature = depositComp.OutputTemperature;

        if (!depositComp.YieldBased)
            depositComp.GasLeft -= allowableMoles;
        else
            depositComp.Yield = MathF.Max(depositComp.MinYield, depositComp.Yield - depositComp.YieldDrop * ent.Comp.ExtractionRate * args.dt);

        _atmosphere.Merge(net.Air, extracted);

        _ambientSound.SetAmbience(ent, true);

        var isLow = depositComp.YieldBased ? depositComp.Yield == depositComp.MinYield : depositComp.GasLeft <= depositComp.LowMoles;
        if (isLow)
            SetDepositState(ent, GasDepositExtractorState.Low);
        else
            SetDepositState(ent, GasDepositExtractorState.On);
    }

    private void OnToggleStatusMessage(Entity<GasDepositExtractorComponent> ent,
        ref GasPressurePumpToggleStatusMessage args)
    {
        ent.Comp.Enabled = args.Enabled;
        _adminLog.Add(LogType.AtmosPowerChanged,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} set the power on {ToPrettyString(ent):device} to {args.Enabled}");
        Dirty(ent);
    }

    private void OnOutputPressureChangeMessage(Entity<GasDepositExtractorComponent> ent,
        ref GasPressurePumpChangeOutputPressureMessage args)
    {
        ent.Comp.TargetPressure = Math.Clamp(args.Pressure, 0f, Atmospherics.MaxOutputPressure);
        _adminLog.Add(LogType.AtmosPressureChanged,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} set the pressure on {ToPrettyString(ent):device} to {args.Pressure}kPa");
        Dirty(ent);
    }

    private void SetDepositState(Entity<GasDepositExtractorComponent> ent, GasDepositExtractorState newState)
    {
        if (newState != ent.Comp.LastState)
        {
            ent.Comp.LastState = newState;
            UpdateAppearance(ent);
        }
    }

    private void UpdateAppearance(Entity<GasDepositExtractorComponent> ent, AppearanceComponent? appearance = null)
    {
        if (!Resolve(ent, ref appearance, false))
            return;

        var pumpOn = ent.Comp.Enabled && (!TryComp<ApcPowerReceiverComponent>(ent, out var power) || power.Powered);
        if (!pumpOn)
            _appearance.SetData(ent, GasDepositExtractorVisuals.State, GasDepositExtractorState.Off, appearance);
        else
            _appearance.SetData(ent, GasDepositExtractorVisuals.State, ent.Comp.LastState, appearance);
    }

    // Atmos update: take any gas from the connecting network and push it into the pump.
    private void OnSalePointUpdate(Entity<GasSalePointComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (TryComp<ApcPowerReceiverComponent>(ent, out var power) && !power.Powered
            || !_nodeContainer.TryGetNode(ent.Owner, ent.Comp.InletPipePortName, out PipeNode? port)
            || port.NodeGroup is not PipeNet { NodeCount: > 1 } net)
            return;

        if (net.Air.TotalMoles > 0)
        {
            _atmosphere.Merge(ent.Comp.GasStorage, net.Air);
            net.Air.Clear();
        }
    }

    private void OnConsoleUiOpened(Entity<GasSaleConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateConsoleInterface(ent);
    }

    private void OnPurchaseConsoleUiOpened(Entity<GasPurchaseConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdatePurchaseConsoleInterface(ent);
    }

    private void OnConsoleRefresh(Entity<GasSaleConsoleComponent> ent, ref GasSaleRefreshMessage args)
    {
        UpdateConsoleInterface(ent);
    }

    private void OnPurchaseConsoleRefresh(Entity<GasPurchaseConsoleComponent> ent, ref GasPurchaseRefreshMessage args)
    {
        UpdatePurchaseConsoleInterface(ent);
    }

    private void OnPurchaseConsoleSelectGas(Entity<GasPurchaseConsoleComponent> ent, ref GasPurchaseSelectGasMessage args)
    {
        if (args.GasId < 0 || args.GasId >= Atmospherics.TotalNumberOfGases)
            return;

        ent.Comp.SelectedGasId = args.GasId;
        UpdatePurchaseConsoleInterface(ent);
    }

    private void OnPurchaseConsoleSetMoles(Entity<GasPurchaseConsoleComponent> ent, ref GasPurchaseSetMolesMessage args)
    {
        ent.Comp.PurchaseAmount = float.Clamp(args.Moles, 0f, ent.Comp.MaxPurchaseMoles);
        UpdatePurchaseConsoleInterface(ent);
    }

    private void OnPurchaseConsolePurchase(Entity<GasPurchaseConsoleComponent> ent, ref GasPurchaseMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid)
        {
            UpdatePurchaseConsoleInterface(ent);
            return;
        }

        var points = GetNearbyPurchasePoints(ent, gridUid);
        if (points.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("gas-purchase-console-no-points"), ent, actor);
            _audio.PlayPvs(ent.Comp.ErrorSound, ent);
            UpdatePurchaseConsoleInterface(ent);
            return;
        }

        if (!TryGetSanitizedPurchaseSelection(ent, out var gasId, out var moles, out var price))
        {
            _popup.PopupEntity(Loc.GetString("gas-purchase-console-invalid-selection"), ent, actor);
            _audio.PlayPvs(ent.Comp.ErrorSound, ent);
            UpdatePurchaseConsoleInterface(ent);
            return;
        }

        if (!_bankSystem.TryBankWithdraw(actor, price))
        {
            _popup.PopupEntity(Loc.GetString("gas-purchase-console-insufficient-funds"), ent, actor);
            _audio.PlayPvs(ent.Comp.ErrorSound, ent);
            UpdatePurchaseConsoleInterface(ent);
            return;
        }

        var mixture = new GasMixture();
        mixture.SetMoles(gasId, moles);
        mixture.Temperature = 293.15f;
        _atmosphere.Merge(points[0].Comp.GasStorage, mixture);

        _audio.PlayPvs(ent.Comp.ApproveSound, ent);
        UpdatePurchaseConsoleInterface(ent);
    }

    private void OnConsoleSell(Entity<GasSaleConsoleComponent> ent, ref GasSaleSellMessage args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid)
        {
            UI.SetUiState(ent.Owner,
                GasSaleConsoleUiKey.Key,
                new GasSaleConsoleBoundUserInterfaceState(0, new GasMixture(), false));
            return;
        }

        var mixture = new GasMixture();
        foreach (var salePoint in GetNearbySalePoints(ent, gridUid))
        {
            _atmosphere.Merge(mixture, salePoint.Comp.GasStorage);
            salePoint.Comp.GasStorage.Clear();
        }

        var amount = _atmosphere.GetPriceNoPurity(mixture); // Mono - No purity penalty
        if (TryComp<MarketModifierComponent>(ent, out var priceMod))
            amount *= priceMod.Mod;

        var stackPrototype = _prototype.Index(ent.Comp.CashType);
        _stack.Spawn((int)amount, stackPrototype, xform.Coordinates);
        _audio.PlayPvs(ent.Comp.ApproveSound, ent);
        UI.SetUiState(ent.Owner,
            GasSaleConsoleUiKey.Key,
            new GasSaleConsoleBoundUserInterfaceState(0, new GasMixture(), false));
    }

    private void UpdateConsoleInterface(Entity<GasSaleConsoleComponent> ent)
    {
        if (Transform(ent).GridUid is not { } gridUid)
        {
            UI.SetUiState(ent.Owner,
                GasSaleConsoleUiKey.Key,
                new GasSaleConsoleBoundUserInterfaceState(0, new GasMixture(), false));
            return;
        }

        GetNearbyMixtures(ent, gridUid, out var mixture, out var amount);
        if (TryComp<MarketModifierComponent>(ent, out var priceMod))
            amount *= priceMod.Mod;

        UI.SetUiState(ent.Owner,
            GasSaleConsoleUiKey.Key,
            new GasSaleConsoleBoundUserInterfaceState((int)amount, mixture, mixture.TotalMoles > 0));
    }

    private void UpdatePurchaseConsoleInterface(Entity<GasPurchaseConsoleComponent> ent)
    {
        if (Transform(ent).GridUid is not { } gridUid)
        {
            UI.SetUiState(ent.Owner,
                GasPurchaseConsoleUiKey.Key,
                new GasPurchaseConsoleBoundUserInterfaceState([], 0, ent.Comp.PurchaseAmount, 0, 0, false));
            return;
        }

        var availableGasIds = Enumerable.Range(0, Atmospherics.TotalNumberOfGases).ToArray();
        if (availableGasIds.Length == 0)
        {
            UI.SetUiState(ent.Owner,
                GasPurchaseConsoleUiKey.Key,
                new GasPurchaseConsoleBoundUserInterfaceState([], 0, ent.Comp.PurchaseAmount, 0, 0, false));
            return;
        }

        if (!availableGasIds.Contains(ent.Comp.SelectedGasId))
            ent.Comp.SelectedGasId = availableGasIds[0];

        ent.Comp.PurchaseAmount = float.Clamp(ent.Comp.PurchaseAmount, 0f, ent.Comp.MaxPurchaseMoles);

        var linkedPoints = GetNearbyPurchasePoints(ent, gridUid).Count;
        var canPurchase = TryGetSanitizedPurchaseSelection(ent, out var gasId, out _, out var price) && linkedPoints > 0;

        UI.SetUiState(ent.Owner,
            GasPurchaseConsoleUiKey.Key,
            new GasPurchaseConsoleBoundUserInterfaceState(
                availableGasIds,
                gasId,
                ent.Comp.PurchaseAmount,
                price,
                linkedPoints,
                canPurchase));
    }

    private void GetNearbyMixtures(EntityUid consoleUid, EntityUid gridUid, out GasMixture mixture, out double value)
    {
        mixture = new GasMixture();

        foreach (var salePoint in GetNearbySalePoints(consoleUid, gridUid))
        {
            _atmosphere.Merge(mixture, salePoint.Comp.GasStorage);
        }

        value = _atmosphere.GetPriceNoPurity(mixture); // Mono - No purity penalty
    }

    private bool TryGetSanitizedPurchaseSelection(
        Entity<GasPurchaseConsoleComponent> ent,
        out int gasId,
        out float moles,
        out int price)
    {
        gasId = int.Clamp(ent.Comp.SelectedGasId, 0, Atmospherics.TotalNumberOfGases - 1);
        moles = float.Clamp(ent.Comp.PurchaseAmount, 0f, ent.Comp.MaxPurchaseMoles);
        price = 0;

        if (moles <= 0f)
            return false;

        var perMole = _atmosphere.GetGas(gasId).PricePerMole;
        var total = moles * perMole;
        if (TryComp<MarketModifierComponent>(ent, out var priceMod))
            total *= priceMod.Mod;

        price = (int) Math.Ceiling(total);
        return price > 0;
    }

    private List<Entity<GasSalePointComponent>> GetNearbySalePoints(EntityUid consoleUid, EntityUid gridUid)
    {
        List<Entity<GasSalePointComponent>> ret = new();

        var query = AllEntityQuery<GasSalePointComponent, TransformComponent>();

        var consolePosition = Transform(consoleUid).Coordinates.Position;
        var maxSalePointDistance = DefaultMaxSalePointDistance;

        // Get the mapped checking distance from the console
        if (TryComp<GasSaleConsoleComponent>(consoleUid, out var cargoShuttleComponent))
            maxSalePointDistance = cargoShuttleComponent.SellPointDistance;

        while (query.MoveNext(out var uid, out var comp, out var compXform))
        {
            if (compXform.ParentUid != gridUid
                || !compXform.Anchored
                || Vector2.Distance(consolePosition, compXform.Coordinates.Position) > maxSalePointDistance)
                continue;

            ret.Add((uid, comp));
        }

        return ret;
    }

    private List<Entity<GasPurchasePointComponent>> GetNearbyPurchasePoints(EntityUid consoleUid, EntityUid gridUid)
    {
        var ret = new List<(Entity<GasPurchasePointComponent> Point, float Distance)>();

        var query = AllEntityQuery<GasPurchasePointComponent, TransformComponent>();
        var consolePosition = Transform(consoleUid).Coordinates.Position;
        var maxPurchasePointDistance = DefaultMaxSalePointDistance;

        if (TryComp<GasPurchaseConsoleComponent>(consoleUid, out var purchaseConsole))
            maxPurchasePointDistance = purchaseConsole.PurchasePointDistance;

        while (query.MoveNext(out var uid, out var comp, out var compXform))
        {
            if (compXform.ParentUid != gridUid || !compXform.Anchored)
                continue;

            var distance = Vector2.Distance(consolePosition, compXform.Coordinates.Position);
            if (distance > maxPurchasePointDistance)
                continue;

            ret.Add(((uid, comp), distance));
        }

        return ret.OrderBy(point => point.Distance).Select(point => point.Point).ToList();
    }

    private void OnPurchasePointUpdate(Entity<GasPurchasePointComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (ent.Comp.GasStorage.TotalMoles <= 0f
            || TryComp<ApcPowerReceiverComponent>(ent, out var power) && !power.Powered
            || !_nodeContainer.TryGetNode(ent.Owner, ent.Comp.OutletPipePortName, out PipeNode? port)
            || port.NodeGroup is not PipeNet { NodeCount: > 1 } net)
        {
            return;
        }

        _atmosphere.Merge(net.Air, ent.Comp.GasStorage);
        ent.Comp.GasStorage.Clear();
    }

    private void OnPurchasePointExamined(Entity<GasPurchasePointComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var storedMoles = ent.Comp.GasStorage.TotalMoles;
        args.PushMarkup(Loc.GetString("gas-purchase-point-examine-stored",
            ("value", storedMoles)));

        var status = GetPurchasePointStatus(ent);
        args.PushMarkup(Loc.GetString(status));
    }

    private string GetPurchasePointStatus(Entity<GasPurchasePointComponent> ent)
    {
        if (TryComp<ApcPowerReceiverComponent>(ent, out var power) && !power.Powered)
            return "gas-purchase-point-examine-unpowered";

        if (!_nodeContainer.TryGetNode(ent.Owner, ent.Comp.OutletPipePortName, out PipeNode? port))
            return "gas-purchase-point-examine-no-node";

        if (port.NodeGroup is not PipeNet { NodeCount: > 1 })
            return "gas-purchase-point-examine-no-pipe-net";

        if (ent.Comp.GasStorage.TotalMoles <= 0f)
            return "gas-purchase-point-examine-empty";

        return "gas-purchase-point-examine-ready";
    }
}
