using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Piping.Binary.Components;
using Content.Server.Atmos.Piping.Binary.EntitySystems;
using Content.Server.Atmos.Piping.Trinary.Components;
using Content.Server.Atmos.Piping.Trinary.EntitySystems;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Atmos.Piping.Unary.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.ProgrammableComputer;
using MoonSharp.Interpreter;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.ProgrammableComputer;

public sealed partial class ProgrammableComputerSystem
{
    private const float AtmosScanRange = 8f;
    private const int MaxAtmosLinks = 32;
    private const int MaxLabelLength = 32;

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly GasPressurePumpSystem _pressurePump = default!;
    [Dependency] private readonly GasVolumePumpSystem _volumePump = default!;
    [Dependency] private readonly GasValveSystem _gasValve = default!;
    [Dependency] private readonly GasVentScrubberSystem _scrubber = default!;
    [Dependency] private readonly GasOutletInjectorSystem _injector = default!;
    [Dependency] private readonly GasMixerSystem _mixer = default!;

    // ─── Initialize (extend) ────────────────────────────────────────────────

    private void InitializeAtmos()
    {
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerAtmosScanMessage>(OnAtmosScan);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerAtmosLinkMessage>(OnAtmosLink);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerAtmosUnlinkMessage>(OnAtmosUnlink);
        SubscribeLocalEvent<ProgrammableComputerComponent, ProgrammableComputerAtmosRenameMessage>(OnAtmosRename);
    }

    // ─── BUI handlers ───────────────────────────────────────────────────────

    private void OnAtmosScan(EntityUid uid, ProgrammableComputerComponent comp, ProgrammableComputerAtmosScanMessage args)
    {
        var links = EnsureComp<ProgrammableComputerAtmosLinksComponent>(uid);
        var coords = Transform(uid).Coordinates;

        var sensorSet = new HashSet<Entity<AtmosMonitorComponent>>();
        var ppSet     = new HashSet<Entity<GasPressurePumpComponent>>();
        var vpSet     = new HashSet<Entity<GasVolumePumpComponent>>();
        var valveSet  = new HashSet<Entity<GasValveComponent>>();
        var scrubSet  = new HashSet<Entity<GasVentScrubberComponent>>();
        var filterSet = new HashSet<Entity<GasFilterComponent>>();
        var ventSet   = new HashSet<Entity<GasVentPumpComponent>>();
        var injectSet = new HashSet<Entity<GasOutletInjectorComponent>>();
        var mixerSet  = new HashSet<Entity<GasMixerComponent>>();
        var regSet    = new HashSet<Entity<GasPressureRegulatorComponent>>();

        _lookup.GetEntitiesInRange(coords, AtmosScanRange, sensorSet);
        _lookup.GetEntitiesInRange(coords, AtmosScanRange, ppSet);
        _lookup.GetEntitiesInRange(coords, AtmosScanRange, vpSet);
        _lookup.GetEntitiesInRange(coords, AtmosScanRange, valveSet);
        _lookup.GetEntitiesInRange(coords, AtmosScanRange, scrubSet);
        _lookup.GetEntitiesInRange(coords, AtmosScanRange, filterSet);
        _lookup.GetEntitiesInRange(coords, AtmosScanRange, ventSet);
        _lookup.GetEntitiesInRange(coords, AtmosScanRange, injectSet);
        _lookup.GetEntitiesInRange(coords, AtmosScanRange, mixerSet);
        _lookup.GetEntitiesInRange(coords, AtmosScanRange, regSet);

        var found = new HashSet<EntityUid>();
        foreach (var e in sensorSet) found.Add(e.Owner);
        foreach (var e in ppSet)     found.Add(e.Owner);
        foreach (var e in vpSet)     found.Add(e.Owner);
        foreach (var e in valveSet)  found.Add(e.Owner);
        foreach (var e in scrubSet)  found.Add(e.Owner);
        foreach (var e in filterSet) found.Add(e.Owner);
        foreach (var e in ventSet)   found.Add(e.Owner);
        foreach (var e in injectSet) found.Add(e.Owner);
        foreach (var e in mixerSet)  found.Add(e.Owner);
        foreach (var e in regSet)    found.Add(e.Owner);
        found.Remove(uid);

        links.PendingScanResults = new List<EntityUid>(found);
        UpdateUi(uid, comp);
    }

    private void OnAtmosLink(EntityUid uid, ProgrammableComputerComponent comp, ProgrammableComputerAtmosLinkMessage args)
    {
        var links = EnsureComp<ProgrammableComputerAtmosLinksComponent>(uid);
        if (links.Links.Count >= MaxAtmosLinks)
            return;

        var label = args.Label.Trim();
        if (label.Length == 0 || label.Length > MaxLabelLength)
            return;

        var target = GetEntity(args.Target);
        if (!Exists(target))
            return;

        // Must still be in range
        if (!InAtmosRange(uid, target))
            return;

        links.Links[label] = args.Target;
        links.PendingScanResults = null;
        UpdateUi(uid, comp);
    }

    private void OnAtmosUnlink(EntityUid uid, ProgrammableComputerComponent comp, ProgrammableComputerAtmosUnlinkMessage args)
    {
        if (!TryComp<ProgrammableComputerAtmosLinksComponent>(uid, out var links))
            return;

        links.Links.Remove(args.Label);
        UpdateUi(uid, comp);
    }

    private void OnAtmosRename(EntityUid uid, ProgrammableComputerComponent comp, ProgrammableComputerAtmosRenameMessage args)
    {
        if (!TryComp<ProgrammableComputerAtmosLinksComponent>(uid, out var links))
            return;

        var newLabel = args.NewLabel.Trim();
        if (newLabel.Length == 0 || newLabel.Length > MaxLabelLength)
            return;

        if (!links.Links.TryGetValue(args.OldLabel, out var target))
            return;

        links.Links.Remove(args.OldLabel);
        links.Links[newLabel] = target;
        UpdateUi(uid, comp);
    }

    // ─── UI state helpers ───────────────────────────────────────────────────

    private AtmosLinkEntry[] BuildAtmosLinkEntries(EntityUid uid)
    {
        if (!TryComp<ProgrammableComputerAtmosLinksComponent>(uid, out var links))
            return [];

        var entries = new List<AtmosLinkEntry>(links.Links.Count);
        foreach (var (label, netEnt) in links.Links)
        {
            var target = GetEntity(netEnt);
            var devType = GetAtmosDeviceType(target);
            var address = GetAtmosAddress(target);
            var name = ToPrettyString(target);
            entries.Add(new AtmosLinkEntry(label, devType, address, name));
        }
        return entries.ToArray();
    }

    private AtmosNearbyEntry[]? BuildAtmosNearbyEntries(EntityUid uid)
    {
        if (!TryComp<ProgrammableComputerAtmosLinksComponent>(uid, out var links) || links.PendingScanResults == null)
            return null;

        var existing = new HashSet<NetEntity>(links.Links.Values);
        var entries = new List<AtmosNearbyEntry>();
        foreach (var target in links.PendingScanResults)
        {
            if (!Exists(target))
                continue;
            var netEnt = GetNetEntity(target);
            if (existing.Contains(netEnt))
                continue;
            var devType = GetAtmosDeviceType(target);
            var address = GetAtmosAddress(target);
            var name = ToPrettyString(target);
            entries.Add(new AtmosNearbyEntry(netEnt, name, devType, address));
        }
        return entries.ToArray();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private bool InAtmosRange(EntityUid computer, EntityUid target)
    {
        var computerCoords = Transform(computer).Coordinates;
        var targetCoords = Transform(target).Coordinates;
        return computerCoords.TryDistance(EntityManager, _xform, targetCoords, out var dist) && dist <= AtmosScanRange;
    }

    public bool IsAtmosLinkableDevice(EntityUid uid)
    {
        return GetAtmosDeviceType(uid) != "unknown";
    }

    public string GetAtmosDeviceTypeForUi(EntityUid uid)
    {
        return GetAtmosDeviceType(uid);
    }

    public string GetAtmosAddressForUi(EntityUid uid)
    {
        return GetAtmosAddress(uid);
    }

    public bool TryAddAtmosLinkedDevice(EntityUid computerUid, EntityUid targetUid, out string assignedLabel)
    {
        assignedLabel = string.Empty;

        if (!Exists(computerUid) || !Exists(targetUid) || !IsAtmosLinkableDevice(targetUid))
            return false;

        var links = EnsureComp<ProgrammableComputerAtmosLinksComponent>(computerUid);
        if (links.Links.Count >= MaxAtmosLinks)
            return false;

        var targetNet = GetNetEntity(targetUid);
        foreach (var (existingLabel, existingTarget) in links.Links)
        {
            if (existingTarget != targetNet)
                continue;

            assignedLabel = existingLabel;
            return false;
        }

        var baseLabel = GetAtmosAddress(targetUid).Trim();
        if (baseLabel.Length == 0)
            baseLabel = GetAtmosDeviceType(targetUid);

        if (baseLabel.Length > MaxLabelLength)
            baseLabel = baseLabel[..MaxLabelLength];

        if (baseLabel.Length == 0)
            baseLabel = "device";

        var label = baseLabel;
        var idx = 2;
        while (links.Links.ContainsKey(label))
        {
            var suffix = $"-{idx++}";
            var maxBaseLength = MaxLabelLength - suffix.Length;
            if (maxBaseLength <= 0)
                return false;

            var trimmedBase = baseLabel.Length > maxBaseLength
                ? baseLabel[..maxBaseLength]
                : baseLabel;

            label = trimmedBase + suffix;
        }

        links.Links[label] = targetNet;
        assignedLabel = label;
        return true;
    }

    private string GetAtmosDeviceType(EntityUid uid)
    {
        // Checked in priority order; a device may match more than one (e.g. scrubber with monitor)
        if (EntityManager.HasComponent<GasVentScrubberComponent>(uid)) return "scrubber";
        if (EntityManager.HasComponent<GasVentPumpComponent>(uid))     return "vent_pump";
        if (EntityManager.HasComponent<GasOutletInjectorComponent>(uid)) return "outlet_injector";
        if (EntityManager.HasComponent<GasFilterComponent>(uid))       return "filter";
        if (EntityManager.HasComponent<GasMixerComponent>(uid))        return "mixer";
        if (EntityManager.HasComponent<AtmosMonitorComponent>(uid))    return "sensor";
        if (EntityManager.HasComponent<GasPressureRegulatorComponent>(uid)) return "pressure_regulator";
        if (EntityManager.HasComponent<GasValveComponent>(uid))        return "valve";
        if (EntityManager.HasComponent<GasVolumePumpComponent>(uid))   return "volume_pump";
        if (EntityManager.HasComponent<GasPressurePumpComponent>(uid)) return "pressure_pump";
        return "unknown";
    }

    private string GetAtmosAddress(EntityUid uid)
    {
        if (EntityManager.TryGetComponent<DeviceNetworkComponent>(uid, out var net) && net.Address is { } addr)
            return addr;
        return uid.Id.ToString();
    }

    // ─── Lua atmos API registration ─────────────────────────────────────────

    private void RegisterAtmosApi(EntityUid uid, ComputerRuntime runtime, Table hostTable)
    {
        // atmos.list() → { {label, type, address, name}, ... }
        hostTable.Set("atmos_list", DynValue.NewCallback((ctx, args) =>
        {
            if (!TryComp<ProgrammableComputerAtmosLinksComponent>(uid, out var links))
                return DynValue.NewTable(runtime.Script!);

            var table = new Table(runtime.Script!);
            var i = 1;
            foreach (var (label, netEnt) in links.Links)
            {
                var target = GetEntity(netEnt);
                var entry = new Table(runtime.Script!);
                entry["label"] = DynValue.NewString(label);
                entry["type"] = DynValue.NewString(GetAtmosDeviceType(target));
                entry["address"] = DynValue.NewString(GetAtmosAddress(target));
                entry["name"] = DynValue.NewString(ToPrettyString(target));
                entry["linked"] = DynValue.True;
                table[i++] = DynValue.NewTable(entry);
            }
            return DynValue.NewTable(table);
        }, "atmos_list"));

        // atmos_read(label) → {pressure, temperature, total_moles, gases={oxygen=N, ...}}  — sensors only
        hostTable.Set("atmos_read", DynValue.NewCallback((ctx, args) =>
        {
            var label = args.Count > 0 ? ToDynString(args[0]) : null;
            var target = ResolveAtmosLink(uid, label);
            if (target == null)
                throw new ScriptRuntimeException($"atmos.read: no linked device '{label}'");

            if (!TryComp<AtmosMonitorComponent>(target.Value, out var monitor) || monitor.TileGas == null)
                throw new ScriptRuntimeException($"atmos.read: '{label}' is not a sensor or has no gas data");

            var mix = monitor.TileGas;
            var result = new Table(runtime.Script!);
            result["pressure"]    = DynValue.NewNumber(mix.Pressure);
            result["temperature"] = DynValue.NewNumber(mix.Temperature);
            result["total_moles"] = DynValue.NewNumber(mix.TotalMoles);

            var gases = new Table(runtime.Script!);
            foreach (Gas gas in Enum.GetValues<Gas>())
            {
                var moles = mix[(int)gas];
                if (moles > 0f)
                    gases[gas.ToString().ToLowerInvariant()] = DynValue.NewNumber(moles);
            }
            result["gases"] = DynValue.NewTable(gases);
            return DynValue.NewTable(result);
        }, "atmos_read"));

        // atmos_pump_enabled(label, bool)
        hostTable.Set("atmos_pump_enabled", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) throw new ScriptRuntimeException("atmos.pump_enabled(label, bool)");
            var target = ResolveAtmosLinkRequired(uid, ToDynString(args[0]));
            var enabled = IsTruthy(args[1]);

            if (TryComp<GasPressurePumpComponent>(target, out var pp))
                _pressurePump.SetPumpStatus(new Entity<GasPressurePumpComponent>(target, pp), enabled, uid);
            else if (TryComp<GasVolumePumpComponent>(target, out var vp))
                _volumePump.SetEnabled(target, vp, enabled);
            else
                throw new ScriptRuntimeException($"atmos.pump_enabled: '{ToDynString(args[0])}' is not a pump");

            return DynValue.Void;
        }, "atmos_pump_enabled"));

        // atmos_pump_rate(label, rate_L_per_s)  — volume pumps only
        hostTable.Set("atmos_pump_rate", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) throw new ScriptRuntimeException("atmos.pump_rate(label, rate)");
            var target = ResolveAtmosLinkRequired(uid, ToDynString(args[0]));

            if (TryComp<GasVolumePumpComponent>(target, out var vp))
                _volumePump.SetTransferRate(target, vp, (float)args[1].Number);
            else
                throw new ScriptRuntimeException($"atmos.pump_rate: '{ToDynString(args[0])}' is not a volume pump");

            return DynValue.Void;
        }, "atmos_pump_rate"));

        // atmos_pump_pressure(label, kpa)  — pressure pumps only
        hostTable.Set("atmos_pump_pressure", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) throw new ScriptRuntimeException("atmos.pump_pressure(label, kpa)");
            var target = ResolveAtmosLinkRequired(uid, ToDynString(args[0]));

            if (TryComp<GasPressurePumpComponent>(target, out var pp))
                _pressurePump.SetPumpPressure(new Entity<GasPressurePumpComponent>(target, pp), (float)args[1].Number, uid);
            else
                throw new ScriptRuntimeException($"atmos.pump_pressure: '{ToDynString(args[0])}' is not a pressure pump");

            return DynValue.Void;
        }, "atmos_pump_pressure"));

        // atmos_valve(label, bool)  — gas valves
        hostTable.Set("atmos_valve", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) throw new ScriptRuntimeException("atmos.valve(label, bool)");
            var target = ResolveAtmosLinkRequired(uid, ToDynString(args[0]));

            if (TryComp<GasValveComponent>(target, out var valve))
                _gasValve.Set(target, valve, IsTruthy(args[1]));
            else
                throw new ScriptRuntimeException($"atmos.valve: '{ToDynString(args[0])}' is not a valve");

            return DynValue.Void;
        }, "atmos_valve"));

        // atmos_scrubber(label, {enabled?, rate?, filter_gases?, wide_net?})
        hostTable.Set("atmos_scrubber", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) throw new ScriptRuntimeException("atmos.scrubber(label, params)");
            var target = ResolveAtmosLinkRequired(uid, ToDynString(args[0]));

            if (!TryComp<GasVentScrubberComponent>(target, out var sc))
                throw new ScriptRuntimeException($"atmos.scrubber: '{ToDynString(args[0])}' is not a scrubber");

            if (args[1].Type == DataType.Table)
            {
                var tbl = args[1].Table;
                var enabledVal = tbl.Get("enabled");
                if (enabledVal.Type != DataType.Nil)
                    _scrubber.SetEnabled(target, sc, IsTruthy(enabledVal));

                var rateVal = tbl.Get("rate");
                if (rateVal.Type == DataType.Number)
                    _scrubber.SetTransferRate(target, sc, (float)rateVal.Number);

                var wideVal = tbl.Get("wide_net");
                if (wideVal.Type != DataType.Nil)
                    _scrubber.SetWideNet(target, sc, IsTruthy(wideVal));

                var filterVal = tbl.Get("filter_gases");
                if (filterVal.Type == DataType.Table)
                {
                    var gases = new HashSet<Gas>();
                    foreach (var pair in filterVal.Table.Pairs)
                    {
                        var gasName = pair.Value.CastToString();
                        if (gasName != null && Enum.TryParse<Gas>(gasName, true, out var gas))
                            gases.Add(gas);
                    }
                    _scrubber.SetFilterGases(target, sc, gases);
                }
            }

            return DynValue.Void;
        }, "atmos_scrubber"));

        // atmos_scrubber_read(label) → {enabled, rate, wide_net, filter_gases={...}}
        hostTable.Set("atmos_scrubber_read", DynValue.NewCallback((ctx, args) =>
        {
            var label = args.Count > 0 ? ToDynString(args[0]) : null;
            var target = ResolveAtmosLink(uid, label);
            if (target == null)
                throw new ScriptRuntimeException($"atmos.scrubber_read: no linked device '{label}'");

            if (!TryComp<GasVentScrubberComponent>(target.Value, out var sc))
                throw new ScriptRuntimeException($"atmos.scrubber_read: '{label}' is not a scrubber");

            var result = new Table(runtime.Script!);
            result["enabled"]   = DynValue.NewBoolean(sc.Enabled);
            result["rate"]      = DynValue.NewNumber(sc.TransferRate);
            result["wide_net"]  = DynValue.NewBoolean(sc.WideNet);

            var filterTbl = new Table(runtime.Script!);
            var fi = 1;
            foreach (var gas in sc.FilterGases)
                filterTbl[fi++] = DynValue.NewString(gas.ToString().ToLowerInvariant());
            result["filter_gases"] = DynValue.NewTable(filterTbl);
            return DynValue.NewTable(result);
        }, "atmos_scrubber_read"));

        // atmos_filter(label, {enabled?, rate?, filter_gases?})
        hostTable.Set("atmos_filter", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) throw new ScriptRuntimeException("atmos.filter(label, params)");
            var target = ResolveAtmosLinkRequired(uid, ToDynString(args[0]));

            if (!TryComp<GasFilterComponent>(target, out var filter))
                throw new ScriptRuntimeException($"atmos.filter: '{ToDynString(args[0])}' is not a filter");

            if (args[1].Type == DataType.Table)
            {
                var tbl = args[1].Table;

                var enabledVal = tbl.Get("enabled");
                if (enabledVal.Type != DataType.Nil)
                    filter.Enabled = IsTruthy(enabledVal);

                var rateVal = tbl.Get("rate");
                if (rateVal.Type == DataType.Number)
                    filter.TransferRate = Math.Clamp((float) rateVal.Number, 0f, filter.MaxTransferRate);

                var filterVal = tbl.Get("filter_gases");
                if (filterVal.Type == DataType.Table)
                {
                    var gases = new HashSet<Gas>();
                    foreach (var pair in filterVal.Table.Pairs)
                    {
                        var gasName = pair.Value.CastToString();
                        if (gasName != null && Enum.TryParse<Gas>(gasName, true, out var gas))
                            gases.Add(gas);
                    }

                    filter.FilterGases = gases;
                    filter.FilteredGas = null;
                    foreach (var gas in gases)
                    {
                        filter.FilteredGas = gas;
                        break;
                    }
                }

                Dirty(target, filter);
            }

            return DynValue.Void;
        }, "atmos_filter"));

        // atmos_filter_read(label) → {enabled, rate, max_rate, filter_gases={...}}
        hostTable.Set("atmos_filter_read", DynValue.NewCallback((ctx, args) =>
        {
            var label = args.Count > 0 ? ToDynString(args[0]) : null;
            var target = ResolveAtmosLink(uid, label);
            if (target == null)
                throw new ScriptRuntimeException($"atmos.filter_read: no linked device '{label}'");

            if (!TryComp<GasFilterComponent>(target.Value, out var filter))
                throw new ScriptRuntimeException($"atmos.filter_read: '{label}' is not a filter");

            var result = new Table(runtime.Script!);
            result["enabled"] = DynValue.NewBoolean(filter.Enabled);
            result["rate"] = DynValue.NewNumber(filter.TransferRate);
            result["max_rate"] = DynValue.NewNumber(filter.MaxTransferRate);

            var filterTbl = new Table(runtime.Script!);
            var fi = 1;
            foreach (var gas in filter.FilterGases)
                filterTbl[fi++] = DynValue.NewString(gas.ToString().ToLowerInvariant());
            result["filter_gases"] = DynValue.NewTable(filterTbl);

            return DynValue.NewTable(result);
        }, "atmos_filter_read"));

        // atmos_vent(label, {enabled?, mode?, pressure_checks?, external_pressure?, internal_pressure?, lockout_override?})
        hostTable.Set("atmos_vent", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) throw new ScriptRuntimeException("atmos.vent(label, params)");
            var target = ResolveAtmosLinkRequired(uid, ToDynString(args[0]));

            if (!TryComp<GasVentPumpComponent>(target, out var vent))
                throw new ScriptRuntimeException($"atmos.vent: '{ToDynString(args[0])}' is not a vent pump");

            if (args[1].Type == DataType.Table)
            {
                var tbl = args[1].Table;

                var enabledVal = tbl.Get("enabled");
                if (enabledVal.Type != DataType.Nil)
                    vent.Enabled = IsTruthy(enabledVal);

                var modeVal = tbl.Get("mode");
                if (modeVal.Type == DataType.String)
                {
                    var mode = modeVal.String.ToLowerInvariant();
                    vent.PumpDirection = mode switch
                    {
                        "release" or "releasing" or "out" => VentPumpDirection.Releasing,
                        "siphon" or "siphoning" or "in" => VentPumpDirection.Siphoning,
                        _ => throw new ScriptRuntimeException("atmos.vent mode must be 'release' or 'siphon'"),
                    };
                }

                var checksVal = tbl.Get("pressure_checks");
                if (checksVal.Type == DataType.String)
                {
                    var checks = checksVal.String.ToLowerInvariant();
                    vent.PressureChecks = checks switch
                    {
                        "none" => VentPressureBound.NoBound,
                        "internal" => VentPressureBound.InternalBound,
                        "external" => VentPressureBound.ExternalBound,
                        "both" => VentPressureBound.Both,
                        _ => throw new ScriptRuntimeException("atmos.vent pressure_checks must be 'none', 'internal', 'external', or 'both'"),
                    };
                }

                var extVal = tbl.Get("external_pressure");
                if (extVal.Type == DataType.Number)
                    vent.ExternalPressureBound = (float) extVal.Number;

                var intVal = tbl.Get("internal_pressure");
                if (intVal.Type == DataType.Number)
                    vent.InternalPressureBound = (float) intVal.Number;

                var lockoutVal = tbl.Get("lockout_override");
                if (lockoutVal.Type != DataType.Nil)
                    vent.PressureLockoutOverride = IsTruthy(lockoutVal);

                Dirty(target, vent);
            }

            return DynValue.Void;
        }, "atmos_vent"));

        // atmos_vent_read(label) -> {enabled, mode, pressure_checks, external_pressure, internal_pressure, max_pressure, lockout_override}
        hostTable.Set("atmos_vent_read", DynValue.NewCallback((ctx, args) =>
        {
            var label = args.Count > 0 ? ToDynString(args[0]) : null;
            var target = ResolveAtmosLink(uid, label);
            if (target == null)
                throw new ScriptRuntimeException($"atmos.vent_read: no linked device '{label}'");

            if (!TryComp<GasVentPumpComponent>(target.Value, out var vent))
                throw new ScriptRuntimeException($"atmos.vent_read: '{label}' is not a vent pump");

            var result = new Table(runtime.Script!);
            result["enabled"] = DynValue.NewBoolean(vent.Enabled);
            result["type"] = DynValue.NewString("vent_pump");
            result["mode"] = DynValue.NewString(vent.PumpDirection == VentPumpDirection.Releasing ? "release" : "siphon");
            result["pressure_checks"] = DynValue.NewString(vent.PressureChecks switch
            {
                VentPressureBound.NoBound => "none",
                VentPressureBound.InternalBound => "internal",
                VentPressureBound.ExternalBound => "external",
                _ => "both",
            });
            result["external_pressure"] = DynValue.NewNumber(vent.ExternalPressureBound);
            result["internal_pressure"] = DynValue.NewNumber(vent.InternalPressureBound);
            result["max_pressure"] = DynValue.NewNumber(vent.MaxPressure);
            result["lockout_override"] = DynValue.NewBoolean(vent.PressureLockoutOverride);
            return DynValue.NewTable(result);
        }, "atmos_vent_read"));

        // atmos_injector(label, {enabled?, rate?})
        hostTable.Set("atmos_injector", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) throw new ScriptRuntimeException("atmos.injector(label, params)");
            var target = ResolveAtmosLinkRequired(uid, ToDynString(args[0]));

            if (!TryComp<GasOutletInjectorComponent>(target, out var injector))
                throw new ScriptRuntimeException($"atmos.injector: '{ToDynString(args[0])}' is not an outlet injector");

            if (args[1].Type == DataType.Table)
            {
                var tbl = args[1].Table;

                var enabledVal = tbl.Get("enabled");
                if (enabledVal.Type != DataType.Nil)
                    _injector.SetEnabled(target, injector, IsTruthy(enabledVal));

                var rateVal = tbl.Get("rate");
                if (rateVal.Type == DataType.Number)
                    _injector.SetTransferRate(target, injector, (float) rateVal.Number);
            }

            return DynValue.Void;
        }, "atmos_injector"));

        // atmos_injector_read(label) -> {enabled, rate, max_rate, max_pressure}
        hostTable.Set("atmos_injector_read", DynValue.NewCallback((ctx, args) =>
        {
            var label = args.Count > 0 ? ToDynString(args[0]) : null;
            var target = ResolveAtmosLink(uid, label);
            if (target == null)
                throw new ScriptRuntimeException($"atmos.injector_read: no linked device '{label}'");

            if (!TryComp<GasOutletInjectorComponent>(target.Value, out var injector))
                throw new ScriptRuntimeException($"atmos.injector_read: '{label}' is not an outlet injector");

            var result = new Table(runtime.Script!);
            result["enabled"] = DynValue.NewBoolean(injector.Enabled);
            result["type"] = DynValue.NewString("outlet_injector");
            result["rate"] = DynValue.NewNumber(injector.TransferRate);
            result["max_rate"] = DynValue.NewNumber(injector.MaxTransferRate);
            result["max_pressure"] = DynValue.NewNumber(injector.MaxPressure);
            return DynValue.NewTable(result);
        }, "atmos_injector_read"));

        // atmos_mixer(label, {enabled?, target_pressure?, inlet_one?, inlet_two?})
        hostTable.Set("atmos_mixer", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) throw new ScriptRuntimeException("atmos.mixer(label, params)");
            var target = ResolveAtmosLinkRequired(uid, ToDynString(args[0]));

            if (!TryComp<GasMixerComponent>(target, out var mixer))
                throw new ScriptRuntimeException($"atmos.mixer: '{ToDynString(args[0])}' is not a mixer");

            if (args[1].Type == DataType.Table)
            {
                var tbl = args[1].Table;

                var enabledVal = tbl.Get("enabled");
                if (enabledVal.Type != DataType.Nil)
                    _mixer.SetEnabled(target, mixer, IsTruthy(enabledVal));

                var pressureVal = tbl.Get("target_pressure");
                if (pressureVal.Type == DataType.Number)
                    _mixer.SetTargetPressure(target, mixer, (float) pressureVal.Number);

                var oneVal = tbl.Get("inlet_one");
                var twoVal = tbl.Get("inlet_two");
                if (oneVal.Type == DataType.Number || twoVal.Type == DataType.Number)
                {
                    var inletOne = oneVal.Type == DataType.Number
                        ? (float) oneVal.Number
                        : 1f - mixer.InletTwoConcentration;

                    var inletTwo = twoVal.Type == DataType.Number
                        ? (float) twoVal.Number
                        : 1f - inletOne;

                    var sum = inletOne + inletTwo;
                    if (sum <= 0f)
                    {
                        inletOne = 0.5f;
                        inletTwo = 0.5f;
                    }
                    else
                    {
                        inletOne /= sum;
                        inletTwo /= sum;
                    }

                    _mixer.SetInletRatio(target, mixer, inletOne, inletTwo);
                }
            }

            return DynValue.Void;
        }, "atmos_mixer"));

        // atmos_mixer_read(label) -> {enabled, target_pressure, max_target_pressure, inlet_one, inlet_two}
        hostTable.Set("atmos_mixer_read", DynValue.NewCallback((ctx, args) =>
        {
            var label = args.Count > 0 ? ToDynString(args[0]) : null;
            var target = ResolveAtmosLink(uid, label);
            if (target == null)
                throw new ScriptRuntimeException($"atmos.mixer_read: no linked device '{label}'");

            if (!TryComp<GasMixerComponent>(target.Value, out var mixer))
                throw new ScriptRuntimeException($"atmos.mixer_read: '{label}' is not a mixer");

            var result = new Table(runtime.Script!);
            result["enabled"] = DynValue.NewBoolean(mixer.Enabled);
            result["type"] = DynValue.NewString("mixer");
            result["target_pressure"] = DynValue.NewNumber(mixer.TargetPressure);
            result["max_target_pressure"] = DynValue.NewNumber(mixer.MaxTargetPressure);
            result["inlet_one"] = DynValue.NewNumber(mixer.InletOneConcentration);
            result["inlet_two"] = DynValue.NewNumber(mixer.InletTwoConcentration);
            return DynValue.NewTable(result);
        }, "atmos_mixer_read"));

        // atmos_regulator(label, {threshold?})
        hostTable.Set("atmos_regulator", DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) throw new ScriptRuntimeException("atmos.regulator(label, params)");
            var target = ResolveAtmosLinkRequired(uid, ToDynString(args[0]));

            if (!TryComp<GasPressureRegulatorComponent>(target, out var regulator))
                throw new ScriptRuntimeException($"atmos.regulator: '{ToDynString(args[0])}' is not a pressure regulator");

            if (args[1].Type == DataType.Table)
            {
                var tbl = args[1].Table;
                var thresholdVal = tbl.Get("threshold");
                if (thresholdVal.Type == DataType.Number)
                    regulator.Threshold = Math.Clamp((float) thresholdVal.Number, 0f, Atmospherics.MaxOutputPressure);

                Dirty(target, regulator);
            }

            return DynValue.Void;
        }, "atmos_regulator"));

        // atmos_regulator_read(label) -> {threshold, max_transfer_rate, enabled, flow_rate, inlet_pressure, outlet_pressure}
        hostTable.Set("atmos_regulator_read", DynValue.NewCallback((ctx, args) =>
        {
            var label = args.Count > 0 ? ToDynString(args[0]) : null;
            var target = ResolveAtmosLink(uid, label);
            if (target == null)
                throw new ScriptRuntimeException($"atmos.regulator_read: no linked device '{label}'");

            if (!TryComp<GasPressureRegulatorComponent>(target.Value, out var regulator))
                throw new ScriptRuntimeException($"atmos.regulator_read: '{label}' is not a pressure regulator");

            var result = new Table(runtime.Script!);
            result["type"] = DynValue.NewString("pressure_regulator");
            result["threshold"] = DynValue.NewNumber(regulator.Threshold);
            result["max_transfer_rate"] = DynValue.NewNumber(regulator.MaxTransferRate);
            result["enabled"] = DynValue.NewBoolean(regulator.Enabled);
            result["flow_rate"] = DynValue.NewNumber(regulator.FlowRate);
            result["inlet_pressure"] = DynValue.NewNumber(regulator.InletPressure);
            result["outlet_pressure"] = DynValue.NewNumber(regulator.OutletPressure);
            return DynValue.NewTable(result);
        }, "atmos_regulator_read"));

        // atmos_pump_read(label) → {enabled, type, transfer_rate?, target_pressure?, max_transfer_rate?, max_target_pressure?}
        hostTable.Set("atmos_pump_read", DynValue.NewCallback((ctx, args) =>
        {
            var label = args.Count > 0 ? ToDynString(args[0]) : null;
            var target = ResolveAtmosLink(uid, label);
            if (target == null)
                throw new ScriptRuntimeException($"atmos.pump_read: no linked device '{label}'");

            var result = new Table(runtime.Script!);

            if (TryComp<GasPressurePumpComponent>(target.Value, out var pp))
            {
                result["enabled"] = DynValue.NewBoolean(pp.Enabled);
                result["type"] = DynValue.NewString("pressure_pump");
                result["target_pressure"] = DynValue.NewNumber(pp.TargetPressure);
                result["max_target_pressure"] = DynValue.NewNumber(pp.MaxTargetPressure);
                return DynValue.NewTable(result);
            }

            if (TryComp<GasVolumePumpComponent>(target.Value, out var vp))
            {
                result["enabled"] = DynValue.NewBoolean(vp.Enabled);
                result["type"] = DynValue.NewString("volume_pump");
                result["transfer_rate"] = DynValue.NewNumber(vp.TransferRate);
                result["max_transfer_rate"] = DynValue.NewNumber(vp.MaxTransferRate);
                result["overclocked"] = DynValue.NewBoolean(vp.Overclocked);
                return DynValue.NewTable(result);
            }

            throw new ScriptRuntimeException($"atmos.pump_read: '{label}' is not a pump");
        }, "atmos_pump_read"));
    }

    // ─── Link resolution helpers ─────────────────────────────────────────────

    private EntityUid? ResolveAtmosLink(EntityUid computer, string? label)
    {
        if (label == null) return null;
        if (!TryComp<ProgrammableComputerAtmosLinksComponent>(computer, out var links))
            return null;
        if (!links.Links.TryGetValue(label, out var netEnt))
            return null;
        var target = GetEntity(netEnt);
        return Exists(target) ? target : null;
    }

    private EntityUid ResolveAtmosLinkRequired(EntityUid computer, string label)
        => ResolveAtmosLink(computer, label) ?? throw new ScriptRuntimeException($"atmos: no linked device '{label}'");
}
